using System.Text;
using System.Text.Json.Serialization;
using GarageFlow.Api.Errors;
using GarageFlow.Application;
using GarageFlow.Application.Abstractions;
using GarageFlow.Infrastructure;
using GarageFlow.Infrastructure.Auth;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GarageFlow")
    ?? throw new InvalidOperationException("Connection string 'GarageFlow' is not configured.");

// The signing key is a secret: anyone who has it can forge tokens for any user and role.
// It lives in user secrets locally (and in a secret store in production), never in Git.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey == "SET_IN_USER_SECRETS" || jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or too short (needs at least 32 characters). Set it with: " +
        "dotnet user-secrets set \"Jwt:SigningKey\" \"<a long random string>\" --project src/GarageFlow.Api");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the short claim names ("role", "sub") exactly as the token has them.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint needs a signed-in user unless it says [AllowAnonymous].
    // Forgetting an attribute therefore fails closed instead of leaving an endpoint open.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services
    .AddControllers()
    // Enums travel as readable text ("Booked") instead of numbers (1).
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Adds the "Authorize" button to Swagger UI so you can paste a token once and call secured endpoints.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Log in with POST /api/auth/login, then paste only the accessToken value here."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Development convenience: create/upgrade the database and add demo data on startup.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    await DbSeeder.SeedUsersAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
}

// Order matters: first find out WHO the caller is (authentication), then decide what they MAY do (authorization).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

app.MapGet("/health/db", async (GarageFlowDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { database = "Reachable" })
        : Results.Problem("The database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.Run();
