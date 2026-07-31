using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Records the donor's food-safety declaration for each donation: they confirm the food is safe to
/// eat and that its quality remains their responsibility, before any volunteer is notified.
/// <para>
/// Stored per listing rather than once per donor account, because the declaration is about *this
/// specific food* — a blanket one-time agreement at sign-up would say nothing about the meal a
/// volunteer is collecting today.
/// </para>
/// <para>
/// Nullable, deliberately: listings created before this existed genuinely have no declaration, and
/// back-filling a timestamp would fabricate an acknowledgement nobody actually gave. New listings
/// cannot be created without one (enforced in <c>CreateListingRequestValidator</c>), so null means
/// "predates the requirement", never "skipped it".
/// </para>
/// </summary>
[Migration(202607311100)]
public sealed class M202607311100_AddFoodSafetyAcceptanceToListings : Migration
{
    public override void Up()
    {
        Alter.Table("Listings")
            .AddColumn("FoodSafetyAcceptedAtUtc").AsDateTime2().Nullable();
    }

    public override void Down()
    {
        Delete.Column("FoodSafetyAcceptedAtUtc").FromTable("Listings");
    }
}
