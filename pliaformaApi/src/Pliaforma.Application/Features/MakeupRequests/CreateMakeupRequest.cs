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
// Sprint 28C-2: talep LINE-driven → StudentContractLineId zorunlu; öğrenci/ders tipi/hoca line'dan türetilir.
public sealed record CreateMakeupRequestCommand(
    Guid StudentContractLineId,
    DateTime StartAt,
    string? Description = null,
    // Sprint 33: line hocası null (herhangi bir hoca) ise telafi talebi için hoca BURADA belirtilir (zorunlu).
    Guid? RequestedInstructorProfileId = null) : ICommand<MakeupRequestDto>, IStudioWriteRequest;

public sealed class CreateMakeupRequestValidator : AbstractValidator<CreateMakeupRequestCommand>
{
    public CreateMakeupRequestValidator()
    {
        RuleFor(x => x.StudentContractLineId).NotEmpty().WithMessage("Telafi talebi bir sözleşme satırına (line) bağlı olmalı.");
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

        // ---- Sprint 28C-2: line authoritative → öğrenci/ders tipi/hoca line'dan türetilir. ----
        var line = await _db.StudentContractLines.Include(l => l.StudentContract)
            .FirstOrDefaultAsync(l => l.Id == request.StudentContractLineId && l.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Sözleşme satırı", request.StudentContractLineId);
        if (line.Status != StudentContractLineStatus.Active)
            throw new BusinessRuleException("Pasif/iptal satıra telafi talebi açılamaz.");

        // Sprint 33: hoca = line tercihi ?? talepte verilen. İkisi de yoksa belirtilmeli (telafi seansının hocası olmak zorunda).
        var instructorProfileId = line.InstructorProfileId ?? request.RequestedInstructorProfileId
            ?? throw new BusinessRuleException("Bu sözleşmenin hocası belirtilmemiş; telafi talebi için bir hoca seçin.");
        // Instructor yalnız KENDİsi için talep açabilir (line ona pinli ya da kendini seçmiş olmalı).
        if (isInstructor)
        {
            var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken)
                ?? throw new ForbiddenException("Hoca profiliniz bulunamadı.");
            if (instructorProfileId != ownProfileId)
                throw new ForbiddenException("Yalnızca kendiniz için telafi talebi açabilirsiniz.");
        }
        else
        {
            await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, instructorProfileId, cancellationToken);
        }

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == line.StudentContract!.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", line.StudentContract!.StudentProfileId);
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci için telafi talebi oluşturulamaz.");

        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == line.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", line.ClassTypeId);

        var startAt = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Unspecified);
        var endAt = startAt.AddMinutes(classType.DefaultDurationMinutes);

        // ---- R5: geçmişe talep YASAK. (Makeup line.EndDate sonrası serbest → tarih-aralığı kontrolü YOK.) ----
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        if (startAt <= nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana telafi talebi oluşturulamaz.");

        // ---- Çakışma: gerçek (hoca+öğrenci) + pending (hoca+öğrenci). Çift yönlü blok. ----
        if (await SessionRules.HasConflictAsync(_db, studioId, instructorProfileId, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında başka bir derse atanmış.");
        if (await SessionRules.StudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, System.Array.Empty<Guid>(), cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");
        if (await SessionRules.PendingInstructorConflictAsync(_db, studioId, instructorProfileId, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Hoca bu zaman aralığında onay bekleyen bir telafi talebine atanmış.");
        if (await SessionRules.PendingStudentConflictAsync(_db, studioId, new[] { student.Id }, startAt, endAt, null, cancellationToken))
            throw new ConflictException("Öğrencinin bu zaman aralığında onay bekleyen bir telafi talebi var.");

        // ---- Rezerve LINE-bazlı: line kullanılabilir bakiye > 0 (lineBalance − line pending). Aynı line 2. pending → 0 → 409. ----
        await _creditLock.LockStudentAsync(studioId, student.Id, cancellationToken);
        var available = await _credits.GetLineAvailableBalanceAsync(studioId, line.Id, cancellationToken);
        if (available <= 0)
            throw new ConflictException("Bu satırda kullanılabilir telafi hakkı yok (kredi yok veya başka talepte rezerve).");

        var entity = new MakeupRequest
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            StudentContractLineId = line.Id,
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
