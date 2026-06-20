using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint28A_ContractLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "StudentProfiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractLineId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractLineId",
                table: "MakeupRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractLineId",
                table: "MakeupCreditTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractLineId",
                table: "ClassBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentContractLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LessonCount = table.Column<int>(type: "integer", nullable: false),
                    PricePerLesson = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MakeupAllowance = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsIndividual = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentContractLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentContractLines_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContractLines_InstructorProfiles_InstructorProfileId",
                        column: x => x.InstructorProfileId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContractLines_StudentContracts_StudentContractId",
                        column: x => x.StudentContractId,
                        principalTable: "StudentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentContractLines_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudentContractLineId",
                table: "Payments",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudioId_StudentContractLineId",
                table: "Payments",
                columns: new[] { "StudioId", "StudentContractLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeupRequests_StudentContractLineId",
                table: "MakeupRequests",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCreditTransactions_StudentContractLineId",
                table: "MakeupCreditTransactions",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupCreditTransactions_StudioId_StudentContractLineId",
                table: "MakeupCreditTransactions",
                columns: new[] { "StudioId", "StudentContractLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_StudentContractLineId",
                table: "ClassBookings",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_StudioId_StudentContractLineId",
                table: "ClassBookings",
                columns: new[] { "StudioId", "StudentContractLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_ClassTypeId",
                table: "StudentContractLines",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_InstructorProfileId",
                table: "StudentContractLines",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_StudentContractId",
                table: "StudentContractLines",
                column: "StudentContractId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_StudioId_ClassTypeId_InstructorProfile~",
                table: "StudentContractLines",
                columns: new[] { "StudioId", "ClassTypeId", "InstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_StudioId_DueDate",
                table: "StudentContractLines",
                columns: new[] { "StudioId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_StudioId_Status",
                table: "StudentContractLines",
                columns: new[] { "StudioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLines_StudioId_StudentContractId",
                table: "StudentContractLines",
                columns: new[] { "StudioId", "StudentContractId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassBookings_StudentContractLines_StudentContractLineId",
                table: "ClassBookings",
                column: "StudentContractLineId",
                principalTable: "StudentContractLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MakeupCreditTransactions_StudentContractLines_StudentContra~",
                table: "MakeupCreditTransactions",
                column: "StudentContractLineId",
                principalTable: "StudentContractLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MakeupRequests_StudentContractLines_StudentContractLineId",
                table: "MakeupRequests",
                column: "StudentContractLineId",
                principalTable: "StudentContractLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_StudentContractLines_StudentContractLineId",
                table: "Payments",
                column: "StudentContractLineId",
                principalTable: "StudentContractLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ---- Sprint 28A BACKFILL (global, deploy-time; idempotent) ----
            // Her sözleşme → 1 line (DueDate=NULL; TotalPrice=Price+Σamendment; PricePerLesson=TotalPrice/max(count,1)).
            // Payment/ClassBooking → contract'ın line'ı. MakeupCreditTransaction → SourceBooking/UsedForBooking→booking→line.
            // MakeupRequest → eşleşen aktif line. Bağlanamayan makeup ledger → soft-delete (line-bazlı zorunlu; NULL fallback yok).
            migrationBuilder.Sql(@"
INSERT INTO ""StudentContractLines""
  (""Id"",""StudioId"",""StudentContractId"",""ClassTypeId"",""InstructorProfileId"",""StartDate"",""EndDate"",
   ""LessonCount"",""PricePerLesson"",""TotalPrice"",""MakeupAllowance"",""DueDate"",""IsIndividual"",""Status"",
   ""Currency"",""SequenceNo"",""Notes"",""CreatedByUserId"",""CreatedAt"",""UpdatedAt"",""IsDeleted"")
SELECT gen_random_uuid(), c.""StudioId"", c.""Id"", c.""ClassTypeId"", c.""InstructorProfileId"", c.""StartDate"", c.""EndDate"",
   c.""TotalLessonCount"",
   CASE WHEN c.""TotalLessonCount"" > 0 THEN round((c.""Price"" + COALESCE(amd.add,0)) / c.""TotalLessonCount"", 2) ELSE 0 END,
   c.""Price"" + COALESCE(amd.add,0), c.""MakeupAllowance"", NULL, c.""IsIndividualDefault"", c.""Status"",
   c.""Currency"", 1, NULL, c.""CreatedByUserId"", now(), NULL, false
FROM ""StudentContracts"" c
LEFT JOIN (SELECT ""StudentContractId"" sc, SUM(""AdditionalPrice"") add FROM ""ContractAmendments"" WHERE ""IsDeleted""=false GROUP BY ""StudentContractId"") amd ON amd.sc = c.""Id""
WHERE c.""IsDeleted"" = false
  AND NOT EXISTS (SELECT 1 FROM ""StudentContractLines"" l WHERE l.""StudentContractId"" = c.""Id"" AND l.""IsDeleted""=false);

UPDATE ""Payments"" p SET ""StudentContractLineId"" = l.""Id""
FROM ""StudentContractLines"" l
WHERE l.""StudentContractId"" = p.""StudentContractId""
  AND p.""StudentContractId"" IS NOT NULL AND p.""StudentContractLineId"" IS NULL AND l.""IsDeleted""=false;

UPDATE ""ClassBookings"" b SET ""StudentContractLineId"" = l.""Id""
FROM ""StudentContractLines"" l
WHERE l.""StudentContractId"" = b.""StudentContractId""
  AND b.""StudentContractId"" IS NOT NULL AND b.""StudentContractLineId"" IS NULL AND l.""IsDeleted""=false;

UPDATE ""MakeupCreditTransactions"" t SET ""StudentContractLineId"" = sub.line
FROM (
  SELECT t2.""Id"" tid, COALESCE(bs.""StudentContractLineId"", bu.""StudentContractLineId"") line
  FROM ""MakeupCreditTransactions"" t2
  LEFT JOIN ""ClassBookings"" bs ON bs.""Id"" = t2.""SourceBookingId""
  LEFT JOIN ""ClassBookings"" bu ON bu.""Id"" = t2.""UsedForBookingId""
  WHERE t2.""StudentContractLineId"" IS NULL AND t2.""IsDeleted""=false
) sub
WHERE t.""Id"" = sub.tid AND sub.line IS NOT NULL;

UPDATE ""MakeupRequests"" r SET ""StudentContractLineId"" = (
  SELECT l.""Id"" FROM ""StudentContractLines"" l
  JOIN ""StudentContracts"" c ON c.""Id"" = l.""StudentContractId""
  WHERE c.""StudentProfileId"" = r.""StudentProfileId""
    AND l.""ClassTypeId"" = r.""ClassTypeId"" AND l.""InstructorProfileId"" = r.""RequestedInstructorProfileId""
    AND l.""Status"" = 'Active' AND l.""IsDeleted""=false
  ORDER BY l.""EndDate"" ASC LIMIT 1)
WHERE r.""StudentContractLineId"" IS NULL AND r.""IsDeleted""=false;

UPDATE ""MakeupCreditTransactions"" SET ""IsDeleted"" = true, ""UpdatedAt"" = now()
WHERE ""StudentContractLineId"" IS NULL AND ""IsDeleted"" = false;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassBookings_StudentContractLines_StudentContractLineId",
                table: "ClassBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_MakeupCreditTransactions_StudentContractLines_StudentContra~",
                table: "MakeupCreditTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_MakeupRequests_StudentContractLines_StudentContractLineId",
                table: "MakeupRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_StudentContractLines_StudentContractLineId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "StudentContractLines");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StudentContractLineId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StudioId_StudentContractLineId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MakeupRequests_StudentContractLineId",
                table: "MakeupRequests");

            migrationBuilder.DropIndex(
                name: "IX_MakeupCreditTransactions_StudentContractLineId",
                table: "MakeupCreditTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MakeupCreditTransactions_StudioId_StudentContractLineId",
                table: "MakeupCreditTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_StudentContractLineId",
                table: "ClassBookings");

            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_StudioId_StudentContractLineId",
                table: "ClassBookings");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "StudentContractLineId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "StudentContractLineId",
                table: "MakeupRequests");

            migrationBuilder.DropColumn(
                name: "StudentContractLineId",
                table: "MakeupCreditTransactions");

            migrationBuilder.DropColumn(
                name: "StudentContractLineId",
                table: "ClassBookings");
        }
    }
}
