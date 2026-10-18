# Day 8: background jobs (no-shows, reminders, offer notices, offer expiry)

## What we built
A worker that wakes up every minute and runs four jobs:

| Job | What it does | How it avoids doing it twice |
|---|---|---|
| **No-shows** | A Booked appointment still not checked in 30 minutes after its start becomes `NoShow` | Only `Booked` rows are picked; the status changes, so the next run skips it |
| **Reminders** | Appointments starting within 24 hours get one notification | `Appointment.ReminderSentUtc`; set only after sending succeeded |
| **Offer notices** | A customer offered a waitlist slot is told once | `WaitlistEntry.OfferNotifiedUtc` |
| **Offer expiry** | An offer not accepted within 2 hours becomes `Expired`, and the slot is offered to the next person in line | The status changes, so it is picked only once |

Day 7's gaps are closed: offers now expire, customers are notified, and an expired offer cannot be accepted even before the job runs (`BeginAccept(nowUtc)` checks the deadline itself).

## How it is built
- **`MaintenanceService` (Application)** holds the job logic. Methods take no clock, only `IClock`, so tests run "as if it were Tuesday 9:30" without waiting.
- **`MaintenanceWorker` (Api)** is a thin `BackgroundService`: a `PeriodicTimer`, a new DI **scope per job** (each job gets a fresh DbContext, because a DbContext must not be shared or kept for hours), and a try/catch per job so one failure never stops the loop.
- **`INotificationSender`** is the seam. Today `LoggingNotificationSender` writes to the log. A real e-mail provider replaces it later without touching the jobs.
- **`MaintenanceSettings`** come from the `Maintenance` section of `appsettings.json` (interval, grace, lead time, batch size, on/off).
- **`POST /api/maintenance/run`** (Admin only) runs all jobs now, so you can see them work without waiting.

## Ideas to be able to explain
1. **Idempotent jobs.** Every job records its result on the row it handled. Running it twice, or two instances at once, must not double the effect.
2. **Send first, record second (at-least-once).** If sending fails, nothing is recorded and the next run retries. If the app crashes between sending and recording, the customer may get two messages, but never zero. For a reminder that is the right trade. (The opposite, record first, would be at-most-once: a crash loses the message.)
3. **Optimistic concurrency again.** If a job and an API request change the same appointment together, the row version makes the loser fail. The job treats that as "someone else already handled it", logs a warning and tries next run.
4. **Batches.** Each run handles at most 100 rows, so a big backlog cannot lock the database for a long time.
5. **Scope per job.** A `BackgroundService` is a singleton that lives for the whole app. A DbContext is scoped. Injecting it directly would be a bug; use `IServiceScopeFactory`.
6. **Why not Hangfire/Quartz?** Fine tools, and worth knowing, but a timer plus idempotent jobs is enough here and keeps the logic easy to test. Say when you would switch: many job types, retries with back-off, a dashboard, exact schedules.
7. **Testing time.** The job logic takes the clock from `IClock`, so tests pass any moment they like. The worker itself has almost no logic, so it needs almost no tests.
8. **Passing the slot on.** On expiry the slot is offered to the next customer, but only after re-checking that the technician and a bay are still free, since someone may have booked it during the 2 hours.

## Known gaps (on purpose)
- Several API instances would each run the worker. The row versions keep the data correct, but reminders could be sent twice in a race. A real fix is a distributed lock (we have `sp_getapplock` already) or a single worker instance. This comes up again on Day 11 (deployment).
- Entries stuck in `Accepting` after a crash are not recovered yet.
- The sender only logs. Real e-mail/SMS is a later swap.
- No retry limit or back-off for a notification that keeps failing.

## Tests added
Domain: `IsNoShowDue` (grace period boundary), `MarkReminderSent` (and reset on reschedule), offer expiry rules, accepting an expired offer.
Application (`MaintenanceServiceTests`): no-show grace period, checked-in untouched, idempotent; reminder timing, once only, skips cancelled, failed send is retried, resent after a move; offer notice once; expiry timing; expired offer passed to the next customer; not passed on if the slot was taken; expired offer cannot be accepted.
Infrastructure: new columns exist and are optional.

## Try it
1. Run the API (Development). In the log you will see "Maintenance worker started".
2. Log in as `admin@garageflow.local`, book an appointment starting in the next few hours (the reminder window is 24h).
3. `POST /api/maintenance/run` in Swagger: the response shows how many reminders were sent; the console shows `NOTIFICATION to demo@example.com: ...`.
4. Call it again: all zeros (idempotent).
5. Join the waitlist, cancel the booking, run the jobs: the offer notice appears in the log. Change `NoShowGraceMinutes` or wait: the other jobs follow.

## Self-test questions
1. Why is the reminder marked as sent after sending, not before? What changes if you swap them?
2. What happens if two instances of the API run the reminder job at the same moment?
3. Why does the worker create a new scope for every job?
4. How does an expired offer get handled if the job is down for an hour?
5. Why does the expiry job re-check availability before offering the slot onward?
6. When would you replace the `PeriodicTimer` with Hangfire or Quartz?

## Do it yourself
- Add a `NoShowRate` count to the report endpoint, or a job that deletes `Expired` entries older than 30 days (test first).
- Replace `LoggingNotificationSender` with one that writes to a file, to feel how the interface protects the jobs.
- Commit style: `feat(jobs): no-show marking, reminders and waitlist offer expiry`.
