# Day 6 study notes: double-booking prevention (transactions, locks, concurrency)

Goal: explain a race condition you can reproduce, the options for fixing it, why you chose this one, and how you proved it works.

## The problem (what Day 5 left open)
Booking used to be "check, then insert":

```
Request A: is Anil free at 10:30?  -> yes
Request B: is Anil free at 10:30?  -> yes      (A has not saved yet)
Request A: insert booking          -> saved
Request B: insert booking          -> saved     <- Anil is now double-booked
```
Each step is correct on its own. The bug only exists in the gap between the check and the insert, and it only appears when two requests arrive at nearly the same time. That is why it is never found by clicking around, and why it matters.

## What was added
- **`IBookingGate`** (Application) and **`SqlServerBookingGate`** (Infrastructure). The gate starts a database transaction, takes a named SQL Server **application lock** (`sp_getapplock`, key `booking:{shopId}`), runs the booking code, commits, and only then releases the lock.
- **`AppointmentService.BookAsync`** now does the cheap validation first, then runs "load busy periods, pick technician and bay, insert" inside the gate.
- **Two unique, filtered indexes** (`UX_Appointments_ActiveTechnicianStart`, `UX_Appointments_ActiveBayStart`). The database itself refuses two active appointments for the same technician (or bay) starting at the same moment. Cancelled and no-show rows are excluded.
- **Error mapping:** a unique-index violation (SQL error 2601/2627) becomes HTTP 409, a concurrency conflict becomes 409, and SQL details are never sent to the caller.
- **`GarageFlow.IntegrationTests`**: 4 tests against the real SQL Server in Docker, each using its own temporary database.

## The options, and why this one
| Option | Idea | Verdict |
|---|---|---|
| Check then insert (Day 5) | Just look first | Race condition |
| Serializable transaction | Let SQL Server lock the ranges you read | Works but easy to get deadlocks, and hard to reason about |
| **Application lock** | Everyone who books for a shop takes one named lock first | **Chosen.** Simple, correct across several API servers. Cost: one booking per shop at a time, which is fine at this scale |
| Unique index | The database refuses duplicates | Great safety net, but SQL Server cannot say "no overlapping time ranges", only "same start". Used as a second layer |
| Optimistic concurrency (row version) | Detect that a row changed since you read it | The right tool for two people editing the **same** appointment. It does not help for two **new** rows that overlap |
| A queue with a single worker | Serialize bookings by design | Valid for much larger scale; more moving parts |

Layers of defence, from outermost to innermost: validation outside the lock, the lock plus the clash check inside it, then the unique index in the database. The row version protects the other kind of conflict, edits to an existing appointment.

## Read in this order
1. `Application/Abstractions/IBookingGate.cs`: read the comment.
2. `Infrastructure/Persistence/SqlServerBookingGate.cs`: transaction, lock, commit, return codes.
3. `Application/Services/AppointmentService.cs`: `BookAsync` from top to bottom. Find the line where the gate is entered and what stays outside.
4. `Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`: the filtered unique indexes.
5. `Api/Errors/ApiExceptionHandler.cs`.
6. `tests/GarageFlow.IntegrationTests/DoubleBookingTests.cs`.

## Concepts to be able to explain
- **Race condition**: result depends on timing between steps. Check-then-act is the classic shape.
- **Transaction (ACID)**: all changes commit together or none do. Here it also scopes the lock: the lock is released when the transaction ends, so a crashed request can never hold it forever.
- **Why commit before releasing the lock**: so the next caller, who is waiting, sees the booking when it reads.
- **Pessimistic versus optimistic concurrency.** Pessimistic: stop others up front (the lock). Optimistic: let everyone work and detect a conflict at save time (the row version, throws `DbUpdateConcurrencyException`, mapped to 409). Know when each fits.
- **Isolation levels** in one sentence each (read committed, repeatable read, serializable) and why the lock was simpler than serializable here.
- **Lock granularity.** One lock per shop is coarse but simple. Finer (per technician and day) allows more parallel bookings with more complexity and a risk of deadlocks. Say this trade-off out loud.
- **Timeouts and retries.** If the lock cannot be taken within 10 seconds the caller gets a 409 "try again". Clients should be prepared to retry on 409.
- **Defence in depth**: application-level check plus a database-level constraint.
- **Why you cannot unit test this with a fake database.** The in-memory provider has no transactions or locks, so unit tests only check that booking goes through the gate. The real proof needs real SQL Server and real parallelism, hence the integration tests.
- **Known limit**: only code that goes through the gate is protected. When rescheduling arrives (Day 7) it must use the same gate.

## The tests, and what each proves
1. **Ten parallel bookings, same technician and bay and time:** exactly 1 succeeds, 9 get a clean conflict, 1 row in the database.
2. **Ten parallel auto-assigned bookings (3 technicians, 3 bays):** exactly 3 succeed, 7 conflict, and the 3 rows have 3 different technicians and 3 different bays.
3. **Two people editing one appointment:** the second save throws a concurrency exception (row version).
4. **Writing straight to the database, bypassing the service:** the unique index refuses a second active appointment for the same technician start, and for the same bay start, but allows one that is cancelled.

Each test creates `GarageFlowTest_<random>` on your Docker SQL Server using the **real migrations**, then deletes it. They are skipped (not failed) when SQL Server is not running.

## Self-test questions
1. Describe the exact sequence in which two requests double-book without the lock.
2. Why is the validation (customer exists, opening hours) done before taking the lock?
3. What releases the lock if the server crashes in the middle of a booking?
4. Why is the row version not enough to stop double booking?
5. What would you change if one shop had thousands of bookings per minute?
6. Why do the integration tests use a new database per test?
7. What does the 409 response mean for the person using the API, and what should their app do?

## Do it yourself
- **Experiment (worth doing):** in `SqlServerBookingGate`, comment out the line `await AcquireLockAsync(key, transaction, ct);`, run `dotnet test --filter DoubleBookingTests`, and watch test 1 fail. Look at the error: the unique index is catching the race with a database exception. That is the safety net doing its job. Now restore the line and run it again. Telling this story in an interview is very strong.
- Add a fifth integration test: ten parallel bookings for ten different start times on the same shop should all succeed (the lock serializes them but none is rejected).
- Commit style: `feat(booking): prevent double booking with a transaction lock and unique indexes`.
