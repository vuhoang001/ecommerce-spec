using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiscountProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discount_projection",
                schema: "catalog",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    discounted_price_minor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount_projection", x => x.product_id);
                    table.CheckConstraint("ck_discount_projection_non_negative", "discounted_price_minor >= 0");
                    table.ForeignKey(
                        name: "FK_discount_projection_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_discount_projection_price",
                schema: "catalog",
                table: "discount_projection",
                column: "discounted_price_minor");

            migrationBuilder.CreateIndex(
                name: "ix_discount_projection_retrieved_at",
                schema: "catalog",
                table: "discount_projection",
                column: "retrieved_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discount_projection",
                schema: "catalog");
        }
    }
}
