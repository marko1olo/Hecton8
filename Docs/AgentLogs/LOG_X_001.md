# LOG_X_001

## 2026-05-23 - Signal Monolith Audit And ABI Fence

What was wrong:
- `GlobalSignals.cs` is still a central bridge surface: 74 `NativeQueue<T>` fields, 141 direct flush invocations, 73 `CreateQueue` sites, and 523 `GlobalSignals` call sites.
- 403 signal-like payload structs exist; 169 are still nested in `GlobalSignals.cs`.
- Two hard DTO violations remain: `ToolEffectSignal` carries `Transform`; `PendingDurabilityCommand` carries `string`.
- The old `SignalBus<T>` ABI fence allowed only 16/32/64/128/192 byte payloads, rejecting valid 8-byte-aligned DTO sizes such as 24, 40, and 48 bytes.

What was done:
- Added Roslyn AST validator `Tools/SignalArchitectureOptimizationAuditX001`.
- Generated `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json` and `Docs/AgentLogs/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.md`.
- Reclassified legacy root scripts as `Legacy Root / Requires Owner Route Card` instead of laundering them into Core ownership.
- Patched `SignalBus<T>.HasValidPayloadStride()` to accept positive 8-byte-aligned payloads up to 192 bytes.
- Updated `Docs/Tasks/Status_X_001.md` and `Docs/AgentLogs/Rationale_X_001.md` with blocked route-card decisions.

Cinematic Cheats used:
- No physical simulation was added.
- Static storm model used cheap burst math: `capacity + 1` when capacity is known, otherwise 257 against default `LaneCapacity=256`.
- No managed hot-loop event path was introduced.

Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler or GCMonitor run was executed.
- Tool-time scan cost is outside frame time. The validator prevents unmeasured migration churn; it does not prove gameplay frame savings.

Verification:
- `dotnet run --project Tools/SignalArchitectureOptimizationAuditX001/SignalArchitectureOptimizationAuditX001.csproj -- --repo C:\hades\Hecton8` passed.
- Report summary: 2372 files scanned, 0 parse failures, 523 `GlobalSignals` call sites, 403 payloads, 2 hard payload violations, canonical hash `973c29f508747223dad454d34fd0be26c3c20a2017143d373af8ff4dbcc503c2`.
- Unity compile, runtime profiler, and GCMonitor proof were not run.

Blocked:
- Full contract extraction, producer/consumer rewiring, and dispatcher flush migration remain blocked until owner route cards exist for the affected lanes and the two hard payload violations are removed.

## 2026-05-23 - Managed Payload Follow-Up

What was wrong:
- `ToolEffectSignal` stored managed object references in the payload path.
- `PendingDurabilityCommand` stored `string ToolId` inside the queued command struct.
- These two findings blocked any honest claim that signal/command payload DTOs were clean.

What was done:
- `ToolEffectSignal` is now `[StructLayout(LayoutKind.Explicit, Size = 40)]` and stores primitive ids plus positions.
- `HabitatIntegrityManager` now matches tool effects by module instance id.
- `PendingDurabilityCommand` is now `[StructLayout(LayoutKind.Explicit, Size = 24)]`.
- Tool string identity was moved into `_queuedDurabilityCommandToolIds`, an owner-managed sidecar outside the command payload.

Cinematic Cheats used:
- No simulation was added.
- Used identity ids instead of object references to preserve gameplay matching with less payload weight.

Exact Microseconds saved:
- Verified runtime savings: 0us. No profiler proof was executed.
- Expected effect is contract cleanliness and future lane migration safety, not a claimed frame-time win.

Verification:
- Source-level `rg` checks confirm the edited payload fields no longer contain `Transform` or `string`.
- AST report rerun passed: 2373 files scanned, 0 parse failures, 0 hard payload violations, 7 layout warnings, canonical hash `18f6a27bd840c835cae400e4fed5f169ebc1025f24dd424b7273ca8e9e2fbe02`.

## 2026-05-23 - APEX Hidden Route And Capacity Audit

What was wrong:
- `GlobalSignals.Publish` is not gone. The APEX AST rerun proves 231 legacy publish sites remain across Core, Gameplay, AI/Biota, World/Environment, Habitat/Construction, UI/UX, QA, Prologue, Power, Audio, Animation, Physiology, Narrative, and legacy-root scripts.
- 181 signal lanes still carry centralization debt through direct flush, legacy queue creation, legacy publish, or legacy consume paths.
- Reactor damage has a typed `ReactorDamageSignal` lane, but related reactor/thermal/outgassing paths still publish legacy signals in `RadioisotopeThermalGenerator` and `ToxicOutgassingChemistryRuntime`.
- Hull deformation has typed `HullDeformedSignal` usage, but legacy hull/damage publishes remain in adjacent fauna/construction/environment paths.

What was done:
- Extended `Tools/SignalArchitectureOptimizationAuditX001` to emit all legacy publish sites, infer payload type from local identifiers and constructor expressions, tag damage/collision, hull, reactor, and airlock concern paths, and produce a 287-entry `signalLaneLedger`.
- Regenerated `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json` and `Docs/AgentLogs/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.md`.
- Recorded capacity tokens, low-tier frame caps, lane hashes, legacy publish counts, typed publish counts, overflow policy text, coalescing policy text, 5000-burst verdicts, and static zero-GC claim text per lane.
- Confirmed hard DTO managed-reference payload violations remain at 0 after the previous `ToolEffectSignal` and `PendingDurabilityCommand` fixes.

Cinematic Cheats used:
- No physical simulation was added.
- Replaced runtime storm execution with deterministic source-ledger math because Unity profiler/GCMonitor was not run in this pass.
- Coalescing proof is limited to lanes with explicit native merge semantics, including `CombatDamageSignal` and acoustic energy lanes; non-coalesced lanes are reported as bounded native drop/clear behavior.

Exact Microseconds saved:
- Verified runtime savings: 0us. This pass produced static source proof and documentation, not a Unity frame capture.
- Expected low-end benefit is avoiding unbounded managed storm behavior during future migrations; no measured frame-time claim is made.

Verification:
- `dotnet run --project Tools/SignalArchitectureOptimizationAuditX001/SignalArchitectureOptimizationAuditX001.csproj -- --repo C:\hades\Hecton8` passed.
- Report summary: 2373 files scanned, 0 parse failures, 403 payload definitions, 0 hard payload violations, 231 legacy publish sites, 0 unknown legacy publish payloads, 287 signal lanes in ledger, 181 centralization-debt lanes, canonical hash `480dd1942320675a360d5b141064dc2899816249440c4e842ed7e3f0f202ce76`.
- Unity compile, runtime profiler, and GCMonitor proof were not run.

## 2026-05-23 - APEX Source Reroute Follow-Up

What was wrong:
- 231 AST-confirmed `GlobalSignals.Publish` sites remained after the first APEX report.
- RTG heat/HUD warning traffic, construction pipe rupture/incursion, and high-frequency collision feedback still used the legacy central bridge despite having typed `SignalBus<T>` lanes.
- Some remaining damage/physiology/player-state publishes cannot be cut blindly because the current overloads update latest-cache data read by runtime consumers.

What was done:
- Rewired 21 safe legacy publish sites to `SignalBus<T>.TryPush`.
- Files touched by this pass: `RadioisotopeThermalGenerator.cs`, `FluidPipeGraphRuntime.cs`, `LogisticsPipeNode.cs`, `HectonPlayerMotor.cs`, `VehicleMotor.cs`, and `FaunaBrain.cs`.
- Rerouted payloads: `TemperatureChangedSignal`, `HUDNotificationSignal`, `PipeRuptureSignal`, `FluidIncursionSignal`, `ImpactSignal`, `HighSpeedImpactSignal`, `DebrisSpawnSignal`, `HapticRequest`, `AcousticPingSignal`, and `FaunaStateChangedSignal`.
- Left `CombatDamageSignal`, `PhysiologyStateSignal`, and `PlayerStateSignal` on legacy bridge where source inspection showed latest-cache consumers still exist.

Cinematic Cheats used:
- No simulation was added.
- Collision audiovisual feedback now enters typed snapshot lanes directly; high-volume impact lanes use existing native overflow/drop behavior rather than object/event fan-out.

Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler or GCMonitor run was executed.
- Static architecture delta: legacy publish sites reduced from 231 to 210; centralization-debt lanes reduced from 181 to 179.

Verification:
- `dotnet run --project Tools/SignalArchitectureOptimizationAuditX001/SignalArchitectureOptimizationAuditX001.csproj -- --repo C:\hades\Hecton8` passed after waiting for CPU/build guard clearance.
- Report summary: 2373 files scanned, 0 parse failures, 403 payload definitions, 0 hard payload violations, 210 legacy publish sites, 0 unknown legacy publish payloads, 287 signal lanes in ledger, 179 centralization-debt lanes, canonical hash `d3114dcb0291c66cb11be3ba6f9c74bf2b6741a98a8fe4cc8c11571cd7c7fbca`.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Unity import, Play Mode, runtime profiler, and GCMonitor proof were not run.

## 2026-05-23 - APEX Second Reroute / Final Audit

What was wrong:
- The prior report still had 210 legacy `GlobalSignals.Publish` sites. The claim "no hidden calls remain" would have been false.
- Damage/impact/acoustic/physiology/player/AUP/pause paths still contained bridge calls. Some are still required because their `GlobalSignals.Publish` overloads mutate latest-cache, sanitizer, sequence, or compatibility bridge state.
- Static storm proof existed, but the report hash was stale after concurrent compile-wall cleanup.

