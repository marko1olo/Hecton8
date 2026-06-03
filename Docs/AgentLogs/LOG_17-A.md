# LOG_17-A

## 2026-06-03 - Agent 17-A Lore Text Bounds Pass

What was wrong:
- No `<AGENT_PROMPT id="17-A">` exists in `Docs/Tasks/CURRENT_BATCH.md`; direct user prompt used as source.
- Disk reality is 460 active applied-lore packets across 15 shipped applied-lore locales, not the requested 375 package assumption.
- RS056 native localization review pack had static 720p risks from verbose draft prefixes and long review-lock labels.
- Full applied-lore static audit still has a large backlog: 62,602 issue flags across 48,300 surface checks.
- Data monolith bake could not be launched: CPU samples were 60% then 55%, over the project gate for .NET build work. No `dotnet`/`csc` processes were present.

What was done:
- Added `Tools/LoreTextBoundsVerifier.py`, a cold CLI verifier for applied-lore packet text against 720p HUD/terminal surface bounds.
- Audited 5 RS056 packets across all 15 disk locales and 7 text surfaces per locale.
- Rewrote RS056 source text for deterministic safe reductions:
  - `Draft XX localization pending native pass.` -> `XX LOC HOLD:`
  - long review lock labels -> shorter gate labels.
- Regenerated applied lore derived data via `Tools/AppliedLoreImporter.py`: 460 packets, 6900 localized rows.
- Updated generated hash catalogs through existing tools:
  - `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`
  - `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`
- Wrote evidence artifacts:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS056_before.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS056_after_titlefix.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.json`
  - `Docs/Reports/H8_HASH_CATALOG_AUDIT_17-A.json`

Cinematic Cheats used:
- Replaced runtime endless simulation with a static, conservative pixel-bound estimator for first-pass triage.
- No per-frame TMP measurement, no scene search, no hot `GlobalRegistry` polling, no managed event traffic.
- This is a content/compiler-side fake, not gameplay logic.

Verification:
- `Tools/LoreTextBoundsVerifier.py` AST syntax parse: PASS.
- RS056 before: 5 packets, 525 surface checks, 747 issues, 0 hash collisions.
- RS056 after source fixes: 5 packets, 525 surface checks, 137 expansion warnings, 0 modeled line/word clipping flags, 0 hash collisions.
- All applied lore: 460 packets, 48,300 surface checks, 62,602 static issue flags, 0 applied-lore hash collisions.
- Project hash catalog: 1243 records, 0 collisions, generated C# check up to date.
- Current `static_data.h8bin` SHA-256: `A3B4510B6D30A8A71FCF02726335D6049554C1D91FC7A8B45161ECB95F5BC971`.
- Monolith bake: BLOCKED by CPU gate, not attempted.

Exact Microseconds saved:
- Proven gameplay frame saving: 0 us. No runtime path changed.
- Projected runtime savings are not claimed without Unity/TMP capture and profiler evidence.
- Offline process cost was spent to prevent future runtime UI churn and content overflow, not to optimize current frame time.

Integrator notes:
- Runtime claim remains `PENDING_VERIFICATION`. Static 720p bounds are evidence class `STATIC_SOURCE`, not screenshot proof.
- Next safe step is a .NET monolith bake when CPU is under 50% and no compiler process is active, followed by `h8bin_validator.py`.
- Do not mass-truncate global lore backlog. Use packet-by-packet native review with verifier reports.

## 2026-06-03 - Agent 17-A Cycle 2: RS081 Worker Dossiers

What was wrong:
- `RS081_COLONY_ANCHOR_WORKER_DOSSIERS` produced 863 static issue flags across 5 packets and 15 locales.
- After the generic draft-prefix pass, remaining modeled clipping was concentrated in tight HUD/terminal surfaces: titles, terminals, and two scanners.
- Full all-lore backlog remained high and needed refreshed counts after source edits.
- Monolith bake was unsafe: CPU was 100% and `dotnet` process `32956` was already active.

What was done:
- Ran focused RS081 verifier before edits:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS081_before.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS081_before.csv`
- Applied deterministic source reductions in `Docs/Lore/AppliedContent/packets/RS081_COLONY_ANCHOR_WORKER_DOSSIERS.packets.json`:
  - Long draft placeholder prefixes collapsed.
  - Worker dossier titles compressed to single-line labels.
  - Scanner summaries compressed to evidence-chain statements.
  - Terminal summaries compressed to hard two-line dossier prose.
