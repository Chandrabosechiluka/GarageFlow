using System.Text.Json.Serialization;
using GarageFlow.Api.Errors;
using GarageFlow.Application;
using GarageFlow.Infrastructure;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GarageFlow")
    ?? throw new InvalidOperationException("Connection string 'GarageFlow' is not configured.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddControllers()
    // Enums travel as readable text ("Booked") instead of numbers (1).
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health/db", async (GarageFlowDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { database = "Reachable" })
        : Results.Problem("The database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();
