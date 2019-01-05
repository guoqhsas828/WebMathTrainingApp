using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WebMathTraining.Migrations.TestDb
{
  public partial class TestSessionAndObjectIds : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "TestSessions",
          columns: table => new
          {
            Id = table.Column<Guid>(nullable: false),
            ObjectId = table.Column<long>(nullable: false)
                  .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
            Name = table.Column<string>(nullable: true),
            Description = table.Column<string>(nullable: true),
            TestQuestionData = table.Column<byte[]>(nullable: true),
            PlannedStart = table.Column<DateTime>(nullable: false),
            PlannedEnd = table.Column<DateTime>(nullable: false),
            TesterData = table.Column<byte[]>(nullable: true),
            LastUpdated = table.Column<DateTime>(nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TestSessions", x => x.Id);
          });

      migrationBuilder.AddColumn<long>(
    name: "ObjectId",
    table: "TestQuestions",
    nullable: false,
    defaultValue: 0L)
    .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "ObjectId",
          table: "TestQuestions");

      migrationBuilder.DropTable(
                name: "TestSessions");

    }
  }
}
