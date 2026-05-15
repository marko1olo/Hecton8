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

## Follow-Up Hardening Pass 10

What was wrong:
- Scanner Atlas presentation had been moved behind a cached helper, but the localization helper used a non-existent `ILocalizationService` local type.
- Operational summary/directive Atlas state needed verification that it now funnels through one cached ingress, not repeated presentation-path service locator calls.
- `ScannerTool` already implemented `IAtlasSignalEventListener`, but the equipped scanner lanes did not register/unregister that listener, so the callback could remain inert.

What was done:
- Corrected scanner localization resolution to use the actual project `LocalizationManager` type while keeping one registry property read per localized string resolve.
- Verified scanner Atlas reads now go through `ResolveCachedAtlasSignalCold()`; only the cold helper touches `GlobalRegistry.AtlasSignal`.
- Registered equipped scanners with `AtlasSignalEvents` and unregister on scientific lane shutdown, using `AtlasSignalEvents.IsRegistered()` to avoid unregister-miss spam if listener capacity rejects the scanner.
- Re-ran the raw batch prompt search and confirmed this agent tag is still absent from `CURRENT_BATCH.md`.

Cinematic Cheats used:
- No new physical truth. Atlas scanner text remains a presentation fake over cached signal state; target authority stays in the spatial-hash/highest-dot scanner path.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Atlas service refs in scanner presentation: constrained to one cold helper ingress.
- Atlas cache invalidation: event-driven while equipped, no extra per-frame polling.
- Localization helper: one registry property read per call, with compile-risk type fixed.

Verification:
- `git diff HEAD --check` on scanner/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 11

What was wrong:
- Diegetic tool RT slow-tick fallback could retry registration from UI `Tick()` every frame if the slow-tick lane was unavailable.
- Scanner pulse audio and threat-prediction lore checks still had duplicate service property reads.
- Scanner cached mode labels/summaries could survive a runtime language change in the old language.
- Cached Atlas/lore/player handles needed a hot-swap rebind path to avoid stale service references.

What was done:
- Added a 0.5s retry fence for failed `ToolDiegeticDisplayController` slow-tick registration; OnEnable/Start still force immediate registration attempts.
- Collapsed scanner ping audio to one local `GlobalRegistry.Audio` read.
- Added cached lore database resolution and equipped-scanner hot-swap listener rebinding for player, Atlas, lore, and localization service replacements.
- Added scanner localization-language listener; language changes refresh cached mode strings and invalidate operational text caches.

Cinematic Cheats used:
- No new physical truth. The scanner still uses spatial-hash/highest-dot target authority and one occlusion command; these edits only protect presentation/cache plumbing.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Failed slow-tick registration retry: worst-case 60Hz UI tick -> 2Hz retry.
- Audio pulse: one duplicate service property read removed per pulse.
- Threat prediction: one duplicate lore service property read removed per sampled threat hash.
- Localization/hot-swap handling: event-only while equipped, no per-frame polling.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 12

What was wrong:
- Scanner cached runtime-service handles could survive unequip, despawn, pool reuse, or service replacement while the scanner was not registered for hot-swap events.
- Diegetic tool display cached `RenderTexturePool` without a service replacement path.

What was done:
- Scanner now clears cached player, survival, Atlas, and lore handles on spawn/equip/unequip/despawn/destroy.
- Player service hot-swap now clears the cached survival component so scientific water/body metrics rebind to the current player.
- `ToolDiegeticDisplayController` now implements `IGlobalRegistryHotSwapListener`.
- On `RenderTexturePoolRuntime` replacement, the display releases any RT owned by the previous pool, binds the new pool, clears pool retry fallback, and marks rendering dirty.
- On disable, the display unregisters from hot-swap events and clears the cached pool handle.

Cinematic Cheats used:
- None added. This is lifecycle and ownership hygiene for the existing physical-tool RT display and scanner presentation caches.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Active scanner remains cache-based without reintroducing per-sample service polling.
- RT pool rebind is event-only; no per-frame pool lookup added.

Verification:
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 13

What was wrong:
- Scanner black-box dumps serialized the 300-entry ring in raw storage order. After wrap, timeline order was not oldest-to-newest.
- `ScannerBlackBoxEntry` was stored in a NativeArray and serialized as telemetry evidence without an explicit layout declaration.
- The physical scanner RT display cached successful lore-title lookups only; unresolved artifact hashes could trigger repeated title registry scans on progress repaints.

What was done:
- Added `_scannerBlackBoxRecordedCount` and ordered dump traversal in `ScannerTool`.
- Added sequential layout declaration to `ScannerBlackBoxEntry`.
- Added a versioned negative title-cache sentinel in `ToolDiegeticDisplayController`; misses retry only when `ScannableTarget.LoreTitleLookupVersion` changes.

