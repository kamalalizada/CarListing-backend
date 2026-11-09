using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCarDeletedAtAndPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cars_UserId",
                table: "Cars");

            // No data step: every existing row gets DeletedAt = NULL, so cars
            // that are already inactive become "Deactivated", not "Deleted".
            // Until now a user delete and an admin deactivation were stored
            // identically, so the safe reading is the one that keeps the car
            // visible to its owner and recoverable by an admin.
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Cars",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cars_UserId_DeletedAt_CreatedAt",
                table: "Cars",
                columns: new[] { "UserId", "DeletedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_CreatedAt",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_TokenHash",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "TokenHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_Cars_UserId_DeletedAt_CreatedAt",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Cars");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_UserId",
                table: "Cars",
                column: "UserId");
        }
    }
}
