# Real OTP delivery via Twilio WhatsApp Sandbox

## Current state

Nothing changes until you follow the steps below. `ISmsProvider`'s only active implementation is `MockSmsProvider` — it logs `[MockSms] OTP for {Mobile} is {Code}` to the console/Serilog file and sends nothing. `TwilioWhatsAppSmsProvider` (an alternate `ISmsProvider` implementation, in `FoodBridge.Infrastructure/Auth/TwilioWhatsAppSmsProvider.cs`) is already built and wired into `Program.cs`, but it's only registered when `Twilio:Enabled` is `true` — which it isn't, in any checked-in config file. This is the same Open/Closed pattern as every other provider in this codebase (`MockGeocodingProvider`, `LocalFileStorage`, ...): swap the implementation, zero consumer changes.

Why Twilio WhatsApp Sandbox specifically: it's free (new accounts get $15 trial credit, enough for roughly 1,700 messages) and delivers to a real phone, unlike `MockSmsProvider`.

## One-time setup, whenever you're ready

1. **Create a Twilio account** at [twilio.com](https://www.twilio.com) (free trial credit, no cost to you for sandbox testing within that credit — double-check current signup terms, they can change).
2. **Find your Account SID and Auth Token** on the Twilio Console dashboard (twilio.com/console) — both are shown right on the main page after login.
3. **Enable the WhatsApp Sandbox**: Console → Messaging → Try it out → Send a WhatsApp message. Note the sandbox's `From` number (usually `whatsapp:+14155238886`) and your unique join code.
4. **Opt in from your own WhatsApp**: send `join <your-code>` to that sandbox number, once. Twilio's sandbox drops this opt-in after a few days of inactivity — repeat this step if messages stop arriving.

## Configure this app

Never put `AccountSid`/`AuthToken` in a checked-in `appsettings*.json` file — use .NET user secrets (local dev) or environment variables (anywhere else).

**Option A — user secrets** (run from `src/FoodBridge.Api`):
```
dotnet user-secrets init
dotnet user-secrets set "Twilio:Enabled" "true"
dotnet user-secrets set "Twilio:AccountSid" "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
dotnet user-secrets set "Twilio:AuthToken" "your-auth-token"
```

**Option B — environment variables** (double underscore = nested key):
```
Twilio__Enabled=true
Twilio__AccountSid=ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Twilio__AuthToken=your-auth-token
```

Only override `Twilio:WhatsAppFromNumber` or `Twilio:DefaultCountryCode` (defaults: the standard sandbox number, `+91`) if yours differ.

## Verify

Restart the API, then `POST /api/auth/send-otp` with the same mobile number that completed the sandbox join step. A real WhatsApp message should arrive within seconds. If it doesn't:
- Confirm the join code hasn't expired (step 4 above) — this is the most common cause.
- Check the app log for the underlying Twilio HTTP error — a failed send surfaces as a generic 500 (`ExceptionHandlingMiddleware`'s catch-all), with the real Twilio response in the Serilog file.
- Confirm `Twilio:AccountSid`/`AuthToken` are actually visible to the app (`dotnet user-secrets list`, or check the environment the process actually runs under).

## Turning it back off

Set `Twilio:Enabled` back to `false` (or unset it — `false` is the default), and `ISmsProvider` reverts to `MockSmsProvider`. Fixed dev OTP (`Otp:FixedDevelopmentCode` in `appsettings.Development.json`) still works independently of this switch either way.
