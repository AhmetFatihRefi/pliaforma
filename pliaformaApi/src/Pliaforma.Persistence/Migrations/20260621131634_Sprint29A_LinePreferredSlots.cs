using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint29A_LinePreferredSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentContractLinePreferredSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentContractLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PreferredStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentContractLinePreferredSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentContractLinePreferredSlots_StudentContractLines_Stud~",
                        column: x => x.StudentContractLineId,
                        principalTable: "StudentContractLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentContractLinePreferredSlots_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLinePreferredSlots_StudentContractLineId",
                table: "StudentContractLinePreferredSlots",
                column: "StudentContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLinePreferredSlots_StudioId_DayOfWeek",
                table: "StudentContractLinePreferredSlots",
                columns: new[] { "StudioId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentContractLinePreferredSlots_StudioId_StudentContractL~",
                table: "StudentContractLinePreferredSlots",
                columns: new[] { "StudioId", "StudentContractLineId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentContractLinePreferredSlots");
        }
    }
}
