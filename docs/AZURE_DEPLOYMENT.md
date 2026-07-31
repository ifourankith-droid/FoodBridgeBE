# FoodBridge — Azure deployment

Target: **Azure App Service** (API) + **Azure SQL Database** (`foodbridge-sql` / `FoodBridgeDb`),
authenticating to SQL with **Microsoft Entra ID** (no password in any connection string).

---

## 1. What is already configured in the repo

`src/FoodBridge.Api/appsettings.Production.json`:

```json
"ConnectionStrings": {
  "Default": "Server=tcp:foodbridge-sql.database.windows.net,1433;Initial Catalog=FoodBridgeDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\""
}
```

Notes on this string:
- **It is safe to commit** — `Authentication="Active Directory Default"` carries no password. The
  identity comes from `DefaultAzureCredential` at runtime: the App Service managed identity in Azure,
  or your Visual Studio / Azure CLI sign-in locally.
- The value supplied was **pasted twice**; the duplicate half was removed. `SqlConnectionStringBuilder`
  does tolerate repeated keywords (last one wins — verified), but the duplication was a copy/paste
  artifact, not intent.
- No package changes were needed: `Microsoft.Data.SqlClient` 5.1.5 supports `Active Directory Default`,
  `Azure.Identity` 1.10.3 is already resolved transitively, and **FluentMigrator 5.2.0 also uses
  `Microsoft.Data.SqlClient`** — so migrations authenticate the same way. (Had it used the older
  `System.Data.SqlClient`, `Active Directory Default` would have been rejected there.)

Also configured:
- **CORS reads `Cors:AllowedOrigins`** (was hardcoded to `http://localhost:4200`). Dev falls back to
  `localhost:4200` + `localhost:4201`.
- **Startup refuses to run outside Development** if `Jwt:Secret` is missing or is still the value
  committed in `appsettings.json`.
- **`Bootstrap:AdminMobile`** creates the first Admin account, once, if set.

---

## 2. What you need to do in Azure

### 2.1 Enable Entra ID auth on the SQL server — **this is the current blocker**

A live test from this machine reached the server and `DefaultAzureCredential` *did* acquire a token,
but SQL rejected it:

```
Login failed for user '<token-identified principal>'.
The server is not currently configured to accept this token.
Error Number: 18456, State: 235
```

That specific message almost always means **the logical server has no Microsoft Entra admin set**.
Fix it first:

> Azure Portal → SQL server `foodbridge-sql` → **Settings → Microsoft Entra ID** →
> **Set admin** → pick your account → **Save**

```bash
# or via CLI
az sql server ad-admin create \
  --resource-group <your-rg> \
  --server foodbridge-sql \
  --display-name "<your-name>" \
  --object-id "<your-entra-object-id>"
```

Encouraging detail: the network path already works — the firewall is not blocking this machine, and
the credential chain already found a signed-in identity. Only server-side authorization is missing.

### 2.2 Turn on the App Service managed identity

> App Service `foodbridge-api` → **Settings → Identity** → **System assigned** → **On** → Save

### 2.3 Create database users

Connect to **`FoodBridgeDb`** (not `master`) as the Entra admin you just set — SSMS, Azure Data Studio,
or the portal query editor — and run:

```sql
-- The App Service, so the running API can read/write.
CREATE USER [foodbridge-api] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [foodbridge-api];
ALTER ROLE db_datawriter ADD MEMBER [foodbridge-api];

-- You, so you can run migrations from your machine.
-- db_owner because FluentMigrator issues DDL: CREATE TABLE, indexes, and the
-- spatial indexes on the geography columns.
CREATE USER [you@yourtenant.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_owner ADD MEMBER [you@yourtenant.com];
```

Use the **App Service name** as the user name for a system-assigned identity — that is what the token
presents. If you'd rather let the API run its own migrations, give `[foodbridge-api]` `db_ddladmin`
too and see §3.2.

### 2.4 Firewall

> SQL server → **Networking** → **Allow Azure services and resources to access this server** = Yes
> (App Service), and add your current client IP for local runs.

### 2.5 App Service settings

> App Service → **Settings → Environment variables → App settings**

| Name | Value | Why |
|---|---|---|
| `Jwt__Secret` | 32+ random bytes, base64 | **Required** — the app will not start without it. Generate: `node -e "console.log(require('crypto').randomBytes(48).toString('base64'))"` |
| `Cors__AllowedOrigins__0` | your frontend's origin | e.g. `https://foodbridge-web.azurewebsites.net`. See the warning below. |
| `Bootstrap__AdminMobile` | e.g. `9999900000` | Creates the first Admin. Remove after first boot if you like — it's a no-op once the account exists. |
| `Bootstrap__AdminName` | e.g. `Platform Admin` | Optional; defaults to "FoodBridge Admin". |

