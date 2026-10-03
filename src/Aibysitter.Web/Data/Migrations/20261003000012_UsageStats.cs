using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aibysitter.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UsageStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsageStats",
                schema: "dbo",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Metric = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Key = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageStats", x => new { x.Date, x.Metric, x.Key });
                    table.CheckConstraint("CK_UsageStats_Metric", "[Metric] IN ('lint', 'finding', 'review', 'badge')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageStats",
                schema: "dbo");
        }
    }
}
