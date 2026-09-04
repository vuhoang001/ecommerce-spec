# Feature Specification: Product Catalog

**Feature Branch**: `002-product-catalog` (not created — no git extension hook installed)

**Created**: 2026-09-04

**Status**: Draft — ready for planning

**Input**: User description: "Build a product catalog feature: customers browse products by
category, view product details (price, description, images, stock), search by name, and filter by
category and price range. A product can belong to multiple categories. Cart and checkout are out of
scope for this feature."

## Clarifications

### Session 2026-09-04

- Q: Should the catalogue keep a copy of every currently discounted product, or only of products a customer has already viewed? (FR-014) → A: Every currently discounted product, refreshed as discounts change.
- Q: Does the catalogue show prices in a single currency, or in more than one? (FR-026, FR-030) → A: A single currency, Vietnamese dong (VND), with no conversion anywhere.
- Q: How large does the catalogue need to be, and how much traffic must it carry, for the 1-second target in SC-003 to count as met? (SC-003) → A: ~100,000 active products at ~200 requests/second peak.
- Q: Can anyone browse the catalogue without signing in, or must a customer be signed in first? (FR-001, SC-002) → A: Fully public; no sign-in required, with rate limiting added because the surface is open.
- Q: What availability should the catalogue itself be held to, and how quickly must it recover from an outage? (SC-008) → A: 99.9% monthly, recovery within 15 minutes.
- Q: SC-001 counted actions from a homepage this feature does not own — restate, reassign, or keep? (SC-001) → A: Restate as catalogue-boundary reachability; the storefront feature owns click counts and perceived timing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse products by category (Priority: P1)

A customer opens a category and sees the products it contains, a page at a time, so they can survey
what is available and pick something worth looking at closely.

**Why this priority**: Category browsing is the entry path to every other story. Without it a
customer has no way to discover a product they cannot already name.

**Independent Test**: Load a category containing more products than one page holds, page through it,
and confirm every visible product is one a customer is allowed to see. Delivers value on its own —
a customer can find products without search, filters, or detail pages existing.

**Acceptance Scenarios**:

1. **Given** a category holding 30 active products and a page size of 24, **When** the customer
   selects that category, **Then** the first 24 are shown with the total count and the current page
   position stated.
2. **Given** an active product whose stock quantity is zero, **When** the customer views its
   category, **Then** the product is listed and labelled "Out of stock".
3. **Given** a product assigned to both "Coffee" and "Gifts", **When** the customer views either
   category, **Then** the product appears exactly once in that category's listing.
4. **Given** a category holding no active products, **When** the customer selects it, **Then** an
   empty result is shown with the stated reason "no products in this category", not an error.

---

### User Story 2 - View product detail (Priority: P1)

A customer opens one product and sees everything needed to decide: what it is, what it costs now,
what it looks like, whether it is in stock, and where it sits in the catalogue.

**Why this priority**: This is where the buying decision is made. Browsing without detail leaves the
customer unable to act on what they found.

**Independent Test**: Open one product with images, a description, stock, several categories, and an
active discount, and confirm every element is present and the two prices are distinguishable.

**Acceptance Scenarios**:

1. **Given** an active product, **When** the customer opens its detail view, **Then** name,
   description, current price, image gallery, stock quantity, and every category it belongs to are
   shown.
2. **Given** the Promotion feature reports an active discount for the product, **When** the detail
   view is shown, **Then** the discounted price is displayed and the original price is displayed
   marked as superseded.
3. **Given** a product with zero stock, **When** the customer opens its detail view, **Then** all
   information is shown and the product is labelled "Out of stock".
4. **Given** a product that is hidden or discontinued, **When** a customer requests it directly,
   **Then** the system reports it as not found and does not disclose that it exists.

---

### User Story 3 - Search by name (Priority: P2)

A customer who already knows roughly what they want types part of a product name and gets the
matching products without navigating the category tree.

**Why this priority**: A shortcut past browsing rather than a replacement for it. Valuable, but a
customer can still reach every product through categories without it.

**Independent Test**: Search a partial name in several letter cases and with and without diacritics,
and confirm the same product set comes back each time.

**Acceptance Scenarios**:

1. **Given** a product named "Cà phê sữa đá", **When** the customer searches "ca phe", **Then** the
   product is returned.
2. **Given** the same product, **When** the customer searches "CÀ PHÊ", **Then** the product is
   returned.
3. **Given** a hidden or discontinued product whose name matches the keyword, **When** the customer
   searches, **Then** it is absent from the results.
4. **Given** an empty or whitespace-only keyword, **When** the customer searches, **Then** a stated
   validation error is returned, not the whole catalogue.

---

### User Story 4 - Filter by category and price range (Priority: P2)

A customer narrows a listing to a category, a price band, or both, so the results match what they
are willing to spend.

