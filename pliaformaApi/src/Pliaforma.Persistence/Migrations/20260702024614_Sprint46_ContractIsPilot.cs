using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint46_ContractIsPilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPilot",
                table: "StudentContracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPilot",
                table: "StudentContractLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPilot",
                table: "StudentContracts");

            migrationBuilder.DropColumn(
                name: "IsPilot",
                table: "StudentContractLines");
        }
    }
}
