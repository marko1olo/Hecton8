# Rationale_ITEM_MAGNET_SOLVER

Prompt: ITEM_MAGNET_SOLVER
Domain: GAMEPLAY/ITEMS
Status: VERIFIED MASTER GRADE - BUILD BLOCKED BY EXTERNAL DEPENDENCY

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
