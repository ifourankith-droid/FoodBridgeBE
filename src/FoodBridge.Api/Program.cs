using System.Reflection;
using System.Text;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentValidation;
using FoodBridge.Api.Common;
using FoodBridge.Api.Middleware;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Auth;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Users;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Auth;
using FoodBridge.Infrastructure.Common;
using FoodBridge.Infrastructure.Repositories;
using FoodBridge.Infrastructure.Storage;
using FoodBridge.Migrations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateBootstrapLogger();

try
{
    // Must exist before WebApplication.CreateBuilder(args) runs: it snapshots
    // IWebHostEnvironment.WebRootFileProvider at that point, and a missing
    // wwwroot locks it in as a NullFileProvider that UseStaticFiles() can't
    // later serve from, even if the directory is created afterward.
    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    Directory.CreateDirectory(uploadsPath);

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();

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

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngularDev", policy => policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });

    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
    builder.Services.AddScoped<ISmsProvider, MockSmsProvider>();
    builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenHelper>();
    builder.Services.AddScoped<IPasswordlessSessionService, PasswordlessSessionHelper>();
    builder.Services.AddSingleton<ITokenDenylist, InMemoryTokenDenylist>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
    builder.Services.AddScoped<IUserService, UserService>();

    builder.Services.AddScoped<IListingRepository, ListingRepository>();
    builder.Services.AddScoped<IListingService, ListingService>();

    builder.Services.AddScoped<IRecipientReader, RecipientReader>();
    builder.Services.AddScoped<IRecipientMatcher, RecipientMatcher>();
    builder.Services.AddScoped<IVolunteerListingService, VolunteerListingService>();

    builder.Services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(uploadsPath, "/uploads"));

    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

    // Only bound in Development, even if the "Otp" key ever leaked into a non-dev
    // config file — AuthService falls back to a random OTP whenever this section
    // isn't registered, so a fixed OTP can never take effect outside local dev.
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection(OtpSettings.SectionName));
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
    app.UseStaticFiles();
    app.UseCors("AllowAngularDev");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
