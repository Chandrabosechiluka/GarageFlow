# Day 1 study notes

Goal: be able to explain every file here aloud without reading it.

## Read in this order
1. `Directory.Build.props` and the four `.csproj` files: who references whom, and why the Domain references nothing.
2. `Domain/Common/` (`BaseEntity`, `DomainException`).
3. `Domain/Entities/Customer.cs` and `Vehicle.cs`: constructors that validate, private setters, an aggregate that owns its children.
4. `Domain/Entities/Appointment.cs`: the heart of Day 1.
5. `tests/.../AppointmentTests.cs`: what each test proves.

## Concepts to be able to explain
- Why layered architecture, and what the dependency direction protects.
- Why entities have **private setters** and a **private parameterless constructor** (invariants, and EF Core still being able to materialise them).
- What a **domain exception** is and how the API will later turn it into an HTTP response.
- Why status changes go through methods instead of `appointment.Status = ...`.
- Why the transition table is a dictionary instead of a chain of `if` statements.
- Why times are UTC and why `Book` takes `nowUtc` as a parameter instead of calling `DateTime.UtcNow` (testability).
- Half-open intervals `[start, end)` for overlap checks, with the 09:00-10:00 / 10:00-11:00 example.
- Why `RowVersion` exists (optimistic concurrency, coming Day 7).

## Self-test questions
1. What happens if two layers reference each other? Which project would break first here?
2. Why does `Technician` expose `IReadOnlyCollection<Skill>` rather than `List<Skill>`?
3. A new status `Rescheduled` is needed. Which files change?
4. Why is `OverlapsWith` on the entity and not in a controller?
5. How would you test "cannot complete without inspection items"? (Find the existing test.)

## Do it yourself (Saturday task)
- Add a rule of your own, for example: a `Booked` appointment cannot be cancelled less than 2 hours before start. Write the failing test first, then the code.
- Commit message style: `feat(domain): add late-cancellation rule`.
