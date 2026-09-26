# 0007. Use CD_CVM as Company's primary key

**Status:** Proposed — DRAFT, needs Pedro's edit before commit
**Date:** 2026-09-25
**Supersedes:** the 2026-09-22 draft of this ADR, which assumed SEC data

## Context

`Company` needs a primary key. The candidates, given CVM as the data source:

- **CD_CVM** — the registration code CVM assigns to every open company. Numeric,
  present on every row of every filing CVM publishes.
- **CNPJ** — the Receita Federal registration of the legal entity. Also present
  on every row.
- A database-generated surrogate `int`.

An earlier draft of this ADR chose a surrogate key over the SEC's CIK. The
argument was optionality: a non-US market would have no CIK, so binding identity
to one jurisdiction's identifier would be expensive to undo.

That argument is now spent. The project covers CVM-registered Brazilian
companies only, deliberately and for the whole of v1. With no second jurisdiction
in prospect, a surrogate key buys optionality nobody will exercise and charges a
CIK-to-Id resolution step on every fact written.

## Decision

`Company.CvmCode` is the primary key: an `int`, assigned by CVM, never generated
by this system. `Cnpj` is carried as data with its own unique index.

CNPJ is explicitly *not* the identity. It describes a legal entity rather than a
registrant — it can change under corporate restructuring, and subsidiaries have
their own — so two CNPJs can describe what the market treats as one company.

The EF configuration must call `ValueGeneratedNever()`. Left to convention, EF
treats an integer key as an identity column, Postgres generates a value, and the
real CD_CVM is silently discarded.

Two guards turn the risks listed below from accepted into enforced:

- A unit test asserts that the `Company` key is not value-generated. That failure
  mode is silent, so it needs a test rather than a comment.
- Ingestion validates every CD_CVM against the CAD registration file before it
  writes facts, and rejects an unknown code instead of inserting it. Caveat to
  settle at M1: CAD describes *currently* registered companies, so a company that
  has since deregistered may be missing from it while its historical filings are
  not. The rule may need to be "present in CAD, or explicitly allow-listed".

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
- Identity is bound to CVM's numbering. Adding a non-Brazilian market means
  every foreign key changes, not one nullable column. That is the cost of the
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
moment a second jurisdiction appears, and the migration path is: add a surrogate,
repoint foreign keys, keep CD_CVM as the natural key. No second jurisdiction is
planned — v1 covers the Brazilian market only and SEC data is not on the roadmap —
so deciding against a change that has been ruled out would mean paying for it
twice.

**CNPJ as the key** — rejected for the reasons above: it identifies a legal
entity, not a registrant, and it is less stable than CD_CVM.

**Composite key of (CD_CVM, CNPJ)** — rejected. Two identifiers for one thing,
wider foreign keys, and no question it answers that a unique index does not.
