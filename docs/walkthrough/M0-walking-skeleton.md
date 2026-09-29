# M0 — Walking skeleton

**Status:** in progress. Domain model and persistence done; fixture and endpoint
still to come.

## What got built (so far)

- `Cnpj` — value object for the Brazilian corporate tax registration
- `Company` — the entity, keyed on CVM's own registration code
- `AccountingBasis` — consolidated vs individual, the distinction CVM forces
- `FinancialIntelligenceDbContext` + `CompanyConfiguration`
- `AddInfrastructure()` — Infrastructure registers its own services

## How the data source works

CVM does not publish an API. It publishes yearly ZIP archives at
`dados.cvm.gov.br/dados/CIA_ABERTA/DOC/DFP/DADOS/dfp_cia_aberta_YYYY.zip`,
refreshed weekly to pick up resubmissions. Inside each archive are per-statement
CSVs: `DRE` (income statement), `BPA` and `BPP` (balance sheet assets and
liabilities), `DFC` (cash flow) — each in a `_con_` and an `_ind_` variant.

Every row carries `CNPJ_CIA`, `CD_CVM`, `DT_REFER`, `VERSAO`, `ESCALA_MOEDA`,
`ORDEM_EXERC`, `CD_CONTA`, `DS_CONTA` and `VL_CONTA`.

Four things about that shape matter more than anything else in this milestone.

**`CD_CONTA` is a standardized account code.** Revenue is `3.01` for every
company. Gross result `3.03`, EBIT `3.05`, net income `3.11`. CVM mandates the
chart of accounts, so turning each company's rows into a common set of metrics is
mostly a lookup table rather than per-company detective work.

The exception, and it's a real one: banks and insurers use a different structure.
`3.01` does not mean for Itaú what it means for Vale. Since the seed universe
includes two banks on purpose, that's ADR 0009's problem and it is the hardest
remaining piece of M1.

**`ORDEM_EXERC` will double-count everything if ignored.** Each file contains the
current period *and* the prior-period comparative, marked `ÚLTIMO` and
`PENÚLTIMO`. A naive parse ingests both and every period appears twice with
slightly different values, because the comparative was restated. This is the trap
most likely to produce silently wrong numbers, so it's a project-wide
non-negotiable rather than a single code comment.

**`ESCALA_MOEDA` is the units problem, stated explicitly.** Values are published
in units or thousands. Storing a figure without applying its scale is wrong by
1000x — large enough to be obvious in a chart, small enough to survive a test
suite that only checks the pipeline ran.

**`VERSAO` gives restatements for free.** When a company resubmits a filing, the
new document carries a higher version. Supersession doesn't have to be inferred
from filing dates; the source format says it directly, which is why "a revised value never silently overwrites the
original" is a cheap promise to keep rather than an expensive one.

## Why CNPJ is a type and not a string

CVM publishes CNPJ punctuated — `33.000.167/0001-01`. Almost everything else,
including B3 files and anything hand-entered, uses the bare fourteen digits.
Held as a `string`, equality silently fails between two representations of the
same company.

`Cnpj.Parse` strips non-digits and validates the mod-11 check digits. The
validation is the part worth arguing for: a transcription error in a hand-curated
seed list then fails at parse time with a clear message, instead of producing an
empty result set three layers away that looks like "this company filed nothing."

One detail the check digits alone don't cover: `00000000000000` genuinely passes
mod-11, because every weighted sum is zero. So repeated-digit strings are
rejected explicitly. They turn up in placeholder rows and test data.

`Cnpj` is a `sealed record` — a class, not a `record struct`. A struct would have
a `default` value whose inner string is `null`: an instance that bypassed every
rule in the type. With a reference type, `null` is the only invalid state, and
nullable reference types already track that.

## Why CD_CVM is the primary key

Full reasoning in ADR 0007. The short version: this system covers one
jurisdiction by decision, CD_CVM is on every row of every filing CVM publishes,
and it is stable per registrant. So ingestion can write a fact without first
resolving an identifier to an internal id — no lookup, no cache, no resolution
step in the hottest path.

The standard case for a surrogate key is that binding identity to one
regulator's numbering is expensive to undo. That's true, but with no second
source of companies in prospect, a surrogate buys optionality nobody would
exercise.

The cost is stated honestly in the ADR: if a Brazilian-only scope ever changes,
every foreign key changes with it.

**CNPJ is deliberately not the identity.** It identifies a legal entity rather
than a registrant. It can change under restructuring, and subsidiaries have their
own — so two CNPJs can describe what the market treats as one company. It's
carried as data with a unique index.

