using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class NormalizeTenant3NF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "TenantCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "TenantCustomers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFirstName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactLastName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "TenantCustomers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "(ltrim(rtrim(concat([FirstName],' ',[LastName]))))",
                stored: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "TenantCustomers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                computedColumnSql: "(case when [Street] IS NOT NULL AND [City] IS NOT NULL then concat([Street],', ',[City],case when [Province] IS NOT NULL then concat(', ',[Province]) else '' end) when [Street] IS NOT NULL then [Street] when [City] IS NOT NULL then [City]  end)",
                stored: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                computedColumnSql: "(ltrim(rtrim(concat(isnull([ContactFirstName],''),' ',isnull([ContactLastName],'')))))",
                stored: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "ContactFirstName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ContactLastName",
                table: "Suppliers");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "TenantCustomers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldComputedColumnSql: "(ltrim(rtrim(concat([FirstName],' ',[LastName]))))");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "TenantCustomers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComputedColumnSql: "(case when [Street] IS NOT NULL AND [City] IS NOT NULL then concat([Street],', ',[City],case when [Province] IS NOT NULL then concat(', ',[Province]) else '' end) when [Street] IS NOT NULL then [Street] when [City] IS NOT NULL then [City]  end)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComputedColumnSql: "(ltrim(rtrim(concat(isnull([ContactFirstName],''),' ',isnull([ContactLastName],'')))))");
        }
    }
}
