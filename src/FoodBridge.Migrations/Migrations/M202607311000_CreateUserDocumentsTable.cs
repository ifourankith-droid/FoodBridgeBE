using FluentMigrator;

namespace FoodBridge.Migrations.Migrations;

/// <summary>
/// Verification evidence uploaded by a user — a government photo ID and a selfie — so an admin
/// reviewing the Verifications queue has something to judge beyond a name and an OTP-verified
/// mobile number.
/// <para>
/// Introduced because volunteers previously registered straight to <c>AccountStatus.Verified</c>:
/// anyone who could receive an SMS could immediately claim a stranger's food and drive off with it.
/// </para>
/// </summary>
[Migration(202607311000)]
public sealed class M202607311000_CreateUserDocumentsTable : Migration
{
    public override void Up()
    {
        Create.Table("UserDocuments")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey().WithDefault(SystemMethods.NewSequentialId)
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_UserDocuments_UserId_Users_Id", "Users", "Id")
            .WithColumn("Type").AsByte().NotNullable()
            .WithColumn("FileUrl").AsString(500).NotNullable()
            .WithColumn("OriginalFileName").AsString(260).Nullable()
            .WithColumn("CreatedAtUtc").AsDateTime2().NotNullable()
            .WithColumn("UpdatedAtUtc").AsDateTime2().NotNullable();

        // One document per type per user: re-uploading replaces rather than accumulating, so a
        // reviewing admin is never guessing which of five ID photos is the current one. Enforced
        // in the database as well as by the service's upsert, not just by convention.
        Execute.Sql(@"
CREATE UNIQUE INDEX UX_UserDocuments_UserId_Type
    ON UserDocuments(UserId, Type);");
    }

    public override void Down()
    {
        Delete.Table("UserDocuments");
    }
}
