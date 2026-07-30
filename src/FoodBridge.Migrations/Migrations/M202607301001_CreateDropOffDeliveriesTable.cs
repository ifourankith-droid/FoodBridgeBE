using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Append-only log of one row per completed drop-off: which spot received food, from which
/// volunteer, for which listing, when.
/// <para>
/// Serves two purposes that both need history rather than a single "last delivered" column:
/// the <b>cooldown</b> (a spot that just received food is hidden from every volunteer for a
/// configured window, so two volunteers can't unknowingly dump food at the same place minutes
/// apart) and <b>hotspot intensity</b> (how much a spot is actually used, so a volunteer can
/// see at a glance where demand concentrates).
/// </para>
/// </summary>
[Migration(202607301001)]
public sealed class M202607301001_CreateDropOffDeliveriesTable : Migration
{
    public override void Up()
    {
        Create.Table("DropOffDeliveries")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("DropOffLocationId").AsGuid().NotNullable()
                .ForeignKey("FK_DropOffDeliveries_DropOffLocationId_DropOffLocations_Id", "DropOffLocations", "Id")
            .WithColumn("VolunteerId").AsGuid().NotNullable()
                .ForeignKey("FK_DropOffDeliveries_VolunteerId_Users_Id", "Users", "Id")
            .WithColumn("ListingId").AsGuid().NotNullable()
                .ForeignKey("FK_DropOffDeliveries_ListingId_Listings_Id", "Listings", "Id")
            .WithColumn("MealsCount").AsInt32().NotNullable()
            .WithColumn("DeliveredAtUtc").AsDateTime2().NotNullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable();

        // The cooldown check and the intensity aggregate both filter/group by location and
        // order by time, so this covers the hot path for each.
        Execute.Sql(@"
CREATE INDEX IX_DropOffDeliveries_DropOffLocationId_DeliveredAtUtc
    ON DropOffDeliveries(DropOffLocationId, DeliveredAtUtc DESC);");

        // One row per listing: a listing is delivered exactly once, so this makes a duplicate
        // log row from a retry impossible at the database level rather than only in code.
        Execute.Sql(@"
CREATE UNIQUE INDEX UX_DropOffDeliveries_ListingId
    ON DropOffDeliveries(ListingId);");
    }

    public override void Down()
    {
        Delete.Table("DropOffDeliveries");
    }
}
