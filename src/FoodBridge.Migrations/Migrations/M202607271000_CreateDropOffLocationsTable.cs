using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Admin-managed fallback pickup destinations (e.g. partner NGO/shelter collection
/// points) — used to give a volunteer somewhere to take food when no recipient is
/// available (initial confirm-pickup match failure, or every candidate rejecting).
/// </summary>
[Migration(202607271000)]
public sealed class M202607271000_CreateDropOffLocationsTable : Migration
{
    public override void Up()
    {
        Create.Table("DropOffLocations")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Address").AsString(500).NotNullable()
            .WithColumn("Latitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("Longitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("Location").AsCustom("geography").NotNullable()
            .WithColumn("City").AsString(100).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Execute.Sql("CREATE SPATIAL INDEX SIX_DropOffLocations_Location ON DropOffLocations(Location) USING GEOGRAPHY_AUTO_GRID;");
    }

    public override void Down()
    {
        Delete.Table("DropOffLocations");
    }
}
