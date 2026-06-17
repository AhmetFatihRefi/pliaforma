using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>
/// Telafi kredisi defterinin (ledger) tek yazma noktası: bakiye hesabı, politika çözümü ve işaretli
/// hareket üretimi burada toplanır. Hareketler çağıranın transaction'ına EKLENİR (SaveChanges çağıranda)
/// — böylece kredi tüketimi + booking aynı transaction içinde kalır. Sign kuralı: Granted/Restored (+),
/// Used/Revoked/Expired (−). Bakiye = aktif hareketlerin Amount toplamı.
/// </summary>
public sealed class MakeupCreditService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;

    public MakeupCreditService(IAppDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Bakiye = aktif (silinmemiş) hareketlerin işaretli toplamı. Expiry bu sürümde ZORLANMAZ.</summary>
    public async Task<int> GetBalanceAsync(Guid studioId, Guid studentProfileId, CancellationToken ct)
        => await _db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.StudentProfileId == studentProfileId)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;

    /// <summary>Stüdyo politikası (yoksa bellek-içi varsayılan; persist edilmez).</summary>
    public async Task<StudioMakeupPolicy> GetPolicyAsync(Guid studioId, CancellationToken ct)
        => await _db.StudioMakeupPolicies.FirstOrDefaultAsync(p => p.StudioId == studioId, ct)
           ?? StudioMakeupPolicy.Default(studioId);

    /// <summary>Politikaya göre geçerlilik sonu (DefaultCreditValidityDays). 0 → süresiz.</summary>
    public DateTime? ComputeExpiry(StudioMakeupPolicy policy)
        => policy.DefaultCreditValidityDays > 0 ? _clock.UtcNow.AddDays(policy.DefaultCreditValidityDays) : null;

    /// <summary>Bu öğrenciye içinde bulunulan takvim ayında manuel verilmiş kredi toplamı (limit kontrolü için).</summary>
    public async Task<int> ManualGrantedThisMonthAsync(Guid studioId, Guid studentProfileId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.StudentProfileId == studentProfileId
                        && t.Type == MakeupCreditTransactionType.Granted
                        && (t.Reason == MakeupCreditReason.ManualByOwner || t.Reason == MakeupCreditReason.ManualByInstructor)
                        && t.CreatedAt >= monthStart)
            .SumAsync(t => (int?)t.Amount, ct) ?? 0;
    }

    /// <summary>Bir kaynak seans için bu öğrenciye zaten otomatik kredi verilmiş mi? (çift kredi engeli).</summary>
    public Task<bool> HasGrantForSessionAsync(Guid studioId, Guid studentProfileId, Guid sourceSessionId, CancellationToken ct)
        => _db.MakeupCreditTransactions.AnyAsync(
            t => t.StudioId == studioId && t.StudentProfileId == studentProfileId
                 && t.SourceClassSessionId == sourceSessionId
                 && t.Type == MakeupCreditTransactionType.Granted, ct);

    public MakeupCreditTransaction Grant(
        Guid studioId, Guid studentProfileId, int amount, MakeupCreditReason reason, Guid createdByUserId,
        Guid? sourceSessionId = null, Guid? sourceBookingId = null, DateTime? expiresAt = null, string? notes = null)
    {
        if (amount <= 0) throw new BusinessRuleException("Kredi miktarı pozitif olmalı.");
        return Add(studioId, studentProfileId, amount, MakeupCreditTransactionType.Granted, reason,
            createdByUserId, sourceSessionId, sourceBookingId, usedForBookingId: null, expiresAt, notes);
    }

    public MakeupCreditTransaction Use(Guid studioId, Guid studentProfileId, int amount, Guid usedForBookingId, Guid createdByUserId)
    {
        if (amount <= 0) throw new BusinessRuleException("Kullanım miktarı pozitif olmalı.");
        return Add(studioId, studentProfileId, -amount, MakeupCreditTransactionType.Used, MakeupCreditReason.MakeupBookingUsed,
            createdByUserId, sourceSessionId: null, sourceBookingId: null, usedForBookingId: usedForBookingId, expiresAt: null, notes: null);
    }

    public MakeupCreditTransaction Restore(Guid studioId, Guid studentProfileId, int amount, Guid sourceBookingId, Guid createdByUserId)
    {
        if (amount <= 0) throw new BusinessRuleException("İade miktarı pozitif olmalı.");
        return Add(studioId, studentProfileId, amount, MakeupCreditTransactionType.Restored, MakeupCreditReason.MakeupBookingCancelled,
            createdByUserId, sourceSessionId: null, sourceBookingId: sourceBookingId, usedForBookingId: null, expiresAt: null, notes: null);
    }

    public MakeupCreditTransaction Revoke(Guid studioId, Guid studentProfileId, int amount, Guid createdByUserId, MakeupCreditReason reason, string? notes)
    {
        if (amount <= 0) throw new BusinessRuleException("Geri alma miktarı pozitif olmalı.");
        return Add(studioId, studentProfileId, -amount, MakeupCreditTransactionType.Revoked, reason,
            createdByUserId, sourceSessionId: null, sourceBookingId: null, usedForBookingId: null, expiresAt: null, notes);
    }

    private MakeupCreditTransaction Add(
        Guid studioId, Guid studentProfileId, int signedAmount, MakeupCreditTransactionType type,
        MakeupCreditReason reason, Guid createdByUserId, Guid? sourceSessionId, Guid? sourceBookingId,
        Guid? usedForBookingId, DateTime? expiresAt, string? notes)
    {
        var tx = new MakeupCreditTransaction
        {
            StudioId = studioId,
            StudentProfileId = studentProfileId,
            Amount = signedAmount,
            Type = type,
            Reason = reason,
            SourceClassSessionId = sourceSessionId,
            SourceBookingId = sourceBookingId,
            UsedForBookingId = usedForBookingId,
            ExpiresAt = expiresAt,
            CreatedByUserId = createdByUserId,
            Notes = notes?.Trim(),
        };
        _db.MakeupCreditTransactions.Add(tx);
        return tx;
    }
}
