using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint13_Subscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "PlatformSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAt",
                table: "PlatformSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntroMonthlyPrice",
                table: "PlatformSubscriptions",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntroPeriodsRemaining",
                table: "PlatformSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId",
                table: "PlatformSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformSubscriptionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSubscriptionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformSubscriptionPayments_PlatformSubscriptions_Platform~",
                        column: x => x.PlatformSubscriptionId,
                        principalTable: "PlatformSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlatformSubscriptionPayments_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IntroMonthlyPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    IntroPeriodCount = table.Column<int>(type: "integer", nullable: true),
                    IsCustomQuote = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FeaturesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MaxInstructors = table.Column<int>(type: "integer", nullable: true),
                    MaxStudents = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSubscriptions_SubscriptionPlanId",
                table: "PlatformSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSubscriptionPayments_PlatformSubscriptionId",
                table: "PlatformSubscriptionPayments",
                column: "PlatformSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSubscriptionPayments_Source_PaidAt",
                table: "PlatformSubscriptionPayments",
                columns: new[] { "Source", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSubscriptionPayments_StudioId_PaidAt",
                table: "PlatformSubscriptionPayments",
                columns: new[] { "StudioId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IsDefault",
                table: "SubscriptionPlans",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                table: "PlatformSubscriptions",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ---- Default plan kataloğu seed (admin sonra düzenler) ----
            // Stüdyo = default + intro (1.990 × 3 ay). Diğerleri örnek; Kurumsal = teklif.
            migrationBuilder.Sql(@"
INSERT INTO ""SubscriptionPlans""
  (""Id"",""Name"",""Tier"",""MonthlyPrice"",""Currency"",""BillingPeriod"",""IntroMonthlyPrice"",""IntroPeriodCount"",
   ""IsCustomQuote"",""IsDefault"",""IsActive"",""DisplayOrder"",""Description"",""FeaturesJson"",""MaxInstructors"",""MaxStudents"",
   ""CreatedAt"",""UpdatedAt"",""IsDeleted"")
VALUES
  ('11111111-1111-4111-a111-111111111111','Başlangıç','Starter',1490,'TRY','Monthly',NULL,NULL,false,false,true,1,'Küçük stüdyolar için temel operasyon.',NULL,NULL,NULL,now(),NULL,false),
  ('22222222-2222-4222-a222-222222222222','Stüdyo','Pro',2990,'TRY','Monthly',1990,3,false,true,true,2,'En çok önerilen paket — tüm temel operasyon + büyümeye uygun.',NULL,NULL,NULL,now(),NULL,false),
  ('33333333-3333-4333-a333-333333333333','Profesyonel','Pro+',4990,'TRY','Monthly',NULL,NULL,false,false,true,3,'Yoğun stüdyolar için gelişmiş raporlar ve öncelikli destek.',NULL,NULL,NULL,now(),NULL,false),
  ('44444444-4444-4444-a444-444444444444','Çok Şube / Kurumsal','Enterprise',7990,'TRY','Monthly',NULL,NULL,true,false,true,4,'Birden fazla şube / özel ihtiyaçlar — tekliflendirilir.',NULL,NULL,NULL,now(),NULL,false);
");

            // ---- Backfill: aboneliği olmayan mevcut stüdyolara Active + bugün..+1 yıl (default plan snapshot) ----
            // Lisans kapısı sonradan geldiği için kimse deploy'da kilitlenmesin (güvenli geçiş).
            migrationBuilder.Sql(@"
INSERT INTO ""PlatformSubscriptions""
  (""Id"",""StudioId"",""SubscriptionPlanId"",""PlanName"",""Tier"",""Status"",""MonthlyPrice"",""Currency"",""BillingPeriod"",
   ""IntroMonthlyPrice"",""IntroPeriodsRemaining"",""CurrentPeriodStart"",""CurrentPeriodEnd"",""TrialEndsAt"",
   ""CancelAtPeriodEnd"",""CancelRequestedAt"",""CancelledAt"",""Notes"",""CreatedAt"",""UpdatedAt"",""IsDeleted"")
SELECT gen_random_uuid(), s.""Id"", '22222222-2222-4222-a222-222222222222','Stüdyo','Pro','Active',2990,'TRY','Monthly',
       NULL, NULL, CURRENT_DATE, (CURRENT_DATE + INTERVAL '1 year')::date, NULL,
       false, NULL, NULL, 'Lisans geçişi backfill (Sprint 13)', now(), NULL, false
FROM ""Studios"" s
WHERE s.""IsDeleted"" = false
  AND NOT EXISTS (SELECT 1 FROM ""PlatformSubscriptions"" ps WHERE ps.""StudioId"" = s.""Id"" AND ps.""IsDeleted"" = false);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                table: "PlatformSubscriptions");

            migrationBuilder.DropTable(
                name: "PlatformSubscriptionPayments");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_PlatformSubscriptions_SubscriptionPlanId",
                table: "PlatformSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "PlatformSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAt",
                table: "PlatformSubscriptions");

            migrationBuilder.DropColumn(
                name: "IntroMonthlyPrice",
                table: "PlatformSubscriptions");

            migrationBuilder.DropColumn(
                name: "IntroPeriodsRemaining",
                table: "PlatformSubscriptions");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "PlatformSubscriptions");
        }
    }
}