Cinematic Cheats used:
- No new physical truth. The scanner still uses the controlled diegetic presentation path: low hardware falls back to percentage text, high tiers keep title/scramble visuals when the title exists.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Black-box normal path: one bounded integer increment per active scanner frame.
- Black-box fault path: ordered traversal only during one-shot dump.
- Unresolved artifact title path: repeated cold registry scans removed until title registry version changes.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, docs line-ending warnings only.
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 14

What was wrong:
- Scanner scientific metrics cached `HectonSurvivalSystem` on success, but an unavailable survival component could still trigger player-transform/component resolution on repeated active samples.

What was done:
- Added a 0.5s miss retry fence for survival-system resolution.
- Reset the retry timer when scanner runtime-service caches are cleared for equip/spawn/despawn/player replacement.

Cinematic Cheats used:
- No physical simulation added. Missing survival physiology continues to use deterministic fallback water/body metrics until the real component is available.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Missing survival component retry cadence: active scanner sample frequency -> 2Hz.
- Cached-success path remains one null check.

Verification:
- `git diff --check` on scanner/doc edits: pass, line-ending warnings only.
- `git diff --cached --check` on scanner/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 15

What was wrong:
- Scanner fast tick read time/frame values repeatedly inside one logical scientific scanner sample.

What was done:
- `FastTick` now snapshots `Time.time` and `Time.frameCount` once.
- Scientific scan update, focused resample scheduling, and black-box writes now share that tick timestamp/frame.

Cinematic Cheats used:
- No new simulation. This is deterministic presentation/acquisition hygiene for the existing scanner fake.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes roughly 3-5 repeated time/frame property reads per active scanner fast tick.

Verification:
- `git diff --check` on scanner/doc edits: pass, docs line-ending warnings only.
- `git diff --cached --check` on scanner/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- Focused scan time-read regression scan: no old `Time.time` contact/resample patterns remain.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 16

What was wrong:
- The physical scanner/tool display rechecked RGB565 support every time it reacquired a render texture.

What was done:
- Added `_renderTextureFormat` cached in `Awake()`.
- `EnsureRenderTexture()` now rents from the pool with that cached format.

Cinematic Cheats used:
- RGB565 remains the low-memory visual cheat for MX350-class hardware when supported; ARGB32 remains fallback.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes one `SystemInfo.SupportsRenderTextureFormat` platform capability probe per tool RT rent.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- RT format support scan: support query only in `ResolveRenderTextureFormatCold()`.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 17

What was wrong:
- Filtered physical tool displays could still let rejected scanner-active packets carry artifact/progress into scanner cache comparison, causing unnecessary dirty-state refreshes on unrelated scanner traffic.

What was done:
- Rejected scanner packets now map to artifact `0` and progress `0` before comparison.
- Accepted scanner packets keep the existing title/scramble path.

Cinematic Cheats used:
- None added. This keeps diegetic scanner visuals scoped to displays that intentionally accept scanner packets.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Avoids one dirty-state refresh per unrelated scanner artifact transition on filtered displays.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- Filter regression scan: rejected scanner packets use zero artifact/progress; no direct `signal.ArtifactHash` scanner-cache writes remain.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 18

What was wrong:
- Changing the physical display tool-hash filter did not reset consumed signal sequence IDs. A newly selected filter could miss the current latest tool/scanner packets until another signal arrived.

What was done:
- `SetToolHashFilter()` now clears scanner display state and resets `_lastSignalSequence` / `_lastScannerSignalSequence`.
- Scanner progress/artifact cache buckets are reset with the filter change.

