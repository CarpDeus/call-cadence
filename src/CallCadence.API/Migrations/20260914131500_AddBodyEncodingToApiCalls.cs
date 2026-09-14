using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBodyEncodingToApiCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BodyEncodings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyEncodings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BodyEncodings",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "JSON" },
                    { 2, "XML" },
                    { 3, "x-www-form-urlencoded" }
                });

            migrationBuilder.AddColumn<int>(
                name: "BodyEncoding",
                table: "ApiCalls",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyEncoding",
                table: "ApiCallArchives",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [ApiCalls]
                SET [BodyEncoding] = 1
                WHERE [Payload] IS NOT NULL AND LTRIM(RTRIM([Payload])) <> '' AND [BodyEncoding] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [ApiCallArchives]
                SET [BodyEncoding] = 1
                WHERE [Payload] IS NOT NULL AND LTRIM(RTRIM([Payload])) <> '' AND [BodyEncoding] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCalls_BodyEncoding",
                table: "ApiCalls",
                column: "BodyEncoding");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallArchives_BodyEncoding",
                table: "ApiCallArchives",
                column: "BodyEncoding");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiCallArchives_BodyEncodings_BodyEncoding",
                table: "ApiCallArchives",
                column: "BodyEncoding",
                principalTable: "BodyEncodings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApiCalls_BodyEncodings_BodyEncoding",
                table: "ApiCalls",
                column: "BodyEncoding",
                principalTable: "BodyEncodings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiCallArchives_BodyEncodings_BodyEncoding",
                table: "ApiCallArchives");

            migrationBuilder.DropForeignKey(
                name: "FK_ApiCalls_BodyEncodings_BodyEncoding",
                table: "ApiCalls");

            migrationBuilder.DropTable(
                name: "BodyEncodings");

            migrationBuilder.DropIndex(
                name: "IX_ApiCalls_BodyEncoding",
                table: "ApiCalls");

            migrationBuilder.DropIndex(
                name: "IX_ApiCallArchives_BodyEncoding",
                table: "ApiCallArchives");

            migrationBuilder.DropColumn(
                name: "BodyEncoding",
                table: "ApiCalls");

            migrationBuilder.DropColumn(
                name: "BodyEncoding",
                table: "ApiCallArchives");
        }
    }
}
