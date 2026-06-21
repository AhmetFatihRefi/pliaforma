using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// =====================================================================================================
// Sprint 29A — StudentContractLine tercih gün/saatleri (PreferredSlot). Gün zorunlu, saat opsiyonel.
// Aynı line içinde aynı (DayOfWeek, PreferredStartTime) — NULL saat dahil — tekrar edemez. Owner/StudioAdmin.
// Kullanım (29B): aday öğrenci seçiminde gün filtre, saat yakınlık sıralaması. 29A: veri modeli + CRUD + gösterim.
// =====================================================================================================

public sealed record PreferredSlotDto(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly? PreferredStartTime,
    bool IsActive,
    int SequenceNo,
    string? Notes);

/// <summary>Yeni line / slot ekleme girdisi (gün zorunlu, saat opsiyonel).</summary>
public sealed record PreferredSlotInput(DayOfWeek DayOfWeek, TimeOnly? PreferredStartTime, string? Notes);

internal static class PreferredSlotRules
{
    /// <summary>Girdi listesinde aynı (gün, saat) — NULL dahil — tekrarı var mı?</summary>
    public static bool HasInternalDuplicates(IReadOnlyList<PreferredSlotInput> inputs) =>
        inputs.GroupBy(s => (s.DayOfWeek, s.PreferredStartTime)).Any(g => g.Count() > 1);

    /// <summary>Bir line için slot varlıkları üretir (SequenceNo başlangıcı verilir).</summary>
    public static IEnumerable<StudentContractLinePreferredSlot> Build(
        Guid studioId, Guid lineId, Guid userId, IReadOnlyList<PreferredSlotInput> inputs, int startSeq = 1)
    {
        var seq = startSeq;
        foreach (var s in inputs)
            yield return new StudentContractLinePreferredSlot
            {
                StudioId = studioId,
                StudentContractLineId = lineId,
                DayOfWeek = s.DayOfWeek,
                PreferredStartTime = s.PreferredStartTime,
                IsActive = true,
                SequenceNo = seq++,
                Notes = s.Notes?.Trim(),
                CreatedByUserId = userId,
            };
    }
}

// ---- Slot ekle (line altına) ----
public sealed record AddLinePreferredSlotCommand(Guid LineId, DayOfWeek DayOfWeek, TimeOnly? PreferredStartTime, string? Notes)
    : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class AddLinePreferredSlotValidator : AbstractValidator<AddLinePreferredSlotCommand>
{
    public AddLinePreferredSlotValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class AddLinePreferredSlotHandler : ICommandHandler<AddLinePreferredSlotCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public AddLinePreferredSlotHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(AddLinePreferredSlotCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var line = await _db.StudentContractLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", request.LineId);

        // Tekillik: aynı line'da aynı (gün, saat) — NULL saat dahil — tekrar edemez (soft-delete'li slotlar query filter ile düşer).
        var time = request.PreferredStartTime;
        var dup = await _db.StudentContractLinePreferredSlots.AnyAsync(s =>
            s.StudentContractLineId == line.Id
            && s.DayOfWeek == request.DayOfWeek
            && ((time == null && s.PreferredStartTime == null) || s.PreferredStartTime == time), ct);
        if (dup)
            throw new ConflictException("Bu satırda aynı gün ve saat için zaten bir tercih var.");

        var nextSeq = 1 + (await _db.StudentContractLinePreferredSlots
            .Where(s => s.StudentContractLineId == line.Id)
            .Select(s => (int?)s.SequenceNo).MaxAsync(ct) ?? 0);

        _db.StudentContractLinePreferredSlots.Add(new StudentContractLinePreferredSlot
        {
            StudioId = studioId,
            StudentContractLineId = line.Id,
            DayOfWeek = request.DayOfWeek,
            PreferredStartTime = request.PreferredStartTime,
            IsActive = true,
            SequenceNo = nextSeq,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        });
        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
        return dtos[0];
    }
}

// ---- Slot sil (soft-delete) ----
public sealed record RemoveLinePreferredSlotCommand(Guid SlotId) : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class RemoveLinePreferredSlotHandler : ICommandHandler<RemoveLinePreferredSlotCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public RemoveLinePreferredSlotHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(RemoveLinePreferredSlotCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var slot = await _db.StudentContractLinePreferredSlots.FirstOrDefaultAsync(s => s.Id == request.SlotId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Tercih slotu", request.SlotId);

        var lineId = slot.StudentContractLineId;
        _db.StudentContractLinePreferredSlots.Remove(slot); // AuditInterceptor → soft-delete (IsDeleted=true)
        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == lineId), ct);
        return dtos.Count > 0 ? dtos[0] : throw new NotFoundException("Sözleşme satırı", lineId);
    }
}
