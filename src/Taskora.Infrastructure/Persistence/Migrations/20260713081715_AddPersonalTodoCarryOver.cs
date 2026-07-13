using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalTodoCarryOver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CarriedOverFromDate",
                table: "PersonalTodos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginalTodoDate",
                table: "PersonalTodos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE PersonalTodos SET OriginalTodoDate = TodoDate WHERE OriginalTodoDate IS NULL");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalTodoDate",
                table: "PersonalTodos",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarriedOverFromDate",
                table: "PersonalTodos");

            migrationBuilder.DropColumn(
                name: "OriginalTodoDate",
                table: "PersonalTodos");
        }
    }
}
