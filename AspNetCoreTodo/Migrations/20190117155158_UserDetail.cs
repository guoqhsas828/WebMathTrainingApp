using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WebMathTraining.Migrations
{
    public partial class UserDetail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AchievedLevel",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AchievedPoints",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestLogin",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AchievedLevel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AchievedPoints",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LatestLogin",
                table: "AspNetUsers");
        }
    }
}
