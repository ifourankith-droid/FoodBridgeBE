using System.Reflection;
using System.Text;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentValidation;
using FoodBridge.Api.BackgroundServices;
using FoodBridge.Api.Common;
using FoodBridge.Api.Hubs;
using FoodBridge.Api.Middleware;
using FoodBridge.Api.Notifications;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Admin;
using FoodBridge.Application.Auth;
using FoodBridge.Application.Certificates;
using FoodBridge.Application.Common;
using FoodBridge.Application.Dashboard;
using FoodBridge.Application.Disputes;
using FoodBridge.Application.DonorAddresses;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.Geocoding;
using FoodBridge.Application.Leaderboard;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Notifications;
using FoodBridge.Application.Reports;
using FoodBridge.Application.Tracking;
using FoodBridge.Application.Users;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Auth;
using FoodBridge.Infrastructure.Common;
using FoodBridge.Infrastructure.Geocoding;
using FoodBridge.Infrastructure.Pdf;
using FoodBridge.Infrastructure.Repositories;
using FoodBridge.Infrastructure.Storage;
using FoodBridge.Infrastructure.Tracking;
using FoodBridge.Migrations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Serilog;

// `optional: true`, and a Console fallback if reading config throws at all. This runs *before* the
// try/catch below, so anything that fails here produces a completely silent crash — on Azure App
// Service that surfaces as a bare HTTP 500.30 with nothing to read. A missing/unreadable
// appsettings.json (e.g. an unexpected working directory) must not be what hides the real error.
try
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build())
        .CreateBootstrapLogger();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FoodBridge] Failed to initialise logging from appsettings.json: {ex}");
    Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
}

