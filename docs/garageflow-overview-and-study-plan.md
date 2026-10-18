# GarageFlow: overview of what we built (Day 1 and Day 2) and what to study next

Read this first. It explains the project in plain words, step by step, then lists what to study. When something is unclear, write the question down and bring it.

---

## 1. What GarageFlow is (the 30-second pitch)

GarageFlow is a backend API for car service centers. A customer books a service for their vehicle. The system picks a qualified technician and a free bay. The appointment then moves through check-in and inspection until it is completed.

Say it like this in an interview: "It is a .NET 8 layered API with SQL Server and EF Core. The interesting part is the appointment lifecycle, which is a state machine enforced in the domain layer, and the double-booking prevention I am building on top of it."

---

## 2. The story so far, one step at a time

### Step 1: Solution and layers (Day 1)
We created four projects plus a test project:

| Project | Job | Knows about |
|---|---|---|
| `GarageFlow.Domain` | Business objects and rules | Nothing (no framework at all) |
| `GarageFlow.Application` | Use cases, DTOs, validation (empty for now) | Domain |
| `GarageFlow.Infrastructure` | Database, EF Core, later jobs | Application, Domain |
| `GarageFlow.Api` | HTTP endpoints, startup | Application, Infrastructure |

Why: the arrows only point inward. Business rules never depend on the database or the web framework, so they are easy to test and to change.
Files: `Directory.Build.props` (shared settings: net8.0, nullable, implicit usings) and the `.csproj` files.

### Step 2: The domain model (Day 1)
Entities: Shop, Customer, Vehicle, ServiceType, Technician, Bay, Appointment, InspectionItem, AuditLog.
Key habits you can point at in the code:
- Private setters and constructors that validate. An object can never exist in an invalid state.
- `DomainException` for broken business rules.
- Technician exposes `IReadOnlyCollection<Skill>` so outsiders cannot modify the list directly.

### Step 3: The appointment state machine (Day 1, the heart of the project)
```
Booked -> CheckedIn -> InInspection -> Completed
Booked -> Cancelled | NoShow
CheckedIn -> Cancelled
```
- A dictionary (`AllowedTransitions`) lists the legal moves. Anything else throws `DomainException`.
- Status changes only through methods (`CheckIn`, `StartInspection`, `Complete`, `Cancel`, `MarkNoShow`), never `appointment.Status = ...`.
- Cancelling needs a reason. Completing needs at least one inspection item.
- Times are UTC. `Book` receives `nowUtc` as a parameter, so tests do not depend on the real clock.
- `OverlapsWith` uses half-open intervals: 09:00-10:00 and 10:00-11:00 do not clash.

### Step 4: Unit tests (Day 1)
37 xUnit tests in `GarageFlow.Domain.Tests`. They prove each rule above. Habit to keep: write the failing test first, then the code.

### Step 5: Git and GitHub (Day 1)
Repo created, `.gitignore` written (it ignores `bin/obj`, `.env`, `appsettings.Development.json`), first commit pushed. Commit style: `feat(domain): ...`.

### Step 6: SQL Server in Docker (Day 2)
`docker-compose.yml` starts SQL Server 2022 in a container.
- Port `1433:1433` lets your PC talk to the container.
- The volume `sqlserver-data` keeps the data when the container stops.
- The SA password comes from `.env`, which Git ignores. `.env.example` shows the shape without the real password.
- Gotcha you hit: the password is fixed the first time the container starts. Changing `.env` later does nothing until `docker compose down -v` deletes the volume. This is a good "challenges" story.

### Step 7: Connecting the API to the database (Day 2)
- `Infrastructure/DependencyInjection.cs` has `AddInfrastructure(connectionString)`, which registers `GarageFlowDbContext` with `UseSqlServer`.
- The real connection string lives in .NET **user secrets** (stored outside the repo). `appsettings.json` only holds a placeholder.
- `Program.cs` reads the connection string, calls `AddInfrastructure`, and exposes `/health` and `/health/db`. The second one runs `CanConnectAsync`, which showed up as `SELECT 1` in your log.

### Step 8: Mapping entities to tables with EF Core (Day 2)
One configuration class per entity in `Infrastructure/Persistence/Configurations/`, loaded together by `ApplyConfigurationsFromAssembly`. The decisions worth remembering:

