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
- Loop 9: Completed. Moved the `TetherManager` 300-frame heartbeat ring and cursor out of local H8Memory ownership into `GlobalDataVault`, fixed BufferID collisions by moving manager lanes to IDs 232/233 after the current enum tail, and re-ran compile/static probes.
- Loop 10: Completed. Re-read status/rationale and XML, moved `TetherInstance` solver/visual scratch arrays to `GlobalDataVault` slot slices, removed local H8Memory ownership from the instance, fixed slice alias reuse on deactivation, and re-ran duplicate BufferID/static/build probes.
- Loop 11: Completed. Re-read status/rationale, purged the remaining managed fire attach resolver sidecar, kept `TetherFiredSignal` as a typed unmanaged observer lane, removed `Time.fixedTime` from payload-current sampling, and re-ran static, BufferID, core-build, and full-project probes.
- Loop 12: Completed. Re-read status/rationale and XML, double-buffered tether GPU position/tension uploads, added High/Ultra procedural fiber/salt/silt cable shading behind quality gates, hardened buffer-size guards, and re-ran static, diff, core-build, and full-project probes. The latest core/full probes are dependency-blocked by concurrent out-of-domain UI/diagnostics/RealtimeCSG errors; no tether errors appeared in the reported compiler sets.
- Loop 13: Completed. Re-read XML, replaced tether shader `_Time` with the manager fixed-step visual clock, replaced the sine hash with multiply/frac triangle fake math, moved tether signal frame stamps off `Time.frameCount`, and re-ran static/core probes. Core compile is still dependency-blocked outside tether code.

## Titanium Task Checklist

