# FoodBridge — API Contracts

> Filled in per phase, in the same phase each endpoint is added. See `docs/PLAN.md` for phase status.

## Conventions
- All responses use the `ApiResponse<T>` / `PagedResponse<T>` envelope.
- Pagination: `page` (1-based), `pageSize`.
- All timestamps are UTC ISO-8601.

## Enum value tables
See `docs/ARCHITECTURE.md` § Data dictionary → Enum value tables (Role, AccountStatus, FreshnessTag, Listings.Status, Disputes.Status).

## Table of contents
- [Auth](#auth) — send-otp, verify-otp, register, logout, me
- [Users](#users) — get/update profile, availability, avatar
- [Listings — Donor](#listings--donor) — create, list, detail, update, cancel, image upload
- [Listings — Volunteer](#listings--volunteer) — nearby, claim, confirm-pickup, confirm-delivery

## Auth
All 5 endpoints route under `/api/auth`. None require a role policy; `logout` and `me` require any authenticated JWT (`[Authorize]`).

### POST /api/auth/send-otp
Sends a 6-digit OTP via the configured `ISmsProvider` (dev: `MockSmsProvider` logs it at Information level — check the console/`logs/foodbridge-*.log`). Rate-limited to 3 sends per mobile per 15 minutes.

Request:
```json
{ "mobile": "9876543210" }
```
Success (200):
```json
{ "success": true, "message": "OTP sent successfully.", "data": null, "errors": null, "traceId": "..." }
```
Errors:
- 400 — invalid mobile format (`errors: ["Mobile: Mobile must be a valid 10-digit Indian mobile number."]`)
- 429 — rate limit exceeded (`message: "Too many OTP requests. Please try again later."`)

### POST /api/auth/verify-otp
Verifies the OTP (max 5 attempts per code before it must be re-requested).

Request:
```json
{ "mobile": "9876543210", "code": "183006" }
```
Success — existing user (200): `token` is a full auth JWT.
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "isNewUser": false,
    "token": "eyJ...",
    "user": { "id": "...", "mobile": "9876543210", "name": "Green Leaf Restaurant", "role": "Donor", "city": "Ahmedabad", "accountStatus": "Verified", "recipientType": null }
  }
}
```
Success — new mobile (200): `token` is a short-lived (10 min) **registration session token**, not an auth JWT. Pass it as `sessionToken` to `POST /api/auth/register`.
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": { "isNewUser": true, "token": "eyJ...", "user": null }
}
```
Errors (422, same envelope shape, `data: null`):
- `"OTP not found or has expired."`
- `"Maximum verification attempts exceeded. Please request a new OTP."`
- `"Invalid OTP."`

### POST /api/auth/register
Completes registration for a mobile that was just OTP-verified. `sessionToken` is the `token` value from a `verify-otp` response where `isNewUser` was `true`.

Request (Donor/Volunteer):
```json
{
  "sessionToken": "eyJ...",
  "role": "Volunteer",
  "name": "Test Volunteer",
  "city": "Ahmedabad",
  "address": "Satellite, Ahmedabad",
  "latitude": 23.03,
  "longitude": 72.55,
  "recipientType": null,
  "capacityMeals": null
}
```
Request (Recipient — `recipientType` and `capacityMeals` required):
```json
{
  "sessionToken": "eyJ...",
  "role": "Recipient",
  "name": "Hope Community Kitchen",
  "city": "Ahmedabad",
  "address": "Paldi, Ahmedabad",
  "latitude": 23.01,
  "longitude": 72.56,
  "recipientType": "Organization",
  "capacityMeals": 200
}
```
`role` must be `Donor`, `Volunteer`, or `Recipient` (Admin cannot self-register). For `Recipient`: `recipientType` (`Individual` or `Organization`) is required; `capacityMeals` is required and > 0 (household size for `Individual`, daily serving capacity for `Organization` — both are just an int, the distinction is presentational). Donors/Volunteers get `accountStatus: Verified` immediately; Recipients get `Pending` (admin verifies later — Phase 9).

> Note: the original spec also mentions a `dailyRequirement` field for recipients — there's no column for it in the Phase 1 schema, so it isn't accepted here.

Success (200): same shape as verify-otp's existing-user response (`token` + `user`).

Errors:
- 422 — `"Session expired or invalid. Please verify your mobile again."`
- 409 — `"An account with this mobile number already exists."`
- 400 — validation errors (missing name, invalid role, etc.)

### POST /api/auth/logout
`[Authorize]`. Adds the current JWT's `jti` to an in-memory denylist until its natural expiry — stateless JWTs otherwise can't be revoked. Tradeoff: denylist doesn't survive an app restart and doesn't scale across instances (documented in `docs/ARCHITECTURE.md`).

No request body. Success (200):
```json
{ "success": true, "message": "Logged out successfully.", "data": null, "errors": null, "traceId": "..." }
```
401 if no/invalid token.

### GET /api/auth/me
`[Authorize]`. Returns the current user's profile from the `sub` claim.

Success (200):
```json
{ "success": true, "message": "Success", "traceId": "...", "data": { "id": "...", "mobile": "9876543210", "name": "Test Volunteer", "role": "Volunteer", "city": "Ahmedabad", "accountStatus": "Verified", "recipientType": null } }
```
401 if no/invalid/revoked token.

## Users
All 4 endpoints route under `/api/users` and require `[Authorize]` (any authenticated JWT). Authorization beyond that (self-or-admin, self-only, role restriction) is enforced in `UserService`, not via policy attributes, since it depends on the target resource, not just the caller's role.

### GET /api/users/{id}
Callable by the user themselves, or an Admin for any user.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "id": "...", "mobile": "9999900003", "name": "Raj Patel", "role": "Volunteer",
    "city": "Ahmedabad", "address": "Satellite", "latitude": 23.02, "longitude": 72.53,
    "recipientType": null, "capacityMeals": null, "isAvailable": true,
    "accountStatus": "Verified", "avatarUrl": null
  }
}
```
403 if requesting another user's profile without the Admin role.

### PUT /api/users/{id}
Self only (no Admin override). Updating `latitude`/`longitude` also recomputes the `Location` geography column. `capacityMeals` only persists for Recipients (ignored otherwise). `role` and `recipientType` cannot be changed here.

Request:
```json
{ "name": "Raj Patel", "city": "Ahmedabad", "address": "New Address, Satellite", "latitude": 23.05, "longitude": 72.54, "capacityMeals": null }
```
Success (200): same shape as GET. 403 if `id` isn't the caller's own.

### PATCH /api/users/{id}/availability
Self only, **and** only for Volunteers/Recipients — Donors and Admins get 403 even on their own account, since availability has no meaning for those roles.

Request:
```json
{ "isAvailable": false }
```
Success (200): same shape as GET, with `isAvailable` updated.

### POST /api/users/{id}/avatar
Self only. `multipart/form-data` with a `file` field. JPG/PNG only, 2MB max.

Success (200):
```json
{ "success": true, "message": "Success", "traceId": "...", "data": { "avatarUrl": "/uploads/b13da59b-....jpg" } }
```
The returned URL is directly servable (static files under `wwwroot/uploads`). Errors: 422 — `"Avatar must be 2MB or smaller."` / `"Avatar must be a JPG or PNG image."`; 400 if no file attached.

## Listings — Donor
All 6 endpoints route under `/api/listings` and require `[Authorize(Policy = "DonorOnly")]` — any non-Donor role gets 403 on every route. Beyond the role check, ownership (a donor can only see/edit their own listings) is enforced in `ListingService` via `ICurrentUser`, not the policy. `FreshnessTag`, `DietType`, and `MealType` are passed/returned as their enum **string name** — see `docs/ARCHITECTURE.md` § Data dictionary → Enum value tables.

### POST /api/listings
Creates a new listing, starting in the `Pending` status. `dietType`/`mealType` are optional (nullable); `freshnessTag` is required.

Request:
```json
{
  "title": "Surplus Wedding Catering",
  "foodType": "Mixed Veg Meals",
  "dietType": "Veg",
  "mealType": "Dinner",
  "quantityMeals": 80,
  "freshnessTag": "JustCooked",
  "preparedAtUtc": "2026-07-23T08:00:00Z",
  "pickupDeadlineUtc": "2026-07-23T14:00:00Z",
  "pickupAddress": "C.G. Road, Navrangpura",
  "latitude": 23.0338,
  "longitude": 72.5623
}
```
Success (200): a `ListingResponse` (see `GET /api/listings/{id}` below) with `status: "Pending"`, empty `images`, and a single `timeline` entry (`fromStatus: null` → `toStatus: "Pending"`).

Errors: 400 — validation (bad enum name, non-future deadline before `preparedAtUtc`, out-of-range lat/lng, etc.); 403 — caller isn't a Donor.

### GET /api/listings
Lists the caller's **own** listings, paginated, optionally filtered by status.

Query params: `page` (default 1), `pageSize` (default 20, max 100 — clamped server-side), `status` (optional; one of the `Listings.Status` enum names).

Success (200) — `PagedResponse<ListingSummaryResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 5, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [
    { "id": "...", "title": "Surplus Wedding Catering", "foodType": "Mixed Veg Meals", "dietType": "Veg", "mealType": "Dinner", "quantityMeals": 80, "freshnessTag": "JustCooked", "pickupDeadlineUtc": "...", "status": "Pending", "createdAtUtc": "..." }
  ]
}
```
422 — unrecognized `status` value (e.g. `?status=Bogus`).

### GET /api/listings/{id}
Full detail, including images and the timeline. Owning donor only.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "id": "...", "donorId": "...", "title": "Surplus Wedding Catering", "foodType": "Mixed Veg Meals",
    "dietType": "Veg", "mealType": "Dinner", "quantityMeals": 80, "freshnessTag": "JustCooked",
    "preparedAtUtc": "2026-07-23T08:00:00Z", "pickupDeadlineUtc": "2026-07-23T14:00:00Z",
    "pickupAddress": "C.G. Road, Navrangpura", "latitude": 23.0338, "longitude": 72.5623,
    "status": "Pending", "volunteerId": null, "recipientId": null,
    "createdAtUtc": "...", "updatedAtUtc": "...",
    "images": [ { "id": "...", "imageUrl": "/uploads/....jpg", "createdAtUtc": "..." } ],
    "timeline": [ { "fromStatus": null, "toStatus": "Pending", "actorUserId": "...", "note": "Listing created.", "photoUrl": null, "createdAtUtc": "..." } ]
  }
}
```
404 — no such listing. 403 — belongs to a different donor.

