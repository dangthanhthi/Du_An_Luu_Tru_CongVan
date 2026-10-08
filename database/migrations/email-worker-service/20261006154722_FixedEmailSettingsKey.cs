using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailWorkerService.Migrations;

public partial class FixedEmailSettingsKey : Migration
{
    // SQL Server cannot ALTER away IDENTITY. Rebuild the known baseline table in the migration transaction.
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Rebuild(identity: false));
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Rebuild(identity: true));

    private static string Rebuild(bool identity)
    {
        var generated = identity ? "IDENTITY(1,1)" : "";
        var check = identity ? "" : ", CONSTRAINT [CK_EmailImapSettings_Singleton] CHECK ([Id] = 1)";
        var on = identity ? "SET IDENTITY_INSERT [emailworker].[EmailImapSettings_KeySwap] ON;" : "";
        var off = identity ? "SET IDENTITY_INSERT [emailworker].[EmailImapSettings_KeySwap] OFF;" : "";
        var expectedOldIdentity = identity ? 0 : 1;
        return $$"""
            IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'emailworker.EmailImapSettings')) <> 9
                OR COLUMNPROPERTY(OBJECT_ID(N'emailworker.EmailImapSettings'),N'Id','IsIdentity') <> {{expectedOldIdentity}}
                THROW 51000, 'Unexpected email settings schema; audited manual migration required.', 1;
            IF EXISTS (SELECT 1 FROM [emailworker].[EmailImapSettings] WHERE [Id] <> 1)
                THROW 51001, 'Unexpected email settings keys; audited manual migration required.', 1;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'emailworker.EmailImapSettings') OR parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                OR EXISTS (SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                OR EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'emailworker.EmailImapSettings') AND index_id>0 AND is_primary_key=0)
                OR EXISTS (SELECT 1 FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                OR EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
                OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings')
                    {{(identity ? "AND NOT (name=N'CK_EmailImapSettings_Singleton' AND definition=N'([Id]=(1))' AND is_disabled=0 AND is_not_trusted=0)" : "")}})
                THROW 51002, 'Custom email settings objects or permissions require audited manual migration.', 1;
            CREATE TABLE [emailworker].[EmailImapSettings_KeySwap] (
                [Id] int {{generated}} NOT NULL,
                [ImapHost] nvarchar(200) NOT NULL,
                [ImapPort] int NOT NULL,
                [UseSsl] bit NOT NULL,
                [EmailAddress] nvarchar(200) NOT NULL,
                [AppPassword] nvarchar(500) NOT NULL,
                [WhitelistedDomains] nvarchar(1000) NOT NULL,
                [AutoScanIntervalMinutes] int NOT NULL,
                [UpdatedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_EmailImapSettings_KeySwap] PRIMARY KEY ([Id]){{check}}
            );
            {{on}}
            INSERT INTO [emailworker].[EmailImapSettings_KeySwap]
                ([Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt])
                SELECT [Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt]
                FROM [emailworker].[EmailImapSettings];
            {{off}}
            DROP TABLE [emailworker].[EmailImapSettings];
            EXEC sp_rename N'emailworker.EmailImapSettings_KeySwap', N'EmailImapSettings';
            EXEC sp_rename N'emailworker.PK_EmailImapSettings_KeySwap', N'PK_EmailImapSettings', N'OBJECT';
            """;
    }
}
