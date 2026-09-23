using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddArchiveFieldsToTenantEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "TenantCustomers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "TenantCustomers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "TenantCustomers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TenantCustomers_IsArchived",
                table: "TenantCustomers",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsArchived",
                table: "Products",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantCustomers_IsArchived",
                table: "TenantCustomers");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "TenantCustomers");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");
        }
    }
}
