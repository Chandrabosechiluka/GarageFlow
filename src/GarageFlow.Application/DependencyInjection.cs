using GarageFlow.Application.Services;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
