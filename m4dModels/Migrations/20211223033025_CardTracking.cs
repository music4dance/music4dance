using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations
{
    public partial class CardTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "FailedCardAttempts",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.AddColumn<decimal>(
                name: "LifetimePurchased",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "FailedCardAttempts",
                table: "AspNetUsers");

            _ = migrationBuilder.DropColumn(
                name: "LifetimePurchased",
                table: "AspNetUsers");
        }
    }
}
