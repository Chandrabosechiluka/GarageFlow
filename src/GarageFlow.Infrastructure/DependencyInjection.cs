using GarageFlow.Application.Abstractions;
using GarageFlow.Infrastructure.Auth;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers everything the Infrastructure layer provides. The Api calls this once at startup.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GarageFlowDbContext>(options => options.UseSqlServer(connectionString));

        // Whoever asks for IGarageFlowDbContext gets the SAME per-request GarageFlowDbContext instance.
        services.AddScoped<IGarageFlowDbContext>(sp => sp.GetRequiredService<GarageFlowDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IBookingGate, SqlServerBookingGate>();

        // Authentication helpers. JwtOptions is bound from configuration by the Api (Configure<JwtOptions>).
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
