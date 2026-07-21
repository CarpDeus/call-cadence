using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHttpMethodToApiCallLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "ApiCallLogs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "GET");

            // Backfill HttpMethod from the corresponding ApiCall record
            migrationBuilder.Sql(
                @"UPDATE l
                  SET l.HttpMethod = a.HttpMethod
                  FROM ApiCallLogs l
                  INNER JOIN ApiCalls a ON l.ApiCallId = a.Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "ApiCallLogs");
        }
    }
}
