using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskora.Infrastructure.Persistence.Migrations
{
    // No-op migration: Up()/Down() are both empty. Reconciles the EF Core
    // model snapshot for owned task-related tables without changing the
    // actual database schema.
    /// <inheritdoc />
    public partial class SyncOwnedTaskTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
