using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// System-initiated timeline events (e.g. automatic expiry) have no human actor.
/// </summary>
[Migration(202607241200)]
public sealed class M202607241200_MakeListingTimelineActorNullable : Migration
{
    public override void Up()
    {
        Alter.Column("ActorUserId").OnTable("ListingTimeline").AsGuid().Nullable();
    }

    public override void Down()
    {
        Alter.Column("ActorUserId").OnTable("ListingTimeline").AsGuid().NotNullable();
    }
}
