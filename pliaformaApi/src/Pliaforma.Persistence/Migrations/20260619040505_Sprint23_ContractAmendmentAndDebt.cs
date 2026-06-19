using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint23_ContractAmendmentAndDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentContractId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AdditionalLessonCount = table.Column<int>(type: "integer", nullable: false),
                    PreviousTotalLessonCount = table.Column<int>(type: "integer", nullable: false),
                    NewTotalLessonCount = table.Column<int>(type: "integer", nullable: false),
                    PreviousEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AdditionalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_StudentContracts_StudentContractId",
                        column: x => x.StudentContractId,
                        principalTable: "StudentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudentContractId",
                table: "Payments",
                column: "StudentContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudioId_StudentContractId",
                table: "Payments",
                columns: new[] { "StudioId", "StudentContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_StudentContractId",
                table: "ContractAmendments",
                column: "StudentContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_StudioId_StudentContractId",
                table: "ContractAmendments",
                columns: new[] { "StudioId", "StudentContractId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_StudentContracts_StudentContractId",
                table: "Payments",
                column: "StudentContractId",
                principalTable: "StudentContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_StudentContracts_StudentContractId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "ContractAmendments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StudentContractId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StudioId_StudentContractId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "StudentContractId",
                table: "Payments");
        }
    }
}
