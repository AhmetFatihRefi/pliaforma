using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Common;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Contexts;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<StudioMembership> StudioMemberships => Set<StudioMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();
    public DbSet<InstructorProfile> InstructorProfiles => Set<InstructorProfile>();
    public DbSet<ClassType> ClassTypes => Set<ClassType>();
    public DbSet<InstructorClassType> InstructorClassTypes => Set<InstructorClassType>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<InstructorAvailability> InstructorAvailabilities => Set<InstructorAvailability>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<ClassBooking> ClassBookings => Set<ClassBooking>();
    public DbSet<StudentContract> StudentContracts => Set<StudentContract>();
    public DbSet<StudentContractLine> StudentContractLines => Set<StudentContractLine>();
    public DbSet<StudentContractLinePreferredSlot> StudentContractLinePreferredSlots => Set<StudentContractLinePreferredSlot>();
    public DbSet<ContractAmendment> ContractAmendments => Set<ContractAmendment>();
    public DbSet<MakeupCreditTransaction> MakeupCreditTransactions => Set<MakeupCreditTransaction>();
    public DbSet<MakeupRequest> MakeupRequests => Set<MakeupRequest>();
    public DbSet<StudioClosure> StudioClosures => Set<StudioClosure>();
    public DbSet<StudioMakeupPolicy> StudioMakeupPolicies => Set<StudioMakeupPolicy>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<PackagePlan> PackagePlans => Set<PackagePlan>();
    public DbSet<PackagePlanClassType> PackagePlanClassTypes => Set<PackagePlanClassType>();
    public DbSet<PackagePlanPriceVersion> PackagePlanPriceVersions => Set<PackagePlanPriceVersion>();
    public DbSet<StudentPackage> StudentPackages => Set<StudentPackage>();
    public DbSet<StudentPackageAllowedClassType> StudentPackageAllowedClassTypes => Set<StudentPackageAllowedClassType>();
    public DbSet<StudentPackageTransaction> StudentPackageTransactions => Set<StudentPackageTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PlatformSubscription> PlatformSubscriptions => Set<PlatformSubscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<PlatformSubscriptionPayment> PlatformSubscriptionPayments => Set<PlatformSubscriptionPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Soft-delete global filter: IsDeleted == false.
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);

            // İstemci tarafı üretilen GUID'ler; EF "var olan" gibi davranmasın.
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.Id))
                .ValueGeneratedNever();
        }
    }
}
