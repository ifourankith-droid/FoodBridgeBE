using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Demo fallback drop-off points around Ahmedabad, Gujarat. Only applied when the
/// runner is invoked with the "Development" profile (see M202607221010's own note).
/// </summary>
[Migration(202607271001)]
[Profile("Development")]
public sealed class M202607271001_SeedDropOffLocations : Migration
{
    public override void Up()
    {
        // [Profile] migrations run through the normal versioned sequence AND get re-invoked
        // unconditionally by ApplyProfiles() on every MigrateUp() call, so this guard is
        // required for idempotency, not just defensive.
        Execute.Sql(@"
IF EXISTS (SELECT 1 FROM DropOffLocations WHERE Id = '33333333-3333-3333-3333-333333333301')
    RETURN;

INSERT INTO DropOffLocations (Id, Name, Address, Latitude, Longitude, Location, City, IsActive, CreatedAtUtc, UpdatedAtUtc)
VALUES
('33333333-3333-3333-3333-333333333301', 'Hope NGO Collection Point', 'Paldi', 23.008900, 72.560100, geography::Point(23.008900, 72.560100, 4326), 'Ahmedabad', 1, GETUTCDATE(), GETUTCDATE()),
('33333333-3333-3333-3333-333333333302', 'Asha Foundation Shelter', 'Chandkheda', 23.107100, 72.583200, geography::Point(23.107100, 72.583200, 4326), 'Ahmedabad', 1, GETUTCDATE(), GETUTCDATE()),
('33333333-3333-3333-3333-333333333303', 'Satellite Community Fridge', 'Satellite', 23.020900, 72.529600, geography::Point(23.020900, 72.529600, 4326), 'Ahmedabad', 1, GETUTCDATE(), GETUTCDATE());
");
    }

    public override void Down()
    {
        Execute.Sql(@"
DELETE FROM DropOffLocations WHERE Id IN (
    '33333333-3333-3333-3333-333333333301', '33333333-3333-3333-3333-333333333302',
    '33333333-3333-3333-3333-333333333303');
");
    }
}
