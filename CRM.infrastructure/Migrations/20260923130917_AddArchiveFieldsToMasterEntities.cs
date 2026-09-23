using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveFieldsToMasterEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "ServiceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ServiceRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ServiceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "FollowUps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "FollowUps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "FollowUps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_IsArchived",
                table: "ServiceRequests",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_IsArchived",
                table: "FollowUps",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_IsArchived",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_IsArchived",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "FollowUps");
        }
    }
}
