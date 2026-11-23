using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations
{
    /// <inheritdoc />
    public partial class PartnerClientSoftCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SoftCap",
                table: "PartnerClients",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoftCap",
                table: "PartnerClients");
        }
    }
}
