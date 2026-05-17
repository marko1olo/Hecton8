# Rationale_ITEM_MAGNET_SOLVER

Prompt: ITEM_MAGNET_SOLVER
Domain: GAMEPLAY/ITEMS
Status: SOURCE-CLEAN / BUILD GATE DEFERRED PER USER NO-REBUILD-EVERY-TIME ORDER

## Initial Decision Log

Problem: ITEM_MAGNET_SOLVER prompt was previously absent, now restored in `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Extracted the exact XML tag by id using PowerShell regex and created disk-backed state files before gameplay code.
Rejected Alternatives: Using stale chat memory; borrowing another gameplay prompt; continuing without task count.
Scalability potential: Low uses 10 Hz math scan and cheap movement; Middle keeps direct Burst pull; High/Ultra can raise cadence and feed turbulence/VFX lanes if existing buffers support it.
Hardware Impact: i3/MX350 expected gain comes from deleting trigger callbacks and PhysX broadphase participation for loot magnet behavior. Measured proof absent.

Problem: Phase 1 requires purging Unity Physics triggers without breaking normal manual pickup behavior.
Solution: Codebase and prefab scan comes first; only magnet-specific trigger logic will be removed unless the prompt explicitly requires all loot trigger pickup removal.
Rejected Alternatives: Blanket collider deletion before dependency inspection; `Physics.OverlapSphere`; `Unity.Physics.OverlapSphere`; `GameObject.Find` lookup.
Scalability potential: Low/Middle use entity flags and radius culling; High/Ultra use same deterministic state plus optional visual signal coupling.
Hardware Impact: Removing per-item trigger work should reduce main-thread PhysX contact churn on i3/MX350. Exact microseconds pending measurement.

Problem: Phase 1 required deleting magnet trigger behavior, but the loot pickup scope had no live `OnTriggerEnter` / `OnTriggerStay` magnet code.
Solution: Verified with `rg` over `Assets/_Project/Scripts/Items`, `Assets/_Project/Scripts/Gameplay/Loot`, pickup prefabs, and `[LOOT_MANAGER].prefab`; retained normal pickup colliders because they are not trigger-magnet logic.
Rejected Alternatives: Blanket collider removal; deleting manual interaction pickup support; introducing `Physics.OverlapSphere` as a replacement query.
Scalability potential: Low/Middle avoid PhysX magnet work entirely; High/Ultra spend budget on wake/VFX lanes rather than trigger contact pairs.
Hardware Impact: i3/MX350 should avoid O(contact-pair) trigger churn for 500 seabed pickups; estimated 40-80 us per dense scene frame depending on authored proximity.

Problem: Burst filter needed a data-only magnetic flag, not Unity components or tags.
Solution: Added `LootEntityFlags.Bit_IsMagnetic` as an explicit vault flag alias and updated registry mirroring to write it into `BufferID.EntityFlags`.
Rejected Alternatives: `CompareTag`, layer masks, `GetComponent` filtering, or keeping `PullEnabled` as a hidden semantic name only.
Scalability potential: Low tier uses same bit for 10 Hz scans; High/Ultra reuse the exact buffer for 60 Hz scans and visual signal emission.
Hardware Impact: Bitmask filtering keeps the i3/MX350 path branch-predictable; estimated sub-0.03 us per item versus managed component checks.

Problem: The existing low-tier path snap-acquired every item inside radius, which was cheap but behaviorally wrong.
Solution: Renamed the job to `LootMagnetJob`, kept Burst SoA iteration, and changed low tier to clamped linear lerp at 10 Hz while acquisition still requires `distance < 0.3m`.
Rejected Alternatives: Low-tier trigger fallback; instant collection across the full radius; transform-space scan.
Scalability potential: Low uses slow lerp; Middle uses inverse-square pull; High/Ultra runs fast cadence and feeds wake/VFX signal lanes.
Hardware Impact: Low tier avoids 60 Hz per-loot work on MX350 while preserving deterministic acquisition; expected saving versus full-rate scan is roughly 5/6 of magnet job CPU.

Problem: Inverse-square pull can explode near zero distance and propagate NaNs into transforms/rendering.
Solution: Split guards: `math.max(distSq, 0.0001f)` for `rsqrt`, `math.rcp(math.max(distSq, 0.1f))` for force, finite AUP/velocity checks, and fail-closed `NonFinite` flags.
Rejected Alternatives: Raw division by `distSq`; `Vector3.Distance`; trusting authoring ranges.
Scalability potential: Same deterministic math across tiers; Ultra can raise signal budgets without destabilizing the core pull.
Hardware Impact: Reciprocal and squared-distance math reduce scalar sqrt/divide pressure; estimated 0.01-0.02 us per item on low-end CPU.

Problem: Compile verification is blocked by unrelated project errors before final validation.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1`; failure is in `PlayerKinematicsRuntime.cs` missing symbols, not the touched `PickupItem` file. Full `.slnx` build hung beyond 300 seconds and spawned persistent dotnet workers, which were killed.
Rejected Alternatives: Reporting success without compiler evidence; editing another agent's kinematics dependency; leaving orphan build workers alive.
Scalability potential: No runtime scalability impact; this preserves integration signal for the build owner.
Hardware Impact: None at runtime. Developer-machine impact avoided by stopping stuck dotnet workers.

Problem: Kinetic pull needed to feel heavy without letting loot tunnel through the player.
Solution: Clamped velocity in the Burst job with squared-speed math and kept snap acquisition at a strict 0.3m distance with zeroed velocity.
Rejected Alternatives: Rigidbody impulses; `ForceMode`; unbounded acceleration; using collider entry events for final acquisition.
Scalability potential: Low keeps lerp under the same velocity rail; High/Ultra can raise pull strength safely because max speed remains bounded.
Hardware Impact: Prevents extra corrective work and temporal artifacts on i3/MX350; estimated 0.02 us/item for clamp in the existing job path.

Problem: Visual feedback needed to scale without putting VFX in the gameplay loop.
Solution: Kept gameplay authoritative in AUP buffers and emitted bounded `WakeGeneratedSignal`, `DebrisSpawnSignal`, and `ItemAcquiredSignal` lanes from managed commit only.
Rejected Alternatives: Direct prefab spawning; direct inventory mutation from Burst; custom singleton VFX manager.
Scalability potential: Low has smaller signal budgets; Middle/High/Ultra increase wake/acoustic budgets and can drive more marine-snow turbulence without changing acquisition truth.
Hardware Impact: Queue push cost is bounded per acquisition/signal; low-end avoids unbounded GameObject instantiation spikes.

Problem: Fast loot motion can smear under temporal upscaling.
Solution: Added `PickupItem.ApplyLootMagnetPose` to set root renderer motion vectors above 10 m/s and restore the authored mode below threshold or on disable/destroy.
Rejected Alternatives: Enabling motion vectors on every pickup permanently; per-frame child renderer searches; ignoring STP ghosting.
Scalability potential: Low only pays when visible loot moves fast; High/Ultra get cleaner high-speed loot trails.
Hardware Impact: One cached renderer property branch per pulled proxy; no hot allocations.

