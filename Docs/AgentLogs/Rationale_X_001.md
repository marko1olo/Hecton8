# Rationale_X_001

Status: LOCAL EVENT COUNTER RECOVERY APPLIED / BUILD GUARD BLOCKED

## 2026-05-23 - Prompt And Authority Recovery

Problem: The launcher instructed reading `current_batch.md` from the project root, but no such file exists at `C:\hades` or `C:\hades\Hecton8`. The active assignment exists at `Docs/Tasks/CURRENT_BATCH.md`.

Solution: Used PowerShell CLI raw read plus an attribute-aware regex to extract only `<AGENT_PROMPT id="X_001">...</AGENT_PROMPT>`. Counted ten explicit task blocks, read the domain authority file, and loaded seven task-relevant mandates before any source edit.

Rejected Alternatives: Relying on chat-provided summary, neighboring prompts, or stale archived batch files would violate the batch prompt isolation rule and risk cross-agent architectural contamination.

Scalability potential: Low/MX350 requires bounded typed lanes with deterministic overflow and no centralized queue growth. Middle keeps full frame snapshots for normal gameplay fan-out. High and Ultra can add richer telemetry and visual-overkill consumers in `VISUAL_SYNC` without increasing gameplay truth broadcast cost.

Hardware Impact: Current work is static archaeology and state tracking only, 0us runtime impact. Expected migration target is lower compile-wall churn and bounded signal processing; no runtime microsecond claim is valid until source scans and profiler evidence exist.

## 2026-05-23 - Signal Architecture AST Audit

Problem: `GlobalSignals.cs` is not just a dead monolith; it now contains a hybrid bridge: typed `SignalBus<T>` lanes, direct `NativeQueue<T>` aliases, 141 direct flush invocations, and hundreds of producers still routing through `GlobalSignals`. Blind deletion would break unrelated domains and invalidate the one-fact/one-owner rule.

Solution: Added `Tools/SignalArchitectureOptimizationAuditX001`, a Roslyn AST CLI that scans `Assets/_Project/Scripts` and emits `Docs/Reports/SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json` plus a markdown sidecar. The report records 2372 scanned files, zero parse failures, 523 `GlobalSignals` call sites, 403 signal-like payload definitions, 169 payloads still nested in `GlobalSignals.cs`, 2 hard managed-field payload violations, and domain ownership buckets.

Rejected Alternatives: Reusing the older lexical `SignalBusContractAuditCli` as final proof was rejected because X_001 requires AST-grade receiver and payload ownership evidence. Mass search-replace rewiring was rejected because 231 publish sites cross AI, Gameplay, World, UI, Audio, Physiology, QA, and legacy root scripts without owner route cards.

Scalability potential: Low uses the report to cut only owner-approved lanes and prevent runtime bridge sprawl. Middle can migrate stable DTO lanes from `GlobalSignals` to owner contracts without changing gameplay truth. High and Ultra can add VISUAL_SYNC-only consumers and richer telemetry after capacity and overflow policies are explicit per lane.

Hardware Impact: Audit cost is editor/tool-time only, 0us runtime. It prevents low-end regressions by exposing managed payloads before they hit hot lanes. No frame-time savings are claimed until a Unity profiler/GCMonitor capture exists.

## 2026-05-23 - SignalBus Payload Stride Fence

Problem: `SignalBus<T>.HasValidPayloadStride()` accepted only 16/32/64/128/192-byte payloads. The ARM64 layout mandate requires natural 8-byte alignment, not artificial size buckets. The old whitelist rejects valid 24/40/48-byte blittable DTOs and pressures agents to add fake padding.

Solution: Changed the ABI fence to accept positive payload sizes up to 192 bytes when `(size & 7) == 0`. Updated the editor/development error text to state the actual invariant: positive 8-byte-aligned payloads up to 192 bytes.

Rejected Alternatives: Keeping the bucket whitelist was rejected because it is a policy mismatch with `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`. Removing the upper bound was rejected because unbounded signal payloads would increase queue copy cost and snapshot pressure.

Scalability potential: Low and Middle can use compact DTOs without padding bloat. High and Ultra can still publish richer signals only within the 192-byte cap, preserving predictable queue copy cost while allowing visual-overkill side channels to use additional lanes.

Hardware Impact: Normal hot path remains one `UnsafeUtility.SizeOf<T>()`, two comparisons, and one bit-test during cold lane initialization, not per signal publish. Runtime frame cost is 0us after initialization. On ABI fault paths, the change reduces rejected valid DTOs and avoids follow-on editor churn; no gameplay microsecond savings are claimed.

## 2026-05-23 - Managed Payload Poison Removal

Problem: The X_001 AST audit found two hard payload violations: `ToolEffectSignal` carried a `Transform`, and `PendingDurabilityCommand` carried a `string`. Both names are signal/command payloads, so letting managed references remain in them weakens the signal-lane contract even if one path is currently an immediate local dispatcher.

Solution: Converted `ToolEffectSignal` to explicit-layout value data: effect type, module target instance id, source transform instance id, magnitude, hit point, and source position. Updated `HabitatIntegrityManager` to compare the module instance id instead of object reference identity. Converted `PendingDurabilityCommand` to explicit-layout blittable data and moved the managed tool id into `_queuedDurabilityCommandToolIds`, an owner-managed sidecar array that is not part of the command payload.

Rejected Alternatives: Keeping `ToolEffectSignal.ModuleTarget` was rejected because it hides an interface reference inside a hot gameplay signal. Completing the durability job synchronously to avoid queued string state was rejected because hidden `.Complete()` in gameplay paths violates the dispatcher-owned completion mandate. Hash-only durability replay was rejected because unregistered legacy string ids still need owner-local resolution for existing public APIs.

Scalability potential: Low tier gets compact 40-byte and 24-byte payloads without managed references. Middle keeps current behavior through owner-local sidecar state. High and Ultra can later migrate these paths into owner-owned `SignalBus<T>` lanes without changing payload shape.

Hardware Impact: `ToolEffectSignal` now performs two `GetInstanceID()` reads and one optional position read at dispatch, replacing stored references with primitive data. `PendingDurabilityCommand` keeps the same queue capacity and does not add hot allocations; the string sidecar uses the existing string reference supplied by the caller. Expected runtime delta is below measurable frame scope; no microsecond saving is claimed without profiler proof.

Verification: `SignalArchitectureOptimizationAuditX001` reran after the patch with 2373 scanned files, zero parse failures, 403 payload definitions, 0 hard payload violations, and canonical hash `18f6a27bd840c835cae400e4fed5f169ebc1025f24dd424b7273ca8e9e2fbe02`. Remaining payload debt is 7 layout warnings, not managed-reference payload poison.

## 2026-05-23 - APEX Total Signal Path Audit

Problem: The first report proved the monolith shape but did not give a complete enough answer to the APEX question: whether hidden `GlobalSignals.Publish` calls remain, which damage/collision/reactor/hull/airlock paths still touch legacy routing, and what each `SignalBus<T>` lane claims for capacity, overflow, coalescing, and static zero-GC behavior under a 5000-signal burst.

Solution: Extended `Tools/SignalArchitectureOptimizationAuditX001` to record every legacy publish site, infer payload types from `new Payload(...)` and local identifier/parameter declarations, tag concern areas, and emit a 287-entry `signalLaneLedger`. The rerun found 231 legacy publish sites, 0 unknown legacy publish payloads, 181 centralization-debt lanes, 0 hard managed payload violations, and canonical hash `480dd1942320675a360d5b141064dc2899816249440c4e842ed7e3f0f202ce76`.

Rejected Alternatives: Claiming `GlobalSignals.Publish` was eliminated was rejected because AST evidence proves 231 remaining sites. Mass rewriting all 231 sites in one pass was rejected because it would manufacture route ownership across AI, Gameplay, World, UI, QA, Power, Audio, and legacy-root scripts without phase and capacity route cards. Runtime GC claims from static source were rejected; the report marks them as static source proof only.

Scalability potential: Low tier survives by fixed native capacities, low-tier frame caps, and deterministic drop/clear behavior instead of queue growth. Middle keeps the current typed snapshots and migrates owner-approved lanes incrementally. High can afford richer telemetry and additional visual consumers after the gameplay truth route is stable. Ultra can spend saved cycles on presentation overkill while leaving DTO layout, authority, and signal capacities deterministic.

Hardware Impact: Audit and report generation are tool-time only, 0us runtime. For i3/MX350-class hardware the practical gain is prevention of heap-backed event storms and compile-wall churn, not a measured frame saving. A 5000 `CombatDamageSignal` or `ImpactSignal` burst is documented as bounded native shedding/coalescing behavior in the ledger, but Unity profiler/GCMonitor proof still has not been executed.

## 2026-05-23 - APEX Source Reroute Pass

Problem: The APEX report proved hidden legacy routing, but it still left low-risk presentation/impact lanes publishing through `GlobalSignals.Publish`. Reactor-adjacent RTG heat/HUD, construction pipe rupture/incursion, and high-frequency player/vehicle/fauna collision feedback were still paying the central bridge call when their overloads did not maintain required latest-cache state.

Solution: Replaced 21 safe producer sites with typed `SignalBus<T>.TryPush`: `TemperatureChangedSignal`, `HUDNotificationSignal`, `PipeRuptureSignal`, `FluidIncursionSignal`, `ImpactSignal`, `HighSpeedImpactSignal`, `DebrisSpawnSignal`, `HapticRequest`, `AcousticPingSignal`, and `FaunaStateChangedSignal`. Left `CombatDamageSignal`, `PhysiologyStateSignal`, and `PlayerStateSignal` legacy publishes in place where current consumers still read `GlobalSignals.TryGetLatest*` caches.

Rejected Alternatives: Rewriting every remaining `GlobalSignals.Publish` in one pass was rejected after source inspection because several overloads update `_latestDamageSignal`, `_latestPhysiologyStateSignal`, or `_latestPlayerStateSignal`. Bypassing those caches before moving their consumers to snapshot reads would silently break `HectonSurvivalSystem`, `PlayerStressMetricsRuntime`, `GlobalShaderDispatcher`, `HectonPlayerHealth`, and related presentation readers.

Scalability potential: Low tier now routes more collision, pipe, and RTG presentation traffic directly into bounded native lanes. Middle retains stable damage/physiology/latest behavior while we isolate remaining consumers. High and Ultra can add richer VISUAL_SYNC consumers without increasing Core bridge traffic for the lanes already cut.

Hardware Impact: Verified runtime savings remain 0us because no Unity profiler/GCMonitor capture was run. Static effect is a reduction from 231 to 210 AST-confirmed legacy publish sites and from 181 to 179 centralization-debt lanes. `Hecton8.Editor.csproj` compile passed with 0 warnings and 0 errors, so the reroute does not introduce a C# compile regression.

Verification: `SignalArchitectureOptimizationAuditX001` reran with 2373 scanned files, zero parse failures, 403 payload definitions, 0 hard payload violations, 210 legacy publish sites, 0 unknown legacy payloads, 287 lane ledger entries, 179 centralization-debt lanes, and canonical hash `d3114dcb0291c66cb11be3ba6f9c74bf2b6741a98a8fe4cc8c11571cd7c7fbca`.

## 2026-05-23 - APEX Second Reroute And Final Static Audit

Problem: The previous APEX pass still left 210 legacy `GlobalSignals.Publish` sites. The user's explicit challenge was correct: the codebase was not clean, and hidden legacy calls still existed in damage, impact, acoustic, physiology, player-state, AUP, pause, and bridge lanes.

