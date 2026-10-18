# GarageFlow: resume, pitch and interview prep

## Resume bullets (pick 3 to 4, keep numbers you can defend)
**GarageFlow: car-service appointment platform** | C#, .NET 8, ASP.NET Core, EF Core, SQL Server, Angular, Docker, GitHub Actions
- Built a layered .NET 8 Web API (Domain, Application, Infrastructure, API) with JWT authentication and role-based access (Admin, Advisor, Technician), secure by default so unknown routes return 401.
- Designed appointment booking that prevents double-booking under concurrent requests (database constraints and optimistic concurrency), with availability by technician skill and bay, stored in UTC and shown in the shop's time zone.
- Implemented a waitlist with timed offers, background jobs for no-shows, reminders and offer expiry, an automatic audit trail, paging, and problem-details errors with correlation ids.
- Wrote 250 backend tests (unit and integration against a real SQL Server) plus 19 frontend tests; CI builds and tests on every push; the full stack runs with one `docker compose` command.
- Built an Angular 19 front end (standalone components, signals, route guards, interceptors) with a live bay board, booking with real-time slots, and admin screens.

## 60-second pitch
"GarageFlow is a booking system for a car service shop. I built it because at Solera I worked on ServiceSuite, the appointments and check-in side of dealer service, and I wanted to build that kind of system end to end myself. The backend is .NET 8 with clean layering and SQL Server. The hard parts were making sure two advisors can't book the same bay and time, handling time zones properly, and a waitlist that offers a freed slot to the next customer for two hours. It has 250 backend tests, CI, Docker, and an Angular front end on top."

## Interview questions and short answers
1. **Why layered architecture?** Domain holds the rules and knows nothing about the database; Application holds use cases; Infrastructure holds EF Core; API only translates HTTP. It keeps rules testable without a database and lets me swap the data layer.
2. **How do you stop double-booking?** The rule is checked in code for good messages, but the guarantee comes from the database (a constraint and concurrency token). If two requests race, one wins and the other gets a 409, which the UI turns into "that time was just taken, pick another".
3. **Why store UTC?** UTC is unambiguous and survives daylight saving. The shop has a time zone; the API returns it with availability and the UI converts for display, so a traveller still sees the garage's clock.
4. **How does auth work?** Login returns a JWT with role claims. Controllers use role policies; a fallback policy requires authentication everywhere, so a forgotten attribute fails closed.
5. **Why does the UI hide buttons if the API checks roles anyway?** Hiding is convenience. The API is the real security boundary because anyone can call it directly.
6. **Where do you keep the token in the browser, and what's the risk?** sessionStorage: simple, cleared when the tab closes, but readable by injected script (XSS). A more secure option is an HttpOnly cookie with CSRF protection. I documented the trade-off.
7. **How do background jobs work?** A hosted service runs maintenance periodically (no-shows, reminders, offer expiry). The same job can be triggered from the admin page. Limit: it runs per instance, so scaling out needs a lock or a proper scheduler.
8. **How do you handle errors?** One problem-details shape with a correlation id. The UI maps it to friendly text and never shows server internals for 5xx.
9. **How did you test it?** Domain and application tests are fast and isolated. Integration tests call the real API against SQL Server. I found a real bug this way: validation attributes on record parameters were silently ignored until I put them on the constructor parameter.
10. **What would you change for production?** HTTPS termination, secrets in a vault, a distributed rate limiter and job lock, cookie-based auth, multi-shop support, and end-to-end browser tests.
11. **Why Docker and CI?** Anyone can run the stack with one command, and every push builds and runs all tests, so a broken change is caught immediately.
12. **How is pagination done and why?** Server-side with page and pageSize, returning total counts, so large tables don't load fully.
13. **Why an audit trail?** Every change records who, when and what. It's written automatically at save time so it can't be forgotten in a controller.
14. **How does the waitlist avoid races when a slot frees?** Offers are single-use with an expiry and accepting goes through the same booking rules, so a stale offer fails with a clear message.

Be ready to say what you would NOT claim: one shop only, no HTTPS inside the container, Azure deployment documented but not run.

## Before you share the repo
- Add 3 to 4 screenshots (login, bay board, booking, audit) to `docs/screenshots/` and link them at the top of the README.
- Pin the repo on GitHub, add a one-line description and topics (dotnet, angular, sql-server, docker).
- Re-read your own code for the two parts you'll be asked about most: booking concurrency and the time zone handling.
