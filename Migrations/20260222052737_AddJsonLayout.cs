using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hive_Movie.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LayoutConfigurationJson",
                table: "Auditoriums",
                newName: "LayoutConfiguration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LayoutConfiguration",
                table: "Auditoriums",
                newName: "LayoutConfigurationJson");
        }
    }
}
