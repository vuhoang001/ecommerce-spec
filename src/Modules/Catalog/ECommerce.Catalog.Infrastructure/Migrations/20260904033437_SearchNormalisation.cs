using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchNormalisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // research.md R3 — normalise_name is the single function both sides of the search
            // pass through: the stored name via the generated column below, and the keyword via
            // a call at query time. IMMUTABLE is required for a generated column and an index.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION catalog.normalise_name(text)
                RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT lower(catalog.immutable_unaccent($1)) $$;");

            migrationBuilder.AddColumn<string>(
                name: "name_normalized",
                schema: "catalog",
                table: "product",
                type: "text",
                nullable: true,
                computedColumnSql: "catalog.normalise_name(name)",
                stored: true);
            // FR-017 — a GIN trigram index makes the infix match usable at 100,000 products;
            // ILIKE '%x%' without one is a sequential scan and misses the SC-003 budget.
            migrationBuilder.Sql(
                "CREATE INDEX ix_product_name_normalized_trgm ON catalog.product " +
                "USING gin (name_normalized gin_trgm_ops);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_product_name_normalized_trgm;");
            migrationBuilder.DropColumn(
                name: "name_normalized",
                schema: "catalog",
                table: "product");
        }
    }
}