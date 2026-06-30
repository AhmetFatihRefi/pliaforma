using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Application.Features.Studios;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>Sprint 43: yeni hedef ders açarak taşıma için ders bilgisi (CreateClassSession ile aynı alanlar).</summary>
public sealed record CreateTargetSessionInput(
    Guid InstructorProfileId,
    Guid ClassTypeId,
    DateTime StartAt,
    int? Capacity);

/// <summary>
/// Sprint 43 — Öğrenci taşıma: kaynak dersten seçili Booked rezervasyonları çıkarıp hedef derse taşır.
/// Hedef ya var olan bir ders (<see cref="TargetSessionId"/>) ya da yeni açılan bir boşluktur
/// (<see cref="CreateTargetSession"/>) — tam olarak biri verilir (XOR). Dersin TAMAMI ertelenmez;
/// kaynakta kalan öğrenciler etkilenmez. Yalnız Owner/StudioAdmin; yalnız Regular booking.
/// </summary>
public sealed record MoveBookingsCommand(
    Guid SourceSessionId,
    IReadOnlyList<Guid> BookingIds,
    Guid? TargetSessionId = null,
    CreateTargetSessionInput? CreateTargetSession = null,
    // Sprint 42 uyumu: yalnız "geçmiş hedef seansa rezervasyon" engelini aşar (Owner/StudioAdmin + UI onayı).
    bool AllowPastBooking = false) : ICommand<MoveBookingsResultDto>, IStudioWriteRequest;

/// <summary>Tek bir taşınan rezervasyonun sonucu (kaynak booking iptal → hedef booking oluştu).</summary>
public sealed record MovedBookingDto(
    Guid SourceBookingId,
    Guid TargetBookingId,
    Guid StudentProfileId,
    string StudentName);

public sealed record MoveBookingsResultDto(
    Guid TargetSessionId,
    bool TargetSessionCreated,
    int MovedCount,
    IReadOnlyList<MovedBookingDto> Moved);

public sealed class MoveBookingsValidator : AbstractValidator<MoveBookingsCommand>
{
    public MoveBookingsValidator()
    {
        RuleFor(x => x.SourceSessionId).NotEmpty();
        RuleFor(x => x.BookingIds).NotEmpty().WithMessage("Taşınacak en az bir rezervasyon seçilmelidir.");
        // Hedef: targetSessionId VEYA createTargetSession'dan TAM OLARAK biri (XOR).
        RuleFor(x => x)
            .Must(x => (x.TargetSessionId is not null) ^ (x.CreateTargetSession is not null))
            .WithMessage("targetSessionId veya createTargetSession'dan tam olarak biri belirtilmelidir.");

        When(x => x.CreateTargetSession is not null, () =>
        {
            RuleFor(x => x.CreateTargetSession!.InstructorProfileId).NotEmpty();
            RuleFor(x => x.CreateTargetSession!.ClassTypeId).NotEmpty();
            RuleFor(x => x.CreateTargetSession!.StartAt).NotEmpty();
            RuleFor(x => x.CreateTargetSession!.Capacity)
                .GreaterThanOrEqualTo(1).When(x => x.CreateTargetSession!.Capacity.HasValue);
        });
    }
}

