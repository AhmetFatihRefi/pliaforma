using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Studios;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Tek gün / tek aralık içinde ardışık ders seansları üretir (ClassType süresi + opsiyonel ara).
/// Bu RECURRING booking DEĞİLDİR. Geçmiş/çakışan slotlar atlanır; created+skipped döner.
/// Sprint 26: müsaitlik penceresi kontrolü YOK — yalnız gerçek çakışma + geçmiş atlanır.
/// </summary>
public sealed record BulkGenerateClassSessionsCommand(
    Guid InstructorProfileId,
    Guid ClassTypeId,
    DateOnly Date,
    TimeOnly RangeStart,
    TimeOnly RangeEnd,
    int? Capacity,
    int? BreakMinutes) : ICommand<BulkGenerateResultDto>, IStudioWriteRequest;

public sealed class BulkGenerateClassSessionsValidator : AbstractValidator<BulkGenerateClassSessionsCommand>
{
    public BulkGenerateClassSessionsValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x).Must(x => x.RangeStart < x.RangeEnd).WithMessage("Aralık başlangıcı bitişten önce olmalı.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).When(x => x.Capacity.HasValue);
        RuleFor(x => x.BreakMinutes).InclusiveBetween(0, 240).When(x => x.BreakMinutes.HasValue);
    }
}

public sealed class BulkGenerateClassSessionsHandler
    : ICommandHandler<BulkGenerateClassSessionsCommand, BulkGenerateResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly IEquipmentPoolLock _poolLock;

    public BulkGenerateClassSessionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, IEquipmentPoolLock poolLock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _poolLock = poolLock;
    }

    public async Task<BulkGenerateResultDto> Handle(BulkGenerateClassSessionsCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, request.InstructorProfileId, cancellationToken);
        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);

        var duration = classType.DefaultDurationMinutes;
        var gap = request.BreakMinutes ?? 0;
        // Sprint 28A: ClassType.MaxCapacity dormant → verilmezse grup-sınırsız (9999).
        var capacity = request.Capacity ?? 9999;
        if (capacity < 1) capacity = 1;

        // Sprint 32B: ekipman-bağlı ders tipi → ön kontroller + havuz kilidi. Slot başına kapasite yetmezse o slot
        // ATLANIR (skip "equipmentCapacity"); aynı bulk'ta üretilen önceki slotlar da used'a dahil (over-allocation guard).
        Guid? equipmentId = classType.EquipmentId;
        var poolTotal = 0;
        if (equipmentId is { } eqId)
        {
            if (EquipmentCapacityRules.IsUnlimited(capacity))
                throw new ConflictException("Ekipman bağlı derslerde sınırsız kapasite kullanılamaz. Lütfen sonlu bir kapasite girin.");
            await _poolLock.LockEquipmentAsync(studioId, eqId, cancellationToken);
            var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == eqId && e.StudioId == studioId, cancellationToken)
                ?? throw new NotFoundException("Ekipman", eqId);
            if (!equipment.IsActive)
                throw new ConflictException("Bu ders tipinin ekipmanı pasif; ders oluşturulamaz. Önce ekipmanı etkinleştirin.");
            poolTotal = EquipmentCapacityRules.TotalCapacity(equipment.Quantity, equipment.PerUnitConcurrentUsage);
            if (capacity > poolTotal)
                throw new ConflictException($"Bu ders için istenen kapasite ({capacity}) ekipman toplam kapasitesini ({poolTotal}) aşıyor.");
        }

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        // Sprint 37: stüdyo çalışma saatleri (kayıt yoksa fallback 07:00–23:00). Pencere dışı slotlar atlanır.
        var workingHours = await StudioWorkingHoursService.LoadAsync(_db, studioId, cancellationToken);

        // O günkü mevcut Scheduled seanslar (çakışma için, in-memory).
        var dayStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var existing = await _db.ClassSessions
            .Where(s => s.StudioId == studioId && s.InstructorProfileId == profile.Id
                        && s.Status == ClassSessionStatus.Scheduled && s.StartAt < dayEnd && s.EndAt > dayStart)
            .Select(s => new { s.StartAt, s.EndAt })
            .ToListAsync(cancellationToken);

        var created = new List<ClassSession>();
        var skipped = new List<SkippedSlotDto>();

        var rangeStart = request.Date.ToDateTime(request.RangeStart);
        var rangeEnd = request.Date.ToDateTime(request.RangeEnd);

        for (var cursor = rangeStart; cursor.AddMinutes(duration) <= rangeEnd; cursor = cursor.AddMinutes(duration + gap))
        {
            var start = DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified);
            var end = start.AddMinutes(duration);

            if (start < nowLocal) { skipped.Add(new SkippedSlotDto(start, "past")); continue; }
            // Sprint 37: çalışma saati dışı (kapalı gün / pencere dışı / kapanış aşımı) → atla.
            if (!workingHours.Fits(start, end)) { skipped.Add(new SkippedSlotDto(start, "outside-working-hours")); continue; }
            if (existing.Any(x => x.StartAt < end && start < x.EndAt)
                || created.Any(c => c.StartAt < end && start < c.EndAt))
            {
                skipped.Add(new SkippedSlotDto(start, "conflict"));
                continue;
            }

            // Sprint 32B: ekipman kapasite kontrolü (DB'deki çakışan kullanım + aynı bulk'ta üretilmiş çakışan slotlar).
            if (equipmentId is { } poolId)
            {
                var used = await EquipmentCapacityRules.UsedCapacityAsync(_db, studioId, poolId, start, end, System.Array.Empty<Guid>(), cancellationToken)
                           + created.Where(c => c.StartAt < end && start < c.EndAt).Sum(c => c.Capacity);
                if (capacity > poolTotal - used)
                {
                    skipped.Add(new SkippedSlotDto(start, "equipmentCapacity"));
                    continue;
                }
            }

            created.Add(new ClassSession
            {
                StudioId = studioId,
                InstructorProfileId = profile.Id,
                ClassTypeId = classType.Id,
                StartAt = start,
                EndAt = end,
                Capacity = capacity,
                CreatedByUserId = userId,
            });
        }

        if (created.Count > 0)
        {
            _db.ClassSessions.AddRange(created);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var createdDtos = created
            .Select(s => ClassSessionDto.From(s, profile.DisplayName, classType.Name))
            .ToList();

        return new BulkGenerateResultDto(createdDtos, skipped);
    }
}