Solution: Performed a second AST-guided reroute pass and cut only pass-through producers that did not own latest-cache, sanitizer, sequence, or bridge side effects. Final X_001 audit now reports 75 legacy `GlobalSignals.Publish` sites, grouped as: `AcousticPingSignal` 21, `CombatDamageSignal` 9, `PhysiologyStateSignal` 7, `ImpactSignal` 5, `PlayerStressSignal` 4, `SimulationPauseSignal` 4, `AupPreShiftSignal` 3, `AupShiftSignal` 3, `SeismicSignal` 3, `PlayerStateSignal` 3, `RebaseSignal` 2, `ToolStateChangedSignal` 2, `SurvivalVitalsChangedSignal` 2, and six single-site bridge lanes. Regenerated both JSON and markdown reports with canonical hash `c70a4bb8fb5dd51905715692e31724b515dff617b61068996dd6fe05065c9c7b`.

Rejected Alternatives: Rewriting the last 75 call sites was rejected because source inspection shows retained side effects: damage and physiology latest snapshots, player-state readers, AUP shift ordering, acoustic compatibility routes, impact sanitizer behavior, pause/time bridge state, and domain handoff bridges. Claiming runtime zero-GC or runtime 5000-storm proof was rejected because Unity profiler/GCMonitor was not run.

Scalability potential: Low tier uses fixed native queue/snapshot limits and storm shedding instead of managed fan-out. Middle tier keeps deterministic frame caps and owner snapshots while consumers migrate. High tier can attach richer telemetry and VISUAL_SYNC consumers to typed lanes. Ultra tier can spend saved bridge traffic on visual-overkill consumers without changing gameplay authority, DTO layout, or save identity.

Hardware Impact: Verified runtime savings remain 0us; this pass is static architecture proof plus compile proof, not profiler proof. For i3/MX350-class hardware, the practical gain is lower risk of managed event storms and fewer central bridge calls; exact frame-time savings require Unity runtime capture.

Verification: Final `SignalArchitectureOptimizationAuditX001` pass scanned 2374 files, found 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 367 `GlobalSignals` call sites, 75 legacy publish sites, 1678 `SignalBus<T>` call sites, 287 signal lanes in the ledger, and canonical hash `c70a4bb8fb5dd51905715692e31724b515dff617b61068996dd6fe05065c9c7b`.

## 2026-05-23 - Compile Wall Hygiene In Concurrent Dirty File

Problem: After the signal reroute, `dotnet build Hecton8.Editor.csproj` exposed a concurrent compile wall in `DebrisManager.cs`: unqualified `SystemID`/`BufferID` constants resolved incorrectly, then a local `frontStates` declaration shadowed a later out-var. The file was already dirty from another agent's GlobalDataVault migration, but the broken build blocked X_001 verification.

Solution: Applied the smallest compile-only fix: fully qualified the three vault enum constants as `Hecton8.Core.Memory.SystemID` / `Hecton8.Core.Memory.BufferID`, and renamed the pending-shift local to `shiftedFrontStates`. No gameplay behavior, queue capacity, or signal route was changed.

Rejected Alternatives: Reverting `DebrisManager.cs` was rejected because the dirty vault migration was not X_001 work and could belong to another agent. Editing broader debris logic was rejected because it is outside the X_001 signal corridor domain.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this was compile restoration only.

Hardware Impact: 0us runtime delta by design. The fix affects C# name binding only.

Verification: A later `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after waiting for CPU/build guard clearance and concurrent X_004 build/audit processes to finish.

## 2026-05-23 - APEX Third Reroute, Rebase Lane Registration, Build Proof

Problem: The second APEX reroute still left 75 AST-confirmed `GlobalSignals.Publish` sites, including safe pass-through `BrownoutSignal`, `RebaseSignal`, and `ImpactSignal` producers. `RebaseSignal` also had typed producers after reroute but lacked explicit `SignalBus<RebaseSignal>.Configure(...)` registration in the category lane initialization block, leaving capacity proof weaker than the rest of the typed lanes.

Solution: Rerouted the safe pass-through producers only: `BrownoutSignal` in `PowerGrid`, `RebaseSignal` in both headless QA runners, and `ImpactSignal` in seismic tide, fluid, structural leak, and tether routes. Added `SignalBus<RebaseSignal>.Configure(RebaseSignalCapacity, maxFrameSignals: RebaseSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(RebaseSignal)))` plus `EnsureInitialized()` in `GlobalSignals.InitializeCategorySignalLanes()`. Reran the X_001 audit; it now reports 67 legacy publish sites, 1690 `SignalBus<T>` call sites, 0 hard payload violations, 5 layout warnings, 288 lane ledger entries, and canonical hash `d3f560003c18fe09e9ea8cea096637c28e90b38936fb87ef6a1638de76d7400f`.

Rejected Alternatives: Rerouting the final 67 sites was rejected because source evidence shows live bridge side effects: latest-cache updates for damage/physiology/player/tool/vitals/light, AUP pre/post shift ordering, pause/time dilation bridge state, acoustic compatibility state, and storage/crafting/fluid bridge counters. Replacing those in one pass would manufacture ownership routes outside X_001's domain and risk silent runtime regressions.

Scalability potential: Low tier now gets direct typed lanes for all audited `ImpactSignal`, `BrownoutSignal`, and `RebaseSignal` producers with fixed native capacity and low-tier frame caps. Middle keeps deterministic snapshot caps while latest-cache consumers are migrated by owners. High and Ultra can attach richer VISUAL_SYNC consumers to typed impact/rebase/brownout lanes without changing gameplay truth ownership, DTO layout, or save identity.

Hardware Impact: Runtime delta remains unmeasured: 0us verified savings because Unity profiler/GCMonitor was not run. Static hot-path risk is lower: eight producer calls no longer enter the legacy bridge, and `RebaseSignal` now has explicit 64/64/16 lane capacity proof. On i3/MX350-class devices the expected benefit is storm containment and fewer central bridge side effects, not a claimed frame-time number.

Verification: Static proof plus compile proof for the third reroute. Direct `rg` still finds editor/test string probes plus the 67 real publish sites; the AST report is authoritative for payload classification and excludes string literals. After waiting for CPU/build guard clearance, `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 97.57s.

## 2026-05-23 - APEX Fourth Reroute, Single-Lane Latest Reader Audit

Problem: The third reroute still left 67 AST-confirmed `GlobalSignals.Publish` sites. Two of those were single-lane bridge calls, `FluidDensityChangedSignal` and `StorageDebtSignal`, whose `GlobalSignals.Publish` overloads updated legacy latest/dequeue surfaces that source scan showed are not consumed outside `GlobalSignals.cs`.

Solution: Rerouted `HectonPlayerMovement.PublishFluidDensityChanged()` to `SignalBus<FluidDensityChangedSignal>.TryPush(in signal)` and `WorldChunkResidencyManager` storage backpressure publication to `SignalBus<StorageDebtSignal>.TryPush(in signal)`. `SignalBus<T>.TryPush` already applies finite guards and deterministic load-shed/drop policy before enqueue, so no sanitizer coverage was lost. Reran `Tools/SignalArchitectureOptimizationAuditX001`; it now reports 2375 scanned files, 0 parse failures, 65 legacy publish sites, 1692 `SignalBus<T>` call sites, 0 hard payload violations, 5 layout warnings, 288 lane ledger entries, and canonical hash `b5bea7ee97b664108c7ece8bb13153c8a1383ad1cef41bd11ef454e3829ed72e`.

Rejected Alternatives: Removing the remaining 65 publish sites was rejected because they still mutate latest-cache, AUP pre/post shift pause ordering, pause/time state, crafting counters, or presentation bridge state with known external readers. Replacing `GlobalSignals.Publish` with direct `SignalBus<T>` on those lanes would silently break `TryGetLatest*` consumers and dispatcher pause/time semantics.

Scalability potential: Low tier removes two more central bridge writes and keeps fixed native queues/snapshot caps. Middle keeps deterministic frame caps and owner snapshots while remaining latest readers are route-carded. High and Ultra can attach storage/fluid visual sync consumers to typed lanes without increasing gameplay truth authority or DTO size.

Hardware Impact: Verified runtime savings remain 0us because Unity profiler/GCMonitor was not run. Static hot-path risk is lower by two central bridge calls; `StorageDebtSignal` and `FluidDensityChangedSignal` now have zero legacy publish sites and typed publish count 2 each.

Verification: Static audit passed and `git diff --check` reported only existing LF/CRLF warnings on touched files. First build attempt after the fourth reroute failed in unrelated `Visor/HectonScooterVolumetricShaftsFeature.cs` because a concurrent change referenced `HectonUnderwaterVisuals` without importing its `Hecton8.Environment` namespace. Applied the smallest compile-wall fix: `using Hecton8.Environment;`. Build rerun is not claimed because the guard stayed blocked by CPU above 50 percent and active `dotnet` processes.

## 2026-05-23 - APEX Fifth Reroute, Publish Elimination, Route Wrappers

Problem: The fourth pass still left legacy `GlobalSignals.Publish` producers because several lanes had side effects beyond enqueue: latest-cache readers, death-only survival latest state, AUP dispatcher pre/post-shift pause ordering, pause/time bridge state, bullet-time intensity, and crafting completion counters. Blind `SignalBus<T>.TryPush` replacement would have broken those side effects.

Solution: Added latest accepted payload storage to `SignalBus<T>` and changed compatibility latest readers for damage, acoustic, light, stress, player state, physiology, seismic, and tool state to read typed lane latest state. Added explicit typed route wrappers: `AupSignalRoute`, `SimulationSignalRoute`, `CraftingSignalRoute`, and `SurvivalSignalRoute`. These wrappers preserve sanitizer calls, dispatcher AUP pause/release, pause-to-system-pause mirroring, time/bullet bridge scalars, crafting sequence/unit counters, and survival death-only latest state while publishing through typed lanes. Rerouted all remaining runtime producers away from `GlobalSignals.Publish`.

Rejected Alternatives: Leaving the remaining 15 runtime `GlobalSignals.Publish` sites was rejected after the user demanded maximum safe migration. Raw direct `SignalBus<T>.TryPush` for survival vitals was rejected because `TryGetLatestSurvivalDeathSignal` is death-filtered, not last-vitals. Removing `GlobalSignals` compatibility accessors was rejected because external consumers still read them and contract extraction is blocked by route ownership.

Scalability potential: Low tier now has no runtime producer entering the monolithic publish API; hot storms route through fixed native lanes with snapshot caps and storm clear/drop policy. Middle keeps compatibility latest readers while owners migrate to snapshots. High can attach richer VISUAL_SYNC consumers to typed lanes without restoring central publish fan-out. Ultra can spend freed architecture budget on visual-overkill consumers while gameplay truth ownership remains unchanged.

Hardware Impact: Verified runtime savings remain 0us because Unity profiler/GCMonitor was not run. Static risk reduction is concrete: AST `GlobalSignals.Publish` sites dropped from 65 to 0, `SignalBus<T>` sites rose to 1752, and the build passed. For i3/MX350-class hardware, the expected benefit is bounded native queue behavior and lower managed event-storm risk, not a measured frame-time number.