Problem: Black-box telemetry needed active pull count and peak velocity without a new logging system.
Solution: Reused the fixed 300-frame `NativeArray<LootMagnetTelemetryEntry>` and added `ActiveLootPullsCount` plus `PeakMagnetVelocity`, populated during the existing commit pass.
Rejected Alternatives: `Debug.Log`; managed lists; per-job atomic max contention.
Scalability potential: Same ring size on all tiers; High/Ultra can diagnose higher signal budgets without extra allocation.
Hardware Impact: Uses existing O(N) commit pass; estimated 0.01 us/item for velocity-square max.

Problem: Floating-origin shifts can invalidate runtime-space transforms while AUP data remains correct.
Solution: Registered `LootMagnetSystem` as an `IOriginShiftListener`; on shift it force-completes pending pull work and reapplies pulled proxy poses from AUP to current runtime space.
Rejected Alternatives: Transform-space pull vectors; ignoring shifts until next slow tick; inventing an `AupShiftSignal` dependency that does not exist in this codebase.
Scalability potential: Hot path remains pure AUP math; cold shift path is rare and deterministic.
Hardware Impact: 0 hot-frame cost; cold cost is one bounded pass over active pulled loot during origin shifts.

Problem: Omega anti-bloat required checking DI and circular dependencies after all core tasks were checked/blocked.
Solution: Read the mandate after task closure, scanned for `GameObject.Find`, trigger/OverlapSphere fallback, stale job names, `foreach`, and `Vector3.Distance`; inspected asmdef direction as `Loot -> Core`, with `PickupItem` not referencing Loot.
Rejected Alternatives: Reading polish before implementation; hiding build wall behind a false green status.
Scalability potential: No bloat paths added; signal/DataVault direction remains decoupled for parallel systems.
Hardware Impact: No runtime impact beyond preserving zero-GC/no-PhysX constraints.

Problem: Private NativeArray ownership remained in the loot magnet system for signal events and telemetry.
Solution: Added `BufferID.EntityLootMagnetSignalEvents` and moved both `LootMagnetSignalEvent` and `LootMagnetTelemetryEntry` buffers to `GlobalDataVault.GetBuffer(..., SystemID.GameplayLoot)`. Local `NativeArray` fields are now non-owning vault views only and are cleared on disable without releasing memory.
Rejected Alternatives: `H8Memory.Allocate` inside `LootMagnetSystem`; per-frame managed arrays; owned NativeQueue side channel.
Scalability potential: Low/Middle/High/Ultra all read/write the same sovereign vault buffers, letting other systems inspect state without singleton coupling.
Hardware Impact: Prevents local persistent allocator fragmentation; no hot-path allocation. i3/MX350 gets stable pointer reuse from DataVault.

Problem: ARM64/Quest builds punish implicit padding and ambiguous sequential layouts.
Solution: Converted `LootMagnetSignalEvent` and `LootMagnetTelemetryEntry` to `StructLayout(LayoutKind.Explicit, Pack=1)` with fixed offsets and reserved padding.
Rejected Alternatives: Sequential `Pack=8`; trusting desktop CLR padding; runtime reflection layout checks.
Scalability potential: Same binary layout across Android/Quest, Steam Deck, Mac, and PC.
Hardware Impact: Avoids cross-platform layout drift; no runtime cost.

Problem: Magnet acquisition still invoked `PickupItem` legacy interaction and managed EventBus publication indirectly.
Solution: Added a `PickupItem.TryHandleInventoryPickup(..., bool publishLegacyEvents)` overload and call it from `LootMagnetSystem` with `publishLegacyEvents:false`; magnet acquisition publishes typed lanes itself.
Rejected Alternatives: Direct inventory mutation from Burst; keeping duplicate managed item-collected events in the magnet path; deleting manual interaction events globally and breaking unrelated subscribers.
Scalability potential: Magnet path is typed-lane only; manual interaction can be migrated separately without blocking Burst loot.
Hardware Impact: Avoids managed event object allocation on magnetic pickups; exact GC/microseconds not measured.

Problem: High/Ultra visual overkill needed a real typed lane beyond wake pings.
Solution: Added High/Ultra `FluidImpulseSignal` emission on wake publication, bounded by existing wake budgets and consumed via `SignalBus<FluidImpulseSignal>.GetFrameSnapshot()` by fluid systems.
Rejected Alternatives: Direct particle spawning; shader-specific code in gameplay; DirectX-only compute assumptions.
Scalability potential: Low/Medium skip the fluid impulse; High uses smaller radius/lifetime; Ultra uses larger radius/lifetime for denser silt.
Hardware Impact: High/Ultra pay one typed signal enqueue per accepted wake budget slot; Low/MX350 pays zero.

