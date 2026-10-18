# Day 10: API integration tests, Docker image, CI

## 1. API-level integration tests
Until now the tests called services directly. These start the **whole API in memory** with `WebApplicationFactory<Program>` and call it with a real `HttpClient`:

routing -> authentication -> authorization -> model validation -> controller -> service -> EF Core -> SQL Server -> error handler/middleware -> JSON.

What they prove (`ApiEndpointTests`): health is public; no token = 401 problem-JSON with a correlation ID; wrong password = 401; a technician cannot book (403) but an advisor can (201 + `Location` header, enum as text); double booking = 409; invalid body = 400 with field errors; unknown id = 404; pagination and its limits; reschedule and cancel over HTTP; only an Admin can use `allowLate`; the audit trail records each step with the advisor's e-mail; roles protect the audit endpoints.

How it is built (worth understanding):
- **`public partial class Program {}`** at the end of `Program.cs` makes the entry point visible to the test project.
- **One shared database**: the fixture creates one database per test run, the API migrates and seeds it by itself (it runs in `Development`), tests share it, and it is deleted at the end. Each test books on its own future day so tests never collide.
- **Configuration through environment variables**, not `appsettings`: environment variables beat both appsettings and your **user secrets**, so these tests can never touch your real development database. Remember this trap: a test that "just overrides the connection string" but loses to user secrets would silently run (and delete!) the wrong database.
- The background worker is switched off (`Maintenance__Enabled=false`), so timers do not interfere.
- `GARAGEFLOW_REQUIRE_SQL=true` (set in CI) turns "SQL Server not found, skip" into a failure, so a broken CI setup can never produce a green build that tested nothing.

## 2. Docker image (`Dockerfile`)
**Multi-stage build**
1. *build* stage: the big .NET SDK image restores and publishes.
2. *runtime* stage: the small ASP.NET image gets only the published files. No SDK, no source code. Smaller, faster to pull, smaller attack surface.

Other choices to explain:
- **Layer caching**: copy the `.csproj` files first and `restore`, then copy the code. Docker re-runs only the steps after a changed file, so editing code does not re-download packages.
- **Non-root**: `USER $APP_UID` and port 8080 (ports below 1024 need root). A bug in the app cannot rewrite the container.
- **No secrets in the image**: `.dockerignore` excludes `.env`, and the connection string and signing key come in as environment variables when the container starts (`ConnectionStrings__GarageFlow` - the double underscore is how a `:` is written in an environment variable).
- **`Database__MigrateOnStartup=true`** lets a container apply migrations on start. Demo data and demo users still exist only in `Development`. In Production a first Admin user and the shop record must be created some other way (Day 11).
- Inside Docker, the database host is **`sqlserver`** (the other container's name), not `localhost`.

### Compose
`docker compose up -d` still starts only SQL Server (your dev workflow). `docker compose --profile api up --build -d` also builds and starts the API on http://localhost:5080/swagger. SQL Server now has a **healthcheck**, and the API waits until it is *healthy*, not merely *started*. Add `JWT_SIGNING_KEY=<40+ random characters>` to your `.env` first.

## 3. Continuous integration (`.github/workflows/ci.yml`)
On every push and pull request GitHub builds a clean machine, starts a real SQL Server as a *service container*, restores, builds in Release, runs **all** tests including the integration ones, uploads the test report, and (only if the tests pass) builds the Docker image.
- **Why**: "works on my machine" is not evidence. A clean machine proves that nothing depends on your PC (Windows paths, installed tools, an old DLL).
- `concurrency` cancels a superseded run; `permissions: contents: read` gives the workflow the least access it needs; the SQL password in the file is a throwaway for a container that lives for minutes, not a real secret (real secrets go in GitHub **Secrets**).
- It also shows the project works on **Linux**: a good check of the time zone code (IANA `Asia/Kolkata`) and file-name casing.

## Known gaps (on purpose)
- The image is built but not pushed anywhere, and nothing deploys it yet (Day 11).
- No code coverage report or code analysis step.
- No vulnerability scan of the image or the NuGet packages (`dotnet list package --vulnerable`).
- No `/ready` health endpoint separate from `/health`.

## Self-test questions
1. Why is a multi-stage Dockerfile better than one big image?
2. Why copy the `.csproj` files before the rest of the code?
3. Why does the compose file say `Server=sqlserver` and not `localhost`?
4. What does `ConnectionStrings__GarageFlow` mean, and why the double underscore?
5. What happens in CI if SQL Server fails to start, and why is that good?
6. Why do the API tests set environment variables instead of changing appsettings?
7. What is the difference between a unit test, a service-level integration test and an API-level one? Which finds which kind of bug?

## Do it yourself
- Add the CI status badge to the README: `![CI](https://github.com/<your-user>/GarageFlow/actions/workflows/ci.yml/badge.svg)`.
- Add `dotnet list package --vulnerable --include-transitive` as a CI step and read the output.
- Commit style: `ci: add Dockerfile, compose api service, API integration tests and GitHub Actions workflow`.

## A real bug the end-to-end tests found
The first run of the API tests failed with 500 on login. The body (shown because Development includes exception detail) said:
"Record type 'LoginRequest' has validation metadata defined on property 'Password' that will be ignored. ... validation metadata must be associated with the constructor parameter."

- Cause: the request records used `[property: Required]`. In a record, MVC reads validation attributes from the **constructor parameter**. Putting them on the generated property is refused with an exception.
- Fix: plain `[Required]` on the parameter (all request records in Contracts).
- Lesson: every unit test passed, and the bug was invisible until a real HTTP request went through model binding. That is exactly what endpoint tests are for. Any request that carried validation attributes would have returned 500 in production too.
