using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.eShopWeb.Infrastructure.Migrations.Catalog
{
    public partial class InitCatalogDb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Baskets",
                columns: table => new
                {
                    BasketId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    BuyerId = table.Column<string>(maxLength: 900, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baskets", x => x.BasketId);
                });

            migrationBuilder.CreateTable(
                name: "TestGroup",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(maxLength: 128, nullable: true),
                    Description = table.Column<string>(maxLength: 512, nullable: true),
                    LastUpdated = table.Column<DateTime>(nullable: false),
                    TeamHeadId = table.Column<int>(nullable: false),
                    MembersInfo = table.Column<string>(nullable: true),
                    EnrolledSessionsInfo = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestImage",
                columns: table => new
                {
                    ObjectId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(maxLength: 128, nullable: true),
                    Data = table.Column<byte[]>(nullable: true),
                    Length = table.Column<int>(nullable: false),
                    Width = table.Column<int>(nullable: false),
                    Height = table.Column<int>(nullable: false),
                    ContentType = table.Column<string>(maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestImage", x => x.ObjectId);
                });

            migrationBuilder.CreateTable(
                name: "TestQuestion",
                columns: table => new
                {
                    ObjectId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Category = table.Column<int>(nullable: false),
                    Level = table.Column<int>(nullable: false),
                    QuestionImageId = table.Column<int>(nullable: false),
                    AnswerStream = table.Column<byte[]>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestQuestion", x => x.ObjectId);
                });

            migrationBuilder.CreateTable(
                name: "TestResult",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    TestSessionId = table.Column<int>(nullable: false),
                    UserId = table.Column<int>(nullable: false),
                    FinalScore = table.Column<double>(nullable: false),
                    MaximumScore = table.Column<double>(nullable: false),
                    Percentile = table.Column<double>(nullable: false),
                    TestStarted = table.Column<DateTime>(nullable: false),
                    TestEnded = table.Column<DateTime>(nullable: false),
                    TestResultData = table.Column<byte[]>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestSession",
                columns: table => new
                {
                    ObjectId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    TestQuestionData = table.Column<byte[]>(nullable: true),
                    PlannedStart = table.Column<DateTime>(nullable: false),
                    PlannedEnd = table.Column<DateTime>(nullable: false),
                    TesterData = table.Column<byte[]>(nullable: true),
                    LastUpdated = table.Column<DateTime>(nullable: false),
                    TargetGrade = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSession", x => x.ObjectId);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    TodoItemId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    OwnerId = table.Column<string>(maxLength: 900, nullable: true),
                    IsDone = table.Column<bool>(nullable: false),
                    Title = table.Column<string>(maxLength: 1024, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.TodoItemId);
                });

            migrationBuilder.CreateTable(
                name: "BasketItems",
                columns: table => new
                {
                    BasketItemId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    UnitPrice = table.Column<decimal>(nullable: false),
                    Quantity = table.Column<int>(nullable: false),
                    CatalogItemId = table.Column<int>(nullable: false),
                    BasketId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketItems", x => x.BasketItemId);
                    table.ForeignKey(
                        name: "FK_BasketItems_Baskets_BasketId",
                        column: x => x.BasketId,
                        principalTable: "Baskets",
                        principalColumn: "BasketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketItems_BasketId",
                table: "BasketItems",
                column: "BasketId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketItems");

            migrationBuilder.DropTable(
                name: "TestGroup");

            migrationBuilder.DropTable(
                name: "TestImage");

            migrationBuilder.DropTable(
                name: "TestQuestion");

            migrationBuilder.DropTable(
                name: "TestResult");

            migrationBuilder.DropTable(
                name: "TestSession");

            migrationBuilder.DropTable(
                name: "TodoItems");

            migrationBuilder.DropTable(
                name: "Baskets");
        }
    }
}