> ⚠️ **`Cors:AllowedOrigins` in `appsettings.Production.json` is still the placeholder**
> `https://REPLACE-ME-foodbridge-web.azurewebsites.net`. Set it — via the App Setting above, or by
> editing that file — to wherever the **Angular app** is served from.
>
> It must be the **frontend's** origin, not the API's. Pointing it at `foodbridge-api.azurewebsites.net`
> does nothing useful: CORS describes which *other* sites may call this API. (If you end up serving the
> built Angular app from this same App Service, calls are same-origin and CORS doesn't apply at all.)

Double underscore `__` is the nesting separator; `Cors__AllowedOrigins__0` is the first array element.

---

## 3. Create the schema

`Database:MigrateOnStartup` is **`false`** in Production, so a fresh `FoodBridgeDb` has **no tables**
until you do one of the following.

### 3.1 Run the migrations console once (recommended)

From your machine, signed in to the tenant (`az login`, or Visual Studio's Azure account):

```bash
cd "F:/Ankit/New Project/FoodBridge"

dotnet run --project src/FoodBridge.Migrations -- \
  "Server=tcp:foodbridge-sql.database.windows.net,1433;Initial Catalog=FoodBridgeDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\""
```

Prints `Migrations applied successfully.` The console runner applies **no profile** unless
`ASPNETCORE_ENVIRONMENT` is set, so this will **not** insert the Development seed data — production
starts clean, which is what you want.

Rollback the most recent migration: append `"" rollback` after the connection string.

### 3.2 Or let the API migrate itself on boot

Set App Setting `Database__MigrateOnStartup` = `true`, and grant `[foodbridge-api]` `db_ddladmin`.

Trade-off: convenient, but every instance runs `MigrateUp()` on start, so a scaled-out App Service has
instances racing on the same schema, and a failing migration takes the whole app down instead of
failing one command you can read. Fine for a single instance; prefer §3.1 otherwise.

---

## 4. First sign-in

There is no seeded user in Production (the seed migration is `[Profile("Development")]`) and **Admin
cannot self-register** — hence `Bootstrap:AdminMobile`. Once set and the app has booted:

1. `POST /api/auth/send-otp` with that mobile.
2. Read the OTP from the **Log Stream** — look for `[MockSms] OTP for … is …`.
3. `POST /api/auth/verify-otp` → you're in as Admin, and can verify volunteers.

> 🔴 **Security debt — remove this before real users exist.**
> `Otp:FixedDevelopmentCode` is correctly *not* bound in Production, so codes are random. But
> `MockSmsProvider` only ever *logs* the code, and Production filters logs at `Warning`, so the OTP
> was reaching **nobody** — with Twilio disabled that's a complete lockout, including for the admin
> account you just bootstrapped.
>
> To make first sign-in possible, `appsettings.Production.json` now pins
> `"FoodBridge.Infrastructure.Auth.MockSmsProvider": "Information"`. **This writes login codes to your
> application logs** — anyone who can read the Log Stream can sign in as anyone. It's acceptable while
> you're the only user; it is not acceptable once anyone else has an account.
>
> The fix is to deliver OTPs for real: set `Twilio:Enabled=true` with credentials
> (see `TWILIO_WHATSAPP_SETUP.md`), which swaps in `TwilioWhatsAppSmsProvider` and stops
> `MockSmsProvider` from being registered at all — then **delete that log override**.

Donors and volunteers self-register normally. Remember volunteers land in `Pending` and need you to
approve their ID + selfie before they can claim anything.

---

## 5. Known limitation worth planning for: uploaded files are ephemeral

`IFileStorage` is `LocalFileStorage`, writing to `wwwroot/uploads` on the App Service filesystem.
That directory **does not survive** a restart, scale-out, or redeploy, and is not shared between
instances.

This now matters more than it did: that folder holds **volunteer ID documents and selfies**, delivery
proof photos, listing images, and generated certificate PDFs. On Azure you should expect verification
documents to disappear — which would leave admins unable to review, and destroy the audit trail behind
an approved volunteer.

The fix is a drop-in: `IFileStorage` exists precisely so a `BlobFileStorage` can replace
`LocalFileStorage` with **one new class and one line in `Program.cs`**, no consumer changes. Not built
here because it wasn't in scope — say the word.

Related: Serilog's file sink writes to `logs/` on the same ephemeral disk. Use the App Service log
stream, or add Application Insights, rather than relying on those files.

---

## 6. Debugging a startup failure (HTTP 500.30)

**500.30 = "ASP.NET Core app failed to start"** — the process crashed before it could serve anything,
so there's no app-level error page to read. In this project the two overwhelmingly likely causes are:

### 6.1 First: is `Jwt__Secret` set?

Startup **deliberately throws** outside Development if `Jwt:Secret` is missing or is still the value
committed in `appsettings.json` (§2.5). If you deployed without setting it, that alone produces a
500.30. Reproduced locally against the real publish output:

```
[FoodBridge] FATAL - the application failed to start:
System.InvalidOperationException: Jwt:Secret is missing or is still the checked-in development
value, and the current environment is 'Production'. Supply a real secret ...
```

Fix: add the `Jwt__Secret` App Setting, then **restart** the App Service.

### 6.2 Second: is the .NET 6 runtime actually there?

The app targets **`net6.0`**, which is out of support and no longer installed on current App Service
runtime stacks. A framework-dependent net6.0 app on a .NET 8 stack fails at startup with:

```
You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '6.0.0' not found
```

This is the same reason `npm run backend` sets `DOTNET_ROLL_FORWARD=Major` locally. Three fixes, in
order of preference:

1. **Already handled** — `src/FoodBridge.Api/web.config` now sets `DOTNET_ROLL_FORWARD=Major`, so the
   app runs on the newest installed major. Verify it survived publish: Kudu → `site/wwwroot/web.config`.
2. Or add App Setting `DOTNET_ROLL_FORWARD` = `Major` (works on Linux too, where web.config is ignored).
3. Or publish self-contained so the runtime ships with the app.

### 6.3 Where to actually read the error

Startup crashes bypass `ILogger`, so they don't appear in `LogFiles/Application`. In rough order of
usefulness:

| Where | How |
|---|---|
| **ANCM stdout log** — best for 500.30 on Windows | Kudu → Debug console → `LogFiles` → `stdout_*.log`. Enabled by the `web.config` added here (`stdoutLogEnabled="true"`). |
| **Log Stream** | Portal → App Service → **Monitoring → Log stream**. Now useful because Production got a **Console** Serilog sink (it previously wrote only to a rolling *file*, so nothing reached the stream). |
| **Application Event Log** | Portal → **Diagnose and solve problems → Application Event Logs**, or Kudu → `LogFiles/eventlog.xml`. This is where "framework not found" appears. |
| **Kudu directly** | `https://foodbridge-api.scm.azurewebsites.net` → Debug console. |
| Serilog rolling file | Kudu → `site/wwwroot/logs/foodbridge-*.log`. Ephemeral — don't rely on it. |

Enable filesystem logging first if Log Stream is empty:
> App Service → **Monitoring → App Service logs** → *Application logging (Filesystem)* = On, Level = Information

### 6.4 What was changed to make this diagnosable

Before this pass, a Production startup crash was genuinely invisible: Serilog's only sink was a file
on ephemeral storage, and the bootstrap logger itself could throw *outside* the `try/catch`.

- **`Console` sink added to `appsettings.Production.json`** — so logs reach Log Stream at all.
- **`Microsoft.Hosting.Lifetime` pinned to `Information`** in **both** `Logging:LogLevel` and Serilog's
  `MinimumLevel.Override`. Both are required: `Logging:LogLevel` filters at the `ILoggerFactory`
  *before* Serilog sees the event, so setting only the Serilog override silently does nothing. With
  this, `Now listening on…` / `Application started` always appear — a successful boot is now
  distinguishable from a silent failure, which is the single most useful signal when chasing a 500.30.
- **Fatal errors also written to `Console.Error`**, unconditionally, independent of sink configuration.
- **Bootstrap logger wrapped in its own `try/catch`** and `appsettings.json` made `optional: true`, so a
  config-loading problem can't be what hides the real error.
- **`web.config`** added to enable ANCM stdout logging and set `DOTNET_ROLL_FORWARD=Major`. Verified the
  publish transform rewrites it correctly (`processPath="dotnet" arguments=".\FoodBridge.Api.dll"`).

### 6.5 Once it starts, expect database errors next

Getting past 500.30 only means the *process* is healthy. `/api/health` will return 200 without touching
SQL. The database still needs §2.1–§2.4 and §3 — until then you'll see, every 30 seconds from
`ListingExpiryBackgroundService`:

```
Listing expiry sweep failed.
Microsoft.Data.SqlClient.SqlException: Login failed for user '<token-identified principal>'...
```

That's the expected next step, not a regression — see the table in §7.

## 7. Quick verification after deploying

```bash
# 1. Liveness — no database involved.
curl https://foodbridge-api.azurewebsites.net/api/health

# 2. Database reachability — this one opens a SQL connection.
curl -X POST https://foodbridge-api.azurewebsites.net/api/auth/send-otp \
     -H 'Content-Type: application/json' \
     -d '{"mobile":"9999900000"}'
```

Reading the failures (see also §6 for startup crashes):

| Symptom | Cause |
|---|---|
| App won't start; log shows `Jwt:Secret is missing or is still the checked-in development value` | `Jwt__Secret` App Setting not set (§2.5) |
| `Login failed for user '<token-identified principal>' … not currently configured to accept this token` (18456/235) | No Entra admin on the SQL server (§2.1) |
| `Login failed for user '<token-identified principal>'` with a principal name | Entra admin is set, but no `CREATE USER … FROM EXTERNAL PROVIDER` for that identity (§2.3) |
| `Invalid object name 'Users'` / `'Listings'` | Migrations never ran (§3) |
| Frontend calls fail with a CORS error | `Cors__AllowedOrigins__0` isn't the frontend's origin (§2.5) |
| Repeating errors every 30s in the log | `ListingExpiryBackgroundService` sweeping against an unreachable or empty database — fix the two above and it goes quiet |
