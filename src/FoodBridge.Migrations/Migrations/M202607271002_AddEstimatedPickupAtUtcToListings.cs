using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Lets a volunteer flag a delayed pickup ("I'll accept, but arrive in an hour")
/// instead of an implied immediate pickup. Nullable — null means no ETA was given.
/// Cleared back to null on unclaim (see ListingRepository.ChangeStatusAsync).
/// </summary>
[Migration(202607271002)]
public sealed class M202607271002_AddEstimatedPickupAtUtcToListings : Migration
{
    public override void Up()
    {
        Alter.Table("Listings")
            .AddColumn("EstimatedPickupAtUtc").AsDateTime2().Nullable();
    }

    public override void Down()
    {
        Delete.Column("EstimatedPickupAtUtc").FromTable("Listings");
    }
}
