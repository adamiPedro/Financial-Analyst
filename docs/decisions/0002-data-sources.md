# 0002. CVM open data for fundamentals, B3 historical quote files for prices

**Status:** Proposed — DRAFT, needs Pedro's edit before commit
**Date:** 2026-09-25
**Supersedes:** the 2026-09-21 draft, which chose Twelve Data and Alpha Vantage
over SEC EDGAR

## Context

The project's purpose is to demonstrate to Brazilian financial institutions that
Pedro can build finance-domain software. The original plan used SEC EDGAR for
fundamentals and a commercial provider for prices.

Two problems with that pairing. Commercially, the free tiers are unreliable:
Alpha Vantage cut its free allowance to 25 requests/day, which could not cover
even a fifteen-company universe, and any replacement can do the same thing
mid-project. More importantly, SEC data says nothing to the intended audience.
Parsing US filings is a generic exercise; the people reading this repo work with
Brazilian filings.

## Decision

Fundamentals come from the [CVM open data portal](https://dados.cvm.gov.br/) —
the DFP (annual) and ITR (quarterly) datasets, plus CAD for the company registry.
Prices come from B3's own
[historical quote files](https://www.b3.com.br/en_us/market-data-and-indices/data-services/market-data/historical-data/equities/historical-quotes/)
(COTAHIST).

Both are bulk downloads of public files rather than REST APIs: yearly ZIP
archives, refreshed weekly, no authentication and no rate limits.

No commercial market-data provider is used in v1.

## Consequences

**Good**
- The audience recognises the data. Handling DFP and ITR filings is evidence of
  domain knowledge; parsing SEC XBRL is not.
- No API keys, no quotas, no free tier that can be withdrawn. A clean clone runs
  with no credentials at all.
- **CVM mandates a standardized chart of accounts.** Revenue is account `3.01`
  for every company, so the tag-fragmentation problem that dominates SEC XBRL
  work — revenue spread across `Revenues`,
  `RevenueFromContractWithCustomerExcludingAssessedTax`, `SalesRevenueNet`,
  varying by company and year — largely does not exist here. This was the single
  largest schedule risk in the plan and the switch removes most of it.
- Restatements are first-class: the filings carry a version field, so "never
  silently overwrite a revised value" is supported by the source format rather
  than inferred.
- Units are explicit via a currency-scale field instead of being guessed.

**Bad**
- Less legible to interviewers outside Brazil. The README has to explain in a
  sentence what CVM and DFP are, and a reader who does not know the market will
  under-rate the work.
- The easier normalization is a weaker showcase. Solving SEC tag fragmentation is
  a harder problem and therefore a more impressive one; that is given up
  deliberately in exchange for relevance and schedule.
- Bulk-file ETL instead of incremental per-company sync: download, unzip, stream
  a large CSV, upsert. A different shape of work, and it means no cheap way to
  refresh one company.
- Locale friction is real: semicolon-delimited, Windows-1252 encoded, decimal
  comma. Mechanical, but it will produce at least one silent wrong number before
  it is handled properly.
- Banks and other financial institutions use a different account structure, so
  `3.01` does not mean for Itaú what it means for Vale. Including them is a
  deliberate choice; see the seed universe in the README.

## Alternatives considered

**SEC EDGAR/XBRL** — rejected on audience relevance, despite better English
documentation, a genuine REST API, and a much larger body of example code to
learn from.

**brapi.dev** — a Brazilian aggregator over B3, CVM and Banco Central with a free
tier. Rejected as primary: it is a third-party wrapper, and building on it would
replace the data-engineering work that is the point of the project. Worth
considering later as a convenience adapter for quotes.

**Banco Central SGS for macro context** (SELIC, IPCA) — deferred. Genuinely
interesting for interpreting anomalies, but it is a third source before the first
two work.

**Keeping a commercial provider as a second adapter** — deferred to post-v1. The
provider abstraction still gets built, but it will have one implementation for a
while, which is a documented compromise with the "no abstraction with a single
implementation" rule rather than an oversight.
