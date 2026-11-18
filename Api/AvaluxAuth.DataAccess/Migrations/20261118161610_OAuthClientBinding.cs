using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvaluxAuth.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class OAuthClientBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "RefreshTokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireClientSecret",
                table: "Applications",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Existing refresh tokens predate client binding and had no lifetime (M-03).
            // Keep the current sessions alive, but give them the client they were issued to
            // and a fresh absolute expiry (30 days, the runtime default) instead of never
            // expiring. RefreshTokens has a cascading FK to Users, so every row gets a client.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" AS rt
                SET "ClientId" = a."ClientId",
                    "CreatedAt" = CURRENT_TIMESTAMP,
                    "ExpiresAt" = CURRENT_TIMESTAMP + INTERVAL '30 days'
                FROM "Users" AS u
                JOIN "Applications" AS a ON a."Id" = u."ApplicationId"
                WHERE u."Id" = rt."UserId";
                """);

            // A client with a loopback redirect URI (desktop/CLI apps) cannot keep a client
            // secret: treat it as a public client and require PKCE for it (H-05).
            migrationBuilder.Sql("""
                UPDATE "Applications"
                SET "RequireClientSecret" = false
                WHERE EXISTS (
                    SELECT 1
                    FROM unnest("RedirectUrls") AS redirect
                    WHERE redirect LIKE 'http://localhost:%'
                       OR redirect LIKE 'http://127.0.0.1:%'
                       OR redirect LIKE 'http://[::1]:%'
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "RefreshTokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RequireClientSecret",
                table: "Applications");
        }
    }
}