What was done:
- Rerouted additional owner-safe pass-through producers to `SignalBus<T>.TryPush` while preserving overloads with required side effects.
- Final X_001 audit now reports 75 legacy publish sites, 0 hard payload violations, 5 layout warnings, 1678 `SignalBus<T>` call sites, and 287 lane-ledger entries.
- Legacy publish grouping: `AcousticPingSignal` 21, `CombatDamageSignal` 9, `PhysiologyStateSignal` 7, `ImpactSignal` 5, `PlayerStressSignal` 4, `SimulationPauseSignal` 4, AUP/rebase/player/tool/survival/light/time/crafting/fluid bridge lanes making up the remainder.
- Fixed a concurrent `DebrisManager.cs` compile wall with fully qualified vault enum constants and a local rename, without touching debris behavior.

Cinematic Cheats used:
- No simulation was added.
- Storm handling remains bounded native shedding: no managed event fan-out, no queue growth, no per-signal object allocation in the static `SignalBus<T>` path.
- `CombatDamageSignal` coalesces below storm threshold by `TargetHash + DamageType + Channel`; collision/impact lanes use deterministic drop-oldest/snapshot cap and storm clear.

Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static architecture delta from this continuation: legacy publish sites reduced from 210 to 75. From the original APEX report: 231 to 75.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet run --project Tools/SignalArchitectureOptimizationAuditX001/SignalArchitectureOptimizationAuditX001.csproj -- --repo C:\hades\Hecton8` passed after CPU/build guard clearance.
- Final report summary: 2374 files scanned, 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 75 legacy publish sites, canonical hash `c70a4bb8fb5dd51905715692e31724b515dff617b61068996dd6fe05065c9c7b`.
- Runtime profiler/GCMonitor proof remains not executed; the zero-GC statement is static-source evidence only.
## 2026-05-23 - APEX Third Reroute Audit

What was wrong:
- The prior state still had 75 AST-confirmed `GlobalSignals.Publish` sites.
- `ImpactSignal`, `BrownoutSignal`, and `RebaseSignal` still had safe pass-through producers routed through the legacy bridge.
- `RebaseSignal` had typed producers after reroute but lacked explicit category-lane `SignalBus<RebaseSignal>.Configure(...)` capacity registration.

What was done:
- Rerouted 8 safe producer sites to typed `SignalBus<T>.TryPush`: `BrownoutSignal` in power, `RebaseSignal` in both headless QA runners, and `ImpactSignal` in seismic tide, fluid, structural leak, and tether paths.
- Added `RebaseSignal` typed lane registration: capacity `RebaseSignalCapacity`, max frame `RebaseSignalCapacity`, low-tier frame cap `16`, stable lane hash from `nameof(RebaseSignal)`.
- Reran `Tools/SignalArchitectureOptimizationAuditX001`; current report is `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json`, hash `d3f560003c18fe09e9ea8cea096637c28e90b38936fb87ef6a1638de76d7400f`.

Cinematic cheats used:
- None. This pass is architecture routing and native-lane proof only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or frame capture was executed.
- Static compile/audit effect: legacy publish sites reduced from 75 to 67; `SignalBus<T>` call sites increased from 1678 to 1690; hard DTO managed-reference violations remain 0.
- Build proof after third reroute: `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 97.57s after CPU/build guard clearance.

Remaining debt:
- 67 legacy publish sites remain by AST: `AcousticPingSignal` 21, `CombatDamageSignal` 9, `PhysiologyStateSignal` 7, `PlayerStressSignal` 4, `SimulationPauseSignal` 4, `AupPreShiftSignal` 3, `AupShiftSignal` 3, `SeismicSignal` 3, `PlayerStateSignal` 3, `ToolStateChangedSignal` 2, `SurvivalVitalsChangedSignal` 2, and six single bridge/latest lanes.
- These retained sites are not random leftovers; they are blocked by latest-cache, bridge-state, pause/time, or AUP ordering ownership that must be replaced by route-carded owner snapshots before removal.

## 2026-05-23 - APEX Fourth Reroute Static Audit

What was wrong:
- The prior state still had 67 AST-confirmed `GlobalSignals.Publish` sites.
- `FluidDensityChangedSignal` and `StorageDebtSignal` were still going through `GlobalSignals.Publish` even though source scan found no external consumers of their legacy latest/dequeue accessors.

What was done:
- Rerouted `HectonPlayerMovement` fluid density publication to `SignalBus<FluidDensityChangedSignal>.TryPush`.
- Rerouted `WorldChunkResidencyManager` storage debt publication to `SignalBus<StorageDebtSignal>.TryPush`.
- Regenerated `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json` and markdown sidecar. Current hash: `b5bea7ee97b664108c7ece8bb13153c8a1383ad1cef41bd11ef454e3829ed72e`.
- Applied a minimal unrelated compile-wall fix in `Visor/HectonScooterVolumetricShaftsFeature.cs`: added `using Hecton8.Environment;` for existing `HectonUnderwaterVisuals` references.

Cinematic cheats used:
- None. This pass is signal routing and compile-wall hygiene only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static architecture delta: legacy publish sites reduced from 67 to 65; `SignalBus<T>` call sites increased from 1690 to 1692; hard DTO managed-reference violations remain 0.

Verification:
- `Tools/SignalArchitectureOptimizationAuditX001` passed: 2375 files, 0 parse failures, 403 payloads, 0 hard payload violations, 5 layout warnings, 65 legacy publish sites, 288 lane ledger entries.
- `git diff --check` on touched files reported only LF/CRLF warnings.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` is not claimed after the fourth reroute. The first attempt found the unrelated Visor namespace compile wall and it was patched; rerun stayed blocked by CPU >50 percent and active `dotnet` processes.

Remaining debt:
- 65 legacy publish sites remain by AST: `AcousticPingSignal` 21, `CombatDamageSignal` 9, `PhysiologyStateSignal` 7, `PlayerStressSignal` 4, `SimulationPauseSignal` 4, `AupPreShiftSignal` 3, `AupShiftSignal` 3, `SeismicSignal` 3, `PlayerStateSignal` 3, `ToolStateChangedSignal` 2, `SurvivalVitalsChangedSignal` 2, plus `LightLevelSignal`, `TimeDilationSignal`, `CraftingCompletedSignal`, and `BulletTimeVisualSignal` single bridge/latest lanes.

---

## 2026-05-23 - X_001 APEX Fifth Route Pass

What was wrong:
- The previous state still had 65 AST-confirmed `GlobalSignals.Publish` sites.
- Several of those were not safe raw `SignalBus<T>.TryPush` replacements because they also maintained latest-cache, death-filter, AUP pause/release, pause/time bridge, bullet-time intensity, and crafting counter state.
- Direct `rg` after the bulk latest-cache pass exposed a smaller real runtime set: AUP, pause/time, crafting, and survival vitals, plus editor/test string probes.

What was done:
- Added latest accepted payload tracking to generic `SignalBus<T>`.
- Changed compatibility latest readers for damage, acoustic, light, player stress, player state, physiology, seismic, and tool state to delegate to typed `SignalBus<T>.TryGetLatest`.
- Added `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs` with explicit route wrappers:
  - `AupSignalRoute`
  - `SimulationSignalRoute`
  - `CraftingSignalRoute`
  - `SurvivalSignalRoute`
- Rerouted final runtime producers from `GlobalSignals.Publish` to typed routes while preserving side effects:
  - AUP pre/post shift dispatcher pause/release
  - pause-to-`SystemPauseSignal` mirror
  - time dilation scalar bridge
  - bullet-time visual scalar bridge
  - crafting sequence and unit counters
  - survival death-only latest state
- Fixed the route wrapper compile wall by importing `Hecton8.Core.Contracts.Signals`.
- Regenerated `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json` and `Docs/AgentLogs/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.md`.
- Updated `Docs/Tasks/Status_X_001.md` and `Docs/AgentLogs/Rationale_X_001.md`.

Cinematic cheats used:
- None. This pass is signal routing, not physical simulation or presentation fakery.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static architecture delta: AST `GlobalSignals.Publish` sites reduced from 65 to 0; `SignalBus<T>` call sites increased to 1752; hard DTO managed-reference violations remain 0.
- 5000-signal storm policy: static report confirms deterministic native clear/drop above `LaneOverflowFaultThreshold=1024`; `CombatDamageSignal` coalesces below threshold by target/damage/channel, `AcousticPingSignal` by channel/AUP meter cell.

Verification:
- `Tools/SignalArchitectureOptimizationAuditX001`: 2379 files, 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 304 `GlobalSignals` call sites, 0 `GlobalSignals.Publish` sites, 288 lane ledger entries, hash `bc01950dc414603239108b740aadfbd745afb9b011e3a99ebf40cbe5c9ebf48d`.
- Direct scan: `rg -n "GlobalSignals\.Publish" Assets/_Project/Scripts -g "*.cs"` returns only editor/test string probes.
- Compile: `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 69.28s.
- Diff hygiene: `git diff --check` reports only LF/CRLF warnings on touched files.

Remaining debt:
- `GlobalSignals.cs` still contains 169 payload definitions and 74 native queue fields. Contract extraction remains blocked by route ownership and asmdef dependency review.
- 304 `GlobalSignals` call sites remain, mostly read/consume compatibility surfaces. They are not publish producers.
- Runtime profiler/GCMonitor proof has not been run; all zero-GC claims here are static source claims only.

---

## 2026-05-23 - X_001 APEX Sixth Consumer Cleanup

