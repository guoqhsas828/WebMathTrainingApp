using Microsoft.EntityFrameworkCore.Migrations;

namespace WebMathTraining.Migrations.TestDb
{
    public partial class TestSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetGrade",
                table: "TestSessions",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetGrade",
                table: "TestSessions");
        }
    }
}
