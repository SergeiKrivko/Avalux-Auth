using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvaluxAuth.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ProviderDefaultScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "DefaultScope",
                table: "Providers",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultScope",
                table: "Providers");
        }
    }
}
