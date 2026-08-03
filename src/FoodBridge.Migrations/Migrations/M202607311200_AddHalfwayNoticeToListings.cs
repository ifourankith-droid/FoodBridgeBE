using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Marks when the donor was warned that their listing is half-way to its pickup deadline with no
/// volunteer yet — the prompt to deliver it themselves rather than let the food expire.
/// <para>
/// A stored timestamp rather than deriving "have we already warned them?" from the Notifications
/// table: the expiry sweep runs every 30 seconds, so the check has to be a cheap column test on the
/// row it is already scanning, not a second query per candidate. It also answers "did we warn them,
/// and when?" later, which a derived check cannot.
/// </para>
/// <para>
/// Nullable, and null means one of two ordinary things — not yet half-way, or already collected.
/// Existing rows are correctly left null: nobody was warned about them.
/// </para>
/// </summary>
[Migration(202607311200)]
public sealed class M202607311200_AddHalfwayNoticeToListings : Migration
{
    public override void Up()
    {
        Alter.Table("Listings")
            .AddColumn("HalfwayNoticeSentAtUtc").AsDateTime2().Nullable();
    }

    public override void Down()
    {
        Delete.Column("HalfwayNoticeSentAtUtc").FromTable("Listings");
    }
}
