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
            migrationBuilder.AddColumn<int>(
                name: "ExpectedStatusCode",
                table: "ApiCalls",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedStatusCode",
                table: "ApiCallArchives",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedStatusCode",
                table: "ApiCalls");

            migrationBuilder.DropColumn(
                name: "ExpectedStatusCode",
                table: "ApiCallArchives");
        }
    }
}