Cinematic Cheats used:
- None added. This keeps diegetic screens responsive during spawn/equip rebinding without new signal lanes.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Runtime hot path unchanged; cold rebind now avoids a stale/fallback display wait.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =`: no matches.
- Filter rebind scan: sequence sentinels and scanner artifact/progress cache reset in `SetToolHashFilter()`.
- `dotnet build` / rebuild: NOT RUN by explicit user order.

## Follow-Up Hardening Pass 19

What was wrong:
- Physical tool display heat, battery, distance, ammo, fault, visual-overkill, and tool-hue values were written through global shader floats. Multiple physical screens could overwrite each other's visual state.

What was done:
- Replaced display-local `Shader.SetGlobalFloat` calls with a batched per-renderer `MaterialPropertyBlock` scalar update.
- Kept texture and low-tier fallback binding in the same existing property-block lane.

Cinematic Cheats used:
- Per-screen visual overkill remains a renderer-local presentation fake. No simulation, material cloning, or new service lane was added.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Replaces up to 9 global shader writes with one per-renderer property-block commit on changed display scalar state.

Verification:
- `git diff --check` on tool-display source: pass, line-ending warning only.
- `git diff --cached --check` on tool-display source: pass.
- Scanner/UI banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, `SetText(`, and `.text =`: no matches.
- Shader-state scan: no `Shader.SetGlobalFloat` or `ApplyGlobalFloat` remains in `ToolDiegeticDisplayController`.
- `dotnet build` / rebuild: NOT RUN.

## Follow-Up Hardening Pass 20

What was wrong:
- Focused scanner fast-tick work used a tick-level `now` snapshot, but quality-tier hysteresis still re-read `Time.time` through helper methods during the same scientific scan sample.

What was done:
- `ResolveFocusedScanResampleInterval()` now accepts the tick timestamp.
- `ResolveScannerQualityTier()` now accepts the caller timestamp and uses it for candidate-age hysteresis.
- Late-frame scanner signal publication snapshots time once before resolving signal tier.

Cinematic Cheats used:
- None added. This preserves the existing Math LOD scanner fake and makes its cadence decision deterministic inside the sample.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes 1-2 repeated engine time reads from active focused scanner ticks.

Verification:
- `git diff --check` on scanner/UI/doc edits: pass, line-ending warnings only.
- `git diff --cached --check` on scanner/UI/doc edits: pass.
- Scanner/UI banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, `SetText(`, and `.text =`: no matches.
- Time-threading scan: focused scan path uses `ResolveFocusedScanResampleInterval(now)` and `ResolveScannerQualityTier(now)`; old `Time.time - _scannerQualityTierCandidateSince` pattern is gone.
- `dotnet build` / rebuild: NOT RUN.

## Follow-Up Hardening Pass 21

What was wrong:
- Scanner tier initialization and tier-candidate updates still read `Time.time` inside helper methods. Scanner tuning signal payload also read `Time.frameCount` directly inside the object initializer.

What was done:
- `QueueScannerQualityTierCandidate()` snapshots time once and passes it through initialization/candidate stamps.
- `InitializeScannerQualityTier()` now receives caller time.
- `PublishScannerTuningSignal()` snapshots frame once and writes that value into `ScannerToolActiveSignal.Frame`.

Cinematic Cheats used:
- None added. This is deterministic timing hygiene for the existing tiered scanner presentation fake.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes up to 2 repeated engine time reads per tier candidate update and one direct frame read per scanner tuning packet.

Verification:
- `git diff --check` on scanner source: pass, line-ending warning only.
- `git diff --cached --check` on scanner source: pass.
- Scanner/UI banned-pattern scan for `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, `SetText(`, and `.text =`: no matches.
- Timestamp scan: no direct candidate-stamp `Time.time`, no no-time `InitializeScannerQualityTier(...)`, and no direct scanner active payload `Time.frameCount` write remain.
- `dotnet build` / rebuild: NOT RUN.

## Follow-Up Hardening Pass 22

What was wrong:
- Scanner operational text generation still mixed timestamps between cache-bucket selection, cooldown/last-result text, low-tier decryption gates, and high-tier title scramble.

What was done:
- `GetOperationalSummary()` snapshots `Time.time` and `Time.frameCount` once before cache/write work.
- `GetOperationalDirective()` snapshots `Time.time` once before cache/write work.
- Summary/directive writes now use timestamped internal helpers.
- Lore decryption summary receives caller `now`/`frame` for tier gating and scramble.

Cinematic Cheats used:
- Kept the existing title scramble fake; made its frame seed coherent per generated scanner line.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes 2-4 repeated engine time/frame reads per uncached operational text refresh.

Verification:
- Static verification pending final pass in this session.
- `dotnet build` / rebuild: NOT RUN.

## Follow-Up Hardening Pass 23

What was wrong:
- Focused scanner contact consumers still wrote `_scientificLastContactTime` from helper-local `Time.time` reads after the fast-tick scheduler had already captured a timestamp.

What was done:
- Voxel and spatial contact consumers now receive the scheduler timestamp.
- Queued occlusion raycast completion captures time at the callback boundary and passes it through lore-target consumption.
- `_scientificLastContactTime` now uses caller-provided time in lore, voxel, and spatial contact paths.

Cinematic Cheats used:
- None added. This is timing hygiene for the existing scientific scanner presentation path.

Exact Microseconds saved:
- Verified exact microseconds: PENDING PROFILER.
- Removes up to 3 helper-local engine time reads across active focused-contact acquisition paths.

Verification:
- Static verification pending final pass in this session.
- `dotnet build` / rebuild: NOT RUN.
