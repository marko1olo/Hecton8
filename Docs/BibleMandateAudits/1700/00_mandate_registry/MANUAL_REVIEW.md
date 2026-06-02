# Mandate Registry Manual Review

Status: STATIC REVIEW - SOURCE MANDATE WORDING NOT FULLY CURRENT
Date: 2026-06-02

## What Exists

- `.agents-skills` contains a large technical mandate registry and the audit scanned 80 mandate files.
- Root bibles now route the major implementation domains and compensate for old mandate wording with explicit proof, rejection, static/runtime, and `GlobalQualityWeight` clauses.
- No red route gap was found in the automated audit baseline.

## What Is Missing / Not Proven

- 55 mandate files are still yellow for wording currency: missing explicit `GlobalQualityWeight`, missing explicit proof language, or legacy/deprecated terms.
- A mandate file being yellow does not mean the root bible is missing. It means the source mandate is not clean enough to be treated as standalone current authority.

## Current Classification

- Registry coverage: `GREEN_ROUTE_COVERED`.
- Source text currency: `YELLOW_REFRESH_REQUIRED`.

## Required Next Proof

- Refresh yellow mandate files or keep root bibles as the current authority layer.
- When a mandate conflicts with a newer root bible, record the root bible as the active route and update the mandate text later.
