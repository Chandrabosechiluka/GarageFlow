# Day 4 study notes: authentication and roles (JWT)

Goal: explain how a request proves who it is, how the API decides what that person may do, and what you did to keep passwords and keys safe.

## What was added
- **Domain:** `AppUser` (email, display name, password hash, role, active flag) and the `UserRole` enum (Admin, Advisor, Technician).
- **Application:** `AuthService` (login), `UserService` (create, list, deactivate), `IPasswordHasher`, `IJwtTokenGenerator`, `AuthenticationFailedException`.
- **Infrastructure:** `Pbkdf2PasswordHasher`, `JwtTokenGenerator`, `JwtOptions`, the `Users` table mapping, seeded demo logins (Development only).
- **Api:** `AuthController` (`/api/auth/login`, `/api/auth/me`), `UsersController` (Admin only), `[Authorize(Roles = ...)]` on every other controller, JWT bearer setup, the Authorize button in Swagger, secure-by-default fallback policy.
- **Tests:** password hashing, token contents and signature, login rules, user rules, and the Users table mapping.

## Who may do what

| Action | Admin | Advisor | Technician |
|---|:-:|:-:|:-:|
| Log in, `GET /api/auth/me`, read customers, appointments and catalog | yes | yes | yes |
| Create customers and vehicles | yes | yes | no |
| Book, check in, cancel, mark no-show | yes | yes | no |
| Start inspection, add inspection items, complete | yes | no | yes |
| Create, list and deactivate users | yes | no | no |

## Follow one login and one call
1. `POST /api/auth/login` with email and password. This is the only open endpoint (plus `/health`).
2. `AuthService` finds the user by email, checks `IsActive`, and asks the hasher to compare the password with the stored hash.
3. If all is well, `JwtTokenGenerator` signs a token that holds the user's id, email, name, role and an expiry time. If not, you get **401** with one generic message.
4. The client sends `Authorization: Bearer <token>` on every later call.
5. `UseAuthentication` checks the signature, issuer, audience and expiry, and builds the user's identity from the claims.
6. `UseAuthorization` compares the role with the `[Authorize(Roles = ...)]` attribute. No token or a bad token gives **401**. A valid token with the wrong role gives **403**.

## Read in this order
1. `Domain/Entities/AppUser.cs`.
2. `Infrastructure/Auth/Pbkdf2PasswordHasher.cs`: read the comments and the stored format.
3. `Infrastructure/Auth/JwtTokenGenerator.cs`.
4. `Application/Services/AuthService.cs` and `UserService.cs`.
5. `Api/Program.cs`: the Jwt block, `AddAuthentication`, `FallbackPolicy`, and the order of `UseAuthentication` then `UseAuthorization`.
6. `Api/Controllers/*`: find each `[Authorize]` and match it to the table above.
7. The tests.

## Concepts to be able to explain
- **Authentication versus authorization.** Who are you, versus what may you do. 401 versus 403.
- **What a JWT is.** Three Base64 parts: header, payload (claims), signature. It is signed, **not encrypted**: anyone can read it, nobody can change it without the key. Try pasting one into jwt.io to see (only a development token).
- **Claims and roles.** Why the role is in the token, and the trade-off: a role change or a deactivated account only takes effect when the token expires. That is why tokens are short-lived (60 minutes here). Refresh tokens are the usual next step and are not built yet.
- **Why passwords are hashed with a slow, salted algorithm.** Salt: same password, different hash, no rainbow tables. Slow (600,000 PBKDF2 iterations): makes guessing expensive. The iteration count is stored inside the hash so it can be raised later. Why plain SHA-256 is not enough.
- **Constant-time comparison.** `FixedTimeEquals` avoids leaking how many bytes matched.
- **Same error for every login failure.** Prevents learning which emails exist (user enumeration). Honest limitation: an unknown email returns slightly faster than a wrong password, because no hash is computed; a timing attack could notice.
- **Secrets management.** The signing key is a user secret, not in Git; the app refuses to start without a strong key. If someone gets the key they can forge an Admin token.
- **Secure by default.** `FallbackPolicy` requires a signed-in user everywhere. Forgetting an attribute fails closed.
- **Middleware order.** Authentication, then authorization, then controllers.
- **Demo accounts.** Seeded only in Development, with a password that is public in the repo. Never in production.

## Self-test questions
1. A token is stolen. What can the thief do, and for how long? How would you reduce the damage?
2. Why is it safe to store the hash but not the password?
3. What status code does a Technician get when calling `POST /api/appointments`? And someone with no token?
4. Why does `AuthService` return the same error for a wrong password and an unknown email?
5. What breaks if `UseAuthorization` is placed before `UseAuthentication`?
6. Why is the JWT signing key not in `appsettings.json`?
7. What does `[Authorize(Roles = "Admin,Advisor")]` mean: both roles required, or either?

## Try it in Swagger
Demo logins (Development only), all with password `Garage@12345`: `admin@garageflow.local`, `advisor@garageflow.local`, `tech@garageflow.local`.
1. Call `GET /api/customers` with no token: expect **401**.
2. `POST /api/auth/login` as the advisor, copy `accessToken`, click **Authorize** at the top of Swagger, paste it.
3. `GET /api/auth/me` shows what the API read from your token.
4. Book an appointment as the advisor: works. Try `start-inspection` as the advisor: **403**.
5. Log in as the technician. Try to book: **403**. Check in is also **403**. Start inspection, add an item, complete: allowed (after an advisor has checked the appointment in).
6. Log in as the admin and `POST /api/users` to create a user. Try the same as the advisor: **403**.
7. Deactivate that user, then try to log in as them: **401**.
8. Log in with a wrong password: **401** with the generic message.

## Known gaps, on purpose
- A technician can act on any appointment, not only their own (resource-based authorization could come later).
- No refresh tokens, no lock-out after repeated failed logins, no password reset.
- Who did what is not yet recorded: the audit trail on Day 9 will use the user id from the token.

## Do it yourself
- Add a test that a deactivated user's old token still validates until it expires, and write down in a comment why that matters.
- Change the token lifetime to 5 minutes in user secrets (`Jwt:ExpiryMinutes`) and watch a token stop working.
- Commit style: `feat(auth): add JWT login and role-based authorization`.
