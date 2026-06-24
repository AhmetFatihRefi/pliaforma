using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint33_LineInstructorOptionalAndCapacityPref : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "InstructorProfileId",
                table: "StudentContracts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InstructorProfileId",
                table: "StudentContractLines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "PreferredMaxCapacity",
                table: "StudentContractLines",
                type: "integer",
                nullable: true);

            // Sprint 33 backfill: eski IsIndividual=true → PreferredMaxCapacity=1 (bireysel). false → NULL (farketmez; AddColumn default).
            migrationBuilder.Sql(@"UPDATE ""StudentContractLines"" SET ""PreferredMaxCapacity"" = 1 WHERE ""IsIndividual"" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredMaxCapacity",
                table: "StudentContractLines");

            migrationBuilder.AlterColumn<Guid>(
                name: "InstructorProfileId",
                table: "StudentContracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InstructorProfileId",
                table: "StudentContractLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
