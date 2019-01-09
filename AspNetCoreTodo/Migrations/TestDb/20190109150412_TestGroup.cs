using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WebMathTraining.Migrations.TestDb
{
    public partial class TestGroup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    LastUpdated = table.Column<DateTime>(nullable: false),
                    TeamHeadId = table.Column<long>(nullable: false),
                    MembersInfo = table.Column<string>(nullable: true),
                    EnrolledSessionsInfo = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGroups", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestGroups");
        }
    }
}
