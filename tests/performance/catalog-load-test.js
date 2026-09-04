// SC-003 / SC-004 — the 300 ms boundary p95 for listing, search and detail at 100,000 active
// products under a sustained 200 requests/second (plan.md Performance Goals).
//
// Run:  k6 run -e BASE_URL=http://localhost:5000 tests/performance/catalog-load-test.js
// Seed: the catalogue must hold ~100,000 Active products across ~1,000 categories first;
//       SC-003 is only meaningful at that scale (spec Assumptions).

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const CATEGORY_ID = __ENV.CATEGORY_ID;
const PRODUCT_ID = __ENV.PRODUCT_ID;

export const options = {
  scenarios: {
    sustained: {
      executor: 'constant-arrival-rate',
      rate: 200,                       // SC-003: 200 requests/second
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
  },
  thresholds: {
    // 95% within 300 ms measured at the catalogue's own boundary. The remaining budget up to
    // 1 second of customer-perceived time belongs to the storefront feature (SC-003).
    'http_req_duration{endpoint:listing}': ['p(95)<300'],
    'http_req_duration{endpoint:search}':  ['p(95)<300'],
    'http_req_duration{endpoint:detail}':  ['p(95)<300'],
    'http_req_failed': ['rate<0.01'],
  },
};

const KEYWORDS = ['ca phe', 'tra', 'sua', 'CÀ PHÊ', 'da'];

export default function () {
  const listing = http.get(
    `${BASE_URL}/catalog/categories/${CATEGORY_ID}/products?page=1&pageSize=24`,
    { tags: { endpoint: 'listing' } });
  check(listing, { 'listing 200': (r) => r.status === 200 });

  const keyword = KEYWORDS[Math.floor(Math.random() * KEYWORDS.length)];
  const search = http.get(
    `${BASE_URL}/catalog/products/search?q=${encodeURIComponent(keyword)}`,
    { tags: { endpoint: 'search' } });
  check(search, { 'search 200': (r) => r.status === 200 });

  const detail = http.get(
    `${BASE_URL}/catalog/products/${PRODUCT_ID}`,
    { tags: { endpoint: 'detail' } });
  check(detail, { 'detail 200': (r) => r.status === 200 });
}
