using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WebMathTraining.Migrations.TestDb
{
    public partial class TestResult : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestResults",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    TestSessionId = table.Column<long>(nullable: false),
                    UserId = table.Column<long>(nullable: false),
                    FinalScore = table.Column<double>(nullable: false),
                    MaximumScore = table.Column<double>(nullable: false),
                    Percentile = table.Column<double>(nullable: false),
                    TestStarted = table.Column<DateTime>(nullable: false),
                    TestEnded = table.Column<DateTime>(nullable: false),
                    TestResultData = table.Column<byte[]>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResults", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestResults");
        }
    }
}