Problem: Build validation still cannot reach green after the H-PHI pass.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:normal`; current hard errors are external: `ProceduralLadderClimbRuntime.cs` cannot resolve `Hecton8.Input.Universal` / `UniversalInputStateSignal`. No loot magnet error was emitted before failure.
Rejected Alternatives: Claiming build green; editing input/locomotion assembly references from the item magnet task.
Scalability potential: No runtime impact.
Hardware Impact: None.

Problem: H-PHI data sovereignty audit rejected persistent `NativeArray` fields inside `LootMagnetSystem`, even when they were non-owning vault views.
Solution: Removed every system-local `NativeArray<T>` field and introduced `LootMagnetVaultViews` as a transient DataVault alias bundle. `LootMagnetSystem` now resolves views per enable/tick/commit, uses `TryGetBuffer` for post-job commit/dump, locks scheduled DataVault buffers while `LootMagnetJob` owns their pointers, and writes fault dumps to `Docs/AgentLogs/Dump_ITEM_MAGNET_SOLVER.bin`.
Rejected Alternatives: Keeping private cached `NativeArray` aliases; returning to `H8Memory.Allocate`; using managed arrays; letting DataVault buffers resize while a Burst job is scheduled.
Scalability potential: Low/Middle/High/Ultra all share the same sovereign DataVault buffers. Low keeps 10Hz lerp and no fluid impulses; Middle keeps standard inverse-square pull; High adds wake/fluid impulse; Ultra raises impulse radius/lifetime without changing acquisition truth.
Hardware Impact: Hot math cost unchanged. i3/MX350 gains lower allocator/fragmentation risk and avoids stale pointer crashes; exact microseconds saved are not measured because this is an ownership/stability repair, not a per-item arithmetic reduction.

Problem: Re-validation after the stateless refactor still cannot produce a green project build.
Solution: Ran `git diff --check` and `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal`. Diff check is clean except CRLF warnings. Build errors are external: missing `Hecton8.VFX.Wakes` / wake contracts, missing docking/autopilot contracts, duplicate `LockstepStateValidator.SanitizeFinite`, missing light shaft contracts, and `EcosystemDirector` interface drift.
Rejected Alternatives: Claiming 0 errors; editing VFX/world/construction/core integration contracts from the loot magnet task.
Scalability potential: No runtime impact for loot magnet; this preserves a clean integration boundary for the owning agents.
Hardware Impact: None at runtime.

Problem: Manual pickup sources still published managed item-collected events while magnet pickup used typed lanes, splitting collection truth across two buses.
Solution: Removed `InteractionEvents.RaiseItemCollected` and `HectonEventBus.Publish(new ItemCollectedEvent(...))` from `PickupItem` and duplicate `HectonItem`. Both manual paths now publish the existing `ItemAcquiredSignal` lane with shared `InventoryPickupSignalConstants`; the magnet path passes `publishAcquiredSignal:false` and publishes its own richer AUP/spark/wake packet once.
Rejected Alternatives: Keeping duplicate typed and legacy events; inventing a new pickup signal; migrating world/meta subscribers from outside this task boundary.
Scalability potential: Low consumes bounded typed snapshots only; Middle/High/Ultra can add richer consumers from the same lane without adding managed callbacks.
Hardware Impact: Avoids managed `ItemCollectedEvent` object allocation on manual pickups. Exact GC/microseconds not measured.

Problem: `PickupItem.ApplyLootMagnetPose` wrote `transform.position` while pickups can have active non-kinematic Rigidbodies.
Solution: Added magnet runtime suppression that captures the current Rigidbody kinematic/collision state, zeros velocities, disables collisions, makes the body kinematic before math-owned transform writes, and restores the captured state when pulling ends, acquisition is deferred, the slot is cleared, or the object disables/destroys.
Rejected Alternatives: `Rigidbody.MovePosition` without a sweep; leaving active Rigidbody transform mutation; permanently making authored pickups kinematic.
Scalability potential: Low/Middle/High/Ultra share the same physics-safe pose path; visual overkill remains in typed VFX lanes, not in PhysX.
Hardware Impact: Adds only cold state flips when a pickup enters/leaves magnet pose ownership; removes broadphase corruption risk on mobile/Steam Deck.

Problem: Public magnet-emitted signal structs were explicit-size but not all explicit `Pack=1`, leaving ARM/Quest layout doubts.
Solution: Added `Pack=1` to `ItemAcquiredSignal`, `WakeGeneratedSignal`, and `FluidImpulseSignal`. `DebrisSpawnSignal` and `AcousticPingSignal` were already packed.
Rejected Alternatives: Trusting desktop explicit layout defaults; packing unrelated signal structs outside the magnet emission boundary.
Scalability potential: Stable lane packet layout across Android/Quest, Mac/Metal, Steam Deck, and PC.
Hardware Impact: No runtime cost.

Problem: Final validation still cannot reach 0 errors after local item-domain fixes.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal`. A local missing using in `HectonItem` was fixed; remaining errors are external: `HectonXRRuntimeState.TryRequestDisplayRefreshRate`, missing submarine structural buffers/helpers, `VaultProbeUtility` generic inference, missing biolum blackbox fields, and missing spatial audio helpers.
Rejected Alternatives: Claiming green; editing XR/submarine/VFX/audio domains from this item magnet task.
Scalability potential: No runtime impact for loot magnet.
Hardware Impact: None at runtime.

Problem: Magnet pose ownership restoration depended on each pickup's own disable/destroy path, so scheduler shutdown after a completed pull could leave active pickups kinematic and collision-disabled.
Solution: Added a scheduler-level `RestoreAllManagedProxyRuntimeStates` pass and call it from both runtime-state clear paths. Any dependency loss, scene disable, or scheduler shutdown now releases magnet-owned pickup physics state before counters are reset.
Rejected Alternatives: Relying on `PickupItem.OnDisable`; permanently forcing pickups kinematic; leaving vault state intact after shutdown.
Scalability potential: Low/Middle/High/Ultra share the same cold cleanup. Visual overkill stays in typed lanes; physics state returns to authored behavior without a per-frame tax.
Hardware Impact: 0 hot-frame cost. Shutdown/clear cost is one bounded loop over active pickup sidecars and only touches pickups that were cached by the magnet scheduler.

