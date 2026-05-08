using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHA_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSihaProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSiha",
                table: "Aircrafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSiha",
                table: "Aircrafts");
        }
    }
}
