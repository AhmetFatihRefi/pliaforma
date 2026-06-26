using Microsoft.EntityFrameworkCore;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Application katmanının veriye eriştiği soyutlama. Generic repository yerine doğrudan
/// DbSet kullanılır. EF Core sağlayıcı detayları (DatabaseFacade vb.) buraya SIZMAZ;
/// transaction yönetimi ayrı ITransactionManager soyutlaması ile yapılır.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Studio> Studios { get; }
    DbSet<StudioMembership> StudioMemberships { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuthToken> AuthTokens { get; }
    DbSet<InstructorProfile> InstructorProfiles { get; }
    DbSet<ClassType> ClassTypes { get; }
    DbSet<InstructorClassType> InstructorClassTypes { get; }

    /// <summary>Stüdyonun ekipman/envanter havuzu (Sprint 32A).</summary>
    DbSet<Equipment> Equipment { get; }
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<InstructorAvailability> InstructorAvailabilities { get; }
    DbSet<ClassSession> ClassSessions { get; }
    DbSet<ClassBooking> ClassBookings { get; }
    DbSet<StudentContract> StudentContracts { get; }
    DbSet<StudentContractLine> StudentContractLines { get; }
    DbSet<StudentContractLinePreferredSlot> StudentContractLinePreferredSlots { get; }

    /// <summary>Sözleşme satırı ödeme planı taksitleri (Sprint 38). Plan yoksa satır yok (eski davranış).</summary>
    DbSet<ContractLinePaymentInstallment> ContractLinePaymentInstallments { get; }
    DbSet<ContractAmendment> ContractAmendments { get; }
    DbSet<MakeupCreditTransaction> MakeupCreditTransactions { get; }
    DbSet<MakeupRequest> MakeupRequests { get; }
    DbSet<StudioClosure> StudioClosures { get; }

    /// <summary>Stüdyonun haftalık çalışma saatleri (Sprint 37). Kayıt yoksa fallback 07:00–23:00.</summary>
    DbSet<StudioWorkingHour> StudioWorkingHours { get; }
    DbSet<StudioMakeupPolicy> StudioMakeupPolicies { get; }
    DbSet<Attendance> Attendances { get; }
    DbSet<PackagePlan> PackagePlans { get; }
    DbSet<PackagePlanClassType> PackagePlanClassTypes { get; }
    DbSet<PackagePlanPriceVersion> PackagePlanPriceVersions { get; }
    DbSet<StudentPackage> StudentPackages { get; }
    DbSet<StudentPackageAllowedClassType> StudentPackageAllowedClassTypes { get; }
    DbSet<StudentPackageTransaction> StudentPackageTransactions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<PlatformSubscription> PlatformSubscriptions { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<PlatformSubscriptionPayment> PlatformSubscriptionPayments { get; }

    /// <summary>Güvenlik denetim kayıtları (append-only; yalnız Add). Bkz. SecurityAuditLog.</summary>
    DbSet<SecurityAuditLog> SecurityAuditLogs { get; }

    /// <summary>TOTP MFA ayarları (Sprint 31-MFA).</summary>
    DbSet<UserMfaSettings> UserMfaSettings { get; }
    DbSet<MfaRecoveryCode> MfaRecoveryCodes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
