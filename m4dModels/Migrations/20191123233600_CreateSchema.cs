using Microsoft.EntityFrameworkCore.Migrations;

namespace m4dModels.Migrations;

public partial class CreateSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                UserName = table.Column<string>(maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(maxLength: 256, nullable: true),
                Email = table.Column<string>(maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(nullable: false),
                PasswordHash = table.Column<string>(nullable: true),
                SecurityStamp = table.Column<string>(nullable: true),
                ConcurrencyStamp = table.Column<string>(nullable: true),
                PhoneNumber = table.Column<string>(nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(nullable: false),
                TwoFactorEnabled = table.Column<bool>(nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(nullable: true),
                LockoutEnabled = table.Column<bool>(nullable: false),
                AccessFailedCount = table.Column<int>(nullable: false),
                StartDate = table.Column<DateTime>(nullable: false),
                LastActive = table.Column<DateTime>(nullable: false),
                Region = table.Column<string>(maxLength: 2, nullable: true),
                Privacy = table.Column<byte>(nullable: false),
                CanContact = table.Column<byte>(nullable: false),
                ServicePreference = table.Column<string>(maxLength: 10, nullable: true),
                RowCountDefault = table.Column<int>(nullable: true),
                ColumnDefaults = table.Column<string>(nullable: true),
                SubscriptionStart = table.Column<DateTime>(nullable: true),
                SubscriptionEnd = table.Column<DateTime>(nullable: true),
                SubscriptionLevel = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Dances",
            columns: table => new
            {
                Id = table.Column<string>(maxLength: 5, nullable: false),
                Description = table.Column<string>(nullable: true),
                Modified = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Dances", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "PlayLists",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                User = table.Column<string>(nullable: true),
                Type = table.Column<int>(nullable: false),
                Name = table.Column<string>(nullable: true),
                Description = table.Column<string>(nullable: true),
                Data1 = table.Column<string>(nullable: true),
                Data2 = table.Column<string>(nullable: true),
                Created = table.Column<DateTime>(nullable: false),
                Updated = table.Column<DateTime>(nullable: true),
                Deleted = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_PlayLists", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "TagGroups",
            columns: table => new
            {
                Key = table.Column<string>(nullable: false),
                Modified = table.Column<DateTime>(nullable: false),
                PrimaryId = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_TagGroups", x => x.Key);
                _ = table.ForeignKey(
                    name: "FK_TagGroups_TagGroups_PrimaryId",
                    column: x => x.PrimaryId,
                    principalTable: "TagGroups",
                    principalColumn: "Key",
                    onDelete: ReferentialAction.Restrict);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<string>(nullable: false),
                ClaimType = table.Column<string>(nullable: true),
                ClaimValue = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(nullable: false),
                ClaimType = table.Column<string>(nullable: true),
                ClaimValue = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(maxLength: 128, nullable: false),
                ProviderKey = table.Column<string>(maxLength: 128, nullable: false),
                ProviderDisplayName = table.Column<string>(nullable: true),
                UserId = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                _ = table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<string>(nullable: false),
                RoleId = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                _ = table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                _ = table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<string>(nullable: false),
                LoginProvider = table.Column<string>(maxLength: 128, nullable: false),
                Name = table.Column<string>(maxLength: 128, nullable: false),
                Value = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                _ = table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "Searches",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ApplicationUserId = table.Column<string>(nullable: true),
                Name = table.Column<string>(nullable: true),
                Query = table.Column<string>(nullable: false),
                Favorite = table.Column<bool>(nullable: false),
                Count = table.Column<int>(nullable: false),
                Created = table.Column<DateTime>(nullable: false),
                Modified = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Searches", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_Searches_AspNetUsers_ApplicationUserId",
                    column: x => x.ApplicationUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        _ = migrationBuilder.CreateTable(
            name: "DanceLink",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                DanceId = table.Column<string>(nullable: true),
                Description = table.Column<string>(nullable: true),
                Link = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_DanceLink", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_DanceLink_Dances_DanceId",
                    column: x => x.DanceId,
                    principalTable: "Dances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        _ = migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");

        _ = migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        _ = migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        _ = migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true,
            filter: "[NormalizedUserName] IS NOT NULL");

        _ = migrationBuilder.CreateIndex(
            name: "IX_DanceLink_DanceId",
            table: "DanceLink",
            column: "DanceId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_Searches_ApplicationUserId",
            table: "Searches",
            column: "ApplicationUserId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_TagGroups_PrimaryId",
            table: "TagGroups",
            column: "PrimaryId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        _ = migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        _ = migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        _ = migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        _ = migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        _ = migrationBuilder.DropTable(
            name: "DanceLink");

        _ = migrationBuilder.DropTable(
            name: "PlayLists");

        _ = migrationBuilder.DropTable(
            name: "Searches");

        _ = migrationBuilder.DropTable(
            name: "TagGroups");

        _ = migrationBuilder.DropTable(
            name: "AspNetRoles");

        _ = migrationBuilder.DropTable(
            name: "Dances");

        _ = migrationBuilder.DropTable(
            name: "AspNetUsers");
    }
}
