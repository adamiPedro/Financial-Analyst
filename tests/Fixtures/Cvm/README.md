# CVM fixtures

## dfp_cia_aberta_DRE_con_2024.csv

Consolidated income statements (DRE) from the 2024 annual filings (DFP).

- **Source:** `dfp_cia_aberta_2024.zip` from
  https://dados.cvm.gov.br/dados/CIA_ABERTA/DOC/DFP/DADOS/, downloaded
  2026-09-29, SHA-256 `ffc488e1a7c7375619a723440c8d5705c712902b15123a31daf9cc79b1d500f9`
- **Trimmed to:** the header, every Petrobras row (CD_CVM 009512), and Vivara's
  revenue rows (CD_CVM 024805, account 3.01). 89 lines, each byte-identical to
  a line in the published file.
- **Encoding:** Latin-1, CRLF, `;`-separated, left exactly as published.

Why these rows:

| Rows | What they exercise |
|---|---|
| Petrobras, `ORDEM_EXERC` = `ÚLTIMO` and `PENÚLTIMO` | Every account appears twice, for 2024 and for the 2023 comparative. Reading both double-counts. |
| Petrobras, `ESCALA_MOEDA` = `MIL` | Values are in thousands. 2024 revenue (3.01) is `490829000` in the file, R$ 490.8 billion once scaled. |
| Vivara, `ESCALA_MOEDA` = `UNIDADE` | The other scale. 2024 revenue is `2577113417`, already in reais. |
| Petrobras 3.99.x (earnings per share) | Marked `MIL` like the rest of the statement but actually in R$ per share (9.57 for 2023). Applying the scale here is wrong. |
| `CD_CVM` = `009512` | Zero-padded to six digits in the file, unlike CAD's `9512`. |