What was wrong:
- The previous pass removed runtime `GlobalSignals.Publish`, but domain consumers still read clean latest/property state through `GlobalSignals`.
- Scanner-active latest fallback was stale-prone because `ScannerTool` already publishes through `SignalBus<ScannerToolActiveSignal>.Push`, while `GlobalSignals.TryGetLatestScannerToolActiveSignal` still read a legacy field updated only by the obsolete publish facade.
- `ProceduralOreSpawner` still used `GlobalSignals.Push` for item acquisition and resource depletion deltas.

What was done:
- Replaced safe latest readers with direct typed latest reads:
  - `SignalBus<AcousticPingSignal>.TryGetLatest`
  - `SignalBus<CombatDamageSignal>.TryGetLatest`
  - `SignalBus<LightLevelSignal>.TryGetLatest`
  - `SignalBus<PlayerStressSignal>.TryGetLatest`
  - `SignalBus<PlayerStateSignal>.TryGetLatest`
  - `SignalBus<PhysiologyStateSignal>.TryGetLatest`
  - `SignalBus<SeismicSignal>.TryGetLatest`
  - `SignalBus<ToolStateChangedSignal>.TryGetLatest`
- Added read facades on route wrappers for bridge-owned state:
  - `SimulationSignalRoute.TimeDilationScalar`
  - `SimulationSignalRoute.SimulationPaused`
  - `SimulationSignalRoute.BulletTimeVisualIntensity01`
  - `CraftingSignalRoute.LatestCompletedUnitCount`
  - `SurvivalSignalRoute.TryGetLatestDeath`
  - `ScannerSignalRoute.TryGetLatestActive`
- Changed `GlobalSignals.TryGetLatestScannerToolActiveSignal` to read `SignalBus<ScannerToolActiveSignal>.TryGetLatest`.
- Replaced `GlobalSignals.Push` in `ProceduralOreSpawner` with direct `SignalBus<ItemAcquiredSignal>.Push` and `SignalBus<ResourceDepletionDeltaSignal>.Push`.
- Regenerated `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json`.
- Updated `Docs/Tasks/Status_X_001.md` and `Docs/AgentLogs/Rationale_X_001.md`.

Cinematic cheats used:
- None. This pass is signal routing and deterministic state access only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Tool/audit wall time: about 22000000us.
- Build wall time: 47300000us.
- Static architecture delta versus fifth pass: `GlobalSignals` call sites reduced from 304 to 266; `SignalBus<T>` call sites increased from 1752 to 1785; hard DTO managed-reference violations remain 0.