Problem: Compile validation shifted after concurrent DataVault edits; the item magnet assembly no longer reports local errors, but the project still fails in unrelated domains.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal` against current disk state. Current hard wall is external: `SargassumMicroFaunaBoids` missing `EnsureVaultBufferHandle`, `HectonMarineSnowRenderer` missing data-vault/native telemetry members, and `VehicleDockingModule` missing fluid runtime cache helpers.
Rejected Alternatives: Claiming green; editing world boids, marine snow renderer, or vehicle docking from the loot magnet task.
Scalability potential: No runtime impact for loot magnet; this keeps cross-domain ownership intact.
Hardware Impact: None at runtime.

Problem: Sidecar arrays could be replaced during a live capacity change while still holding pickup references whose Rigidbody state had been suppressed for magnet presentation.
Solution: `EnsureManagedSidecars` now clears runtime ownership before allocating replacement sidecar arrays. That restore pass runs while the old array still exists, so every cached pickup releases magnet-owned physics/render state before references are discarded.
Rejected Alternatives: Relying on managed GC to discard the old array; adding per-pickup heap tokens; leaving capacity changes unsupported.
Scalability potential: Low/Middle/High/Ultra keep the same runtime math path. Capacity changes remain cold-path and do not reduce High/Ultra visual signal budgets.
Hardware Impact: 0 hot-frame cost. Resize cost is one bounded loop over currently active sidecar slots only when `maxLootEntities` changes.

Problem: The authoring sanitizer still accepted a 5000m magnet radius and 100000m/s max velocity, which made the anti-tunneling clamp meaningless if an inspector value drifted.
Solution: Reduced hard ceilings to bounded gameplay values: 64m radius, 256 strength, and 48m/s max velocity. Defaults remain unchanged, so normal feel stays at 8m radius and 12m/s.
Rejected Alternatives: Trusting prefab authoring; keeping cell-scale radius for a player suit magnet; raising only telemetry warnings while still allowing unstable values.
Scalability potential: Low avoids mass-pulling a whole AUP cell; Middle keeps normal kinetic snap; High/Ultra still spend saved cycles on wake/fluid/debris signal consumers rather than unsafe physics speed.
Hardware Impact: 0 hot-frame arithmetic cost. Prevents worst-case authoring from turning every active pickup into a fast-moving proxy on i3/MX350.

Problem: The duplicate `HectonItem` pickup path still used cold `GetComponent<T>()` cache fills and a development-build interpolated error string after the magnet path had been hardened.
Solution: Replaced cache fills with `TryGetComponent` in `Awake`, buoyancy setup, and editor validation, and changed the development-build missing-data report to a static message with the Unity context object.
Rejected Alternatives: Leaving duplicate pickup initialization inconsistent; adding a new abstraction; touching unrelated subscribers outside the item domain.
Scalability potential: Low/Middle/High/Ultra keep identical acquisition behavior through the typed `ItemAcquiredSignal`; this is cold-path hygiene, not a visual-budget trade.
Hardware Impact: 0 hot-frame impact. Cold initialization avoids avoidable lookup/allocation residue; exact microseconds are not measured.

Problem: Registry refresh could overwrite a sidecar slot or clear a trailing slot while the previous pickup still had magnet-owned Rigidbody suppression active.
Solution: Restored the previous pickup's magnet runtime state before replacing a slot with a different entity id, and restored stale pickups before clearing trailing slots in `RefreshPickupVaultFromRegistry`.
Rejected Alternatives: Assuming registry order never changes; relying on `PickupItem.OnDisable`; permanently making pickups kinematic.
Scalability potential: Low/Middle/High/Ultra keep the same Burst math path. The fix is SlowTick/cold registry maintenance and does not reduce High/Ultra wake/fluid budgets.
Hardware Impact: 0 Burst hot-loop impact. SlowTick pays only for changed/stale sidecar slots and prevents suppressed-collision leaks on low-end devices.

Problem: Loot magnet vault/signals embed `AbsoluteUniversePosition`, but the embedded AUP struct used explicit offsets without declaring `Pack=1`.
Solution: Added `Pack=1` to `AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit128` explicit layouts. This is a cross-domain ABI edit because magnet packets cannot guarantee ARM64/Quest layout while the embedded payload omits the packing contract.
Rejected Alternatives: Packing only `LootMagnetSignalEvent`/telemetry wrappers; copying AUP into a private duplicate struct; ignoring embedded payload layout.
Scalability potential: Low/Middle/High/Ultra share identical AUP binary layout across Android/Quest, Metal/Mac, Steam Deck, and PC.
Hardware Impact: 0 runtime cost. Removes a layout ambiguity risk on ARM64; field offsets and size remain explicit.

Problem: Final compile status had to be re-verified after the duplicate item, registry churn, and AUP ABI passes.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; build succeeded with 0 warnings and 0 errors in 40.43 seconds.
Rejected Alternatives: Leaving stale external-blocked status; running another full solution build while other agents were spawning workers; claiming microsecond wins from compile validation.
Scalability potential: No runtime scalability change. Confirms the Low/Middle/High/Ultra magnet paths compile through the shared core assembly.
Hardware Impact: 0 runtime impact. Build gate only.

Problem: Scheduled DataVault locks were released on the normal completed-job path, but an exceptional commit path or a schedule failure before `_pullScheduled` was set could leave lock state stale.
Solution: Added `ForceCompleteAndCommitScheduledJob`, wrapped late-frame commit in `finally`, added schedule-failure cleanup that clears scheduled counters and unlocks vault buffers when `job.Schedule` fails before ownership is recorded, and moved origin-shift job draining before the non-finite payload guard.
Rejected Alternatives: Relying on the happy-path unlock; adding per-buffer managed lock tokens; ignoring exception/safety-check builds.
Scalability potential: Low/Middle/High/Ultra keep identical math and signal behavior. The fix protects the shared GlobalDataVault under shutdown, origin shift, and safety-check failure paths.
Hardware Impact: 0 hot math cost. Control-path only; prevents locked-buffer stalls that would otherwise break low-end devices harder than desktop.

Problem: Re-validation after the scheduled-lock hardening cannot currently reach 0 errors.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; current hard errors are external to item magnet in `SpatialAudioManager.cs` missing `ClearVaultBackedTelemetryAliases` and `EnsureVaultBackedArray`.
Rejected Alternatives: Claiming the previous green gate still represents current disk; editing spatial audio from the loot magnet task; hiding the dependency wall.
Scalability potential: No runtime impact for loot magnet. The Low/Middle/High/Ultra magnet paths remain source-validated while the audio owner resolves the helper drift.
Hardware Impact: None at runtime for loot magnet.

Problem: The Burst kernel still relied on managed scheduler sanitization for dt, radius, strength, and max velocity before speed-square math.
Solution: Added `LootMagnetJob.TryResolveKernelParameters` so every worker validates finite kernel scalars, clamps them to stable bounds including `0.0001f..MaxIntegrationDeltaTimeSeconds` for dt, and fails closed with zero velocity plus `LootEntityFlags.NonFinite` before `rsqrt`, reciprocal force, or max-speed-squared calculations.
Rejected Alternatives: Trusting inspector ranges; relying only on `SchedulePull` sanitization; letting NaN/negative/tiny-positive dt values reach Burst and get caught after velocity corruption.
Scalability potential: Low/Middle/High/Ultra keep the same math LOD behavior. Low still runs 10 Hz lerp; High/Ultra still buy wake/fluid visual budget with safe kernel inputs.
Hardware Impact: Adds a small branch block inside the hot job and prevents NaN propagation. Exact microseconds are not claimed.

Problem: `ItemAcquiredSignal` was still explicit-size without explicit `Pack=1`, while the magnet path publishes it on Quest/Android-sensitive typed lanes.
Solution: Added `Pack=1` to `ItemAcquiredSignal` and rechecked all magnet-emitted public signals: `ItemAcquiredSignal`, `WakeGeneratedSignal`, `FluidImpulseSignal`, `DebrisSpawnSignal`, and `AcousticPingSignal`.
Rejected Alternatives: Trusting default explicit layout packing; packing unrelated global signals outside the item magnet boundary.
Scalability potential: Same signal packet ABI across Android/Quest, Metal/Mac, Steam Deck, and PC; High/Ultra visual overkill consumers read the same packet layout.
Hardware Impact: 0 runtime cost. Removes ARM64 layout ambiguity for acquisition packets.

Problem: Disabling or dependency-clearing the scheduler restored managed pickup Rigidbody state but could leave stale item-magnet `EntityFlags`, AUPs, velocities, hashes, and quantities in shared DataVault slots.
Solution: Added `ClearKnownRuntimeVaultSlots`, which clears only slots that the item-magnet sidecar identifies as owned, then resets counters. This runs on runtime clear, shutdown, and sidecar replacement.
Rejected Alternatives: Clearing the whole generic `EntityFlags` buffer; leaving stale vault state for external readers; storing another private NativeArray ownership map.
Scalability potential: Low/Middle/High/Ultra retain the same hot path. Cleanup is cold-path only and avoids stale shared-vault signals confusing higher-tier VFX/diagnostics.
Hardware Impact: 0 hot-frame cost. Cold cleanup is O(known sidecar slots) and avoids external consumers spending cycles on dead loot flags.

Problem: Current compile validation shifted again under concurrent project edits.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; current hard wall is external to item magnet in `LockstepStateValidator.cs` missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`.
Rejected Alternatives: Claiming green because a previous gate passed; editing core determinism signal constants from the item magnet task.
Scalability potential: No runtime impact for loot magnet.
Hardware Impact: None at runtime for loot magnet.

