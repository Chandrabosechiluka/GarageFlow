# Run GarageFlow from scratch, then tour the code

## 0. One-time install (skip what you already have)
1. **Visual Studio Community** (free): https://visualstudio.microsoft.com/downloads/ . In the installer tick the workload **ASP.NET and web development**. Leave ".NET 8" selected (it is included). Install, then restart the PC if asked.
2. **.NET 8 SDK**: check with `dotnet --list-sdks` (you need a line starting with `8.`). If missing: `winget install Microsoft.DotNet.SDK.8`.
3. **Docker Desktop**: https://www.docker.com/products/docker-desktop/ (SQL Server runs in it).
4. **Node.js 22 LTS** (for the front end): `winget install OpenJS.NodeJS.LTS`, then `node -v`.
5. **Git**: `git --version`. Chrome is needed only for the front-end tests.

## 1. Start everything (after closing all apps), in order
Open **PowerShell**.

```powershell
# 1. Start Docker Desktop from the Start menu and wait until it says "Engine running".

# 2. Database
cd F:\projects\GarageFlow
docker compose up -d
docker ps                       # garageflow-sqlserver should say "healthy" after ~30 s

# 3. API  (option A: command line)
dotnet run --project src/GarageFlow.Api
#   leave this window open. Swagger: http://localhost:5080/swagger

# 4. Front end (a SECOND PowerShell window)
cd F:\projects\GarageFlow\web
npm start
#   open http://localhost:4200  and sign in with a demo account (see README, password Garage@12345)
```

**Option B: start the API from Visual Studio** (best for learning, because you can use breakpoints)
1. Open `F:\projects\GarageFlow\GarageFlow.sln`.
2. In Solution Explorer right-click **GarageFlow.Api**, choose **Set as Startup Project**.
3. In the toolbar pick the **http** profile (not IIS Express, not Docker) and press **F5**. The port must be 5080; if VS picks another one, open `src/GarageFlow.Api/Properties/launchSettings.json` and check `applicationUrl`.
4. Steps 1, 2 and 4 above are still needed (Docker, database, `npm start`).

The connection string and JWT key come from user-secrets (see README step 3). If login fails with a 500 or the API says the signing key is missing, redo README steps 3 and 3b.

## 2. Stop everything
- API or `npm start`: click the window and press **Ctrl+C**. In Visual Studio press **Shift+F5**.
- Database: `docker compose stop` (keeps data) or `docker compose down` (removes the container, keeps the data volume).
- Whole stack in containers instead: `docker compose --profile api --profile web up --build -d`, then http://localhost:8081. Stop with `docker compose --profile api --profile web down`. Do not run this and `dotnet run` together (both use port 5080).

## 3. Quick check that it is alive
- http://localhost:5080/health returns healthy; http://localhost:5080/health/db checks the database.
- Port already in use? `netstat -ano | findstr :5080` then `taskkill /PID <number> /F`.

---

# Code tour: four flows (open these files in this order)
Tip: in Visual Studio, put the cursor on a method and press **F12** (Go to Definition) to follow the call; **Shift+F12** shows who calls it. Put a breakpoint (F9) on the first line of each flow's controller method and call it from the UI or Swagger.

## Flow 1: Login and roles
1. `web/src/app/features/login/login.component.ts` (form, calls `AuthService.login`)
2. `web/src/app/core/auth.service.ts` and `api.service.ts` (stores the session, `login()`)
3. `src/GarageFlow.Api/Controllers/AuthController.cs`
4. `src/GarageFlow.Application/Services/AuthService.cs` → `LoginAsync` (find user, check password)
5. `src/GarageFlow.Infrastructure/Auth/Pbkdf2PasswordHasher.cs` (how the password is checked)
6. `src/GarageFlow.Infrastructure/Auth/JwtTokenGenerator.cs` (what goes into the token)
7. `src/GarageFlow.Api/Program.cs` → `AddAuthorization` and the `FallbackPolicy` (secure by default), and `Security/Roles.cs`
8. Back in the browser: `core/interceptors.ts` adds the token, `core/guards.ts` blocks screens.
**Be able to say:** what is in the JWT, why the fallback policy exists, why hiding buttons is not security.

