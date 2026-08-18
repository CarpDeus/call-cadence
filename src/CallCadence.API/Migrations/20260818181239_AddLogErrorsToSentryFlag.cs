using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLogErrorsToSentryFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LogErrorsToSentry",
                table: "ApiCalls",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LogErrorsToSentry",
                table: "ApiCallArchives",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogErrorsToSentry",
                table: "ApiCalls");

            migrationBuilder.DropColumn(
                name: "LogErrorsToSentry",
                table: "ApiCallArchives");
        }
    }
}
