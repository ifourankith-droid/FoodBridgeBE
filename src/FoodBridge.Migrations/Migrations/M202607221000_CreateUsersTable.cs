using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Role: 1=Donor, 2=Volunteer, 3=Recipient, 4=Admin.
/// AccountStatus: 1=Pending, 2=Verified, 3=Suspended.
/// </summary>
[Migration(202607221000)]
public sealed class M202607221000_CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("Mobile").AsString(15).NotNullable().Unique("UX_Users_Mobile")
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Role").AsByte().NotNullable()
            .WithColumn("City").AsString(100).Nullable()
            .WithColumn("Address").AsString(500).Nullable()
            .WithColumn("Latitude").AsDecimal(9, 6).Nullable()
            .WithColumn("Longitude").AsDecimal(9, 6).Nullable()
            .WithColumn("Location").AsCustom("geography").Nullable()
            .WithColumn("CapacityMeals").AsInt32().Nullable()
            .WithColumn("IsAvailable").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("AccountStatus").AsByte().NotNullable().WithDefaultValue(1)
            .WithColumn("AvatarUrl").AsString(500).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Users");
    }
}
