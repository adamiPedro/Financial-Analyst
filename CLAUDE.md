# Financial Intelligence Platform

## What this is

A backend that ingests Brazilian public-company financial data (CVM open data:
DFP, ITR, CAD) and B3 historical quotes, normalizes it into a stable internal
model with full provenance, calculates deterministic analytics, detects
explainable anomalies, and exposes all of it to an AI research agent through
narrow read-only tools.

It is a portfolio project built to production-minded standards. It is not a
trading system, not investment advice, and not a CRUD dashboard.

**Who it is for:** Brazilian financial institutions. The project exists to show
that I can ship a complex, coherent product *and* that I understand the finance
side of the domain they operate in — which is why it uses CVM filings rather
than SEC ones. Fundamentals depth is demonstrated separately through coursework.

That division is deliberate, but it does not mean I can hand-wave this codebase.
Interviewers will ask about this project, because it is the impressive one. If I
cannot explain a design decision here, the shipping claim collapses with it.

## Who you're working with

I'm a C#/C++ game programmer moving deeper into backend engineering. C# itself
I know well — language features, async, generics, performance. What's new to me
is the .NET *backend* ecosystem: ASP.NET Core, EF Core, PostgreSQL, HTTP
resilience, background services, observability.

So: don't explain what a `Task` or an interface is. Do explain why EF Core
tracks entities the way it does, or what `AddHttpClient` is actually wiring up.

Analogies to Unity and game engine work are useful to me when they're accurate.
Don't force one if it doesn't fit.

---

## Working contract

### Build freely, explain as you go

Write any part of this system — domain model, normalization, analytics, anomaly
scoring, AI tooling, infrastructure. No area is off limits and you don't need
to stop and ask before implementing.

What you owe me in exchange is understanding. Every piece of work carries an
explanation, in two places, and they have different jobs.

### 1. Code comments — why, never what

A comment earns its place when a reader would otherwise ask *"why is this like
this?"* Specifically:

- A non-obvious decision and its reason
- A domain subtlety that isn't visible in the code (why Q4 is derived, why a
  `CD_CONTA` priority list exists at all, why this value is nullable)
- An invariant or constraint the type system can't express
- A trade-off deliberately accepted, and what it costs
- A footgun the next reader would otherwise step on

Do **not** write:

- Narration of what the line does — `// increment the counter`
- XML doc summaries that restate the method name
- Tutorial explanations of standard .NET mechanics
- A comment on every method because every method has one

Tutorial-style explanation in source is the specific thing to avoid. A repo
densely commented with teaching prose reads as AI-written student code, which
is the opposite of the signal this project exists to send. Teaching goes in the
build log instead.

### 2. Build log — the teaching

One file per milestone in `docs/walkthrough/`, e.g. `M1-cvm-ingestion.md`,
written as the milestone completes. Structure:

1. **What got built** — the components and how they fit together
2. **How it works** — the actual flow, end to end, in prose
3. **The .NET mechanics** — what each unfamiliar piece genuinely does
   (`HttpClientFactory`, `IEntityTypeConfiguration`, `BackgroundService`),
   including the failure modes and what they'd replace
4. **Why these decisions** — alternatives rejected and the reasoning
5. **Interview answers** — the three or four questions this milestone invites,
   answered properly
6. **What I'd change at 10,000 companies** — the honest scaling answer

Write it at the same depth you'd use to bring a new engineer onto the codebase.
It is not a summary; it is the explanation the code comments deliberately don't
carry. It also ships as part of the repo — a documented build narrative is a
portfolio asset in itself.

### ADRs

You may draft them. I edit every one before it's committed — an ADR in my repo
in someone else's reasoning is worse than no ADR, because I'll be asked to
defend it. Draft, hand it to me, I'll revise.

### Rules

- Prefer the boring solution. No abstraction with a single implementation
  unless a second one is coming in this milestone.
- Tests alongside the code, never in a cleanup pass afterwards.
- Tell me when I'm cargo-culting — copying a pattern without a reason for it.
- Tell me when I'm about to overengineer. This is my known weakness and the
  main risk to this project.
- If something contradicts a decision recorded in `docs/decisions/`, say so
  before doing it.
- Never write a PR description for me.
- Don't pad answers. If the answer is one line, it's one line.
- If a milestone's build log isn't written, the milestone isn't done.

---

## Architecture

Modular monolith. Organized by business capability, not by technical layer
alone.

```
src/
  FinancialIntelligence.Api             ASP.NET Core host, controllers, DTOs
  FinancialIntelligence.Application     use cases, abstractions, orchestration
  FinancialIntelligence.Domain          entities, value objects, invariants
  FinancialIntelligence.Infrastructure  EF Core, provider clients, AI adapters
  FinancialIntelligence.Worker          scheduled ingestion
tests/
  FinancialIntelligence.UnitTests
  FinancialIntelligence.IntegrationTests
  FinancialIntelligence.ApiTests
```

**Dependency rule, enforced:**

