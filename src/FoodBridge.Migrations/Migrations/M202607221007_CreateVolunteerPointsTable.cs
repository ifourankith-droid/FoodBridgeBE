using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221007)]
public sealed class M202607221007_CreateVolunteerPointsTable : Migration
{
    public override void Up()
    {
        Create.Table("VolunteerPoints")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("VolunteerId").AsGuid().NotNullable()
            .WithColumn("ListingId").AsGuid().NotNullable()
            .WithColumn("Points").AsInt32().NotNullable()
            .WithColumn("Reason").AsString(200).NotNullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_VolunteerPoints_Users_VolunteerId")
            .FromTable("VolunteerPoints").ForeignColumn("VolunteerId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_VolunteerPoints_Listings_ListingId")
            .FromTable("VolunteerPoints").ForeignColumn("ListingId")
            .ToTable("Listings").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("VolunteerPoints");
    }
}
