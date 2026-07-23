using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221003)]
public sealed class M202607221003_CreateListingImagesTable : Migration
{
    public override void Up()
    {
        Create.Table("ListingImages")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("ListingId").AsGuid().NotNullable()
            .WithColumn("ImageUrl").AsString(500).NotNullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_ListingImages_Listings_ListingId")
            .FromTable("ListingImages").ForeignColumn("ListingId")
            .ToTable("Listings").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("ListingImages");
    }
}
