using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221001)]
public sealed class M202607221001_CreateOtpCodesTable : Migration
{
    public override void Up()
    {
        Create.Table("OtpCodes")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("Mobile").AsString(15).NotNullable()
            .WithColumn("CodeHash").AsString(256).NotNullable()
            .WithColumn("ExpiresAtUtc").AsDateTime2().NotNullable()
            .WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ConsumedAtUtc").AsDateTime2().Nullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("OtpCodes");
    }
}
