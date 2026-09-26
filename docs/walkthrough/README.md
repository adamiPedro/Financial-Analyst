# Build walkthrough

One file per milestone, written as the milestone completes. Together they
explain how this system was built and why — the material the source comments
deliberately leave out.

This exists for two reasons. It is interview preparation: every question this
codebase invites gets answered here, in writing, while the work is fresh. And
it is part of the deliverable — a complex system with a documented construction
narrative is a stronger portfolio artifact than the same system without one.

## Files

| Milestone | File | Status |
| --- | --- | --- |
| M0 | `M0-walking-skeleton.md` | in progress |
| M1 | `M1-cvm-ingestion.md` | not written |
| M2 | `M2-analytics.md` | not written |
| M3 | `M3-anomaly-engine.md` | not written |
| M4 | `M4-market-data.md` | not written |
| M5 | `M5-research-layer.md` | not written |
| M6 | `M6-ai-agent.md` | not written |
| M7 | `M7-hardening.md` | not written |

## Structure

Each file follows the same six sections:

1. **What got built** — the components and how they fit together
2. **How it works** — the actual flow, end to end, in prose
3. **The .NET mechanics** — what each unfamiliar piece genuinely does, its
   failure modes, and what it replaces
4. **Why these decisions** — alternatives rejected and the reasoning
5. **Interview answers** — the questions this milestone invites, answered
6. **What I'd change at 10,000 companies** — the honest scaling answer

Written at the depth you'd use to onboard a new engineer. Not a summary.

## Rule

A milestone is not done until its walkthrough is written. The git tag comes
after the file, not before.
