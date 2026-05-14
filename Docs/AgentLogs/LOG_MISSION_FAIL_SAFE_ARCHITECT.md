# LOG_MISSION_FAIL_SAFE_ARCHITECT

## 2026-05-14 - Outpost Quest DAG Fail-Safe Audit

Status: SCENARIO STABILIZED - PENDING UNITY VERIFICATION
Evidence Class: STATIC_DOC / STATIC_SOURCE

What was wrong:
- Requested `CURRENT_BATCH_OSHINO.md` is absent. Active prompt exists in `Docs/Tasks/CURRENT_BATCH.md`.
- Requested `Status_META_CAMPAIGN_DIRECTOR.md` is absent. Current source shows only four implemented global campaign variables, not a 20+ outpost variable set.
- Current WFC outpost source exposes `Empty`, `Corridor`, `Room`, `Hatch`, `Datapad`, `SealedDoor`, `Window`, and `Pillar`. It does not expose a `Generator` cell kind.
- A mission requiring a generated Generator room, a generated Datapad, or a safe gas room can soft-lock without a DAG fallback.

What was done:
- Created `Docs/Design/Missions/Outpost_Failure_Modes.md`.
- Authored 30 outpost DAG variables covering generation, connectivity, power relay repair, Ghost Power, gas safety, lore commits, deadlock revert, marker fallback, save restore, and mission completion.
- Wrote an edge-case matrix for WFC, power, gas, lore, save/load, signal, marker, and low-tier grid failures.
- Defined Ghost Power as a deterministic mission-only reserve-bus fallback. It powers only the relay, door, terminal, optional scrubber, and entry marker. It does not publish fake full-grid generation.
- Wrote 10 exact diegetic tooltip strings for the first outpost power-grid repair sequence.
- Wrote 5 exact Marauder log strings and bound each to required physical state.
- Re-read gas/O2 source constraints from `GasDynamicsSolver` and documented mission limits: no critical fire/breached rooms, 90 second cap in unpowered sealed rooms, scrubber removes CO2 only.
- Created and updated `Docs/Tasks/Status_MISSION_FAIL_SAFE_ARCHITECT.md`.
- Created and updated `Docs/AgentLogs/Rationale_MISSION_FAIL_SAFE_ARCHITECT.md`.

Cinematic Cheats used:
- Ghost Power reserve bus: mission fail-safe bit plus panel/audio/VFX presentation, not physical generator simulation.
- Scalar gas truth: room flags and kPa constraints instead of particle gas simulation.
- Brownout storytelling through relay flicker, hum, wet spark, and CRT decay by tier.
- Low-tier route stability through fallback terminal/marker instead of WFC retry loops.

Exact Microseconds saved:
- Measured runtime savings: 0 us. This pass changed documentation/status/log files only.
- Claimed profiler savings: none. No Unity Profiler, GCMonitor, Play Mode, or player build was run.
- Static design estimate for future implementation: Ghost Power should avoid any WFC retry or full-grid fake generation on the failure path; runtime target remains an O(1) quest flag check plus dirty visual update.

Regression Model:
- CPU: no runtime code changed. Future implementation must not add Tick polling or WFC retries for missing generator.
- GC: no runtime code changed. Future tooltip/log integration must use hashed localization keys and fixed char buffers.
- Memory: no runtime code changed. Future mission flags should compile into existing quest bit bands.
- Cadence: no runtime code changed. Future route decisions should occur on event signals, not per-frame scans.
- Correctness: static contract closes known no-Generator, unsafe-room, missing-marker, missing-datapad, and critical-item-loss soft-lock classes.

Verification:
- Static text scan found no non-ASCII in the authored mission/status/rationale files.
- Compile guard attempted: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Compile guard result: BLOCKED BY TOOLCHAIN. `dotnet` is not recognized in the current shell PATH.
- Unity Console, Play Mode, profiler, GCMonitor, save/load route, and visual proof remain PENDING VERIFICATION.

## 2026-05-14 - Outpost Handoff Addendum

Status: SCENARIO STABILIZED - PENDING UNITY VERIFICATION
Evidence Class: STATIC_DOC / STATIC_SOURCE

What was wrong:
- Tooltip and Marauder log text existed in prose, but a downstream integrator would still need to manually recover keys, hashes, trigger flags, and suppress flags.
- The initially observed `Data/Localization/en_US.json` path is not present in the current workspace. The active observed language source is `Assets/_Project/Scripts/English.json`.
- Editing only English localization data would leave generated `LocKeys` and translated language tables stale.

What was done:
- Added `Docs/Design/Missions/Outpost_FailSafe_Handoff.json`.
- The handoff contains 30 mission flags, topological order, fallback rules, 10 tooltip entries, 5 Marauder log entries, `LocHash`-compatible FNV hashes, and gas constraints.
- Updated `Docs/Design/Missions/Outpost_Failure_Modes.md` to point to the handoff and document why runtime localization assets were not mutated.
- Updated status and rationale with Decision 009 covering the handoff choice.

Cinematic Cheats used:
- Same Ghost Power reserve-bus fake as the main audit: deterministic mission truth with tiered panel/audio/VFX presentation.
- Same scalar gas truth: scrubber removes CO2 only; no O2 generation fiction and no particle gas simulation.
- Same route-marker fallback: authored flag and marker target instead of WFC retry loops.

Exact Microseconds saved:
- Measured runtime savings: 0 us. This pass changed documentation and handoff JSON only.
- Claimed profiler savings: none. No Unity Profiler, GCMonitor, Play Mode, or player build was run.
- Future runtime target remains O(1) hash/flag lookup after a proper localization/quest bake.

Verification:
- JSON parse/hash check: PASS. `Outpost_FailSafe_Handoff.json` parsed with `flags=30`, `locEntries=15`, `hashMismatches=0`.
- ASCII scan: PASS. Authored mission/status/rationale/log/handoff files returned no non-ASCII matches.
- Toolchain probe: BLOCKED. `dotnet` is absent from PATH and standard `Program Files` dotnet install paths.
- Polish mandate: NOT PRESENT in `Docs/Tasks/CURRENT_BATCH.md`.
- Unity Console, Play Mode, profiler, GCMonitor, localization bake, quest asset bake, and save/load route remain PENDING VERIFICATION.
