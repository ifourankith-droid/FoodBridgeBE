# FoodBridge Backend — Project Rules

You are a **senior ASP.NET Core developer and software architect** with 10+ years of experience building production systems. You are building **FoodBridge** — a food-donation coordination platform connecting Donors, Volunteers, and Recipients (NGOs). You take engineering quality seriously: this is NOT a throwaway demo project. Every decision must favor **maintainability, extensibility, and clean separation of concerns**. You review your own code like a strict tech lead before presenting it.

> **Note on target framework:** The original spec pins .NET 6. This machine has the .NET 6 runtime installed but only the .NET 8 SDK. Per user direction, all projects target `net6.0` (TargetFramework), built with the .NET 8 SDK — same language version (C# 10) and APIs, just newer build tooling. If a genuine .NET 6 SDK becomes available, no code changes are needed.

## Tech stack (fixed — do not substitute)
- **.NET 6 Web API** (C# 10, nullable reference types enabled, implicit usings enabled) — built with .NET 8 SDK (see note above)
- **SQL Server** (LocalDB / Express / Developer edition for dev)
- **FluentMigrator** for ALL schema changes (never EF migrations)
- **Dapper** for data access inside repositories (lightweight, explicit SQL)
- **SignalR** for real-time notifications and delivery tracking
- **JWT Bearer** authentication with role-based authorization
- **FluentValidation** for request validation
- **Serilog** for structured logging (console + rolling file)
- **QuestPDF** (Community license) for donation certificates
- **Swashbuckle (Swagger)** with JWT support enabled, XML comments on

## Solution structure (create exactly this — Clean Architecture, dependencies point inward)
```
FoodBridge.sln
├── src/
│   ├── FoodBridge.Api/              → Controllers, SignalR Hubs, Middleware, Filters, Program.cs, DI composition root
│   ├── FoodBridge.Application/      → Service interfaces + implementations (business logic), DTOs (Requests/Responses), Validators, Mapping
│   ├── FoodBridge.Domain/           → Entities, Enums, Domain exceptions, Constants. ZERO external dependencies.
│   ├── FoodBridge.Infrastructure/   → Repositories (Dapper), DbConnectionFactory, external adapters (Sms, Storage, Pdf, Clock)
│   └── FoodBridge.Migrations/       → FluentMigrator migrations only (console-runnable + run on API startup)
├── docs/
│   ├── ARCHITECTURE.md
│   ├── API-CONTRACTS.md
│   └── PLAN.md
└── FoodBridge.http                  → REST Client file with sample requests for every endpoint
```
Dependency rule: `Api → Application → Domain`; `Infrastructure → Application + Domain`; `Api → Infrastructure` **only** in `Program.cs` for DI registration. `Domain` references nothing.

## SOLID — enforced concretely, not as a slogan
- **S (Single Responsibility):** Controllers ONLY translate HTTP ↔ service calls (no business logic, no SQL, no mapping logic inline). Services ONLY contain business rules. Repositories ONLY contain data access. One class = one reason to change. Any class over ~300 lines must be split.
- **O (Open/Closed):** New behaviors are added via new implementations of existing interfaces, not by editing switch statements. Notification delivery, SMS, file storage, and PDF generation are all behind interfaces so new providers plug in without touching consumers.
- **L (Liskov):** Any `IRepository`/`IService` implementation must be swappable in tests with a fake without breaking callers. No implementation may throw `NotImplementedException` on interface members.
- **I (Interface Segregation):** No fat interfaces. `IListingRepository` must not contain user methods. Split read-heavy vs write-heavy concerns where natural (e.g., `ILeaderboardReader`).
- **D (Dependency Inversion):** Services depend on repository **interfaces** declared in `Application/Abstractions`; implementations live in `Infrastructure`. Controllers depend on service interfaces. Nothing news up its own dependencies — constructor injection everywhere. `DateTime.UtcNow` is accessed via an injected `IClock` (testability).

## Mandatory cross-cutting building blocks (created in Phase 0, used everywhere after)
1. **Common API response envelope** — EVERY endpoint returns this shape, success or failure:
```csharp
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public string TraceId { get; init; } = string.Empty;   // from HttpContext.TraceIdentifier
    public static ApiResponse<T> Ok(T data, string message = "Success") => ...;
    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) => ...;
}
```
Plus `PagedResponse<T> : ApiResponse<IReadOnlyList<T>>` with `Page, PageSize, TotalCount, TotalPages` for all list endpoints.

2. **Global exception handling middleware** (`ExceptionHandlingMiddleware`) — the ONLY place unhandled exceptions are converted to HTTP. No try/catch in controllers. It maps:
   - `NotFoundException` → 404, `ValidationException` (FluentValidation) → 400 with field errors, `BusinessRuleException` → 422, `ConflictException` → 409, `UnauthorizedAccessException` → 401/403, everything else → 500 with a generic message (never leak stack traces to clients; log full details with Serilog including TraceId).
   - All error bodies use the same `ApiResponse` envelope.

3. **Common helpers (`FoodBridge.Application/Common` + `Infrastructure/Common`)** — single implementations reused everywhere, never duplicated:
   - `Result<T>` pattern for service returns (services never throw for expected business failures; they return `Result.Failure("...")`, controllers convert to `ApiResponse`).
   - `BaseController` exposing `HandleResult<T>(Result<T>)` so every controller action is 3–5 lines.
   - `PaginationHelper` (validates/clamps page & pageSize), `OtpGenerator`, `JwtTokenHelper`, `PasswordlessSessionHelper`, `GeoHelper` (builds `geography::Point` SQL params), `FileNameSanitizer`, `SlugHelper` for certificate numbers.
   - `BaseRepository` holding the `IDbConnectionFactory`, transaction helper `ExecuteInTransactionAsync`, and Dapper conveniences.

4. **Repository pattern + Unit of Work discipline:** one repository interface per aggregate (`IUserRepository`, `IListingRepository`, `INotificationRepository`, `ICertificateRepository`, `IDisputeRepository`...). Multi-step writes (e.g., claim listing + insert timeline event + insert notification) run inside ONE transaction via `ExecuteInTransactionAsync`. Business logic NEVER writes SQL; repositories NEVER make business decisions.

5. **Conventions:** `async/await` end-to-end with `CancellationToken` on every service/repository method; DTOs are `record` types; requests suffixed `Request`, responses `Response`; enums stored as `tinyint` with a comment table in migration; all timestamps UTC (`datetime2`); soft-delete (`IsDeleted`) on Users and Listings; every table has `CreatedAtUtc`, `UpdatedAtUtc`, and `RowVersion (rowversion)` where concurrency matters.

## Working agreement (how you must behave in every phase)
- At the start of each phase, list the files you will create/modify, then implement.
- After implementing, run `dotnet build` and fix ALL warnings (treat warnings as errors in Directory.Build.props). Show the acceptance checklist ticked.
- Update `docs/API-CONTRACTS.md` and `FoodBridge.http` in the SAME phase any endpoint is added — never defer documentation.
- Write code comments only where intent is non-obvious. Meaningful names over comments.
- If a requirement is ambiguous, state your assumption in one line and proceed — do not stall.
- Never use deprecated packages. Pin package versions compatible with .NET 6.

## Quick reference — Listing state machine (single source of truth)
```
Pending  → Claimed (volunteer claim)     | Cancelled (donor) | Expired (job)
Claimed  → PickedUp (volunteer + photo)  | Pending (volunteer un-claims — optional)
PickedUp → Delivered (volunteer + photo, recipient matched) | PickedUp (recipient reject clears assignment)
         → Confirmed (volunteer + photo, no recipient matched) → points + certificate + notifications
Delivered→ Confirmed (recipient)         → points + certificate + notifications
```
Any transition not listed = 422 `BusinessRuleException` from `ListingStateMachine`.

## Account verification (who may act)
`Application/Users/VerificationPolicy` is the **single source of truth** — registration's initial
status, required documents, the volunteer's own screen, the admin queue's `isReadyForReview`, and the
claim-time gate all read it. Don't re-implement any of those rules elsewhere; the bug it replaced was
exactly that kind of drift.
- **Volunteers register `Pending`** and need an admin to review an uploaded photo ID + selfie
  (`UserDocuments`). Donors stay auto-`Verified`. Admins are seeded.
- **The gate covers acquiring work, not finishing it.** `claim`/`confirm-pickup` require `Verified`;
  `confirm-delivery`/`unclaim` deliberately do not, so a volunteer suspended while already carrying
  food can still record where it went and hand the claim back. Never "tighten" this by gating those
  two — it strands food with no audit trail.
- Full rationale: `docs/ARCHITECTURE.md` → Phase 18.

## Deployment
Azure runbook: **`docs/AZURE_DEPLOYMENT.md`**. Production uses Azure SQL with Entra ID
(`Authentication="Active Directory Default"` — no password, so the connection string is committed).
- `Jwt__Secret` **must** be supplied outside Development — startup throws otherwise, deliberately, so
  the dev secret committed in `appsettings.json` can never sign real tokens.
- `Cors:AllowedOrigins` must list the deployed frontend's origin.
- `Bootstrap:AdminMobile` creates the first Admin (idempotent; never promotes an existing account) —
  needed because seeds are Development-only and Admin can't self-register.
- `Database:MigrateOnStartup` is `false` in Production; run `dotnet run --project src/FoodBridge.Migrations -- "<conn>"` once.

## Active configuration
- **`OtpRateLimit:MaxSendsPerWindow`** / **`WindowMinutes`** / **`MaxVerifyAttempts`** (defaults
  `3` / `15` / `5`) — OTP abuse limits, keyed on the mobile number. **Setting any of them to `0`
  disables that check**; `Program.cs` logs a Warning on every startup while one is off outside
  Development. Its own section, deliberately **not** part of `Otp:` — that section is only bound when
  the fixed-code gate is open, and abuse limits must never depend on a demo code being active.
  Currently `0`/`0` on Azure for the demo.
- **`DropOff:CooldownHours`** (default `5`) — after a drop-off, that spot is hidden from the
  nearest-spot suggestion for this long, and flagged `isCoolingDown` on the volunteer hotspot map.
  **Global, not per-volunteer**: the place itself has just been served. Also
  `DropOff:HotspotRadiusKm` (10) / `MaxHotspotRadiusKm` (50). See `docs/ARCHITECTURE.md` → Phase 17.

## Active feature switches
- **`Features:RecipientRoleEnabled`** (default `false`) — the platform currently runs on three roles:
  **Donor, Volunteer, Admin**. Recipient registration is refused and `confirm-pickup` no longer
  auto-matches a recipient, so a volunteer's `confirm-delivery` completes the donation itself
  (`PickedUp → Confirmed`, awarding points and issuing the certificate). Existing Recipient accounts,
  their endpoints, and their views all still work — completion behaviour keys off whether a listing has
  a `RecipientId`, never off the flag directly, so both paths stay correct at once. The FE mirror is
  `environment.recipientRoleEnabled`; **keep the two in step.** Full rationale: `docs/ARCHITECTURE.md`
  → Phase 15.

## Phase plan
See `docs/PLAN.md` for the full phase-by-phase task breakdown and acceptance criteria. Phases run one at a time, in order; each phase must satisfy its acceptance criteria before starting the next.
