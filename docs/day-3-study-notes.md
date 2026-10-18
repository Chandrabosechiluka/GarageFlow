# Day 3 study notes: the API on top of the domain

Goal: be able to follow one HTTP request from the browser down to SQL Server and back, and say what each layer did.

## What was added
- **Application layer:** DTOs (`Contracts/`), three services (`Services/`), `IGarageFlowDbContext`, `IClock`, two exceptions (`NotFoundException`, `ConflictException`).
- **Infrastructure:** `GarageFlowDbContext` now implements `IGarageFlowDbContext`; `SystemClock`; `DbSeeder` (demo data).
- **Api:** three controllers, `ApiExceptionHandler`, Swagger, JSON enums as text, automatic migration plus seeding in Development only.
- **Tests:** `GarageFlow.Application.Tests` (16 tests: booking rules, clashes, customers).

## Follow one request: `POST /api/appointments`
1. `AppointmentsController.Book` receives JSON, ASP.NET turns it into `BookAppointmentRequest` and validates it.
2. The controller calls `IAppointmentService.BookAsync` (DI hands it the `AppointmentService`).
3. The service loads customer, vehicle, service, technician and bay through `IGarageFlowDbContext` (EF Core turns LINQ into SQL).
4. It checks things that need several entities: vehicle belongs to customer, technician has the skill, bay and technician are in the same shop.
5. `Appointment.Book(...)` (Domain) checks UTC and "in the future" and works out the end time.
6. A query checks for an overlapping appointment for the same technician or bay.
7. `SaveChangesAsync` inserts the row. The controller returns `201 Created` with the new appointment.
8. If anything throws, `ApiExceptionHandler` converts it to 400, 404, 409 or 500 in one standard JSON shape.

## Read in this order
1. `Application/Contracts/*`: the request and response shapes, and the validation attributes.
2. `Application/Services/AppointmentService.cs`: the main use case.
3. `Application/Services/CustomerService.cs` and `CatalogService.cs`.
4. `Api/Controllers/*`: note how thin they are.
5. `Api/Errors/ApiExceptionHandler.cs` and `Api/Program.cs`.
6. `Infrastructure/DependencyInjection.cs` and `Persistence/DbSeeder.cs`.
7. `tests/GarageFlow.Application.Tests/*`.

## Concepts to be able to explain
- **Why DTOs** instead of returning entities (contract stability, no leaking of internals, no circular references, private setters).
- **Why services** exist: controllers only translate HTTP; services run the use case; the domain owns the rules. Where does each kind of rule live?
- **`IGarageFlowDbContext`** in Application, implemented in Infrastructure: the dependency points inward (dependency inversion).
- **Dependency injection lifetimes:** scoped (per request) for services and DbContext, singleton for the clock.
- **Global exception handling** versus try/catch in every controller; why a 500 never shows the real message.
- **HTTP status codes:** 201 Created, 400 (invalid input or broken rule), 404, 409 (conflict).
- **Validation in two layers:** DataAnnotations on DTOs (shape of the request) and domain rules (business meaning).
- **AsNoTracking** for reads, tracked entities for writes.
- **UTC at the edges:** why a date without `Z` is rejected on booking and how query-string dates are normalised.
- **Seeding** only in Development, and why `MigrateAsync` on startup is fine locally but not how you would deploy to production.
- **Known gap on purpose:** two simultaneous bookings can both pass the clash check. Day 6 fixes it with a transaction and the row version. Be ready to say this out loud; it shows you know the limit of your own code.

## Self-test questions
1. Why does `AppointmentService` not call `DateTime.UtcNow`?
2. Where would you add the rule "a customer can have at most 3 active appointments"? Domain, service, or controller, and why?
3. What happens if you POST a customer with an email that already exists? Which line decides the status code?
4. Why do status changes use `POST /{id}/check-in` rather than `PUT /{id}` with a new status?
5. Why must the Application project not reference SQL Server?
6. What does `[property: Required]` on a record parameter do?

## Try it in Swagger
1. `GET /api/customers` and copy the demo customer's `id` and the vehicle's `id`.
2. `GET /api/service-types`, `/api/technicians`, `/api/bays` and copy one id from each. Pick a technician who has the skill the service needs.
3. `POST /api/appointments` with a start time like `2026-11-03T09:00:00Z`.
4. Book the same technician at `2026-11-03T09:30:00Z`: expect **409**.
5. Send the start time without the `Z`: expect **400**.
6. Walk the lifecycle: `check-in`, `start-inspection`, `inspection-items`, `complete`. Then try `check-in` again and read the error.
7. Cancel without a reason: expect **400** from validation.

## Do it yourself
- Add `GET /api/customers/{id}/appointments` (service method, controller action, one test).
- Write the test for the clash check using a different date, first to fail, then to pass.
- Commit style: `feat(api): add customer and appointment endpoints`.