## Flow 2: Book an appointment (the most important one)
1. `web/src/app/features/booking/booking.component.ts` → `book()`
2. `src/GarageFlow.Api/Controllers/AvailabilityController.cs` and `src/GarageFlow.Application/Services/AvailabilityService.cs` (how slots are produced)
3. `src/GarageFlow.Domain/Scheduling/SlotFinder.cs` → `FindSlots`, `FreeTechnicians`, `FreeBays` (pure logic, no database)
4. `src/GarageFlow.Api/Controllers/AppointmentsController.cs` → book action
5. `src/GarageFlow.Application/Services/AppointmentService.cs` → `BookAsync` (line ~32)
6. `src/GarageFlow.Application/Abstractions/IBookingGate.cs` and `src/GarageFlow.Infrastructure/Persistence/SqlServerBookingGate.cs` → `RunExclusiveAsync` (the lock that stops two requests booking at once)
7. `src/GarageFlow.Domain/Entities/Appointment.cs` → `Book`, `ValidateStart`, the status transitions (`CanTransitionTo`)
8. `src/GarageFlow.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` (`RowVersion`, indexes, the unique indexes on technician/bay + start time)
9. `src/GarageFlow.Api/Errors/ApiExceptionHandler.cs` (how a conflict becomes a 409 problem-details response)
**Be able to say:** three layers of protection (check in code, lock per shop/time, database constraint), what 409 means, why time is UTC.
**Try it:** book the same slot in two browser tabs; read the error. Then find the test for it in `tests/`.

## Flow 3: Waitlist and offers
1. `src/GarageFlow.Application/Services/WaitlistService.cs` → `JoinAsync`, `AcceptOfferAsync`, `CancelAsync`
2. `src/GarageFlow.Application/Services/WaitlistOffers.cs` → `OfferAsync` (who gets a freed slot)
3. `AppointmentService.CancelAsync` (a cancellation triggers an offer)
4. `src/GarageFlow.Application/Services/MaintenanceService.cs` → `NotifyOffersAsync`, `ExpireOffersAsync`, `MarkNoShowsAsync`, `SendRemindersAsync`
5. `src/GarageFlow.Api/BackgroundJobs/MaintenanceWorker.cs` (the timer that runs it) and `Controllers/MaintenanceController.cs` (run it by hand)
6. `src/GarageFlow.Domain/Entities/WaitlistEntry.cs` (states: Waiting, Offered, Accepting, Fulfilled, Cancelled, Expired)
**Be able to say:** why offers expire, what happens if two people are waiting, the per-instance limit of the worker.

## Flow 4: Audit trail
1. `src/GarageFlow.Infrastructure/Persistence/GarageFlowDbContext.cs` → `SaveChangesAsync` override and the method near line 96 that adds `AuditLog` rows
2. `src/GarageFlow.Domain/Entities/AuditLog.cs`
3. `src/GarageFlow.Api/Security/HttpContextCurrentUser.cs` (where "who" comes from)
4. `src/GarageFlow.Application/Services/AuditService.cs` and `Controllers/AuditController.cs`
5. `web/src/app/features/audit/audit.component.ts`
**Be able to say:** why it is done at save time and not in each controller, what happens to the audit row if the save fails (same transaction).

## Also know the shape of the solution
- `Domain` → rules, no dependencies. `Application` → use cases and interfaces. `Infrastructure` → EF Core, JWT, SQL Server. `Api` → HTTP only. Start at `src/GarageFlow.Api/Program.cs` and read it top to bottom once: it shows every moving part (auth, CORS, rate limit, middleware order, background worker).
- Dependencies point inwards: Domain references nothing. Check the `.csproj` files to prove it.
- Tests: `tests/` mirrors the layers. Run one test in Test Explorer with a breakpoint (right-click → Debug) to see a flow without the UI.

## Suggested 3-evening plan
- Evening 1: install and start everything, read `Program.cs`, do Flow 1.
- Evening 2: Flow 2 with breakpoints, then the two-tab conflict experiment.
- Evening 3: Flows 3 and 4, then answer the 14 questions in `portfolio-and-interview.md` out loud.
