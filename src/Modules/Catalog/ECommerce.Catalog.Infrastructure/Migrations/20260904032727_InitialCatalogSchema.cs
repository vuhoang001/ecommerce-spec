using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // research.md R3 — case- and diacritic-insensitive partial name search (FR-017).
            // unaccent() is STABLE, not IMMUTABLE, so it cannot be used in a generated column
            // or an index directly. The wrapper below pins the dictionary and is safe to mark
            // IMMUTABLE, which is the documented way to do this, not a workaround.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION catalog.immutable_unaccent(text)
                RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "category",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    price_minor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.id);
                    table.CheckConstraint("ck_product_price_non_negative", "price_minor >= 0");
                    table.CheckConstraint("ck_product_stock_non_negative", "stock_quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "product_category",
                schema: "catalog",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_category", x => new { x.category_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_product_category_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_category_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_image",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_image", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_image_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_category_name",
                schema: "catalog",
                table: "category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_category_slug",
                schema: "catalog",
                table: "category",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_status_created_at",
                schema: "catalog",
                table: "product",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_product_status_price_minor",
                schema: "catalog",
                table: "product",
                columns: new[] { "status", "price_minor" });

            migrationBuilder.CreateIndex(
                name: "IX_product_category_product_id",
                schema: "catalog",
                table: "product_category",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_image_position",
                schema: "catalog",
                table: "product_image",
                columns: new[] { "product_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_image_primary",
                schema: "catalog",
                table: "product_image",
                column: "product_id",
                unique: true,
                filter: "is_primary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS catalog.immutable_unaccent(text);");
            migrationBuilder.DropTable(
                name: "product_category",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_image",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "category",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product",
                schema: "catalog");
        }
    }
}
