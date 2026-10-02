using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aibysitter.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialScoreHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ScoreHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Repo = table.Column<string>(type: "varchar(140)", unicode: false, maxLength: 140, nullable: false),
                    FileName = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    RulesetVersion = table.Column<short>(type: "smallint", nullable: false),
                    Score = table.Column<byte>(type: "tinyint", nullable: false),
                    Grade = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreHistory", x => x.Id);
                    table.CheckConstraint("CK_ScoreHistory_FileName", "[FileName] IN ('CLAUDE.md', 'AGENTS.md', '.github/copilot-instructions.md', 'GEMINI.md', '.cursorrules', '.windsurfrules')");
                    table.CheckConstraint("CK_ScoreHistory_Grade", "[Grade] IN ('A', 'B', 'C', 'D', 'F')");
                    table.CheckConstraint("CK_ScoreHistory_Score", "[Score] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_ScoreHistory_Source", "[Source] IN (1, 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistory_Repo_File_Created",
                schema: "dbo",
                table: "ScoreHistory",
                columns: new[] { "Repo", "FileName", "CreatedUtc" },
                descending: new[] { false, false, true })
                .Annotation("SqlServer:Include", new[] { "RulesetVersion", "Score", "Grade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreHistory",
                schema: "dbo");
        }
    }
}
