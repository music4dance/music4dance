using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations
{
    /// <inheritdoc />
    public partial class UsageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "HitCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.CreateTable(
                name: "UsageLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsageId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Page = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Query = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Filter = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_UsageLog", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_UsageLog_UsageId",
                table: "UsageLog",
                column: "UsageId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_UsageLog_UserName",
                table: "UsageLog",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "UsageLog");

            _ = migrationBuilder.DropColumn(
                name: "HitCount",
                table: "AspNetUsers");
        }
    }
}
