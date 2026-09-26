# 0001. Build a modular monolith, not microservices

**Status:** Accepted
**Date:** 2026-09-21

## Context

This is a solo project built at roughly 8 hours per week, targeting a complete
v1 in about 160 hours. It has to demonstrate architectural judgment to
technical interviewers.

There is a pull toward microservices in portfolio projects precisely because
they look impressive. The system does have plausible service seams — ingestion,
analytics, AI — and splitting them would produce an architecture diagram with
more boxes.

There is no scaling pressure, no team boundary, and no independent deployment
requirement. The seams are conceptual, not operational.

## Decision

A single deployable ASP.NET Core application plus a worker, organized into
modules by business capability: Companies, FinancialData, MarketData,
Ingestion, Analytics, Anomalies, Research, AI, Observability.

Boundaries are enforced structurally through the project graph, not by
convention alone. The dependency rule is `Api → Application → Domain`, with
Infrastructure implementing Application abstractions, and Domain referencing
nothing — no EF Core, no `HttpClient`, no AI SDKs.

## Consequences

**Good**
- One database, one transaction scope. Ingestion idempotency stays a solved
  problem rather than a distributed one.
- Local setup is `docker compose up` and `dotnet run`. A stranger can run it.
- Refactoring across module boundaries costs a rename, not a version
  negotiation.
- The dependency rule is demonstrable and enforceable, which is the actual
  architectural skill being shown.

**Bad**
- No story about independent scaling or deployment. If asked, the answer has
  to be "here's when I'd split and why", not a demo.
- Module boundaries can erode silently. A project reference added in a hurry
  is all it takes.
- Less visually impressive than a service diagram to a non-technical reviewer.

## Alternatives considered

**Microservices** — rejected. The operational overhead (service discovery,
inter-service contracts, distributed tracing, multiple deployments) would
consume a large share of a 160-hour budget while solving no problem this
system has. Splitting without a forcing pressure is the specific mistake this
decision exists to avoid.

**Single-project layered application** — rejected. Simpler, but the dependency
rule becomes a convention rather than a constraint, and there would be nothing
to point at when asked how boundaries are maintained.

**Modular monolith with in-process message bus between modules** — rejected for
v1. It would be a reasonable evolution, but adding it now means introducing
indirection before any module has a reason to be decoupled from another.
Revisit if the ingestion pipeline grows enough to justify it.
