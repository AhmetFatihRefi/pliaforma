using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Studios;

// =====================================================================================================
// Sprint 37 — Stüdyo haftalık çalışma saatleri. TEK KAYNAK: StudioWorkingHoursService.
//   - Boşluk Bul, ders oluşturma/güncelleme/erteleme, program üretimi ve replan bu saatlere uyar.
//   - Kayıt yoksa fallback 07:00–23:00 (her gün açık) → mevcut davranış korunur (register'da tohumlama YOK).
//   - IsClosed=true → o gün kapalı. Açık günde OpensAt < ClosesAt zorunlu. Gece aşan saat kapsam dışı.
//   - end == ClosesAt KABUL (tam kapanışta biten ders geçerli).
//   - Yetki: PUT Owner/StudioAdmin; GET tüm authenticated roller.
// =====================================================================================================

/// <summary>Bir günün çözülmüş penceresi: kapalı veya [Open, Close).</summary>
public readonly record struct WorkingDayWindow(bool IsClosed, TimeOnly Open, TimeOnly Close);

/// <summary>Stüdyonun 7 günlük çözülmüş çalışma saatleri haritası (fallback dahil). Tek sefer yüklenir, çok yerde kullanılır.</summary>
public sealed class StudioWorkingHoursMap
{
    public static readonly TimeOnly FallbackOpen = new(7, 0);    // 07:00 — kayıt yoksa
    public static readonly TimeOnly FallbackClose = new(23, 0);  // 23:00

    private readonly Dictionary<DayOfWeek, WorkingDayWindow> _byDay;
    public bool IsConfigured { get; }

    private StudioWorkingHoursMap(Dictionary<DayOfWeek, WorkingDayWindow> byDay, bool isConfigured)
    {
        _byDay = byDay;
        IsConfigured = isConfigured;
    }

    public static StudioWorkingHoursMap From(IEnumerable<StudioWorkingHour> rows)
    {
        var byDay = new Dictionary<DayOfWeek, WorkingDayWindow>();
        foreach (var r in rows)
        {
            // Aynı güne birden çok aktif kayıt olmamalı; olsa bile ilki esas alınır (full-replace bunu garanti eder).
            if (byDay.ContainsKey(r.DayOfWeek)) continue;
            byDay[r.DayOfWeek] = r.IsClosed || r.OpensAt is null || r.ClosesAt is null
                ? new WorkingDayWindow(true, default, default)
                : new WorkingDayWindow(false, r.OpensAt.Value, r.ClosesAt.Value);
        }
        var configured = byDay.Count > 0;
        // Kayıt olmayan günler için fallback (açık 07:00–23:00).
        for (var d = DayOfWeek.Sunday; d <= DayOfWeek.Saturday; d++)
            if (!byDay.ContainsKey(d))
                byDay[d] = new WorkingDayWindow(false, FallbackOpen, FallbackClose);
        return new StudioWorkingHoursMap(byDay, configured);
    }

    public WorkingDayWindow Day(DayOfWeek day) => _byDay[day];

    /// <summary>O günün açık penceresi; kapalıysa null.</summary>
    public (TimeOnly Open, TimeOnly Close)? WindowFor(DayOfWeek day)
        => _byDay[day] is { IsClosed: false } w ? (w.Open, w.Close) : null;

    /// <summary>Verilen seans [start, end] tamamen o günün çalışma penceresinde mi? (kapalı gün/saat dışı → false)</summary>
    public bool Fits(DateTime start, DateTime end)
    {
        var w = _byDay[start.DayOfWeek];
        if (w.IsClosed) return false;
        var startT = TimeOnly.FromDateTime(start);
        if (startT < w.Open || startT >= w.Close) return false;
        if (end.Date != start.Date) return false;            // gece aşan ders → dışarıda
        return TimeOnly.FromDateTime(end) <= w.Close;         // end == Close KABUL
    }
}

/// <summary>Çalışma saatleri tek kaynağı: yükleme + içerme kontrolleri (statik — SessionRules deseni).</summary>
public static class StudioWorkingHoursService
{
    public static async Task<StudioWorkingHoursMap> LoadAsync(IAppDbContext db, Guid studioId, CancellationToken ct)
    {
        var rows = await db.StudioWorkingHours
            .Where(w => w.StudioId == studioId && w.IsActive)
            .ToListAsync(ct);
        return StudioWorkingHoursMap.From(rows);
    }

    /// <summary>Manuel create/update/reschedule HARD guard'ı (ihlalde kullanıcı dostu 409). Karar #5: gün-bazlı mesaj.</summary>
    public static void EnsureWithin(StudioWorkingHoursMap map, DateTime start, DateTime end)
    {
        var w = map.Day(start.DayOfWeek);
        if (w.IsClosed)
            throw new ConflictException("Bu gün stüdyo kapalı.");

        var startT = TimeOnly.FromDateTime(start);
        if (startT < w.Open || startT >= w.Close)
            throw new ConflictException("Seans saati stüdyo çalışma saatleri dışında.");

        if (end.Date != start.Date || TimeOnly.FromDateTime(end) > w.Close)
            throw new ConflictException("Seans bitiş saati stüdyo kapanış saatini aşıyor.");
    }
}

