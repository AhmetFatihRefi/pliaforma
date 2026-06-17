using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pliaforma.Application.Common.Behaviors;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Auth;

namespace Pliaforma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<AuthSessionService>();
        services.AddScoped<Features.Bookings.BookingCreator>();
        services.AddScoped<Features.MakeupCredits.MakeupCreditService>();
        services.AddScoped<Features.Scheduling.SessionCancellationService>();
        services.AddScoped<Features.Packages.StudentPackageConsumer>();
        services.AddScoped<Features.Attendance.AttendanceMarker>();
        services.AddScoped<Features.Notifications.ReminderScanner>();

        RegisterHandlers(services, assembly);

        // Pipeline sırası (dıştan içe çalışır): Logging → Validation → StudioStatus →
        // MembershipGuard → Transaction → Handler.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(StudioStatusBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MembershipGuardBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IRequestHandler<,>);
        var registrations = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => new { Service = i, Implementation = t }));

        foreach (var reg in registrations)
            services.AddScoped(reg.Service, reg.Implementation);
    }
}
