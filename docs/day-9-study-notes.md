# Day 9: error handling, logging, pagination, audit trail

No database migration today: the `AuditLogs` table has existed since Day 2.

## 1. Pagination
Lists no longer return "the first 100 or 200 rows". `GET /api/appointments`, `GET /api/customers` and `GET /api/audit` take `?page=1&pageSize=20` (1 to 100) and return:

```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 45, "totalPages": 3, "hasNext": true, "hasPrevious": false }
```
- **Stable sort order.** `ORDER BY StartTimeUtc, Id`. If two rows have the same start time and nothing breaks the tie, SQL Server may order them differently on each query, so a row can show up on two pages or none. A unique tie-breaker column fixes it. A test books appointments that start at the same moment and checks that paging loses and repeats nobody.
- **Bad input is rejected, not "fixed".** Page 0 or page size 5000 returns 400.
- **Count + page = two queries.** `COUNT(*)` then `OFFSET/FETCH`. Offset paging gets slower for very deep pages. The alternative, *keyset (cursor) paging* ("give me rows after this start time and id"), is faster for huge tables but cannot jump to page 50. Know both and when to choose.
- Waitlist and users lists are still plain lists: they are short queues/admin lists. Say that out loud if asked; do not pretend everything needs paging.

## 2. Audit trail: who did what
Every create/change/delete of Appointments, InspectionItems, WaitlistEntries, Customers, Vehicles and Users is written to `AuditLogs` with: entity, id, action (`Created` / `Updated` / `Deleted`), who, when (UTC), and a JSON summary (`{"Status":{"from":"Booked","to":"CheckedIn"}}`).

How it works (the interesting part):
- `GarageFlowDbContext` overrides `SaveChanges`. Just before saving, it looks at the **change tracker** (what EF already knows changed), adds `AuditLog` rows to the same unit of work, and saves everything together. One transaction: a change can never be saved without its record, and a record never exists for a change that failed.
- **Automatic, not remembered.** Nobody has to call a logging method in each service, so nobody can forget it. New code that changes an Appointment is audited for free.
- **Who** comes from `ICurrentUser`, filled from the JWT (`email` claim) by `HttpContextCurrentUser`. Background jobs and seeding have no request, so they show as `system`.
- **Never recorded:** `PasswordHash` (secrets) and `RowVersion` (noise). A test proves the hash never appears.
- Only real changes: a property "marked modified" with the same value writes nothing.
- Read it: `GET /api/audit?entityName=Appointment&entityId=...&performedBy=...` (Admin) and `GET /api/appointments/{id}/history` (Admin, Advisor).

Trade-offs to be able to discuss:
- Interceptor/override vs writing audit code in each service (simple, explicit, but easy to forget) vs database triggers or SQL Server temporal tables (cannot be bypassed by any code path, but invisible in the C# code and no "who" from the app without extra work).
- The audit table grows forever. Real systems archive or partition it.
- `Details` is truncated at 2000 characters.
- Anyone with database write access could edit it. "Append-only" here is a convention, not a guarantee.

## 3. Error handling
Already in place from Day 3/6: one `IExceptionHandler` turns exceptions into problem-details JSON (400/401/404/409/500). Today:
- **Correlation ID** on every response: header `X-Correlation-ID` and the `correlationId` field of error bodies. A caller can send their own. The value is accepted only if it is short and plain (no new lines), so it cannot forge log lines.
- **Validation errors** (missing field, too-long name) keep the standard 400 shape plus the correlation ID.
- **Empty 401/403/404 responses** become problem-details JSON too (`UseStatusCodePages`).
- **Caller hung up** (cancelled request) is logged quietly and is not a 500.
- **500s never leak details.** The caller gets a generic message and an ID; the full exception is in the log under the same ID.

## 4. Logging
- **One request log line**: `HTTP POST /api/appointments responded 409 in 12 ms`. Levels: 2xx/3xx Information, 4xx Warning, 5xx Error, `/health` Debug. Query strings are not logged (they may contain personal data).
- **Scopes**: the correlation ID is attached to every line written during the request, including the ones inside services and EF. Search the log for one ID and you get that request's whole story.
- **Log levels** mean something: Information for normal events, Warning for the caller's mistakes, Error for our bugs (with the exception).
- **Structured logging**: `logger.LogInformation("Handled {ExceptionType}", ...)` keeps `ExceptionType` as a named field, not just text, so a log system (Seq, Application Insights) can filter on it. Never build log text with `$"..."`.
- **Middleware order matters**: correlation ID -> request logging -> exception handler -> status code pages -> auth -> controllers. Request logging sits outside the exception handler so it sees the final status.
- **Never log**: passwords, tokens, full request bodies.

## Tests added
`PagingTests` (validation, page maths, no lost/duplicated rows with tied start times, sort order, empty page past the end, default page, bad page rejected, customers alphabetical) and `AuditTrailTests` (created with who/when, from/to changes, password hash never stored, no-op saves write nothing, `system` actor, appointment history, filters and paging).

## Self-test questions
1. Why add `Id` to the sort order when paging by start time?
2. Offset paging or keyset paging for 50 million audit rows? Why?
3. Why is the audit row saved in the same transaction as the change?
4. What are the downsides of auditing inside `SaveChanges` compared with SQL Server temporal tables?
5. A user reports "it failed at 10:42". What do you ask for, and how do you find the cause?
6. Why does the 500 response say so little while the log says everything?
7. Why must request logging sit outside the exception handler?

## Do it yourself
- Add `?sort=` (start time ascending or descending) to the appointments list with a whitelist of allowed values (never sort by a raw string from the caller).
- Add an `X-Total-Count` response header to the paged lists and think about whether the body or the header is better.
- Commit style: `feat(api): pagination, automatic audit trail, correlation ids and request logging`.
