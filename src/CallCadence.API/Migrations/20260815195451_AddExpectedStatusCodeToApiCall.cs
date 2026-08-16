using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExpectedStatusCodeToApiCall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotency guard: some databases may already have the column applied
            // out-of-band (without a matching __EFMigrationsHistory row). Only add the
            // column when it does not already exist so re-applying is safe.
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApiCalls', 'ExpectedStatusCode') IS NULL " +
                "ALTER TABLE [ApiCalls] ADD [ExpectedStatusCode] int NULL;");

            migrationBuilder.Sql(
                "IF COL_LENGTH('ApiCallArchives', 'ExpectedStatusCode') IS NULL " +
                "ALTER TABLE [ApiCallArchives] ADD [ExpectedStatusCode] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApiCalls', 'ExpectedStatusCode') IS NOT NULL " +
                "ALTER TABLE [ApiCalls] DROP COLUMN [ExpectedStatusCode];");

            migrationBuilder.Sql(
                "IF COL_LENGTH('ApiCallArchives', 'ExpectedStatusCode') IS NOT NULL " +
                "ALTER TABLE [ApiCallArchives] DROP COLUMN [ExpectedStatusCode];");
        }
    }
}