- `Api` → `Application` → `Domain`
- `Infrastructure` implements `Application` abstractions
- `Worker` uses `Application` services
- `Domain` references **nothing** — no EF Core, no `HttpClient`, no AI SDKs,
  no `Microsoft.Extensions.*`
- `Api` → `Infrastructure` exists solely as the composition root in
  `Program.cs`. No Infrastructure type may be referenced anywhere else in Api.

`DependencyRuleTests` enforces the Domain half at build time. If a change would
violate any of this, say so rather than working around it.

### Modules

`Companies` · `FinancialData` · `MarketData` · `Ingestion` · `Analytics` ·
`Anomalies` · `Research` · `AI` · `Observability`

---

## Stack

.NET 10 (LTS) / C# 14 · ASP.NET Core · EF Core 10 · PostgreSQL 16 ·
xUnit · Docker Compose · GitHub Actions · OpenTelemetry (from M7)

No Kubernetes. No message broker. No microservices. No React or Blazor —
the frontend is one static HTML page served by the API.

---

## Commands

```bash
docker compose up -d                        # Postgres
dotnet build
dotnet test
dotnet run --project src/FinancialIntelligence.Api

# Migrations
dotnet ef migrations add <Name> \
  --project src/FinancialIntelligence.Infrastructure \
  --startup-project src/FinancialIntelligence.Api
dotnet ef database update \
  --project src/FinancialIntelligence.Infrastructure \
  --startup-project src/FinancialIntelligence.Api

# Secrets (never appsettings.json)
# The data path needs none. This is the dev-time cloud model only (ADR 0006).
dotnet user-secrets set "AI:Cloud:ApiKey" "<key>" \
  --project src/FinancialIntelligence.Api
```

---

## Conventions

**Code.** Nullable reference types on, warnings as errors. `CancellationToken`
on every I/O path. DTOs at the API boundary — EF entities never leave the
Infrastructure/Application seam. Constructor injection only.

**Tests.** Financial math is unit-tested against values calculated by hand from
real filings, with the source noted in the test name or a comment. Ingestion is
tested against committed CVM and B3 fixtures, never a live download.
Integration tests use a real Postgres in Docker, not an in-memory provider —
in-memory doesn't enforce the constraints that make idempotency work.

**Git.** Short-lived feature branches, PR into `main` even working alone.
Conventional commits: `feat(ingestion): add CVM DFP importer`.
Never `fix`, `update stuff`, `wip`. `main` always builds and passes tests.

---

## Non-negotiables

- Every financial fact stores provenance: source file, document version,
  retrieval timestamp, and the CD_CONTA it came from.
- Ingestion is idempotent. Running a sync twice creates nothing new.
- A revised value never silently overwrites the original. CVM's `VERSAO` field
  makes supersession explicit — record it, don't collapse it.
- **`ORDEM_EXERC` must be filtered.** Every CVM file carries the current period
  *and* the prior-period comparative. Ingesting both double-counts everything.
- **`ESCALA_MOEDA` must be applied.** Values are published in units or
  thousands. A fact stored without its scale applied is off by 1000x.
- Every fact records its `AccountingBasis`. Consolidated and individual are
  different numbers for the same period and must never be mixed in one series
  (ADR 0008).
- Bank and insurer charts of accounts differ from industrials. `3.01` does not
  mean the same thing for Itaú as for Vale — never assume one mapping fits all.
- Derived facts (Q4 from the annual figure minus the three quarters) are flagged
  as derived.
- Financial math is deterministic and unit-tested. The model never calculates.
- The AI never receives database credentials and never executes SQL. Read-only
  tools over Application services, nothing else.
- The AI does not invent missing values. Unavailable data is reported as
  unavailable.
- Output is never framed as investment advice. Call them anomalies or signals,
  never fraud or indicators. Never `fraude` or `manipulação`.
- No secrets in git, ever. CVM and B3 need no credentials, so the whole data
  path — download, ingestion, normalization, analytics, anomalies — runs without
  one, and so does the test suite. The only secret in the project is the
  development-time cloud AI key (ADR 0006); the Ollama demo path needs nothing.

---

## Current state

> Update this every session. It's the first thing you read.

**Milestone:** M0 — walking skeleton
**Goal:** one company, one committed CVM fixture, revenue in Postgres and back
out through one endpoint. Ugly and hardcoded on purpose — no provider
abstraction, no normalization pipeline, no interfaces yet.

**Decided 25 Sep:** data source switched from SEC to CVM + B3 (ADR 0002).
`Company` keyed on CD_CVM (ADR 0007). Consolidated basis with individual
fallback, resolved to one basis per company and metric and never spliced
(ADR 0008). Seed universe includes banks.

**Open:** ADRs 0002, 0007 and 0008 are drafts awaiting my pass. The session-3
working tree is committed on `m0-cvm-rewrite`; `main` still holds only the initial
commit, because `dotnet build` and `dotnet test` have not run since the rewrite.

See `NEXT.md` for where I left off, `docs/decisions/` for decisions made, and
`docs/walkthrough/` for how each milestone actually works.
