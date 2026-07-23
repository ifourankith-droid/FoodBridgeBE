using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Status: 1=Open, 2=Resolved.
/// </summary>
[Migration(202607221008)]
public sealed class M202607221008_CreateDisputesTable : Migration
{
    public override void Up()
    {
        Create.Table("Disputes")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("ListingId").AsGuid().NotNullable()
            .WithColumn("RaisedByUserId").AsGuid().NotNullable()
            .WithColumn("Reason").AsString(1000).NotNullable()
            .WithColumn("Status").AsByte().NotNullable().WithDefaultValue(1)
            .WithColumn("ResolvedByUserId").AsGuid().Nullable()
            .WithColumn("ResolutionNote").AsString(1000).Nullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_Disputes_Listings_ListingId")
            .FromTable("Disputes").ForeignColumn("ListingId")
            .ToTable("Listings").PrimaryColumn("Id");

        Create.ForeignKey("FK_Disputes_Users_RaisedByUserId")
            .FromTable("Disputes").ForeignColumn("RaisedByUserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_Disputes_Users_ResolvedByUserId")
            .FromTable("Disputes").ForeignColumn("ResolvedByUserId")
            .ToTable("Users").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Disputes");
    }
}
