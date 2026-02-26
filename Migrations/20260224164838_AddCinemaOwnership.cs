using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hive_Movie.Migrations
{
    /// <inheritdoc />
    public partial class AddCinemaOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Cinemas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Cinemas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrganizerId",
                table: "Cinemas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "OrganizerId",
                table: "Cinemas");
        }
    }
}
