using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint10_PlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Yeni durum kolonu. Varsayılan Active; mevcut satırlar IsActive'e göre backfill edilir (aşağıda),
            // sonra eski IsActive kolonu düşürülür → veri kaybı yok.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Studios",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReactivatedAt",
                table: "Studios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "Studios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                table: "Studios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByPlatformAdminUserId",
                table: "Studios",
                type: "uuid",
                nullable: true);

            // Mevcut stüdyoların durumunu eski IsActive değerinden taşı (askıdakiler Suspended olur).
            migrationBuilder.Sql(
                "UPDATE \"Studios\" SET \"Status\" = CASE WHEN \"IsActive\" THEN 'Active' ELSE 'Suspended' END;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Studios");

            migrationBuilder.CreateTable(
                name: "PlatformSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentPeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentPeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TrialEndsAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformSubscriptions_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudioId_DueDate",
                table: "Payments",
                columns: new[] { "StudioId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSubscriptions_StudioId",
                table: "PlatformSubscriptions",
                column: "StudioId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StudioId_DueDate",
                table: "Payments");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Studios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Status'tan IsActive'e geri taşı (yalnızca Active → true).
            migrationBuilder.Sql(
                "UPDATE \"Studios\" SET \"IsActive\" = (\"Status\" = 'Active');");

            migrationBuilder.DropColumn(
                name: "ReactivatedAt",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "UpdatedByPlatformAdminUserId",
                table: "Studios");
        }
    }
}
