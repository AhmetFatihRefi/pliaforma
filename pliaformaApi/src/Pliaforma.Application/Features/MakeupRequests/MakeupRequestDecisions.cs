using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Bookings;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupRequests;

// =====================================================================================================
// WITHDRAW — talebi açan (kendi pending'i) veya manager geri çeker. Slot boşalır, rezerve serbest, ledger DOKUNULMAZ.
// =====================================================================================================
public sealed record WithdrawMakeupRequestCommand(Guid Id) : ICommand<MakeupRequestDto>, IStudioWriteRequest;

public sealed class WithdrawMakeupRequestHandler : ICommandHandler<WithdrawMakeupRequestCommand, MakeupRequestDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public WithdrawMakeupRequestHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MakeupRequestDto> Handle(WithdrawMakeupRequestCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var entity = await _db.MakeupRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Telafi talebi", request.Id);

        if (entity.Status != MakeupRequestStatus.Pending)
            throw new ConflictException("Yalnızca onay bekleyen (Pending) talep geri çekilebilir.");

        // Instructor yalnız KENDİ açtığı talebi geri çekebilir; manager herhangi birini.
        if (_currentUser.StudioRole == StudioRole.Instructor && entity.RequestedByUserId != userId)
            throw new ForbiddenException("Yalnızca kendi açtığınız telafi talebini geri çekebilirsiniz.");

        entity.Status = MakeupRequestStatus.Withdrawn;
        entity.DecisionByUserId = userId;
        entity.DecidedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await MakeupRequestView.BuildAsync(_db, studioId, new[] { entity }, cancellationToken);
        return dtos[0];
    }
}

// =====================================================================================================
// REJECT — Owner/StudioAdmin reddeder. Slot boşalır, rezerve serbest, ledger DOKUNULMAZ. Kayıt AUDIT için saklanır.
// =====================================================================================================
public sealed record RejectMakeupRequestCommand(Guid Id, string? DecisionNote = null) : ICommand<MakeupRequestDto>, IStudioWriteRequest;

public sealed class RejectMakeupRequestValidator : AbstractValidator<RejectMakeupRequestCommand>
{
    public RejectMakeupRequestValidator() => RuleFor(x => x.DecisionNote).MaximumLength(1000);
}

public sealed class RejectMakeupRequestHandler : ICommandHandler<RejectMakeupRequestCommand, MakeupRequestDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public RejectMakeupRequestHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MakeupRequestDto> Handle(RejectMakeupRequestCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var entity = await _db.MakeupRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Telafi talebi", request.Id);

        if (entity.Status != MakeupRequestStatus.Pending)
            throw new ConflictException("Yalnızca onay bekleyen (Pending) talep reddedilebilir.");

        entity.Status = MakeupRequestStatus.Rejected;
        entity.DecisionByUserId = userId;
        entity.DecidedAt = _clock.UtcNow;
        entity.DecisionNote = request.DecisionNote?.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await MakeupRequestView.BuildAsync(_db, studioId, new[] { entity }, cancellationToken);
        return dtos[0];
    }
}

// =====================================================================================================
// APPROVE (+ edit-approve) — Owner/StudioAdmin onaylar. Final değerlerle (gerekirse tarih/saat/hoca/ders tipi
// değiştirilerek) çakışma YENİDEN doğrulanır; talep önce Approved'a çevrilir (flip-first → kendini rezerve bloklamaz),
// sonra gerçek Makeup ClassSession (IsIndividual=true) + ClassBooking materyalize edilir. Kredi BookingCreator makeup
// yolundan Use(−1) ile GERÇEKTEN tüketilir. Tümü tek transaction (IStudioWriteRequest) — hata = tam rollback.
// =====================================================================================================
public sealed record ApproveMakeupRequestCommand(
    Guid Id,
    DateTime? StartAt = null,
    Guid? RequestedInstructorProfileId = null,
    Guid? ClassTypeId = null) : ICommand<MakeupRequestDto>, IStudioWriteRequest;

