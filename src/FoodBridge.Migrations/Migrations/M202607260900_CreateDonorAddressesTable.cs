using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// A donor's saved address book — lets a donor with multiple locations (e.g. restaurant
/// branches) pick a saved address on listing creation instead of retyping it every time.
/// Independent of Users.Address (a single profile address) and of Listings.PickupAddress
/// (still freeform per listing; a saved address is just one way to fill it in).
/// </summary>
[Migration(202607260900)]
public sealed class M202607260900_CreateDonorAddressesTable : Migration
{
    public override void Up()
    {
        Create.Table("DonorAddresses")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("DonorId").AsGuid().NotNullable()
            .WithColumn("Label").AsString(100).NotNullable()
            .WithColumn("Address").AsString(500).NotNullable()
            .WithColumn("Latitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("Longitude").AsDecimal(9, 6).NotNullable()
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_DonorAddresses_Users_DonorId")
            .FromTable("DonorAddresses").ForeignColumn("DonorId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.Index("IX_DonorAddresses_DonorId")
            .OnTable("DonorAddresses").OnColumn("DonorId");
    }

    public override void Down()
    {
        Delete.Table("DonorAddresses");
    }
}
