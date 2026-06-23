using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Audit;
using Pliaforma.Persistence.Auth;
using Pliaforma.Persistence.Contexts;
using Pliaforma.Persistence.Interceptors;
using Pliaforma.Persistence.Services;
using Pliaforma.Persistence.Transactions;

namespace Pliaforma.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ITransactionManager, EfTransactionManager>();
        // Sprint 31B-2: bağımsız bağlamla yazan güvenlik servisleri (ana transaction rollback olsa bile kalıcı).
        services.AddScoped<ISecurityAuditSink, EfSecurityAuditSink>();
        services.AddScoped<ILoginAttemptStore, EfLoginAttemptStore>();
        services.AddScoped<IClassSessionCapacityService, EfClassSessionCapacityService>();
        services.AddScoped<IMakeupCreditLock, EfMakeupCreditLock>();
        services.AddScoped<IEquipmentPoolLock, EfEquipmentPoolLock>();
        services.AddScoped<IStudentPackageCreditService, EfStudentPackageCreditService>();

        return services;
    }
}
