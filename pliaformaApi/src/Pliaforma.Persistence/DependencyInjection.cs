using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pliaforma.Application.Common.Interfaces;
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
        services.AddScoped<IClassSessionCapacityService, EfClassSessionCapacityService>();
        services.AddScoped<IMakeupCreditLock, EfMakeupCreditLock>();
        services.AddScoped<IStudentPackageCreditService, EfStudentPackageCreditService>();

        return services;
    }
}
