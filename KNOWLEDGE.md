# FoodBridge Backend — Knowledge / Work Log

A running log of notable backend changes, the problem each solved, and where it lives.

---

## My Deliveries API: include each delivery's lifecycle timeline

**Done by me on 2026-08-01**

### Problem
`GET /listings/deliveries` (the volunteer's My Deliveries) returned each listing
with its lifecycle **timeline omitted** — the client had to make a separate
`GET /listings/{id}/timeline` call per delivery to render the steps.

### Fix
Populate the timeline in the deliveries response, mirroring what the donor's
`GetByIdAsync` already does.

- **`src/FoodBridge.Application/Listings/VolunteerListingService.cs`**
  - `GetMyDeliveriesAsync` now loads each row's timeline via
    `_listingRepository.GetTimelineAsync(...)` and passes it into
    `ToResponseAsync`, instead of `Array.Empty<ListingTimelineEvent>()`.
  - `ListingResponse.Timeline` (mapped by `ListingMapper.ToResponseAsync`) is now
    filled for every delivery row.

No other layers needed changes: `ListingResponse` already carries `Timeline`, and
the frontend `ApiListing` model already declares `timeline`, so it receives the
data automatically.

### Notes
- Adds one `GetTimelineAsync` call per delivery row (an N+1, matching the existing
  per-row `GetImagesAsync` pattern) — fine for a volunteer's small delivery list.
- The standalone `GET /listings/{id}/timeline` endpoint is unchanged; it remains the
  one that resolves actor **names**, which the embedded timeline entries don't carry.

### Verify
- `dotnet build src/FoodBridge.Api/FoodBridge.Api.csproj` → succeeds.
- Call `GET /listings/deliveries` as a volunteer → each item now has a populated
  `timeline` array.

---

## Listing food-photo upload: accept WebP & AVIF (match the frontend)

**Done by me on 2026-08-01**

### Problem
When creating a donation, the listing would save but its photo could fail to
upload — the "save" and the "image upload" appeared to work separately. Root
cause was an allowed-extension mismatch between the frontend and backend:

- Frontend food-photo picker (`<app-image-picker>` in the New Donation form)
  accepts `image/jpeg, image/png, image/webp, image/avif`.
- Backend only allowed `.jpg / .jpeg / .png`.

The image endpoint is keyed on the listing id, so the client must create the
listing first and upload the photo second. When a donor picked a **WebP** or
**AVIF** photo, the create succeeded but the follow-up upload was rejected with
*"Image must be a JPG or PNG file"*, leaving a listing with no photo.

### Fix
Extended the backend to accept every extension the frontend picker allows, and
ensured all of them are also servable.

1. **`src/FoodBridge.Application/Listings/ListingService.cs`**
   - `AllowedImageExtensions` → `{ ".jpg", ".jpeg", ".png", ".webp", ".avif" }`
     (was `{ ".jpg", ".jpeg", ".png" }`), with a comment noting it mirrors the
     frontend picker's `accept` list.
   - Validation error message → *"Image must be a JPG, PNG, WebP or AVIF file."*

2. **`src/FoodBridge.Api/Controllers/ListingsController.cs`**
   - Updated the `POST /listings/{id}/images` XML doc comment to
     `JPG/PNG/WebP/AVIF, max 5MB`.

3. **`src/FoodBridge.Api/Program.cs`**
   - Added `using Microsoft.AspNetCore.StaticFiles;`.
   - `UseStaticFiles` now uses a `FileExtensionContentTypeProvider` that maps
     `.avif → image/avif`. AVIF isn't in the default content-type map, so an
     uploaded `.avif` would otherwise save but serve as a 404 (broken image).
     WebP is already in the default map.

### Scope / intentionally unchanged
- **Avatar** (`UserService.AllowedAvatarExtensions`) and **volunteer delivery
  photo** (`VolunteerListingService.AllowedPhotoExtensions`) stay `.jpg/.jpeg/.png`.
  Their frontend flows go through `PhotoDialog`, which defaults `accept` to
  `image/jpeg,image/png` (delivery sets it explicitly), so those already match —
  widening them would make the backend more permissive than the UI.
- **Verification documents** (`.jpg/.jpeg/.png/.pdf`) already match their picker.

### Verify
- `dotnet build src/FoodBridge.Api/FoodBridge.Api.csproj` → succeeds.
- Create a donation with a `.webp` (and `.avif`) photo → listing saves **and**
  the photo uploads and displays. Files over 5MB and other extensions are still
  rejected with the updated message.
