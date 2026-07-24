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
- [x] incoming, accept, reject, confirm-receipt (atomic: timeline + points + certificate + notifications), history.
- [x] **Reject implements simple auto-reassignment** (nearest other available Verified recipient via `RecipientMatcher`) — scope increase from the original "manual re-pick, roadmap-only" note, decided after comparing against the prototype; see `docs/ARCHITECTURE.md` decisions log.

**Acceptance criteria**
- [x] Confirm-receipt is all-or-nothing (atomicity proven) — verified live via direct DB query: `Listings.Status`, `ListingTimeline`, `VolunteerPoints`, `Certificates`, and both `Notifications` rows all landed together after one call.
- [x] Reject reassigns to a different available recipient automatically (or produces a clear "no recipient available" outcome if none exists) — verified live with the 2 seeded recipients: 1st reject → reassigned to the other; 2nd reject (by that other) → `recipientId: null`, `"No other recipient is currently available."`. Caught and fixed a real bug here: excluding only the *current* recipient let two recipients ping-pong forever, so "no recipient available" was unreachable — fixed by excluding everyone who has already rejected this listing (see decisions log).
- [x] Bonus verified live: wrong-recipient (already reassigned away) on any action → 403; `confirm-receipt` before `Delivered` → 422; double `confirm-receipt` → 422; `accept`/`reject` don't change `Status`; `history` shows confirmed receipts.

## Phase 7 — Real-time: SignalR notifications + tracking + expiry job
- [x] `INotificationDispatcher` + `SignalRNotificationDispatcher`.
- [x] `NotificationsHub`, `TrackingHub`.
- [x] REST fallbacks: track, notifications list/read, geocode.
- [x] `ListingExpiryBackgroundService`.
- [x] SignalR contract documented.

**Acceptance criteria**
- [x] Live notification pops across two tabs — verified with a real two-connection SignalR client harness (not just code review): donor and volunteer each held an open `NotificationsHub` connection, `confirm-receipt` fired, and each received *only* their own notification instantly over the wire (donor: `DonationConfirmed`; volunteer: `PointsAwarded`).
- [x] Expiry job flips a past-deadline seed listing within a minute of startup — verified live: 3 Pending-but-overdue listings (both a seed row and a leftover test listing) flipped to `Expired` immediately on startup (well under a minute), with `ListingTimeline.ActorUserId = NULL` and the correct system note.
- [x] Bonus verified live via the same SignalR harness: `TrackingHub.UpdateLocation` (volunteer) → `LocationUpdated` pushed live to the donor's joined tracking group; the REST `GET .../track` fallback returned the identical stored location afterward; an uninvolved user hitting `track` got 403; `GET/PATCH /api/notifications` REST fallback works; `GET /api/geocode` resolves known Ahmedabad localities and falls back to an approximate city-center marker for unknown addresses.

## Phase 8 — Certificates, Leaderboard, Reports (8 endpoints)
- [x] `IPdfGenerator` + `QuestPdfCertificateGenerator`.
- [x] Certificate list/detail/pdf, leaderboard (+ my-rank), donor/volunteer/recipient reports (8: 3 + 2 + 3).

**Acceptance criteria**
- [x] PDF downloads and opens — verified live: downloaded a real certificate PDF over HTTP (`%PDF-1.4` magic bytes, correct `Content-Type`/`Content-Disposition`) and visually confirmed it renders correctly (donor name, listing title, meal count, certificate number, issue date all correct).
- [x] Leaderboard ranks correctly — verified live with two volunteers at different point totals (62 vs 5): returned in descending-points order with `RANK()`; a volunteer with zero deliveries correctly gets `null` data on `/leaderboard/me` rather than an error.
- [x] Report JSON is chart-ready — verified live for all three roles: each returns a summary plus a `ChartPoint[]` (`{ period: "yyyy-MM", value }`) series, directly bindable to a chart with no client-side reshaping.
- [x] Bonus verified live: PdfUrl starts null and gets populated on first render only; cross-donor certificate access → 403; wrong-role report access (Donor → `/reports/volunteer`) → 403.

## Phase 9 — Admin module (8 endpoints)
- [x] Dashboard stats, listings/accounts management, verify/suspend, disputes (list/resolve), platform reports. (5 + 2 + 1 = 8; see `docs/ARCHITECTURE.md` decisions log for the exact breakdown and the "raise a dispute" scope note.)

**Acceptance criteria**
- [x] Non-admin JWT gets 403 on every admin route — verified live: a Donor JWT swept across all 8 endpoints (`dashboard`, `listings`, `accounts`, `verify`, `suspend`, `disputes` list/resolve, `reports/platform`) got 403 on every single one.
- [x] Verifying a Pending recipient makes them eligible for `RecipientMatcher` — verified live with a real before/after test: created a listing next to a genuinely Pending recipient (`Test Household`, seeded from earlier Phase 2 testing) → matched to a farther-away *Verified* recipient instead (correctly excluded); Admin verified `Test Household`; repeated the identical scenario → matched to `Test Household` this time.
- [x] Bonus verified live: dashboard stats numerically correct (10 users, 17 listings, 67 meals donated, 3 certificates, etc.); suspending an Admin account or the caller's own account is blocked (422); a real dispute row was listed, resolved, and a second resolve attempt correctly blocked (422).

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
