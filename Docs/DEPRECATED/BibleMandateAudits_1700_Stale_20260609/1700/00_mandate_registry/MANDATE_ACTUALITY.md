# Mandate Registry Actuality Report

Status: YELLOW_SOURCE_WORDING_REFRESH_REQUIRED
Date: 2026-06-02
Evidence class: `STATIC_DOC`

## What Exists

- 80 mandate source files were found under `.agents-skills`.
- Every mandate has at least one root bible route or audit-group route.
- No red route-coverage gap was found in the current static matrix.

## What Is Not Current Enough

- 55 mandate files are yellow for wording currency.
- 48 mandates do not explicitly state continuous `GlobalQualityWeight` scaling.
- 11 mandates contain deprecated, legacy, or older architecture wording that should not override root bibles.
- 1 mandate lacks an explicit proof word.

## Correct Authority

The current authority is not the older source text by itself. Agents must read the root bible route and system audit before implementation. When `.agents-skills` conflicts with root bibles, `AGENTS.md` and root bibles win.

## Required Update

- Refresh yellow mandate files with explicit `GlobalQualityWeight`, proof artifacts, and no legacy route language.
- Keep `MANDATE_CURRENCY_MATRIX.md` as the source list for which files need wording refresh.

