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

A company can also change basis partway through its history — it acquires its
first subsidiary and starts consolidating. That case is what the decision below
is really about, because it is the only one where the two bases compete for the
same series.

## Decision

Both bases are ingested and stored. Every stored fact carries its
`AccountingBasis`, so the answer to "which basis is this?" is always in the data
rather than in a convention someone has to remember.

Resolution happens when a series is served, per company and per metric:

1. Prefer consolidated.
2. Fall back to individual only when the company has no consolidated data for
   that metric at all.
3. Once a basis is chosen, the whole series is served on it. Periods missing from
   the chosen basis are reported as unavailable. They are never filled from the
   other basis.
4. The resolved basis is returned with every series, in API responses and in AI
   tool results, and stated in any cross-company output.
5. Where the unchosen basis does hold data for periods the series reports as
   unavailable, the response says so as a count. It does not return the figures.

Point 5 exists so that a gap is never mistaken for an absence of filings. It is a
single nullable field, not a second series.

One basis per series, always. A metric is never computed across mixed bases for
one company.

## Consequences

**Good**
- Matches what analysts actually use. Consolidated is the default in every
  Brazilian equity research note.
- The universe stays complete: companies without subsidiaries are included rather
  than silently dropped.
- Provenance is honest. Every number can state its basis, which is exactly the
  kind of question a reader at a financial institution would ask first.
- Analytics and anomaly detection never have to know that basis exists. A rolling
  window cannot straddle a basis change, because a served series only ever has
  one basis. That removes a whole class of false positive — a basis change has
  the same shape as the step the anomaly engine is built to find.
- The response shape stays flat: one basis, one array of points.

**Bad**
- Every fact carries an extra discriminating column, and every query that does
  not filter on it is a latent bug.
- Coverage is lost where consolidated exists only for later years. The series
  resolves to consolidated and the early periods read as unavailable while usable
  individual rows sit in the table. This is the deliberate cost, and point 5 is
  what keeps it from being a silent one.
- Resolution is per metric, so two metrics for the same company can come back on
  different bases. Defensible, but surprising unless the basis is surfaced
  everywhere — which is why point 4 is not optional.
- A company that files both, where we take consolidated, means the individual
  figures are ingested and never served. Storage is trivial and re-ingesting is
  not, so they are kept.

## Alternatives considered

**Segment the series at the basis change** — return the consolidated stretch and
the individual stretch as separate labelled segments rather than one trend. This
was the earlier draft of this ADR, and it is the more truthful option: it serves
every figure held rather than reporting some as unavailable.

Rejected on complexity that spreads. It changes the response shape from one
series to a list of segments, and that shape reaches DTOs, the static page and
the AI tools, where a model then has to reason about segment boundaries. Worse,
every rolling-window computation in analytics has to test whether the window
straddles a boundary, and that is exactly the check that gets omitted once and
then produces the false positive this ADR exists to prevent.

It also buys coverage for a case the seed universe may not contain. A basis
change requires a first acquisition, and the universe is 12 large filers that
have consolidated for decades. **Verify this against the real data before
treating the decision as settled** — if a switch is genuinely present, promote
segments on that evidence rather than on anticipation.

**Consolidated only** — rejected. Simpler, but it drops companies from the
universe for a reason that has nothing to do with their financials.

**Individual only** — rejected. It is the legally primary statement but not what
anyone analyses, and for a holding company it is close to meaningless.

**Both, with no default** — rejected. Every query and every metric would have to
specify a basis, which pushes the decision onto every caller and guarantees
inconsistency. A documented default with an explicit override is the same
flexibility with a safe fallback.

**Filling gaps in the chosen basis from the other one** — rejected outright. It
is the splice this ADR is written to forbid, and it would put a step change in a
series and present it as continuous.
