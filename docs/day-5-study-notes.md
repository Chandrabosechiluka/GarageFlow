# Day 5 study notes: slot availability and time zones

Goal: explain how the system decides which times can be booked, why that logic lives in the Domain, and how time zones are handled correctly.

## What was added
- **Domain:** `Shop` now has a `TimeZoneId` and can convert its local opening hours to UTC. New `Scheduling/SlotFinder` (pure logic) and `BusyPeriod` / `AvailableSlot` records. `Appointment.ValidateStart` (the start-time rules on their own).
- **Application:** `AvailabilityService`, `ShopLookup`. `AppointmentService.BookAsync` now accepts an empty technician and/or bay and assigns them, and rejects bookings outside opening hours.
- **Api:** `GET /api/availability?serviceTypeId=...&date=2026-11-03` (optional `technicianId`, `shopId`).
- **Database:** one new column, `Shops.TimeZoneId` (needs a migration).
- **Tests:** about 32 new ones (slot finder, time zones, availability, auto-assignment, opening hours).

## The algorithm in plain words
For one service on one local day:
1. Convert the shop's opening and closing time on that day to UTC (`Shop.GetOpenWindowUtc`).
2. Walk from opening time in 30-minute steps. A start time is only considered if the whole service still fits before closing, and the start is in the future.
3. For each candidate, find the technicians who have the required skill and have no overlapping appointment, and the bays with no overlapping appointment.
4. Keep the start time only if there is at least one technician AND at least one bay.
5. Return each slot with all free technicians and bays. The first of each (alphabetical) is the "suggested" one, which is also what booking assigns when you leave them empty.

Overlap uses half-open intervals `[start, end)`, the same rule as `Appointment.OverlapsWith`: an appointment ending at 10:00 does not block one starting at 10:00.

## Why a pure function in the Domain
`SlotFinder.FindSlots` takes everything it needs as parameters and touches no database, clock or framework. So the tests build a few objects in memory and check exact answers (9 tests, no setup). The Application layer's only job is to load the data and call it. One query loads the busy periods for the day, and the rest is done in memory: far fewer database round trips than asking the database about every slot.

## Time zones (the part interviewers like)
- Rule: **store UTC, show local.** Appointments are UTC; the shop's opening hours are local wall-clock times. The shop therefore stores its own zone (`Asia/Kolkata`).
- "09:00 in Hyderabad" is `03:30 UTC`. "09:00 in New York" is `13:00 UTC` before 1 November 2026 and `14:00 UTC` after, because clocks change. A test proves that.
- A local time can also not exist (the hour skipped when clocks go forward). `LocalToUtc` handles that instead of crashing.
- The availability response carries both `startUtc` and `startLocal` (with the `+05:30` offset), so a UI never has to guess.
- You send and receive UTC times with a `Z`. The date in the availability query is the shop's local date.

## Read in this order
1. `Domain/Scheduling/SlotFinder.cs`.
2. `Domain/Entities/Shop.cs` (the time zone methods).
3. `tests/GarageFlow.Domain.Tests/SlotFinderTests.cs` and `ShopTimeZoneTests.cs`.
4. `Application/Services/AvailabilityService.cs`.
5. `Application/Services/AppointmentService.cs`, the `BookAsync` method top to bottom.
6. `Api/Controllers/AvailabilityController.cs`.

## Concepts to be able to explain
- Why the algorithm is a pure function and what that buys you in tests.
- Why UTC storage plus a zone per shop, and what goes wrong if you store local times.
- What a half-open interval is and why it matters for back-to-back bookings.
- What "assign automatically" means here (first free, alphabetical), why it is deterministic, and its weakness: it always loads the first technician. A fairer version would pick the one with the fewest bookings that day.
- Why availability is only advice: between "I saw the slot" and "I booked it" someone else can take it. The booking re-checks, but two requests at the same instant can still both pass. That is exactly Day 6.
- Time complexity, roughly: slots × (technicians + bays) × busy periods for one day. Fine for one shop, and you can say when it would stop being fine.
- Why the default value on the new column matters for a table that already has rows.

## Self-test questions
1. A shop in Chennai opens at 09:00. What UTC time does the first slot have? What if the shop were in London in summer?
2. Why does `FindSlots` skip a slot whose start is exactly "now"?
3. Alice is busy 09:00-10:00. Is she offered for a 10:00 slot? For 09:30?
4. Why does the booking code check opening hours even though the UI would only offer valid slots?
5. Why does the availability endpoint load the busy periods once instead of querying per slot?
6. What would you change to make the garage closed on Sundays?

## Try it in Swagger
Seeded skills: Oil Change and Tyre Rotation need Ravi, Brake Inspection needs Anil, Engine Diagnostics needs Meena, General Service can be done by all three. There are three bays.
1. Log in as the advisor and click Authorize.
2. `GET /api/service-types` and copy the id of **General Service**.
3. `GET /api/availability` with that id and a date from tomorrow on, for example `2026-10-20`. Look at `startUtc` versus `startLocal`.
4. `GET /api/customers` and copy the customer and vehicle ids.
5. `POST /api/appointments` with one of the `startUtc` values, and **delete the technicianId and bayId lines** (or set them to null). Look at who was assigned.
6. Book the same start time two more times: each time a different technician and bay. The fourth gets **409**.
7. Run availability again: that start time (and the ones overlapping it) are gone.
8. Try a start time of `02:00Z`: **400**, the shop is closed.
9. Choose Brake Inspection and book the same time twice: the second is **409** (only Anil can do brakes).

## Do it yourself
- Add a `ClosedDays` idea: shops closed on Sunday return no slots and reject bookings. Where does the rule belong? Write the failing test first.
- Make auto-assignment fairer: prefer the free technician with the fewest appointments that day (`SlotFinder.FreeTechnicians` would need the busy list, which it already receives).
- Commit style: `feat(scheduling): add slot availability and automatic assignment`.