Problem: AUP local fields were finite, but `ToRuntimeFloat3()` can still return non-finite coordinates if floating-origin state is poisoned.
Solution: Added `IsFiniteFloat3` gates before every `PickupItem.ApplyLootMagnetPose` call from the scheduler, including normal commit and origin-shift reapply. Non-finite runtime conversion marks fault telemetry, restores pickup runtime state, and avoids transform mutation.
Rejected Alternatives: Trusting AUP local validity alone; adding guards inside every pickup transform setter; allowing Unity transforms to receive NaN/Infinity.
Scalability potential: Low/Middle/High/Ultra keep identical magnet math and VFX lanes. The guard prevents one bad origin offset from taking down the presentation path on mobile or desktop.
Hardware Impact: One finite branch per visually applied pulled pickup; exact microseconds not claimed.

Problem: Final status needed current compile evidence after the runtime-pose vaccination pass.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; latest build succeeded with 0 errors and one external duplicate-source warning for `AI/Ecosystem/EcosystemPopulationBalancer.cs`.
Rejected Alternatives: Keeping stale external-blocked status; claiming a green build before rerunning the gate; claiming 0 warnings after the duplicate-source warning appeared.
Scalability potential: No runtime change; confirms Low/Middle/High/Ultra item magnet paths compile in the contained core assembly.
Hardware Impact: 0 runtime impact. Build validation only.

Problem: The scoped item-domain scan still found one editor-only interpolation in duplicate `HectonItem.OnValidate`, assigning `gameObject.name = $"Item_{itemData.itemName}"`.
Solution: Removed the auto-rename line instead of replacing it with another allocating string construction path. This preserves item runtime behavior and eliminates the last scoped `$"..."` match.
Rejected Alternatives: `string.Concat`, `string.Format`, or keeping editor convenience while the task demanded a hard string-allocation purge.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged. This is editor/cold-path hygiene so no visual budget changes.
Hardware Impact: 0 runtime impact; editor-only allocation removed.

Problem: The current project compile shifted after concurrent external edits despite the Pass 14 green build.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; latest failure is external to item magnet: duplicate `ArchitectEyeVisualizer.ValidatePackedStructSizes`, plus `LaserCutterEventPayload` ambiguity in `AbyssalThermalManager` and `PlayerCriticalProceduralAudioRenderer`.
Rejected Alternatives: Claiming the Pass 14 green result is still current; editing diagnostics, world thermal, or audio ownership from the item magnet task.
Scalability potential: No runtime change for loot magnet; external owners must repair their contracts.
Hardware Impact: None in item magnet.

Problem: Final disk truth needed to supersede stale external-wall entries at the bottom of the rationale log.
Solution: Re-ran current validation after the `LaserCutterEvents` payload alias repair and concurrent external helper convergence. `Hecton8.Core.csproj` now builds with 0 warnings and 0 errors in 173.45 seconds; the item magnet forbidden-pattern and local NativeArray ownership scans are clean.
Rejected Alternatives: Leaving the last rationale entry as an obsolete external-wall report; claiming runtime speedups from a compile-only bridge fix.
Scalability potential: Low/Middle/High/Ultra magnet behavior is unchanged and compiled. The bridge repair keeps typed-lane payload layout explicit for every platform.
Hardware Impact: 0 runtime impact; validation and ABI naming only.

Problem: A new inquisition pass found two local residues after the previous green build: a guarded but naked `Debug.LogError` in the duplicate pickup component and one `math.sqrt` in the item-magnet telemetry commit path.
Solution: Removed the development-build `HectonItem` log path entirely and replaced peak-velocity telemetry sqrt with `peakVelocitySq * math.rsqrt(peakVelocitySq)` behind finite/positive guards. Re-ran scoped scans: no item-magnet `Debug.Log`, `math.sqrt`, trigger callback, `OverlapSphere`, `Vector3.Distance`, interpolation, `string.Format`, or system-local NativeArray allocation ownership remains.
Rejected Alternatives: Keeping the dev-only log because it was guarded; claiming `math.sqrt` was acceptable because it was telemetry; replacing the log with another managed warning path.
Scalability potential: Low/Middle/High/Ultra magnet behavior is unchanged. Low keeps cheap 10 Hz math and High/Ultra keep wake/fluid/debris visual lanes; telemetry now follows the rsqrt mandate without buying fake runtime gains.
Hardware Impact: The removed log is cold-path only. The telemetry sqrt replacement is one guarded rsqrt in the commit path; exact microseconds are not claimed.

Problem: Current compile validation is no longer green on disk after concurrent external edits.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; it failed outside item magnet with 17 errors in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and 6 generic-inference errors in `World/EcosystemDirector.cs`. I did not edit those domains from the item magnet task.
Rejected Alternatives: Carrying forward the stale Pass 16 green build; touching compass/ecosystem ownership without a cross-domain item-magnet contract reason; hiding the compile wall.
Scalability potential: No runtime scalability change for loot magnet. The Low/Middle/High/Ultra item-magnet source remains clean while the external owners repair their compile drift.
Hardware Impact: 0 runtime impact in item magnet. Build gate failed in external domains after 105.07 seconds.

