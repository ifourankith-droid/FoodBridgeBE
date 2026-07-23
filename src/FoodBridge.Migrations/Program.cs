using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FoodBridge.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("FOODBRIDGE_CONNECTION")
      ?? "Server=(localdb)\\mssqllocaldb;Database=FoodBridgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

// No profile is applied unless ASPNETCORE_ENVIRONMENT is set, so running this console
// tool standalone never accidentally seeds a database.
var profile = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

// "up" (default) migrates to the latest version; "rollback" reverts only the most recently applied migration.
var command = args.Length > 2 ? args[2] : "up";

using var serviceProvider = new ServiceCollection()
    .AddFluentMigratorCore()
    .ConfigureRunner(runnerBuilder => runnerBuilder
        .AddSqlServer()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(AssemblyMarker).Assembly).For.Migrations())
    .Configure<RunnerOptions>(options => options.Profile = profile)
    .AddLogging(loggingBuilder => loggingBuilder.AddFluentMigratorConsole())
    .BuildServiceProvider(false);

using var scope = serviceProvider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

if (string.Equals(command, "rollback", StringComparison.OrdinalIgnoreCase))
{
    runner.Rollback(1);
    Console.WriteLine("Last migration rolled back successfully.");
}
else
{
    runner.MigrateUp();
    Console.WriteLine("Migrations applied successfully.");
}
