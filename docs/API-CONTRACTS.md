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

## Listings — Volunteer

## Listings — Recipient

## Notifications & real-time (SignalR contract)

## Certificates, Leaderboard, Reports

## Admin

## Chart data contract