| Decision | Why |
|---|---|
| `DeleteBehavior.Restrict` on appointment foreign keys | Deleting a customer must never silently erase appointment history; also avoids SQL Server's "multiple cascade paths" error |
| Inspection items cascade with their appointment | They belong to it and make no sense alone |
| `RowVersion` (`IsRowVersion`) | SQL Server changes it on every update, so EF can detect two people editing the same row (optimistic concurrency) |
| Enums stored as text | Readable in SQL, safe if enum order changes |
| `UtcDateTimeConverter` | `datetime2` forgets it is UTC; the converter restores `DateTimeKind.Utc` on read |
| Technician skills in one column (`SkillsStorage`, private) | Keeps the public API as a read-only collection of enums while storing a simple comma-separated value |
| Unique indexes (customer email, vehicle registration per customer) | The database enforces uniqueness even if code has a bug |
| Indexes on `(TechnicianId, StartTimeUtc)` and `(BayId, StartTimeUtc)` | Make "who or what is busy in this time range?" fast for slot availability later |

### Step 9: Migrations (Day 2)
`dotnet ef migrations add InitialCreate` generated C# code (`Up` creates the tables, `Down` removes them) plus a **model snapshot**. `dotnet ef database update` ran it against Docker SQL Server.
- `migrations add` = write the plan. `database update` = execute the plan.

### Step 10: Tests for the mapping (Day 2)
9 tests in `GarageFlow.Infrastructure.Tests` build the EF model without opening a database and check the decisions in the table above. About 46 tests in total.

---

## 3. Where each thing lives

```
GarageFlow/
  docker-compose.yml, .env.example      Step 6
  src/GarageFlow.Domain/                Steps 2-3
  src/GarageFlow.Infrastructure/
    DependencyInjection.cs              Step 7
    Persistence/GarageFlowDbContext.cs  Step 8
    Persistence/Configurations/*.cs     Step 8
    Persistence/Converters/             Step 8
    Migrations/                         Step 9
  src/GarageFlow.Api/Program.cs         Step 7
  tests/GarageFlow.Domain.Tests/        Step 4
  tests/GarageFlow.Infrastructure.Tests/ Step 10
  docs/day-1-study-notes.md, day-2-study-notes.md
```

---

## 4. Tomorrow's study list (Saturday, no new building)

Aim for about 3 hours in total. Bring every question you have.

**A. C# and .NET fundamentals behind the code (1 hour)**
1. Encapsulation: private setters, private constructors, `IReadOnlyCollection<T>` versus `List<T>`.
2. Exceptions: custom exceptions, `throw`, `try/catch`.
3. Dictionaries and `Contains` (the transition table).
4. `DateTime` and `DateTimeKind`: UTC, Local, Unspecified.
5. Dependency injection basics: what `AddInfrastructure` registers and what "scoped lifetime" means for a DbContext.

**B. EF Core and SQL Server (1 hour)**
1. DbContext as a unit of work; DbSet; change tracking.
2. Fluent API versus data annotations.
3. Migrations: `Up`, `Down`, snapshot. Open the `Migrations` folder and read it.
4. Foreign keys, cascade versus restrict, unique and composite indexes.
5. Optimistic versus pessimistic concurrency.
6. In Visual Studio: View, then SQL Server Object Explorer; connect to `localhost,1433` and look at the `Appointments` table.

**C. Docker basics (30 minutes)**
Image versus container, ports, volumes, what `down` versus `down -v` does, why `.env` is not committed.

**D. Interview practice (30 minutes)**
Answer these aloud, one minute each:
1. Why layered architecture?
2. Why private setters and a state machine in the domain?
3. Why Restrict instead of Cascade?
4. What is a migration?
5. Tell me about a problem you hit and how you fixed it (use the Docker/virtualization or the SA password story).

**E. Self-test**
Answer the self-test questions at the end of `docs/day-1-study-notes.md` and `docs/day-2-study-notes.md`.

**Optional, only if you have energy:** the Saturday coding task from Day 1 (a Booked appointment cannot be cancelled less than 2 hours before start; test first).

---

## 5. What comes next

Day 3 (Monday): controllers, DTOs, validation, seed data, Swagger, so you can book an appointment from a browser. After that: JWT auth, slot availability, double-booking prevention with transactions and the row version, background jobs, integration tests, Docker image, CI, Azure.
