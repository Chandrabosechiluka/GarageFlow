# Day 7: cancellation policy, rescheduling, waitlist

## What we built
1. **Cancellation policy.** A Booked appointment cannot be cancelled (or moved) less than 2 hours before it starts. An Admin can override with `allowLate: true`. The rule lives in the domain (`Appointment.Cancel(reason, nowUtc, allowLate)`), and the API only checks *who* may override.
2. **Rescheduling** (`POST /api/appointments/{id}/reschedule`). Same checks as booking (UTC, future, opening hours, skill, clashes), run under the **same booking lock**. It keeps the current technician and bay when they are free, otherwise assigns another free qualified one. The appointment never clashes with its own old time (it is excluded from the busy list).
3. **Waitlist.** A customer asks "tell me if a slot opens" with a window of acceptable start times. When a booking is cancelled or moved, the oldest matching waiting customer is **offered** the freed slot. Accepting the offer books a real appointment.

## The waitlist life cycle
```
Waiting -> Offered -> Accepting -> Fulfilled
   ^          |           |
   +----------+-----------+   (offer withdrawn: slot was taken, or accept failed)
Waiting/Offered -> Cancelled   (customer leaves)
```
Why an offer instead of auto-booking? Booking someone without asking is a surprise for the customer. An offer keeps a human in the loop and the code easy to explain.

### Who gets the offer? (`OfferFreedSlotAsync`)
Oldest `Waiting` entry in the same shop where: the freed start is inside the customer's window, the service fits in the freed time, and the freed technician has the skill. Because the freed technician and bay were occupied for exactly that time, they are guaranteed free, so the offer is bookable unless someone else books it first. Accept re-checks everything anyway.

### Why the extra `Accepting` status?
Two people pressing "accept" at once both read `Offered`. Step 1 of accepting changes the status to `Accepting` and **saves immediately**. The `RowVersion` makes only one save win; the other gets a 409 before any booking happens. Without it, the customer could get two appointments. If booking then fails (slot taken), the entry goes back to `Waiting`.

## Design points worth saying out loud
- **Every code path that takes a slot must use the gate.** Day 6 warned about this; reschedule is the proof. A new test runs two reschedules into the same free hour in parallel against real SQL Server: exactly one wins.
- **Side effects must not break the main action.** Offering a slot is a courtesy after a cancel. It runs after the cancel is saved, and a concurrency clash there is swallowed, so a successful cancel never returns an error.
- **The offer is a snapshot, not a reservation.** Nothing is held for the customer. Holding slots needs expiry (a background job, Day 8).
- **Policy in the domain, permission in the API.** "2 hours notice" is a business rule (domain). "Only an Admin may override" is authorization (controller).

## Known gaps (on purpose)
- Offers never expire. Day 8's background jobs can expire stale offers and re-offer.
- No notification yet; someone has to look at `GET /api/waitlist?status=Offered`. Reminders and email come with jobs.
- Accept is three steps (claim, book, record). A crash between "book" and "record" leaves the entry in `Accepting` with a booked appointment; an admin would fix it by hand. A real system would use an outbox or one transaction.
- Matching only offers the *freed* technician's time, not any combination of free people.

## Tests added
- Domain: reschedule rules, the 2-hour notice (exactly 2 hours is allowed), waitlist state machine and window validation.
- Application: reschedule keeps/reassigns technician and bay, moves into its own overlap, conflicts, closed hours, status, override, uses the gate; cancel notice; waitlist join/duplicate, offer on cancel and on reschedule, no offer outside window, accept, accept after slot stolen, cancel entry.
- Infrastructure: waitlist mapping (row version, text status, no cascades).
- Integration: parallel reschedules into one slot.

## Self-test questions
1. Why does `Reschedule` exclude the appointment's own id from the busy list?
2. Why must rescheduling take the same lock as booking?
3. What goes wrong if "accept" only checks the status and then books?
4. Why is the offer step allowed to fail silently but booking is not?
5. Where would you put offer expiry, and what would trigger it?
6. Why is the notice rule in the domain but the Admin check in the controller?

## Do it yourself
- Add an `ExpiresUtc` to offers (2 hours) and make `AcceptOfferAsync` refuse expired ones (test first).
- Make the offer FIFO test: two entries, the older one must get the offer (use a clock you can advance).
- Commit style: `feat(booking): reschedule, cancellation notice policy and waitlist offers`.