- Ran focused RS081 verifier after compact pass:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS081_after_compact.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS081_after_compact.csv`
- Regenerated applied-lore derived data: 460 packets, 6900 localized rows.
- Refreshed full lore report:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.csv`
- Rechecked project hash catalog:
  - 1243 records
  - generated C# up to date
  - 0 collisions

Cinematic Cheats used:
- Replaced live UI loop with cold static bounds simulation across 15 disk locales.
- Replaced verbose dossier prose on tight surfaces with compact evidence-first labels.
- No runtime `TMP_Text` scanning, no `GlobalRegistry` hot polling, no same-frame job/readback loop.

Verification:
- RS081 before: 5 packets, 525 surface checks, 863 issue flags, 0 collisions.
- RS081 after draft-prefix pass: 226 issue flags, 0 collisions.
- RS081 after compact pass: 65 title expansion warnings, 0 modeled line/word clipping flags, 0 collisions.
- Full all-lore after cycle 2: 460 packets, 48,300 surface checks, 61,804 static issue flags, 0 applied-lore hash collisions.
- Project FNV catalog: 1243 records, 0 collisions, C# output up to date.
- Current `static_data.h8bin` SHA-256 unchanged: `A3B4510B6D30A8A71FCF02726335D6049554C1D91FC7A8B45161ECB95F5BC971`.
- Monolith bake: BLOCKED by CPU/dotnet gate, not attempted.

Exact Microseconds saved:
- Proven gameplay frame saving: 0 us. Runtime code path not changed.
- Static modeled clipping removed for RS081 tight surfaces; runtime proof still requires Unity/TMP capture.
- Bake contention avoided under CPU 100% / active `dotnet`; this is process safety, not a frame-time claim.

Integrator notes:
- RS081 remaining 65 warnings are expansion ratio only; no modeled 720p clipping remains in that package.
- Source data and generated hash artifacts are ready for the next safe monolith bake window.
- The global backlog still needs packet-by-packet review; do not mass rewrite localized prose.

## 2026-06-03 - Agent 17-A Cycle 3: RS082 Deep Reach Artifact Memos

What was wrong:
- `RS082_DEEP_REACH_ARTIFACT_MEMO_PACK` produced 863 static issue flags across 5 packets and 15 locales.
- After draft-prefix reduction, all remaining modeled clipping was in tight `title` and `terminal` fields.
- Full all-lore backlog still required refreshed global counts after the source edit.
- Monolith bake was unsafe: CPU was 100% and `dotnet` processes `20944` and `28544` were active.