## The .NET mechanics

**`ValueGeneratedNever()` is the line that matters most in the whole mapping.**
EF's convention for an integer primary key is a database-generated identity
column. CD_CVM is assigned by CVM; we only record it. Without that call, Postgres
generates its own value, the real registration code is discarded, and nothing
errors — an auto-generated integer key looks perfectly valid. It's the kind of
bug you find weeks later when a join returns nothing.

**`IEntityTypeConfiguration<T>`.** Mapping lives in `CompanyConfiguration`, not
as attributes on `Company`. That's what lets the Domain project reference nothing
at all — no `[Table]`, no `[MaxLength]`, no EF assembly.
`ApplyConfigurationsFromAssembly` discovers every configuration by reflection at
model-build time, so adding an entity doesn't mean remembering to register it.

**Value converters.** `Cnpj` reaches Postgres as a string via `HasConversion`:
`cnpj => cnpj.Value` out, `Cnpj.Parse(value)` back. A converted property normally
also wants a `ValueComparer` so EF can detect changes — but only for mutable
types. EF compares against the converted string, and `Cnpj` is immutable, so
there's no way to mutate one behind EF's back the way you could with a `List<T>`
property. The absence of a comparer is a decision, not an oversight.

**Private setters and the parameterless constructor.** EF materializes entities
through `private Company()` and then writes each mapped property by reflection,
private setter or not. This is the nearest EF equivalent to Unity deserializing a
MonoBehaviour: the object exists before any of its fields are populated, so the
constructor can't assume they're valid. Hence the `null!` assignments — they
satisfy nullable analysis for that window, and application code never observes
null because EF always follows up.

**Nullable unique index.** `Ticker` is uniquely indexed *and* nullable, which
works because Postgres permits multiple NULLs in a unique index. That's exactly
the behaviour wanted: tickers must not collide, but most companies have none
until B3 data loads. Worth knowing this is not universal — SQL Server needs a
filtered index to get the same result.

## Two modelling consequences of the data source

**CVM publishes no tickers.** It identifies companies by CNPJ and CD_CVM only.
Tickers come from B3 or from the curated seed list, which means `Company.Ticker`
has to be nullable — a company legitimately exists in this system before its
ticker is known. That surprised me, and it's a good example of the data source
shaping the model rather than the reverse.

**CVM does publish a sector.** The company registry (CAD) carries a
sector-of-activity classification, so `Company.Sector` comes from the same source
as everything else rather than needing a second data provider.

## Interview answers

**Why a value object for CNPJ but not for the ticker?** CNPJ has two competing
external representations and a checksum, and getting it wrong fails silently.
A B3 ticker's only rule is "uppercase four letters plus a digit", which a
constructor guard covers. Value objects pay where there's a normalization rule
worth centralizing; below that they're ceremony.

**Why validate check digits at all — isn't CVM data already valid?** CVM's data
is. The seed list isn't: it's hand-typed, and a wrong digit there produces a
company that matches no filings. Validation moves that failure from "mysterious
empty result" to "throws at startup with the bad value in the message."

**Why is the accounting basis on every fact rather than a global setting?**
Because it isn't global. Consolidated is the default, but a company with no
consolidated statements falls back to individual, so the basis varies per company
and metric. Storing it on every fact keeps that visible in the data, and because a
served series is resolved to one basis for its whole length (ADR 0008), analytics
never see a series that changes basis partway through.

**How do you avoid double-counting from ORDEM_EXERC?** Filter to `ÚLTIMO` on
ingest and treat the prior-period rows as what they are: a restated comparative
that belongs to the earlier filing, not new data. The restated values are still
interesting — they're evidence of a restatement — but they're not a second
observation of the same period.

## What I'd change at 10,000 companies

CVM publishes roughly 700 open companies, so 10,000 isn't reachable within this
data source — the honest scaling answer is about years and statements, not
companies.

The real pressure point is the bulk-file model. Every ingest downloads and parses
a whole year for every company, so refreshing one company's figures means
reprocessing the entire archive. At current scale that's acceptable. To do better
I'd track a per-file content hash and skip archives that haven't changed since
the last run, then stream rows straight into a staging table with `COPY` rather
than materializing entities — EF's change tracker is the wrong tool for a
million-row insert, and the right one is bulk-load-then-merge.

The ticker unique index would also have to go. Delistings free tickers for reuse,
and across the full B3 history that constraint becomes wrong.
