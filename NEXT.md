# Where I left off

Overwrite this at the end of every session. Commit it.

---

**Session:** 4
**Date:** 2026-09-25
**Milestone:** M0 — walking skeleton

**DONE:**
- Session 3's working tree was entirely untracked. Committed on branch
  `m0-cvm-rewrite` (67a53d5) — not `main`, since build and tests have not run
  since the rewrite and `main` stays green.
- **ADR 0008 resolved to one basis per series.** Consolidated preferred,
  individual only when there is no consolidated data at all, gaps in the chosen
  basis reported unavailable rather than filled, plus a count when the unchosen
  basis holds data for those periods. Segmenting at a basis change is now a
  rejected alternative with the reasoning kept.
- **ADR 0007 keeps CD_CVM.** Added two guards: a test that the key is not
  value-generated, and CD_CVM validated against CAD at ingest. Hot-path claim
  restated as one less moving part rather than a throughput argument.
- SEC confirmed off the roadmap. Brazilian market only for v1, and the surrogate
  alternative in 0007 no longer reads as pending a revisit.
- CLAUDE.md and `.gitignore`: SEC-era references removed, and the secrets
  non-negotiable narrowed to the data path so it stops contradicting ADR 0006.

**BLOCKED:**
- Nothing blocking, but worth recording: `api.nuget.org`, `dados.cvm.gov.br` and
  B3 are all blocked from Claude's cloud container *and* from the shell it reaches
  on this Mac. So restore, build, test, `dotnet ef` and the fixture downloads are
  mine to run in macOS Terminal — Claude can edit files here but cannot verify
  them.

**NEXT:**
1. `dotnet build` and `dotnet test` — still the first run since the rewrite
2. Edit ADRs 0002, 0007 and 0008 into my own words, then commit
3. Confirm seed-universe CD_CVM codes and CNPJs against the real CAD file — the
   values in `CompanyTests` are plausible but unverified
4. Download `cad_cia_aberta.csv` and one `dfp_cia_aberta_YYYY.zip`, commit a
   trimmed fixture. While CAD is open: does any CNPJ appear under more than one
   CD_CVM (re-registration)? If so, drop `IsUnique()` on the CNPJ index. And does
   CAD include cancelled companies? If so, 0007 needs no allow-list.
5. First migration: `dotnet ef migrations add AddCompany ...`
6. Hardcoded endpoint reading revenue (CD_CONTA 3.01) out of the fixture —
   closes M0
7. Implement 0007's two guards in code: the key test, and CAD validation at
   ingest
8. Fix `docs/walkthrough/M0-walking-skeleton.md` — its basis section predates
   the single-basis rule — and finish it before tagging
   `v0.1-skeleton`
9. PR `m0-cvm-rewrite` into `main` once build and tests are green

**Open question carried forward:**
- Confirm `SETOR_ATIV` is the right CAD column for sector, and check what CAD
  actually calls registration status — `Company.Sector` and
  `Company.RegistrationStatus` assume both exist.
- Decide the bank account-code mapping before M2 (ADR 0009). `3.01` does not
  mean for Itaú what it means for Vale.
- **New:** does any of the 12 seed companies actually switch accounting basis
  mid-history? If one does, 0008's single-basis rule may need the segment model
  after all. Decide on the data, not on taste.
- Whether CAD validation needs an allow-list for deregistered filers, whose
  historical filings exist but who may be absent from current CAD (0007).
