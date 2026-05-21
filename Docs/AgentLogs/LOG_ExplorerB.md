## 2026-05-20 ExplorerB UI/RU/Settings Audit

What was wrong: Source contains doctor-facing English fallback remnants and raw key rendering despite smoke scripts passing.
What was done: Static audit of `C:\hades\dental-crm` web/api files and relevant smoke scripts. No product code edited.
Cinematic Cheats used: None; web UI audit only.
Exact Microseconds saved: 0 measured. Static source evidence only; no profiler artifact.

Evidence:
- Ran `node scripts/smoke-ui-preferences.mjs`: ok, requiredPreferenceCount 18.
- Ran `node scripts/smoke-telegram-control-ui-source.mjs`: ok, tokenPatternHits 0.
- Ran `node scripts/smoke-onboarding-configuration-source.mjs`: ok.
- Ran `node scripts/smoke-settings-preferences.mjs`: ok.

Residual risk: No browser runtime pass, screenshots, or API integration run. Findings are STATIC_SOURCE.
