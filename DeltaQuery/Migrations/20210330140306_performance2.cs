using Microsoft.EntityFrameworkCore.Migrations;

namespace DeltaQuery.Migrations
{
    public partial class performance2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoOfRuns",
                table: "Performances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalDuration",
                table: "Performances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoOfRuns",
                table: "Performances");

            migrationBuilder.DropColumn(
                name: "TotalDuration",
                table: "Performances");
        }
    }
}
