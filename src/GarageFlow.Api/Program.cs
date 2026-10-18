using System.Text;
using System.Text.Json.Serialization;
using GarageFlow.Api.BackgroundJobs;
using GarageFlow.Api.Errors;
using GarageFlow.Api.Middleware;
using GarageFlow.Api.Security;
using GarageFlow.Application;
using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Services;
using GarageFlow.Infrastructure;
using GarageFlow.Infrastructure.Auth;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// One line per log entry, with the request's correlation ID (a logging "scope") printed in front.
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = true;
    options.TimestampFormat = "HH:mm:ss ";
});

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
// Background job settings (grace period, reminder lead time...). Registered before AddApplication so these values win.
var maintenance = builder.Configuration.GetSection("Maintenance").Get<MaintenanceSettings>() ?? new MaintenanceSettings();
builder.Services.AddSingleton(maintenance);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// Who is calling: lets the DbContext write the user's e-mail into the audit trail.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

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

// Runs the maintenance jobs (no-shows, reminders, offer expiry) on a timer for as long as the API is up.
if (maintenance.Enabled)
    builder.Services.AddHostedService<MaintenanceWorker>();

builder.Services.AddProblemDetails(options =>
    // Also covers the empty 401/403/404 responses that UseStatusCodePages turns into JSON.
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext));

// Invalid input (a missing field, a too-long name) already becomes a 400 automatically; this adds the correlation ID.
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
        problem.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });
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

// Order matters. Correlation ID first so everything after it is tagged; request logging next so it sees the FINAL
// status; then the exception handler, which turns any exception into a problem-details response.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401 / 403 / 404 responses become problem-details JSON too

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
