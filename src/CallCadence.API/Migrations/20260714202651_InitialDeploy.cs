using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallCadence.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeploy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCallArchives",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiCallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCallArchives", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "ApiCallLogs",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiCallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestUri = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestHeaders = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestParameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseCode = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCallLogs", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "ApiCalls",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCalls", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "ApiCallSchedules",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiCallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCallSchedules", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SsoConfigurations",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SchemeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Authority = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientSecret = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataAddress = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CallbackPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoConfigurations", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    PkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.PkId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallArchives_ApiCallId",
                table: "ApiCallArchives",
                column: "ApiCallId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallArchives_Id",
                table: "ApiCallArchives",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallLogs_ApiCallId",
                table: "ApiCallLogs",
                column: "ApiCallId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallLogs_ExecutedAt",
                table: "ApiCallLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallLogs_Id",
                table: "ApiCallLogs",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCalls_Id",
                table: "ApiCalls",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallSchedules_ApiCallId",
                table: "ApiCallSchedules",
                column: "ApiCallId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallSchedules_Id",
                table: "ApiCallSchedules",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SsoConfigurations_SchemeName",
                table: "SsoConfigurations",
                column: "SchemeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Value",
                table: "Tags",
                column: "Value",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCallArchives");

            migrationBuilder.DropTable(
                name: "ApiCallLogs");

            migrationBuilder.DropTable(
                name: "ApiCalls");

            migrationBuilder.DropTable(
                name: "ApiCallSchedules");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "SsoConfigurations");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
