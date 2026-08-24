using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SuspendWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isPostgres = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL";
            var guidType = isPostgres ? "uuid" : "TEXT";
            var dateTimeOffsetType = isPostgres ? "timestamp with time zone" : "TEXT";

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAt",
                table: "Workspaces",
                type: dateTimeOffsetType,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendedByUserId",
                table: "Workspaces",
                type: guidType,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                table: "Workspaces",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_SuspendedByUserId",
                table: "Workspaces",
                column: "SuspendedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_UserProfiles_SuspendedByUserId",
                table: "Workspaces",
                column: "SuspendedByUserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_UserProfiles_SuspendedByUserId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_SuspendedByUserId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "SuspendedByUserId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                table: "Workspaces");
        }
    }
}
