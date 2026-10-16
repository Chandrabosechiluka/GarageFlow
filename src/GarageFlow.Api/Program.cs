using GarageFlow.Infrastructure;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GarageFlow")
    ?? throw new InvalidOperationException("Connection string 'GarageFlow' is not configured.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health/db", async (GarageFlowDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { database = "Reachable" })
        : Results.Problem("The database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();
