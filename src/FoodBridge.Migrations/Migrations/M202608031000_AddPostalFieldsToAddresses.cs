using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Completes the postal address on both places one is stored: the account itself (<c>Users</c>) and a
/// donor's saved pickup addresses (<c>DonorAddresses</c>).
/// <para>
/// Until now only free-text <c>Address</c> + <c>City</c> existed on Users, and <c>DonorAddresses</c>
/// had neither city nor pincode. Both the registration form and the profile's address form already
/// collected these fields and reverse-geocoding already resolved them — they were being discarded at
/// the API boundary, so the data was gathered and thrown away. This adds the columns so a complete
/// address can round-trip.
/// </para>
/// <para>
/// All nullable: every existing row genuinely has no pincode, and there is nothing to back-fill it
/// from. A donor's <c>Location</c> geography column stays the authority for anything spatial —
/// pincode is for display and for a human reading a pickup address, never for distance queries.
/// </para>
/// </summary>
[Migration(202608031000)]
public sealed class M202608031000_AddPostalFieldsToAddresses : Migration
{
    public override void Up()
    {
        Alter.Table("Users")
            .AddColumn("State").AsString(100).Nullable()
            .AddColumn("Pincode").AsString(10).Nullable();

        // City/State mirror Users so a saved pickup address is self-contained — a donor's branch in
        // another city must not inherit the account's city.
        Alter.Table("DonorAddresses")
            .AddColumn("City").AsString(100).Nullable()
            .AddColumn("State").AsString(100).Nullable()
            .AddColumn("Pincode").AsString(10).Nullable();
    }

    public override void Down()
    {
        Delete.Column("State").Column("Pincode").FromTable("Users");
        Delete.Column("City").Column("State").Column("Pincode").FromTable("DonorAddresses");
    }
}
