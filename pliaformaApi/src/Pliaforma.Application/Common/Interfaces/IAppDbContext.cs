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
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<InstructorAvailability> InstructorAvailabilities { get; }
    DbSet<ClassSession> ClassSessions { get; }
    DbSet<ClassBooking> ClassBookings { get; }
    DbSet<StudentContract> StudentContracts { get; }
    DbSet<StudentContractLine> StudentContractLines { get; }
    DbSet<ContractAmendment> ContractAmendments { get; }
    DbSet<MakeupCreditTransaction> MakeupCreditTransactions { get; }
    DbSet<MakeupRequest> MakeupRequests { get; }
    DbSet<StudioClosure> StudioClosures { get; }
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
