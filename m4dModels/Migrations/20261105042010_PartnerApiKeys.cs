using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations
{
    /// <inheritdoc />
    public partial class PartnerApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerClients",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RatePerSecond = table.Column<double>(type: "float", nullable: true),
                    Burst = table.Column<int>(type: "int", nullable: true),
                    DailyCap = table.Column<long>(type: "bigint", nullable: true),
                    Disabled = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerApiKeys",
                columns: table => new
                {
                    KeyId = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(40)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Revoked = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApiKeys", x => x.KeyId);
                    table.ForeignKey(
                        name: "FK_PartnerApiKeys_PartnerClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "PartnerClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartnerDevelopers",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "nvarchar(40)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDevelopers", x => new { x.ClientId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PartnerDevelopers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnerDevelopers_PartnerClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "PartnerClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiKeys_ClientId",
                table: "PartnerApiKeys",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDevelopers_UserId",
                table: "PartnerDevelopers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerApiKeys");

            migrationBuilder.DropTable(
                name: "PartnerDevelopers");

            migrationBuilder.DropTable(
                name: "PartnerClients");
        }
    }
}
