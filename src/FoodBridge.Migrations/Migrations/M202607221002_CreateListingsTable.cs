using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// FreshnessTag: 1=JustCooked, 2=FewHoursOld, 3=Packaged.
/// Status: 1=Pending, 2=Claimed, 3=PickedUp, 4=Delivered, 5=Confirmed, 6=Expired, 7=Cancelled, 8=Rejected.
/// </summary>
[Migration(202607221002)]
public sealed class M202607221002_CreateListingsTable : Migration
{
    public override void Up()
    {
        Create.Table("Listings")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("DonorId").AsGuid().NotNullable()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("FoodType").AsString(100).NotNullable()
            .WithColumn("QuantityMeals").AsInt32().NotNullable()
            .WithColumn("FreshnessTag").AsByte().NotNullable()
            .WithColumn("PreparedAtUtc").AsDateTime2().Nullable()
            .WithColumn("PickupDeadlineUtc").AsDateTime2().NotNullable()
            .WithColumn("PickupAddress").AsString(500).NotNullable()
            .WithColumn("Latitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("Longitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("Location").AsCustom("geography").NotNullable()
            .WithColumn("Status").AsByte().NotNullable().WithDefaultValue(1)
            .WithColumn("VolunteerId").AsGuid().Nullable()
            .WithColumn("RecipientId").AsGuid().Nullable()
            .WithColumn("RowVersion").AsCustom("rowversion")
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_Listings_Users_DonorId")
            .FromTable("Listings").ForeignColumn("DonorId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_Listings_Users_VolunteerId")
            .FromTable("Listings").ForeignColumn("VolunteerId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_Listings_Users_RecipientId")
            .FromTable("Listings").ForeignColumn("RecipientId")
            .ToTable("Users").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Listings");
    }
}
