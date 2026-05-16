# Status_VERLET_TOW_WINCH

Prompt: `VERLET_TOW_WINCH`
Role: `PHYSICS_PROGRAMMER`
Domain: `PHYSICS/KINEMATICS`
Authoritative code boundary: `Assets/_Project/Scripts/Physics/Tethers/`
Status policy: `PENDING VERIFICATION` until Unity/Profiler evidence exists.

## Mandates Loaded Before Code

- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Tether_Cable_Acceleration_Constraints.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `MATH_Rsqrt_i3_SIMD.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Iterative Loop Ledger

- Loop 1: Completed. Re-read XML, scanned first-party tether/joint state, implemented Tasks 1-5, and ran compile attempt 1. Build is blocked by unrelated cross-domain errors; no tether errors appeared in the reported set.
- Loop 2: Completed. Implemented Tasks 6-10, added High/Ultra indirect render path, and static-checked signal/DataVault/LOD paths.
- Loop 3: Completed. Re-read XML after task group, reviewed Tasks 11-15, and verified motion-vector mode, velocity clamps, telemetry tension, and dispatcher lane order by source inspection.
- Loop 4: Completed. Reviewed Tasks 16-18, force routing, snap cleanup, and compile dependency wall. Full validation is marked dependency-blocked, not passed.
- Loop 5: Completed. Read Omega Polish after all tasks were checked/blocked, verified `math.rsqrt` in `VerletCableSolverJob`, scanned for banned tether joints/singletons/distance calls, and ran final diff/static checks.
- Loop 6: Completed. Multiplatform inquisition pass: converted tether signal/telemetry structs to `Pack=1`, moved snap notifications to typed `SignalBus<TetherSnappedSignal>` with `ReadOnlySpan<T>` reads, removed the private fallback allocation for public cable DataVault lanes, moved remaining tether-owned NativeArray allocations to `H8Memory.Allocate/Release(SystemID.Physics)`, and re-ran static scans.
- Loop 7: Completed. Purged the private `NativeQueue<TetherFiredSignal>`, moved fire notifications to typed `SignalBus<TetherFiredSignal>`, repaired the compiled contract placement, and re-ran the directed compile probe until tether errors were gone.
- Loop 8: Completed. Re-read XML, moved the 300-frame Verlet blackbox ring and per-slot cursor into `GlobalDataVault`, updated the telemetry job/dump to use fixed vault slot offsets, and re-ran compile/static probes. Build remains dependency-blocked outside tether code.

## Titanium Task Checklist

- [x] 1. PURGE_SINGLETONS | DOD: first-party `rg` scan found no `TetherManager.Instance` dependency in tether code. Rejected: adding singleton access. Estimate: 0 us runtime.
- [x] 2. DEBT_CLEANUP | DOD: first-party tether/physics scan found no `ConfigurableJoint`, `SpringJoint`, or `HingeJoint` in the implemented path. Rejected: Unity Joint towing. Estimate: prevents unbounded PhysX solver cost; 0 us direct runtime.
- [x] 3. DATA_EVICTION | DOD: added fixed BufferID lanes and persistent DataVault cable point storage for canonical 11 points / 10 segments; public fallback allocation is removed and fails closed if the vault is absent. Rejected: managed per-frame arrays. Estimate: +3-6 us publish cost, 0 B GC.
- [x] 4. BURST_ALGORITHM | DOD: implemented `VerletCableSolverJob` with segment stretch constraints and `math.rsqrt`. Rejected: scalar spring/joint. Estimate: 8-20 us per active tether by tier.
- [x] 5. AUP_INTEGRITY | DOD: solver nodes are local offsets relative to tow anchor, with origin rebase separated from visual upload. Rejected: raw world-space node authority. Estimate: <1 us rebase for 11 nodes.
- [x] 6. DOD_SOA_LAYOUT | DOD: DataVault lanes store position, previous position, velocity, mass, and segment tension separately. Rejected: AoS cable node objects. Estimate: +3-6 us publish, cache-stable.
- [x] 7. SIGNAL_FLOW | DOD: added `TetherTensionSignal` with tension force, AUP endpoints, snap threshold, and reactive scalar. Rejected: direct VFX/audio references. Estimate: <2 us active publish.
- [x] 8. LOW_TIER_FAKE | DOD: Low/MX350 resolves 3 authority segments and renders a straight-line visual fake when tension is high. Rejected: full 10-segment solve on weak devices. Estimate: saves roughly 6-12 us versus 10-segment high path.
- [x] 9. HIGH_END_OVERKILL | DOD: High/Ultra path uses persistent segment mesh plus `Graphics.RenderMeshIndirect`, mapping `SV_InstanceID` to cable segments. Rejected: per-frame tube mesh generation. Estimate: CPU neutral to -5 us on high tier, buys richer shader pulse.
- [x] 10. REACTIVE_VFX | DOD: tension over 0.9 snap threshold drives shader stress and emits high-flag creak/impact signals. Rejected: polling components. Estimate: <2 us when threshold crossed.
- [x] 11. STP_STABILIZATION | DOD: tether render submissions use `MotionVectorGenerationMode.Camera` and persistent point buffers, avoiding invalid object-motion history. Rejected: per-frame mesh transforms. Estimate: 0 B GC, motion vectors remain camera-valid.
- [x] 12. NAN_VACCINATION | DOD: integration and DataVault velocity export clamp to `MaxCableVelocity` with finite guards; constraint correction weight uses an epsilon floor before reciprocal. Rejected: raw stuck-wreck velocity and raw near-zero reciprocal. Estimate: <1 us per 11-node pass.
- [x] 13. BLACKBOX_LOGGING | DOD: `PeakCableTension` is written to a `GlobalDataVault`-owned `TetherVerletTelemetryEntry` ring and binary dump `Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin`. Rejected: private per-instance telemetry ring. Estimate: 64 bytes/frame in fixed 300-frame ring; cursor write is one int/frame.
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: solver runs in `TetherManager.FixedTick` registered to `PriorityLayer.Environment`; `PlayerKinematicsRuntime` registers to `PriorityLayer.Player`; dispatcher lane order is Core=0, Environment=1, Player=2. Rejected: same-lane ordering guess. Estimate: 0 us direct overhead.
- [x] 15. HOMEOSTASIS_ADAPTATION | DOD: no adaptive homeostasis mutation was added; physics tiering is deterministic from scalability tier. Rejected: runtime self-tuning force changes. Estimate: 0 us.
- [x] 16. NEWTONS_3RD_LAW | DOD: peak tension queues equal/opposite endpoint force packets through `PhysicsForceRouter`, scaled by `MassSub / (MassSub + MassWreck)` and max acceleration. Rejected: direct `Rigidbody.AddForce` and one-sided tow force. Estimate: 2 queued force packets per active tow step.
- [x] 17. SNAP_LOGIC | DOD: snap stress clears DataVault cable slot and publishes `ImpactSignal` with snap material hash plus tether snap signal. Rejected: stale cable entries after break. Estimate: <3 us cleanup for fixed slot.
- [x] 18. FINAL_VALIDATION | `[BLOCKED BY DEPENDENCY]` Compile attempts fail in unrelated cross-domain code before project validation can pass. DOD used: full/directed dotnet probes and error attribution. Rejected: editing out-of-domain dependency walls. Estimate: no runtime claim.

## Multiplatform Inquisition Pass

- ARM64/Quest: `TetherTensionSignal`, `TetherSnappedSignal`, `TetherFiredSignal`, `TetherVerletTelemetryEntry`, and `TetherManagerTelemetryEntry` now declare `Pack=1` with explicit sizes. Remaining `Pack=4` hits in `GlobalSignals.cs` are unrelated pre-existing global contracts outside this tether patch.
- Metal/Mac: `Hecton_TetherLineStrip.shader` has no `numthreads`, `RWTexture`, `ByteAddressBuffer`, or DirectX-only token hits in the tether shader scan. It uses Unity cross-compiled vertex semantics `SV_VertexID` / `SV_InstanceID`.
- Steam Deck/I/O: tether binary dumps remain fault-path/dev-build only; no per-frame file reads or writes were added.
- Data sovereignty: public 10-segment cable SOA export and the 300-frame blackbox telemetry ring now resolve through `GlobalDataVault`; no private fallback is created when the vault is unavailable. The ring is partitioned by fixed tether slot.
- Memory sentinel: remaining tether visual/solver scratch `NativeArray` allocations use `H8Memory.Allocate(..., SystemID.Physics)` and release through `H8Memory.Release`. The Verlet blackbox ring/head are vault-owned views, not local persistent allocations.
- SignalBus: snap notification moved off private `NativeQueue<TetherSnappedSignal>` to typed `SignalBus<TetherSnappedSignal>` with `ReadOnlySpan<TetherSnappedSignal>` readback. Fire notification moved off private `NativeQueue<TetherFiredSignal>` to typed `SignalBus<TetherFiredSignal>` with `ReadOnlySpan<TetherFiredSignal>` snapshot reads. Tension remains typed `SignalBus<TetherTensionSignal>`.
- Remaining bounded exception: fire attach still uses a managed fixed-size request sidecar because it carries Unity object references for immediate same-frame attach. It is not a delegate/EventBus/native queue path, but it is not a pure unmanaged typed lane.
- Remaining bounded exception: solver and visual staging arrays remain local H8Memory-tracked scratch, not fully DataVault-evicted. The blackbox telemetry exception was removed; full solver scratch eviction still requires offset-aware solver/visual upload refactor.
- Hot-path debt scan: no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, LINQ, banned Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hits in the touched tether path.

## Compile Attempts

- Attempt 1: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` failed after reaching `Hecton8.Core` due unrelated cross-domain errors in `PlayerKinematicsRuntime`, `HectonVisorFluidDistortionFeature`, `SpatialAudioManager`, brine-layer call sites, and ecosystem macro-swarm types. No tether-related compiler errors appeared in the reported error set. Status: `[BLOCKED BY DEPENDENCY]` for full-project compile evidence.
- Attempt 2: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed before tether validation on missing `Hecton8.AI.Perception`, `Hecton8.Animation.Fauna`, `IResolutionScalerService`, `JawIkTarget`, `CurrentJawPos`, and `BiteIkSolveEvent`. Status: `[BLOCKED BY DEPENDENCY]`.
- Attempt 3: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` timed out after 306 seconds with no final compiler result. Status remains `[BLOCKED BY DEPENDENCY]`; no successful Unity/profiler validation exists.
- Attempt 4: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed on unrelated docking, wake/flora, and ecosystem interface errors. No tether compile errors appeared in the reported set.
- Attempt 5: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed before C# analysis because `Temp/obj/Hecton8.Core/project.assets.json` was missing.
- Attempt 6: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` exposed a tether contract placement error for `TetherFiredSignal` plus unrelated fauna errors. Tether error was fixed by moving the compiled fire payload contract into `TetherSignals.cs`.
- Attempt 7: same command failed because the generated project still referenced `Physics/Tethers/Contracts/TetherSignalContracts.cs` after the dead contract stub was deleted. The source path and Unity metadata were restored as an empty compile anchor.
- Attempt 8: same command exposed unqualified fire payload resolution as `Hecton8.Physics.TetherFiredSignal`; fixed by explicitly routing runtime fire payload usage through the `Hecton8.Core.Contracts.Signals.TetherFiredSignal` contract alias.
- Attempt 9: same command failed only on unrelated `GameBootstrapper` / `ModuloSimulationBucketer` namespace errors. No tether compiler errors appeared in the reported set.
- Attempt 10: same command failed on unrelated XR refresh-rate API, item signal import, submarine structural breach buffers, biolum buffers, and vault probe generic inference errors. No tether compiler errors appeared in the reported set.
- Attempt 11: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed before tether validation on an unrelated `GlobalDataVault.ValidateAbiLayout` duplicate report. No tether compiler errors appeared.
- Attempt 12: `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` failed on unrelated Sargassum, MarineSnow, and VehicleDocking missing-member errors. No tether compiler errors appeared in the reported set.

## Omega Polish

- Polish mandate read after task closure: `Did you use math.rsqrt for the constraint solving?`
- Result: `math.rsqrt` is present in `VerletCableSolverJob` constraint normalization and velocity clamps.
- Status: `PENDING VERIFICATION`, not `VERIFIED MASTER GRADE`, because project compile is dependency-blocked and no Unity runtime/profiler pass was available.
