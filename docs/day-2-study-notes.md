# Day 2 study notes: SQL Server, EF Core and migrations

Goal: be able to explain how a C# object ends up as a row in SQL Server, and why each mapping choice was made.

## Read in this order
1. `docker-compose.yml` and `.env.example`: what a container, a port mapping and a volume are doing here.
2. `Infrastructure/Persistence/GarageFlowDbContext.cs`: the DbContext and its DbSets.
3. `Persistence/Configurations/AppointmentConfiguration.cs`: the richest mapping (foreign keys, row version, indexes).
4. `Persistence/Configurations/TechnicianConfiguration.cs` plus the private `SkillsStorage` in `Domain/Entities/Technician.cs`.
5. `Persistence/Converters/UtcDateTimeConverter.cs`.
6. `Api/Program.cs` and `appsettings.json`: how the connection string reaches the DbContext.
7. `tests/GarageFlow.Infrastructure.Tests/ModelTests.cs`: what each test proves.
8. After you run it: the generated `Migrations/` folder (`Up`, `Down`, and the model snapshot).

## Concepts to be able to explain
- DbContext is a **unit of work** (tracks changes, saves them together); a DbSet is like a repository for one entity.
- **Fluent API configuration classes** instead of attributes: why the Domain project stays free of EF Core.
- What a **migration** is, what `Up` and `Down` do, and why the **model snapshot** exists.
- Why the real password lives in **user secrets** and `.env`, not in Git. What would happen if it were committed?
- **Cascade vs Restrict**: why appointment foreign keys use Restrict, and the SQL Server "multiple cascade paths" error.
- **Row version** (`rowversion`): how SQL Server detects two people editing the same row.
- Why enums are stored as **text**, and the trade-off (readable data vs slightly larger rows).
- Why `datetime2` loses the UTC flag and how the converter restores it.
- Docker basics: image vs container, why the data survives `docker compose down` (the volume) but not `docker compose down -v`.
- `TrustServerCertificate=True`: what it skips and why it is acceptable for local development only.

## Self-test questions
1. What does `ApplyConfigurationsFromAssembly` save you from doing?
2. If you delete a Customer who has appointments, what happens, and which line of code decides that?
3. Why does the `Technician.Skills` property have no column, and where do the values go?
4. A teammate pulls your branch. What exactly must they run to get a working database?
5. What is the difference between `dotnet ef migrations add` and `dotnet ef database update`?
6. Why are there indexes on `(TechnicianId, StartTimeUtc)` and `(BayId, StartTimeUtc)`?

## Do it yourself
- Run `dotnet ef migrations script --project src/GarageFlow.Infrastructure --startup-project src/GarageFlow.Api` and read the SQL EF Core generated. Find the Appointments table and its foreign keys.
- Practise the migration workflow: add `public bool IsActive { get; private set; } = true;` to `Bay`, run `dotnet ef migrations add AddBayIsActive ...`, read the migration, apply it, then undo it with `dotnet ef database update InitialCreate` followed by `dotnet ef migrations remove`.
- Commit style: `feat(infra): map domain entities to SQL Server`.