Verification:
- `Tools/SignalArchitectureOptimizationAuditX001`: 2379 files, 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 266 `GlobalSignals` call sites, 0 `GlobalSignals.Publish` sites, 0 `GlobalSignals` consume sites, 1785 `SignalBus<T>` call sites, 288 lane ledger entries, hash `0c51f9089edf8d069c4b5c224d13cc72feca4ee78f810949e91a4d8dcca26cdc`.
- Direct scan: `rg -n "GlobalSignals\.Push|GlobalSignals\.Publish|GlobalSignals\.TryDequeue|GlobalSignals\.[A-Za-z0-9_]+Writer" Assets/_Project/Scripts -g "*.cs"` returns only editor/test string probes for `GlobalSignals.Publish`.
- Compile: `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 47.30s.
- Diff hygiene: `git diff --check` reports only LF/CRLF warnings on touched files.

Remaining debt:
- `GlobalSignals.cs` still contains 169 payload definitions, 74 native queue fields, 141 direct flush lane invocations, and 73 create-queue invocations. Contract extraction and phase split remain blocked by owner route cards and asmdef review.
- Cold bootstrap/dispose calls, dispatcher flush/clear, AUP origin helpers, and source-id folding still reference `GlobalSignals`; these are not hot publish/consume routes.
- `HectonEventBus` remains for mod/API/cold managed isolation and several economy/inventory/progression notifications; those are outside this hot `SignalBus<T>` corridor unless an owner route card reclassifies them.
- Runtime profiler/GCMonitor proof has not been run; all zero-GC statements remain static source claims only.

---

## 2026-05-23 - X_001 APEX Seventh Payload Extraction

What was wrong:
- `GlobalSignals.cs` still carried the DTO contract mass after hot producer/consumer cleanup.
- Direct domain-asmdef extraction is not safe yet because the consumer graph still compiles heavily through `Hecton8.Core`.

What was done:
- Moved 32 unmanaged payload structs out of `GlobalSignals.cs`:
  - `GlobalSignalPayloads.PhysicsInventory.cs`: impact, haptic, player state, survival vitals, player action, inventory, item, and radiation DTOs.
  - `GlobalSignalPayloads.UiSaveWorld.cs`: manual override, HUD, PDA scan/exchange, vehicle upgrade, thermal/battery, recon, save lifecycle, macro sector hydration, WFC outpost, save metadata, and compliance DTOs.
- Added both extracted files to `Hecton8.Core.csproj`.
- Fixed an unrelated compile-wall exposed by the first build attempt: `FaunaDirector.cs` needed `using Hecton8.Core.Contracts;` for `IDynamicResolutionRuntime`.

Cinematic cheats used:
- None. This is contract extraction only.

Exact microseconds saved:
- Verified runtime savings: 0us.
- Source ownership delta: quick source count now shows 137 `public struct ... : ISignal` definitions still in `GlobalSignals.cs`; 32 moved definitions are outside the monolith.

Verification:
- Static `rg`: moved payload names are no longer defined in `GlobalSignals.cs`.
- Static `rg`: `GlobalSignals.Push/Publish/TryDequeue/*Writer` still returns no runtime hot routes, only editor/test string probes for `GlobalSignals.Publish`.
- `git diff --check` on touched extraction files reports only LF/CRLF warnings.
- Full build/audit after the second extraction is not claimed. Repeated guard checks found CPU above 50 percent and active external `dotnet/csc`, so launching another build would violate AGENTS.

Remaining debt:
- 137 `ISignal` structs still remain in `GlobalSignals.cs` by quick source count.
- `GlobalSignals.cs` still owns direct native queue fields, create-queue calls, and flush invocations.
- True domain-asmdef extraction still requires route cards to avoid dependency cycles.

---

## 2026-05-23 - X_001 APEX Full Static DTO Extraction

What was wrong:
- Partial extraction was not enough: `GlobalSignals.cs` still held 137 signal-like DTO definitions after the first two payload files.
- That kept the file as a central contract warehouse even though runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` routes were already removed from external runtime code.

What was done:
- Moved the remaining core-foundation contract block into `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs`.
- Moved the remaining bottom payload block into `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`.
- Added both files to `Hecton8.Core.csproj`.
- Rechecked direct legacy hot routes: only editor/test string probes for `GlobalSignals.Publish` remain.
- Rechecked extracted payload field declarations: no `GameObject`, `Transform`, `string`, `FixedString*`, `NativeArray`, `NativeQueue`, `NativeList`, or `NativeHashMap` field declarations were found.
- Updated `Docs/Tasks/Status_X_001.md` and `Docs/AgentLogs/Rationale_X_001.md`.

Cinematic cheats used:
- None. This is source ownership extraction only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Source ownership delta: `GlobalSignals.cs` now contains 0 exact `public struct ... : ISignal` DTO definitions; `Core/Signals/GlobalSignalPayloads.*.cs` contains 168 exact DTO structs plus one `ISignalSnapshotTransformer<CombatDamageSignal>`.

Verification:
- Static `rg`: `GlobalSignals.cs` contains 0 exact `public struct ... : ISignal` definitions.
- Static `rg`: extracted payload files contain 168 exact DTO structs.
- Static managed-field scan: no managed field declarations in extracted payload files for the banned types listed above.
- Direct route scan: `GlobalSignals.Push/Publish/TryDequeue/*Writer` returns no runtime hot routes, only editor/test string probes for `GlobalSignals.Publish`.
- `git diff --check` on touched extraction files reports only LF/CRLF warnings.
- Full build/audit is not claimed. Latest guard check after waiting reported CPU 92.22 percent with active `csc` and `dotnet`, still above the 50 percent threshold, so launching `dotnet build` or Roslyn audit would violate AGENTS.

Remaining debt:
- `GlobalSignals.cs` still owns typed lane registry, bootstrap queue creation, dispatcher flush/clear orchestration, origin bridge helpers, sanitizer logic, and compatibility readers.
- True domain-asmdef extraction still requires route cards and dependency-cycle review.
- Runtime profiler/GCMonitor proof has not been run; zero-GC statements remain static source claims only.

---

## 2026-05-23 - X_001 APEX GlobalSignals Shell Split And Capacity Snapshot

What was wrong:
- `GlobalSignals.cs` was still a physical monolith after DTO extraction: bus registry/runtime, SPSC ring buffer, lifecycle/flush, native queue state, legacy facade, and writer bridge code still lived in one central file.
- The last capacity evidence existed in the Roslyn JSON, but its line references pointed at the pre-split `GlobalSignals.cs` body.
- Mechanical split introduced three compile-risk defects: extra close brace in lifecycle, orphan `RuntimeInitializeOnLoadMethod` attribute in legacy facade, and an unclosed namespace in `SpscSignalRingBuffer.cs`.

What was done:
- Left `Assets/_Project/Scripts/Core/GlobalSignals.cs` as a 12-line compatibility shell.
- Moved bus runtime and finite guards to `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`.
- Moved `SpscSignalRingBuffer<T>` to `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs`.
- Moved legacy publish/push/dequeue/latest facades to `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs`.
- Moved legacy `NativeQueue<T>.ParallelWriter` accessors to `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyWriters.cs`.
- Moved bootstrap/dispose/flush/telemetry/lifecycle code to `Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs`.
- Moved constants, native queue fields, latest bridge state, and read-only scalar properties to `Assets/_Project/Scripts/Core/Signals/GlobalSignals.State.cs`.
- Fixed the split fallout defects listed above.
- Added all extracted files to `Hecton8.Core.csproj`.
- Wrote `Docs/Reports/SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md`: 144 current `SignalBus<T>.Configure*` sites, 73 legacy `CreateQueue` slots that configure typed lanes, and 288 last-Roslyn ledger rows.
- Ran a guarded build once CPU dropped below 50 percent. It failed on unrelated `TraumaDispatcher.cs` missing two `BufferID` enum names; replaced them with local explicit `BufferID` constants 73398/73399 after static collision search.

Cinematic cheats used:
- None. This pass is source ownership, capacity evidence, and compile-wall hygiene only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static source delta: central shell reduced to 12 lines; exact DTO definitions in shell remain 0; external runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits remain 0.
- Build wall time before failure: 21100000us. The failure was outside the signal split path and is not a green compile proof.

Verification:
- Direct route scan: 0 external runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits.
- DTO scan: 0 exact `ISignal` DTO definitions in `GlobalSignals.cs`; 168 exact DTO structs in `Core/Signals/GlobalSignalPayloads.*.cs`.
- Managed DTO field scan: 0 field declarations of `GameObject`, `Transform`, `string`, `FixedString*`, `NativeArray`, `NativeQueue`, `NativeList`, or `NativeHashMap`.
- Brace-balance scan over `Assets/_Project/Scripts/Core/Signals/*.cs`: no deltas after fixes.
- `git diff --check`: only LF/CRLF warnings.
- Compile proof after the `TraumaDispatcher` fix is pending; latest CPU guard was 100 percent with active `csc` and multiple `dotnet` processes, so a second build would violate AGENTS.

Remaining debt:
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="X_001">`; local status/rationale preserve the recovered assignment, but the batch source is currently hygienically broken.
- Roslyn audit JSON still has pre-split line references and must be rerun when build guard permits.
- Runtime profiler/GCMonitor proof is absent; zero-GC statements remain static source claims only.

---

## 2026-05-23 - X_001 APEX DTO Duplicate/String Scrub And Capacity Ledger Refresh

What was wrong:
- `Hecton8.Modding.FutureCommandSandboxValidator` defined public `HapticPulseSignal` and `SubtitleCueSignal` DTOs with layouts different from the Core haptic and UI subtitle contracts.
- `MockPlayerFootstepSignal` carried `FixedString64Bytes SurfaceName`, which is unmanaged but still string-like identity in a signal payload.
- The capacity report still mixed the pre-rename haptic/subtitle lane names.

What was done:
- Renamed modding DTOs to `ModHapticPulseSignal` and `ModSubtitleCueSignal`.
- Updated every local `SignalBus<T>`, `NativeQueue<T>.ParallelWriter`, `UnsafeUtility.SizeOf<T>()`, and enqueue construction site in `FutureCommandSandboxValidator.cs` for those two lanes.
- Replaced `MockPlayerFootstepSignal.SurfaceName` with `SurfaceHash` plus explicit padding, preserving the 128-byte signal ABI.
- Regenerated `Docs/Reports/SIGNAL_DTO_MANAGED_REFERENCE_AUDIT_X_001.md`.
- Regenerated `Docs/Reports/SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md` with current configure locations and separated mod/core haptic/subtitle lanes. Wrote `Docs/Reports/SIGNAL_DOMAIN_HOT_ROUTE_AUDIT_X_001.md` for targeted domain-folder hot-route proof.

Cinematic cheats used:
- None. This is signal DTO hygiene and static audit/reporting only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static copy-risk reduction: removed one 64-byte fixed string field from a signal DTO and replaced identity with a 4-byte FNV-style hash field while preserving ABI padding.

Verification:
- Direct route scan: 0 runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits outside Editor.
- DTO audit: 292 runtime `ISignal` structs, 0 managed/string/native-container field violations, 0 duplicate signal names, 0 layout warnings.
- Capacity snapshot: 248 runtime `SignalBus<T>.Configure` sites, 224 unique configured typed lanes, 73 legacy native queue compatibility slots. Domain audit: 11 scanned domain folders, 0 legacy hot-route hits, 110 non-hot helper/read/bootstrap `GlobalSignals.` hits.
- Diff hygiene on touched files reports only LF/CRLF warnings.
- Build not run: latest guard showed CPU 100 percent with seven active `dotnet` processes, so launching another `dotnet build` would violate AGENTS.

Remaining debt:
- Post-split green compile is still pending.
- Roslyn audit JSON still has pre-split line references until build/tool guard permits rerun.
- Runtime zero-GC proof remains absent; static source only.

## 2026-05-23 - X_001 APEX CENTRAL QUEUE REMOVAL PASS

What was wrong:
- `GlobalSignals.RuntimeLifecycle.cs` still had 73 stale `CreateQueue(ref _*Signals...)` bootstrap calls and 73 stale `DisposeQueue(ref _*Signals...)` shutdown calls after the backing `NativeQueue<T> _*Signals` fields had been removed.
- `GlobalSignals.LegacyFacade.TryDequeueProgressionEvent` consumed `SignalBus<ProgressionEventSignal>` twice through the same frame cursor.
- Fresh capacity ledger found 273 unique configured/prewarmed `SignalBus<T>` lanes outside Editor while `SignalBusRegistry` still had a 256-slot dispatch table.

What was done:
- Replaced the 73 legacy queue bootstrap calls with `RegisterLegacyLane<T>(capacity, nameof(T))` typed registrations.
- `RegisterLegacyLane<T>` now calls `SignalBus<T>.Configure(...)` and `SignalBus<T>.EnsureInitialized()`, so legacy lanes prewarm as typed native lanes during bootstrap rather than allocating a second central queue or allocating on first gameplay signal.
- Removed the 73 stale `DisposeQueue(ref _*Signals...)` calls; shutdown now uses `SignalBusRegistry.DisposeAll()` plus existing telemetry/scratchpad cleanup.
- Fixed `TryDequeueProgressionEvent` to a single `SignalBus<ProgressionEventSignal>.TryConsumeFrame(out signal)` call.
- Raised `SignalBusRegistry.LaneCapacity` from 256 to 512 and updated cold-allocation comments.
- Regenerated `Docs/Reports/SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md` and `Docs/Reports/SIGNAL_DOMAIN_HOT_ROUTE_AUDIT_X_001.md`; appended fresh DTO verification to `Docs/Reports/SIGNAL_DTO_MANAGED_REFERENCE_AUDIT_X_001.md`.

Cinematic cheats used:
- None. This pass is signal bus architecture, cold dispatch capacity, and static audit proof only.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler, GCMonitor, or player capture was executed.
- Static/cold-path effect: removed duplicate central native queue ownership; increased three cold managed dispatch arrays from 256 to 512 slots to prevent registry overflow with the current 273-lane source envelope. No steady-state per-frame loop count increase beyond actual registered lane count.

Verification:
- Runtime hot-route scan: 0 runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits outside Editor; remaining hits are Editor/test string probes only.
- Core central queue scan: 0 hits for `NativeQueue<.*>\s+_.*Signals`, `CreateQueue(`, `DisposeQueue(`, `TryDequeue(ref _)`, and `OpenQueueForLegacyGlobalSignals` in `Assets/_Project/Scripts/Core/Signals`.
- Capacity report: 251 direct runtime `SignalBus<T>.Configure/ConfigureCacheLineCritical` sites, 73 legacy typed prewarm registrations, 273 unique configured/prewarmed lanes, 512 registry dispatch slots, 192 boot-prewarm upper-bound lanes, 0 central queue fields/refs.
- Domain report: 11 scanned domain folders, 0 legacy hot-route hits, 110 non-hot helper/read/bootstrap `GlobalSignals.` hits.
- DTO verification: 292 runtime `ISignal` structs, 0 managed/string/native-container field violations.
- Brace balance on touched Core signal files: 0 delta.
- Build not run: guard showed CPU 100 percent with active `csc` and multiple `dotnet` processes, so launching `dotnet build` would violate AGENTS.

Remaining debt:
- Post-removal green compile is pending until CPU/dotnet guard clears.
- Roslyn audit JSON still needs rerun after compile guard clearance for fresh line/hash proof.
- Runtime zero-GC remains a static-source claim only; Unity profiler/GCMonitor capture has not been executed.
## 2026-05-23 23:18 +04:00 - X_001 Legacy Facade Compile-Time Ban

What was wrong: `GlobalSignals.Publish/Push/TryDequeue/*Writer` had 0 external runtime callers, but the public central compatibility surface was still callable and could reintroduce the old hot route.

What was done: Added `[Obsolete(..., true)]` to 119 `Publish` overloads, 3 `Push` aliases, 84 destructive `TryDequeue*` methods, and 34 writer properties. Verified 0 unannotated central legacy hot declarations and 0 external runtime hot-route hits outside Core/Signals, Editor, and Tests.

Cinematic Cheats used: none. This is corridor hardening, not presentation work.

Exact Microseconds saved: 0us verified. No Unity profiler/GCMonitor capture was run. The gain is compile-time regression blocking and prevention of future central hot-route relapse.

Capacity/overflow proof delta: unchanged from the 23:18 capacity snapshot. Current source still has 251 direct configure sites, 73 typed legacy prewarm registrations, 273 unique configured/prewarmed lanes, 512 registry slots, deterministic drop-oldest below 1024, and native clear/drop telemetry above 1024. A 5000-signal storm is bounded by native clear/drop/coalescing policy, not heap growth.

Build status: not run. Latest guard reported CPU 100 percent with active `csc` and multiple `dotnet` processes, above the project build threshold and no-parallel-build rule.

## 2026-05-23 23:28 +04:00 - X_001 Managed Event Hotpath Audit

What was wrong: `HectonPlayerHealth` still had five public managed events in damage/heal/death/mutation paths despite zero runtime subscribers in source. `HectonSurvivalSystem` also had 16 unused managed vitals/injury/thermal/bleed events while already publishing `SurvivalVitalsChangedSignal`.

What was done: Removed `OnHealthChanged`, `OnDeath`, `OnDamageTaken`, `OnHealed`, `OnMutationFlagsChanged`, and their invoke sites from `HectonPlayerHealth`. Removed unused survival vitals/critical/injury/thermal/bleed events from `HectonSurvivalSystem`; retained `OnDeath` because PDA logbook subscribes. Wrote `Docs/Reports/SIGNAL_MANAGED_EVENT_HOTPATH_AUDIT_X_001.md` with the remaining selected-domain/survival C# events and 29 non-modding `HectonEventBus` cold/API hits.

Cinematic Cheats used: none.

Exact Microseconds saved: 0us verified. No profiler capture. Static cleanup removed five callback fields and nine invoke checks from health mutation paths, plus 16 survival callback declarations and 17 invoke checks from vitals/injury/thermal paths.

Rejected: mass-converting `HectonEventBus` managed API events without owner route cards; deleting live low-frequency transport/UI events with subscribers.

Build status: not run. CPU/build guard remains blocked by active `csc`/`dotnet`.

## 2026-05-24 - X_001 Item Lifecycle Managed Bus Cut

What was wrong:
- `ItemCollectedEvent`, `ItemRecycledEvent`, and `ItemDiscardedEvent` were first-party managed `HectonEventBus` payloads carrying `ItemData`.
- `EnvironmentalStrainManager` and `GlobalProfileManager` consumed those managed events for gameplay state.

What was done:
- Added `ItemLifecycleSignal` as a 64-byte unmanaged DTO.
- Added `ItemLifecycleSignalRoute` to publish hash/category/family/flag fields instead of managed item references.
- Configured `SignalBus<ItemLifecycleSignal>` with capacity 128, max frame 128, low-tier frame cap 32, direct flush/clear wiring, and finite guard.
- Rerouted collected/recycled/discarded item producers to the typed route.
- Rewired world/meta consumers to `SignalBus<ItemLifecycleSignal>.GetFrameSnapshot()` with local sequence cursors.
- Marked retired managed item event classes `[Obsolete(..., true)]`.
- Wrote `Docs/Reports/SIGNAL_ITEM_LIFECYCLE_TYPED_ROUTE_X_001.md`.

Cinematic Cheats used:
- None. This is signal routing and managed-reference removal only.

Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler/GCMonitor capture was run.
- Static source effect: four managed item event publish sites and five item event subscriptions/handlers removed from first-party runtime code.

Verification:
- First-party item event publish/subscribe scan outside `ModdingAPI`: 0 hits.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests: 0 hits.
- Non-modding `HectonEventBus` traffic outside Editor/Tests/ModdingAPI: 21 hits, down from 29.
- Touched-file brace balance: 0.
- `git diff --check`: LF/CRLF warnings only.
- Full Editor build is not claimed. Guarded build exceeded 120 seconds after `Hecton8.Core.dll` emission; retry is blocked by CPU 100 percent and external `dotnet` processes.
## 2026-05-24 - X_001 Progression Meta Managed Bus Cut

What was wrong: achievement unlocks and PDA advisories still used managed `HectonEventBus` classes with string payloads for first-party profile/difficulty decisions.

What was done: added 32-byte unmanaged `ProgressionMetaSignal`; added `ProgressionMetaSignalRoute`; configured `SignalBus<ProgressionMetaSignal>` at capacity 64/max-frame 64/low-tier 16; wired direct flush and clear; rewired `PlayerAchievementRegistry`, `PDAContextualAdvisorySystem`, `DynamicDifficultyDirector`, and `GlobalProfileManager`; retired managed achievement/advisory event classes with compile-time obsolete errors.

Cinematic Cheats used: none. This is control-plane signal hygiene, not visual simulation.

Exact Microseconds saved: 0us verified. Runtime profiler not run. Static expected gain is removal of first-party managed event object/string payloads in achievement/advisory meta flow.

Proof: runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests = 0; first-party achievement/advisory managed event publish/subscribe scan = 0; DTO poison scan over Core signal fields = 0; brace balance = 0; build blocked by seven active `dotnet` processes.
## 2026-05-24 - X_001 Survival Death And Biome Discovery Managed Event Cut

What was wrong: `HectonSurvivalSystem.OnDeath` and `HectonDiscoveryManager.OnBiomeDiscovered` still formed first-party managed callback routes after the central `GlobalSignals` hot path was cut.

What was done: `PDALogbookManager` now reads death from `SurvivalSignalRoute.TryGetLatestDeath`; survival `OnDeath` was removed. Biome discovery now publishes `ProgressionMetaSignal.KindBiomeDiscovered`; difficulty, profile, achievements, and logbook consume it from `SignalBus<ProgressionMetaSignal>`; discovery `OnBiomeDiscovered` was removed.

Cinematic cheats used: no simulation work added; reused hash-only meta and survival latest-state lanes.

Exact microseconds saved: 0us claimed. Static route removal only; no Unity profiler/GCMonitor run.

Proof: runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan = 0 hits outside Editor/Tests; retired first-party item/progression/death/biome managed event scan = 0 hits outside `ModdingAPI`; build blocked by active `dotnet`/`VBCSCompiler` guard.

## 2026-05-24 - X_001 Session Lifecycle Managed Bus Cut

What was wrong: `RunModifierController`, `GlobalProfileManager`, `DynamicDifficultyDirector`, `PlayerAchievementRegistry`, `PDALogbookManager`, `PDAContextualAdvisorySystem`, and `HectonOSBootManager` still consumed first-party save-load/player-spawn facts through managed `HectonEventBus` events. `PlayerInventory` also had one unmanaged non-modding `HectonEventBus.Publish` for physical drops with no source subscriber.

What was done: added 64-byte unmanaged `SessionLifecycleSignal`; added `SessionLifecycleSignalRoute`; configured `SignalBus<SessionLifecycleSignal>` capacity 16/max-frame 16/low-tier 8; wired direct flush/clear, direct lane recognition, finite guard, and contract id 134. Rewired seven first-party consumers to snapshot drains with local sequence cursors. `ModLoader` now emits typed lifecycle signals before the mod gate, while managed `GameLoadedEvent`/`PlayerSpawnedEvent` remains mod/API bridge only. Removed the dead inventory physical-drop event-bus publish.

Cinematic cheats used: none. This was signal route isolation, not simulation or presentation work.

Exact microseconds saved: 0us claimed. No Unity profiler/GCMonitor capture. Static source effect: first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is now 0, and first-party `GameLoadedEvent`/`PlayerSpawnedEvent` outside ModdingAPI is 0.

Capacity/overflow proof: `SessionLifecycleSignal` lane is 16 native entries, 16 max per frame, 8 low-tier cap. Lifecycle spam is deterministically bounded by `SignalBus<T>` native shedding/drop policy; no managed queue growth and no DTO managed fields.

Build status: guarded build ran twice. X_001 fallout was fixed. Current build still fails on 14 unrelated compile walls in `MainMenuController`, `HectonDirectorAI`, `ModSettingsRegistry`, `GameBootstrapper`, and `MesofaunaBehavioralStateMachine`.

## 2026-05-24 - X_001 Global Helper Route Demonolithization

What was wrong: the publish/consume path was clean, but runtime domains still used `GlobalSignals` as a helper and lifecycle utility bucket: AUP conversion, entity-id folding, queue initialization, flush, clear, and lane prewarm.

What was done: added `RuntimeOriginRoute` and rerouted 244 runtime helper call sites across 190 files. Added `SignalCorridorRuntime` and rerouted 20 lifecycle/phase/prewarm call sites across 18 files. Replaced the remaining external `PrologueReentrySignalLanes.Warm()` bootstrap call with `SignalCorridorRuntime.EnsureInitialized()`. Moved `SignalBusRuntime` pause reads through `SimulationSignalRoute`.

Cinematic cheats used: none. This is signal ownership cleanup, not visual simulation.

Exact microseconds saved: 0us verified. No Unity profiler/GCMonitor capture. Static effect: external runtime `GlobalSignals.` outside `Core/Signals` is now 0, and helper callers no longer depend on the central class name.

Proof: runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Editor/Tests = 0; external runtime `GlobalSignals.` outside `Core/Signals` = 0; `GlobalSignals.CurrentRuntimeOriginAup/TryRuntimePositionToAup/FoldEntityIdToSourceId` runtime references = 0; first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests = 0; Core signal DTO/route banned-field scan = 0.

Build status: not launched. CPU guard reported 96 percent after a 30-second wait, so `AGENTS.md` forbids `dotnet build`.

## 2026-05-24 - X_001 Bridge State Extraction And Impact Storm Closure

What was wrong:
- `GlobalSignals` still owned pause/time/bullet-time/crafting/survival-death bridge state after external hot callers were removed.
- `ImpactSignal` and `HighSpeedImpactSignal` did not coalesce same-cell storm facts before bounded overflow.
- 13 direct domain `SignalBus<T>.Configure` sites lacked immediate `EnsureInitialized`.
- Several collision/damage/acoustic producers still used `SignalBus<T>.Push` wrappers instead of explicit `TryPush`.

What was done:
- Added `SignalBridgeState` and moved bridge counters/latest state out of `GlobalSignals`.
- Rerouted `SignalBridgeRoutes` to `SignalBridgeState` and `SignalCorridorRuntime`; it now has 0 direct `GlobalSignals.` references.
- Marked 16 remaining central latest/bridge read facades `[Obsolete(..., true)]`.
- Added allocation-free coalescing for `ImpactSignal` and `HighSpeedImpactSignal` inside `SignalBus<T>`.
- Patched all 13 missing configure-prewarm sites; `missingEnsure=0`.
- Converted storm producer wrappers for impact/high-speed-impact/combat-damage/acoustic/deferred-submarine-impact lanes to direct `TryPush`.
- Removed the string-taking first-party session lifecycle route; `ModLoader` now computes a FNV-1a slot hash before publishing `SessionLifecycleSignal`.
- Wrote `Docs/Reports/SIGNAL_BRIDGE_STATE_AND_IMPACT_STORM_X_001.md`.

Cinematic cheats used:
- Impact storms are compressed by AUP meter-cell identity instead of simulating every redundant contact fact through consumers. This is deterministic visual/control signal compression, not physical truth mutation.

Exact microseconds saved:
- Verified runtime savings: 0us. No Unity profiler/GCMonitor capture was run.
- Static effect: same-cell impact bursts now coalesce in existing native frame snapshots; no managed dictionary, heap allocation, or queue growth is introduced.

Proof:
- External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- Runtime configure/prewarm ledger: 329 records, 277 unique typed lanes, 74 legacy prewarm registrations, 4 cache-critical lanes, 221 Core lifecycle registrations, 108 domain-local registrations.
- Storm-lane `Push` wrappers outside `GlobalSignals.LegacyFacade.cs`: 0.
- Storm-lane `TryPush` call sites: 74.
- DTO/route banned-field scan over Core payload/route/runtime-origin/bridge-state files: 0.

Build status:
- Not launched after this pass. Guarded checks reported CPU 53 percent, then 90 percent after a 20-second wait; no active `dotnet/csc/VBCSCompiler` process was present in the last process check, but CPU alone violates the project build threshold.

## 2026-05-24 - X_001 Registered Dispatch And Bounded Legacy Prewarm

What was wrong:
- `SignalBusRuntime` still used a hidden concrete DTO table for direct flush/clear dispatch.
- `SignalLanePolicyCache<T>.DirectRegistryDispatch` preserved a central type predicate.
- `SignalBus<T>.Configure(expectedCapacity, laneHash: ...)` inherited `maxFrameSignals=10000` and `lowTierFrameSignals=1000`, which allowed implicit legacy lanes to absorb 5000-signal bursts before frame-cap shedding.
- Eight lanes were still centrally prewarmed even though their domains already configured and prewarmed them.
- Six lanes were centrally prewarmed despite having no runtime source use outside generated hash constants.
- Sixteen direct Core lifecycle configure/prewarm pairs duplicated outside-Core domain owners.

What was done:
- Replaced fallback/direct DTO dispatch with a registered closed-generic `SignalLaneDispatch[]`.
- Removed `FlushDirectSignalLanes`, `FlushDirectSignalLane<T>`, `ClearDirectSignalLaneSnapshots`, and `DirectRegistryDispatch`.
- Changed implicit configure defaults so max frame cap resolves to expected capacity and low-tier cap resolves to quarter capacity.
- Updated `RegisterLegacyLane<T>` to pass explicit max/low caps.
- Removed central prewarm for `AcousticPingSignal`, `FluidDensityChangedSignal`, `FluidIncursionSignal`, `PhysiologyStateSignal`, `ProgressionEventSignal`, `SeismicSignal`, `SubmarineLightsChangedSignal`, and `ToolAcousticSignal`.
- Removed dead central prewarm for `DataReloadSignal`, `ItemDecaySignal`, `ReconDataSignal`, `SolarFlareSignal`, `SpectrumScanSignal`, and `WeatherStrengthSignal`.
- Removed duplicate Core lifecycle configure/prewarm pairs for 16 domain-owned lanes, including acoustic, physiology, respawn, seismic, compass, camera-juice, structural-warning, submarine-light, dynamic-music, and scalability lanes.
- Reordered inventory economy signal warmup into configure/prewarm pairs.
- Wrote `Docs/Reports/SIGNAL_REGISTERED_DISPATCH_AND_BOUNDED_PREWARM_X_001.md`.

Cinematic Cheats used:
- None. This was signal dispatch ownership and deterministic capacity bounding.

Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler/GCMonitor capture was run.
- Static effect: removed a central concrete DTO flush/clear list and changed implicit legacy storm caps from 10000/1000 to capacity/quarter-capacity.

Proof:
- Hardcoded direct-dispatch identifiers in `Core/Signals`: 0.
- Runtime `SignalBus<T>.Configure/ConfigureCacheLineCritical` refs: 241.
- Central legacy prewarm registrations: 59, down from 74.
- Core lifecycle configure refs: 131, with 0 overlap against outside-Core configured lanes.
- Core legacy prewarm overlap with outside-Core configured lanes: 0.
- Runtime configure sites missing immediate `EnsureInitialized`: 0.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.

Build status:
- Not launched. Guarded check reported CPU 96 percent with active `csc` PID 11192 and `dotnet` PID 21360.

## 2026-05-24 - Canonical Storm Lane Contract Closure

What was wrong:
- Cross-domain lanes could be initialized by first producer before owner configure.
- Local config calls used domain/source hashes for shared generic lanes (`AcousticPingSignal`, `ToolAcousticSignal`, `BubbleSpawnSignal`, `SubmarineLightsChangedSignal`, `AnomalyProximitySignal`).
- Reactor bridge initialized `BaseModuleCompromisedSignal` without the habitat deformation lane hash/capacity contract.

What was done:
- Added DTO-owned capacity/hash contracts for 17 shared/storm/respawn-inventory lanes.
- Added `SignalBus<T>` known-contract application before native queue/snapshot allocation.
- Normalized known-lane `Configure(...)` calls back to DTO contracts.
- Added direct `TryPush` pre-enqueue rejection at `_expectedCapacity`.
- Patched reactor, habitat, atmosphere, battery charger, seaglide, manta, gyro compass, metabolism, physiology, respawn, and inventory config/publish paths.
- Replaced remaining external `SignalBus<T>.Push` wrappers for the selected storm/cross-domain/respawn-inventory lane set with `TryPush`.
- Wrote `Docs/Reports/SIGNAL_CANONICAL_LANE_CONTRACTS_X_001.md`.

Cinematic Cheats used:
- No physical simulation added. The change keeps storm events compressed through existing grid/target coalescing and bounded VFX shedding instead of raising capacities.

Exact Microseconds saved:
- 0us verified. No Unity profiler/GCMonitor capture was run.
- Expected static gain: prevents accidental default-lane native initialization and false telemetry hashes under storm startup order.

Verification:
- External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- Selected storm/cross-domain/respawn-inventory `SignalBus<T>.Push` wrappers outside Core/Signals/Editor/Tests: 0.
- Selected storm/cross-domain/respawn-inventory `ParallelWriter` job producer opens: 10 native MPSC compatibility sites.
- Core signal DTO managed/string/native-container fields: 0.
- Touched-file brace delta: 0.
- Build skipped: CPU guard 56.7 percent.
## 2026-05-24 - TryPush Surface Closure

What was wrong -> The signal corridor had removed external `GlobalSignals` hot routes, but first-party code still used `SignalBus<T>.Push(...)`, a silent `void` wrapper over `TryPush(...)`. Editor smoke tests also asserted the old wrapper strings.

What was done -> Converted 169 external runtime `SignalBus<T>.Push` calls across 87 files to `TryPush`, converted 121 internal Core facade/determinism calls to `TryPush`, and updated editor probes. Current project-script scan for `SignalBus<...>.Push` is 0.

Cinematic cheats used -> None. This is routing surface hardening, not visual simulation.

Exact microseconds saved -> 0us verified. No profiler/GCMonitor capture was run. Static gain is relapse prevention and visible load-shed/drop semantics at producer call sites.

Verification -> External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside `Core/Signals`, Editor, and Tests: 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0. DTO managed/string/native-container field scan: 0. Build skipped under CPU guard at 100 percent.

## 2026-05-24 - Parallel Writer Budget Closure

What was wrong -> Job-side `SignalBus<T>.ParallelWriter` producers still had a storm gap. Main-thread `TryPush` rejected before enqueue, but parallel writers could call `NativeQueue<T>.ParallelWriter.Enqueue` without claiming a lane budget first. Flush-time shedding was deterministic too late: the native queue could already take storm pressure before the drop counter saw it.

What was done -> Added a lane-owned `NativeArray<int>[2]` budget/drop counter to `SignalBus<T>`, exposed `ParallelWriterBudget`, and added `TryEnqueueBounded(writer, budget, signal)` with atomic pre-enqueue budget claim. Migrated the first-party job-writer surface so every external runtime writer acquisition has a matching budget acquisition, then replaced selected signal writer `.Enqueue(...)` calls with bounded enqueue. Wrote `Docs/Reports/SIGNAL_PARALLEL_WRITER_BUDGET_CLOSURE_X_001.md`.

Cinematic Cheats used -> None. This is memory-route containment. The existing storm cheat remains deterministic coalescing/drop instead of simulating every redundant collision/damage/acoustic fact.

Exact Microseconds saved -> 0us verified. No Unity profiler/GCMonitor capture was run. Static gain is bounded native producer behavior before queue enqueue under 5000-signal bursts.

Proof -> External first-party writer acquisition sites: 57. Matching `ParallelWriterBudget` acquisition sites: 57. External first-party `TryEnqueueBounded` call sites: 60. Unique job-writer lane types: 47. Selected signal writer raw `.Enqueue(...)` hits after migration: 0. `SignalBus<...>.Push` source hits: 0. Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests: 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0. Core signal DTO managed/string/native-container field scan: 0.

Build status -> Not launched. Guarded check reported CPU 100 percent with 0 compiler processes; CPU alone violates the 50 percent threshold.

## 2026-05-24 - Local Sidecar And Residual Writer Budget Closure

What was wrong -> The central signal route was clean, but adjacent event/signal surfaces still had weak proof: Apex brain raw job writer enqueues, five growable `uint -> string` diagnostic/event sidecars, uncapped narrative/order string identity state, owner-local organic/KCC job queues without native producer budgets, and a retired gas toxicity writer that still needed hard no-enqueue proof.

What was done -> Patched 20 runtime files. Immediate configure/prewarm proof was restored across the remaining inspected domain owners. `ShinobuApexBrainJob` now receives native writer budgets for proximity/mock-damage/panic queues. Notification, Atlas decoded-message, Atlas directive-conflict, narrative discovery/audio-log, and pool diagnostic hash sidecars now use fixed arrays. Narrative discovery/order identity growth is capped. Organic drops and KCC mock input reject before native enqueue when their owner budgets are exhausted. Gas toxicity keeps the old writer field only as a retired interface and never enqueues. Wrote `Docs/Reports/SIGNAL_LOCAL_SIDECAR_AND_WRITER_BUDGET_CLOSURE_X_001.md`.

Cinematic Cheats used -> No new simulation cheat. Existing storm compression remains deterministic coalescing/drop instead of processing every redundant collision, damage, acoustic, or event fact.

Exact Microseconds saved -> 0us verified. No Unity profiler/GCMonitor capture was run. Static gain is bounded producer behavior and fixed sidecar memory under 5000-signal/event bursts.

Proof -> Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside `Core/Signals`, Editor, and Tests: 0. `SignalBus<...>.Push` source hits: 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0. Core signal DTO managed/string/native-container fields: 0. First-party raw `*Writer.Enqueue(...)` outside allowed helper/modding/editor/test contexts: 0. Configure/prewarm scanner: `ConfigureHits=243`, `MissingImmediateEnsure=0`. `git diff --check` on 20 touched runtime files: no errors, LF-to-CRLF warnings only.

Build status -> Not launched. Latest guard reported CPU 100 percent; latest compiler process scan returned no rows, but CPU alone violates the 50 percent threshold.

## 2026-05-24 - Queue Ingress Budget Closure

What was wrong -> The signal corridor was clean, but adjacent ingress still had weak overload proof: silent `ThreadSafeCommandQueue.Enqueue`, bootstrap `Dictionary<uint,string>` failure diagnostics, owner-local fluid/drone/vitals job queues without native budget claims, uncapped spawn promotion paths, pool return growth, retired gas toxicity writer residue, and stale pending counters after failed dequeues.

What was done -> Patched 20 runtime files. Added `ThreadSafeCommandQueue.TryEnqueue` with fixed pending/drop counters and overflow telemetry. Converted first-party command producers. Replaced bootstrap failure reason map with fixed slots. Added native pre-enqueue budgets to fluid rupture, drone task, and wrist vitals local writers. Capped resource/scavenge spawn ingress and ghost-proxy promotion. Bounded object/particle pool returns. Retired gas toxicity enqueue. Fixed stale pending counters in voxel and flora queues. Wrote `Docs/Reports/SIGNAL_QUEUE_INGRESS_BUDGET_CLOSURE_X_001.md`.

Cinematic Cheats used -> No new physical simulation. The policy remains deterministic coalescing/drop and owner-local budget claims instead of simulating or retaining every redundant event fact.

Exact Microseconds saved -> 0us verified. No Unity profiler/GCMonitor capture was run. Static expected gain is bounded queue ingress and no managed sidecar growth under 5000-signal/event bursts.

Proof -> Runtime scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `SignalBus<T>.Push`, and `ThreadSafeCommandQueue.Enqueue` outside Core/Signals/Editor/Tests/ModdingAPI: 0 hits. Scoped writer-budget scan shows matching `ParallelWriterBudget` and `TryEnqueueBounded` across inspected job-writer surfaces. Residual `Dictionary<uint,string>` hits are ModdingAPI cold bundle lookup and Quest cold compile/collision diagnostics only. `git diff --check` on tracked touched runtime files: no errors, LF-to-CRLF warnings only.

Build status -> One guarded build was launched at CPU 44.1 percent with 0 compiler processes. It timed out after 124 seconds with no diagnostic output from the shell wrapper. Orphaned MSBuild/Roslyn child nodes from that build were stopped by exact PID. Retry blocked: later guards report CPU above 50 percent and active `csc`/`dotnet`.

## 2026-05-24 - Local Event Counter Recovery

What was wrong -> The hot `GlobalSignals` route was still clean, but 34 owner-local event lanes had failed `TryDequeue` branches that could leave stale positive pending counters. Under a rare counter/queue desync that means false-full local lanes: future bounded enqueue can be rejected even after the native queue is empty or unrecoverable for that owner phase.

What was done -> Patched 48 runtime files. Failed dequeue branches now reset the matching pending counter immediately before `return` or `break`. Covered bootstrap, audio log, crafting, inventory, interaction, narrative, save, scan, localization, Atlas, module status, base integrity HUD, PDA intrusion, notification, airlock, submarine OS, player expression, weather, power telemetry, biome/celestial, ending, first-hour, eclipse, depth zone, soundscape, emergency relay, pool diagnostics, performance, MapMagic, suit mesh, player-signal, random-event, core command/registry, world chunk, sargassum, PDA, submarine atmosphere/electrolysis, spatial audio, repair drone, visor, and quest lanes. Wrote `Docs/Reports/SIGNAL_LOCAL_EVENT_COUNTER_RECOVERY_X_001.md`.

Cinematic Cheats used -> None. This is deterministic event-lane recovery. It preserves the existing policy of bounded local queues plus typed `SignalBus<T>` storm coalescing/drop instead of increasing capacities.

Exact Microseconds saved -> 0us verified. No Unity profiler/GCMonitor capture was run. Static expected gain is preventing false-full local event lanes under storm-shaped gameplay/UI/world event traffic.

Proof -> Patched files: 48. Full runtime counted-dequeue scanner after excluding prewarm/smoke-test loops: `TotalMissingCountedReset=0`. Brace delta scanner: no output. Runtime scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `SignalBus<T>.Push`, and `ThreadSafeCommandQueue.Enqueue` outside Core/Signals/Editor/Tests/ModdingAPI: 0 hits. DTO field scan over extracted payload/contract files: 0 managed/string/native-container field declarations. `git diff --check` on patched files: no errors, LF-to-CRLF warnings only.

Build status -> Not launched. Guard reported CPU 100.0 percent with active `dotnet`; this violates the 50 percent CPU rule and the compiler-process rule.

## 2026-05-24 - Contract And Native Queue Hardening

What was wrong -> Late `SignalBus<T>.Configure` could still mutate a live lane's capacity/hash after native initialization. Fatal job-side `PlayerFatalPressureSignal` publishing could bypass owner-phase fatal handling. `FluidIncursionSignal`, `ToxicityExposureSignal`, and `HabitatFloodAcousticMuffleSignal` had competing local configure values. Hydraulic erosion, anomaly flood-fill, and wreck propagation owner queues had admission gaps. Spatial audio captions carried semantic strings through a deferred sidecar.

What was done -> Patched 27 runtime/editor files. `SignalBus<T>` now rejects mismatched late configuration and rejects fatal lanes in `TryEnqueueBounded`. Respawn mock fatal pressure now publishes in owner phase with `TryPush`. Added DTO contracts for `DeflectSignal`, `DeconstructResultSignal`, `InteractionUiSignal`, `FluidIncursionSignal`, `HabitatFloodAcousticMuffleSignal`, and `ToxicityExposureSignal`. Added native budget/drop counters for `BurstCallback`, hydraulic height deltas, and anomaly deferred flood-fill. Bounded wreck propagation with explicit `PropagationQueueOverflow`. Banned legacy voxel/vehicle `Publish` aliases. Converted submarine caption ingress to hash-only `AudioCaptionEvents.TryRaiseHash` and removed the caption string sidecar.

Cinematic Cheats used -> Wreck propagation now fails closed with deterministic overflow state instead of growing the queue. Hydraulic erosion caps visual delta writeback by apply budget. Captions carry hash identity through the queue and resolve static strings only at UI edge.

Exact Microseconds saved -> 0us verified. No Unity profiler/GCMonitor capture was run. Static expected gain is bounded native queue pressure and no hot-adjacent managed caption sidecar under storm conditions.

Proof -> `Docs/Reports/SIGNAL_CONTRACT_AND_NATIVE_QUEUE_HARDENING_X_001.md`. `SignalBus<T>.Push`: 0. Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: 0 outside allowed zones. First-party non-modding `HectonEventBus.Publish/Subscribe/Unsubscribe`: 0. `ThreadSafeCommandQueue.Enqueue`: 0. `VehicleCommandSignalBus.Publish`, `VoxelChunkModifiedEvents.Publish`, and string-based `AudioCaptionEvents` ingress: 0. DTO banned-field scan: 0. `AudioCaptionPayload` banned-field scan: 0. Configure/prewarm heuristic: `MissingImmediateEnsure=0`. Touched-file brace delta: 0. `git diff --check`: LF-to-CRLF warnings only.

Build status -> Not launched. Guard reported CPU 100 percent with active `csc` and eight active `dotnet` processes; this violates both build guard conditions.
## 2026-05-24 - Try Surface And Hash Save Closure

What was wrong:
- `SaveEventPayload` still carried `FixedString64Bytes SlotName` and `FixedString128Bytes Message` through a deferred native queue.
- Several hot-adjacent first-party lanes still exposed void `Raise*`/`Publish*` producer names, hiding queue cap/ref-slot/listener absence from call sites.
- Airlock transition events, player trauma/interaction/tool-depleted events, DirectorAI threat/mission requests, Spectrum sonar events, soundscape/celestial/biome/atmosphere/acoustic events, and physics impact listener fanout did not return admission status.

What was done:
- Patched 30 targeted runtime/editor files.
- Converted `SaveEventPayload` to hash-only fields: `SlotHash`, `MessageHash`, `MessageSlot`.
- Added fixed `MessageSlot[16]` sidecar in `SaveEvents`; sidecar text is released after dispatch/drain.
- Added `SaveEvents.TryRaise*` methods that take precomputed hashes; old string `Raise*` methods are compile-time banned.
- Added `TryRaise*`/`TryNotify*` producer APIs for selected airlock/player/director/visor/soundscape/celestial/biome/atmosphere/acoustic/physics lanes and moved first-party call sites.
- Updated editor smoke probes to check the new `Try*`/hash-only save event route.

Cinematic cheats used:
- Save/load UI keeps exact failure text only as a fixed presentation sidecar; the queued fact is hash-only.
- Celestial sun-angle and planet-phase lanes retain existing latest-scalar coalescing instead of queuing redundant visual samples.
- Spectrum/player/airlock event bursts are bounded by existing small fixed native queues instead of capacity inflation.

Exact microseconds saved:
- 0us verified. Unity profiler/GCMonitor was not run.
- Static effect only: fewer silent DTO string carriers and visible bounded refusal at producer edges.

Verification:
- Selected old `Raise`/`Publish` call-site scan for this closure plus previously closed wrappers: 0 runtime hits outside wrapper declarations.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside Core/Signals, Editor, and Tests: 0.
- `SignalBus<T>.Push`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- `ThreadSafeCommandQueue.Enqueue`: 0.
- `SaveEventPayload` banned-field scan: 0.
- Core signal DTO banned-field scan: 0.
- `git diff --check`: no errors, LF-to-CRLF warnings only.
- Build not launched: guards reported CPU 52.9 percent with 0 compiler processes, then CPU 50.2 percent with eight active compiler processes, above the allowed build conditions.

## 2026-05-24 - Owner-Local TryRaise And Sidecar Closure

What was wrong:
- Selected owner-local lanes still exposed `void Raise*` producer methods after their queues had fixed capacities.
- `DepthZoneEvents` and `EmergencyServiceRelayEvents` used growable managed dictionaries as live-object sidecars next to deferred event queues.
- A missed voxel shockwave producer still called `RandomEventEvents.RaiseSeismicShockwave(...)`.

What was done:
- Patched 22 runtime files.
- Added `TryRaise*` / `TryRaise` APIs to eclipse gameplay, ending, first-hour, random-event, depth-zone, emergency relay, base integrity, tool effect, laser cutter, flashlight, PDA, suit mesh, power telemetry, and submarine OS event surfaces.
- Marked old selected wrappers `[Obsolete(..., true)]`.
- Updated all selected first-party call sites, including `HectonVoxelVolume`.
- Replaced depth-zone and emergency-relay dictionaries with fixed 32-slot sidecars.

Cinematic cheats used:
- No new simulation. The correction is bounded admission and fixed sidecar storage.
- Random seismic shockwave keeps the existing acoustic-ping side route, while the random-event listener queue now returns admission status.

Exact microseconds saved:
- 0us verified. No Unity profiler/GCMonitor capture was run.
- Static effect only: less hidden queue pressure and no managed dictionary growth in selected sidecars under storm load.

Proof:
- Report: `Docs/Reports/SIGNAL_OWNER_LOCAL_TRYRAISE_SIDECAR_CLOSURE_X_001.md`.
- Touched runtime files: 22.
- Selected old `Raise` call-site scan for converted lanes: 0 outside obsolete wrapper declarations.
- Selected `Dictionary` sidecar scan for depth-zone/emergency-relay: 0.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: 0 outside allowed zones.
- `SignalBus<T>.Push`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe`: 0 outside ModdingAPI/Editor/Tests.
- `ThreadSafeCommandQueue.Enqueue`: 0.
- Core signal DTO field scan: 0 managed/string/native-container field declarations.
- Touched-file brace delta: 0.
- `git diff --check`: no errors, LF-to-CRLF warnings only.

Build status -> Not launched. Latest guard reported CPU 37 percent but active `dotnet` PID 42500; the build-process guard blocks a new compile.
## 2026-05-25 - SIGNAL_TRYPUBLISH_DEFERRED_INGRESS_CLOSURE_X_001

What was wrong:
- Selected owner-local and typed-route-adjacent surfaces still exposed `void Raise/Publish/Notify` calls, hiding fixed-capacity enqueue refusal from producers.
- `MapMagicTerrainTileEvents` dispatched listener callbacks synchronously from the MapMagic bridge path and carried managed terrain/provider references directly through that callback surface.
- `PerformanceEvents`, `LocalizationEvents`, `ModuleStatusEvents`, `PlayerExpressionEvents`, and `MapMagicBiomeEvents` lacked complete producer-visible drop counters for their selected ingress paths.

What was done:
- Patched 24 runtime files.
- Added explicit `Try*` ingress for performance, localization, MapMagic biome/tile, module status, player expression, pool diagnostics, fluid splash, tether, geology telemetry, and HUD luminance paths.
- Marked selected old wrappers `[Obsolete(..., true)]` and updated first-party callers.
- Converted `MapMagicTerrainTileEvents` to a deferred `NativeQueue<MapMagicTerrainTileEventPayload>` with 16 fixed sidecar slots for managed tile snapshots and dispatcher flush/drop integration.
- Added missing drop counters and made `PlayerExpressionEventPayload` an explicit 8-byte unmanaged DTO.

Cinematic cheats used:
- MapMagic tile events now defer terrain/vegetation residency reactions to late-frame budget instead of synchronous producer callbacks.
- Geology telemetry remains scalar/hash packed into `GlobalTelemetryBus`; no physical geology simulation or managed text path was added.
- HUD fog luminance remains a single saturated scalar owner write with a `Try*` refusal surface.

Exact microseconds saved:
- `0us` verified runtime saving. No Unity Play Mode/profiler/GCMonitor capture was run.
- CLI proof: initial build hit `NETSDK1004` missing `project.assets.json`; guarded restore passed; guarded `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 63.15s.

Proof:
- Report: `Docs/Reports/SIGNAL_TRYPUBLISH_DEFERRED_INGRESS_CLOSURE_X_001.md`
- Selected old producer call sites: 0.
- External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: 0.
- `SignalBus<T>.Push`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- Core signal payload banned-field scan: 0.
- `git diff --check`: no errors; LF-to-CRLF warnings only.

## 2026-05-25 - SIGNAL_REFUSAL_TELEMETRY_CLOSURE_X_001

What was wrong:
- Runtime source still had storm-adjacent `TryEnqueueBounded(...)` calls where the bool result was ignored.
- The generic lane budget could drop safely, but reactor, hull/fluid, combat, fabrication, equipment, and inventory owners did not always record the local refusal.
- Some physical-domain wrappers returned `void`, hiding bounded refusal from first-party callers.

What was done:
- Patched 37 runtime/contract files.
- Added owner-local signal-overflow/drop flags to reactor, fluid ingress, hull integrity, submarine fluid/structural/atmosphere, thermodynamics hazard, cavitation, exosuit, KCC, vehicle, cable, ballast, and structural warning paths.
- Patched fabrication completion/tick, modular equipment depleted/overheat, inventory logistics transfer, ballistic damage, and combat deflect paths so bounded refusal increments existing counters or flags.
- Converted selected physical wrappers to explicit `Try*` surfaces where refusal is caller-visible.

Cinematic cheats used:
- No new simulation was added. The pass only exposes bounded refusal and preserves existing cheap visual/presentation paths.
- Cavitation cooldown now retries faster when both haptic/audio presentation signals refuse instead of pretending the full presentation happened.
- High-volume visual feedback remains scalar/hash packed; no string logs or managed sidecars were added.

Exact microseconds saved:
- 0us verified. No Unity Play Mode/profiler/GCMonitor capture was run.
- Static effect only: fewer hidden native queue accepts and owner-visible drop state during 5000-signal reactor/fluid/combat/fabrication/equipment/inventory storms.

Proof:
- Report: `Docs/Reports/SIGNAL_REFUSAL_TELEMETRY_CLOSURE_X_001.md`.
- Touched code files: 37.
- Runtime statement-level `TryEnqueueBounded(...)` outside Core/Signals/Editor/Tests/ModdingAPI: 0.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: 0 outside allowed zones.
- `SignalBus<T>.Push`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe`: 0 outside ModdingAPI/Editor/Tests.
- `ThreadSafeCommandQueue.Enqueue`: 0.
- Core signal DTO banned-field scan: 0.
- Touched-file brace delta: 0.

Build status -> Not launched. Latest guard reported `CPU=100 compiler_count=0`; AGENTS blocks `dotnet build` above 50 percent CPU.
