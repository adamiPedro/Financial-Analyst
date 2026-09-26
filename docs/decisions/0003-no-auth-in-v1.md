# 0003. Ship v1 without authentication

**Status:** Accepted
**Date:** 2026-09-21

## Context

The original plan scheduled authentication and authorization for week 9,
roughly 8–10 hours of a 160-hour budget.

Nothing in the Definition of Done requires it. The system has no user-specific
data in v1: no watchlists, no saved research sessions, no per-user
configuration. Authentication would be protecting a single user's access to
public SEC filings.

The competing use for those hours is an AI evaluation harness — a suite that
scores the agent's answers for grounding and correct tool use against known-
answer questions.

## Decision

v1 is a single-user system by design and ships with no authentication. The
decision and its consequences are documented in the README under Known
limitations, alongside a precise description of what multi-tenancy would
require.

The reclaimed hours go to the AI evaluation harness in M6.

## What multi-tenancy would actually require

Stated precisely, because "I'd just add Identity" is not an answer:

- A `User` table, and a foreign key on `ResearchSession` and `Watchlist` —
  the only two entities that are ever user-scoped. `Company`, `FinancialFact`,
  `PricePoint` and `Anomaly` are public data and stay global.
- A row-level filter applied in the Application layer query handlers for those
  two entities, not in controllers, so the Worker path is covered too.
- ASP.NET Core Identity or an external provider, with a tenant claim on the
  principal.
- A deny-by-default authorization policy, so a new endpoint is inaccessible
  until explicitly opened.
- Rate limiting moves from per-IP to per-user.

This is additive. No existing table or service signature changes.

## Consequences

**Good**
- 8–10 hours redirected to something rarer in a portfolio and much harder to
  fake than a login form.
- No half-wired Identity setup, which reads worse than none at all.
- The written analysis above demonstrates the thinking that building it would
  have demonstrated, at a fraction of the cost.

**Bad**
- Cannot demo a login flow or user-scoped features. If a job posting
  specifically emphasises auth work, this project won't show it.
- Requires being able to talk through the multi-tenancy delta confidently.
  This ADR is the preparation for that conversation, not a substitute for it.
- If the project is ever deployed publicly, the ingestion trigger endpoint
  must be disabled or protected — an unauthenticated endpoint that makes
  outbound API calls is an abuse vector.

## Alternatives considered

**ASP.NET Core Identity** — rejected on cost. It is the right choice the
moment there is a second user, and nothing here makes adopting it later
harder.

**A static API key in a header** — rejected. It looks like security without
being security, and it would be a worse signal in an interview than an honest
"deliberately out of scope".

**Build it but leave it disabled behind a feature flag** — rejected. Pays the
full implementation cost to demo nothing, and untested auth code in a repo is
a liability rather than an asset.
