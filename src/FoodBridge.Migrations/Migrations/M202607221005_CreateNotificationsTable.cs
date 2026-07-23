using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221005)]
public sealed class M202607221005_CreateNotificationsTable : Migration
{
    public override void Up()
    {
        Create.Table("Notifications")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("Type").AsString(50).NotNullable()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Body").AsString(1000).NotNullable()
            .WithColumn("PayloadJson").AsString(int.MaxValue).Nullable()
            .WithColumn("IsRead").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_Notifications_Users_UserId")
            .FromTable("Notifications").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Notifications");
    }
}
