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

## cad_cia_aberta.csv

CVM's register of every company that has filed as an open company (CAD).

- **Source:** `cad_cia_aberta.csv` from
  https://dados.cvm.gov.br/dados/CIA_ABERTA/CAD/DADOS/, downloaded
  2026-09-29, SHA-256 `e87cb2bef6b0d1e3fabdcf7c9301b0fe00da690f812696fc0f74f8053f3ca45e`
- **Trimmed to:** the header and every row for 11 companies. 13 rows, each
  byte-identical to a line in the published file.
- **Encoding:** Latin-1, CRLF, `;`-separated, left exactly as published.

Why these rows:

| Rows | What they exercise |
|---|---|
| Petrobras (9512) ×2, Vale (4170) ×2 | One row per market (`TP_MERC`), otherwise identical. Must merge into one company each. Petrobras's name has `Ó`. |
| Equatorial Goiás 2445 and 25577 | One CNPJ under two CVM codes after re-registration. 2445 is `CANCELADA`. |
| Rossi Residencial (16306) | Status `SUSPENSO(A) - DECISÃO ADM`: the `Ã` breaks if the file is read as UTF-8. |
| Banco Santander S.A. (868) | Cancelled, with a blank sector. |
| Vivara, Itaú, Bradesco, Banco do Brasil, Santander Brasil | Seed companies. Banco do Brasil's CNPJ `00.000.000/0001-91` is real despite the zeros. |

## dfp_2024/, dfp_2017/, itr_2024/

The five statements the importer reads (BPA, BPP, DRE, DFC_MI, DFC_MD), both
bases, under CVM's own entry names. Tests zip a folder to rebuild what a
download gives the importer.

| Folder | Source ZIP (SHA-256) | Companies | Rows |
|---|---|---|---|
| `dfp_2024/` | `dfp_cia_aberta_2024.zip` (`f52caa45b6fc202334d7b87bb9bb24830280f5ff6a29b8b8557894b26fed1be8`) | Petrobras 9512, Itaú 19348, Bradesco 906, BB Seguridade 23159 | 3,602 |
| `dfp_2017/` | `dfp_cia_aberta_2017.zip` (`1ff8185d61759e49b406c55c5d0b593587c2e76e9032c95f836f7fea02fc9d58`) | Bradesco 906 | 890 |
| `itr_2024/` | `itr_cia_aberta_2024.zip` (`18bd7c2c3b45d42a1a408edb16e43bf62ba59bb27d0d592f12c69316e9600ccd`) | Petrobras 9512 | 3,730 |

- **Source:** https://dados.cvm.gov.br/dados/CIA_ABERTA/DOC/{DFP,ITR}/DADOS/,
  downloaded 2026-10-13.
- **Trimmed to:** the header and every row for the listed companies, both
  `ORDEM_EXERC` values. Each line is byte-identical to the published file.
- **Encoding:** Latin-1, CRLF, `;`-separated.

Why these rows:

| Rows | What they exercise |
|---|---|
| Petrobras 2024 | Industrial layout. Equity is `2.03` − `2.03.09` (367,514,000 − 1,508,000 thousand), which equals its individual `2.03`. |
| Itaú 2024 | Two layouts in one company: consolidated net income at `3.09` (Bank309), individual at `3.13` (Bank313). |
| Bradesco 2024 and 2017 | The 2020 switch: `3.09`/`2.08` in 2017 (version 2), `3.11`/`2.07` in 2024. Consolidated `2.07.01` is shareholders' equity; individual `2.07.01` is paid-in capital. |
| BB Seguridade 2024 | Insurer layout (`3.01` "Receitas das Atividades Seguradoras", net income at `3.13`). |
| `BPA`/`BPP` | No `DT_INI_EXERC` column: balances have only an end date. |
| `DFC_MD` | Header only: these companies file the indirect cash flow. |
| Petrobras ITR 2024 | Q2 and Q3 income rows twice: three months and year to date. |
