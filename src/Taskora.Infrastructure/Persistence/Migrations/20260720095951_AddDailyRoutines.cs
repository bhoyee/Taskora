using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyRoutines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isPostgres = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL";
            var guidType = isPostgres ? "uuid" : "TEXT";
            var dateOnlyType = isPostgres ? "date" : "TEXT";
            var boolType = isPostgres ? "boolean" : "INTEGER";
            var ticksType = isPostgres ? "bigint" : "INTEGER";

            migrationBuilder.AddColumn<Guid>(
                name: "DailyRoutineId",
                table: "PersonalTodos",
                type: guidType,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "PersonalTodos",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.CreateTable(
                name: "DailyRoutines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    UserId = table.Column<Guid>(type: guidType, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: dateOnlyType, nullable: false),
                    EndDate = table.Column<DateOnly>(type: dateOnlyType, nullable: true),
                    IsActive = table.Column<bool>(type: boolType, nullable: false),
                    LastGeneratedDate = table.Column<DateOnly>(type: dateOnlyType, nullable: true),
                    CreatedAt = table.Column<long>(type: ticksType, nullable: false),
                    UpdatedAt = table.Column<long>(type: ticksType, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: guidType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRoutines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyRoutines_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalTodos_DailyRoutineId_TodoDate",
                table: "PersonalTodos",
                columns: new[] { "DailyRoutineId", "TodoDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRoutines_UserId_IsActive",
                table: "DailyRoutines",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRoutines_UserId_StartDate",
                table: "DailyRoutines",
                columns: new[] { "UserId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalTodos_DailyRoutines_DailyRoutineId",
                table: "PersonalTodos",
                column: "DailyRoutineId",
                principalTable: "DailyRoutines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalTodos_DailyRoutines_DailyRoutineId",
                table: "PersonalTodos");

            migrationBuilder.DropTable(
                name: "DailyRoutines");

            migrationBuilder.DropIndex(
                name: "IX_PersonalTodos_DailyRoutineId_TodoDate",
                table: "PersonalTodos");

            migrationBuilder.DropColumn(
                name: "DailyRoutineId",
                table: "PersonalTodos");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "PersonalTodos");
        }
    }
}
