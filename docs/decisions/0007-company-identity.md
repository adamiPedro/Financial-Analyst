# 0007. Use CD_CVM as Company's primary key

**Status:** Proposed — DRAFT, needs Pedro's edit before commit
**Date:** 2026-09-25

## Context

`Company` needs a primary key. The candidates, given CVM as the data source:

- **CD_CVM** — the registration code CVM assigns to every open company. Numeric,
  present on every row of every filing CVM publishes.
- **CNPJ** — the Receita Federal registration of the legal entity. Also present
  on every row.
- A database-generated surrogate `int`.

The usual argument for a surrogate key is optionality: binding identity to one
regulator's numbering is expensive to undo if a second data source with its own
identifiers ever arrives.

That argument does not apply here. The project covers CVM-registered Brazilian
companies only, by design. With no second source of companies in prospect, a
surrogate key buys optionality nobody will exercise and charges a
CD_CVM-to-Id resolution step on every fact written.

## Decision

`Company.CvmCode` is the primary key: an `int`, assigned by CVM, never generated
by this system. `Cnpj` is carried as data with its own index, which is
deliberately *not* unique.

CNPJ is explicitly *not* the identity. It describes a legal entity rather than a
registrant — it can change under corporate restructuring, and subsidiaries have
their own — so two CNPJs can describe what the market treats as one company.

The reverse also happens: one CNPJ under two CD_CVM codes. When a company
re-registers, CVM issues a new code and keeps the old one as cancelled. The CAD
file checked on 29 Sep 2026 has 34 such CNPJs (Equatorial Goiás, for example, is
both 2445 and 25577). A unique CNPJ index would reject real data, so it was
dropped in the `MakeCnpjIndexNonUnique` migration.

The EF configuration must call `ValueGeneratedNever()`. Left to convention, EF
treats an integer key as an identity column, Postgres generates a value, and the
real CD_CVM is silently discarded.

Two guards turn the risks listed below from accepted into enforced:

- A unit test asserts that the `Company` key is not value-generated. That failure
  mode is silent, so it needs a test rather than a comment.
- Ingestion validates every CD_CVM against the CAD registration file before it
  writes facts, and rejects an unknown code instead of inserting it. An earlier
  worry was that CAD might list only current registrations, leaving the history
  of deregistered companies unimportable. The real file settles it: cancelled
  companies are included (`SIT = CANCELADA`), so no allow-list is needed.

## Consequences

**Good**
- Ingestion writes facts directly. CD_CVM is on every CVM row, so there is no
  lookup, no cache, and no resolution step in the hottest path. Stated honestly,
  the surrogate alternative would cost one dictionary loaded per run rather than a
  query per row — so this is an argument about having one less moving part, not
  about throughput.
- A fact row is legible in `psql` without a join — the code identifies the
  company, and it is the same code CVM's own systems use.
- One identity concept instead of a key plus a natural key.
- Narrow: a 4-byte integer in every foreign key on the largest tables.

**Bad**
- Identity is bound to CVM's numbering. Adding companies from outside CVM would
  mean every foreign key changes, not one nullable column. That is the cost of the
  decision and it is accepted knowingly.
- A CD_CVM misparsed on first ingest cannot be corrected in one row; it means
  updating every fact that references it. The CAD validation above is what stops a
  bad code being written at all, which is far cheaper than any repair.
- `ValueGeneratedNever()` is a footgun for anyone adding an entity later, because
  the failure mode is silent rather than an error. The test above covers
  `Company`; a second entity keyed on an externally assigned number needs its
  own.

## Alternatives considered

**Surrogate int with CD_CVM unique** — rejected. It is the right answer the
moment a second source of companies appears, and the migration path is: add a
surrogate, repoint foreign keys, keep CD_CVM as the natural key. The project is
Brazil-only by design, so paying for that now would be paying for a change that
has been ruled out.

**CNPJ as the key** — rejected for the reasons above: it identifies a legal
entity, not a registrant, and it is less stable than CD_CVM.

**Composite key of (CD_CVM, CNPJ)** — rejected. Two identifiers for one thing,
wider foreign keys, and no question it answers that a unique index does not.
