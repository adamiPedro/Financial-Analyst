# 0005. Docker Compose is the deployment story; a hosted instance is optional

**Status:** Accepted
**Date:** 2026-09-21

## Context

The Definition of Done requires that a clean clone can restore, build and run
the solution, and that a stranger can get it running from the README.

A live hosted URL is attractive on a resume, but free hosting tiers for a
stateful application are unreliable over the timescale a portfolio piece needs
to survive. Free Postgres instances expire or get archived after a period of
inactivity; free app tiers sleep and cold-start slowly. A dead link on a
resume is actively worse than no link — it reads as a project that was
abandoned.

## Decision

`docker compose up -d` bringing up Postgres, plus `dotnet run`, is the primary
and supported way to run this system. The Compose file, the migrations, and
committed demo fixtures together are the reproducibility claim.

A hosted instance is added in M7 only if hours allow. If deployed, the README
links it as a demo instance with an explicit note that it may be sleeping, and
a monthly calendar reminder exists to check it still responds.

## Consequences

**Good**
- The reproducibility claim does not depend on any third party's free tier
  continuing to exist.
- Works identically on a reviewer's machine regardless of platform, and works
  offline.
- Containerising from M0 rather than M7 means deployment problems surface
  early, when they are cheap.

**Bad**
- A reviewer has to install Docker and run two commands rather than click a
  link. Some fraction will not bother.
- No demonstration of a real deployment pipeline, cloud configuration, or
  production environment management.
- "It runs on my machine and in Compose" is a weaker operational story than a
  live system with uptime.

## Alternatives considered

**Deploy to Render or Railway as the primary story** — rejected as primary,
retained as optional. Free tiers sleep and free Postgres expires, so the link
degrades over exactly the months the project is on a resume.

**Fly.io** — the strongest of the free options for a containerised app with a
small Postgres. Reconsider at M7 if a hosted instance is wanted; the Compose
setup transfers with little rework.

**A recorded demo video instead of a live instance** — adopted as a complement
rather than an alternative. The 3–5 minute video in M7 covers the reviewers
who will not run anything locally, and it never breaks.

**Kubernetes** — rejected, and explicitly listed as out of scope in the plan.
It would demonstrate nothing this system needs and consume hours the project
does not have.
