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

## Follow-Up Hardening Pass 2

What was wrong:
- Scanner acquisition still used the tool transform as the primary forward source, which can diverge from the player camera/crosshair.
- Focused scanner resampling used a single cadence across low-end and high-end tiers.
- Prompt re-extraction logic needed to tolerate role/chat attributes on the XML tag.

What was done:
- Changed focused scanner acquisition to use `GlobalRegistry.Player.PlayerCamera.transform` first, with cached tool transform fallback.
- Added `ResolveFocusedScanResampleInterval()` so Low/Unknown/MX350 resample slower and High/Ultra can resample tighter without increasing occlusion count.
- Re-extracted the full DIEGETIC_LORE_SCANNER prompt with an attribute-safe regex.
- Re-ran scanner-path static checks and a post-patch build attempt.

Cinematic Cheats used:
- Kept the camera-space highest-dot selection as the target lie.
- Kept exactly one `RaycastCommand` after candidate selection.
- Scaled cadence, not authority complexity, between toaster and high-end devices.

Exact Microseconds saved:
- Verified exact microseconds: PENDING VERIFICATION.
- Estimated low-end saving: fewer focused acquisition resamples on Low/Unknown/MX350.
- Estimated high-end spend: tighter resample cadence for responsiveness, still bounded to one selected candidate and one occlusion check.

Verification:
- `git diff --check` on scanner/UI/target/status/rationale/log: pass, line-ending warnings only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- `dotnet build Hecton8.Core.csproj`: still fails globally, latest observed count 132 errors.
- Build output filtered for `ScannerTool.cs`, `ScannableTarget.cs`, and `ToolDiegeticDisplayController.cs`: no matches.

## Follow-Up Hardening Pass 3

What was wrong:
- Low/MX350 scan cadence was slowed, but contact grace still used the serialized base interval, risking progress dropouts between resamples.
- The scheduler recalculated the effective resample interval more than once in a single acquisition pass.
- The non-RT scanner summary path could still perform repeated lore title registry scans for the same active target.

What was done:
- Made scan hold timeout derive from `ResolveFocusedScanResampleInterval()`.
- Resolved the effective resample interval once per `ScheduleScientificConeBatch()` pass and reused it.
- Added last hash/index caching in `ScannableTarget.TryWriteLoreEntityTitle()`, with invalidation on resolved string refresh, lore register, and lore unregister.

Cinematic Cheats used:
- Preserved highest-dot camera-space target selection.
- Preserved exactly one post-selection `RaycastCommand`.
- Improved low-tier stability by timing, not by spending more physics work.

Exact Microseconds saved:
- Verified exact microseconds: PENDING VERIFICATION.
- Estimated saved work: one avoided duplicate tier lookup per acquisition pass.
- Estimated saved work: repeated same-target title lookup reduced from O(1024) worst case to O(1).
- Estimated low-tier benefit: stable held scan contact at slower resample cadence instead of raising sampling cost.

Verification:
- Prompt re-extracted cover-to-cover with attribute-safe XML regex.
- `git diff --check` on scanner/UI/target edits: pass, line-ending warnings only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- `dotnet build` verification remains pending: one filtered pass returned no scanner-file matches, but follow-up plain/minimal build behavior was inconsistent and a single-thread retry timed out. No leftover `dotnet` processes remained.

## Follow-Up Hardening Pass 4

What was wrong:
- `OnUnequip()` cleared scanner RT state through an inactive signal, but `OnDespawn()` and non-quit `OnDestroy()` could reset the scanner without publishing that inactive packet.
- Previous compile status was stale after the project graph recovered.

What was done:
- Added `PublishInactiveScannerTuningSignal()` with play-mode and application-quit guards.
- Replaced direct unequip inactive publish with the helper.
- Published inactive scanner state on despawn after focus reset.
- Published inactive scanner state on destroy only during play and only when not application quitting.

