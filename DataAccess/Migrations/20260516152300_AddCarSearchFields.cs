using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCarSearchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyType",
                table: "Cars",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Cars",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Cars",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Cars",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Engine",
                table: "Cars",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FuelType",
                table: "Cars",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Mileage",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Transmission",
                table: "Cars",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_BodyType",
                table: "Cars",
                column: "BodyType");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Brand",
                table: "Cars",
                column: "Brand");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_City",
                table: "Cars",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_FuelType",
                table: "Cars",
                column: "FuelType");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Mileage",
                table: "Cars",
                column: "Mileage");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Model",
                table: "Cars",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Price",
                table: "Cars",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Transmission",
                table: "Cars",
                column: "Transmission");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Year",
                table: "Cars",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cars_BodyType",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Brand",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_City",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_FuelType",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Mileage",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Model",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Price",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Transmission",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_Year",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "BodyType",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Engine",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "FuelType",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Mileage",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Transmission",
                table: "Cars");
        }
    }
}
