# Day 11: making it deployable (production readiness)

Deploying is not only "copy the image somewhere". A fresh production environment differs from your laptop in four ways, and each one needed code. Nothing here needs Azure or a credit card: you can try all of it on localhost.

## 1. A new database is empty, and nobody can log in
In Development the API adds demo data and demo users (public password), so a real environment must **never** use that path. The old gap: an empty Production database had no shop and no user, so the API was unusable.

**`ProductionBootstrapper`** runs at startup (only when migrations are enabled and the environment is not Development):
- no shop yet -> create one from `Bootstrap__ShopName`, `OpeningTime`, `ClosingTime`, `TimeZoneId`, plus the standard services;
- no user yet -> create one **Admin** from `Bootstrap__AdminEmail` and `Bootstrap__AdminPassword`.

Design choices to explain:
- **Idempotent**: it only fills in what is missing and never overwrites. Running it on every start is safe, and changed settings later do nothing.
- **Fail fast on bad configuration**: a password under 12 characters, a time like `9am`, or an unknown time zone stops the start with a clear message instead of creating a weak or half-working system.
- **Does not crash when the admin settings are absent**: it logs a warning. A crashing container is restarted forever by the platform and hides the real cause.
- **The password is never logged**, and it is stored only as a PBKDF2 hash. After the first start, remove `Bootstrap__AdminPassword` from the environment.
- It lives in Infrastructure (it needs the DbContext and the hasher); the settings come from configuration, never from the repository.

## 2. The Admin can set the shop up through the API
Previously technicians, bays and services could only be inserted by the seeder. New Admin-only endpoints: `POST /api/technicians`, `/api/bays`, `/api/service-types` (in `CatalogService`). Rules: names are trimmed; duplicates (ignoring case) give **409**; a technician needs at least one valid skill; a service duration must be 5-600 minutes; the shop is resolved with the same `ShopLookup` the other services use. Reading stays open to every signed-in user. Every creation lands in the audit trail automatically (Day 9).

## 3. Hardening, all through configuration
The same image runs in every environment; the environment variables decide.
- **Swagger** lists every endpoint, so it is on only in Development unless `Swagger__Enabled=true`.
- **CORS**: browsers block a web page from calling another address unless the API allows it. The allow-list is `Cors__AllowedOrigins__0...`, empty by default. CORS protects *browsers*, not the API itself: tokens and roles still do the real protection.
- **Rate limiting** on `POST /api/auth/login`: a fixed window per caller address (10 per minute by default), then **429**. Without it, passwords can be guessed at machine speed. Other endpoints are not limited.
- **Behind a proxy**: on Azure and most hosts, every request reaches your container from the platform's load balancer, so the rate limiter would see one single "caller". `Hosting__BehindProxy=true` makes the API read the real address from `X-Forwarded-For`. It must stay **off** when the API is reachable directly, otherwise anyone could send a fake header and dodge the limit (a test proves the header is ignored in that case).
- `X-Content-Type-Options: nosniff` on every response.
- The limiter reads its numbers when first used, not at startup. That is why the tests can start the same app again with different settings.

## 4. What is proven by tests
- Application tests: catalog creation rules, duplicates, a new technician and bay can be booked, bootstrapper behaviour (creates, idempotent, never overwrites, bad settings).
- API tests (`ProductionReadinessTests`): Admin creates the catalog, advisor and technician get 403, 409 and 400 cases, Swagger open in Development and closed in Production, CORS allowed vs unknown origin, `nosniff`, 429 after the limit, per-address allowance behind a proxy, forged header ignored without a proxy.

## Try it yourself
1. `dotnet user-secrets` stays as is. Edit `.env`: `API_ENVIRONMENT=Production`, `BOOTSTRAP_ADMIN_EMAIL=you@example.com`, `BOOTSTRAP_ADMIN_PASSWORD=<12+ characters>`, plus `JWT_SIGNING_KEY`.
2. Use a clean database so you see a real first start: `docker compose --profile api down -v` (this deletes the data volume), then `docker compose --profile api up --build -d`.
3. `docker logs garageflow-api` shows the two "Bootstrap:" lines. `http://localhost:5080/swagger` now returns 404. Log in with `POST /api/auth/login` (use curl or Postman), then add a technician and a bay with the token.
4. Send 11 wrong logins quickly: the last ones return 429.
5. Remove `BOOTSTRAP_ADMIN_PASSWORD` from `.env`, restart, and log in again: nothing was reset.
6. Put `API_ENVIRONMENT=Development` back when you finish.

## Questions to practise out loud
1. Why must the demo users never exist in a real environment, and how does the bootstrapper avoid that?
2. Why does the bootstrapper refuse a short password instead of just warning?
3. Why is the bootstrapper idempotent, and what would go wrong if it overwrote the shop on every start?
4. What does CORS protect, and what does it not protect?
5. Why does the rate limiter need `X-Forwarded-For` behind a load balancer, and why is trusting that header dangerous when there is no proxy?
6. Why is the signing key an environment variable and not part of the image?
7. A container "starts" but nobody can log in. Which three things do you check?
8. Known limits you would name honestly: one shop only, fixed-window limiter is per instance (several instances each have their own counters), the maintenance worker would run once per instance, no HTTPS inside the container (the platform terminates it), no automated image push yet.
