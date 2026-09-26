# 0004. One static HTML page, no SPA framework

**Status:** Accepted
**Date:** 2026-09-21

## Context

The project needs a user interface for two reasons only: the demo video needs
to show something other than curl output, and the README needs screenshots.
The backend is the portfolio artifact.

The original plan left this open — "minimal ASP.NET/Blazor/React page" — with
a warning not to let the UI consume the schedule. Leaving it open is how it
consumes the schedule.

A React or Blazor frontend for this system is realistically 20+ hours once
tooling, state management, routing, charting and build integration are
included. The static equivalent is around 6.

## Decision

A single `index.html` served as a static file by the API, using `fetch` and
one charting library loaded from a CDN. No build step, no `package.json`, no
framework.

It shows: company selector, financial metric trends, price chart, anomaly
timeline, and the provenance of whatever fact is being displayed.

If the frontend ever needs a build step, that is a signal the scope grew and
should be questioned, not accommodated.

## Consequences

**Good**
- ~14 hours returned to backend work, which is what is actually being
  assessed.
- No Node toolchain in the repo, no `node_modules`, no build step between a
  clone and a running system.
- Provenance display — showing which filing a number came from — is the one
  genuinely interesting UI requirement, and it needs no framework.

**Bad**
- Will not impress anyone evaluating frontend skill. Accepted: this project is
  not applying for frontend roles.
- Hand-written DOM manipulation gets unpleasant past a few views. This is a
  ceiling, and hitting it means the UI has outgrown its brief.
- A CDN dependency means the page needs network access for charts. Vendor the
  library into the repo if the demo has to run fully offline.

## Alternatives considered

**Blazor** — tempting because it is C# and on-theme for a .NET portfolio.
Rejected: Blazor Server adds a stateful connection to reason about, Blazor
WASM adds a build step and a multi-megabyte payload, and neither improves the
thing being demonstrated.

**React** — rejected on cost. Would produce the nicest result and demonstrate
breadth, but at roughly three times the hours, taken directly from the
analytics and AI work that carries this project.

**Razor Pages** — a reasonable middle option, server-rendered with no build
step. Rejected narrowly: it couples presentation to the API project more
tightly than a static file does, and the charting still ends up as JavaScript.

**No UI at all, curl and Swagger only** — rejected. OpenAPI is good for
exercising the API but makes a poor demo video, and screenshots of Swagger
do not communicate what the system does.
