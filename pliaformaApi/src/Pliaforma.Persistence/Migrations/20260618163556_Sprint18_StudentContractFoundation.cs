using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint18_StudentContractFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActualInstructorProfileId",
                table: "ClassSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndividual",
                table: "ClassSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractId",
                table: "ClassBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalLessonCount = table.Column<int>(type: "integer", nullable: false),
                    MakeupAllowance = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsIndividualDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentContracts_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContracts_InstructorProfiles_InstructorProfileId",
                        column: x => x.InstructorProfileId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContracts_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContracts_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_StudentContractId",
                table: "ClassBookings",
                column: "StudentContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_StudioId_StudentContractId",
                table: "ClassBookings",
                columns: new[] { "StudioId", "StudentContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContracts_ClassTypeId",
                table: "StudentContracts",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContracts_InstructorProfileId",
                table: "StudentContracts",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContracts_StudentProfileId",
                table: "StudentContracts",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContracts_StudioId_Status",
                table: "StudentContracts",
                columns: new[] { "StudioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContracts_StudioId_StudentProfileId",
                table: "StudentContracts",
                columns: new[] { "StudioId", "StudentProfileId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassBookings_StudentContracts_StudentContractId",
                table: "ClassBookings",
                column: "StudentContractId",
                principalTable: "StudentContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassBookings_StudentContracts_StudentContractId",
                table: "ClassBookings");

            migrationBuilder.DropTable(
                name: "StudentContracts");

            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_StudentContractId",
                table: "ClassBookings");

            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_StudioId_StudentContractId",
                table: "ClassBookings");

            migrationBuilder.DropColumn(
                name: "ActualInstructorProfileId",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "IsIndividual",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "StudentContractId",
                table: "ClassBookings");
        }
    }
}