Cinematic Cheats used:
- No new simulation. This preserves the existing latest-signal UI lie and clears stale decryption presentation through the signal lane.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Runtime steady-state cost: 0 us; this only runs on unequip/despawn/destroy.
- Estimated UX gain: stale scanner RT lifetime reduced from indefinite until next scanner packet to one lifecycle signal.

Verification:
- Prompt re-extracted cover-to-cover with attribute-safe XML regex.
- `git diff --check` on scanner/UI/target edits: pass, line-ending warnings only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- Filtered `dotnet build Hecton8.Core.csproj`: exit 0, 0 error lines, 0 scanner-file matches.
- Plain `dotnet build Hecton8.Core.csproj`: Build succeeded, 0 warnings, 0 errors, elapsed 00:01:36.62.

## Follow-Up Hardening Pass 5

What was wrong:
- The diegetic scanner RT title cache was keyed by artifact hash only. Same-hash runtime title refresh or lore registry churn could leave stale decrypted title text in the fixed UI buffer.
- `GlobalSignals.cs` is dirty from other agents, so core latest-signal ordering was not a safe ownership target for this scanner pass.

What was done:
- Added `ScannableTarget.LoreTitleLookupVersion`.
- Incremented that version from the existing lore title cache invalidation path.
- Bound `ToolDiegeticDisplayController` scanner-title cache to artifact hash plus lore-title version.

Cinematic Cheats used:
- Kept the title cache as a fixed-buffer display fake. Runtime title changes now invalidate with an integer stamp, not a managed cache.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Steady added cost: one integer read/compare per title resolve.
- Avoided cost retained: same-target title repaint stays O(1) instead of repeated 1024-entry scans.

Verification:
- Prompt re-extracted cover-to-cover with attribute-safe XML regex.
- `git diff --check` on scanner/UI/target edits: pass.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- Filtered `dotnet build Hecton8.Core.csproj`: exit 0, 0 error lines, 0 scanner-file matches.
- Plain `dotnet build Hecton8.Core.csproj`: Build succeeded, 0 warnings, 0 errors, elapsed 00:00:42.03.

## Follow-Up Hardening Pass 6

What was wrong:
- Scanner active packets used the synthetic `SCNR` hash as `ToolHash`, while diegetic tool displays can be filtered by real runtime tool id.
- The signal dedup cache did not include tool hash, so a late runtime id correction could be suppressed.

What was done:
- `ScannerToolActiveSignal.ToolHash` now uses `RuntimeToolId` when nonzero, with `SCNR` fallback.
- Added `_lastPublishedTuningToolHash` to the scanner tuning signal dedup key.

Cinematic Cheats used:
- None added. This preserves the existing decryption display fake and makes the signal identity match the physical tool filter.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Added cost: one uint selection and one uint compare per late-frame publish attempt.
- Saved cost retained: no new UI polling, no manager, no direct object reference.

Verification:
- Prompt re-extracted cover-to-cover with attribute-safe XML regex.
- `git diff --check` on scanner/UI/target edits: pass, line-ending warning only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- Filtered `dotnet build Hecton8.Core.csproj`: exit 0, 0 error lines, 0 scanner-file matches.
- Plain `dotnet build Hecton8.Core.csproj`: Build succeeded, 0 warnings, 0 errors, elapsed 00:00:07.86.

## Follow-Up Hardening Pass 7

What was wrong:
- Scanner acquisition/progress had no scanner-local 300-frame black box. Invalid pose/progress could reach signals/UI with weak postmortem evidence.
- `CURRENT_BATCH.md` no longer contains the `DIEGETIC_LORE_SCANNER` prompt; it now contains other agents.