**Why this priority**: Refines a result set the earlier stories already produce. Useful once the
catalogue is large enough that a category alone is too broad.

**Independent Test**: Apply a category alone, a price range alone, and both together, and confirm
each result set satisfies every filter applied.

**Acceptance Scenarios**:

1. **Given** a category and a price range of 50,000–200,000, **When** both filters are applied,
   **Then** every returned product is in that category and priced within those bounds inclusive.
2. **Given** a minimum of 200,000 and a maximum of 50,000, **When** the filter is applied, **Then**
   a stated error identifies the minimum as greater than the maximum, and no empty result list is
   returned in its place.
3. **Given** a maximum with no minimum, **When** the filter is applied, **Then** the range is
   treated as unbounded below.
4. **Given** a negative price bound, **When** the filter is applied, **Then** a stated validation
   error is returned.

---

### Edge Cases

- A product is assigned to no category at all — it is unreachable by browsing but MUST still be
  reachable by search and by direct detail view.
- A product's category is removed while a customer is paging through that category.
- A product changes from active to hidden between the listing and the customer opening its detail.
- Two products share the same name; search MUST return both, distinguished by their other details.
- The keyword matches every product in the catalogue — pagination MUST still bound the response.
- The customer requests a page number beyond the last page — an empty page with the stated total,
  not an error.
- A product has no images — the detail view MUST render without an image gallery rather than fail.
- Stock quantity changes from positive to zero while the detail view is open.
- The Promotion feature reports a discount larger than the product's price.
- The Promotion feature is unreachable or slow when a listing or detail view is requested.
- The Promotion feature is unreachable and the catalog holds no discount copy for the product —
  nothing stale exists to fall back to.
- The catalog starts up cold with discounts already active, and must seed its copy before a price
  range filter can be answered correctly.
- A caller pages through the whole catalogue at machine speed — legitimate deep browsing and
  scraping look identical, and the rate limit is what separates them.
- A caller hits the rate limit partway through paging a category, and must be told why rather than
  shown a short page.
- A discount copy is shown while, unknown to the catalog, the promotion behind it has
  already expired.
- A product is priced 250,000 and discounted to 180,000, and the customer filters 200,000-300,000 —
  it matches on original price alone and must show both prices.
- A price range matches a product's original price and a second range matches its discounted price,
  so the product legitimately appears in two adjacent price bands.

## Requirements *(mandatory)*

### Functional Requirements

**Visibility**

- **FR-001**: System MUST show a product to customers only while its status is Active, in every
  listing, search result, filter result, and detail view.
- **FR-002**: System MUST report a direct request for a non-Active product as not found, without
  disclosing that the product exists.

**Browse by category**

- **FR-003**: System MUST list the Active products of a selected category, a page at a time.
- **FR-004**: Each listed product MUST show its name, its primary image, and its current price.
- **FR-005**: System MUST label a product whose stock quantity is zero as "Out of stock" and MUST
  keep it listed and viewable.
- **FR-006**: A product belonging to several categories MUST appear in each of those categories'
  listings, and exactly once within any one listing.
- **FR-007**: Every paged result MUST state the total number of matching products and which page is
  being shown.
- **FR-008**: A category with no Active products MUST return an empty result carrying the reason,
  not an error.

**Product detail**

- **FR-009**: The detail view MUST show name, description, current price, image gallery, stock
  quantity, and every category the product belongs to.
- **FR-010**: When the Promotion feature reports an active discount, the detail view MUST show the
  discounted price together with the original price marked as superseded.
- **FR-011**: Catalog MUST NOT calculate any discount. It displays the discount result the Promotion
  feature supplies (constitution PRM-001).
- **FR-012**: When the Promotion feature returns a rejection reason instead of a discount, the
  catalog MUST show the undiscounted price and MUST record the reason in the log without showing it
  to the customer.
- **FR-013**: When the Promotion feature cannot be reached, the catalog MUST show the discount copy
  it holds for that product, marked as possibly out of date, and MUST record the failure in the log.
- **FR-014**: The catalog MUST maintain its own copy of the currently active discount for every
  discounted product — not only for products a customer has viewed — together with the moment each
  copy was received. This copy serves both the price range filter (FR-026) and the
  unreachable-Promotion fallback (FR-013).
- **FR-015**: A discount copy older than the staleness limit MUST NOT be shown and MUST NOT be used
  by the price range filter. In that case, and when no copy is held for the product, the catalog
  MUST show the undiscounted price marked as possibly out of date.
- **FR-016**: System MUST NOT display a discounted price below zero.

**Search**

- **FR-017**: System MUST match a keyword against product names as a partial match, ignoring letter
  case and ignoring diacritics in both directions — a keyword without diacritics MUST match a name
  with them, and the reverse.
- **FR-018**: Search MUST return only Active products.
- **FR-019**: An empty or whitespace-only keyword MUST return a stated validation error rather than
  the full catalogue.
