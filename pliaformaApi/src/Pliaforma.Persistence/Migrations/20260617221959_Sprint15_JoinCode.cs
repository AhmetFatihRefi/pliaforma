using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint15_JoinCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Studios",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JoinCodeIsActive",
                table: "Studios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinCodeUpdatedAt",
                table: "Studios",
                type: "timestamp with time zone",
                nullable: true);

            // ---- Backfill: mevcut her stüdyoya benzersiz, tahmin edilemez JoinCode (kimse etkilenmez) ----
            // Alfabe karışan karakterleri (0/1/O/I/L) içermez; her satır için çakışmasız üretilir (loop).
            migrationBuilder.Sql(@"
DO $$
DECLARE r RECORD; code TEXT; alphabet TEXT := '23456789ABCDEFGHJKMNPQRSTUVWXYZ'; i INT;
BEGIN
  FOR r IN SELECT ""Id"" FROM ""Studios"" WHERE ""JoinCode"" IS NULL LOOP
    LOOP
      code := '';
      FOR i IN 1..10 LOOP
        code := code || substr(alphabet, floor(random()*length(alphabet))::int + 1, 1);
      END LOOP;
      EXIT WHEN NOT EXISTS (SELECT 1 FROM ""Studios"" WHERE ""JoinCode"" = code);
    END LOOP;
    UPDATE ""Studios"" SET ""JoinCode"" = code, ""JoinCodeIsActive"" = true, ""JoinCodeUpdatedAt"" = now()
    WHERE ""Id"" = r.""Id"";
  END LOOP;
END $$;
");

            migrationBuilder.CreateIndex(
                name: "IX_Studios_JoinCode",
                table: "Studios",
                column: "JoinCode",
                unique: true,
                filter: "\"JoinCode\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Studios_JoinCode",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "JoinCodeIsActive",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "JoinCodeUpdatedAt",
                table: "Studios");
        }
    }
}