public sealed class ApproveMakeupRequestHandler : ICommandHandler<ApproveMakeupRequestCommand, MakeupRequestDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly BookingCreator _bookingCreator;
    private readonly EquipmentCapacityGuard _equipmentGuard;

    public ApproveMakeupRequestHandler(
        IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, BookingCreator bookingCreator, EquipmentCapacityGuard equipmentGuard)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _bookingCreator = bookingCreator;
        _equipmentGuard = equipmentGuard;
    }

    public async Task<MakeupRequestDto> Handle(ApproveMakeupRequestCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole ?? StudioRole.Owner;

        var entity = await _db.MakeupRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Telafi talebi", request.Id);

        if (entity.Status != MakeupRequestStatus.Pending)
            throw new ConflictException("Yalnızca onay bekleyen (Pending) talep onaylanabilir.");

        // ---- Sprint 28C-2: talep LINE'a bağlı → bireysellik/kredi line'dan. ----
        if (entity.StudentContractLineId is not { } lineId)
            throw new BusinessRuleException("Talep bir sözleşme satırına bağlı değil (eski kayıt); line-bazlı onay yapılamaz.");
        var line = await _db.StudentContractLines
            .FirstOrDefaultAsync(l => l.Id == lineId && l.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Sözleşme satırı", lineId);

        // ---- Final değerler (edit-approve: tarih/saat/hoca/ders tipi değiştirilebilir). ----
        var instructorProfileId = request.RequestedInstructorProfileId ?? entity.RequestedInstructorProfileId;
        var classTypeId = request.ClassTypeId ?? entity.ClassTypeId;
        var startAt = DateTime.SpecifyKind(request.StartAt ?? entity.StartAt, DateTimeKind.Unspecified);

        var instructor = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, instructorProfileId, cancellationToken);
        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == classTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", classTypeId);
        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == entity.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", entity.StudentProfileId);

        var endAt = startAt.AddMinutes(classType.DefaultDurationMinutes);

        // ---- R5: geçmişe approve YASAK (final saat geçmişe düşemez). ----
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        if (startAt <= nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana telafi dersi onaylanamaz.");

        // ---- Çakışma YENİDEN (final değerlerle): gerçek (hoca+öğrenci) + pending (bu talep HARİÇ). ----
        if (await SessionRules.HasConflictAsync(_db, studioId, instructor.Id, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında başka bir derse atanmış.");
        if (await SessionRules.StudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, System.Array.Empty<Guid>(), cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");
        if (await SessionRules.PendingInstructorConflictAsync(_db, studioId, instructor.Id, startAt, endAt, entity.Id, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında onay bekleyen başka bir telafi talebine atanmış.");
        if (await SessionRules.PendingStudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, entity.Id, cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında onay bekleyen başka bir telafi talebi var.");

        // ---- Sprint 32B: Makeup seansı kapasitesi. Makeup tek öğrenci içindir → ekipman-bağlı ders tipinde
        // Capacity=1 (1 ekipman kapasitesi rezerve; sınırsız 9999 YASAK). Ekipmansız grupta eski davranış (9999). ----
        var capacity = (line.IsIndividual || classType.EquipmentId is not null) ? 1 : EquipmentCapacityRules.UnlimitedCapacity;

        // Ekipman-bağlıysa guard: aktif/≤toplam/≤available + havuz FOR UPDATE kilidi. Dolu aralık → 409 (yeni seans, exclude yok).
        await _equipmentGuard.EnsureCanReserveAsync(studioId, classType, startAt, endAt, capacity, excludeSessionId: null, pendingOverlapCapacity: 0, cancellationToken);

        // ---- flip-first: talep artık Pending DEĞİL → kullanılabilir-bakiye hesabında kendini rezerve bloklamaz. ----
        entity.Status = MakeupRequestStatus.Approved;
        entity.DecisionByUserId = userId;
        entity.DecidedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // ---- Sprint 28C-2: Makeup seansı line.IsIndividual'a göre (group line → group makeup); kredi o line'dan Use(−1). ----
        var session = new ClassSession
        {
            StudioId = studioId,
            InstructorProfileId = instructor.Id,
            ClassTypeId = classType.Id,
            StartAt = startAt,
            EndAt = endAt,
            Capacity = capacity,
            IsIndividual = line.IsIndividual,
            SessionKind = ClassSessionKind.Makeup,
            Notes = entity.Description,
            CreatedByUserId = userId,
        };
        _db.ClassSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        var booking = await _bookingCreator.CreateAsync(studioId, session, student, userId, role, cancellationToken, studentContractLineId: line.Id);

        entity.ResultingClassSessionId = session.Id;
        entity.ResultingBookingId = booking.Id;
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await MakeupRequestView.BuildAsync(_db, studioId, new[] { entity }, cancellationToken);
        return dtos[0];
    }
}
