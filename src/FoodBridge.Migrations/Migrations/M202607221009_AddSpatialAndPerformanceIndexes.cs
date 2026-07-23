using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

[Migration(202607221009)]
public sealed class M202607221009_AddSpatialAndPerformanceIndexes : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE SPATIAL INDEX SIX_Users_Location ON Users(Location) USING GEOGRAPHY_AUTO_GRID;");
        Execute.Sql("CREATE SPATIAL INDEX SIX_Listings_Location ON Listings(Location) USING GEOGRAPHY_AUTO_GRID;");
        Execute.Sql("CREATE NONCLUSTERED INDEX IX_Listings_Status_PickupDeadlineUtc ON Listings(Status, PickupDeadlineUtc);");
        Execute.Sql("CREATE NONCLUSTERED INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX SIX_Users_Location ON Users;");
        Execute.Sql("DROP INDEX SIX_Listings_Location ON Listings;");
        Execute.Sql("DROP INDEX IX_Listings_Status_PickupDeadlineUtc ON Listings;");
        Execute.Sql("DROP INDEX IX_Notifications_UserId_IsRead ON Notifications;");
    }
}
