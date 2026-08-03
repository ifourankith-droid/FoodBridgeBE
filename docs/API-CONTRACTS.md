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
- [Donor Addresses](#donor-addresses) — create, list, detail, update, delete a saved address book
- [Listings — Donor](#listings--donor) — create, list, detail, update, cancel, image upload
- [Listings — Volunteer](#listings--volunteer) — nearby, claim, unclaim, confirm-pickup, confirm-delivery
- [Listings — Recipient](#listings--recipient) — available-nearby, request, incoming, accept, reject, confirm-receipt, history
- [Notifications & real-time (SignalR contract)](#notifications--real-time-signalr-contract) — notifications list/read, tracking, geocode
- [Certificates, Leaderboard, Reports](#certificates-leaderboard-reports) — certificate list/detail/pdf, leaderboard (+ my-rank), donor/volunteer/recipient reports
- [Dashboard](#dashboard) — one consolidated per-role dashboard endpoint (Donor/Volunteer/Recipient)
- [Admin](#admin) — dashboard, listings/accounts browse, verify/suspend, disputes (raise/list/resolve), platform report
- [Drop-off Locations](#drop-off-locations) — admin CRUD for partner sites, plus the `hotspots` map for whoever is delivering (shared pool, cooldown-aware)

## Auth
All 5 endpoints route under `/api/auth`. None require a role policy; `logout` and `me` require any authenticated JWT (`[Authorize]`).

### POST /api/auth/send-otp
Sends a 6-digit OTP via the configured `ISmsProvider` (dev: `MockSmsProvider` logs it at Information level — check the console/`logs/foodbridge-*.log`).

**Rate limit** — `OtpRateLimit:MaxSendsPerWindow` sends per mobile per `OtpRateLimit:WindowMinutes` (defaults 3 / 15). Keyed on the **mobile number only**, not IP or device, so changing browser or network doesn't reset it. Every OTP issued in the window counts, *including ones used successfully* — a clean login consumes one.

The window **slides**: capacity returns one send at a time as individual sends age out, not all at once. Sends at 10:00/10:01/10:02 are blocked until 10:15, which frees exactly one. Waiting the full window from your *last* attempt always works, and that is what the 429 message quotes.

Set `OtpRateLimit__MaxSendsPerWindow` to `0` to disable the limit entirely (demo only — the app logs a Warning on every startup while it's off).

> **Dev shortcut**: `appsettings.Development.json`'s `Otp:FixedDevelopmentCode` (default `123456`) makes every OTP that fixed value — skip the log, just call `verify-otp` with `123456`. See `docs/ARCHITECTURE.md` § Seed data for the seeded mobile numbers per role. Never active outside Development (see the decisions log).

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
- 429 — rate limit exceeded (`message: "Too many OTP requests. Please try again in 15 minutes."`, quoting the configured window)

### POST /api/auth/verify-otp
Verifies the OTP. An OTP is valid for **5 minutes**, is single-use (consumed on success), and tolerates `OtpRateLimit:MaxVerifyAttempts` wrong codes (default 5) before it locks.

> Exceeding the attempt limit returns **422**, not 429, with `"Maximum verification attempts exceeded. Please request a new OTP."` — note that following that advice can itself hit the send-side 429 above if the window is exhausted. Set `OtpRateLimit__MaxVerifyAttempts` to `0` to disable the attempt limit (demo only).

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
    "user": { "id": "...", "mobile": "9876543210", "name": "Green Leaf Restaurant", "role": "Donor", "city": "Ahmedabad", "accountStatus": "Verified", "recipientType": null, "avatarUrl": null }
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
  "state": "Gujarat",
  "pincode": "380015",
  "address": "Satellite, Ahmedabad",
  "latitude": 23.03,
  "longitude": 72.55,
  "recipientType": null,
  "capacityMeals": null
}
```
`state` and `pincode` are optional (both default to null if omitted). `pincode` must be exactly 6 digits when supplied — 400 otherwise. Both are **display-only**: every distance and nearby query runs off `latitude`/`longitude`.

For a **Donor**, a supplied `address` + coordinates are also saved into their address book as a default `"Home"` entry, carrying `city`/`state`/`pincode` across, so their first donation needs no re-typing.
> _(see also `POST /api/listings` below — it now requires `acceptedFoodSafety`.)_

> **`role: "Recipient"` is currently refused** with 422 `"Recipient registration is currently unavailable. Please register as a Donor or a Volunteer."` — the platform runs on three roles (Donor, Volunteer, Admin) while `Features:RecipientRoleEnabled` is `false`. Accounts registered before the switch keep working normally. The Recipient request shape below is documented for when the flag is turned back on. See `docs/ARCHITECTURE.md` → Phase 15.

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
`role` must be `Donor`, `Volunteer`, or `Recipient` (Admin cannot self-register). For `Recipient`: `recipientType` (`Individual` or `Organization`) is required; `capacityMeals` is required and > 0 (household size for `Individual`, daily serving capacity for `Organization` — both are just an int, the distinction is presentational).

**Initial `accountStatus` by role:**

| Role | Status | Why |
|---|---|---|
| Donor | `Verified` | A fake donor mostly wastes a volunteer's trip; gating them would block real surplus from being posted while it spoils. |
| **Volunteer** | **`Pending`** | Takes physical custody of a stranger's food and travels with it. Must upload an ID + selfie and be approved before they can claim — see [`/verification`](#get-apiusersidverification). |
| Recipient | `Pending` | Admin verifies before the matcher will route food to them. (Role currently disabled — Phase 15.) |

> Note: the original spec also mentions a `dailyRequirement` field for recipients — there's no column for it in the Phase 1 schema, so it isn't accepted here.

Success (200): same shape as verify-otp's existing-user response (`token` + `user`).

Errors:
- 422 — `"Session expired or invalid. Please verify your mobile again."`
- 422 — `"Recipient registration is currently unavailable. Please register as a Donor or a Volunteer."` (`role: "Recipient"` while the role is disabled)
- 409 — `"An account with this mobile number already exists."`
- 400 — validation errors (missing name, invalid role, etc.)

### GET /api/users/{id}/verification
`[Authorize]`, **self or admin** — the volunteer tracks their own progress, the admin reads the same payload to review it.

Success (200) — `UserVerificationResponse`:
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "userId": "...", "role": "Volunteer", "accountStatus": "Pending",
    "documents": [
      { "id": "...", "type": "IdProof", "fileUrl": "/uploads/....jpg", "originalFileName": "my-aadhaar.jpg", "uploadedAtUtc": "2026-07-31T00:20:11" }
    ],
    "requiredDocumentTypes": ["IdProof", "Selfie"],
    "missingDocumentTypes": ["Selfie"],
    "isReadyForReview": false
  }
}
```
`isReadyForReview` is `true` only when the account is still `Pending` **and** nothing is missing — i.e. the ball is with the admin, not the user. Computed server-side; don't re-derive it. `requiredDocumentTypes` is `[]` for roles that need no documents (Donor), so a client can render "nothing needed" without hard-coding which roles those are.

Errors: 403 — neither self nor admin; 404 — no such user.

### POST /api/users/{id}/documents
`[Authorize]`, **self only** — nobody, admin included, submits evidence on another person's behalf; the point is that it came from them.

`multipart/form-data` with `file` (required) and `type` (required: `IdProof` or `Selfie`). Max 5MB; JPG/PNG/PDF — except a **`Selfie` must be an image**, since a PDF there would defeat comparing a face against the ID.

**Re-uploading the same `type` replaces it** and deletes the superseded file, so a bad photo can simply be retaken rather than leaving the admin to guess which copy is current.

Success (200): the refreshed `UserVerificationResponse` (same shape as above), so the client updates its checklist from one round-trip.

Errors: 403 — not self; 422 — unknown `type`, a type this role doesn't need, file too large, wrong extension, or a PDF selfie; 400 — no file or no type; 404 — no such user.

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
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "id": "...", "mobile": "9876543210", "name": "Test Volunteer", "role": "Volunteer",
    "city": "Ahmedabad", "accountStatus": "Verified", "recipientType": null, "avatarUrl": null,
    "address": {
      "label": "Home", "address": "Satellite, Ahmedabad", "city": "Ahmedabad",
      "state": "Gujarat", "pincode": "380015", "latitude": 23.03, "longitude": 72.55
    }
  }
}
```
401 if no/invalid/revoked token.

**`address` is the complete postal address, or `null`** when the account has none. Which address it is depends on the role:

| Role | Source | `label` |
| --- | --- | --- |
| Donor | Their **default saved address** (`GET /api/donor-addresses`, `isDefault` first) — the one a new donation would be posted from | The saved label, e.g. `"Home"` |
| Volunteer / Admin | The account's own address on the user row (no address book exists for them) | `null` |
| Donor with no saved addresses | Falls back to the account's own address | `null` |

Registration seeds the two identically, so for a donor they only diverge once the address book is edited. Every part inside `address` is independently nullable — render what's present rather than assuming a shape.

Top-level `city` is retained alongside it: it predates the block and always mirrors the user row, so it can differ from `address.city` when a donor's default address is in another city.

`avatarUrl` (also present on `register`'s and `verify-otp`'s embedded `user`) mirrors `GET /api/users/{id}`'s field of the same name — populated once `POST /api/users/{id}/avatar` has been called at least once, `null` until then. Kept in sync so a client never needs a second call just to render the logged-in user's own avatar.

> `register` and `verify-otp` embed the same `UserResponse`, but their `address` is always the account's own (label `null`) — the address book isn't queried on those paths, since a brand-new account has nothing but what it just sent.

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
    "city": "Ahmedabad", "state": "Gujarat", "pincode": "380015",
    "address": "Satellite", "latitude": 23.02, "longitude": 72.53,
    "recipientType": null, "capacityMeals": null, "isAvailable": true,
    "accountStatus": "Verified", "avatarUrl": null
  }
}
```
Unlike `/auth/me`, `address` here is the plain street string on the user row — this endpoint returns the account's own fields flat, never the address book.

403 if requesting another user's profile without the Admin role.

### PUT /api/users/{id}
Self only (no Admin override). Updating `latitude`/`longitude` also recomputes the `Location` geography column. `capacityMeals` only persists for Recipients (ignored otherwise). `role` and `recipientType` cannot be changed here.

Request:
```json
{ "name": "Raj Patel", "city": "Ahmedabad", "state": "Gujarat", "pincode": "380015", "address": "New Address, Satellite", "latitude": 23.05, "longitude": 72.54, "capacityMeals": null }
```
`state` and `pincode` are optional; `pincode` must be 6 digits when supplied.

> **This is a full replace, not a patch.** Any field omitted is written as null — so echo back the values you don't intend to change. Clients that update only coordinates (e.g. going online) must still send `city`/`state`/`pincode`/`address` or they will be erased.

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

## Donor Addresses
All 5 endpoints route under `/api/donor-addresses` and require `[Authorize(Policy = "DonorOnly")]` — self only throughout, enforced in `DonorAddressService` via `ICurrentUser`. A donor's saved address book, independent of `Users.Address` (a single profile address) and `Listings.PickupAddress` (still freeform per listing) — lets a donor with multiple locations (e.g. restaurant branches) save each once and reuse it on `POST /api/listings` via `donorAddressId` instead of retyping it every time.

### POST /api/donor-addresses
Saves a new address. Setting `isDefault: true` clears it on every other address the caller owns (only one default at a time).

Request:
```json
{
  "label": "Main Branch", "address": "C.G. Road, Navrangpura",
  "city": "Ahmedabad", "state": "Gujarat", "pincode": "380009",
  "latitude": 23.0338, "longitude": 72.5623, "isDefault": true
}
```
Success (200):
```json
{
  "success": true, "message": "Address saved successfully.", "traceId": "...",
  "data": {
    "id": "...", "label": "Main Branch", "address": "C.G. Road, Navrangpura",
    "city": "Ahmedabad", "state": "Gujarat", "pincode": "380009",
    "latitude": 23.0338, "longitude": 72.5623, "isDefault": true,
    "createdAtUtc": "...", "updatedAtUtc": "..."
  }
}
```
`city`, `state` and `pincode` are optional and independent of the account's own — a branch can be in another city. Blank is stored as null. They are display-only: `POST /api/listings` still resolves the pickup point from the saved `latitude`/`longitude`.

400 — validation (missing label/address, out-of-range lat/lng, `pincode` not exactly 6 digits).

### GET /api/donor-addresses
Lists the caller's own saved addresses, default-first then newest-first. Query params: `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<DonorAddressResponse>` (same item shape as create's response).

### GET /api/donor-addresses/{id}
Self only. Success (200): a single `DonorAddressResponse`. 403 — belongs to a different donor; 404 — no such address.

### PUT /api/donor-addresses/{id}
Self only. Request: same shape as create. Success (200): the updated `DonorAddressResponse`. Errors: same as create, plus 403/404 as above.

> **Full replace, not a patch** — an omitted `city`/`state`/`pincode` is written as null. Flipping `isDefault` on an existing address means re-sending its other fields, or they are erased.

### DELETE /api/donor-addresses/{id}
Self only. Hard delete — `DonorAddresses` isn't one of the two tables (`Users`, `Listings`) CLAUDE.md's soft-delete convention covers, and nothing else references a saved address by id (listings that used one already copied its `pickupAddress`/`latitude`/`longitude` at creation time, so deleting the saved address afterward doesn't affect any existing listing).

No request body. Success (200): empty `data`. 403 — belongs to a different donor; 404 — no such address.

## Listings — Donor
All 6 endpoints route under `/api/listings` and require `[Authorize(Policy = "DonorOnly")]` — any non-Donor role gets 403 on every route. Beyond the role check, ownership (a donor can only see/edit their own listings) is enforced in `ListingService` via `ICurrentUser`, not the policy. `FreshnessTag`, `DietType`, and `MealType` are passed/returned as their enum **string name** — see `docs/ARCHITECTURE.md` § Data dictionary → Enum value tables.

### POST /api/listings
Creates a new listing, starting in the `Pending` status. `dietType`/`mealType` are optional (nullable); `freshnessTag` is required. The pickup location comes from **either** `donorAddressId` (a saved address from [`GET /api/donor-addresses`](#donor-addresses)) **or** `pickupAddress`+`latitude`+`longitude` together — exactly one path, never both, never neither.

Request (freeform address):
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
  "longitude": 72.5623,
  "acceptedFoodSafety": true
}
```

**`acceptedFoodSafety` is required and must be `true`.** It is the donor's confirmation that the food is safe to eat and that its quality remains their responsibility — collected in a dialog before the donation is posted, and shown to them as a short plain-English checklist rather than legalese.

Enforced on the API, not just in the UI: the declaration is only meaningful if it was actually given for *this* donation, and a client-side-only checkbox is bypassed by calling this endpoint directly. **Omitting the field deserialises to `false` and is refused** — a caller written against the older contract is rejected rather than silently treated as having agreed:

```json
{
  "success": false, "message": "One or more validation errors occurred.", "data": null,
  "errors": ["AcceptedFoodSafety: Please confirm the food is safe to eat and that you take responsibility for its quality before posting this donation."],
  "traceId": "..."
}
```

On success the server stamps `foodSafetyAcceptedAtUtc` on the listing from `IClock` (never from the request) and returns it on `ListingResponse`. `PUT /api/listings/{id}` does **not** re-ask — the donor already declared this food safe when they posted it, and the timestamp is preserved through an edit.
Request (saved address instead — same fields otherwise):
```json
{ "...": "same as above, minus pickupAddress/latitude/longitude", "donorAddressId": "..." }
```
Success (200): a `ListingResponse` (see `GET /api/listings/{id}` below) with `status: "Pending"`, `pickupAddress`/`latitude`/`longitude` resolved from whichever path was used, empty `images`, and a single `timeline` entry (`fromStatus: null` → `toStatus: "Pending"`).

**Side effect — real-time volunteer alert:** every available (`isAvailable = true`), `Verified`, non-deleted Volunteer within 10km of the listing's pickup point gets a `Notifications` row (`type: "NewListingNearby"`) inserted in the same transaction as the listing, then a best-effort live push over `ReceiveNotification` on `/hubs/notifications` (see [Notifications & real-time](#notifications--real-time-signalr-contract) below) once the write commits. A volunteer with no open connection still sees it via `GET /api/notifications`.

Errors: 400 — validation (bad enum name, non-future deadline before `preparedAtUtc`, out-of-range lat/lng, neither/both of `donorAddressId` vs. the freeform fields provided); 403 — caller isn't a Donor, or `donorAddressId` belongs to a different donor; 404 — `donorAddressId` doesn't exist.

### GET /api/listings
Lists the caller's **own** listings, paginated, optionally filtered by status, diet type, and/or meal type.

Query params: `page` (default 1), `pageSize` (default 20, max 100 — clamped server-side), `status` (optional; one of the `Listings.Status` enum names), `dietType` (optional; `Veg`/`NonVeg`), `mealType` (optional; `Breakfast`/`Lunch`/`Dinner`/`Snacks`). 422 — an unrecognized value for any of the three.

Success (200) — `PagedResponse<ListingSummaryResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 5, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [
    { "id": "...", "title": "Surplus Wedding Catering", "foodType": "Mixed Veg Meals", "dietType": "Veg", "mealType": "Dinner", "quantityMeals": 80, "freshnessTag": "JustCooked", "pickupDeadlineUtc": "...", "status": "Pending", "createdAtUtc": "...", "imageUrl": "/uploads/....jpg" }
  ]
}
```
`imageUrl` is the listing's first uploaded photo (earliest by `CreatedAtUtc`), or `null` when it has none — a thumbnail for list rows without loading every image.

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
    "status": "Pending", "volunteerId": null, "recipientId": null, "estimatedPickupAtUtc": null,
    "donorName": "Green Leaf Restaurant", "donorMobile": "9999900001",
    "volunteerName": null, "volunteerMobile": null, "recipientName": null, "recipientMobile": null,
    "createdAtUtc": "...", "updatedAtUtc": "...",
    "images": [ { "id": "...", "imageUrl": "/uploads/....jpg", "createdAtUtc": "..." } ],
    "timeline": [ { "fromStatus": null, "toStatus": "Pending", "actorUserId": "...", "note": "Listing created.", "photoUrl": null, "createdAtUtc": "..." } ],
    "suggestedDropOffLocation": null
  }
}
```
`donorName`/`donorMobile` are always populated; `volunteerName`/`volunteerMobile`/`recipientName`/`recipientMobile` populate once `volunteerId`/`recipientId` are set (`null` until then) — this is every party's way to coordinate the physical handoff (who's picking up, who's delivering to whom), and every caller of this same `ListingResponse` shape (donor/volunteer/recipient endpoints below) is already restricted to a party on that specific listing, so this never leaks contact info to anyone uninvolved. `estimatedPickupAtUtc` is set only if the claiming volunteer gave one (see claim, below); cleared back to `null` on unclaim. `suggestedDropOffLocation` (see [Drop-off Locations](#drop-off-locations-admin)) is only ever non-null in the `confirm-pickup` response itself, when no recipient could be matched — see that endpoint. 404 — no such listing. 403 — belongs to a different donor.

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
All 5 endpoints route under `/api/listings` and require `[Authorize(Policy = "VolunteerOnly")]` — any non-Volunteer role gets 403. `unclaim`/`confirm-pickup`/`confirm-delivery` additionally check that the caller is the listing's assigned `VolunteerId` (403 otherwise, enforced in `VolunteerListingService`, not a policy).

> **Verification gate.** A volunteer registers as `AccountStatus.Pending` and must be approved by an admin (see [`/verification`](#get-apiusersidverification)) before they can take on work. `claim` and `confirm-pickup` return **422** for a `Pending` or `Suspended` volunteer:
> - Pending — `"Your account is still being verified. Upload your ID and selfie, and an admin will review it shortly."`
> - Suspended — `"Your account has been suspended. Please contact support."`
>
> `nearby` is **not** gated — an unverified volunteer can browse and see the hotspot map, they just can't act. `confirm-delivery` and `unclaim` are **deliberately not gated either**: a volunteer suspended while already carrying food must still be able to record where it went (otherwise the food is stranded with no audit trail), and releasing a claim is the outcome we want. So a mid-flight suspension stops them taking on anything *new* while still letting the current listing reach a clean end state.

### GET /api/listings/nearby
Lists `Pending` listings within `radiusKm` of the given coordinates, ordered by ascending distance. Listings whose `pickupDeadlineUtc` has already passed are excluded even though their `Status` is still `Pending` (the expiry job that formally flips them hasn't run yet — see `docs/ARCHITECTURE.md` decisions log).

Query params: `latitude`, `longitude` (required), `radiusKm` (optional, default 10, clamped to a max of 50), `dietType` (optional; `Veg`/`NonVeg`), `mealType` (optional; `Breakfast`/`Lunch`/`Dinner`/`Snacks`), `page` (default 1), `pageSize` (default 20, max 100).

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

No request body. Optional query parameter `estimatedPickupAtUtc` (UTC ISO-8601, e.g. `?estimatedPickupAtUtc=2026-07-27T18:00:00Z`) lets a volunteer flexibly commit ("I'll take it, but I'm coming in an hour") instead of only being able to claim right before showing up — omit it for an implied immediate pickup, same as before this field existed. Deliberately a query parameter and not a JSON body: `[FromBody]` model binding 415s a request sent with no `Content-Type` header at all, which would have silently broken every existing bodyless caller of this endpoint (found via live testing, not anticipated — see `docs/ARCHITECTURE.md` decisions log). Success (200): a `ListingResponse` (same shape as the Donor detail endpoint) with `status: "Claimed"` and `estimatedPickupAtUtc` echoed back.

**409** — the listing is no longer `Pending` (already claimed by someone else, cancelled, expired, etc.) — `"Listing is no longer available to claim (current status: {status})."`. Verified live: two concurrent claim requests for the same listing resolve to exactly one 200 and one 409, backed by a conditional `UPDATE ... WHERE Status = Pending` rather than the `RowVersion` column. **422** — `estimatedPickupAtUtc` is in the past (`"EstimatedPickupAtUtc must be in the future."`) or later than the listing's own `pickupDeadlineUtc` (`"EstimatedPickupAtUtc cannot be later than the listing's pickup deadline."`). 404 — no such listing. `estimatedPickupAtUtc` is cleared back to `null` on `unclaim`.

### POST /api/listings/{id}/unclaim
Voluntarily releases a claim (`Claimed → Pending`), making the listing available for someone else to claim — e.g. the volunteer realizes they can't make it. Assigned volunteer only.

No request body. Success (200): a `ListingResponse` with `status: "Pending"` and `volunteerId: null` again. Errors: 403 — not the assigned volunteer; 422 — the listing is no longer `Claimed` (e.g. pickup was already confirmed — there's no undoing a physical pickup); 404 — no such listing.

If a volunteer claims and simply goes silent instead of calling this explicitly, the same `Claimed → Pending` transition happens automatically once `pickupDeadlineUtc` passes — see the expiry job note in `docs/ARCHITECTURE.md` decisions log.

### POST /api/listings/{id}/confirm-pickup
Confirms pickup (`Claimed → PickedUp`). Assigned volunteer only. `multipart/form-data` with a required `photo` field (JPG/PNG, max 5MB). Auto-matches the nearest available Verified recipient via `RecipientMatcher` if the listing doesn't already have one — **only while `Features:RecipientRoleEnabled` is true.** With the Recipient role disabled (the default) no matching happens at all, so `recipientId` stays `null` and `suggestedDropOffLocation` is always the destination. A `recipientId` the listing already carries (a recipient reserved it via `request`) is never overwritten either way.

Success (200): same shape as `claim`, with `status: "PickedUp"`, a new timeline entry carrying `photoUrl`, and `recipientId` populated if a match was found (stays `null` if no recipient is currently available — pickup still succeeds). **When `recipientId` is still `null`, this response's `suggestedDropOffLocation` is populated with the nearest active [drop-off location](#drop-off-locations-admin)** (or `null` if none are configured) — a place to take the food instead of standing there with nowhere to go.

Errors: 403 — not the assigned volunteer; 422 — `photo` missing/wrong type/too large, or the listing isn't currently `Claimed` (`"Cannot transition listing from '{status}' to 'PickedUp'."`); 400 — no file attached.

### POST /api/listings/{id}/confirm-delivery
Confirms delivery. Assigned volunteer only. `multipart/form-data` with a required `photo` field (JPG/PNG, max 5MB).

**Recording where the food went is required.** Send exactly one of:

| Form fields | Meaning |
|---|---|
| `dropOffLocationId` | A spot that already exists (get candidates from [`GET /api/dropoff-locations/hotspots`](#get-apidropoff-locationshotspots)). Must exist and be active. |
| `latitude` + `longitude` + `locationName` (+ optional `locationAddress`) | A spot the volunteer found in the field. **Saved to the shared pool** (`source: "Volunteer"`, active immediately) so every volunteer can use it next time. |

Supplying both forms, or neither, is a 422 — as is an unknown/inactive `dropOffLocationId`, a new spot with no name, or out-of-range coordinates. The new location and its delivery-log row are written in the same transaction as the status change.

**Ends in one of two states, decided by whether the listing has a `recipientId` — not by the caller:**

| `recipientId` | Transition | What happens |
|---|---|---|
| set | `PickedUp → Delivered` | The matched recipient still has to call `confirm-receipt`. Message: `"Delivery confirmed successfully."` |
| `null` | `PickedUp → Confirmed` | **Completes the donation** — nobody is waiting to receive it (the food went to a drop-off location), so this call awards the volunteer's points, issues the donor's certificate, and notifies both, all in the same transaction `confirm-receipt` uses. Message: `"Delivery confirmed — donation completed and {points} points awarded."` |

A `null` `recipientId` is the normal case while the Recipient role is disabled (`Features:RecipientRoleEnabled = false`), since `confirm-pickup` no longer auto-matches anyone. See `docs/ARCHITECTURE.md` → Phase 15.

Success (200): same shape as `confirm-pickup`, `status` either `"Delivered"` or `"Confirmed"` per the table, plus a new timeline entry carrying `photoUrl`.

Errors: 403 — not the assigned volunteer; 422 — `photo` missing/wrong type/too large, the listing isn't currently `PickedUp`, or any of the drop-off problems above; 400 — no file attached.

> The previous `"Cannot confirm delivery before a recipient has been matched."` (422) no longer exists — an unmatched listing is now the completing path rather than an error.

### GET /api/dropoff-locations/hotspots
`[Authorize(Policy = "CanDropOff")]` — **Volunteer or Donor**; Admin gets 403 (they have the full CRUD list instead). The map of where food has been delivered before, so whoever is carrying it can see where demand concentrates. Donors need it for [`POST /listings/{id}/self-deliver`](#post-apilistingsidself-deliver), which offers the identical list.

Query params: `latitude`, `longitude` (required), `radiusKm` (optional; default `DropOff:HotspotRadiusKm` = 10, clamped to `MaxHotspotRadiusKm` = 50), `page`, `pageSize`.

**Ordered available-first, then nearest** — so `data[0]` is the best next destination. Don't re-sort client-side by distance alone, or a cooling-down spot floats to the top. Inactive spots are excluded entirely; spots on **cooldown are included but flagged**, so the map can explain why a closer spot isn't being suggested.

Success (200) — `PagedResponse<DropOffHotspotResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 5, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [
    {
      "id": "...", "name": "Hope NGO Collection Point", "address": "Paldi",
      "latitude": 23.0089, "longitude": 72.5601, "city": "Ahmedabad",
      "source": "Admin", "distanceKm": 2.9,
      "deliveryCount": 0, "totalMeals": 0, "lastDeliveredAtUtc": null,
      "isCoolingDown": false, "cooldownUntilUtc": null
    },
    {
      "id": "...", "name": "Paldi underbridge camp", "address": "Under the Paldi flyover, west side",
      "latitude": 23.041, "longitude": 72.552, "city": null,
      "source": "Volunteer", "distanceKm": 1.1,
      "deliveryCount": 1, "totalMeals": 12, "lastDeliveredAtUtc": "2026-07-30T18:05:11",
      "isCoolingDown": true, "cooldownUntilUtc": "2026-07-30T23:05:11"
    }
  ]
}
```

**Cooldown.** After a delivery, a spot is hidden from the single-suggestion paths (`confirm-pickup`'s `suggestedDropOffLocation`, and reject-exhaustion's `DropOffLocationSuggested` notification) for `DropOff:CooldownHours` (default 5). It applies to **every volunteer, not just the one who delivered** — the place itself has been served, and another volunteer has no way of knowing that. `cooldownUntilUtc` says when it frees up.

Errors: 422 — latitude/longitude out of range; 403 — not a Volunteer.

## Listings — Recipient
All 8 endpoints route under `/api/listings` and require `[Authorize(Policy = "RecipientOnly")]`. `accept`/`reject`/`confirm-receipt`/`request` (DELETE) additionally check that the caller is the listing's current `RecipientId` (403 otherwise, enforced in `RecipientListingService`) — once a recipient rejects, they're no longer matched and lose access to that listing.

**Two ways a recipient gets food.** *Push* (the original): nobody involves the recipient until a volunteer confirms pickup, at which point `RecipientMatcher` assigns the nearest available Verified recipient and it appears in `incoming`. *Pull* (`available-nearby` + `request`): the recipient browses uncollected donations around them and reserves one, which pre-sets `RecipientId` so confirm-pickup keeps it instead of running the matcher (`ConfirmPickupAsync` only matches when `RecipientId is null`). Either way the listing still lands in `incoming` at `PickedUp` for the usual accept/reject decision — requesting reserves the *destination*, it never moves the listing through the state machine.

### GET /api/listings/available-nearby
Donations the caller could still receive, nearest first: `Status IN (Pending, Claimed)`, `PickupDeadlineUtc > now`, not deleted, and either unmatched or already requested by the caller. `Claimed` is included deliberately — a volunteer already being on the way is the *most* likely donation to arrive.

Query params: `latitude`, `longitude` (required), `radiusKm` (default 10, max 50 — values ≤ 0 fall back to the default), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<ListingAvailableNearbyResponse>`:
```json
{
  "id": "...", "title": "Surplus Wedding Catering", "foodType": "Mixed Veg Meals",
  "dietType": "Veg", "mealType": "Dinner", "quantityMeals": 50, "freshnessTag": "JustCooked",
  "pickupDeadlineUtc": "2026-07-28T18:30:00Z", "pickupAddress": "Paldi, Ahmedabad",
  "latitude": 23.0089, "longitude": 72.5601, "distanceKm": 2.1,
  "status": "Claimed", "isRequestedByMe": false
}
```
Errors: 422 — `latitude`/`longitude` out of range.

### POST /api/listings/{id}/request
Reserves an unmatched donation for the caller. No request body.

Requires the caller's `accountStatus` to be `Verified` — the same bar `RecipientMatcher` applies, so an unverified NGO can't reserve what it could never be routed. Concurrency-safe: the reservation is a conditional `UPDATE … WHERE RecipientId IS NULL`, so exactly one of two racing NGOs wins. If the listing already has a volunteer, they get a `RecipientRequested` notification ("Drop-off confirmed") inserted in the same transaction.

Success (200): a `ListingResponse` with `recipientId` set, `status` unchanged. Idempotent — re-requesting a listing you already hold returns 200 with `"Donation already requested."`.

Errors: 404 — no such listing; 409 — `"This donation has already been matched to another organization."` / `"This donation is no longer available to request."` (lost the race); 422 — caller not Verified, `"Only a donation that hasn't been collected yet can be requested (current status: X)."`, or `"This donation's pickup window has already closed."`.

### DELETE /api/listings/{id}/request
Releases a request the caller made, while the food is still uncollected — without this an accidental request would pin the listing to that NGO permanently, since the matcher only runs when `RecipientId is null`.

Success (200): a `ListingResponse` with `recipientId` cleared. Errors: 404 — no such listing; 403 — not the requesting recipient; 409 — no longer withdrawable; 422 — `"The food has already been collected — accept or reject it instead."` (status is past `Claimed`).

### GET /api/listings/incoming
Lists listings currently matched to the caller and awaiting an accept/reject decision (`Status = PickedUp`, `RecipientId = caller`). Query params: `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<ListingSummaryResponse>` (same item shape as the Donor list endpoint).

### POST /api/listings/{id}/accept
Acknowledges the incoming match. Doesn't change `Status` — just records a timeline entry.

No request body. Success (200): a `ListingResponse`, `status` unchanged (`"PickedUp"`). Errors: 403 — not the matched recipient; 422 — `"Only an in-transit listing awaiting your decision can be accepted or rejected."` (e.g. already rejected, or not yet picked up).

### POST /api/listings/{id}/reject
Declines the match. Auto-reassigns to the nearest other available Verified recipient via `RecipientMatcher`, excluding every recipient who has already rejected this same listing (not just the current one — otherwise two recipients could reassign back and forth forever). `Status` stays `"PickedUp"` throughout.

No request body. Success (200): a `ListingResponse` with `recipientId` set to the new match, or `null` if none is currently available — check the last `timeline` entry's `note` for which outcome occurred (`"Reassigned to another available recipient."` vs `"No other recipient is currently available."`). Errors: same as `accept`.

**When every recipient is exhausted (`recipientId: null`), the assigned volunteer — not the rejecting recipient calling this endpoint — is pushed a `DropOffLocationSuggested` notification** (live over `/hubs/notifications` if connected, `GET /api/notifications` otherwise) naming the nearest active [drop-off location](#drop-off-locations-admin). It doesn't appear in *this* response, since the volunteer isn't the caller here.

### POST /api/listings/{id}/confirm-receipt
Confirms receipt (`Delivered → Confirmed`). Atomically (one transaction): inserts a timeline event, awards the volunteer `quantityMeals × 1` points, issues a donor certificate (`CertificateNumber` format `FB-{yyyyMM}-{seq:D5}`; `PdfUrl` stays null until Phase 8 renders it), and creates one notification each for the donor and the volunteer.

No request body. Success (200):
```json
{
  "success": true, "message": "Receipt confirmed successfully.", "traceId": "...",
  "data": {
    "listing": { "...": "same shape as GET /api/listings/{id}, status: \"Confirmed\"" },
    "certificateNumber": "FB-202607-00001",
    "pointsAwarded": 50
  }
}
```
Errors: 403 — not the matched recipient; 422 — the listing isn't currently `Delivered` (`"Cannot transition listing from '{status}' to 'Confirmed'."`), including on a repeat call once already `Confirmed`.

### GET /api/listings/history
Lists the caller's past confirmed receipts (`Status = Confirmed`, `RecipientId = caller`). Same query params and response shape as `incoming`.

## Notifications & real-time (SignalR contract)
Live delivery is via SignalR (`/hubs/notifications`, `/hubs/tracking`) — full event/method contract in `docs/ARCHITECTURE.md` § Real-time (SignalR) contract, including the `access_token` query-string auth required for the hub connection itself. The endpoints below are the REST fallbacks for clients that aren't (or can't stay) connected.

### GET /api/notifications
`[Authorize]`, any role. Lists the caller's own notifications, optionally filtered by read status. Known `type` values: `NewListingNearby` (Volunteer, on listing creation near them), `DonationConfirmed` (Donor, on confirm-receipt), `PointsAwarded` (Volunteer, on confirm-receipt), `DropOffLocationSuggested` (Volunteer, on reject exhausting every recipient).

Query params: `isRead` (optional bool), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<NotificationResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "type": "NewListingNearby", "title": "New pickup available near you", "body": "'...' — 80 meals ready for pickup near C.G. Road, Navrangpura.", "payloadJson": null, "isRead": false, "createdAtUtc": "..." } ]
}
```

### PATCH /api/notifications/{id}/read
`[Authorize]`, self only. Marks a notification read (idempotent — re-marking an already-read one just returns it unchanged).

No request body. Success (200): the updated `NotificationResponse` (`isRead: true`). 403 — not the notification's owner; 404 — no such notification.

### GET /api/listings/{id}/track
`[Authorize]`. REST fallback for `TrackingHub`'s live position updates. Donor, assigned volunteer, or matched recipient only.

Success (200):
```json
{ "success": true, "message": "Success", "traceId": "...", "data": { "listingId": "...", "latitude": 23.03, "longitude": 72.56, "reportedAtUtc": "..." } }
```
`data` is `null` if the volunteer hasn't reported a position yet (still 200, `message: "No location has been reported for this listing yet."`). 403 — caller isn't the donor/volunteer/recipient of this listing. 404 — no such listing.

### GET /api/geocode
No authentication required (useful before registration completes). Resolves a free-form address to approximate coordinates.

Query params: `address` (required).

Success (200):
```json
{ "success": true, "message": "Success", "traceId": "...", "data": { "latitude": 23.0089, "longitude": 72.5601, "isApproximate": false } }
```
`isApproximate: true` means the address wasn't recognized and the response falls back to the Ahmedabad city center. 422 — `address` missing/blank.

## Certificates, Leaderboard, Reports

### GET /api/certificates
`[Authorize(Policy = "DonorOnly")]`. Lists the caller's own donation certificates, newest first.

Query params: `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<CertificateResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 2, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "certificateNumber": "FB-202607-00002", "listingId": "...", "mealsCount": 12, "issuedAtUtc": "...", "pdfUrl": null } ]
}
```
`pdfUrl` is `null` until `GET .../pdf` is called for that certificate at least once.

### GET /api/certificates/{id}
`[Authorize(Policy = "DonorOnly")]`, self only. Success (200): a single `CertificateResponse` (same shape as above). 403 — belongs to a different donor; 404 — no such certificate.

### GET /api/certificates/{id}/pdf
`[Authorize(Policy = "DonorOnly")]`, self only. Returns the certificate as a downloadable PDF — **not** wrapped in the `ApiResponse` envelope (a binary file can't be JSON); 403/404 for ownership/missing still go through the normal exception-handling pipeline. Regenerates the PDF fresh on every call; lazily records `Certificates.PdfUrl` the first time only (subsequent calls don't need it to be set).

Success (200): `Content-Type: application/pdf`, `Content-Disposition: attachment; filename=FoodBridge-Certificate-{id}.pdf`, PDF bytes as the body.

### GET /api/leaderboard
`[Authorize]`, any role. Volunteers ranked by total `VolunteerPoints`, descending.

Query params: `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<LeaderboardEntryResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 2, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [
    { "volunteerId": "...", "name": "Raj Patel", "totalPoints": 62, "totalDeliveries": 2, "rank": 1 },
    { "volunteerId": "...", "name": "Priya Shah", "totalPoints": 5, "totalDeliveries": 1, "rank": 2 }
  ]
}
```

### GET /api/leaderboard/me
`[Authorize(Policy = "VolunteerOnly")]`. The caller's own entry from the same ranking.

Success (200): a single `LeaderboardEntryResponse`, or `data: null` with `message: "You haven't completed a delivery yet."` if the caller has no `VolunteerPoints` rows yet.

### GET /api/reports/donor
`[Authorize(Policy = "DonorOnly")]`. Chart-ready impact summary sourced from the caller's own `Certificates` (completed, confirmed donations) plus a raw `Listings` count for overall activity.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": { "totalListings": 12, "totalMealsDonated": 67, "totalCertificates": 3, "mealsDonatedByMonth": [ { "period": "2026-07", "value": 67 } ] }
}
```

### GET /api/reports/volunteer
`[Authorize(Policy = "VolunteerOnly")]`. Sourced from the caller's own `VolunteerPoints` rows (one per completed delivery).

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": { "totalDeliveries": 2, "totalPoints": 62, "deliveriesByMonth": [ { "period": "2026-07", "value": 2 } ] }
}
```

### GET /api/reports/recipient
`[Authorize(Policy = "RecipientOnly")]`. Sourced from the caller's own `Listings` where `Status = Confirmed` (no `RecipientId` column on `Certificates` to join through instead).

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": { "totalMealsReceived": 117, "totalDeliveriesReceived": 3, "mealsReceivedByMonth": [ { "period": "2026-07", "value": 117 } ] }
}
```

All three report `*ByMonth` series use the same `{ period: "yyyy-MM", value: number }` shape — directly bindable to a chart with no client-side reshaping.

## Dashboard
One consolidated, chart-ready endpoint per role under `/api/dashboard` — everything the role's dashboard screen needs in a single call instead of composing it client-side from `reports`/`leaderboard`/`listings`. Each action carries its own role policy (`[Authorize(Policy = "...Only")]`) rather than a shared class-level one.

### GET /api/dashboard/donor
`DonorOnly`. Query params: `latitude`/`longitude` (both optional) — when omitted, the "nearby recipients" widget falls back to the donor's own registered profile location (`Users.Latitude`/`Longitude`); if neither is available, that widget is just an empty list, not an error.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "totalMealsDonated": 67, "mealsDonatedToday": 12, "totalDonations": 12, "totalCertificates": 3,
    "mealsDonatedByMonth": [ { "period": "2026-07", "value": 67 } ],
    "recentActivity": [ { "id": "...", "title": "Surplus Wedding Catering", "foodType": "Mixed Veg Meals", "dietType": "Veg", "mealType": "Dinner", "quantityMeals": 80, "freshnessTag": "JustCooked", "pickupDeadlineUtc": "...", "status": "Pending", "createdAtUtc": "..." } ],
    "nearbyRecipients": [ { "id": "...", "name": "Hope NGO", "address": "Paldi", "city": "Ahmedabad", "latitude": 23.0089, "longitude": 72.5601, "capacityMeals": 200, "distanceKm": 2.8 } ]
  }
}
```
`totalMealsDonated`/`totalDonations`/`totalCertificates`/`mealsDonatedByMonth` mirror `GET /api/reports/donor` exactly (same source query). `mealsDonatedToday` sums `quantityMeals` across this donor's `Confirmed` listings whose confirm time falls on today's UTC calendar date — a true daily figure, unlike the report's monthly buckets. `recentActivity` is the 5 most recent of the donor's own listings (same shape as `GET /api/listings`'s list items). `nearbyRecipients` is informational browsing (not a match) — every Verified, non-deleted recipient within 10km, **regardless of their current `isAvailable` flag**, up to 5, nearest first. 422 — `latitude`/`longitude` out of range if explicitly provided.

### GET /api/dashboard/volunteer
`VolunteerOnly`. Query params: `latitude`/`longitude` (both optional, same fallback-to-profile-location behavior as above, for the "open listings nearby" widget).

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "totalDeliveries": 5, "totalPoints": 67, "leaderboardRank": 2, "totalMealsHelped": 67,
    "deliveriesByMonth": [ { "period": "2026-07", "value": 5 } ],
    "badges": [ { "code": "FirstDelivery", "name": "First Delivery", "earned": true }, { "code": "TenDeliveries", "name": "10 Deliveries", "earned": false }, { "code": "HundredMeals", "name": "100 Meals", "earned": false } ],
    "openListingsNearby": [ { "id": "...", "title": "Extra Lunch Boxes", "foodType": "Packaged Meals", "dietType": null, "mealType": null, "quantityMeals": 25, "freshnessTag": "FewHoursOld", "pickupDeadlineUtc": "...", "pickupAddress": "S.G. Highway, Bodakdev", "latitude": 23.0282, "longitude": 72.5061, "distanceKm": 3.6 } ]
  }
}
```
`totalDeliveries`/`totalPoints`/`deliveriesByMonth` mirror `GET /api/reports/volunteer`; `leaderboardRank` mirrors `GET /api/leaderboard/me` (`null` if this volunteer has never delivered — no `VolunteerPoints` rows yet). `totalMealsHelped` sums `quantityMeals` across this volunteer's own `Confirmed` deliveries — computed independently of the `VolunteerPoints` formula (currently 1 point per meal, so the two numbers coincide today, but `totalMealsHelped` stays correct even if that formula changes later). **`badges` only includes the 3 badges with an objective, data-backed definition** (`totalDeliveries >= 1`, `>= 10`, `totalMealsHelped >= 100`) — computed on the fly, not a persisted achievements table. The prototype's "Speed Runner" and "Night Owl" badges have no measurable criteria anywhere in the domain model (no delivery-speed or time-of-day tracking exists), so they're intentionally not included rather than faked. `openListingsNearby` is the same query as `GET /api/listings/nearby` (10km, `Pending` only), capped to the 5 nearest. 422 — `latitude`/`longitude` out of range if explicitly provided.

### GET /api/dashboard/recipient
`RecipientOnly`. No query params.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "totalMealsReceived": 117, "totalDeliveriesReceived": 3, "mealsReceivedToday": 20, "upcomingDeliveries": 1,
    "storageCapacityMeals": 200, "storageUsedPercentToday": 10.0,
    "mealsReceivedByMonth": [ { "period": "2026-07", "value": 117 } ],
    "donorDistribution": [ { "donorId": "...", "donorName": "Green Leaf Restaurant", "totalMealsReceived": 80 }, { "donorId": "...", "donorName": "Sunrise Caterers", "totalMealsReceived": 37 } ],
    "incomingFood": [ { "id": "...", "title": "Corporate Cafeteria Surplus", "foodType": "Mixed Veg Meals", "dietType": "Veg", "mealType": "Lunch", "quantityMeals": 60, "freshnessTag": "JustCooked", "pickupDeadlineUtc": "...", "status": "PickedUp", "createdAtUtc": "..." } ]
  }
}
```
`totalMealsReceived`/`totalDeliveriesReceived`/`mealsReceivedByMonth` mirror `GET /api/reports/recipient`. `mealsReceivedToday` sums `quantityMeals` across this recipient's `Confirmed` listings confirmed today (UTC calendar date) — a true daily figure the report's monthly buckets can't give you. `upcomingDeliveries` is `GET /api/listings/incoming`'s `totalCount` (listings matched and awaiting this recipient's accept/reject decision). `storageCapacityMeals` is this recipient's own `Users.CapacityMeals`; `storageUsedPercentToday` is `mealsReceivedToday` as a percentage of it, rounded to 1 decimal (`null` if `capacityMeals` isn't set, e.g. a household recipient who never set one). `donorDistribution` breaks down all-time `Confirmed` meals received by donor, highest first, capped to 5. `incomingFood` is the 5 most recent incoming matches (same shape as `GET /api/listings/incoming`'s list items).

## Admin
8 of these 9 endpoints require `[Authorize(Policy = "AdminOnly")]` — any non-Admin role gets 403 on every one (verified live with a real Donor JWT swept across all 8 original ones). The exception is `POST /api/disputes` (raise a dispute), open to any authenticated role and gated by listing-ownership in the service instead — see its own entry below. `AdminController`'s browse/moderation actions are nested under `/api/admin`; `DisputesController` is a flat `/api/disputes` resource; the platform report lives on the existing `ReportsController` (`GET /api/reports/platform`) alongside the donor/volunteer/recipient reports from the previous section.

### GET /api/admin/dashboard
At-a-glance platform counts, plus the two status breakdowns needed for the dashboard's charts.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": {
    "totalDonors": 2, "totalVolunteers": 4, "totalRecipients": 3, "pendingRecipients": 1,
    "totalListings": 17, "pendingListings": 0, "activeListings": 8, "confirmedListings": 4,
    "totalMealsDonated": 67, "totalCertificatesIssued": 3, "totalVolunteerPointsAwarded": 67,
    "openDisputes": 0, "resolvedDisputes": 0,
    "listingsByStatus": [ { "status": "Pending", "count": 0 }, { "status": "Claimed", "count": 3 }, { "status": "PickedUp", "count": 2 }, { "status": "Delivered", "count": 3 }, { "status": "Confirmed", "count": 4 }, { "status": "Expired", "count": 1 }, { "status": "Cancelled", "count": 4 } ],
    "accountsByStatus": [ { "status": "Verified", "count": 8 }, { "status": "Pending", "count": 1 }, { "status": "Suspended", "count": 0 } ]
  }
}
```
`activeListings` counts `Claimed`+`PickedUp`+`Delivered` (in flight, not yet `Confirmed`) — a single collapsed number, whereas `listingsByStatus`/`accountsByStatus` give the full per-status breakdown a bar/doughnut chart needs (only statuses with at least one row appear). Added so the frontend doesn't have to make 9 separate `admin/listings`/`admin/accounts` calls (one per status) just to render two charts.

### GET /api/admin/listings
All listings platform-wide (any donor), with the donor's name attached — unlike the donor's own `GET /api/listings`, which is implicitly self-scoped.

Query params: `status` (optional, a `Listings.Status` enum name), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<AdminListingSummaryResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 17, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "title": "...", "status": "Confirmed", "donorId": "...", "donorName": "Green Leaf Restaurant", "volunteerId": "...", "recipientId": "...", "quantityMeals": 5, "pickupDeadlineUtc": "...", "createdAtUtc": "..." } ]
}
```

### GET /api/admin/accounts
All user accounts platform-wide.

Query params: `role` (optional, a `Users.Role` enum name), `accountStatus` (optional, a `Users.AccountStatus` enum name), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<AdminUserSummaryResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 3, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "mobile": "9123456799", "name": "Test Household", "role": "Recipient", "accountStatus": "Pending", "city": "Ahmedabad", "isAvailable": true, "createdAtUtc": "..." } ]
}
```

### PATCH /api/admin/accounts/{id}/verify
Sets `AccountStatus` to `Verified` — unconditional (works from any current status, so it's also the only way to reverse a suspension; no separate "unsuspend" endpoint exists). For a `Pending` recipient, this is what unlocks them for `RecipientMatcher` — **verified live**: a listing created next to a genuinely `Pending` recipient matched to a farther-away *Verified* recipient instead; after this endpoint verified them, the identical scenario matched to them.

No request body. Success (200): the updated `AdminUserSummaryResponse`. 404 — no such user.

### PATCH /api/admin/accounts/{id}/suspend
Sets `AccountStatus` to `Suspended`.

No request body. Success (200): the updated `AdminUserSummaryResponse`. 422 — target is an Admin account, or the caller's own account (`"Admin accounts cannot be suspended."` / `"You cannot suspend your own account."`); 404 — no such user.

### POST /api/disputes
Raises a dispute about a listing. `[Authorize]`, any role — but callable only by that specific listing's donor, assigned volunteer, or matched recipient (403 for anyone else, enforced in `DisputeService`, not a policy).

Request:
```json
{ "listingId": "...", "reason": "Volunteer never showed up for pickup." }
```
Success (200): the created `DisputeResponse` (`status: "Open"`, `raisedByUserId` set to the caller). Errors: 400 — `listingId`/`reason` missing or `reason` over 1000 chars; 403 — caller isn't a party to this listing; 404 — no such listing.

### GET /api/disputes
`[Authorize(Policy = "AdminOnly")]`. Lists disputes. Query params: `status` (optional, `Open` or `Resolved`), `page` (default 1), `pageSize` (default 20, max 100).

Success (200) — `PagedResponse<DisputeResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "listingId": "...", "raisedByUserId": "...", "reason": "...", "status": "Open", "resolvedByUserId": null, "resolutionNote": null, "createdAtUtc": "..." } ]
}
```

### PATCH /api/disputes/{id}/resolve
Resolves an open dispute.

Request:
```json
{ "resolutionNote": "Contacted volunteer and donor; issued a warning. Case closed." }
```
Success (200): the updated `DisputeResponse` (`status: "Resolved"`, `resolvedByUserId` set to the caller). Errors: 422 — `resolutionNote` missing/blank, or the dispute is already `Resolved` (`"This dispute has already been resolved."`); 404 — no such dispute.

### GET /api/reports/platform
Platform-wide chart-ready summary — same shape family as the donor/volunteer/recipient reports, scoped to everyone instead of one user.

Success (200):
```json
{
  "success": true, "message": "Success", "traceId": "...",
  "data": { "totalMealsDonated": 67, "totalDeliveries": 3, "totalCertificates": 3, "totalUsers": 10, "mealsDonatedByMonth": [ { "period": "2026-07", "value": 67 } ] }
}
```

## Drop-off Locations
All 5 endpoints route under `/api/dropoff-locations`. **Authorization is per-action, not class-level**: the 4 CRUD endpoints below are `AdminOnly`, while [`GET /hotspots`](#get-apidropoff-locationshotspots) is `CanDropOff` (Volunteer **or** Donor).

This is the **shared pool** of places food can be taken, and it grows two ways:
- **Admins** curate partner collection points (NGO offices, shelters, community fridges) via `POST` below → `source: "Admin"`.
- **Whoever delivers** adds spots they find in the field by naming a new one at `confirm-delivery` (volunteer) or `self-deliver` (donor) → `source: "Volunteer"`, active immediately with no moderation step. Admins see these badged in the list and can retire one with `deactivate`.

> `source: "Volunteer"` predates donor self-delivery and is kept for wire compatibility — read it as **"added during a delivery"**, not "added by a volunteer". `addedByName` on the hotspot response is who actually added it, and that is what the UI shows.

The nearest **available** location (skipping anything on cooldown) is automatically resolved and attached to a listing's `suggestedDropOffLocation` when no recipient is available — see `confirm-pickup` and `reject`, above.

### POST /api/dropoff-locations
Request:
```json
{ "name": "Hope NGO Collection Point", "address": "Paldi", "latitude": 23.0089, "longitude": 72.5601, "city": "Ahmedabad" }
```
Success (200): the created `DropOffLocationResponse` (`isActive: true`). 400 — validation (`name`/`address` missing/too long, lat/lng out of range).

### GET /api/dropoff-locations
Query params: `page` (default 1), `pageSize` (default 20, max 100). Success (200) — `PagedResponse<DropOffLocationResponse>`:
```json
{
  "page": 1, "pageSize": 20, "totalCount": 3, "totalPages": 1,
  "success": true, "message": "Success", "traceId": "...",
  "data": [ { "id": "...", "name": "Hope NGO Collection Point", "address": "Paldi", "latitude": 23.0089, "longitude": 72.5601, "city": "Ahmedabad", "isActive": true, "createdAtUtc": "..." } ]
}
```

### PATCH /api/dropoff-locations/{id}/deactivate
Excludes the location from future nearest-active lookups without deleting it (it stays visible in `GET` above). No request body. Success (200): the updated `DropOffLocationResponse` (`isActive: false`). 404 — no such location.

### PATCH /api/dropoff-locations/{id}/activate
Reverses `deactivate`. No request body. Success (200): the updated `DropOffLocationResponse` (`isActive: true`). 404 — no such location.

## Chart data contract
Every chart-shaped series in the API (currently: `GET /api/reports/donor|volunteer|recipient`'s `*ByMonth` fields) uses the same `ChartPoint` shape:
```json
{ "period": "2026-07", "value": 67 }
```
`period` is `"yyyy-MM"` — a plain string, already lexicographically sortable, one point per calendar month that has at least one contributing row (no zero-filled gaps for months with no activity). `value` is a plain integer whose meaning is defined per-series (meals, deliveries, points, etc. — see the field name it's nested under). Bind directly to any bar/line chart's x/y without reshaping.
