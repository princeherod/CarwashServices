using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFollowUpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Customers_CustomerId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_ServiceRequests_RequestId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Users_CreatedBy",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_CreatedBy",
                table: "FollowUps");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "FollowUps",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_FollowUps_RequestId",
                table: "FollowUps",
                newName: "IX_FollowUps_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "FollowUps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FollowUps",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "FollowUps",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "FollowUps",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ContactMethod",
                table: "FollowUps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FollowUps",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DiscountOffer",
                table: "FollowUps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "FollowUps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "FollowUps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRequestRequestId",
                table: "FollowUps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "FollowUps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_ServiceRequestRequestId",
                table: "FollowUps",
                column: "ServiceRequestRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Customers_CustomerId",
                table: "FollowUps",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_ServiceRequests_ServiceRequestRequestId",
                table: "FollowUps",
                column: "ServiceRequestRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Users_UserId",
                table: "FollowUps",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Customers_CustomerId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_ServiceRequests_ServiceRequestRequestId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Users_UserId",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_ServiceRequestRequestId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ContactMethod",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "DiscountOffer",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ServiceRequestRequestId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "FollowUps");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "FollowUps",
                newName: "RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_FollowUps_UserId",
                table: "FollowUps",
                newName: "IX_FollowUps_RequestId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "FollowUps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FollowUps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "FollowUps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "FollowUps",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CreatedBy",
                table: "FollowUps",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Customers_CustomerId",
                table: "FollowUps",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_ServiceRequests_RequestId",
                table: "FollowUps",
                column: "RequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Users_CreatedBy",
                table: "FollowUps",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
