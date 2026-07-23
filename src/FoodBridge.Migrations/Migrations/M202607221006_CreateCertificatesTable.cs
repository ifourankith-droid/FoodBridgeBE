using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221006)]
public sealed class M202607221006_CreateCertificatesTable : Migration
{
    public override void Up()
    {
        Create.Table("Certificates")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("CertificateNumber").AsString(30).NotNullable().Unique("UX_Certificates_CertificateNumber")
            .WithColumn("DonorId").AsGuid().NotNullable()
            .WithColumn("ListingId").AsGuid().NotNullable()
            .WithColumn("MealsCount").AsInt32().NotNullable()
            .WithColumn("IssuedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("PdfUrl").AsString(500).Nullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_Certificates_Users_DonorId")
            .FromTable("Certificates").ForeignColumn("DonorId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_Certificates_Listings_ListingId")
            .FromTable("Certificates").ForeignColumn("ListingId")
            .ToTable("Listings").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Certificates");
    }
}