### PUT /api/listings/{id}
Updates a listing. Owning donor only, and **only while `Status == Pending`**.

Request: same shape as `POST /api/listings` (all fields required except `dietType`/`mealType`/`preparedAtUtc`).

Success (200): same shape as `GET /api/listings/{id}`. Errors: 403 — not the owner; **422 — `"Only pending listings can be edited."`** once `Claimed` or later (verified live against a seeded `Claimed` listing).

### POST /api/listings/{id}/cancel
Cancels a listing (`Pending → Cancelled`) via `ListingStateMachine`. Owning donor only.

No request body. Success (200): same shape as `GET /api/listings/{id}` with `status: "Cancelled"` and a new timeline entry. 422 — any status other than `Pending` (e.g. cancelling an already-`Cancelled` listing → `"Cannot transition listing from 'Cancelled' to 'Cancelled'."`).

### POST /api/listings/{id}/images
Uploads a photo of the food (JPG/PNG, max 5MB). Owning donor only, and only while `Status == Pending`. `multipart/form-data` with a `file` field.

Success (200):
```json
{ "success": true, "message": "Image uploaded successfully.", "data": { "imageId": "...", "imageUrl": "/uploads/....jpg" }, "errors": null, "traceId": "..." }
```
The URL is directly servable (static files under `wwwroot/uploads`, same as avatars). Errors: 422 — `"Image must be 5MB or smaller."` / `"Image must be a JPG or PNG file."` / `"Images can only be added to pending listings."`; 400 — no file attached.

