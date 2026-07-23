# FoodBridge Backend — Phase Plan

Phases run one at a time, in order. A phase is not "done" until its acceptance criteria are checked off.

## Phase 0 — Solution scaffold, cross-cutting foundation
- [x] Solution + 5 projects created with exact structure; `Directory.Build.props` (Nullable, TreatWarningsAsErrors, ImplicitUsings).
- [x] Packages installed (Dapper, SqlClient, FluentMigrator + Runner.SqlServer, FluentValidation.AspNetCore, Serilog.AspNetCore + sinks, Swashbuckle, JwtBearer, QuestPDF).
- [x] `ApiResponse<T>`, `PagedResponse<T>`, `Result<T>`, domain exceptions, `ExceptionHandlingMiddleware`, request-logging middleware, `BaseController`, `IClock`/`SystemClock`, `IDbConnectionFactory`, `BaseRepository` with `ExecuteInTransactionAsync`.
- [x] `Program.cs`: Serilog bootstrap, Swagger w/ JWT Authorize button, CORS `AllowAngularDev`, `GET /api/health`, FluentMigrator runner on startup behind `Database:MigrateOnStartup`.
- [x] `docs/PLAN.md`, `docs/ARCHITECTURE.md`, `docs/API-CONTRACTS.md` skeletons.

**Acceptance criteria**
- [x] `dotnet build` — zero warnings.
- [x] `GET /api/health` returns the standard envelope with a TraceId.
- [x] Throwing a test exception returns a clean 500 envelope; full details appear in the Serilog file.
- [x] Swagger UI loads with JWT auth button.

> Note: target framework is `net6.0` built with the .NET 8 SDK (see `CLAUDE.md` note) — only the .NET 6 runtime, not SDK, is installed on this machine.

## Phase 1 — Database schema via FluentMigrator
- [x] Migrations for Users, OtpCodes, Listings, ListingImages, ListingTimeline, Notifications, Certificates, VolunteerPoints, Disputes.
- [x] Spatial indexes + supporting nonclustered indexes.
- [x] Seed migration (Development only): 1 admin, 2 donors, 3 volunteers, 2 recipients, 8 listings.
- [x] Data dictionary + Mermaid ER diagram in `docs/ARCHITECTURE.md`.

**Acceptance criteria**
- [x] Fresh DB: app starts, all migrations apply, re-running is idempotent.
- [x] Rollback of latest migration works (`Down()` implemented for all).
- [x] Seed data visible; spatial index exists (verified via `sys.indexes`).

> Note: the seed migration's `[Profile("Development")]` tag causes FluentMigrator to re-invoke its `Up()` on every run (not just once) — guarded with an `IF EXISTS ... RETURN` check. See `docs/ARCHITECTURE.md` decisions log.

## Phase 2 — Authentication & Registration (5 endpoints)
- [x] `ISmsProvider` + `MockSmsProvider`.
- [x] `POST /api/auth/send-otp`, `POST /api/auth/verify-otp`, `POST /api/auth/register`, `POST /api/auth/logout`, `GET /api/auth/me`.
- [x] JWT claims/expiry/policies (`DonorOnly`, `VolunteerOnly`, `RecipientOnly`, `AdminOnly`) — policies registered; first consumers arrive in later phases.
- [x] FluentValidation validators for every request.
- [x] `docs/API-CONTRACTS.md` + `FoodBridge.http` updated.

**Acceptance criteria**
- [x] Full flow works end-to-end: send-otp → verify → register → me (verified via curl against the real SQL Express DB; Swagger UI wiring confirmed in Phase 0).
- [x] Rate limit (429 after 3 sends/15min) and attempt limit (422 after 5 wrong verifies) provably work.
- [x] No OTP stored in plaintext (`OtpCodes.CodeHash` is SHA-256 hex, verified in DB).

## Phase 3 — User / Profile module (4 endpoints)
- [x] `GET/PUT /api/users/{id}`, `PATCH /api/users/{id}/availability`, `POST /api/users/{id}/avatar`.
- [x] `ICurrentUser` service; `IFileStorage` + `LocalFileStorage`.
- [x] Docs + .http updated.

**Acceptance criteria**
- [x] User A cannot edit user B (403 envelope) — verified with a Volunteer JWT against another user's `GET`/`PUT`.
- [x] Avatar upload returns a servable URL — verified end-to-end (upload → 200 with `/uploads/{guid}.jpg` → fetched that exact URL → 200 `image/jpeg`).
- [x] Bonus verified: Admin can `GET` any profile; Donor gets 403 on `PATCH .../availability` (role-restricted even for their own account); bad file extension → 422.