- [x] 1. PURGE_SINGLETONS | DOD: first-party `rg` scan found no `TetherManager.Instance` dependency in tether code. Rejected: adding singleton access. Estimate: 0 us runtime.
- [x] 2. DEBT_CLEANUP | DOD: first-party tether/physics scan found no `ConfigurableJoint`, `SpringJoint`, or `HingeJoint` in the implemented path. Rejected: Unity Joint towing. Estimate: prevents unbounded PhysX solver cost; 0 us direct runtime.
- [x] 3. DATA_EVICTION | DOD: added fixed BufferID lanes for public cable SOA plus vault-owned per-slot solver/visual scratch slices; public fallback allocation is removed and fails closed if the vault is absent. Rejected: managed per-frame arrays and local persistent NativeArray ownership. Estimate: +3-6 us publish cost, 0 B GC; scratch ownership move has no measured microsecond claim.
- [x] 4. BURST_ALGORITHM | DOD: implemented `VerletCableSolverJob` with segment stretch constraints and `math.rsqrt`. Rejected: scalar spring/joint. Estimate: 8-20 us per active tether by tier.
- [x] 5. AUP_INTEGRITY | DOD: solver nodes are local offsets relative to tow anchor, with origin rebase separated from visual upload. Rejected: raw world-space node authority. Estimate: <1 us rebase for 11 nodes.
- [x] 6. DOD_SOA_LAYOUT | DOD: DataVault lanes store position, previous position, velocity, mass, and segment tension separately. Rejected: AoS cable node objects. Estimate: +3-6 us publish, cache-stable.
- [x] 7. SIGNAL_FLOW | DOD: added `TetherTensionSignal` with tension force, AUP endpoints, snap threshold, and reactive scalar. Rejected: direct VFX/audio references. Estimate: <2 us active publish.
- [x] 8. LOW_TIER_FAKE | DOD: Low/MX350 resolves 3 authority segments and renders a straight-line visual fake when tension is high. Rejected: full 10-segment solve on weak devices. Estimate: saves roughly 6-12 us versus 10-segment high path.
- [x] 9. HIGH_END_OVERKILL | DOD: High/Ultra path uses persistent segment mesh plus `Graphics.RenderMeshIndirect`, double-buffered `LockBufferForWrite` GPU upload lanes, and a gated 16-tap procedural fiber/salt/silt cable shader. Rejected: per-frame tube mesh generation and Low-tier fragment overdraw. Estimate: CPU neutral to -5 us on high tier for indirect repetition; shader cost is quality-gated and unmeasured.
- [x] 10. REACTIVE_VFX | DOD: tension over 0.9 snap threshold drives shader stress and emits high-flag creak/impact signals. Rejected: polling components. Estimate: <2 us when threshold crossed.
- [x] 11. STP_STABILIZATION | DOD: tether render submissions use `MotionVectorGenerationMode.Camera` and persistent point buffers, avoiding invalid object-motion history. Rejected: per-frame mesh transforms. Estimate: 0 B GC, motion vectors remain camera-valid.
- [x] 12. NAN_VACCINATION | DOD: integration and DataVault velocity export clamp to `MaxCableVelocity` with finite guards; constraint correction weight uses an epsilon floor before reciprocal. Rejected: raw stuck-wreck velocity and raw near-zero reciprocal. Estimate: <1 us per 11-node pass.
- [x] 13. BLACKBOX_LOGGING | DOD: `PeakCableTension` is written to a `GlobalDataVault`-owned `TetherVerletTelemetryEntry` ring; `TetherManager` heartbeat now uses vault-owned `TetherManagerTelemetryEntry`; binary dump remains `Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin`. Rejected: private per-instance telemetry rings. Estimate: 64 bytes/frame for Verlet ring, 16 bytes/frame for manager ring; each cursor write is one int/frame.
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: solver runs in `TetherManager.FixedTick` registered to `PriorityLayer.Environment`; `PlayerKinematicsRuntime` registers to `PriorityLayer.Player`; dispatcher lane order is Core=0, Environment=1, Player=2. Rejected: same-lane ordering guess. Estimate: 0 us direct overhead.
- [x] 15. HOMEOSTASIS_ADAPTATION | DOD: no adaptive homeostasis mutation was added; physics tiering is deterministic from scalability tier. Rejected: runtime self-tuning force changes. Estimate: 0 us.
- [x] 16. NEWTONS_3RD_LAW | DOD: peak tension queues equal/opposite endpoint force packets through `PhysicsForceRouter`, scaled by `MassSub / (MassSub + MassWreck)` and max acceleration. Rejected: direct `Rigidbody.AddForce` and one-sided tow force. Estimate: 2 queued force packets per active tow step.
- [x] 17. SNAP_LOGIC | DOD: snap stress clears DataVault cable slot and publishes `ImpactSignal` with snap material hash plus tether snap signal. Rejected: stale cable entries after break. Estimate: <3 us cleanup for fixed slot.
- [x] 18. FINAL_VALIDATION | `[PARTIAL PASS / BLOCKED BY DEPENDENCY]` A core probe succeeded once after the fire-sidecar/fixed-clock pass and again after the initial GPU double-buffer pass; the latest probes are now blocked by concurrent out-of-domain `DiegeticGyroCompassRuntime`, `ArchitectEyeVisualizer`, `GlobalSignals`, and `RealtimeCSG.csproj` errors. DOD used: directed core build, full-project probe, static tether scans, and error attribution. Rejected: editing UI/diagnostics/package domains to make this tether report look clean. Estimate: no runtime claim.

## Multiplatform Inquisition Pass

