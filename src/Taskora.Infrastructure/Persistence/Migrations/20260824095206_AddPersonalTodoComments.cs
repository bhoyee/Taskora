using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskora.Infrastructure.Persistence.Migrations
{
    // Adds a PersonalTodoComments table (FK to PersonalTodos), supporting
    // threaded comments on personal todos.
    /// <inheritdoc />
    public partial class AddPersonalTodoComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isPostgres = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL";
            var guidType = isPostgres ? "uuid" : "TEXT";
            var ticksType = isPostgres ? "bigint" : "INTEGER";

            migrationBuilder.CreateTable(
                name: "PersonalTodoComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    TodoId = table.Column<Guid>(type: guidType, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<long>(type: ticksType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalTodoComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalTodoComments_PersonalTodos_TodoId",
                        column: x => x.TodoId,
                        principalTable: "PersonalTodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalTodoComments_TodoId_CreatedAt",
                table: "PersonalTodoComments",
                columns: new[] { "TodoId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalTodoComments");
        }
    }
}
