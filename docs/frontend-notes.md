# Front end notes

## Run it
1. API: `dotnet run --project src/GarageFlow.Api` (Development gives demo data and users, password `Garage@12345`).
2. `cd web && npm install && npm start`, open http://localhost:4200. The dev server proxies `/api` and `/health` to `http://localhost:5080` (`proxy.conf.json`), so the browser sees one origin.
3. Containers: `docker compose --profile api --profile web up --build -d`, open http://localhost:8081. nginx serves the build and proxies `/api` to the API container. Do not run `dotnet run` at the same time (port clash on 5080 only with the api container; stop one).

## Structure
- `core/`: API client (one typed method per endpoint), auth session, interceptors, guards, time zone maths, lookups cache.
- `features/`: one folder per screen, each lazy-loaded.
- `shared/`: small reusable pieces (modal, pager, badge, slot picker, customer modals).
- `styles.scss`: the design system (tokens, shell, tables, forms, modals).

## Decisions worth explaining in an interview
- **Roles in the UI are only convenience.** Guards and hidden buttons stop confusion; the API re-checks the role on every call.
- **Token in sessionStorage.** Simple and gone when the tab closes, but readable by any script on the page (XSS). The safer production choice is an HttpOnly cookie issued by the API with CSRF protection. Left as a documented trade-off.
- **Shop time zone, not browser time zone.** Appointments are stored in UTC; the API returns the shop's `timeZoneId` with availability; `core/tz.ts` converts with `Intl`. A customer travelling still sees the garage's clock.
- **Runtime config.** `public/config.json` holds `apiBaseUrl`, loaded before the app starts, so one build runs anywhere. Empty means same origin.
- **Errors.** Every failure becomes an `ApiError` with a friendly message and the server's correlation id; a 401 on a normal call signs the user out.
- **Bay board.** Pure placement maths in `board-layout.ts` (unit-tested), rendered as percentages so it scales.
- **Concurrency.** Two people booking the same slot: the API returns 409; the booking screen reloads slots and says so.

## Known gaps
- Code was written without running `ng build` in the authoring environment; `strictTemplates` is off in `tsconfig.json`. Turn it on once the build is green and fix what it finds.
- Component tests cover logic (time zones, board maths, errors, interceptors), not every screen. A Playwright end-to-end test would be the next step.
- Demo-account chips on the login page appear only in development builds.
- CI: copy the job from `docs/ci-web-job.yml` into `.github/workflows/ci.yml`.
