# Financial Intelligence Platform

Ingests Brazilian public-company financial data from the CVM open data portal
and B3 historical quotes, normalizes it into a stable internal model with full
provenance, calculates deterministic analytics, detects explainable anomalies,
and exposes all of it to an AI research agent through narrow read-only tools.

For readers outside Brazil: the **CVM** (Comissão de Valores Mobiliários) is
Brazil's securities regulator, the counterpart of the SEC. **DFP** and **ITR**
are the standardized annual and quarterly financial statement filings every
listed company must submit. **B3** is the São Paulo stock exchange.

**This is not investment advice, a trading system, or a stock-picking tool.**
Detected signals are statistical unusualness, not evidence of wrongdoing or a
judgement about any company.

## Status

M0 — walking skeleton. One company's revenue goes from a real CVM filing into
Postgres and out through `GET /api/companies/{cvmCode}/revenue`. See
`docs/decisions/` for decisions made so far.

## Running it

Requires .NET 10 SDK and Docker.

```bash
docker compose up -d                                    # Postgres on :5432
dotnet build
dotnet test
dotnet run --project src/FinancialIntelligence.Api      # http://localhost:5080
curl http://localhost:5080/health
```

`docker compose down` stops the database and keeps the data.
`docker compose down -v` removes it.

## Configuration

Connection strings live in `appsettings.json` with development defaults that
match `docker-compose.yml`. Nothing secret is committed.

**There are no API keys.** Both data sources are public bulk downloads with no
authentication and no rate limits, so a clean clone runs with no credentials at
all. If this project ever asks you for a key, something has gone wrong.

## Architecture

Modular monolith. Dependency rule:

```
Api  ->  Application  ->  Domain
Infrastructure implements Application abstractions
Worker uses Application services
Domain references nothing
```

`Api` references `Infrastructure` only so `Program.cs` can register concrete
implementations. That is the composition root and the single permitted
exception; `DependencyRuleTests` enforces the Domain half of the rule at build
time.

Modules: Companies · FinancialData · MarketData · Ingestion · Analytics ·
Anomalies · Research · AI · Observability

## Repo layout

```
src/
  FinancialIntelligence.Domain          entities, value objects, invariants
  FinancialIntelligence.Application     use cases, abstractions
  FinancialIntelligence.Infrastructure  EF Core, provider clients
  FinancialIntelligence.Api             HTTP host, static frontend
  FinancialIntelligence.Worker          scheduled ingestion
tests/
  ...UnitTests                          math and rules, no I/O
  ...IntegrationTests                   real Postgres, no mocked DB
  ...ApiTests                           endpoint behaviour
docs/decisions/                         ADRs
```

## Data sources

| Source | Dataset | Use |
| --- | --- | --- |
| [CVM open data](https://dados.cvm.gov.br/) | DFP | Annual financial statements |
| CVM open data | ITR | Quarterly financial statements |
| CVM open data | CAD | Company registry — name, CNPJ, sector, status |
| [B3](https://www.b3.com.br/en_us/market-data-and-indices/data-services/market-data/historical-data/equities/historical-quotes/) | COTAHIST | Daily historical quotes |

All four are yearly bulk files, publicly downloadable, no authentication. CVM
refreshes weekly to pick up resubmissions. See ADR 0002 for why these rather than a
commercial data provider.

Recorded fixtures are committed so the test suite and a fresh clone run with no
network at all.

### Seed universe

Twelve companies, chosen for long filing history and sector spread. Banks are
included deliberately: financial institutions use a different chart of accounts,
so handling them is harder — and it is the most relevant thing this project can
demonstrate to its intended readers.

| Company | Ticker | Why |
| --- | --- | --- |
| Petrobras | PETR4 | Largest filer, long history, state-influenced |
| Vale | VALE3 | Mining, commodity-cycle volatility |
| Itaú Unibanco | ITUB4 | Bank — different account structure |
| Bradesco | BBDC4 | Bank — second case to prove the first wasn't a one-off |
| Ambev | ABEV3 | Consumer staples, stable margins |
| WEG | WEGE3 | Industrials, consistent growth |
| Suzano | SUZB3 | Pulp, heavy FX exposure |
| Embraer | EMBR3 | Aerospace, lumpy revenue |
| Magazine Luiza | MGLU3 | Retail, dramatic swings — good anomaly candidate |
| B3 | B3SA3 | The exchange itself, financial-sector-adjacent |
| Localiza | RENT3 | Asset-heavy services |
| JBS | JBSS3 | Protein, international operations |

## Known limitations

Updated as the project goes. Honest limitations are the point of this section.

- No authentication. Single-user by design — see ADR 0003 for what
  multi-tenancy would require.
- Anomaly detection is statistical, on short quarterly histories. Treat
  severity as a prompt to look closer, never as a conclusion.
- Q4 figures are derived (the annual figure minus the three quarters), not
  reported, and are flagged as derived.
- Consolidated basis is the default; where a company files only individual
  statements those are used instead, and every fact records which (ADR 0008).
- Bank and insurer account structures differ from industrials. Metric coverage
  for financial-sector companies is narrower and documented per metric.
- Brazilian filings are in BRL with no inflation adjustment. Multi-year nominal
  comparisons across a high-inflation period are not like-for-like, and this
  system does not deflate them.
- Twelve companies, not the whole market. The pipeline is the point, not
  coverage.
