# 0008. Prefer consolidated statements, fall back to individual

**Status:** Proposed — DRAFT, needs Pedro's edit before commit
**Date:** 2026-09-25

## Context

CVM publishes every financial statement twice. The DFP and ITR archives contain
`_con_` files (consolidated — the parent plus its subsidiaries) and `_ind_` files
(individual — the parent company alone). Both are legally required, both are
correct, and for a company with subsidiaries they report materially different
numbers for the same period.

This is not a technical detail. "Petrobras revenue for 2024" has two different
right answers depending on the basis, and a system that mixes them produces
figures that are wrong in a way no test would catch.

Not every company files both. A company with nothing to consolidate files only
individual statements, so a consolidated-only rule would silently exclude part of
the universe.

## Decision

Consolidated is the default basis everywhere: ingestion, metrics, anomaly
baselines and API responses.

Where a company has no consolidated statements for a period, the individual
statements are ingested instead and the fact is tagged with the basis that
produced it. Every stored fact carries its `AccountingBasis`, so the answer to
"which basis is this?" is always in the data rather than in a convention someone
has to remember.

A metric is never computed across mixed bases for one company. If a company's
history switches basis mid-series — it acquired its first subsidiary and started
consolidating — the series is reported as two segments rather than joined into
one misleading trend.

## Consequences

**Good**
- Matches what analysts actually use. Consolidated is the default in every
  Brazilian equity research note.
- The universe stays complete: companies without subsidiaries are included rather
  than silently dropped.
- Provenance is honest. Every number can state its basis, which is exactly the
  kind of question a reader at a financial institution would ask first.

**Bad**
- Every fact carries an extra discriminating column, and every query that does
  not filter on it is a latent bug.
- The mixed-history case needs real handling — detecting a basis change and
  refusing to join the series is more work than computing growth blindly.
- A company that files both, where we take consolidated, means the individual
  figures are ingested and never used, or not ingested and unavailable later.
  Currently the former: storage is trivial and re-ingesting is not.

## Alternatives considered

**Consolidated only** — rejected. Simpler, but it drops companies from the
universe for a reason that has nothing to do with their financials.

**Individual only** — rejected. It is the legally primary statement but not what
anyone analyses, and for a holding company it is close to meaningless.

**Both, with no default** — rejected. Every query and every metric would have to
specify a basis, which pushes the decision onto every caller and guarantees
inconsistency. A documented default with an explicit override is the same
flexibility with a safe fallback.
