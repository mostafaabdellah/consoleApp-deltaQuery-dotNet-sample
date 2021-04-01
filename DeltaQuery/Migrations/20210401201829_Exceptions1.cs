using Microsoft.EntityFrameworkCore.Migrations;

namespace DeltaQuery.Migrations
{
    public partial class Exceptions1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CallStack",
                table: "Exceptions",
                newName: "StackTrace");

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Exceptions",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "Exceptions");

            migrationBuilder.RenameColumn(
                name: "StackTrace",
                table: "Exceptions",
                newName: "CallStack");
        }
    }
}