try
{
    // Must exist before WebApplication.CreateBuilder(args) runs: it snapshots
    // IWebHostEnvironment.WebRootFileProvider at that point, and a missing
    // wwwroot locks it in as a NullFileProvider that UseStaticFiles() can't
    // later serve from, even if the directory is created afterward.
    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    Directory.CreateDirectory(uploadsPath);

    QuestPDF.Settings.License = LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);

    // 10MB ceiling: comfortably above the largest real upload (5MB listing/pickup/delivery
    // photos) plus JSON overhead, while still rejecting grossly oversized bodies before they're
    // fully buffered. Per-endpoint [RequestSizeLimit] on the 4 upload actions is tighter still.
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddSignalR();

    builder.Services.AddValidatorsFromAssembly(typeof(AuthService).Assembly);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodBridge API", Version = "v1" });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer {your JWT token}'",
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                },
                Array.Empty<string>()
            },
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // Origins come from Cors:AllowedOrigins so a deployed frontend can be allowed without a code
    // change; the Angular dev server stays the default when nothing is configured. AllowCredentials
    // rules out a wildcard origin, so this has to be an explicit list either way.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (allowedOrigins is null || allowedOrigins.Length == 0)
    {
        allowedOrigins = new[] { "http://localhost:4200", "http://localhost:4201" };
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowConfiguredOrigins", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });

    // Product feature switches. Bound unconditionally (unlike OtpSettings) because these
    // must take effect in every environment, not just Development.
    builder.Services.Configure<FeatureSettings>(builder.Configuration.GetSection(FeatureSettings.SectionName));
    builder.Services.Configure<DropOffSettings>(builder.Configuration.GetSection(DropOffSettings.SectionName));

    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IUserDocumentRepository, UserDocumentRepository>();
    builder.Services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();

    // Real OTP delivery is opt-in: stays MockSmsProvider (logs the code, sends nothing)
    // until Twilio:Enabled is explicitly set true with real credentials — see
    // docs/TWILIO_WHATSAPP_SETUP.md. Nothing here changes behavior for anyone who
    // hasn't configured Twilio.
    builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection(TwilioSettings.SectionName));
    var twilioSettings = builder.Configuration.GetSection(TwilioSettings.SectionName).Get<TwilioSettings>() ?? new TwilioSettings();
    if (twilioSettings.Enabled)
    {
        // Validated at startup rather than discovered on the first send. Every one of these
        // mistakes otherwise surfaces as an opaque Twilio 400 at the moment a real user is waiting
        // on a login code, which is the worst possible time to be reading API docs.
        var twilioProblems = new List<string>();

        if (string.IsNullOrWhiteSpace(twilioSettings.AccountSid))
        {
            twilioProblems.Add("Twilio:AccountSid is empty.");
        }

        if (string.IsNullOrWhiteSpace(twilioSettings.AuthToken))
        {
            twilioProblems.Add("Twilio:AuthToken is empty.");
        }

        // A Twilio WhatsApp sender must be "whatsapp:" + E.164, and must be a number Twilio owns —
        // your own mobile is not a valid sender. The sandbox sender is whatsapp:+14155238886.
        var from = twilioSettings.WhatsAppFromNumber ?? string.Empty;
        if (!from.StartsWith("whatsapp:+", StringComparison.Ordinal))
        {
            twilioProblems.Add(
                $"Twilio:WhatsAppFromNumber is '{from}', which is not a valid WhatsApp sender. It must be " +
                "'whatsapp:' followed by an E.164 number including the country code — e.g. the sandbox " +
                "sender 'whatsapp:+14155238886'. This is the number messages are sent FROM (owned by " +
                "Twilio), not the recipient's number.");
        }

        if (twilioProblems.Count > 0)
        {
            throw new InvalidOperationException(
                "Twilio:Enabled is true but the configuration is incomplete: "
                + string.Join(" ", twilioProblems)
                + " Supply values via environment variables (Twilio__AccountSid, Twilio__AuthToken) or "
                + "user-secrets — never in appsettings.json, which is committed. "
                + "See docs/TWILIO_WHATSAPP_SETUP.md.");
        }

        builder.Services.AddHttpClient<ISmsProvider, TwilioWhatsAppSmsProvider>();
    }
    else
    {
        builder.Services.AddScoped<ISmsProvider, MockSmsProvider>();
    }

    builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenHelper>();
    builder.Services.AddScoped<IPasswordlessSessionService, PasswordlessSessionHelper>();
    builder.Services.AddSingleton<ITokenDenylist, InMemoryTokenDenylist>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
    builder.Services.AddScoped<IUserService, UserService>();

    builder.Services.AddScoped<IListingRepository, ListingRepository>();
    builder.Services.AddScoped<IListingService, ListingService>();

    builder.Services.AddScoped<IDonorAddressRepository, DonorAddressRepository>();
    builder.Services.AddScoped<IDonorAddressService, DonorAddressService>();

    builder.Services.AddScoped<IRecipientReader, RecipientReader>();
    builder.Services.AddScoped<IRecipientMatcher, RecipientMatcher>();
    builder.Services.AddScoped<IVolunteerListingService, VolunteerListingService>();
    builder.Services.AddScoped<IRecipientListingService, RecipientListingService>();

    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<INotificationDispatcher, SignalRNotificationDispatcher>();

    builder.Services.AddSingleton<ITrackingStore, InMemoryTrackingStore>();
    builder.Services.AddScoped<ITrackingService, TrackingService>();

    builder.Services.AddScoped<IGeocodingProvider, MockGeocodingProvider>();
    builder.Services.AddScoped<IGeocodingService, GeocodingService>();

    builder.Services.AddHostedService<ListingExpiryBackgroundService>();

    builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
    builder.Services.AddScoped<ICertificateService, CertificateService>();
    builder.Services.AddScoped<IPdfGenerator, QuestPdfCertificateGenerator>();

    builder.Services.AddScoped<ILeaderboardReader, LeaderboardReader>();
    builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

    builder.Services.AddScoped<IReportsReader, ReportsReader>();
    builder.Services.AddScoped<IReportService, ReportService>();

    builder.Services.AddScoped<IDashboardReader, DashboardReader>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    builder.Services.AddScoped<IAdminRepository, AdminRepository>();
    builder.Services.AddScoped<IAdminService, AdminService>();

    builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();
    builder.Services.AddScoped<IDisputeService, DisputeService>();

    builder.Services.AddScoped<IDropOffLocationRepository, DropOffLocationRepository>();
    builder.Services.AddScoped<IDropOffLocationService, DropOffLocationService>();

    // Shared by the volunteer's confirm-delivery and the donor's self-deliver, so both validate a
    // drop-off choice identically.
    builder.Services.AddScoped<IDropOffResolver, DropOffResolver>();

    builder.Services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(uploadsPath, "/uploads"));

    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

    // appsettings.Production.json deliberately carries no Jwt section — but the *base*
    // appsettings.json does, and base config is always loaded first, so a real deployment would
    // otherwise silently sign tokens with a secret that is committed to source control. Anyone
    // holding the repo could mint a valid admin token. Fail loudly instead: a startup crash with a
    // clear message beats a quietly forgeable authentication scheme.
    const string CommittedDevSecret = "ADg2Oa0rofyUwOH5pRnSJO+ftSdO8OujVDwJi6SyOF1zgD1qlfVU9Ra6Vw3/RA7b";
    if (!builder.Environment.IsDevelopment()
        && (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret == CommittedDevSecret))
    {
        // A near-miss on the variable *name* is the likeliest reason to land here — a single
        // underscore ('Jwt_Secret') is not a nesting separator in .NET config, so it binds to
        // nothing and looks identical to "never set it". Name the trap and show which environment
        // variables actually arrived looking like an attempt, so the next person can self-diagnose
        // instead of guessing. Names only — never values.
        var lookalikes = Environment.GetEnvironmentVariables()
            .Keys
            .Cast<string>()
            .Where(key => key.Contains("Jwt", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("Jwt__Secret", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hint = lookalikes.Count > 0
            ? $" Found these JWT-ish environment variables, none of which is the expected name: {string.Join(", ", lookalikes)}. Note the separator is a DOUBLE underscore."
            : " No JWT-related environment variable was found at all.";

        throw new InvalidOperationException(
            $"Jwt:Secret is missing or is still the checked-in development value, and the current " +
            $"environment is '{builder.Environment.EnvironmentName}'. Supply a real secret (32+ random " +
            "bytes, base64) as the environment variable 'Jwt__Secret' (Jwt, two underscores, Secret) — " +
            "in Azure App Service, add it under Settings > Environment variables > App settings. " +
            "Never commit it." + hint);
    }

    // A fixed OTP is honoured automatically in Development, and outside it ONLY when
    // Otp:AllowFixedCodeOutsideDevelopment is explicitly true — for a live demo where reading codes
    // out of a log stream isn't practical. When the section isn't registered at all, AuthService
    // falls back to a random code, so an "Otp" key leaking into a non-dev config file still can't
    // weaken anything on its own.
    var otpSection = builder.Configuration.GetSection(OtpSettings.SectionName);
    var otpSettings = otpSection.Get<OtpSettings>() ?? new OtpSettings();
    var isDevelopment = builder.Environment.IsDevelopment();

    if (isDevelopment || otpSettings.AllowFixedCodeOutsideDevelopment)
    {
        builder.Services.Configure<OtpSettings>(otpSection);
    }

    // Logged every startup, at Warning so it survives Production's log levels: a fixed OTP outside
    // Development means anyone who knows a registered mobile can sign in as that account, so it must
    // never sit there forgotten after a demo.
    if (!isDevelopment
        && otpSettings.AllowFixedCodeOutsideDevelopment
        && !string.IsNullOrWhiteSpace(otpSettings.FixedDevelopmentCode))
    {
        Log.Warning(
            "SECURITY: a FIXED OTP is active in the '{Environment}' environment because " +
            "Otp:AllowFixedCodeOutsideDevelopment is true. Every login accepts the same code, so anyone " +
            "who knows a registered mobile number can sign in as that account. This is intended for a " +
            "demo only — remove the Otp__AllowFixedCodeOutsideDevelopment and Otp__FixedDevelopmentCode " +
            "settings to restore random codes.",
            builder.Environment.EnvironmentName);
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Without this, inbound "sub" is remapped to the legacy long-form
            // ClaimTypes.NameIdentifier URI, breaking FindFirstValue(sub) lookups.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero,
            };

            options.Events = new JwtBearerEvents
            {
                // SignalR's WebSocket/SSE transports can't set an Authorization header, so
                // the JS client sends the token as a query-string parameter instead; only
                // honor that fallback for the hub paths themselves, never for plain REST.
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var jti = context.Principal?.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
                    var denylist = context.HttpContext.RequestServices.GetRequiredService<ITokenDenylist>();
                    if (jti is not null && denylist.IsDenylisted(jti))
                    {
                        context.Fail("Token has been revoked.");
                    }

                    return Task.CompletedTask;
                },
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("DonorOnly", policy => policy.RequireRole(nameof(UserRole.Donor)));
        options.AddPolicy("VolunteerOnly", policy => policy.RequireRole(nameof(UserRole.Volunteer)));
        options.AddPolicy("RecipientOnly", policy => policy.RequireRole(nameof(UserRole.Recipient)));
        options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(UserRole.Admin)));

        // Anyone who can record a drop-off: volunteers on their deliveries, and donors delivering
        // an unclaimed listing themselves. Both need the same "where should this go?" map, so the
        // hotspot lookup can't stay VolunteerOnly.
        options.AddPolicy("CanDropOff", policy => policy.RequireRole(
            nameof(UserRole.Volunteer),
            nameof(UserRole.Donor)));
    });

    builder.Services
        .AddFluentMigratorCore()
        .ConfigureRunner(runnerBuilder => runnerBuilder
            .AddSqlServer()
            .WithGlobalConnectionString(builder.Configuration.GetConnectionString("Default"))
            .ScanIn(typeof(AssemblyMarker).Assembly).For.Migrations())
        .Configure<RunnerOptions>(options => options.Profile = builder.Environment.EnvironmentName)
        .AddLogging(loggingBuilder => loggingBuilder.AddFluentMigratorConsole());

    var app = builder.Build();

    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    // The default content-type map doesn't know AVIF or the JPEG aliases (.jfif, .jpe),
    // so an upload we accept would save and then serve as a 404. ImageFileTypes owns the
    // list, which keeps "what may be uploaded" and "what can be served" the same set.
    var staticFileContentTypes = new FileExtensionContentTypeProvider();
    foreach (var mapping in ImageFileTypes.ExtraContentTypes)
    {
        staticFileContentTypes.Mappings[mapping.Key] = mapping.Value;
    }
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileContentTypes });
    app.UseCors("AllowConfiguredOrigins");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<NotificationsHub>("/hubs/notifications");
    app.MapHub<TrackingHub>("/hubs/tracking");

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    // After migrations, so the Users table is guaranteed to exist on a first deployment.
    await AdminBootstrapper.EnsureAdminAsync(app.Services, app.Configuration, app.Logger);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");

    // Also straight to stderr, unconditionally. Serilog's Production configuration writes only to a
    // rolling *file*, on storage that App Service treats as ephemeral — so a startup crash would
    // otherwise leave nothing in the Log Stream and the whole failure reads as a bare HTTP 500.30.
    // stderr is captured by App Service (and by `docker logs` on Linux) regardless of sink config,
    // which makes this the difference between a diagnosable failure and a mystery.
    Console.Error.WriteLine("[FoodBridge] FATAL — the application failed to start:");
    Console.Error.WriteLine(ex.ToString());

    // Non-zero exit so the host reports a genuine failure rather than a clean shutdown.
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
