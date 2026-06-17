using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>effectiveStatus etiketleri (kullanıcı tarafından sabitlendi).</summary>
public static class EffectiveSubscriptionStatus
{
    public const string Trialing = "Trialing";
    public const string Active = "Active";
    public const string PastDueGrace = "PastDueGrace";
    public const string CancelPending = "CancelPending";
    public const string Expired = "Expired";
    public const string FailOpenLicensed = "FailOpenLicensed";
}

/// <summary>Hesaplanan lisans sonucu. UI kilit davranışı bu kaynak alanlara bağlanır (string etikete değil).</summary>
public sealed record LicenseResult(
    string EffectiveStatus,
    bool IsLicensed,
    bool IsReadOnlyByLicense,
    int DaysRemaining,
    string LicenseMessage);

/// <summary>
/// Abonelik yaşam döngüsü kuralları (saf fonksiyonlar; kalıcı job yok). Lisans DİNAMİK hesaplanır:
/// dönem + 7g grace + cancel-at-period-end. Aboneliği olmayan stüdyo FAIL-OPEN ile lisanslı sayılır
/// (yanlışlıkla kilitlememek için) ama görünür (FailOpenLicensed). today = UTC gün (grace tamponu TZ sapmasını
/// emer).
/// </summary>
public static class SubscriptionRules
{
    public const int GraceDays = 7;
    public const int TrialDays = 14;

    /// <summary>Bir abonelik (veya null) + bugün → lisans sonucu.</summary>
    public static LicenseResult Evaluate(PlatformSubscription? sub, DateOnly today)
    {
        if (sub is null)
            return new LicenseResult(
                EffectiveSubscriptionStatus.FailOpenLicensed, IsLicensed: true, IsReadOnlyByLicense: false,
                DaysRemaining: 0, "Abonelik kaydı yok — geçici (fail-open) lisanslı. Lütfen bir plan atayın.");

        var periodEnd = sub.CurrentPeriodEnd;
        var graceEnd = periodEnd.AddDays(GraceDays);
        var inPeriod = today <= periodEnd;
        var inGrace = today <= graceEnd;
        var cancelPending = sub.CancelAtPeriodEnd;
        var isTrial = sub.Status == SubscriptionStatus.Trialing;

        if (inPeriod)
        {
            var days = periodEnd.DayNumber - today.DayNumber;
            if (cancelPending)
                return new LicenseResult(EffectiveSubscriptionStatus.CancelPending, true, false, days,
                    $"Aboneliğiniz {periodEnd:yyyy-MM-dd} tarihinde sona erecek; o tarihe kadar erişiminiz açık ({days} gün).");
            if (isTrial)
                return new LicenseResult(EffectiveSubscriptionStatus.Trialing, true, false, days,
                    $"Deneme süreniz {days} gün sonra ({periodEnd:yyyy-MM-dd}) bitiyor.");
            return new LicenseResult(EffectiveSubscriptionStatus.Active, true, false, days,
                $"Aboneliğiniz aktif (dönem sonu {periodEnd:yyyy-MM-dd}, {days} gün).");
        }

        if (inGrace && !cancelPending)
        {
            // Grace: yazma HÂLÂ açık (tolerans) ama uyarı gösterilir. Bitince Expired → kilit.
            var days = graceEnd.DayNumber - today.DayNumber;
            return new LicenseResult(EffectiveSubscriptionStatus.PastDueGrace, IsLicensed: true, IsReadOnlyByLicense: false, days,
                $"Ödeme bekleniyor. {days} gün içinde yenilenmezse yazma işlemleri kısıtlanacak.");
        }

        return new LicenseResult(EffectiveSubscriptionStatus.Expired, IsLicensed: false, IsReadOnlyByLicense: true,
            DaysRemaining: 0, "Aboneliğiniz sona erdi. Yazma işlemleri kısıtlı; yenilemek için iletişime geçin.");
    }

    /// <summary>Dönemi faturalama periyoduna göre ilerletir.</summary>
    public static DateOnly AddPeriod(DateOnly start, BillingPeriod period) =>
        period == BillingPeriod.Yearly ? start.AddYears(1) : start.AddMonths(1);
}
