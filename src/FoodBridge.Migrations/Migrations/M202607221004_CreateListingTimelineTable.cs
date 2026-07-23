using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221004)]
public sealed class M202607221004_CreateListingTimelineTable : Migration
{
    public override void Up()
    {
        Create.Table("ListingTimeline")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("ListingId").AsGuid().NotNullable()
            .WithColumn("FromStatus").AsByte().Nullable()
            .WithColumn("ToStatus").AsByte().NotNullable()
            .WithColumn("ActorUserId").AsGuid().NotNullable()
            .WithColumn("Note").AsString(1000).Nullable()
            .WithColumn("PhotoUrl").AsString(500).Nullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_ListingTimeline_Listings_ListingId")
            .FromTable("ListingTimeline").ForeignColumn("ListingId")
            .ToTable("Listings").PrimaryColumn("Id");

        Create.ForeignKey("FK_ListingTimeline_Users_ActorUserId")
            .FromTable("ListingTimeline").ForeignColumn("ActorUserId")
            .ToTable("Users").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("ListingTimeline");
    }
}