## Listings — Volunteer
All 4 endpoints route under `/api/listings` and require `[Authorize(Policy = "VolunteerOnly")]` — any non-Volunteer role gets 403. `confirm-pickup`/`confirm-delivery` additionally check that the caller is the listing's assigned `VolunteerId` (403 otherwise, enforced in `VolunteerListingService`, not a policy).

### GET /api/listings/nearby
Lists `Pending` listings within `radiusKm` of the given coordinates, ordered by ascending distance. Listings whose `pickupDeadlineUtc` has already passed are excluded even though their `Status` is still `Pending` (the expiry job that formally flips them hasn't run yet — see `docs/ARCHITECTURE.md` decisions log).

Query params: `latitude`, `longitude` (required), `radiusKm` (optional, default 10, clamped to a max of 50), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<ListingNearbyResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 2, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [
    { "id": "...", "title": "Fresh Test Listing", "foodType": "Sandwiches", "dietType": "Veg", "mealType": "Snacks", "quantityMeals": 20, "freshnessTag": "JustCooked", "pickupDeadlineUtc": "...", "pickupAddress": "C.G. Road, Navrangpura", "latitude": 23.0338, "longitude": 72.5623, "distanceKm": 0 },
    { "id": "...", "title": "Second Fresh Listing", "foodType": "Rice and Curry", "dietType": "NonVeg", "mealType": "Lunch", "quantityMeals": 15, "freshnessTag": "FewHoursOld", "pickupDeadlineUtc": "...", "pickupAddress": "S.G. Highway, Bodakdev", "latitude": 23.0282, "longitude": 72.5061, "distanceKm": 5.79 }
  ]
}
```
422 — `latitude`/`longitude` out of range (e.g. `?latitude=999`).

### POST /api/listings/{id}/claim
Claims a `Pending` listing (`Pending → Claimed`, sets `VolunteerId` to the caller). Any available volunteer may claim any pending listing — no ownership beyond the role check.

No request body. Success (200): a `ListingResponse` (same shape as the Donor detail endpoint) with `status: "Claimed"`.

**409** — the listing is no longer `Pending` (already claimed by someone else, cancelled, expired, etc.) — `"Listing is no longer available to claim (current status: {status})."`. Verified live: two concurrent claim requests for the same listing resolve to exactly one 200 and one 409, backed by a conditional `UPDATE ... WHERE Status = Pending` rather than the `RowVersion` column. 404 — no such listing.

### POST /api/listings/{id}/confirm-pickup
Confirms pickup (`Claimed → PickedUp`). Assigned volunteer only. `multipart/form-data` with a required `photo` field (JPG/PNG, max 5MB). Auto-matches the nearest available Verified recipient via `RecipientMatcher` if the listing doesn't already have one.

Success (200): same shape as `claim`, with `status: "PickedUp"`, a new timeline entry carrying `photoUrl`, and `recipientId` populated if a match was found (stays `null` if no recipient is currently available — pickup still succeeds).

Errors: 403 — not the assigned volunteer; 422 — `photo` missing/wrong type/too large, or the listing isn't currently `Claimed` (`"Cannot transition listing from '{status}' to 'PickedUp'."`); 400 — no file attached.

### POST /api/listings/{id}/confirm-delivery
Confirms delivery (`PickedUp → Delivered`). Assigned volunteer only; requires a recipient to already be matched. `multipart/form-data` with a required `photo` field (JPG/PNG, max 5MB).

Success (200): same shape, `status: "Delivered"`, new timeline entry with `photoUrl`.

Errors: 403 — not the assigned volunteer; 422 — `"Cannot confirm delivery before a recipient has been matched."` (no `recipientId` yet), `photo` missing/wrong type/too large, or the listing isn't currently `PickedUp`; 400 — no file attached.

## Listings — Recipient

## Notifications & real-time (SignalR contract)

## Certificates, Leaderboard, Reports

## Admin

## Chart data contract