What was done:
- Ran focused RS082 verifier before edits:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS082_before.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS082_before.csv`
- Applied deterministic source reductions in `Docs/Lore/AppliedContent/packets/RS082_DEEP_REACH_ARTIFACT_MEMO_PACK.packets.json`:
  - Long draft placeholder prefixes collapsed.
  - Artifact titles compressed to single-line labels.
  - Terminal summaries compressed to hard liability/procedure statements.
  - Scanner/wiki/audio/site fields left intact after they were no longer flagged.
- Ran focused RS082 verifier after compact pass:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS082_after_compact.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS082_after_compact.csv`
- Regenerated applied-lore derived data: 460 packets, 6900 localized rows.
- Refreshed full lore report:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.csv`
- Rechecked project hash catalog:
  - 1243 records
  - generated C# up to date
  - 0 collisions

Cinematic Cheats used:
- Used cold static bounds simulation instead of live UI scene churn.
- Turned verbose terminal memo prose into compact liability readouts.
- No runtime text measurement loop, no scene search, no build while compiler lane was occupied.

Verification:
- RS082 before: 5 packets, 525 surface checks, 863 issue flags, 0 collisions.
- RS082 after draft-prefix pass: 187 issue flags, 0 collisions.
- RS082 after compact pass: 65 title expansion warnings, 0 modeled line/word clipping flags, 0 collisions.
- Full all-lore after cycle 3: 460 packets, 48,300 surface checks, 61,006 static issue flags, 0 applied-lore hash collisions.
- Project FNV catalog: 1243 records, 0 collisions, C# output up to date.
- Current `static_data.h8bin` SHA-256 unchanged: `A3B4510B6D30A8A71FCF02726335D6049554C1D91FC7A8B45161ECB95F5BC971`.
- Monolith bake: BLOCKED by CPU/dotnet gate, not attempted.

Exact Microseconds saved:
- Proven gameplay frame saving: 0 us. Runtime code path not changed.
- Static modeled clipping removed for RS082 tight surfaces; runtime proof still requires Unity/TMP capture.
- Bake contention avoided under CPU 100% / active `dotnet`; this is process safety, not a frame-time claim.

Integrator notes:
- RS082 remaining 65 warnings are expansion ratio only; no modeled 720p clipping remains in that package.
- Source data and generated hash artifacts are ready for the next safe monolith bake window.
- Next cycle should continue from refreshed `LORE_TEXT_BOUNDS_17-A_all.csv`, not stale report counts.

## 2026-06-03 - Agent 17-A Cycle 4: RS085 Celestial Ephemeris Public Bands

What was wrong:
- `RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS` produced 851 static issue flags across 5 packets and 15 locales.
- After draft-prefix reduction, remaining modeled clipping was concentrated in `title`, `scanner`, and `terminal` fields.
- Full all-lore backlog needed refreshed counts after astronomy source edits.
- Monolith bake was unsafe: CPU was 63% and `dotnet` process `28544` was active.

What was done:
- Ran focused RS085 verifier before edits:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS085_before.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS085_before.csv`
- Applied deterministic source reductions in `Docs/Lore/AppliedContent/packets/RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS.packets.json`:
  - Long draft placeholder prefixes collapsed.
  - Public astronomy titles compressed to single-line labels.
  - Scanner summaries compressed to route/band statements.
  - Terminal summaries compressed to hard ephemeris ownership rules.
  - External site and field note prose left untouched because they were not flagged.
- Ran focused RS085 verifier after compact pass:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS085_after_compact.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_RS085_after_compact.csv`
- Regenerated applied-lore derived data: 460 packets, 6900 localized rows.
- Refreshed full lore report:
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.json`
  - `Docs/Reports/LORE_TEXT_BOUNDS_17-A_all.csv`
- Rechecked project hash catalog:
  - 1243 records
  - generated C# up to date
  - 0 collisions

Cinematic Cheats used:
- Used cold static bounds simulation instead of live terminal/HUD iteration.
- Converted verbose astronomy guidance into compact route labels and ownership rules.
- No runtime UI scan, no scene search, no build under active compiler load.

Verification:
- RS085 before: 5 packets, 525 surface checks, 851 issue flags, 0 collisions.
- RS085 after draft-prefix pass: 279 issue flags, 0 collisions.
- RS085 after compact pass: 41 title expansion warnings, 0 modeled line/word clipping flags, 0 collisions.
- Full all-lore after cycle 4: 460 packets, 48,300 surface checks, 60,196 static issue flags, 0 applied-lore hash collisions.
- Project FNV catalog: 1243 records, 0 collisions, C# output up to date.
- Current `static_data.h8bin` SHA-256 unchanged: `A3B4510B6D30A8A71FCF02726335D6049554C1D91FC7A8B45161ECB95F5BC971`.
- Monolith bake: BLOCKED by CPU/dotnet gate, not attempted.

Exact Microseconds saved:
- Proven gameplay frame saving: 0 us. Runtime code path not changed.
- Static modeled clipping removed for RS085 tight surfaces; runtime proof still requires Unity/TMP capture.
- Bake contention avoided under CPU 63% / active `dotnet`; this is process safety, not a frame-time claim.

Integrator notes:
- RS085 remaining 41 warnings are expansion ratio only; no modeled 720p clipping remains in that package.
- Source data and generated hash artifacts are ready for the next safe monolith bake window.
- Next cycle should continue from refreshed `LORE_TEXT_BOUNDS_17-A_all.csv`.
