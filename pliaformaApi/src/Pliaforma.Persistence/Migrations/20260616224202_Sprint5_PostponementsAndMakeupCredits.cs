using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5_PostponementsAndMakeupCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RescheduleReason",
                table: "ClassSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RescheduledAt",
                table: "ClassSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionKind",
                table: "ClassSessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.CreateTable(
                name: "MakeupCreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceClassSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedForBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeupCreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakeupCreditTransactions_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MakeupCreditTransactions_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudioClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OffsetDays = table.Column<int>(type: "integer", nullable: true),
                    AffectedInstructorProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedClassTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false),
                    CreditedCount = table.Column<int>(type: "integer", nullable: false),
                    RescheduledCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudioClosures_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudioMakeupPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyManualCreditLimit = table.Column<int>(type: "integer", nullable: false),
                    DefaultCreditValidityDays = table.Column<int>(type: "integer", nullable: false),
                    AllowInstructorManualGrant = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMakeupBookingWithoutCreditByAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioMakeupPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudioMakeupPolicies_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCreditTransactions_StudentProfileId_SourceClassSessio~",
                table: "MakeupCreditTransactions",
                columns: new[] { "StudentProfileId", "SourceClassSessionId" },
                unique: true,
                filter: "\"Type\" = 'Granted' AND \"SourceClassSessionId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCreditTransactions_StudioId_StudentProfileId",
                table: "MakeupCreditTransactions",
                columns: new[] { "StudioId", "StudentProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCreditTransactions_UsedForBookingId",
                table: "MakeupCreditTransactions",
                column: "UsedForBookingId",
                unique: true,
                filter: "\"UsedForBookingId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StudioClosures_StudioId_StartAt",
                table: "StudioClosures",
                columns: new[] { "StudioId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudioMakeupPolicies_StudioId",
                table: "StudioMakeupPolicies",
                column: "StudioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MakeupCreditTransactions");

            migrationBuilder.DropTable(
                name: "StudioClosures");

            migrationBuilder.DropTable(
                name: "StudioMakeupPolicies");

            migrationBuilder.DropColumn(
                name: "RescheduleReason",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "RescheduledAt",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "SessionKind",
                table: "ClassSessions");
        }
    }
}
