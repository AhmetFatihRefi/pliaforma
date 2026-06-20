using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupRequests;

/// <summary>
/// Telafi dersi talebi oluşturur (Sprint 27). Instructor YALNIZ kendi instructor profiliyle ve kendi AKTİF
/// SÖZLEŞMELİ öğrencisi için açabilir (RequestedInstructorProfileId body'den değil token'dan ZORLANIR). Owner/
/// StudioAdmin herhangi öğrenci/hoca kombinasyonunu seçebilir (sözleşme şartı YOK) ama yine conflict + kredi
/// (kullanılabilir bakiye) kontrolünden geçer. HARD: geçmişe talep yasak (StartAt &gt; studioNow); gerçek+pending
/// hoca/öğrenci çakışması; kullanılabilir telafi hakkı &gt; 0 (örtük rezerve = aktif Pending talep sayısı).
/// </summary>
public sealed record CreateMakeupRequestCommand(
    Guid StudentProfileId,
    Guid ClassTypeId,
    DateTime StartAt,
    string? Description = null,
    Guid? RequestedInstructorProfileId = null) : ICommand<MakeupRequestDto>, IStudioWriteRequest;

public sealed class CreateMakeupRequestValidator : AbstractValidator<CreateMakeupRequestCommand>
{
    public CreateMakeupRequestValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateMakeupRequestHandler : ICommandHandler<CreateMakeupRequestCommand, MakeupRequestDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    private readonly IMakeupCreditLock _creditLock;

    public CreateMakeupRequestHandler(
        IAppDbContext db, ICurrentUserService currentUser, IDateTime clock,
        MakeupCreditService credits, IMakeupCreditLock creditLock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _credits = credits;
        _creditLock = creditLock;
    }

    public async Task<MakeupRequestDto> Handle(CreateMakeupRequestCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole;
        var isInstructor = role == StudioRole.Instructor;

        // ---- Hoca profili: Instructor → token'dan ZORLANIR (başka hoca adına talep YASAK). Manager → seçer. ----
        Guid instructorProfileId;
        if (isInstructor)
        {
            var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken)
                ?? throw new ForbiddenException("Hoca profiliniz bulunamadı.");
            if (request.RequestedInstructorProfileId is { } req && req != ownProfileId)
                throw new ForbiddenException("Başka bir hoca adına telafi talebi oluşturamazsınız.");
            instructorProfileId = ownProfileId;
        }
        else
        {
            if (request.RequestedInstructorProfileId is not { } req)
                throw new BusinessRuleException("Hoca seçilmeli (requestedInstructorProfileId).");
            var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, req, cancellationToken);
            instructorProfileId = profile.Id;
        }

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci için telafi talebi oluşturulamaz.");

        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);

        var startAt = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Unspecified);
        var endAt = startAt.AddMinutes(classType.DefaultDurationMinutes);

        // ---- R5: geçmişe talep YASAK. ----
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        if (startAt <= nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana telafi talebi oluşturulamaz.");

        // ---- R2: Instructor "kendi bağlamı" = bu hocayla AKTİF SÖZLEŞMELİ + ders tipi uyumlu + tarih aralığında öğrenci. ----
        if (isInstructor)
        {
            var requestDate = DateOnly.FromDateTime(startAt);
            var hasContract = await _db.StudentContracts.AnyAsync(c =>
                c.StudioId == studioId
                && c.StudentProfileId == student.Id
                && c.InstructorProfileId == instructorProfileId
                && c.ClassTypeId == classType.Id
                && c.Status == StudentContractStatus.Active
                && c.StartDate <= requestDate && requestDate <= c.EndDate, cancellationToken);
            if (!hasContract)
                throw new ForbiddenException("Bu öğrenci için sizinle aktif/uygun bir sözleşme yok; telafi talebi açamazsınız.");
        }

        // ---- Çakışma: gerçek (hoca+öğrenci) + pending (hoca+öğrenci). Çift yönlü blok. ----
        if (await SessionRules.HasConflictAsync(_db, studioId, instructorProfileId, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında başka bir derse atanmış.");
        if (await SessionRules.StudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, System.Array.Empty<Guid>(), cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");
        if (await SessionRules.PendingInstructorConflictAsync(_db, studioId, instructorProfileId, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında onay bekleyen bir telafi talebine atanmış.");
        if (await SessionRules.PendingStudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında onay bekleyen bir telafi talebi var.");

        // ---- Rezerve: kullanılabilir bakiye > 0 (ledger − aktif Pending talep). Lock ile serileştir. ----
        await _creditLock.LockStudentAsync(studioId, student.Id, cancellationToken);
        var available = await _credits.GetAvailableBalanceAsync(studioId, student.Id, cancellationToken);
        if (available <= 0)
            throw new ConflictException("Öğrencinin kullanılabilir telafi hakkı yok (kredi yok veya başka talepte rezerve).");

        var entity = new MakeupRequest
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            RequestedInstructorProfileId = instructorProfileId,
            ClassTypeId = classType.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = MakeupRequestStatus.Pending,
            Description = request.Description?.Trim(),
            RequestedByUserId = userId,
        };
        _db.MakeupRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = await MakeupRequestView.BuildAsync(_db, studioId, new[] { entity }, cancellationToken);
        return dtos[0];
    }
}
