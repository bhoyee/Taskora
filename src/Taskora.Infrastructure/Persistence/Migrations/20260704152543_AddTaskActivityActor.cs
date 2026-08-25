using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApp.Infrastructure.Persistence.Migrations
{
    // Adds an Actor column to TaskActivities, likely to record who performed
    // each logged activity rather than just what changed.
    /// <inheritdoc />
    public partial class AddTaskActivityActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actor",
                table: "TaskActivities",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Actor",
                table: "TaskActivities");
        }
    }
}
