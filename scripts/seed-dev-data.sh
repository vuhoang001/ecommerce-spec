#!/usr/bin/env bash
# Seeds the catalogue with the data quickstart.md's scenarios assume.
#
# Without this, every endpoint returns an empty page and a correctly working system looks
# broken. The automated suites seed their own containers, so this gap only shows up when a human
# follows the quickstart by hand — which is exactly when a confusing empty result costs the most.
#
# Requires the schema to exist. The host applies migrations at start-up (DEP-001), so run it once
# before seeding, or apply them with `dotnet ef database update`.
set -euo pipefail

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-ecommerce}"
PGUSER="${PGUSER:-ecommerce}"
export PGPASSWORD="${PGPASSWORD:-ecommerce}"

# -X ignores ~/.psqlrc. Without it a developer's `\timing on` injects "Time: 0.1 ms" lines into
# the output and any parsing of that output silently misreads it.
psql_run() {
    psql -X -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 "$@"
}

schema_count="$(psql_run -tAc "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'catalog'" | tr -d '[:space:]')"

if [ "$schema_count" != "1" ]; then
    echo "The 'catalog' schema does not exist. Start the host once so it applies migrations, or run:"
    echo "  dotnet ef database update --project src/Modules/Catalog/ECommerce.Catalog.Infrastructure"
    exit 1
fi

echo "Seeding development data into $PGUSER@$PGHOST:$PGPORT/$PGDATABASE ..."

psql_run <<'SQL'
BEGIN;

-- Idempotent: re-running replaces the seed rather than duplicating it.
DELETE FROM catalog.product_category WHERE category_id IN
    (SELECT id FROM catalog.category WHERE slug IN ('coffee','tea','gifts'));
DELETE FROM catalog.product WHERE name LIKE 'Seed:%';
DELETE FROM catalog.category WHERE slug IN ('coffee','tea','gifts');

INSERT INTO catalog.category (id, name, slug) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'Coffee', 'coffee'),
    ('a0000000-0000-0000-0000-000000000002', 'Tea',    'tea'),
    ('a0000000-0000-0000-0000-000000000003', 'Gifts',  'gifts');

-- 30 products in Coffee, so quickstart scenario 1 can page a 30-product category at page size 24.
INSERT INTO catalog.product (id, name, description, price_minor, currency_code,
                             stock_quantity, status, created_at)
SELECT
    ('b0000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid,
    'Seed: Cà phê số ' || n,
    'A seeded drink for local development.',
    30000 + (n * 5000),
    'VND',
    CASE WHEN n = 7 THEN 0 ELSE 5 END,       -- one out-of-stock product (FR-005)
    'Active',
    now() - (n || ' minutes')::interval       -- distinct timestamps so paging is deterministic
FROM generate_series(1, 30) AS n;

INSERT INTO catalog.product_category (product_id, category_id)
SELECT p.id, 'a0000000-0000-0000-0000-000000000001'
FROM catalog.product p WHERE p.name LIKE 'Seed: Cà phê%';

-- A product in two categories (FR-006), and one that is deliberately concealed (FR-001, SC-002).
INSERT INTO catalog.product (id, name, description, price_minor, currency_code,
                             stock_quantity, status, created_at) VALUES
    ('b0000000-0000-0000-0000-000000000901', 'Seed: Trà đào cam sả', 'In two categories.',
     45000, 'VND', 12, 'Active', now()),
    ('b0000000-0000-0000-0000-000000000902', 'Seed: Hidden blend',   'Must never be visible.',
     99000, 'VND', 3,  'Hidden', now()),
    ('b0000000-0000-0000-0000-000000000903', 'Seed: Discontinued brew', 'Must never be visible.',
     99000, 'VND', 3,  'Discontinued', now());

INSERT INTO catalog.product_category (product_id, category_id) VALUES
    ('b0000000-0000-0000-0000-000000000901', 'a0000000-0000-0000-0000-000000000002'),
    ('b0000000-0000-0000-0000-000000000901', 'a0000000-0000-0000-0000-000000000003');

-- A discount copy, so the price filter and the dual-price display have something to show
-- (FR-010, FR-026, FR-028). 250,000 discounted to 180,000 -- the worked example in the spec.
INSERT INTO catalog.product (id, name, description, price_minor, currency_code,
                             stock_quantity, status, created_at) VALUES
    ('b0000000-0000-0000-0000-000000000904', 'Seed: Cà phê sữa đá', 'Has an active discount.',
     250000, 'VND', 8, 'Active', now());

INSERT INTO catalog.product_category (product_id, category_id) VALUES
    ('b0000000-0000-0000-0000-000000000904', 'a0000000-0000-0000-0000-000000000001');

INSERT INTO catalog.discount_projection (product_id, promotion_id, discounted_price_minor,
                                         currency_code, occurred_at, retrieved_at) VALUES
    ('b0000000-0000-0000-0000-000000000904', 'c0000000-0000-0000-0000-000000000001',
     180000, 'VND', now(), now())
ON CONFLICT (product_id) DO UPDATE
    SET discounted_price_minor = EXCLUDED.discounted_price_minor,
        retrieved_at           = EXCLUDED.retrieved_at;

COMMIT;
SQL

echo
echo "Seeded. Try:"
echo "  curl 'http://localhost:5000/catalog/categories/a0000000-0000-0000-0000-000000000001/products'"
echo "  curl 'http://localhost:5000/catalog/products/search?q=ca%20phe'      # diacritic-insensitive"
echo "  curl 'http://localhost:5000/catalog/products?minPriceMinor=150000&maxPriceMinor=200000'"
echo
echo "The last one returns the discounted product flagged matchedOnDiscountedPriceOnly (FR-028)."
echo "'Seed: Hidden blend' and 'Seed: Discontinued brew' must NEVER appear in any of them (SC-002)."