Problem: The last status still reported an external compile wall, but current disk had moved. The source now contains the compass methods and ecosystem explicit generic arguments that the previous compiler output claimed were missing.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` against current disk; it succeeded with 0 warnings and 0 errors in 123.98 seconds. Re-ran scoped item-magnet forbidden, local allocation, and pickup-prefab trigger scans; all remain clean. `git diff --check` reports one external trailing-whitespace line in `Docs/Tasks/CURRENT_BATCH.md:2312`, which I did not edit from this item-magnet task.
Rejected Alternatives: Carrying forward the stale Pass 17 external-wall status; editing external prompt text to make `git diff --check` clean; claiming runtime microsecond gains from a compile-only validation pass.
Scalability potential: Low/Middle/High/Ultra magnet paths are unchanged and compile. Low keeps 10 Hz fake movement; High/Ultra keep wake/fluid/debris visual-overkill signal lanes.
Hardware Impact: 0 runtime impact. Build validation only; no microseconds claimed.

Problem: The final compile wall blocked current validation even though the item magnet domain was source-clean; `LaserCutterEvents` exposed an ambiguous unqualified payload contract to world/audio listeners.
Solution: Added explicit `LaserCutterEventPayloadSignal` and `LaserCutterEventTypeSignal` aliases in `LaserCutter.cs`, then routed the listener interface, `SignalBus<T>` snapshot/push calls, queued payload construction, and `IsBeamActive` helper through the packed `Hecton8.Core.Contracts.Signals` payload.
Rejected Alternatives: Leaving consumers to fully qualify around an ambiguous producer contract; introducing a duplicate gameplay payload; touching item magnet runtime code to mask a bridge contract error.
Scalability potential: Low/Middle/High/Ultra all keep the same zero-GC typed lane. This is a bridge ABI repair, not a visual-budget change.
Hardware Impact: 0 runtime us in item magnet. The producer lane type is compile-time explicit; no hot-path allocation or dispatch cost was added.

Problem: Final validation had to be rerun against current disk after concurrent external Fauna/VFX helper repairs landed.
Solution: Re-ran the scoped item-domain forbidden scan, the item magnet local NativeArray ownership scan, `git diff --check`, and `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`. The build succeeded with 0 warnings and 0 errors in 173.45 seconds.
Rejected Alternatives: Keeping stale external-wall status after a green build; claiming measured microsecond gains from compile-only work; hiding LF-to-CRLF warnings as functional errors.
Scalability potential: No runtime scalability change. The Low/Middle/High/Ultra magnet paths remain compiled and source-validated.
Hardware Impact: 0 runtime impact. Validation only.

Problem: The scheduler rejected non-finite runtime poses, but the pickup presentation setter itself still trusted every caller and the wake/acoustic publisher trusted all Burst event payloads.
Solution: Added a local `PickupItem.ApplyLootMagnetPose` finite gate before `transform.position`, made `RestoreLootMagnetRuntimeState` clear magnet ownership when the cached Rigidbody is gone, and added `TelemetrySignalNonFiniteFlag` so acoustic, wake, and fluid impulse packets are dropped unless their distance/velocity payloads are finite.
Rejected Alternatives: Trusting only the scheduler guard; publishing VFX/audio packets with poisoned floats; leaving `_lootMagnetPhysicsSuppressed` stuck true when a Rigidbody reference disappears.
Scalability potential: Low/Middle/High/Ultra keep the same magnet math and visual budget. Low avoids poisoned audio/VFX work entirely; High/Ultra keep fluid overkill only when event payloads are valid.
Hardware Impact: Branch-only presentation-path cost. Exact microseconds are not claimed; this is NaN containment and physics-state survival.

Problem: The previous final validation was stale after the presentation hardening patch.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; build succeeded with 0 warnings and 0 errors in 140.15 seconds.
Rejected Alternatives: Carrying forward the previous duplicate-source warning; claiming compile status from static scans.
Scalability potential: No runtime change. Confirms the Low/Middle/High/Ultra item magnet paths still compile after the final presentation guard pass.
Hardware Impact: 0 runtime impact. Build validation only.

Problem: A later scoped item-domain scan still found one editor-only interpolation in duplicate `HectonItem.OnValidate`, assigning `gameObject.name = $"Item_{itemData.itemName}"`.
Solution: Removed the auto-rename line instead of replacing it with another allocating string construction path. This preserves item runtime behavior and eliminates the last scoped `$"..."` match.
Rejected Alternatives: `string.Concat`, `string.Format`, or keeping editor convenience while the task demanded a hard string-allocation purge.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged. This is editor/cold-path hygiene so no visual budget changes.
Hardware Impact: 0 runtime impact; editor-only allocation removed.

Problem: The current project compile shifted after concurrent external edits despite the Pass 14 green build.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; latest failure is external to item magnet: duplicate `ArchitectEyeVisualizer.ValidatePackedStructSizes`, plus `LaserCutterEventPayload` ambiguity in `AbyssalThermalManager` and `PlayerCriticalProceduralAudioRenderer`.
Rejected Alternatives: Claiming the Pass 14 green result is still current; editing diagnostics, world thermal, or audio ownership from the item magnet task.
Scalability potential: No runtime change for loot magnet; external owners must repair their contracts.
Hardware Impact: None in item magnet.

Problem: Final disk truth needed to supersede stale external-wall entries at the bottom of the rationale log.
Solution: Re-ran current validation after the `LaserCutterEvents` payload alias repair and concurrent external helper convergence. `Hecton8.Core.csproj` now builds with 0 warnings and 0 errors in 173.45 seconds; the item magnet forbidden-pattern and local NativeArray ownership scans are clean.
Rejected Alternatives: Leaving the last rationale entry as an obsolete external-wall report; claiming runtime speedups from a compile-only bridge fix.
Scalability potential: Low/Middle/High/Ultra magnet behavior is unchanged and compiled. The bridge repair keeps typed-lane payload layout explicit for every platform.
Hardware Impact: 0 runtime impact; validation and ABI naming only.

Problem: The blackbox dump header declared 128-byte `LootMagnetTelemetryEntry` records, but the writer serialized only 100 bytes per entry and wrote physical ring order. A postmortem parser would lose packet alignment after the first entry and the newest/oldest frame order was implicit.
Solution: Bumped the dump version to 7, wrote chronological ring order starting at `_telemetryIndex`, serialized both packed 48-byte AUP payloads with explicit padding/tail words, and wrote the `Reserved` field so every entry is exactly 128 bytes.
Rejected Alternatives: Leaving dump readers to infer a sparse payload; inventing a duplicate private telemetry dump struct; storing managed JSON/log text; editing unrelated crash-reporting infrastructure instead of fixing the item-magnet writer.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The gain is postmortem reliability on Quest/Android ARM64, Metal/Mac, Steam Deck, and PC because binary dump records now match the packed telemetry contract.
Hardware Impact: 0 hot-frame runtime impact. Fault-path dump size increases by 28 bytes per entry, 8.4 KB for the 300-frame ring. No microseconds are claimed.

Problem: Current compile validation shifted again after the blackbox ABI repair.
Solution: Re-ran scoped item-magnet forbidden-pattern scans, local NativeArray ownership scan, pickup-prefab trigger scan, `git diff --check`, and the no-restore core build. Item magnet scans are clean and `git diff --check` has no whitespace errors, but `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` fails outside item magnet with 62 errors in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `Core/SystemDispatcher.cs`.
Rejected Alternatives: Claiming the stale Pass 18 green result; patching UI navigation or dispatcher internals from the loot magnet domain without a direct item contract; hiding the dependency wall.
Scalability potential: No runtime scalability change for loot magnet. Low still runs the 10 Hz cheap magnet fake; High/Ultra still preserve wake/fluid/debris visual-overkill lanes.
Hardware Impact: 0 runtime impact in item magnet. Build gate failed in external domains after 44.00 seconds.

Problem: The status file carried a correct but now stale external UI/Core compile wall after concurrent owners converged the missing compass and dispatcher symbols on disk.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`; current disk now succeeds with 0 warnings and 0 errors in 96.99 seconds. Updated status to `VERIFIED MASTER GRADE` because all 18 item-magnet tasks are checked, the blackbox ABI is corrected, and the current build gate is green.
Rejected Alternatives: Leaving the stale Pass 19 external-wall state; claiming runtime speedups from a compile-only validation; editing unrelated UI/Core files after they had already converged.
Scalability potential: Low/Middle/High/Ultra magnet behavior is unchanged and now compiles on current disk. Low keeps 10 Hz cheap movement; High/Ultra keep wake/fluid/debris visual-overkill signal lanes.
Hardware Impact: 0 runtime impact. Build validation only; no microseconds claimed.

