using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddVehicleFieldsToTenantCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlateNumber",
                table: "TenantCustomers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TenantCustomers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleColor",
                table: "TenantCustomers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleMake",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleModel",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "TenantCustomers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleYear",
                table: "TenantCustomers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlateNumber",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "VehicleColor",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "VehicleMake",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "VehicleModel",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "VehicleYear",
                table: "TenantCustomers");
        }
    }
}
