# LOG - DIEGETIC_LORE_SCANNER

Status: PENDING VERIFICATION
Domain: ECHELON 8 - PRESENTATION & UX
Prompt: Spatial Hashing Scanner UI
Date: 2026-05-14

## Final Report

What was wrong:
- Scanner targeting depended on continuous focused ray work before target selection. That burns CPU on i3/MX350 for a problem that only needs near-crosshair forgiveness.
- Scanner UI risked managed string churn if decryption text used normal TMP `.text` paths.
- Lore completion needed decoupled campaign/lore notification instead of direct service dependencies.
- Build verification is blocked by unrelated global missing contracts before scanner-specific compiler proof is reachable.

What was done:
- Replaced scanner lore acquisition authority with a DataVault-backed SOA path: `LoreEntityAUPs` plus `LoreEntityHashes`.
- Added Burst candidate selection by highest forward dot under 15m, using AUP-relative vectors and range-squared rejection.
- Kept exactly one dispatcher `RaycastCommand` after candidate selection for occlusion.
- Replaced occlusion setup `Vector3.magnitude` plus direction division with `math.lengthsq`, `math.rsqrt`, and multiplication.
- Added/preserved `LoreFragmentScannedSignal(Hash)` and progression signal publication for DAG consumers.
- Kept active scanner UI on dispatcher/late-frame lanes and wrote scanner RT text through fixed buffers, `Span<char>`, `ZeroGCFormatter.FastIntToChars`, and TMP `SetCharArray`.
- Low/Unknown/MX350 presentation disables title scrambling and displays numeric percentage only.
- Re-read the assignment after core work and executed OMEGA polish only after core tasks were complete or blocked.

Cinematic Cheats used:
- Highest-dot auto-aim fake replaces pixel-perfect screen/collider selection.
- One-ray occlusion replaces ray fans and continuous authoritative ray probing.
- Percentage-only low-tier scanner output replaces animated glyph scrambling.
- Deterministic title scramble is a visual fake; it does not affect scan authority.

Exact Microseconds saved:
- Verified exact microseconds: PENDING VERIFICATION. Unity profiler/console is unavailable and `dotnet build Hecton8.Core.csproj` is globally blocked.
- Estimated removed work per active scanner resample: continuous forward raycast before candidate selection replaced by Burst SOA dot loop plus at most one occlusion command.
- Estimated OMEGA polish saving per candidate occlusion request: one sqrt and one vector divide removed.
- Estimated UI allocation saving during active scanner RT write: managed allocation avoided by `Span<char>`/fixed buffer path; exact allocator delta PENDING VERIFICATION.

Verification:
- `git diff --check` on touched scanner/status/rationale files: pass, only line-ending warnings.
- `rg ScannerManager` under project scripts: no matches.
- `rg Physics.Raycast` in scanner tool: no matches.
- `rg void Update(` in scanner tool, diegetic display, and scannable target: no matches.
- `rg .text =` in scanner UI/tool/target files: no matches.
- `dotnet build Hecton8.Core.csproj`: failed with 135 unrelated global dependency errors including missing `Hecton8.Environment.Fluids`, audio virtualization/propagation, CCD, persistence, macro database, `IGroundRadarService`, and related contracts.
- Unity MCP console: unavailable, `no_unity_session`.

Final Git Diff:
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/ScannerTool.cs`
- `Docs/AgentLogs/Rationale_DIEGETIC_LORE_SCANNER.md`
- `Docs/Tasks/Status_DIEGETIC_LORE_SCANNER.md`
- `Docs/AgentLogs/LOG_DIEGETIC_LORE_SCANNER.md`

## Follow-Up Hardening Pass

What was wrong:
- The previous report over-attributed dirty core diffs. Current workspace includes unrelated `GlobalSignals.cs` and `H8Memory.cs` changes from parallel work.
- Scanner RT title rendering still had avoidable registry lookup cost during progress repaints.
- Lore SOA sync rewrote all registered lore AUP/hash slots on every buffer request, including duplicate same-frame reads.

What was done:
- Added a fixed `char[96]` scanner-title cache in `ToolDiegeticDisplayController`, keyed by artifact hash.
- Added same-frame AUP/hash SOA sync debounce in `ScannableTarget`.
- Re-checked scanner authority path against prompt, AGENTS, domain map, Unity MCP skill, source, and actual diffs.
- Re-ran static checks for scanner/UI/target edited files.

Cinematic Cheats used:
- Kept the highest-dot target lie and one-ray occlusion path.
- Kept low-tier percentage-only display.
- Improved cached title scramble path without adding per-frame managed strings.

Exact Microseconds saved:
- Verified exact microseconds: PENDING VERIFICATION.
- Estimated saved work: repeated 1024-entry title lookup removed after first active artifact resolve.
- Estimated saved work: duplicate same-frame 1024-target AUP/hash rewrites avoided.
- Build/profiler proof remains blocked by global compile dependencies and unavailable Unity MCP session.

Verification:
- `git diff --check` on scanner/UI/target edits: pass, line-ending warnings only.
- `rg foreach`, `.ToString(`, `.text =`, `void Update(`, and `Physics.Raycast` over scanner/UI/target files: no hot-path regression hits.
- `dotnet build Hecton8.Core.csproj`: still fails on global missing contracts, latest observed count 128 errors.
- Unity MCP `validate_script`: transport/session unavailable at `127.0.0.1:8088/mcp`.