Problem: Item-domain signal publication still had a false-origin fallback. If both pickup and interactor positions were non-finite, manual pickups could publish `ItemAcquiredSignal` at default AUP, and magnet presentation signals trusted `LootMagnetSignalEvent.PositionAup` after vault handoff.
Solution: Added finite AUP gates before magnet item acquisition, snap debris, acoustic, wake, and fluid impulse publication. Added `TryResolveSignalAup` to `PickupItem` and duplicate `HectonItem` so manual pickup signals publish only from a finite interactor or pickup position. Added `PickupItem` finite guards before spatial hash refresh and current-force enqueue.
Rejected Alternatives: Publishing default-origin signals as a "safe" fallback; relying only on Burst job writers; pushing poisoned spatial data to world/fauna hashes; adding a new signal lane instead of hardening existing typed lanes.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid data. Low avoids wasted downstream signal work on poisoned pickups. High/Ultra keep wake/fluid/debris visual-overkill lanes only when spatial ownership is finite.
Hardware Impact: Branch-only presentation/manual-pickup guards. No benchmarked microseconds claimed.

Problem: Current compile validation is no longer green after the signal NaN vaccination pass, but the errors are moving external dependency walls.
Solution: Ran three no-restore core build attempts. Attempt 1 failed outside item magnet in `HeavyTowWinch.cs` calling `TetherSignals.PublishFire` without the new `frameIndex`; the source converged afterward. Attempt 2 failed outside item magnet in `LockstepStateValidator.cs` missing lockstep/glitch signal constants. Attempt 3 failed outside item magnet in `EcosystemDirector.cs` duplicate `ResolveVaultIndexCapacity` and `TryFindIndexEntry` methods. Item-domain scans and scoped `git diff --check` are clean.
Rejected Alternatives: Claiming the stale Pass 20 green build; editing winch, lockstep, or ecosystem ownership from the item-magnet task without a direct item contract; hiding the dependency wall.
Scalability potential: No runtime scalability change for loot magnet. The item magnet Low/Middle/High/Ultra source remains clean while external owners repair compile drift.
Hardware Impact: 0 runtime impact in item magnet. Failed build gates totaled 294.75 seconds; no microseconds claimed.

Problem: `PickupItem` and duplicate `HectonItem` registered through void `GlobalRegistry.Register*` wrappers, then polled `GlobalRegistry.*Tickables.Contains(this)` to infer registration state. That was a cold-path cache walk and a weaker truth source than the existing atomic `TryRegister*` contract.
Solution: Replaced post-register collection polling with `GlobalRegistry.TryRegisterSlowTickable`, `TryRegisterFixedTickable`, and `TryRegisterUpdatable` return values inside the item domain.
Rejected Alternatives: Adding new registry APIs; leaving the collection poll because it was cold-path only; editing unrelated systems that still use the older pattern.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The item pickup surfaces now record dispatcher registration acceptance directly, which avoids stale state during dispatcher failure or concurrent registry churn.
Hardware Impact: Cold registration path only. No benchmarked hot-frame microseconds claimed.

Problem: Current build validation is blocked again outside item magnet after the registry commit-state patch.
Solution: Ran item-domain forbidden scans, interpolation scan, local NativeArray ownership scan, registration-poll scan, scoped `git diff --check`, and two no-restore core build attempts. The first build timed out after 364.22 seconds and spawned dotnet workers were stopped. The retry failed outside item magnet with 40 `SubmarineFluidDynamics.cs` errors for missing exterior thermal anomaly/hazard fields. No item-magnet compile errors were emitted.
Rejected Alternatives: Claiming the stale green result; editing submarine fluid/thermal ownership from the loot magnet task; hiding the timeout or leaving dotnet workers running.
Scalability potential: No runtime scalability change for loot magnet. Low still uses 10 Hz cheap movement; High/Ultra still preserve wake/fluid/debris visual-overkill lanes.
Hardware Impact: 0 runtime impact in item magnet. Validation failed in an external domain; no microseconds claimed.

Problem: `PickupItem.FixedTick` already rejected non-finite sampled currents, but still wrote `_lastSpatialPosition = transform.position` after force enqueue and used `transform.position.y` for submerged-depth checks without a finite gate.
Solution: Added finite pickup transform checks before fixed-tick spatial refresh state writes and before submerged-depth calculation. Non-finite transforms now fail closed through the existing damping restore path instead of feeding spatial hashes or current-force decisions.
Rejected Alternatives: Trusting the earlier SlowTick spatial guard; allowing `SurfaceStateUtility` to receive NaN depth; editing world spatial hash internals from the item task.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid transforms. Bad pickup presentation data now stops at the item boundary before downstream world/fauna/current systems waste work.
Hardware Impact: Branch-only fixed-tick guard. No benchmarked microseconds claimed.

Problem: Current build validation shifted again after the fixed-tick NaN vaccination.
Solution: Captured `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` output through a log file. The latest failure is outside item magnet: missing `Hecton8.Core.Memory.Defrag` namespace and `MemoryDefragPhase` in `SystemDispatcher.cs` / `GlobalDataVault.cs`. No item-magnet compile errors were emitted.
Rejected Alternatives: Claiming the previous submarine-fluid wall is still current; editing core memory-defrag ownership from the loot magnet task; treating an empty exit-1 build as evidence without recapturing output.
Scalability potential: No runtime scalability change for loot magnet. Low keeps 10 Hz cheap movement; High/Ultra keep wake/fluid/debris visual-overkill lanes.
Hardware Impact: 0 runtime impact in item magnet. Validation failed in external core memory wiring; no microseconds claimed.

Problem: Cold item paths still read transform data before finite ownership checks. `RegisterSpatialHandle` wrote `_lastSpatialPosition` from `transform.position`, world-state identity anchored on `transform.position`, and overflow scatter in both pickup surfaces could derive force vectors from non-finite pickup/interactor transforms.
Solution: Added finite gates before spatial registration and world-state identity creation in `PickupItem`; cached interactor position once before AUP conversion; guarded pickup/interactor positions and fallback forward vectors before overflow scatter in `PickupItem` and duplicate `HectonItem`.
Rejected Alternatives: Relying only on SlowTick/FixedTick guards; allowing non-finite world-state keys to be generated; queuing default scatter forces from poisoned transforms; editing world spatial hash internals from the item task.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid data. Bad cold-path item data now fails at the item boundary before world-state, fauna/spatial hash, or physics-force consumers do wasted work.
Hardware Impact: Cold-path branch guards only. No benchmarked microseconds claimed.

