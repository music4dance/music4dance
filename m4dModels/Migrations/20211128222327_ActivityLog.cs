using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations;

public partial class ActivityLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "ActivityLog",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Details = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ActivityLog", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ActivityLog_AspNetUsers_ApplicationUserId",
                    column: x => x.ApplicationUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_ActivityLog_ApplicationUserId",
            table: "ActivityLog",
            column: "ApplicationUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "ActivityLog");
    }
}
