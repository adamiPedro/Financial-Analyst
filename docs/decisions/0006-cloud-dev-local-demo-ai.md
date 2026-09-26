# 0006. Develop the agent against a cloud model, demo it on Ollama, and measure the gap

**Status:** Accepted
**Date:** 2026-09-21

## Context

The plan specifies a local-first AI layer using Ollama, behind an
`IAIProvider` abstraction, with a model capable of reliable tool calling.

The development machine is an Apple Silicon Mac with 16 GB of unified memory,
shared with Docker, Postgres and an IDE. That comfortably runs a 7–8B model at
4-bit quantisation and is tight for anything larger.

Tool-calling reliability at that model size is the problem. Failures are
frequent and varied: malformed arguments, invented tool names, tool calls
skipped in favour of a hallucinated answer, and multi-step questions answered
after a single call. When the agent's plumbing is also new and unproven, it is
impossible to tell whether a failure is a bug or the model.

## Decision

`IAIProvider` gets two adapters from the first day of M6 — a cloud provider
and Ollama.

Development and debugging happen against the cloud model, so that failures
observed during implementation are attributable to the code. Once the tool
layer is correct and the eval suite passes, the same suite runs against a
local model and the difference is measured and written up.

The demo runs on Ollama, so the system demonstrably works at zero cost with no
external dependency.

## Consequences

**Good**
- The abstraction is exercised by two real providers from the start, so it is
  shaped by actual differences rather than guessed at.
- Debugging is tractable. A failure during development means a bug, not an
  ambiguity.
- The measured comparison — same eval suite, same questions, local vs cloud
  grounding and tool-use accuracy — is the most distinctive artifact in the
  project. Very few portfolio projects quantify this.
- The system is demonstrable offline and at no cost, which was the original
  point of going local-first.

**Bad**
- Some cost during M6 development. Small for an eval suite of a few dozen
  questions, but not zero, and it breaks the project's $0 target.
- Two providers to keep working, and their tool-calling formats differ enough
  to be a real source of bugs in the adapter layer.
- The measurement only holds for the models and date tested. It needs a
  version and a date stated beside it in the README, or it becomes a stale
  claim.

## Alternatives considered

**Ollama only, as originally planned** — rejected. Debugging a new tool layer
against an unreliable model conflates two independent sources of failure, and
the likely outcome is hours lost to a suspected bug that was the model all
along.

**Cloud only** — rejected. Loses the zero-cost demo, adds a hard external
dependency and an API key to the run instructions, and discards the most
interesting comparison the project could make.

**A larger local model via heavier quantisation** — rejected for now. A 14B at
Q4 is roughly 9 GB and contends with Docker and Postgres on a 16 GB machine.
Worth revisiting if the eval suite shows 7–8B failing in ways that matter.
