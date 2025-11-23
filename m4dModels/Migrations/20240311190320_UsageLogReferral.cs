using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace m4dModels.Migrations;

/// <inheritdoc />
public partial class UsageLogReferral : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.AddColumn<string>(
            name: "Referrer",
            table: "UsageLog",
            type: "nvarchar(1024)",
            maxLength: 1024,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropColumn(
            name: "Referrer",
            table: "UsageLog");
    }
}
