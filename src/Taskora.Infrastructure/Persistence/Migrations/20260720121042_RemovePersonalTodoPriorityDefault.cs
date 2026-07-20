using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePersonalTodoPriorityDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "PersonalTodos",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "Medium");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "PersonalTodos",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);
        }
    }
}
