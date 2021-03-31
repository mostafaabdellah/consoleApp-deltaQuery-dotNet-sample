using Microsoft.EntityFrameworkCore.Migrations;

namespace DeltaQuery.Migrations
{
    public partial class performance3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AvgDuration",
                table: "Performances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgDuration",
                table: "Performances");
        }
    }
}
