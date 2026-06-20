using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint27_MakeupRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MakeupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedInstructorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResultingClassSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultingBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeupRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakeupRequests_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupRequests_StudioId_RequestedInstructorProfileId_Status",
                table: "MakeupRequests",
                columns: new[] { "StudioId", "RequestedInstructorProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupRequests_StudioId_Status",
                table: "MakeupRequests",
                columns: new[] { "StudioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupRequests_StudioId_StudentProfileId_Status",
                table: "MakeupRequests",
                columns: new[] { "StudioId", "StudentProfileId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MakeupRequests");
        }
    }
}
