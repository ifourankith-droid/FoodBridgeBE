using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// DietType: 1=Veg, 2=NonVeg. MealType: 1=Breakfast, 2=Lunch, 3=Dinner, 4=Snacks.
/// Both nullable, on top of the existing freeform FoodType text column.
/// </summary>
[Migration(202607231100)]
public sealed class M202607231100_AddDietTypeAndMealTypeToListings : Migration
{
    public override void Up()
    {
        Alter.Table("Listings").AddColumn("DietType").AsByte().Nullable();
        Alter.Table("Listings").AddColumn("MealType").AsByte().Nullable();
    }

    public override void Down()
    {
        Delete.Column("DietType").FromTable("Listings");
        Delete.Column("MealType").FromTable("Listings");
    }
}
