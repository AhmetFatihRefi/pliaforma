using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint38_ContractLinePaymentInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractLinePaymentInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentContractLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractLinePaymentInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractLinePaymentInstallments_StudentContractLines_Studen~",
                        column: x => x.StudentContractLineId,
                        principalTable: "StudentContractLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractLinePaymentInstallments_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractLinePaymentInstallments_StudentContractLineId",
                table: "ContractLinePaymentInstallments",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLinePaymentInstallments_StudioId_StudentContractLin~",
                table: "ContractLinePaymentInstallments",
                columns: new[] { "StudioId", "StudentContractLineId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractLinePaymentInstallments");
        }
    }
}
