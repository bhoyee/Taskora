using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApp.Infrastructure.Persistence.Migrations
{
    // Adds a nullable CreatedByUserId to Tasks to track task authorship, likely as
    // groundwork for creator-targeted notifications (no notification table added here).
    /// <inheritdoc />
    public partial class AddTaskCreatorAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Tasks");
        }
    }
}