What was done:
- Added a fixed `NativeArray<ScannerBlackBoxEntry>[300]` scanner ring in `ScannerTool`.
- Ring entries record frame, runtime tool hash, artifact/blueprint hashes, active/pending lore hashes, progress, battery, dt, contact age, pending occlusion distance, tool pose, active probe, pending occlusion position, flags, and quality tier.
- Non-finite scanner state now writes finite fallbacks, publishes math-guard telemetry, and dumps `Docs/AgentLogs/Dump_DIEGETIC_LORE_SCANNER.bin` once.
- Scanner signal and scientific snapshot writes now sanitize progress/battery/density/toxicity/chemical/depth/direction values before display-facing state.
- Scanner summary and diegetic RT decryption reveal math now sanitize progress before percent/reveal calculations and dirty-state comparison.
- No-contact idle no longer counts as invalid just because `_scientificLastContactTime` starts at negative infinity.

Cinematic Cheats used:
- No extra physical truth. The scanner remains highest-dot fake targeting plus exactly one post-selection `RaycastCommand`; the new ring only records the lie and its state.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Added normal-path cost: estimated below 1 us per equipped scanner fast tick for one sequential native write plus finite branches.
- Added repaint-path cost: one finite branch before scanner decryption reveal math.
- Fault-path cost: one binary dump only after invalid state.
- Saved integration/debug cost: scanner postmortem no longer depends on transient UI or chat logs.

Verification:
- Raw CLI prompt extraction attempted; `DIEGETIC_LORE_SCANNER` absent from current batch, neighboring prompts ignored.
- `git diff --check` on scanner edit: pass, line-ending warning only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 8

What was wrong:
- Scanner tier decisions were scattered: signal publishing, low-tier decryption, and focused resample cadence each reached for `GlobalRegistry.ScalabilityTier`.
- The physical tool RT display had hysteresis but still polled the registry every UI tick.
- Display tick delta was clamped without an explicit finite gate.

What was done:
- Added `ScannerTool.ResolveScannerQualityTier()` with 0.5s probe cadence and 2s candidate hysteresis.
- Routed scanner signal tier, black-box tier stamp, low-tier decryption choice, and focused resample cadence through the cached scanner tier.
- Added a 0.5s quality-tier probe countdown to `ToolDiegeticDisplayController` while preserving its 2s low-tier hysteresis.
- Added `SanitizeSeconds()` for display tick delta before timer math.

Cinematic Cheats used:
- Tier changes are now deliberately sticky. Low-tier percentage display and High/Ultra visual-overkill scanner responsiveness change only after stable evidence, not transient tier noise.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Source-level `GlobalRegistry.ScalabilityTier` refs: `ScannerTool.cs` 3 -> 1; `ToolDiegeticDisplayController.cs` 2 -> 1.
- Runtime registry tier polling target: active display 60 Hz -> 2 Hz; active scanner tier reads are shared and probe-capped.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- `rg Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` over scanner/UI/target files: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 9

What was wrong:
- The previous scanner H-Phi tier fix still performed timed registry probes from helpers reached by active scanner/UI ticks.
- Focused scanner acquisition read `GlobalRegistry.Player` while resolving the held-scan camera pose.

What was done:
- `ScannerTool` now consumes `ScalabilityEvents` through `IScalabilityChangedEventListener`, queues tier candidates, and accepts them after the existing 2s hysteresis.
- `ToolDiegeticDisplayController` now consumes the same scalability event lane and removed its per-display tier probe countdown.
- Both scanner systems use `ISlowTickable` as a fallback for platform pressure overrides that currently do not raise scalability events.
- Scanner player-camera acquisition now uses a cached `IPlayerRuntimeContext` refreshed on Awake, OnSpawn, and OnEquip.

Cinematic Cheats used:
- Tier presentation remains a controlled lie: low hardware gets percentage/fallback after stable evidence; High/Ultra keeps richer scanner RT visual scalar without polling a global bus.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Active fast/late/UI tier registry polling: removed. Silent override fallback runs on SlowTick only.
- Focused acquisition: one `GlobalRegistry.Player` read removed per focused resample.

Verification:
- `git diff HEAD --check` and `git diff --cached --check` on scanner/UI edits: pass.
- Scanner/UI/target banned-pattern scan for `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- Direct registry reads remaining in scanner-owned files are cold lifecycle seed reads only.
- `dotnet build` / rebuild: NOT RUN by explicit user order.
