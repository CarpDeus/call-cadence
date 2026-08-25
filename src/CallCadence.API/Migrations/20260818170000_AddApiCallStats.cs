using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class AddApiCallStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('ApiCallStats', 'U') IS NULL " +
                "CREATE TABLE [ApiCallStats] (" +
                "    [PkId] int NOT NULL," +
                "    [TotalApiCalls] bigint NOT NULL DEFAULT 0," +
                "    [TotalSuccessfulCalls] bigint NOT NULL DEFAULT 0," +
                "    [LastSuccessfulCallAt] datetime2 NULL," +
                "    [TotalErroredCalls] bigint NOT NULL DEFAULT 0," +
                "    [LastErroredCallAt] datetime2 NULL," +
                "    [FirstApiCallAt] datetime2 NULL," +
                "    CONSTRAINT [PK_ApiCallStats] PRIMARY KEY ([PkId])" +
                ");");

            // Seed the single stats row from existing ApiCallLogs data
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [ApiCallStats] WHERE [PkId] = 1)
BEGIN
    INSERT INTO [ApiCallStats] ([PkId], [TotalApiCalls], [TotalSuccessfulCalls], [LastSuccessfulCallAt], [TotalErroredCalls], [LastErroredCallAt], [FirstApiCallAt])
    SELECT
        1,
        ISNULL(COUNT(*), 0),
        ISNULL(SUM(CASE WHEN [Success] = 1 THEN 1 ELSE 0 END), 0),
        MAX(CASE WHEN [Success] = 1 THEN [ExecutedAt] END),
        ISNULL(SUM(CASE WHEN [Success] = 0 THEN 1 ELSE 0 END), 0),
        MAX(CASE WHEN [Success] = 0 THEN [ExecutedAt] END),
        MIN([ExecutedAt])
    FROM [ApiCallLogs];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('ApiCallStats', 'U') IS NOT NULL DROP TABLE [ApiCallStats];");
        }
    }
}
