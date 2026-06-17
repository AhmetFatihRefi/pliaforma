using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint14_PackagePlanV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: mevcut paketler/planlar "tüm ders gruplarında geçerli" (kimse kilitlenmesin).
            migrationBuilder.AddColumn<bool>(
                name: "AllowAllClassTypesSnapshot",
                table: "StudentPackages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowAllClassTypes",
                table: "PackagePlans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpectedStudentPackageId",
                table: "ClassBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageEligibilityStatus",
                table: "ClassBookings",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPayment",
                table: "ClassBookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PackagePlanClassTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePlanClassTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagePlanClassTypes_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagePlanClassTypes_PackagePlans_PackagePlanId",
                        column: x => x.PackagePlanId,
                        principalTable: "PackagePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackagePlanClassTypes_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackagePlanPriceVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePlanPriceVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagePlanPriceVersions_PackagePlans_PackagePlanId",
                        column: x => x.PackagePlanId,
                        principalTable: "PackagePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackagePlanPriceVersions_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPackageAllowedClassTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPackageAllowedClassTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPackageAllowedClassTypes_ClassTypes_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "ClassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPackageAllowedClassTypes_StudentPackages_StudentPack~",
                        column: x => x.StudentPackageId,
                        principalTable: "StudentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentPackageAllowedClassTypes_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_StudioId_ExpectedStudentPackageId",
                table: "ClassBookings",
                columns: new[] { "StudioId", "ExpectedStudentPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackagePlanClassTypes_ClassTypeId",
                table: "PackagePlanClassTypes",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePlanClassTypes_PackagePlanId_ClassTypeId",
                table: "PackagePlanClassTypes",
                columns: new[] { "PackagePlanId", "ClassTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePlanClassTypes_StudioId",
                table: "PackagePlanClassTypes",
                column: "StudioId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePlanPriceVersions_PackagePlanId_EffectiveFrom",
                table: "PackagePlanPriceVersions",
                columns: new[] { "PackagePlanId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PackagePlanPriceVersions_StudioId",
                table: "PackagePlanPriceVersions",
                column: "StudioId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackageAllowedClassTypes_ClassTypeId",
                table: "StudentPackageAllowedClassTypes",
                column: "ClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackageAllowedClassTypes_StudentPackageId_ClassTypeId",
                table: "StudentPackageAllowedClassTypes",
                columns: new[] { "StudentPackageId", "ClassTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackageAllowedClassTypes_StudioId",
                table: "StudentPackageAllowedClassTypes",
                column: "StudioId");

            // ---- Backfill: her mevcut plan için başlangıç fiyat versiyonu (Price = bugünden geçerli) ----
            // Fiyatın source of truth'u versiyon tablosu olduğu için, mevcut planların fiyatı kaybolmasın.
            // CreatedByUserId = stüdyo sahibi (gerçek kullanıcı). Kimse kilitlenmez; satış doğru fiyatı bulur.
            migrationBuilder.Sql(@"
INSERT INTO ""PackagePlanPriceVersions""
  (""Id"",""StudioId"",""PackagePlanId"",""Price"",""Currency"",""EffectiveFrom"",""CreatedByUserId"",""CreatedAt"",""UpdatedAt"",""IsDeleted"")
SELECT gen_random_uuid(), p.""StudioId"", p.""Id"", p.""Price"",
       COALESCE(NULLIF(s.""Currency"",''),'TRY'), CURRENT_DATE, s.""OwnerUserId"", now(), NULL, false
FROM ""PackagePlans"" p
JOIN ""Studios"" s ON s.""Id"" = p.""StudioId""
WHERE p.""IsDeleted"" = false
  AND NOT EXISTS (SELECT 1 FROM ""PackagePlanPriceVersions"" v WHERE v.""PackagePlanId"" = p.""Id"" AND v.""IsDeleted"" = false);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackagePlanClassTypes");

            migrationBuilder.DropTable(
                name: "PackagePlanPriceVersions");

            migrationBuilder.DropTable(
                name: "StudentPackageAllowedClassTypes");

            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_StudioId_ExpectedStudentPackageId",
                table: "ClassBookings");

            migrationBuilder.DropColumn(
                name: "AllowAllClassTypesSnapshot",
                table: "StudentPackages");

            migrationBuilder.DropColumn(
                name: "AllowAllClassTypes",
                table: "PackagePlans");

            migrationBuilder.DropColumn(
                name: "ExpectedStudentPackageId",
                table: "ClassBookings");

            migrationBuilder.DropColumn(
                name: "PackageEligibilityStatus",
                table: "ClassBookings");

            migrationBuilder.DropColumn(
                name: "RequiresPayment",
                table: "ClassBookings");
        }
    }
}
