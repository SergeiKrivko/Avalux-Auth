using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvaluxAuth.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UserSubscriptionStartsAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                table: "UserSubscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartsAt",
                table: "UserSubscriptions");
        }
    }
}