public sealed class MoveBookingsHandler : ICommandHandler<MoveBookingsCommand, MoveBookingsResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly BookingCreator _creator;
    private readonly IClassSessionCapacityService _capacity;
    private readonly EquipmentCapacityGuard _equipmentGuard;
    private readonly ISecurityAuditLogger _audit;

    public MoveBookingsHandler(
        IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, BookingCreator creator,
        IClassSessionCapacityService capacity, EquipmentCapacityGuard equipmentGuard, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _creator = creator;
        _capacity = capacity;
        _equipmentGuard = equipmentGuard;
        _audit = audit;
    }

    public async Task<MoveBookingsResultDto> Handle(MoveBookingsCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole;

        // v1: yalnız Owner/StudioAdmin (controller da kısıtlar; handler savunma amaçlı tekrar doğrular).
        if (role is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Öğrenci taşıma yetkisi yalnızca Owner/StudioAdmin'dedir.");

        // ---- KAYNAK DERS ----
        var source = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SourceSessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Kaynak ders seansı", request.SourceSessionId);

        if (source.Status != ClassSessionStatus.Scheduled)
            throw new ConflictException("Yalnızca planlı (Scheduled) bir dersten öğrenci taşınabilir.");

        // Sprint 43: geçmiş kaynak kapalı (v1 kararı).
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        if (source.StartAt <= nowLocal)
            throw new ConflictException("Geçmiş bir dersten öğrenci taşınamaz.");

        // Sprint 43: yalnız Regular kaynak (Makeup/Trial/Special taşıma kapsam dışı).
        if (source.SessionKind != ClassSessionKind.Regular)
            throw new ConflictException("Yalnızca normal (Regular) derslerden öğrenci taşınabilir; telafi dersi taşıma bu sürümde desteklenmiyor.");

        var bookingIds = request.BookingIds.Distinct().ToList();
        var bookings = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == source.Id && bookingIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        if (bookings.Count != bookingIds.Count)
            throw new ConflictException("Seçili rezervasyonlardan bazıları bu derse ait değil.");
        if (bookings.Any(b => b.Status != ClassBookingStatus.Booked))
            throw new ConflictException("Yalnızca aktif (Booked) rezervasyonlar taşınabilir.");

        // Yoklaması alınmış rezervasyon taşınamaz.
        var hasAttendance = await _db.Attendances
            .AnyAsync(a => a.StudioId == studioId && bookingIds.Contains(a.ClassBookingId), cancellationToken);
        if (hasAttendance)
            throw new ConflictException("Yoklaması alınmış rezervasyon taşınamaz.");

        // ---- HEDEF DERS ----
        ClassSession target;
        bool targetCreated;
        if (request.TargetSessionId is { } targetId)
        {
            target = await _db.ClassSessions
                .FirstOrDefaultAsync(s => s.Id == targetId && s.StudioId == studioId, cancellationToken)
                ?? throw new NotFoundException("Hedef ders seansı", targetId);
            if (target.Id == source.Id)
                throw new ConflictException("Hedef ders kaynak ders ile aynı olamaz.");
            if (target.Status != ClassSessionStatus.Scheduled)
                throw new ConflictException("Hedef ders planlı (Scheduled) olmalı.");
            // Sprint 43: yalnız Regular hedefe taşıma (Makeup hedef telafi-kredi tüketir → kapsam dışı).
            if (target.SessionKind != ClassSessionKind.Regular)
                throw new ConflictException("Hedef ders normal (Regular) bir ders olmalı.");
            targetCreated = false;
        }
        else
        {
            target = await CreateTargetSessionAsync(studioId, userId, request.CreateTargetSession!, cancellationToken);
            targetCreated = true;
        }

        // ---- TAŞIMA (her booking: kaynaktan çıkar → persist → hedefe ekle) ----
        var moved = new List<MovedBookingDto>(bookings.Count);
        foreach (var booking in bookings)
        {
            var student = await _db.StudentProfiles
                .FirstOrDefaultAsync(s => s.Id == booking.StudentProfileId && s.StudioId == studioId, cancellationToken)
                ?? throw new NotFoundException("Öğrenci", booking.StudentProfileId);
            if (student.Status == StudentStatus.Inactive)
                throw new BusinessRuleException("Pasif öğrenci taşınamaz.");

            // Aynı line/header hedef booking'e aynen taşınır → ders hakkı net etki olarak nötr.
            var lineId = booking.StudentContractLineId;
            var contractId = booking.StudentContractId;

            // 1) Kaynaktan çıkar — TAŞIMA: telafi kredisi restore/grant edilmez (CancelBooking yan etkileri YOK).
            booking.Status = ClassBookingStatus.CancelledByStudio;
            booking.CancelledByUserId = userId;
            booking.CancelledAt = _clock.UtcNow;
            booking.CancellationReason = "Taşındı";
            await _capacity.ReleaseSeatAsync(studioId, source.Id, cancellationToken);

            // 2) Persist — ders hakkı/sayım sorguları (BookingCreator) güncel durumu görsün diye kritik.
            await _db.SaveChangesAsync(cancellationToken);

            // 3) Hedefe ekle — mevcut Regular booking motoru tüm hedef guard'larını uygular.
            var newBooking = await _creator.CreateAsync(
                studioId, target, student, userId, role.Value, cancellationToken,
                studentContractId: contractId, studentContractLineId: lineId, allowPast: request.AllowPastBooking);

            moved.Add(new MovedBookingDto(booking.Id, newBooking.Id, student.Id, student.FullName));
        }

        // ---- AUDIT (taşıma başına tek aggregate; PII yok) ----
        await _audit.LogAsync(SecurityAuditEventType.BookingMoved, SecurityAuditOutcome.Success,
            actorUserId: userId, studioId: studioId,
            targetType: "ClassSession", targetId: target.Id,
            detail: $"source={source.Id};target={target.Id};count={moved.Count}", cancellationToken: cancellationToken);

        return new MoveBookingsResultDto(target.Id, targetCreated, moved.Count, moved);
    }

    /// <summary>
    /// Mod B: yeni hedef dersi inline oluşturur (CreateClassSession ile aynı guard'lar). Sprint 43 kararı:
    /// yeni hedef boşluk normalde gelecek/bugün içindir → <c>allowPast=false</c> (geçmişe otomatik girilmez).
    /// </summary>
    private async Task<ClassSession> CreateTargetSessionAsync(
        Guid studioId, Guid userId, CreateTargetSessionInput input, CancellationToken ct)
    {
        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, input.InstructorProfileId, ct);
        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == input.ClassTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders tipi", input.ClassTypeId);

        var startAt = DateTime.SpecifyKind(input.StartAt, DateTimeKind.Unspecified);
        var endAt = startAt.AddMinutes(classType.DefaultDurationMinutes);
        var capacity = input.Capacity ?? 9999;
        if (capacity < 1) capacity = 1;

        await SessionRules.EnsureValidAsync(_db, _clock, studioId, profile.Id, startAt, endAt, excludeId: null, ct, allowPast: false);

        var workingHours = await StudioWorkingHoursService.LoadAsync(_db, studioId, ct);
        StudioWorkingHoursService.EnsureWithin(workingHours, startAt, endAt);

        await _equipmentGuard.EnsureCanReserveAsync(studioId, classType, startAt, endAt, capacity, excludeSessionId: null, pendingOverlapCapacity: 0, ct);

        var entity = new ClassSession
        {
            StudioId = studioId,
            InstructorProfileId = profile.Id,
            ClassTypeId = classType.Id,
            StartAt = startAt,
            EndAt = endAt,
            Capacity = capacity,
            SessionKind = ClassSessionKind.Regular,
            CreatedByUserId = userId,
        };

        _db.ClassSessions.Add(entity);
        await _db.SaveChangesAsync(ct); // hedef session Id'si gerekli (aynı transaction içinde).
        return entity;
    }
}
