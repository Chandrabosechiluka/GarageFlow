using GarageFlow.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GarageFlow.Application;

public static class DependencyInjection
{
    /// <summary>Registers the Application services. The Api calls this once at startup.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped = one instance per HTTP request, matching the lifetime of the DbContext they use.
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IAuditService, AuditService>();

        // Background jobs. TryAdd = the Api may register its own configured settings first; this is only the fallback.
        services.TryAddSingleton(new MaintenanceSettings());
        services.AddScoped<IMaintenanceService, MaintenanceService>();

        return services;
    }
}