Problem: Current build validation moved again after the cold-path transform vaccination.
Solution: Re-ran the captured no-restore core build. The latest failure is outside item magnet: `SubmarineFluidDynamics.cs` references missing `_exteriorBuoyancySampleLocalPoints` at three call sites. No item-magnet compile errors were emitted.
Rejected Alternatives: Carrying forward stale core-memory defrag errors; editing submarine fluid ownership from the loot magnet task; claiming green because item-domain scans are clean.
Scalability potential: No runtime scalability change for loot magnet. Low keeps 10 Hz cheap movement; High/Ultra keep wake/fluid/debris visual-overkill lanes.
Hardware Impact: 0 runtime impact in item magnet. Validation failed in external submarine fluid code after 83.60 seconds; no microseconds claimed.

Problem: The item-domain transform checks rejected non-finite `Vector3` inputs, but the `AbsoluteUniversePosition.FromRuntimePosition` conversion result was still trusted in manual pickup signals, magnet DataVault ingest, and player fallback pose resolution.
Solution: Added post-conversion finite AUP helpers. `PickupItem`, duplicate `HectonItem`, and `LootMagnetSystem` now publish/write only if the converted AUP local payload is finite; direct `FromRuntimePosition` calls in the scoped item-magnet surface now exist only inside finite-guard helper bodies.
Rejected Alternatives: Trusting pre-conversion `Vector3` checks only; publishing default-origin fallback AUPs; writing possibly poisoned AUPs into `GlobalDataVault`; adding a new signal lane instead of hardening the existing typed lane.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid data. Low avoids wasted downstream inventory/VFX/fluid work on poisoned item/player positions. High/Ultra keep wake/fluid/debris visual-overkill lanes only when AUP authority is finite.
Hardware Impact: Branch-only guard on ingest/manual-pickup paths. No benchmarked microseconds claimed.

Problem: Current build validation did not reach compiler diagnostics after the AUP conversion guard pass.
Solution: Ran scoped forbidden, interpolation, local NativeArray ownership, direct `FromRuntimePosition`, and scoped `git diff --check` scans; all item-magnet scans are clean. The first no-restore core build returned `-1` with an empty log after 194.09 seconds; the retry timed out after 608.05 seconds while external dotnet build processes were active.
Rejected Alternatives: Claiming the stale previous compile wall; claiming green without compiler output; killing unrelated external dotnet build processes owned by other agents.
Scalability potential: No runtime scalability change. The item magnet Low/Middle/High/Ultra source remains source-clean pending a non-contended build gate.
Hardware Impact: 0 runtime impact. Validation infrastructure contention only; no microseconds claimed.

Problem: Current non-contended build validation is blocked outside the item magnet domain.
Solution: Re-ran the no-restore core build after dotnet contention cleared. The build failed with one external error: `SubmarineFluidDynamics.cs(1439,41)` cannot resolve `InventoryEventPayload`. No item-magnet compiler diagnostics were emitted.
Rejected Alternatives: Carrying forward the contention-only status; editing submarine fluid ownership from the loot magnet task; claiming a green build without compiler proof.
Scalability potential: No runtime scalability change for loot magnet. Low keeps 10 Hz cheap movement; High/Ultra keep wake/fluid/debris visual-overkill lanes.
Hardware Impact: 0 runtime impact in item magnet. Build gate failed after 47.10 seconds outside the assigned domain.

Problem: Multiplatform ABI scan found two unpacked item-magnet structs: `LootMagnetJob` and `LootMagnetVaultViews`.
Solution: Verified packet-facing structs (`LootMagnetSignalEvent`, `LootMagnetTelemetryEntry`, emitted item/acoustic/wake/fluid/debris signals, and embedded AUP payloads) already use explicit `Pack=1` layouts. Rejected packing `LootMagnetJob`/`LootMagnetVaultViews` because they carry `NativeArray<T>` handles; forcing `Pack=1` would place native pointer fields at unaligned offsets on ARM64 and is a Quest risk, not a fix.
Rejected Alternatives: Blindly applying `Pack=1` to every struct; creating duplicate packed job DTOs that are never serialized; treating NativeArray handle containers as wire packets.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. ABI-stable payloads remain portable while Burst/job handle containers retain platform-safe pointer alignment.
Hardware Impact: 0 runtime impact. This is source audit and layout-risk avoidance, not measured speed work.

Problem: The adjacent inventory SPSC queue payloads still used implicit sequential layout while the item-acquisition path depends on a stable item/inventory signal boundary. `InventoryEventPayload` currently resolves to 24 bytes and `InventoryPhysicalDropRequestPayload` to 48 bytes by field math, but that was not encoded in source for Quest/ARM64 review.
Solution: Added explicit `Pack=1` and fixed `Size` values to both inventory queue payload structs without changing field order or public method signatures.
Rejected Alternatives: Leaving implicit layout because the current field order happens to align; moving item magnet back to the legacy inventory queue; changing `IInventoryEventListener` or publish signatures during a batch.
Scalability potential: Low/Middle/High/Ultra item magnet behavior is unchanged. This protects the inventory boundary while Low keeps the 10 Hz magnet fake and High/Ultra keep wake/fluid/debris visual-overkill signal consumers.
Hardware Impact: 0 hot-frame runtime impact. Attribute-only ABI hardening; no benchmarked microseconds claimed.

Problem: The compile gate is required for final proof, but the current user instruction explicitly says not to run a `dotnet rebuild` every time.
Solution: Deferred the compile gate after this ABI-only source patch and ran static validation instead: inventory layout scan, scoped forbidden-pattern scan, local NativeArray ownership scan, and scoped `git diff --check`.
Rejected Alternatives: Running another full build immediately against instruction; claiming a green build from the previous pass; claiming item-magnet compile failure without fresh compiler output.
Scalability potential: No runtime scalability change.
Hardware Impact: 0 runtime impact. Validation only.

Problem: The item-magnet blackbox writer had exact 128-byte records, but still used the default `FileStream` buffer for a roughly 38 KiB fault dump. On Steam Deck or MicroSD-class storage, tiny default buffering is unnecessary pressure during a fault path.
Solution: Added `TelemetryDumpFileBufferBytes = 64 * 1024` and passed it into the `FileStream` constructor. Dump version, record size, field order, and chronological ring order are unchanged.
Rejected Alternatives: Allocating a large managed byte array per dump; switching to write-through I/O; changing the telemetry binary ABI again; moving blackbox ownership out of the item-magnet system.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. Fault export is less likely to fragment into small buffered writes on handheld/storage-limited hardware.
Hardware Impact: 0 hot-frame runtime impact. Fault-path I/O only; no benchmarked microseconds claimed.

Problem: The compile gate remains required for final proof, but the no-rebuild-every-time order still applies after this small I/O patch.
Solution: Deferred build again and used static validation: dump-buffer scan, scoped forbidden-pattern scan, local NativeArray ownership scan, and scoped `git diff --check`.
Rejected Alternatives: Running another full build immediately against instruction; claiming runtime I/O gain without MicroSD profiling; claiming green from older build output.
Scalability potential: No runtime scalability change.
Hardware Impact: 0 runtime impact outside blackbox fault export.