- **FR-020**: Search results MUST be paged and MUST state the total and page position as in FR-007.

**Filter**

- **FR-021**: The category filter and the price range filter MUST be combinable; a returned product
  MUST satisfy every filter applied.
- **FR-022**: A price range whose minimum exceeds its maximum MUST return a stated error naming the
  problem. Returning an empty result in its place is FORBIDDEN.
- **FR-023**: Price range bounds MUST be inclusive.
- **FR-024**: An omitted minimum or maximum MUST be treated as unbounded on that side.
- **FR-025**: A negative price bound MUST return a stated validation error.
- **FR-026**: A product MUST match a price range when its original price falls within the bounds,
  when its current discounted price falls within the bounds, or when both do. A product matching on
  either price MUST be returned exactly once.
- **FR-027**: When no discounted price is known for a product — no promotion applies, or the
  Promotion feature is unreachable and no copy is held — the price range MUST be evaluated against
  its original price alone.
- **FR-028**: A product returned because only its discounted price matched the range MUST display
  both prices, so the customer can see why it appeared.

**Cross-cutting**

- **FR-029**: Every rejected request MUST carry a reason the customer can act on. Returning an empty
  result in place of an error is FORBIDDEN.
- **FR-030**: Every monetary amount shown MUST be exact, with no rounding drift between the amount
  stored, the amount compared against a filter, and the amount displayed (constitution MON-001).
- **FR-031**: The discount copy MUST be updated when a discount starts, changes, or ends, and MUST
  be seeded on start-up so a product discounted before the catalog began running is present. A
  discount that has ended MUST be removed, after which the product matches on its original price
  alone (FR-027).
- **FR-032**: Every price in the catalogue MUST be expressed in Vietnamese dong (VND). Price range
  bounds MUST be interpreted in that same currency, and the system MUST NOT convert between
  currencies anywhere.
- **FR-033**: Every monetary amount MUST be a whole number of dong. Displaying or comparing a
  fractional amount is FORBIDDEN, which is what makes FR-030's no-drift guarantee achievable.
- **FR-034**: Browsing, searching, filtering, and opening a product MUST NOT require the customer to
  sign in. No read path in this feature depends on a customer identity.
- **FR-035**: Every read path MUST be rate limited per caller. A caller exceeding the limit MUST
  receive a stated rejection carrying the reason and when to retry, never a silently empty or
  truncated result (FR-029).
- **FR-036**: The catalogue MUST keep serving reads when a single instance fails. No read path may
  depend on one instance being alive.
- **FR-037**: Rate limiting MUST NOT weaken FR-001. A caller who exceeds the limit is refused; no
  request is ever answered with a non-Active product because a check was skipped under load.

### Key Entities *(include if feature involves data)*

- **Product**: A purchasable item. Carries a name, a description, a current price in whole dong, a
  stock quantity, a status, an ordered set of images, and membership in zero or more categories.
- **Category**: A named grouping a customer browses by. Holds many products; a product may belong to
  many categories.
- **Product Status**: The visibility state of a product. Only Active products are visible to
  customers; Hidden and Discontinued products are not, and are indistinguishable from non-existent.
- **Product Image**: One picture of a product, with a position in the gallery. Exactly one image per
  product is the primary image used in listings.
- **Stock Quantity**: The count of units currently available. Governs the "Out of stock" label only;
  this feature never changes it.
- **Discount Result**: What the Promotion feature returns for a product — either a discounted price
  or a reason no discount applies. Owned by Promotion; read, never written, by Catalog.
- **Discount Copy** (formerly referred to as "Retained Discount Result"): Catalog's own copy of the
  currently active discount for a product and the moment it arrived. Held for every discounted
  product, whether or not anyone has viewed it. Serves the price range filter and the
  unreachable-Promotion fallback. Never authoritative — Promotion remains the owner.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Active products are reachable in at most two catalogue requests — one category
  listing or one search that returns the product, followed by one detail view. No Active product is
  reachable only by knowing its identifier in advance.
- **SC-002**: Across the full catalogue, zero Hidden or Discontinued products appear in any listing,
  search result, or filter result.
- **SC-003**: 95% of category listings and searches return their first page within 300 ms measured at
  the catalogue's own boundary, against 100,000 active products under a sustained 200 requests per
  second. The remaining budget up to 1 second of customer-perceived time belongs to whichever feature
  owns the storefront interface.
- **SC-004**: A customer searching a product name they already know finds it on the first page of
  results in at least 90% of attempts, measured at the same catalogue size as SC-003.
- **SC-005**: 100% of rejected searches and filters show a reason. Zero rejections are answered with
  a silently empty result.
- **SC-006**: 100% of discounted prices shown match what the Promotion feature returned for that
  product, with zero discrepancies, except where marked as possibly out of date.
