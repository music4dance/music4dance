using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations
{
    /// <inheritdoc />
    public partial class ServiceAccountTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAccountTokens",
                columns: table => new
                {
                    Service = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProtectedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InvalidReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LastAlertSent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAccountTokens", x => x.Service);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAccountTokens");
        }
    }
}
