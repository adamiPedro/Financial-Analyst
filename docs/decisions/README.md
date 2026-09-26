# Architecture decision records

Each file records one decision: what was decided, why, what it costs, and what
was rejected. They are written when the decision is made, not reconstructed
afterwards. A decision that was obvious doesn't need one.

Numbered sequentially. Never renumber. A superseded ADR stays in place with its
status changed and a pointer to the one replacing it — the history is the point.

## Decided

| # | Decision | Status | Date |
| --- | --- | --- | --- |
| [0001](0001-modular-monolith.md) | Modular monolith over microservices | Accepted | 2026-09-21 |
| [0002](0002-data-sources.md) | CVM open data + B3 quote files; no commercial provider | **Draft** | 2026-09-25 |
| [0003](0003-no-auth-in-v1.md) | No authentication in v1 | Accepted | 2026-09-21 |
| [0004](0004-static-frontend.md) | Static HTML frontend, no SPA framework | Accepted | 2026-09-21 |
| [0005](0005-compose-as-deployment.md) | Docker Compose is the deployment story | Accepted | 2026-09-21 |
| [0006](0006-cloud-dev-local-demo-ai.md) | Cloud LLM for development, Ollama for demo | Accepted | 2026-09-21 |
| [0007](0007-company-identity.md) | CD_CVM as Company's primary key | **Draft** | 2026-09-25 |
| [0008](0008-consolidated-vs-individual.md) | Consolidated basis, individual as fallback | **Draft** | 2026-09-25 |

Three drafts await Pedro's pass. 0002 and 0007 supersede earlier drafts written
when the project targeted SEC data; the superseded text is in git history rather
than kept as separate files, since neither was ever committed as accepted.

## Expected, not yet decided

Listed so they don't get made by accident. Each gets written **by me**, after
working through the options — they're the ones an interviewer is most likely to
probe.

| # | Decision | Due |
| --- | --- | --- |
| 0009 | Account-code mapping: how CD_CONTA maps to canonical metrics, and how bank charts of accounts are handled | M1 |
| 0010 | Idempotency key for a financial fact | M1 |
| 0011 | Restatement handling: which VERSAO is current and how supersession is recorded | M1 |
| 0012 | ORDEM_EXERC policy: how the prior-period rows in every file are treated | M1 |
| 0013 | Anomaly baseline: rolling window length and why | M3 |
| 0014 | AI tool granularity: few broad tools vs many narrow ones | M7 |

0010 and 0011 are the two best architecture questions in this project. Give them
real time.

## Format

Copy [0000-template.md](0000-template.md). Keep them short — one page. An ADR
nobody rereads is a worse artifact than a paragraph someone does.
