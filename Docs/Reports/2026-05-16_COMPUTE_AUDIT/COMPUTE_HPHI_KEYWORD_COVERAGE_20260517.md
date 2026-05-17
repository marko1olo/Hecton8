# COMPUTE H-PHI KEYWORD COVERAGE 2026-05-17

Status: AUDIT COMPLETE
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 only. Timaert excluded.
Evidence class: STATIC_DOC.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Purpose

The user asked to add searchable terms where the H-Phi / ash-fi integration metric exists. Blindly editing every historical or other-agent log would create cross-domain churn and collision risk. The chosen boundary is:

- Tag active compute audit docs and stable H-Phi authority docs.
- Keep other-agent logs and archives immutable unless they are owned by this audit.
- Use `COMPUTE_HPHI_SEARCH_INDEX_20260517.md` as the canonical search entry point for all H-Phi/token accounting.

## Coverage Scan

Command class: local UTF-8 Markdown scan under `Docs/**/*.md`.

| Metric | Value |
|---|---:|
| Markdown docs with H-Phi / HPhi / hphi / HФ / Аш-Фи / ash-fi text | 340 |
| Same docs missing the full search alias line | 326 |
| Non-archive Markdown docs with H-Phi-family text | 232 |
| Non-archive docs missing aliases | 220 |

The high missing count is expected: many hits are unrelated agent logs, generated atlas/dependency summaries, or archived evidence snapshots. They are indexed but not mass-mutated.

## Tagged By This Audit

Active compute audit trail:

- `COMPUTE_AUDIT_BRIEF.md`
- `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_SEARCH_INDEX_20260517.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1337.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1142.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md`
- `Docs/Tasks/Status_COMPUTE_LOGISTICS_AUDITOR.md`
- `Docs/AgentLogs/Rationale_COMPUTE_LOGISTICS_AUDITOR.md`

Stable authority docs:

- `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/H8_GLOSSARY.md`

## Search Contract

Use one of these commands:

```powershell
rg "ash-fi|Аш-Фи|HФ|token-H-Phi-ROI" Docs
rg "Runtime H-Phi risk|Data sovereignty|token-H-Phi-ROI" Docs/Reports
```

## Verdict

The canonical H-Phi / ash-fi audit trail is now searchable without relying on one spelling. Other-agent logs remain unmodified because this audit owns compute accounting, not their chronology.

STATUS: AUDIT COMPLETE.