## Phase 4 — Listings: Donor side (6 endpoints)
- [x] Create/list/detail/update/cancel listing, image upload.
- [x] `ListingStateMachine` single source of truth for transitions.
- [x] **Add `DietType` (Veg/Non-Veg) and `MealType` (Breakfast/Lunch/Dinner/Snacks) tinyint columns to `Listings`** (new migration, on top of the freeform `FoodType` text column) — decided after comparing against the prototype; see `docs/ARCHITECTURE.md` decisions log.

**Acceptance criteria**
- [x] Editing a Claimed listing returns 422 (verified live: `PUT` on a seeded Claimed listing → 422 `"Only pending listings can be edited."`). Detail endpoint shows the timeline. Contracts updated.
- [x] Bonus verified live: create → list (own listings, paginated) → detail → update (Pending only) → image upload (servable URL) → cancel → re-cancel blocked 422 via `ListingStateMachine` → cross-donor `GET` blocked 403 → non-Donor role blocked 403 by the `DonorOnly` policy.

## Phase 5 — Listings: Volunteer side + geo (4 endpoints)
- [x] `GET /api/listings/nearby`, claim (optimistic concurrency), confirm-pickup, confirm-delivery.
- [x] `RecipientMatcher` helper.

**Acceptance criteria**
- [x] Two parallel claims: exactly one 200, one 409 (verified live with two real concurrent `curl` requests against the same listing).
- [x] Nearby returns correct ordered distances (verified live: two listings 0km and 5.79km apart, returned in ascending order).
- [x] Invalid transitions all blocked by `ListingStateMachine` (verified live: `PickedUp→PickedUp`, `Delivered→Delivered`, `Claimed→Delivered` all 422).
- [x] Bonus verified live: wrong-volunteer `confirm-pickup`/`confirm-delivery` → 403; `RecipientMatcher` auto-assigns the nearest available Verified recipient on pickup; `confirm-delivery` blocked (422) until a recipient is matched; Donor role blocked (403) from all Volunteer routes; out-of-range latitude on `nearby` → 422.

## Phase 6 — Recipient side (5 endpoints)
- [ ] incoming, accept, reject, confirm-receipt (atomic: timeline + points + certificate + notifications), history.
- [ ] **Reject implements simple auto-reassignment** (nearest other available Verified recipient via `RecipientMatcher`) — scope increase from the original "manual re-pick, roadmap-only" note, decided after comparing against the prototype; see `docs/ARCHITECTURE.md` decisions log.

**Acceptance criteria**
- [ ] Confirm-receipt is all-or-nothing (atomicity proven).
- [ ] Reject reassigns to a different available recipient automatically (or produces a clear "no recipient available" outcome if none exists).

## Phase 7 — Real-time: SignalR notifications + tracking + expiry job
- [ ] `INotificationDispatcher` + `SignalRNotificationDispatcher`.
- [ ] `NotificationsHub`, `TrackingHub`.
- [ ] REST fallbacks: track, notifications list/read, geocode.
- [ ] `ListingExpiryBackgroundService`.
- [ ] SignalR contract documented.

**Acceptance criteria**
- [ ] Live notification pops across two tabs.
- [ ] Expiry job flips a past-deadline seed listing within a minute of startup.

## Phase 8 — Certificates, Leaderboard, Reports (8 endpoints)
- [ ] `IPdfGenerator` + `QuestPdfCertificateGenerator`.
- [ ] Certificate list/detail/pdf, leaderboard, donor/recipient reports.

**Acceptance criteria**
- [ ] PDF downloads and opens; leaderboard ranks correctly; report JSON is chart-ready.

## Phase 9 — Admin module (8 endpoints)
- [ ] Dashboard stats, listings/accounts management, verify/suspend, disputes, platform reports.

**Acceptance criteria**
- [ ] Non-admin JWT gets 403 on every admin route. Verifying a Pending recipient makes them eligible for `RecipientMatcher`.

## Phase 10 — Documentation, hardening, demo polish
- [ ] Finish `docs/ARCHITECTURE.md` and `docs/API-CONTRACTS.md`.
- [ ] Hardening sweep (`[ProducesResponseType]`, request size limits, `appsettings.Production.json`, `dotnet format`).
- [ ] Demo script in `PLAN.md`.
- [ ] Final self-review.

**Acceptance criteria**
- [ ] A new developer can clone, set one connection string, press F5, and hit every endpoint from `FoodBridge.http` following only the docs.
- [ ] Zero build warnings, all phases checked off.

---

## Demo script
_To be filled in during Phase 10._
