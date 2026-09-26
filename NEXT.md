# Where I left off

Overwrite this at the end of every session. Commit it.

---

**Session:** 3
**Date:** 2026-09-25
**Milestone:** M0 — walking skeleton

**DONE:**
- **Data source switched from SEC to CVM + B3.** Reason: the audience is
  Brazilian financial institutions, and CVM's standardized chart of accounts
  removes the biggest schedule risk in the plan. ADR 0002 rewritten.
- `Company` now keyed on CD_CVM as a natural key — no surrogate (ADR 0007)
- `Cnpj` value object with real mod-11 check-digit validation, replacing `Cik`
- `AccountingBasis` enum; consolidated default with individual fallback (ADR 0008)
- `CompanyConfiguration` rewritten — `ValueGeneratedNever()` on the key, unique
  indexes on Cnpj and Ticker
- Seed universe chosen: 12 companies including Itaú and Bradesco
- README rewritten: data sources, seed universe, honest limitations

**BLOCKED:**
- Nothing.

**NEXT:**
1. `dotnet build` and `dotnet test` — first run since the rewrite
2. Edit ADRs 0002, 0007, 0008 into my own words, then commit
3. Confirm CD_CVM codes and CNPJs for the seed universe against the real CAD
   file — the values in `CompanyTests` are plausible but unverified
4. Download `cad_cia_aberta.csv` and one `dfp_cia_aberta_YYYY.zip`, commit a
   trimmed fixture
5. First migration: `dotnet ef migrations add AddCompany ...`
6. Hardcoded endpoint reading revenue (CD_CONTA 3.01) out of the fixture —
   closes M0
7. Rewrite `docs/walkthrough/M0-walking-skeleton.md` (currently all about Cik)
   before tagging v0.1-skeleton

**Open question carried forward:**
- Confirm `SETOR_ATIV` is the right CAD column for sector, and check what CAD
  actually calls registration status — `Company.Sector` and
  `Company.RegistrationStatus` assume both exist.
- Decide the bank account-code mapping before M2 (ADR 0009). `3.01` does not
  mean for Itaú what it means for Vale.
