using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// RecipientType: 1=Individual, 2=Organization. Only meaningful when Role=Recipient.
/// Added post-Phase-1 after comparing against the FoodBridge_Bootstrap_Prototype,
/// which distinguishes household vs. NGO/org recipients at registration.
/// </summary>
[Migration(202607230900)]
public sealed class M202607230900_AddRecipientTypeToUsers : Migration
{
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("RecipientType").AsByte().Nullable();
    }

    public override void Down()
    {
        Delete.Column("RecipientType").FromTable("Users");
    }
}
