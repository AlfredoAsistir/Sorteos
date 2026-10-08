using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eLotto.Migrations
{
    /// <inheritdoc />
    public partial class SorteoZonaHorariaMexico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZonaHoraria",
                table: "Sorteos",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "CDMX");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZonaHoraria",
                table: "Sorteos");
        }
    }
}