- **SC-007**: 100% of out-of-stock Active products remain listed and openable, with zero disappearing
  from the catalogue because their stock reached zero.
- **SC-008**: When the Promotion feature is unreachable, 100% of product listings and detail views
  still render, and every price shown from a discount copy is marked as possibly out of date.
- **SC-009**: 100% of promotion rejections are recorded in the log with their reason, and zero are
  shown to the customer.
- **SC-010**: Every product returned by a price range satisfies that range on its original price, on
  its discounted price, or on both — zero products are returned matching on neither.
- **SC-011**: 100% of currently discounted products are represented in the discount copy within 1
  minute of the discount starting, changing, or ending. Zero discounted products are missing from a
  price range filter that their discounted price satisfies.
- **SC-012**: 100% of prices displayed, filtered, and compared are whole dong. Zero fractional or
  converted amounts appear anywhere in the catalogue.
- **SC-013**: 100% of catalogue read paths are reachable by an anonymous customer, with zero
  requiring a sign-in.
- **SC-014**: 100% of rate-limited rejections state the reason and when to retry. Zero are answered
  with a silently empty or truncated result.
- **SC-015**: The catalogue is available 99.9% of each calendar month — no more than roughly 43
  minutes of unavailability.
- **SC-016**: Service is restored within 15 minutes of an outage beginning, demonstrated by a
  recovery exercise rather than asserted.

## Out of Scope

- Cart, checkout, and order placement — the Order feature owns them. This feature never adds a
  product to a cart, and the "Out of stock" label carries no purchase behaviour of its own.
- Discount calculation — the Promotion feature owns it. This feature only displays a discount result
  it is given.
- Inventory receiving and stock adjustment. Stock quantity is read here, never changed.
- Creating, editing, categorising, and retiring products and categories. This feature is the
  customer-facing read path only; the authoring path is a separate feature.
- Sorting and ranking controls beyond the default order stated in Assumptions.
- Personalisation, recommendations, and recently-viewed history.

## Assumptions

- **Availability is 99.9% monthly with 15-minute recovery** (SC-015, SC-016). No requirement stated
  a target; this one suits a storefront where an outage costs browsing rather than money, since the
  feature has no write path and no order flow to lose. It implies redundant instances and health
  checks, but no standby region.
- **The catalogue is public** (FR-034). Nothing in the description or the user stories mentions a
  customer account, and requiring a sign-in would put every product behind a credential the
  description never mentions.
  Rate limiting (FR-035) follows from the surface being open, not from any stated requirement.
- **Target scale is 100,000 active products across roughly 1,000 categories, averaging 3 categories
  per product, at 200 requests per second peak** (SC-003). No requirement stated a size; this is the
  scale every performance criterion is measured against, and the one the design is sized for.
- **Page size is 24 products**, chosen as a common default; no requirement fixed it.
- **Default ordering** is newest first for category listings and closest-match first for search.
  No ordering was specified and none is customer-selectable in this feature.
- **Product status is one of Draft, Active, Hidden, Discontinued**, and only Active is visible to
  customers. The description named "active" and "hidden/discontinued" without defining the full set.
- **Search matches the product name only** — not description, not category name — as stated in US3.
- **Out-of-stock products appear everywhere**, not only in category listings. US1 stated it for
  browsing; applying it inconsistently elsewhere would be surprising.
- **Stock quantity is displayed as an exact number** on the detail view, as US2 requests, rather
  than as a band such as "low stock".
- **Reachability is measured at the catalogue boundary, not in a browser** (SC-001, SC-003). This
  feature exposes no interface a customer clicks; a storefront feature owns the homepage, the click
  count, and perceived timing. What this feature can guarantee is that every Active product is
  returned by some listing or search.
- **A product with no categories is valid** and reachable by search and direct link.
- **The staleness limit for a discount copy is 15 minutes** (FR-015). No requirement
  fixed it; beyond that the catalog shows the undiscounted price rather than a price it can no
  longer stand behind.
- **A promotion rejection is invisible to the customer but always logged** (FR-012). Constitution
  PRM-003 forbids skipping a promotion without a reason; it does not require showing that reason to
  a shopper, for whom "minimum order value not met" is noise on a catalogue page.
- **"Possibly out of date" is shown as a visible note beside the price**, not merely recorded.
- **The catalogue is single-currency (VND)** (FR-032). The description gave prices as 50,000-200,000
  and named a Vietnamese product without stating a currency; multi-currency support, if ever needed,
  is a separate feature with its own conversion and rounding rules.
- **Depends on the Promotion feature** to supply discount results per product. Catalog reads them
  through an interface it owns and never writes to Promotion (constitution COM-001, PRM-001).
- **Depends on the authoring path** — some other feature sets product status, prices, images, and
  category membership. Nothing in this feature creates catalogue data.
