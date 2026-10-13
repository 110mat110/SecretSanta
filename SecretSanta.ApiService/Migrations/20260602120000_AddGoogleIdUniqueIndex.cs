using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecretSanta.ApiService.Database;

#nullable disable

namespace SecretSanta.ApiService.Migrations
{
    [DbContext(typeof(Context))]
    [Migration("20260602120000_AddGoogleIdUniqueIndex")]
    public partial class AddGoogleIdUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");
        }
    }
}