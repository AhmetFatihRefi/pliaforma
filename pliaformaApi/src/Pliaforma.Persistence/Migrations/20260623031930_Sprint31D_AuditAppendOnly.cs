using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pliaforma.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Sprint 31D-1 — SecurityAuditLogs DB-level append-only. BEFORE UPDATE OR DELETE trigger:
    /// UPDATE her zaman engellenir; DELETE yalnız oturumda <c>app.audit_purge='on'</c> (retention purge) ise izinli.
    /// INSERT serbest (audit logger çalışmaya devam eder). Model değişmez → has-pending clean.
    /// Bu, kaza/bug/normal app update-delete'ine karşı DB-level guard'dır; tam-compromised app'e karşı mutlak değildir.
    /// </summary>
    public partial class Sprint31D_AuditAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pliaforma_audit_append_only() RETURNS trigger AS $$
                BEGIN
                    IF (TG_OP = 'UPDATE') THEN
                        RAISE EXCEPTION 'SecurityAuditLogs append-only: UPDATE engellendi (Sprint 31D-1).';
                    ELSIF (TG_OP = 'DELETE') THEN
                        IF current_setting('app.audit_purge', true) IS DISTINCT FROM 'on' THEN
                            RAISE EXCEPTION 'SecurityAuditLogs append-only: DELETE yalniz retention purge ile (app.audit_purge) yapilabilir.';
                        END IF;
                        RETURN OLD;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_append_only
                    BEFORE UPDATE OR DELETE ON "SecurityAuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION pliaforma_audit_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_append_only ON \"SecurityAuditLogs\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS pliaforma_audit_append_only();");
        }
    }
}
