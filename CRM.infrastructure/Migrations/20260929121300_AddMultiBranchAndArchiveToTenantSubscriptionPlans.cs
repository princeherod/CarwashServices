using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBranchAndArchiveToTenantSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "TenantSubscriptionPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "TenantSubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MultiBranchEnabled",
                table: "TenantSubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "TenantSubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "TenantSubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MultiBranchEnabled",
                table: "TenantSubscriptionPlans");
        }
    }
}