- ARM64/Quest: `TetherTensionSignal`, `TetherSnappedSignal`, `TetherFiredSignal`, `TetherVerletTelemetryEntry`, and `TetherManagerTelemetryEntry` now declare `Pack=1` with explicit sizes. Remaining `Pack=4` hits in `GlobalSignals.cs` are unrelated pre-existing global contracts outside this tether patch.
- Metal/Mac: `Hecton_TetherLineStrip.shader` has no `_Time`, `sin(`, `numthreads`, `RWTexture`, `ByteAddressBuffer`, `groupshared`, or DirectX-only group-token hits in the tether shader scan. The High/Ultra visual path uses a fixed 16-tap fragment loop, not compute thread groups, and keeps Unity cross-compiled `SV_VertexID` / `SV_InstanceID` semantics.
- Steam Deck/I/O: tether binary dumps remain fault-path/dev-build only; no per-frame file reads or writes were added. GPU uploads now write to a non-current double buffer via `GraphicsBuffer.UsageFlags.LockBufferForWrite`, then flip the read index after upload.
- PC High/Ultra: High tier sets visual tier 2 for procedural cable fibers, salt glints, and silt tint. Ultra adds a stress rim on the same impostor path. Low/MX350 keeps the cheap taut-line visual fake and visual tier 0.
- Data sovereignty: public 10-segment cable SOA export, the 300-frame Verlet blackbox ring, the `TetherManager` heartbeat ring, and `TetherInstance` solver/visual scratch lanes now resolve through `GlobalDataVault`; no private fallback is created when the vault is unavailable. The Verlet ring and scratch lanes are partitioned by fixed tether slot.
- Memory sentinel: `TetherInstance` no longer calls `H8Memory.Allocate`/`H8Memory.Release` for visual or solver `NativeArray` state; its NativeArray fields are vault views. `TetherManager` blackbox fields are also vault-owned views.
- SignalBus: snap notification moved off private `NativeQueue<TetherSnappedSignal>` to typed `SignalBus<TetherSnappedSignal>` with `ReadOnlySpan<TetherSnappedSignal>` readback. Fire notification now publishes only the unmanaged typed `SignalBus<TetherFiredSignal>` observer payload; the Unity-object attach resolver sidecar was removed. Tension remains typed `SignalBus<TetherTensionSignal>`.
- Time source: payload-current sampling no longer reads `Time.fixedTime`; tether telemetry/signals/cooldowns no longer read `Time.frameCount`; the shader no longer reads `_Time`. `TetherManager` advances a finite wrapped fixed-step clock and fixed simulation frame index from dispatcher `fixedDeltaTime`, then passes them into `TetherInstance.Simulate` and the visual property block.
- Remaining bounded exception: no local persistent NativeArray owner remains in `TetherInstance`; job structs still receive `NativeArray` views over vault memory instead of direct vault handles.
- Hot-path debt scan: no `Time.frameCount`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, LINQ, banned Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hits in the touched tether path.

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
- Attempt 13: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed on unrelated Lockstep, Ecosystem, and SubmarineFluid dependency errors. No tether compiler errors appeared in the reported set.
- Attempt 14: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` exited non-zero with no errors emitted under `ErrorsOnly`; rerun without `ErrorsOnly` failed on unrelated `GameBootstrapper.Initialize` arity and `ToolDurabilitySystem` missing-field/member errors. No tether compiler errors appeared in the reported set.
- Attempt 15: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors after the fire-sidecar purge and fixed-step clock change.
- Attempt 16: `dotnet build Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed in unrelated `RealtimeCSG.csproj` missing source-file references. No tether compiler errors appeared in the reported set.
- Attempt 17: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` initially failed on an unrelated `LockstepStateValidator.ValidateBinaryLayout` resolution error while the source already contained that method. No tether compiler errors appeared in the reported set.
- Attempt 18: same core command reran successfully with 0 warnings and 0 errors after the initial GPU double-buffer/high-tier shader pass.
- Attempt 19: `dotnet build Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed in unrelated `RealtimeCSG.csproj` missing source-file references: 83 warnings, 216 errors. No tether compiler errors appeared in the reported set.
- Attempt 20: after the final double-buffer guard/comment patch, `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed on out-of-domain `DiegeticGyroCompassRuntime`, `SystemDispatcher`, and `ArchitectEyeVisualizer` errors. No tether compiler errors appeared in the reported set.
- Attempt 21: same core command reran and failed on out-of-domain `DiegeticGyroCompassRuntime`, `ArchitectEyeVisualizer`, and `GlobalSignals` contract errors. No tether compiler errors appeared in the reported set.
- Attempt 22: after deterministic visual-clock and shader hash cleanup, same core command failed on out-of-domain `VFX/Bioluminescence/BiolumPulseSyncRuntime.ResolveDataVault`. No tether compiler errors appeared in the reported set.
- Attempt 23: after removing remaining tether `Time.frameCount` signal stamps, same core command failed on out-of-domain `LockstepStateValidator` missing lockstep signal capacity/hash constants. No tether compiler errors appeared in the reported set.

## Omega Polish

- Polish mandate read after task closure: `Did you use math.rsqrt for the constraint solving?`
- Result: `math.rsqrt` is present in `VerletCableSolverJob` constraint normalization and velocity clamps.
- Status: `PENDING VERIFICATION`, not `VERIFIED MASTER GRADE`, because project compile is dependency-blocked and no Unity runtime/profiler pass was available.