// ---------------------------------------------------------------------------------------------------
// DTO / Query / Command
// ---------------------------------------------------------------------------------------------------

public sealed record WorkingHourDayDto(DayOfWeek DayOfWeek, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt);

/// <summary><c>IsConfigured=false</c> → stüdyo hiç kaydetmemiş; <c>Days</c> fallback 07:00–23:00 (öneri/uygulanan).</summary>
public sealed record StudioWorkingHoursDto(bool IsConfigured, IReadOnlyList<WorkingHourDayDto> Days);

public sealed record WorkingHourDayInput(DayOfWeek DayOfWeek, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt);

// ---- GET /api/studio/working-hours ----
public sealed record GetStudioWorkingHoursQuery : IQuery<StudioWorkingHoursDto>;

public sealed class GetStudioWorkingHoursHandler : IQueryHandler<GetStudioWorkingHoursQuery, StudioWorkingHoursDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetStudioWorkingHoursHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudioWorkingHoursDto> Handle(GetStudioWorkingHoursQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var map = await StudioWorkingHoursService.LoadAsync(_db, studioId, ct);
        return new StudioWorkingHoursDto(map.IsConfigured, StudioWorkingHoursWeek.ToDays(map));
    }
}

// ---- PUT /api/studio/working-hours (full replace, 7 gün) ----
public sealed record UpdateStudioWorkingHoursCommand(IReadOnlyList<WorkingHourDayInput> Days)
    : ICommand<StudioWorkingHoursDto>, IStudioWriteRequest;

public sealed class UpdateStudioWorkingHoursValidator : AbstractValidator<UpdateStudioWorkingHoursCommand>
{
    public UpdateStudioWorkingHoursValidator()
    {
        RuleFor(x => x.Days).NotNull()
            .Must(d => d is { Count: 7 }).WithMessage("Haftanın 7 günü de gönderilmelidir.")
            .Must(d => d!.Select(x => x.DayOfWeek).Distinct().Count() == 7).WithMessage("Her gün için tek kayıt olmalıdır.")
            .When(x => x.Days is not null);

        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.OpensAt).NotNull().When(d => !d.IsClosed)
                .WithMessage("Açık günlerde açılış saati zorunludur.");
            day.RuleFor(d => d.ClosesAt).NotNull().When(d => !d.IsClosed)
                .WithMessage("Açık günlerde kapanış saati zorunludur.");
            day.RuleFor(d => d).Must(d => d.OpensAt!.Value < d.ClosesAt!.Value)
                .When(d => !d.IsClosed && d.OpensAt.HasValue && d.ClosesAt.HasValue)
                .WithMessage("Açılış saati kapanış saatinden önce olmalıdır (gece aşan saat desteklenmez).");
        });
    }
}

public sealed class UpdateStudioWorkingHoursHandler : ICommandHandler<UpdateStudioWorkingHoursCommand, StudioWorkingHoursDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public UpdateStudioWorkingHoursHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudioWorkingHoursDto> Handle(UpdateStudioWorkingHoursCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();

        // FULL REPLACE: mevcut aktif kayıtları soft-delete + 7 yeni (tek transaction — TransactionBehavior).
        var existing = await _db.StudioWorkingHours.Where(w => w.StudioId == studioId).ToListAsync(ct);
        _db.StudioWorkingHours.RemoveRange(existing); // AuditInterceptor → soft-delete

        foreach (var d in request.Days)
        {
            var closed = d.IsClosed;
            _db.StudioWorkingHours.Add(new StudioWorkingHour
            {
                StudioId = studioId,
                DayOfWeek = d.DayOfWeek,
                IsClosed = closed,
                OpensAt = closed ? null : d.OpensAt,
                ClosesAt = closed ? null : d.ClosesAt,
                IsActive = true,
            });
        }
        await _db.SaveChangesAsync(ct);

        var map = await StudioWorkingHoursService.LoadAsync(_db, studioId, ct);
        return new StudioWorkingHoursDto(map.IsConfigured, StudioWorkingHoursWeek.ToDays(map));
    }
}

/// <summary>Haritayı Pazartesi→Pazar sıralı 7 günlük DTO listesine çevirir (UI dostu sıralama).</summary>
internal static class StudioWorkingHoursWeek
{
    private static readonly DayOfWeek[] MondayFirst =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    public static IReadOnlyList<WorkingHourDayDto> ToDays(StudioWorkingHoursMap map) =>
        MondayFirst.Select(d =>
        {
            var w = map.Day(d);
            return w.IsClosed
                ? new WorkingHourDayDto(d, true, null, null)
                : new WorkingHourDayDto(d, false, w.Open, w.Close);
        }).ToList();
}