Verification: Final `SignalArchitectureOptimizationAuditX001` pass scanned 2379 files, found 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 304 `GlobalSignals` call sites, 0 `GlobalSignals.Publish` sites, 1752 `SignalBus<T>` call sites, 288 signal lanes in the ledger, and canonical hash `bc01950dc414603239108b740aadfbd745afb9b011e3a99ebf40cbe5c9ebf48d`. Direct `rg` finds only editor/test string probes for `GlobalSignals.Publish`. `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors in 69.28s.

## 2026-05-23 - APEX Sixth Consumer Cleanup, Scanner Latest Repair

Problem: Eliminating producer calls was not enough. Domain consumers still reached through `GlobalSignals.TryGetLatest*`, pause/time/crafting properties, scanner-active latest fallback, and two `GlobalSignals.Push` generic facade calls in ore depletion. The scanner-active case was worse than cosmetic debt: the producer already used `SignalBus<ScannerToolActiveSignal>.Push`, while `GlobalSignals.TryGetLatestScannerToolActiveSignal` still read a legacy field updated only by the obsolete publish facade.

Solution: Replaced clean latest readers with `SignalBus<T>.TryGetLatest` for damage, acoustic, light, stress, player state, physiology, seismic, tool state, and scanner-active lanes. Added route read facades on `SimulationSignalRoute`, `CraftingSignalRoute`, `SurvivalSignalRoute`, and `ScannerSignalRoute` for bridge state that still has side effects or filters. Changed `GlobalSignals.TryGetLatestScannerToolActiveSignal` itself to read typed lane latest state. Replaced `GlobalSignals.Push` in `ProceduralOreSpawner` with direct `SignalBus<ItemAcquiredSignal>.Push` and `SignalBus<ResourceDepletionDeltaSignal>.Push`.

Rejected Alternatives: Replacing survival-death reads with raw `SignalBus<SurvivalVitalsChangedSignal>.TryGetLatest` was rejected because death latest is filtered state, not simply the last vitals payload. Replacing pause/time/crafting state with raw signal latest was rejected because the current bridge stores scalar/counter projections that consumers depend on. Deleting `GlobalSignals.InitializeAllQueues`, AUP origin helpers, or dispatcher flush/clear was rejected because those are cold bootstrap/origin/phase ownership routes, not the hot publish/consume corridor requested here.

Scalability potential: Low tier now avoids the monolithic hot facade for all audited external signal producers and destructive/latest consumers; native lanes own bounded ingress and snapshots. Middle keeps route wrappers for bridge state until owner route cards split them. High and Ultra can add richer VISUAL_SYNC consumers without reintroducing `GlobalSignals.Publish`, `GlobalSignals.Push`, or destructive queue readers in domain folders.

Hardware Impact: Verified runtime savings remain 0us because no Unity profiler/GCMonitor capture was run. Static risk reduction is concrete: `GlobalSignals` call sites dropped from 304 to 266, `SignalBus<T>` call sites rose from 1752 to 1785, and external runtime `GlobalSignals.Push/Publish/TryDequeue/*Writer` hits are zero. On i3/MX350-class devices the expected benefit remains deterministic native storm shedding and less central facade pressure; measured frame-time impact requires runtime capture.

Verification: `SignalArchitectureOptimizationAuditX001` scanned 2379 files, found 0 parse failures, 403 payload definitions, 0 hard payload violations, 5 layout warnings, 266 `GlobalSignals` call sites, 0 `GlobalSignals.Publish` sites, 0 `GlobalSignals` consume sites, 1785 `SignalBus<T>` call sites, and canonical hash `0c51f9089edf8d069c4b5c224d13cc72feca4ee78f810949e91a4d8dcca26cdc`. Direct source scan shows no runtime `GlobalSignals.Push`, `GlobalSignals.Publish`, `GlobalSignals.TryDequeue`, or `GlobalSignals.*Writer` usage. Build passed: `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, 0 warnings, 0 errors, 47.30s.

## 2026-05-23 - APEX Seventh Payload Extraction Under Build Guard

Problem: The hot route cleanup left too many DTO definitions embedded in `GlobalSignals.cs`. Direct domain-asmdef extraction is still unsafe because many consumers compile through `Hecton8.Core` and several domain contract dependencies are not route-carded.

Solution: Moved 32 unmanaged signal payload structs out of `GlobalSignals.cs` into `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs` and `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs`. Added both files to `Hecton8.Core.csproj`. The extracted group covers impact/haptic/player/inventory/radiation payloads and manual override/HUD/PDA/save/WFC/compliance payloads. Quick source count now shows 137 `public struct ... : ISignal` definitions still in `GlobalSignals.cs`.

Rejected Alternatives: Moving these structs directly into domain asmdefs was rejected for this pass because it would risk dependency cycles before owner route cards are complete. Reverting the extraction was rejected because the first compile fault was a missing project include, not a contract-layout fault. Launching more `dotnet` work while CPU is above 50 percent or external `dotnet/csc` is active was rejected because the project guard explicitly forbids it.

Scalability potential: Low tier gets smaller Core monolith edit surface without runtime route churn. Middle keeps existing assembly boundaries while ownership cards are prepared. High and Ultra can later move these same files into true domain contract asmdefs without changing DTO layout or signal route semantics.

Hardware Impact: Runtime delta is 0us by design; this is source ownership extraction only. Compile-time savings are not claimed because final build/audit rerun after the second extraction is blocked by external CPU/dotnet/csc guard.

Verification: Static `rg` confirms the 32 moved payload structs no longer have definitions in `GlobalSignals.cs`, and both new files are listed in `Hecton8.Core.csproj`. `git diff --check` on touched extraction files reports only LF/CRLF warnings. Full `dotnet build` and final Roslyn audit after the second extraction are not claimed: repeated guard checks showed CPU above 50 percent and active external `dotnet/csc` processes.

## 2026-05-23 - APEX Full Static DTO Extraction Under Build Guard

Problem: The partial extraction still left `GlobalSignals.cs` as a DTO warehouse. Static count after the first two payload files showed 137 signal-like definitions still embedded in the central file, which preserved merge contention even though runtime publish/consume routes were already cut.

Solution: Moved the remaining core-foundation contract block into `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs`, then moved the remaining bottom payload block into `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`. Added both files to `Hecton8.Core.csproj`. The result is 0 exact `public struct ... : ISignal` definitions in `GlobalSignals.cs`, 168 exact DTO structs in `GlobalSignalPayloads.*.cs`, and one preserved `ISignalSnapshotTransformer<CombatDamageSignal>` in the extracted domain remainder file.

Rejected Alternatives: Moving the extracted files into true domain asmdefs was rejected because that requires owner route cards and dependency-cycle review. Deleting `GlobalSignals.cs` was rejected because it still owns the typed lane registry, bootstrap queues, origin bridge, sanitizer path, flush/clear orchestration, and compatibility readers. Running `dotnet build` or the Roslyn audit under CPU 100 percent with active `csc/dotnet` was rejected because the project guard explicitly forbids competing builds.

Scalability potential: Low tier now has a smaller central hot-corridor file with DTO edit churn isolated into payload files. Middle keeps the same assembly boundary while preserving deterministic lane capacity behavior. High and Ultra can later split these payload files into true domain contracts without changing payload layout, route semantics, save identity, or authority ownership.

Hardware Impact: Runtime delta is 0us by design; this was source ownership extraction only. Low-end hardware gains no claimed frame time until runtime profiler evidence exists. The practical gain is lower compile-wall contention and lower risk of central-file merge churn.

Verification: Static source scans report 0 exact `ISignal` DTO structs in `GlobalSignals.cs`; 168 exact DTO structs in extracted `GlobalSignalPayloads.*.cs`; no extracted payload field declarations of `GameObject`, `Transform`, `string`, `FixedString*`, `NativeArray`, `NativeQueue`, `NativeList`, or `NativeHashMap`; and no runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` call sites outside editor/test string probes. `git diff --check` on the touched extraction files reports only LF/CRLF warnings. Full `dotnet build` and final Roslyn audit are not claimed because the latest guard check after waiting reported CPU 92.22 percent with active `csc` and `dotnet`, still above the 50 percent build threshold.

## 2026-05-23 - APEX GlobalSignals Shell Split And Capacity Snapshot

Problem: DTO extraction removed contract payloads from `GlobalSignals.cs`, but the file still physically owned the bus registry/runtime, SPSC buffer, lifecycle/flush orchestration, state catalog, and legacy bridge APIs. That preserved a central merge hotspot and made future domain ownership review harder even though external runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` routes were already cut.

Solution: Split the remaining monolithic body into `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`, `SpscSignalRingBuffer.cs`, `GlobalSignals.LegacyFacade.cs`, `GlobalSignals.LegacyWriters.cs`, `GlobalSignals.RuntimeLifecycle.cs`, and `GlobalSignals.State.cs`. `Assets/_Project/Scripts/Core/GlobalSignals.cs` is now a 12-line compatibility shell. Fixed split fallout: moved the subsystem-registration attribute back onto `ResetStaticState`, removed the orphan attribute from the legacy facade, removed an extra lifecycle closing brace, and closed the SPSC namespace. Wrote `Docs/Reports/SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md` with 144 current `SignalBus<T>.Configure*` sites, 73 legacy `CreateQueue` typed-lane slots, and the 288-entry last-Roslyn capacity/coalescing ledger.

Rejected Alternatives: Deleting the legacy facade was rejected because compatibility readers and bridge state still exist. Moving payload files directly into domain asmdefs was rejected because owner route cards and dependency-cycle review are still missing. Claiming runtime zero-GC proof from static source was rejected because Unity profiler/GCMonitor was not run. Running another build after fixing `TraumaDispatcher` was rejected because CPU guard rose above 50 percent.

Scalability potential: Low tier keeps bounded native lanes, low-tier frame caps, deterministic oldest-drop, and storm clear above 1024 queued payloads. Middle keeps current typed snapshots with route wrappers for bridge state. High can attach richer telemetry and VISUAL_SYNC consumers to typed lanes without changing gameplay truth ownership. Ultra can spend saved architecture bandwidth on visual-overkill consumers while DTO layout, save identity, authority route, and native lane capacity remain stable.

Hardware Impact: Runtime delta is 0us verified because this pass is source ownership and compile-wall hygiene only. Static impact is reduced central-file churn: `GlobalSignals.cs` is 12 lines, no exact DTO definitions remain there, and external runtime legacy hot-route scan is clean. For i3/MX350 the expected benefit is lower merge/compile contention and preserved deterministic storm shedding, not a measured frame-time saving.

Verification: Fresh post-split scans found 0 external runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits, 0 exact `ISignal` DTO definitions in `GlobalSignals.cs`, 168 exact DTO structs in extracted payload files, 0 managed DTO field hits for `GameObject`, `Transform`, `string`, `FixedString*`, or native containers, and no brace-balance deltas in `Core/Signals/*.cs`. `git diff --check` reports only LF/CRLF warnings. Post-split build attempt reached unrelated `TraumaDispatcher.cs` missing `BufferID` constants; fixed with local explicit IDs `(BufferID)73398` and `(BufferID)73399`. Rebuild after that fix is pending because the latest guard reported CPU 100 percent with active `csc` and multiple `dotnet` processes.

## 2026-05-23 - APEX DTO Duplicate/String Scrub And Capacity Ledger Refresh

Problem: The post-split DTO audit still exposed signal hygiene debt: `Hecton8.Modding` declared public `HapticPulseSignal` and `SubtitleCueSignal` names that shadowed Core/UI signal contracts with different layouts, and `MockPlayerFootstepSignal` carried `FixedString64Bytes SurfaceName`. `FixedString64Bytes` is unmanaged, but it is still text identity in a hot signal payload instead of a fixed hash.

Solution: Renamed the modding-only DTOs to `ModHapticPulseSignal` and `ModSubtitleCueSignal`, then updated all local `SignalBus<T>`, `NativeQueue<T>.ParallelWriter`, layout validation, and enqueue sites in `FutureCommandSandboxValidator.cs`. Replaced `MockPlayerFootstepSignal.SurfaceName` with `uint SurfaceHash` and explicit padding at offsets 52-111, preserving the 128-byte ABI expected by `ValidateSignalSize<MockPlayerFootstepSignal>(128)`. Regenerated `SIGNAL_DTO_MANAGED_REFERENCE_AUDIT_X_001.md` refreshed the capacity snapshot with current source locations, and wrote `SIGNAL_DOMAIN_HOT_ROUTE_AUDIT_X_001.md` for targeted Power/Habitat/Environment/Construction/Gameplay/Physics/World/Animation/UI/Audio/Inventory proof.

Rejected Alternatives: Keeping duplicate simple names was rejected because it makes lane ownership ambiguous and can hide incorrect simple-name binding inside the modding namespace. Keeping `FixedString64Bytes` was rejected because the request explicitly requires hash identity instead of string names in signal DTOs. Shrinking `MockPlayerFootstepSignal` was rejected because that would alter the current ABI fence and require a wider capacity/layout review.

Scalability potential: Low tier keeps smaller, hash-addressed DTO identity and avoids copying 64 bytes of surface text through the lane. Middle keeps current lane ABI and capacity behavior. High and Ultra can enrich presentation consumers by resolving `SurfaceHash` through static data or visual tables without changing gameplay signal layout.

Hardware Impact: Verified runtime savings remain 0us because no Unity profiler/GCMonitor capture was run. Static copy pressure is lower for future footstep producers because a 64-byte fixed string field was replaced by a 4-byte hash plus padding while preserving ABI size. The practical low-end impact is reduced risk of string-like identity churn in hot signals, not a measured frame-time number.

Verification: Direct `rg` reports 0 runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits. `SIGNAL_DTO_MANAGED_REFERENCE_AUDIT_X_001.md` reports 292 runtime `ISignal` structs, 0 managed/string/native-container field violations, 0 duplicate signal names, and 0 layout warnings. `SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md` now reports 248 configure sites, 224 unique configured typed lanes, 73 legacy native queue compatibility slots, and separates `HapticPulseSignal`, `ModHapticPulseSignal`, `SubtitleCueSignal`, and `ModSubtitleCueSignal` capacities. `SIGNAL_DOMAIN_HOT_ROUTE_AUDIT_X_001.md` reports 11 scanned domain folders, 0 legacy hot-route hits, and 110 non-hot helper/read/bootstrap `GlobalSignals.` hits. Build is not claimed: Latest guard is worse: CPU 100 percent and active external `dotnet` processes remain, so the no-parallel-build guard blocks `dotnet build`.

## 2026-05-23 - APEX Central Native Queue Removal And Registry Capacity Fix

Problem: The previous split removed DTOs and external hot routes, but `GlobalSignals.RuntimeLifecycle.cs` still carried 73 `CreateQueue(ref _*Signals...)` bootstrap calls and 73 matching `DisposeQueue(ref _*Signals...)` shutdown calls, while `GlobalSignals.State.cs` no longer owned the backing fields. Static capacity regeneration also found 273 unique configured/prewarmed `SignalBus<T>` lane types against a 256-slot `SignalBusRegistry` dispatch table.

Solution: Replaced the 73 lifecycle queue calls with `RegisterLegacyLane<T>(capacity, nameof(T))`, changed that helper to call `SignalBus<T>.Configure(...)` plus `SignalBus<T>.EnsureInitialized()`, and removed the 73 dispose calls. `GlobalSignals.LegacyFacade.TryDequeue*` remains a compatibility API but now consumes typed frame snapshots through `SignalBus<T>.TryConsumeFrame`; a duplicated `TryDequeueProgressionEvent` consume was reduced to one cursor read. `SignalBusRegistry.LaneCapacity` was raised from 256 to 512 and cold-allocation comments were updated. Reports now record 251 direct configure sites, 73 legacy typed prewarm registrations, 273 unique configured/prewarmed lanes, 192 boot-prewarm upper-bound lanes, and 0 central queue fields/refs.

Rejected Alternatives: Leaving the empty central queue calls was rejected because they were stale monolithic ownership and would fail compile after field removal. Lazy-only first-signal initialization was rejected because the first gameplay event would pay native allocation outside the bootstrap phase. Keeping the 256 registry cap was rejected because the refreshed source ledger already exceeds it. Deleting `GlobalSignals.Publish` compatibility overloads was rejected for this pass because no external runtime call sites remain and the overloads still preserve latest/bridge side effects for any legacy bootstrap callers until owner route cards delete them deliberately.

Scalability potential: Low tier gets deterministic typed prewarm for legacy lanes and no second central queue. Middle tier keeps route compatibility while domains migrate helper reads. High tier has dispatch headroom for full domain feature activation. Ultra tier has enough registry headroom for visual-overkill signal consumers without changing gameplay DTO layout or route authority.

Hardware Impact: Steady-state frame impact remains 0us verified because no Unity profiler/GCMonitor capture was run. Cold managed dispatch table references grow from 256 to 512 slots across three static arrays; that is a small bootstrap memory trade for eliminating registry overflow with the current 273-lane source envelope. The central native queue removal eliminates duplicate bootstrap queue ownership; exact runtime microseconds saved are unclaimed.

Verification: Direct runtime hot-route scan still returns only Editor/test string probes for `GlobalSignals.Publish`. Core signal scan returns 0 hits for `NativeQueue<.*>\s+_.*Signals`, `CreateQueue(`, `DisposeQueue(`, `TryDequeue(ref _)`, and `OpenQueueForLegacyGlobalSignals`. DTO line/brace scan over non-Editor scripts reports 292 `ISignal` structs and 0 managed/string/native-container field violations. Build is not claimed: CPU guard reports 100 percent with active `csc` and multiple `dotnet` processes.

## 2026-05-23 - APEX Legacy Facade Compile-Time Ban

Problem: External runtime callers were already removed, but the central `GlobalSignals` compatibility surface still exposed usable `Publish`, `Push`, destructive `TryDequeue*`, and writer-property APIs. That kept a path for future code to reattach to the old commutator without failing fast.

Solution: Marked 119 `GlobalSignals.Publish` overloads, 3 `GlobalSignals.Push` aliases, 84 `GlobalSignals.TryDequeue*` methods, and 34 `GlobalSignals.*Writer` properties as `[Obsolete(..., true)]`. The methods remain as bridge code for deliberate Core-side removal later, but any new source caller now fails compilation instead of silently routing through the legacy facade.

Rejected Alternatives: Deleting the facade outright was rejected because some overloads still preserve latest-cache and bridge-side state until owner route cards replace that state. Leaving the facade merely unused was rejected because it would allow the same architectural regression to return under pressure. Runtime-profiler zero-GC claims were rejected because no Unity profiler or GCMonitor capture was run.

Scalability potential: Low tier keeps bounded native typed lanes and prevents new central hot fan-out. Middle tier keeps compatibility state while owners migrate helper reads. High tier can attach richer typed consumers without reopening central routes. Ultra tier can add visual-overkill lanes through explicit `SignalBus<T>` contracts without changing gameplay authority, DTO layout, or save identity.

Hardware Impact: Verified runtime savings remain 0us because no profiler capture was run. Static impact is regression prevention: 206 central facade methods plus 34 writer properties now fail at compile time if used. On i3/MX350-class hardware this protects against managed or central queue relapse; measured frame-time change is unclaimed.

Verification: Static annotation scan reports 0 unannotated central legacy hot declarations. External runtime scan excluding Core/Signals definitions, Editor, and Tests reports 0 `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits. Core queue scan still reports 0 central `NativeQueue<T> _*Signals`, 0 `CreateQueue`, 0 `DisposeQueue`, 0 `TryDequeue(ref _)`, and 0 `OpenQueueForLegacyGlobalSignals` refs. Build is not claimed: CPU guard reports 100 percent with active `csc` and multiple `dotnet` processes.

## 2026-05-23 - Managed Event Hotpath Audit And Player Health Cleanup

Problem: The signal corridor was clean, but selected gameplay/UI/domain scans still found managed C# event declarations and non-modding `HectonEventBus` traffic. `HectonPlayerHealth` carried five public managed events in damage/heal/death/mutation paths with zero runtime subscribers in source. `HectonSurvivalSystem` carried 16 vitals/injury/thermal/bleed managed events even though it already publishes `SurvivalVitalsChangedSignal`.

Solution: Removed `OnHealthChanged`, `OnDeath`, `OnDamageTaken`, `OnHealed`, and `OnMutationFlagsChanged` from `HectonPlayerHealth` and deleted their invoke sites. Removed unused survival vitals/critical/injury/thermal/bleed managed events from `HectonSurvivalSystem`, while retaining `OnDeath` because `PDA/PDALogbookManager` subscribes to it. Health owner state continues through `MarkCombatDamageSyncDirty`; survival vitals continue through `SurvivalSignalRoute.QueueVitals` and typed `SignalBus<SurvivalVitalsChangedSignal>`. Added `Docs/Reports/SIGNAL_MANAGED_EVENT_HOTPATH_AUDIT_X_001.md`.

Rejected Alternatives: Keeping unused events was rejected because they are managed callback surfaces in gameplay damage/vitals paths. Deleting `HectonSurvivalSystem.OnDeath` was rejected because it has a live PDA logbook subscriber. Converting every `HectonEventBus` event to `SignalBus<T>` was rejected because those payloads are managed cold/API contracts with authored IDs/messages and require owner route cards. Removing `PlayerTransportCoordinator.ActiveTransportLifecycleChanged` was rejected because it has live audio and trauma subscribers and fires on lifecycle changes, not per-frame storms.

Scalability potential: Low tier loses unused managed callback checks from player health mutations. Middle keeps existing typed health/vital lanes. High and Ultra can add richer health presentation consumers from typed snapshots without reintroducing gameplay managed events.

Hardware Impact: Verified runtime savings remain 0us because no profiler capture was run. Static effect is five unused managed event declarations and nine invoke sites removed from player health paths, plus 16 unused survival managed event declarations and 17 invoke sites removed from vitals/injury/thermal paths. On i3/MX350-class hardware the expected benefit is reduced callback relapse risk, not a measured frame-time number.

Verification: `rg` finds no `OnHealthChanged/OnDeath/OnDamageTaken/OnHealed/OnMutationFlagsChanged` in `HectonPlayerHealth.cs`; brace balance for the file is 0. `HectonSurvivalSystem.cs` retains only `OnDeath` from the removed event group; brace balance is 0. Selected signal-heavy domains plus survival root now show three remaining C# event declarations: transport lifecycle, subtitle cue presentation, and survival death. Non-modding `HectonEventBus` hits remain 29 and are documented as cold/API debt.

## 2026-05-24 - Item Lifecycle Typed Route

Problem: First-party collected/recycled/discarded item traffic still used managed `HectonEventBus` classes carrying `ItemData` references. The first-party consumers were `EnvironmentalStrainManager` and `GlobalProfileManager`, so this was not just mod/API isolation.

Solution: Added `ItemLifecycleSignal` as a 64-byte unmanaged DTO and `ItemLifecycleSignalRoute` as the owner-local conversion point from `ItemData` to item hash/category/family/flags. Configured the lane at capacity 128, max frame 128, low-tier cap 32, added direct flush/clear wiring, finite guards, and per-consumer sequence cursors. Rewired item collected/recycled/discarded producers and marked retired item managed event classes obsolete true.

Rejected Alternatives: Keeping `ItemData` on managed item events was rejected because first-party world/meta state does not need managed object references. Reusing `ProgressionEventSignal` was rejected because existing narrative consumers destructively consume that lane. Removing the unmanaged inventory drop mod payload was rejected because it is mod-facing and has no first-party subscriber in source.

Scalability potential: Low tier gets bounded 128/32 native item lifecycle traffic and no first-party managed item event fan-out. Middle keeps deterministic item progression/environment state from snapshot reads. High and Ultra can attach richer item telemetry or presentation consumers without changing gameplay DTO layout.

Hardware Impact: Verified runtime savings remain 0us; no Unity profiler/GCMonitor capture was run. Static effect is removal of four first-party managed item event publish sites and five first-party item event subscriptions/handlers, replaced by one fixed-size native lane.

Verification: first-party item event publish/subscribe scan returns 0 hits outside `ModdingAPI`; DTO poison scan for `ItemLifecycleSignal` returns no banned managed/string/native-container fields; brace balance is 0 on touched files; full Editor build is not claimed because retry is blocked by CPU 100 percent and external `dotnet` processes.

## 2026-05-24 - Progression Meta Typed Route

Problem: First-party achievement unlock and PDA advisory traffic still used managed `HectonEventBus` classes carrying strings. The live first-party consumers were `DynamicDifficultyDirector` and `GlobalProfileManager`, so this was gameplay/meta state depending on managed event objects instead of hash-only signal lanes.

Solution: Added `ProgressionMetaSignal` as a 32-byte unmanaged DTO and `ProgressionMetaSignalRoute` as the producer route. Configured the lane at capacity 64, max frame 64, low-tier cap 16, and wired direct flush/clear dispatch. `PlayerAchievementRegistry` and `PDAContextualAdvisorySystem` now publish typed hash-only signals. `DynamicDifficultyDirector` and `GlobalProfileManager` now drain `SignalBus<ProgressionMetaSignal>` with local sequence cursors. Managed `AchievementUnlockedEvent` and `PlayerAdvisoryIssuedEvent` were retired with compile-time obsolete errors.

Rejected Alternatives: Keeping strings on managed events was rejected because first-party meta decisions only need stable hashes. Deleting cold game-load/player-spawn/player-death managed subscriptions was rejected because those are low-frequency bootstrap/death surfaces and require separate owner route cards. Sharing a new achievement catalog was rejected for this pass because it would widen the edit set without improving the signal lane contract; the profile owner keeps its cold fixed hash/title map.

Scalability potential: Low tier gets 64/16 bounded native meta traffic and no managed achievement/advisory fan-out. Middle keeps deterministic profile/difficulty progression from snapshot reads. High and Ultra can attach richer meta telemetry or presentation consumers to the typed lane without changing save identity or gameplay authority.

Hardware Impact: Verified runtime savings remain 0us; no Unity profiler/GCMonitor capture was run. Static effect is removal of first-party managed achievement/advisory publish and subscription sites, replaced by one fixed-size native lane. On i3/MX350-class hardware the practical gain is avoiding managed object/string payload relapse in meta progression.

Verification: first-party `AchievementUnlockedEvent`/`PlayerAdvisoryIssuedEvent` publish/subscribe scan returns 0 hits outside `ModdingAPI`; DTO poison scan over Core signal files returns 0 managed/string/native-container fields; runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests returns 0 hits; brace balance is 0 on touched files. Full Editor build is not claimed because seven active `dotnet` processes block another build under the project guard.

## 2026-05-24 - Survival Death And Biome Discovery Typed Route

Problem: After item and progression event cuts, two first-party managed multicast routes still bypassed the typed corridor: `HectonSurvivalSystem.OnDeath` fed PDA logbook death entries, and `HectonDiscoveryManager.OnBiomeDiscovered` fed difficulty, profile, achievement, and PDA logbook state. Both were low-frequency, but they were still hidden managed signal paths beside the `SignalBus<T>` contract.

Solution: Reused existing lanes instead of adding new bus sprawl. Survival death now rides the existing 32-byte `SurvivalVitalsChangedSignal` route; `PDALogbookManager` consumes `SurvivalSignalRoute.TryGetLatestDeath` with source/sequence filtering, and the survival `OnDeath` event was deleted. Biome discovery now rides the existing 32-byte `ProgressionMetaSignal` lane as `KindBiomeDiscovered`; difficulty/profile/achievement/logbook consumers drain local sequence cursors from `SignalBus<ProgressionMetaSignal>`, and `OnBiomeDiscovered` was deleted.

Rejected Alternatives: Keeping death/biome callbacks because they are cold was rejected because they are first-party signal routes and already had matching typed infrastructure. Adding a separate `BiomeDiscoveredSignal` lane was rejected because `ProgressionMetaSignal` is already the hash-only meta progression lane and has explicit 64/16 frame caps. Routing PDA death through `HectonEventBus` was rejected because mod/API isolation is not a first-party runtime path.

Scalability potential: Low tier receives bounded native death/meta snapshots without managed multicast. Middle keeps deterministic profile/difficulty/logbook state through local cursors. High and Ultra can attach richer meta telemetry to `ProgressionMetaSignal` without changing save identity, DTO layout, or gameplay authority.

Hardware Impact: Verified runtime savings remain 0us; no Unity profiler/GCMonitor capture was run. Static effect is removal of two managed event declarations and all first-party subscriptions/invokes for survival death and biome discovery. For i3/MX350-class hardware, the practical gain is lower callback relapse risk and deterministic overload shedding through existing lanes.

Verification: Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests returns 0 hits; first-party retired item/progression/death/biome event scan outside `ModdingAPI` returns 0 hits; Core signal DTO banned-field scan returns 0 hits; brace balance is 0 on touched files. Build is not claimed because CPU/build guard reports active `dotnet` and `VBCSCompiler` processes.

## 2026-05-24 - Session Lifecycle Typed Route And Inventory Drop Bus Cut

Problem: After death/discovery cleanup, first-party session load/spawn code still depended on managed `GameLoadedEvent` and `PlayerSpawnedEvent` subscriptions in meta, PDA, progression, and UI systems. `PlayerInventory` also emitted an unmanaged `InventoryPhysicalDropRequestPayload` through `HectonEventBus` with no source subscriber, leaving one non-modding event-bus publish outside the mod/API boundary.

Solution: Added 64-byte `SessionLifecycleSignal` and `SessionLifecycleSignalRoute`; configured `SignalBus<SessionLifecycleSignal>` at capacity 16, max-frame 16, low-tier cap 8; wired direct flush/clear, direct-lane recognition, finite guard, and lane contract id 134. `ModLoader` now publishes typed lifecycle signals before the mod-only envelope gate while keeping managed `GameLoadedEvent` and `PlayerSpawnedEvent` publishes behind the mod/API gate. Rewired seven first-party consumers to local sequence-cursor snapshot drains. Removed the dead non-modding inventory physical-drop `HectonEventBus.Publish`; item discard remains on `ItemLifecycleSignalRoute`.

Rejected Alternatives: Keeping load/spawn on managed `HectonEventBus` because it is cold was rejected because it was still a first-party signal route with managed string/object payloads. Reusing `ProgressionMetaSignal` was rejected because session lifecycle is not progression state and needs player entity/position context. Preserving the physical-drop unmanaged event publish was rejected because source search found no subscriber and the authoritative discard fact already rides `ItemLifecycleSignal`.

Scalability potential: Low tier has bounded 16/8 lifecycle snapshots with no managed fan-out. Middle tier preserves deterministic bootstrap rebinding through local cursors. High and Ultra can attach richer session telemetry or diegetic presentation consumers to the typed lane without changing save identity, DTO layout, or gameplay authority.

Hardware Impact: Verified runtime savings remain 0us; no Unity profiler/GCMonitor capture was run. Static effect is removal of seven first-party managed event subscriptions/handlers plus the final non-modding `HectonEventBus.Publish` site outside ModdingAPI. On i3/MX350-class hardware the benefit is bounded native lifecycle routing and removal of managed event-bus relapse points, not a measured frame-time number.

Verification: First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is 0; first-party `GameLoadedEvent`/`PlayerSpawnedEvent` outside ModdingAPI is 0; runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Editor/Tests is 0; Core signal DTO banned-field scan is 0; touched-file brace balance is 0. Guarded build was run twice: X_001 fallout was fixed, but the remaining build wall is 14 unrelated errors in `MainMenuController`, `HectonDirectorAI`, `ModSettingsRegistry`, `GameBootstrapper`, and `MesofaunaBehavioralStateMachine`.

## 2026-05-24 - Global Helper Route Demonolithization

Problem: External runtime domains no longer called `GlobalSignals.Publish`, but many files still depended directly on `GlobalSignals` helper/bootstrap names for AUP conversion, entity-source folding, initialization, flush, clear, and lane prewarm. Those calls were not hot queue producers, but they kept the central class as an architectural utility bucket.

Solution: Added pure `RuntimeOriginRoute` for AUP conversion and entity-id folding, then bulk-rerouted 244 runtime call sites across 190 files. Added `SignalCorridorRuntime` for lifecycle/phase operations, then moved 20 runtime call sites across 18 files off direct `GlobalSignals` lifecycle names. Kept `GlobalSignals` wrappers only as compatibility delegates inside `Core/Signals`.

Rejected Alternatives: Deleting bridge state from `SignalBridgeRoutes` was rejected because pause, time-dilation, crafting, and survival latest-state bridge state still have live consumers and need owner route cards before removal. Leaving helper calls on `GlobalSignals` was rejected because it preserved a non-hot but real monolithic dependency pattern.

Scalability potential: Low tier gets the same zero-allocation helper math without a central queue dependency. Middle keeps deterministic route ownership through explicit Core routes. High and Ultra can add richer signal consumers without letting utility reads drift back into a central commutator.

Hardware Impact: Runtime verified savings remain 0us because no Unity profiler/GCMonitor capture was run. Static path impact is removal of central helper/bootstrap coupling from 190 runtime files. `RuntimeOriginRoute` performs value math only; `SignalCorridorRuntime` delegates to existing cold lifecycle operations and adds no managed allocation.

Verification: external runtime `GlobalSignals.` outside `Core/Signals` is 0; runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Editor/Tests is 0; runtime helper references to `GlobalSignals.CurrentRuntimeOriginAup/TryRuntimePositionToAup/FoldEntityIdToSourceId` are 0; first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is 0; Core signal DTO/route banned-field scan is 0. Build was not launched because CPU guard reported 96 percent after waiting.

## 2026-05-24 - Bridge State Extraction And Legacy Latest Read Ban

Problem: `GlobalSignals` no longer had external hot publish/consume callers, but it still owned bridge state for time dilation, simulation pause, bullet-time intensity, crafting completion counters, and survival death latest state. That kept the central class as state owner instead of a compatibility facade.

Solution: Added `SignalBridgeState` as the internal state owner. `SignalBridgeRoutes` now records bridge state directly and initializes through `SignalCorridorRuntime.EnsureInitialized()`, so it has 0 direct `GlobalSignals.` references. `GlobalSignals.ClearLatestSignals()` resets the bridge state through `SignalBridgeState.Reset()`. Public central latest/bridge read facades now delegate to typed state and are marked `[Obsolete(..., true)]` so new runtime readers fail compile.

Rejected Alternatives: Deleting compatibility read facades outright was rejected because older editor/test surfaces and staged integration code can still require a compile-time visible migration error. Leaving bridge counters inside `GlobalSignals` was rejected because it preserved central fact ownership after the route split. Replacing pause/time/crafting with new lanes was rejected for this pass because those typed lanes already exist and the remaining problem was state location, not DTO shape.

Scalability potential: Low tier keeps the same fixed native signal lanes and no additional allocation. Middle keeps deterministic latest-state reads through route wrappers. High and Ultra can add richer visual consumers to existing lanes without routing through the central class.

Hardware Impact: 0us verified runtime savings; no Unity profiler/GCMonitor capture was run. Static impact is ownership cleanup: bridge state no longer lives on `GlobalSignals`, and 16 central latest/bridge read facades are compile-time banned for new callers.

Verification: `SignalBridgeRoutes.cs` has 0 `GlobalSignals.` references; external runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests is 0; DTO banned-field scan over payload/route/runtime-origin/bridge-state files is 0.

## 2026-05-24 - Impact Storm Coalescing And Lane Prewarm Closure

Problem: `CombatDamageSignal` and `AcousticPingSignal` had explicit coalescing, but collision-heavy `ImpactSignal` and `HighSpeedImpactSignal` still relied only on bounded queue/drop behavior. A 5000-signal collision burst would be bounded, but it would not merge same-cell impact facts before overflow. Separately, 13 domain `SignalBus<T>.Configure` call sites did not immediately prewarm with `EnsureInitialized`, and several storm producers still used `SignalBus<T>.Push` wrappers instead of explicit `TryPush`.

Solution: Added allocation-free coalescing to `SignalBus<T>` for `ImpactSignal` and `HighSpeedImpactSignal`. `ImpactSignal` coalesces by AUP meter cell plus primary body/material alias identity and preserves max force/intensity/weight plus OR flags. `HighSpeedImpactSignal` coalesces by AUP meter cell plus source/target/material identity and preserves the max kinetic/lost-energy scalar, highest-energy point/normal/frame sample, max effective mass, and OR flags. Patched all missing configure-prewarm sites and converted impact/high-speed-impact/combat-damage/acoustic/deferred-submarine-impact storm producers to direct `TryPush`. Removed the string-taking first-party session lifecycle route; `ModLoader` now computes a FNV-1a slot hash before publishing `SessionLifecycleSignal`.

Rejected Alternatives: Increasing native capacities to absorb 5000 events was rejected because it spends memory to hide a storm shape and still leaves redundant same-cell facts. Managed dictionaries for coalescing were rejected because the hot path must stay zero-GC. Coalescing across unrelated body/material ids was rejected because it would destroy useful gameplay/presentation identity. Leaving `Push` wrappers was rejected for storm producers because explicit `TryPush` makes bounded/drop semantics visible at the call site.

Scalability potential: Low tier now sheds and coalesces collision storms in fixed native memory. Middle keeps deterministic per-cell impact summaries. High and Ultra can consume richer visual overkill from the same typed lanes after the gameplay facts have been compressed deterministically.

Hardware Impact: 0us verified runtime savings; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is fewer redundant collision entries reaching consumers during dense contact frames, with no heap allocation and no queue growth.

Verification: Runtime configure ledger is 329 records, 277 unique lanes, 74 legacy typed prewarm registrations, 4 cache-critical lanes, 221 Core lifecycle registrations, and 108 domain-local registrations; missing immediate `EnsureInitialized` after `SignalBus<T>.Configure` is 0. Storm-lane `Push` wrappers for impact/high-speed-impact/combat-damage/acoustic/deferred-submarine-impact outside `GlobalSignals.LegacyFacade.cs` are 0; typed `TryPush` call sites for those lanes are 74. DTO/route banned-field scan is 0 after removing the session route string parameter. Build is not claimed because guarded CPU checks reported 53 percent, then 90 percent after waiting.

## 2026-05-24 - Registered Dispatch And Bounded Legacy Prewarm

Problem: `SignalBusRuntime` still contained a hardcoded concrete DTO dispatch table through `FlushDirectSignalLanes`, `ClearDirectSignalLaneSnapshots`, and `SignalLanePolicyCache<T>.DirectRegistryDispatch`. That was a central switch hidden inside the new typed corridor. A second issue was worse for storm behavior: `Configure(expectedCapacity, laneHash: ...)` inherited the old optional defaults of 10000 max-frame signals and 1000 low-tier signals, so legacy lanes prewarmed through Core could accept a 5000-signal burst without deterministic frame-cap shedding unless they had an explicit max-frame override.

Solution: Replaced the concrete DTO dispatch table with a registered closed-generic dispatch table in `SignalBusRegistry`. Each initialized `SignalBus<T>` now registers dispose, flush, clear, telemetry, and pause policy delegates once; `FlushPreSimulation()` and `ClearPostSimulationSnapshots()` iterate that table. Removed `DirectRegistryDispatch` and the generated concrete flush/clear methods. Changed implicit `SignalBus<T>.Configure` semantics so missing `maxFrameSignals` resolves to `expectedCapacity`, and missing `lowTierFrameSignals` resolves to quarter capacity. Updated `RegisterLegacyLane<T>` to pass explicit max/low caps. Removed eight central legacy prewarm lines where domain-local owners already configure and prewarm the lane, then removed six dead central prewarm lines with no runtime source use outside generated hashes. Removed 16 duplicate `GlobalSignals.RuntimeLifecycle` configure/prewarm pairs whose lanes already have outside-Core owners. Reordered `Shinobu19EconomyLedger.WarmSignalLanes()` so each local configure is immediately followed by `EnsureInitialized()`.

Rejected Alternatives: Keeping the concrete DTO table was rejected because it preserves Core as the flush owner for every signal type. Raising registry capacity again was rejected because the problem was ownership shape, not array size. Keeping the 10000/1000 defaults was rejected because it contradicts deterministic storm shedding. Deleting all remaining 59 legacy prewarm lines was rejected because many still do not have domain-local owner route cards in source.

Scalability potential: Low tier now gets quarter-cap default frame budgets for legacy-style lanes instead of a 1000-signal low-tier default. Middle uses expected-capacity frame caps unless a domain explicitly buys a larger budget. High and Ultra can still request overkill frame budgets with explicit `maxFrameSignals`, but the default no longer silently grants 10000 entries.

Hardware Impact: Verified runtime savings remain 0us; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is bounded native snapshot pressure for implicit legacy lanes and less compile churn from the removed central DTO dispatch list.

Verification: hardcoded direct dispatch scan is 0 for `DirectRegistryDispatch`, `ResolveDirectRegistryDispatch`, `FlushDirectSignalLane`, `ClearDirectSignalLane`, `_fallbackDispatch`, `_fallbackLaneCount`, and `directDispatch`. `RegisterLegacyLane<T>` registration count is 59, Core lifecycle configure count is 131, and overlap with outside-Core configured lanes is 0. Immediate configure/prewarm gaps are 0. Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests is 0. Build was not launched because CPU was 96 percent with active `csc` and `dotnet` processes.

## 2026-05-24 - Canonical Storm Lane Contract Closure

Problem: Removing duplicate Core lifecycle prewarms exposed a first-initializer race for cross-domain lanes. A reactor bridge could initialize `BaseModuleCompromisedSignal` before habitat deformation configured it. Battery charger, seaglide, manta, gyro compass, metabolism, combat, and damage paths also had local `Configure` or `EnsureInitialized` paths capable of assigning generic defaults or local hashes to shared storm lanes.

Solution: Added DTO-owned capacity/hash contracts for `CombatDamageSignal`, `ImpactSignal`, `HighSpeedImpactSignal`, `AcousticPingSignal`, `ToolAcousticSignal`, `BubbleSpawnSignal`, `SubmarineLightsChangedSignal`, `AnomalyProximitySignal`, `BaseModuleCompromisedSignal`, `HullDeformedSignal`, `HullRepairedSignal`, `PhysiologyStateSignal`, `ReactorDamageSignal`, `PlayerRespawnSignal`, `InventoryRespawnDeathAupSignal`, `InventoryRespawnPenaltyResultSignal`, and `InventoryDeathLootCacheSignal`. `SignalBus<T>` now applies those known contracts before first native initialization and normalizes later `Configure(...)` calls back to the DTO contract. Patched the inspected reactor, habitat, atmosphere, acoustic, bubble, lights, anomaly, physiology, respawn, and inventory config/publish paths to use DTO constants. Replaced the remaining external `SignalBus<T>.Push` wrappers for this selected storm/cross-domain/respawn-inventory lane set with `TryPush` so bounded drops are visible at the producer call site. Direct `TryPush` now rejects before enqueue when a lane reaches `_expectedCapacity`, preventing single-thread storms from growing beyond the prewarmed native queue budget.

Rejected Alternatives: Reintroducing every removed Core prewarm was rejected because it restores central ownership. Patching only the reactor `EnsureInitialized` call was rejected because the same race existed on acoustic, bubble, light, damage, impact, hull, and physiology lanes. Leaving domain-specific hashes on shared generic lanes was rejected because telemetry/backpressure would lie about one lane as several facts.

Scalability potential: Low tier gets deterministic bounded capacities even when the first publisher is not the nominal owner. Middle keeps one stable telemetry hash per shared lane. High and Ultra can add richer consumers to these lanes without changing DTO layout, gameplay truth, or memory ownership.

Hardware Impact: 0us verified runtime savings; no Unity profiler/GCMonitor capture was run. Static effect on i3/MX350-class hardware is prevention of accidental 64-entry/default-hash initialization for storm lanes such as combat damage and acoustic pings, plus stable bounded drop/coalescing under 5000-signal bursts.

Verification: Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside `Core/Signals`, Editor, and Tests is 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI, Editor, and Tests is 0. External `SignalBus<T>.Push` wrappers for the selected storm/cross-domain/respawn-inventory lane set outside `Core/Signals`, Editor, and Tests are 0. External selected-lane `ParallelWriter` compatibility opens remain at 10 job producer sites and stay on native MPSC lanes, not managed events. Core signal DTO banned-field scan is 0. Touched-file brace balance is 0. `git diff --check` reports no whitespace errors, only LF-to-CRLF warnings. Build was not launched because CPU guard reported 56.7 percent, then 66.9 percent.

## 2026-05-24 - TryPush Surface Closure

Problem: The hot corridor had no external `GlobalSignals` producers left, but many first-party producers still called `SignalBus<T>.Push(...)`, a `void` wrapper over `TryPush(...)`. That hid overload/drop semantics at call sites and left editor smoke tests looking for the retired wrapper text.

Solution: Converted 169 external runtime `SignalBus<T>.Push(...)` calls across 87 files to `TryPush(...)`, converted 121 internal Core facade/determinism calls to `TryPush(...)`, and updated editor static probes to expect `TryPush(...)`. The wrapper remains as a compatibility API in `SignalBusRuntime`, but project source no longer uses it.

Rejected Alternatives: Deleting `Push(...)` was rejected because it is a public compatibility surface and could break staged external/test integrations without a batch boundary. Reading the bool return at every fire-and-forget presentation producer was rejected for this pass because it would manufacture per-domain fallback policy without owner route cards; telemetry already records shed/corrupt counts in `TryPush` and flush.

Scalability potential: Low tier benefits from producer-visible bounded semantics and no accidental migration back to a silent `void` wrapper. Middle keeps deterministic frame snapshots. High and Ultra can still attach richer VISUAL_SYNC consumers without changing gameplay truth ownership or DTO layout.

Hardware Impact: 0us verified runtime savings; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is lower relapse risk and clearer producer-side drop semantics. No managed allocation is added.

Verification: `rg -n "SignalBus<[^>]+>\.Push" Assets/_Project/Scripts -g "*.cs"` returns 0 hits after editor probe updates. External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside `Core/Signals`, Editor, and Tests is 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI, Editor, and Tests is 0. Core signal DTO managed/string/native-container field scan is 0. Build was not launched because CPU guard reported 100 percent.

## 2026-05-24 - Parallel Writer Budget Closure

Problem: Main-thread `SignalBus<T>.TryPush` rejected at `_queue.Count >= _expectedCapacity`, but job-side producers using `SignalBus<T>.ParallelWriter` still reached `NativeQueue<T>.ParallelWriter.Enqueue` without a producer-side budget claim. Flush-time shedding was deterministic after the fact, but it did not prevent pre-flush native queue block pressure during a 5000-signal parallel burst.

Solution: Added a per-closed-generic `NativeArray<int>[2]` to `SignalBus<T>` for job-writer remaining budget and pre-enqueue drop count. The budget resolves to `max(1, min(expectedCapacity, LaneOverflowFaultThreshold))`, is reset on lane init and flush, and is consumed by `SignalBus<T>.TryEnqueueBounded(...)` with atomic decrement before native enqueue. Migrated the first-party runtime job-writer surface to carry `ParallelWriterBudget` beside every `SignalBus<T>.ParallelWriter` / `OpenParallelWriter()` and replaced selected signal writer `.Enqueue(...)` calls with `TryEnqueueBounded(...)`.

Rejected Alternatives: Relying only on flush/load-shed counters was rejected because the queue had already accepted native blocks by then. Adding managed dictionaries or delegates for job-side coalescing was rejected because Burst/hot lanes need unmanaged, fixed memory. Deleting `ParallelWriter` outright was rejected because several systems legitimately produce from jobs and already own dependency fences.

Scalability potential: Low tier gets hard pre-enqueue native budget for job storms. Middle tier keeps deterministic snapshots and lane telemetry without queue growth surprises. High and Ultra can still spend explicit per-lane capacity on visual-overkill consumers, but that capacity must be bought through the lane contract instead of implicit native queue expansion.

Hardware Impact: 0us verified runtime saving; Unity profiler/GCMonitor was not run. Static expected effect on i3/MX350-class hardware is prevention of avoidable native queue block pressure during parallel collision/damage/acoustic/UI bursts, with no managed allocation and no `GlobalSignals` relapse.

Verification: `Docs/Reports/SIGNAL_PARALLEL_WRITER_BUDGET_CLOSURE_X_001.md` records 57 external first-party writer acquisition sites, 57 matching `ParallelWriterBudget` acquisition sites, 60 external first-party `TryEnqueueBounded` call sites, and 47 unique job-writer lane types. `SignalBus<...>.Push` source hits are 0. External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests is 0. First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is 0. Core signal DTO banned-field scan is 0. Build was not launched because the latest guard reported CPU 100 percent with 0 compiler processes, and CPU exceeds the 50 percent threshold.

## 2026-05-24 - Local Sidecar And Residual Writer Budget Closure

Problem: After the parallel writer budget pass, the strict hot corridor was clean, but several adjacent routes still weakened the proof. `ShinobuApexBrainJob` carried raw proximity/mock-damage/panic writer enqueue calls. Notification, Atlas, directive, narrative, and pool diagnostic event payloads used hash payloads but resolved them through growable `Dictionary<uint,string>` sidecars. Narrative/order consumers could grow string identity state without a hard cap. Organic drop and KCC mock-input owner queues accepted job writes without a native pre-enqueue budget. The retired gas toxicity queue still exposed a writer field and needed hard proof that it never enqueues.

Solution: Patched 20 runtime files. Added native writer budgets to Apex brain schedules and jobs, bounded organic drops and KCC mock input before native enqueue, replaced five `uint -> string` event/diagnostic dictionaries with fixed slot arrays, capped narrative and corporate order identity growth, retired the gas toxicity enqueue path with a constant-false helper, and kept all local `SignalBus<T>.Configure(...)` calls immediately paired with `EnsureInitialized()`. Wrote `Docs/Reports/SIGNAL_LOCAL_SIDECAR_AND_WRITER_BUDGET_CLOSURE_X_001.md`.

Rejected Alternatives: Leaving the dictionaries as "cold enough" was rejected because they sit beside event payload routes and make hash payload proof depend on managed map growth. Increasing queue capacities was rejected because it hides storms instead of bounding producers. Routing AI.Cognition directly through Core `SignalBus<T>` was rejected because that would add the dependency the local comments already forbid; owner-passed budgets keep the assembly boundary intact.

Scalability potential: Low tier gets fixed slots and native pre-enqueue budgets with deterministic drops. Middle tier keeps the same functional diagnostics and narrative identity resolution under explicit caps. High and Ultra can spend explicit lane capacity or richer presentation consumers without changing DTO layout, authority route, or save identity.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is prevention of hidden native queue block pressure and managed sidecar growth during signal/event storms.

Verification: runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside `Core/Signals`, Editor, and Tests is 0; `SignalBus<...>.Push` source hits are 0; first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is 0; Core signal DTO managed/string/native-container field scan is 0; first-party runtime raw `*Writer.Enqueue(...)` outside allowed helper/modding/editor/test contexts is 0; configure/prewarm scanner reports `ConfigureHits=243` and `MissingImmediateEnsure=0`; `git diff --check` reports only LF-to-CRLF warnings. Build was not launched because CPU was 100 percent.

## 2026-05-24 - Queue Ingress Budget Closure

Problem: The hot signal corridor no longer had external `GlobalSignals` producers, but adjacent ingress routes still hid storm pressure. `ThreadSafeCommandQueue.Enqueue` was a silent void command producer. Bootstrap failure diagnostics still used a growable `Dictionary<uint,string>`. Fluid rupture, mock drone task, and wrist vitals jobs used owner-local native queues without a native pre-enqueue budget claim. Resource/scavenge spawn queues, pool returns, and stale pending counters could hide queue growth or false-full state.

Solution: Added `ThreadSafeCommandQueue.TryEnqueue(in EntityCommand)` with fixed pending/drop counters, overflow telemetry, and storage reservation negative ack. Converted first-party command producers to `TryEnqueue`. Replaced bootstrap failure reason dictionary with an 8-slot fixed sidecar. Added owner-passed native writer budgets to fluid rupture, drone task, and wrist vitals local queues. Capped spawn ingress and ghost-proxy promotion queues, bounded pool/particle returns, retired gas toxicity enqueue, and reset stale pending counters on failed dequeue.

Rejected Alternatives: Leaving local queues out of scope was rejected because they receive storm-shaped gameplay and event facts next to the signal corridor. Increasing capacities was rejected because it hides overload instead of proving deterministic drop/coalescing. Converting Quest and ModdingAPI cold dictionaries was rejected because the remaining `Dictionary<uint,string>` hits are cold diagnostics/mod lookup, not signal DTO payloads or first-party hot broadcast paths.

Scalability potential: Low tier gets fixed command/spawn/local-writer ingress and deterministic drops instead of hidden queue growth. Middle keeps current functionality with explicit overflow telemetry. High and Ultra can buy higher explicit capacities through owner contracts without changing DTO layout, authority route, or save identity.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is prevention of managed sidecar growth and native queue block pressure during command, rupture, spawn, vitals, and event bursts.

Verification: runtime hot-route scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `SignalBus<T>.Push`, and `ThreadSafeCommandQueue.Enqueue` outside Core/Signals/Editor/Tests/ModdingAPI is 0. Scoped writer-budget scans over auxiliary, fabrication, seismic, physiology, scavenging, seed-ship, rollback, and terminal surfaces show matching `ParallelWriterBudget` plus `TryEnqueueBounded`. `git diff --check` reports no errors beyond LF-to-CRLF warnings. Build attempt timed out after 124 seconds; retry is blocked by CPU/compiler guard.

## 2026-05-24 - Local Event Counter Recovery

Problem: The strict signal corridor was clean, but many owner-local event lanes still depended on post-loop `IsEmpty()` cleanup after `TryDequeue` failure. If a native queue/counter pair ever desynchronized, those paths could keep a stale positive pending count and create a false-full local event lane. That is not a central `GlobalSignals` relapse, but it still weakens deterministic overload recovery beside the signal corridor.

Solution: Patched 48 runtime files so failed `TryDequeue` branches reset their associated pending counter before returning or breaking. The pass covers bootstrap, audio log, crafting, inventory, interaction, narrative, save, scan, localization, Atlas, module status, UI, airlock, submarine OS, weather, power telemetry, biome/celestial, first-hour/ending/eclipse, soundscape, emergency relay, pool diagnostics, performance, MapMagic, suit mesh, player-signal, random-event, core command/registry, world chunk, sargassum, PDA, submarine atmosphere/electrolysis, spatial audio, repair drone, visor, and quest queues.

Rejected Alternatives: Increasing local queue capacities was rejected because it hides overload and costs native memory. Moving these owner-local lanes into Core was rejected because it would invent cross-domain ownership. Relying only on the end-of-loop `queue.IsEmpty()` cleanup was rejected because the failure branch itself must leave a deterministic counter state.

Scalability potential: Low tier gets deterministic local event-lane recovery without higher capacity or managed fallback. Middle tier keeps the same event behavior with less false-full risk. High and Ultra can still spend explicit capacity on richer presentation events, but stale counters no longer silently block future bounded ingress.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected gain on i3/MX350-class hardware is prevention of local event-lane false-full stalls under storm-shaped gameplay/UI/world event traffic, with no heap allocation and no queue growth.

Verification: 48 runtime files patched. Full runtime counted-dequeue scanner after excluding prewarm/smoke-test loops reports `TotalMissingCountedReset=0`. Brace delta scan has no output. Runtime hot-route scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `SignalBus<T>.Push`, and `ThreadSafeCommandQueue.Enqueue` outside Core/Signals/Editor/Tests/ModdingAPI returns 0 hits. DTO field scan over extracted payload/contract files reports 0 managed/string/native-container field declarations. `git diff --check` reports only LF-to-CRLF warnings. Build blocked by guard: CPU 100.0 percent with active `dotnet`.

## 2026-05-24 - Contract And Native Queue Hardening

Problem: The main signal corridor had no external `GlobalSignals` hot callers, but several remaining paths still weakened storm proof. Late `SignalBus<T>.Configure(...)` could mutate capacity/hash after native initialization. Fatal `PlayerFatalPressureSignal` could be enqueued from a job through bounded writer semantics, bypassing fatal interrupt handling. `FluidIncursionSignal`, `ToxicityExposureSignal`, and `HabitatFloodAcousticMuffleSignal` had competing local configs. Hydraulic erosion, anomaly deferred flood-fill, and wreck propagation used owner-local native queues without hard admission semantics. Spatial audio captions still had a string sidecar and string-first request API.

Solution: Patched 27 runtime/editor files. `ConfigureInternal` now refuses mismatched late configuration after initialization and logs one fault. `TryEnqueueBounded` rejects fatal lanes. Respawn fatal pressure publishing moved to owner-phase `TryPush`. Added DTO-owned contracts for `DeflectSignal`, `DeconstructResultSignal`, `InteractionUiSignal`, `FluidIncursionSignal`, `HabitatFloodAcousticMuffleSignal`, and `ToxicityExposureSignal`, then patched conflicting configure sites to those constants. Added native budget/drop counters to `BurstCallback`, hydraulic height deltas, and anomaly deferred state. Bounded wreck propagation with explicit overflow termination. Retired legacy voxel/vehicle `Publish` calls behind `TryPublish`. Converted submarine caption ingress to hash-only `AudioCaptionEvents.TryRaiseHash`; `AudioCaptionPayload` now carries only unmanaged fields and static hash resolution happens at UI presentation edge.

Rejected Alternatives: Allowing late `Configure` to overwrite live lanes was rejected because first-initializer races can falsify capacity/hash telemetry. Treating fatal job writers as normal bounded lanes was rejected because fatal lanes own simulation authority. Increasing hydraulic/anomaly/wreck queue capacities was rejected because it hides storm shape instead of bounding admission. Keeping caption string sidecar as "only UI" was rejected because it was still a semantic event payload crossing a deferred queue.

Scalability potential: Low tier gets fixed native budgets, explicit overflow status, and no managed caption sidecar. Middle tier keeps deterministic signal identities and owner-local queue recovery. High and Ultra can buy richer presentation by raising explicit lane capacities or adding consumers without changing DTO layout, save identity, or authority route.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is less native queue block pressure during erosion/anomaly/wreck/caption storms and fewer hidden managed sidecar references in hot-adjacent event paths.

Verification: `SignalBus<T>.Push`, runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`, first-party non-modding `HectonEventBus.Publish/Subscribe/Unsubscribe`, `ThreadSafeCommandQueue.Enqueue`, legacy voxel/vehicle publish calls, and string-based `AudioCaptionEvents` ingress are 0. DTO banned-field scan over extracted payload/contracts is 0. `AudioCaptionPayload` banned-field scan is 0. Runtime configure/prewarm heuristic reports `MissingImmediateEnsure=0`. Touched-file brace delta is 0. `git diff --check` reports LF-to-CRLF warnings only. Build blocked by guard: CPU 100 percent with active `csc` and eight active `dotnet` processes.

## 2026-05-24 - Try Surface And Hash Save Closure

Problem: The central `GlobalSignals` hot route was clean, but several first-party hot-adjacent lanes still presented as void `Raise*`/`Publish*` calls. That hides bounded refusal from producers. `SaveEventPayload` was worse: it carried `FixedString64Bytes SlotName` and `FixedString128Bytes Message` through a deferred native queue, so save/load failures could move string-shaped data through an event DTO instead of hashes.

Solution: Patched 30 targeted runtime/editor files. `SaveEventPayload` now carries only `SlotHash`, `MessageHash`, and a fixed `MessageSlot` index. `SaveEvents.TryRaise*` requires precomputed slot/message hashes and stores transient UI text in `MessageSlot[16]`, released after dispatch/drain. Converted selected airlock, player-signal, DirectorAI, Spectrum, soundscape, celestial, biome, atmosphere, acoustic-zone, and physics-impact producer facades to `TryRaise*`/`TryNotify*`; old selected wrappers are `[Obsolete(..., true)]`.

Rejected Alternatives: Keeping `FixedString` in `SaveEventPayload` was rejected because it still makes the DTO a string carrier. Letting `Raise*` silently drop events was rejected because overload behavior must be visible at the producer edge. Moving airlock and save UI references into `SignalBus<T>` was rejected because those are owner-local scene/UI callback lanes; the correction is bounded admission plus hash-only DTOs, not central ownership creep.

Scalability potential: Low tier gets deterministic refusal at small fixed capacities and no string/FixString DTO transport. Middle tier keeps current save/airlock/player/visor presentation behavior through fixed sidecars and bounded queues. High and Ultra can raise explicit owner capacities or add richer presentation consumers without changing DTO layout, save identity, or authority route.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is less hidden event pressure during save/load UI bursts, airlock animation transitions, player trauma spam, sonar/spectrum bursts, and DirectorAI threat spikes, with no managed queue growth and no `GlobalSignals` relapse.

Verification: selected old `Raise`/`Publish` call-site scan for save, airlock, player, DirectorAI, Spectrum, soundscape, celestial, biome, atmosphere, acoustic, physics, and previously closed Atlas/Narrative/Scan/Weather/Audio/Crafting/Quest/Interaction/bridge/determinism wrappers is 0 outside wrapper declarations. Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue` scans are 0. `SaveEventPayload` has 0 `FixedString`, `string`, `GameObject`, or `Transform` fields. `git diff --check` reports LF-to-CRLF warnings only. Build blocked by guard: CPU 52.9 percent with no compiler, then CPU 50.2 percent with eight active compiler processes.

## 2026-05-24 - Owner-Local TryRaise And Sidecar Closure

Problem: Several owner-local lanes had been hardened for counters and flush recovery, but their producer API still exposed `void Raise*` methods. That hid admission refusal when fixed NativeQueue or SignalBus capacity was reached. Two selected lanes also kept growable managed dictionaries as sidecars: depth-zone profile resolution and emergency-relay live relay resolution.

Solution: Patched 22 runtime files. Added `TryRaise*` or `TryRaise` to eclipse, ending, first-hour, random-event, depth-zone, emergency-relay, base-integrity, tool-effect, laser-cutter, flashlight, PDA, suit-mesh, power-grid, and submarine-OS event surfaces. Marked the old selected wrappers `[Obsolete(..., true)]` and updated all selected first-party call sites, including the missed voxel shockwave producer. Replaced the depth-zone `Dictionary<uint, DepthZoneProfile>` with `ProfileSlot[32]` and the emergency-relay `Dictionary<ulong, EmergencyServiceRelay>` with `RelaySlot[32]`.

Rejected Alternatives: Keeping void wrappers was rejected because overflow would remain invisible at producer edges. Increasing capacities was rejected because it hides storms instead of exposing bounded refusal. Leaving the two dictionaries because they were small was rejected because a 5000-event storm must not depend on managed map growth. Moving these owner-local scene/UI lanes into Core `SignalBus<T>` was rejected because it would centralize ownership and add cross-domain compile pressure without a route-card owner.

Scalability potential: Low tier gets explicit refusal on fixed 8/16/32-entry lanes and no managed sidecar growth. Middle tier keeps current event behavior with deterministic capped queues. High and Ultra can buy richer presentation through explicit owner capacities or additional consumers without changing DTO layout, save identity, or the authority route.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is less hidden event pressure and no managed dictionary growth in the selected depth/emergency sidecars during storm-shaped gameplay, PDA, power, submarine, and UI event bursts.

Verification: 22 runtime files touched. Selected old `Raise` call-site scan for the converted lanes is 0 outside obsolete wrapper declarations. Selected depth-zone/emergency-relay dictionary sidecar scan is 0. Runtime hot-route scans for `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue` remain 0. Core signal DTO managed/string/native-container field scan is 0. Touched-file brace delta is 0. `git diff --check` reports LF-to-CRLF warnings only. Build blocked by guard: CPU 37 percent with active `dotnet` PID 42500.

## 2026-05-25 - TryPublish Deferred Ingress Closure

Problem: The central signal corridor stayed clean, but selected adjacent lanes still exposed `void Raise/Publish/Notify` ingress. That hides producer-side refusal when fixed queues or SignalBus lanes are saturated. `MapMagicTerrainTileEvents` was worse: tile events invoked listeners synchronously from the third-party bridge path and carried managed `Terrain`/provider references directly through an immediate callback instead of a deferred owner-lane drain.

Solution: Patched 24 runtime files. Converted selected performance, localization, MapMagic biome/tile, module status, player expression, object-pool diagnostics, fluid splash, tether, geology telemetry, and HUD luminance APIs to explicit `Try*` surfaces. Old selected wrappers are compile-time banned with `Obsolete(..., true)`. Added missing drop counters to performance/localization/MapMagic/module/player-expression lanes. Made `PlayerExpressionEventPayload` explicit 8-byte unmanaged layout. Rebuilt `MapMagicTerrainTileEvents` as a deferred `NativeQueue<MapMagicTerrainTileEventPayload>` with a 16-slot fixed snapshot sidecar and dispatcher integration; listener callbacks now run under late-frame event budget.

Rejected Alternatives: Leaving old wrappers was rejected because bounded failure would remain invisible. Moving MapMagic tile snapshots into Core `SignalBus<T>` was rejected because the payload contains scene/third-party references and would violate unmanaged DTO law; a fixed owner-local sidecar keeps managed refs out of native payloads. Increasing capacities was rejected because storms must hit fixed ceilings and shed deterministically. Removing listener callbacks was rejected because vegetation residency still consumes tile facts.

Scalability potential: Low tier gets visible refusal on 4/8/16/64/128-entry lanes and no synchronous MapMagic tile listener work from producer context. Middle tier keeps existing presentation and terrain-residency behavior with dispatcher budget control. High and Ultra can raise explicit owner capacities or attach richer presentation consumers without changing DTO layout, gameplay truth ownership, or save identity.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is less hidden queue pressure and removal of immediate MapMagic tile listener execution from the bridge producer path. The only compile-verified timing is CLI build wall time: 63.15s after a guarded restore.

Verification: selected old producer call-site scan is 0; runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside allowed Core/Signals/Editor/Tests/ModdingAPI zones is 0; `SignalBus<T>.Push` source scan is 0; first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests is 0; Core signal payload banned-field scan is 0; `git diff --check` reports LF-to-CRLF warnings only; `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.

## 2026-05-25 - Signal Refusal Telemetry Closure

Problem: Job-side `SignalBus<T>.TryEnqueueBounded(...)` already had a native budget/drop counter, but several runtime owners still threw away the returned bool. That meant overload was bounded at the lane but not visible to the reactor, hull, fluid, combat, fabrication, equipment, or inventory owner state. A 5000-signal burst would not allocate, but some local black-box/diagnostic routes would miss the fact that presentation or gameplay-adjacent signals were refused.

Solution: Patched 37 runtime/contract files. Reactor, fluid, hull, submarine, thermodynamics, cavitation, exosuit, KCC, vehicle, fabrication, equipment, inventory, ballistics, and combat producers now convert bounded refusal into existing unmanaged owner flags, counters, telemetry fault bits, or transaction result flags. `SargassumGlobalDragManager` first-party callers now use explicit `TryRaise*` routes while the old wrappers remain compile-time-banned compatibility surfaces.

Rejected Alternatives: Increasing lane capacities was rejected because it hides overload. Adding managed logs or dictionaries for refused signals was rejected because hot-route proof must stay zero-GC. Depending only on the generic lane drop counter was rejected because critical owners need local black-box context without querying global state.

Scalability potential: Low tier gets deterministic refusal and owner-visible drops at small fixed capacities. Middle tier keeps current gameplay/presentation behavior with better telemetry. High and Ultra can increase explicit lane capacity or add richer visual consumers, but gameplay truth ownership, DTO layout, and save identity stay unchanged.

Hardware Impact: 0us verified runtime saving; no Unity profiler/GCMonitor capture was run. Static expected effect on i3/MX350-class hardware is less hidden native queue pressure and more useful 300-frame state when reactor/fluid/combat/fabrication/equipment/inventory bursts exceed fixed budgets.

Verification: runtime statement-level `TryEnqueueBounded(...)` scan outside Core/Signals/Editor/Tests/ModdingAPI is 0. Runtime hot-route scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue` outside allowed zones is 0. Core signal DTO banned-field scan is 0. Touched-file brace delta is 0. Build was not launched because the latest guard reported CPU 100 percent with 0 compiler processes.
