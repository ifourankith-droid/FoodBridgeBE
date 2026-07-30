using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Widens DropOffLocations from "admin-curated partner collection points" to the shared pool
/// of places food can be taken — including recipient hotspots discovered by volunteers in the
/// field and saved automatically at confirm-delivery.
/// <para>
/// One pool rather than a parallel table: a volunteer looking for somewhere to take food wants
/// a single ranked list, not two competing sets of suggestions. `Source` records where an entry
/// came from so the UI can distinguish a verified partner site from a crowd-sourced spot.
/// </para>
/// </summary>
[Migration(202607301000)]
public sealed class M202607301000_AddSourceToDropOffLocations : Migration
{
    public override void Up()
    {
        // 1 = Admin. Every pre-existing row was created through the admin endpoint, so the
        // default correctly backfills history as well as covering future admin inserts.
        Alter.Table("DropOffLocations")
            .AddColumn("Source").AsByte().NotNullable().WithDefaultValue(1);

        Alter.Table("DropOffLocations")
            .AddColumn("CreatedByUserId").AsGuid().Nullable()
            .ForeignKey("FK_DropOffLocations_CreatedByUserId_Users_Id", "Users", "Id");
    }

    public override void Down()
    {
        Delete.ForeignKey("FK_DropOffLocations_CreatedByUserId_Users_Id").OnTable("DropOffLocations");
        Delete.Column("CreatedByUserId").FromTable("DropOffLocations");
        Delete.Column("Source").FromTable("DropOffLocations");
    }
}
