using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DeltaQuery.Migrations
{
    public partial class CreateSourceTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SiteId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ListId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ListItemUniqueId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResType = table.Column<int>(type: "int", nullable: false),
                    ActType = table.Column<int>(type: "int", nullable: false),
                    OrgActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObsActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeDif = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
