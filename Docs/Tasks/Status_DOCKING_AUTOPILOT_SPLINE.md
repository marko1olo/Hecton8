# Status_DOCKING_AUTOPILOT_SPLINE

Agent: HYDRO_MECHANIC
Prompt ID: DOCKING_AUTOPILOT_SPLINE
Domain: PHYSICS/VEHICLES
Authoritative source: `Docs/Tasks/CURRENT_BATCH.md`
Task Count: 18
Current loop: Phase 5 - Multiplatform/H-Phi Audit
Runtime status: PENDING VERIFICATION

## Batch Extraction

- [x] Read `AGENTS.md` | Justification: authority spine and zero-GC/tick/registry constraints are project-local | Alternative rejected: chat-only override | Estimate: 0 us runtime.
- [x] Extract `<AGENT_PROMPT id="DOCKING_AUTOPILOT_SPLINE">` cover-to-cover via CLI | Justification: batch protocol requires exact XML extraction | Alternative rejected: using stale missing-prompt audit | Estimate: 0 us runtime.
- [x] Identify relevant mandates | Justification: docking crosses AUP, physics determinism, GlobalRegistry, GlobalDataVault, zero-GC, execution phases, telemetry, and currents | Alternative rejected: editing docking movement in isolation | Estimate: 0 us runtime.

## Phase 1 Checklist

- [x] Task 1 `[PURGE_LERPS]` | Justification: `VehicleDockingModule` no longer uses `ResolveRuntimeAupLerp` or local quaternion nlerp; docking evaluates cubic spline position and tangent-facing rotation | Alternative rejected: Unity `Vector3.Lerp`, `Quaternion.Slerp`, `AnimationCurve`, direct linear AUP interpolation | Estimate: saves ~2-4 us per active dock on i3/MX350 by removing per-frame quaternion blend and repeated linear target solve; 0 B/frame.
- [x] Task 2 `[SINGLETON_KILL]` | Justification: static scan found no `DockingManager.Instance`; added `IDockingAutopilotService` slot, property, register/unregister, heartbeat resolution, and generic `TryGet` mapping in `GlobalRegistry` | Alternative rejected: classic `Instance`, service lookup inside `FixedTick`, or direct hard dependency on a scene singleton | Estimate: 0 us hot-path registry cost; service is cached outside docking tick.
- [x] Task 3 `[DATA_EVICTION]` | Justification: added `ActiveSplineData` with double3 P0-P3 and stored slots in `GlobalDataVault` via `BufferID.VehicleDockingActiveSplines` owned by `SystemID.VehiclesPhysics` | Alternative rejected: private managed arrays, per-dock local NativeArray ownership, and non-vault authority | Estimate: 0 B/frame; fixed vault slot write is sub-1 us per active dock.

## Later Tasks

- [x] Task 4 `[BURST_BEZIER_SOLVER]` | Justification: added Burst-compiled unsafe `CubicBezierJob` over vault-compatible pointer lanes with explicit lengths, so docking automation declares no `NativeArray<T>` storage lanes | Alternative rejected: main-thread class solver only, `AnimationCurve`, managed arrays, local `NativeArray<T>` declarations, same-frame schedule/complete policy | Estimate: batchable solver cost target <0.1 ms for 64 active splines; 0 B/frame.
- [x] Task 5 `[TANGENT_MATH]` | Justification: derivative `B'(t)` is evaluated beside position and normalized with fail-closed target-forward fallback for LookRotation consumers | Alternative rejected: nlerping from start rotation to anchor rotation | Estimate: tangent adds ~0.5-1 us per 64-spline batch on i3/MX350; 0 B/frame.
- [x] Task 6 `[AUP_INTEGRITY]` | Justification: `ActiveSplineData.P0/P1/P2/P3` are `double3`, and runtime conversion goes through `AbsoluteUniversePosition.FromAbsolutePosition` | Alternative rejected: float3 control points for submarine-scale authority | Estimate: prevents high-coordinate spline warp; arithmetic cost accepted because active docking count is low.
- [x] Task 7 `[CURRENT_COMPENSATION]` | Justification: `VehicleDockingModule` samples cached `HectonFluidEngine.TrySampleModAbyssalFlow` at the evaluated spline point and subtracts that flow from the path velocity command | Alternative rejected: global current force on every entity or registry polling in `FixedTick` | Estimate: one gameplay-critical flow sample per active dock; 0 B/frame; expected <3 us on i3/MX350.
- [x] Task 8 `[LOW_TIER_FAKE]` | Justification: Math LOD 0 keeps the spline solve at 10 Hz and uses manual position interpolation between cached samples; no Unity `Lerp` API is called | Alternative rejected: instant low-tier snap and full-rate cubic solve on MX350 | Estimate: reduces low-tier cubic evaluations from 50 Hz to 10 Hz, ~80% solve cadence reduction for active dock.
- [x] Task 9 `[HIGH_END_OVERKILL]` | Justification: Math LOD 2 uses a seventh-order Hermite progress curve with endpoint velocity/acceleration/jerk flattened before Bezier evaluation | Alternative rejected: `AnimationCurve` or author-time curve assets | Estimate: adds ~7 scalar multiplies per active dock only on High/Ultra.
- [x] Task 10 `[REACTIVE_VFX]` | Justification: docking publishes existing `WakeGeneratedSignal` and `FluidImpulseSignal` into the zero-GC signal lanes at 10 Hz for wake/fluid advection consumers | Alternative rejected: adding an orphan `VehicleWakeSignal` with no consumer or spawning particles directly | Estimate: bounded 10 Hz signal push; fluid/VFX owners decide visual cost.
- [x] Task 11 `[STP_STABILIZATION]` | Justification: docking now keeps kinematic Rigidbody interpolation enabled during capture and writes compensated linear velocity instead of zeroing it every fixed tick | Alternative rejected: postprocess-specific hacks in the docking handler | Estimate: no extra allocations; motion vector stability uses existing Rigidbody/renderer history.
- [x] Task 12 `[NAN_VACCINATION]` | Justification: tangent normalization already fails closed to target forward, and new deviation/flow/velocity paths reject non-finite vectors before motion or signal publish | Alternative rejected: trusting raw spline/flow outputs | Estimate: finite checks are sub-1 us per active dock.
- [x] Task 13 `[BLACKBOX_LOGGING]` | Justification: the 300-frame telemetry ring now stores `SplineDeviationError`, spline target, flow velocity, command velocity, owner hash, request id, and runtime flags in the `GlobalDataVault` buffer `VehicleDockingTelemetryRing`; dumps to `Docs/AgentLogs/Dump_DOCKING_AUTOPILOT_SPLINE.bin` on invalid pose/deviation | Alternative rejected: chat-only failure notes, private `NativeArray` ownership, or the older generic vehicle dump name | Estimate: fixed vault ring write only; 0 B/frame.
- [x] Task 14 `[TRIPLE_STRIKE_REPAIR]` | Justification: static scan found no `VehicleCommandSignal` call-site in docking autopilot; existing command bus users are outside this prompt, so no signature repair is required here | Alternative rejected: editing `MountablePlayerTransport`, tether, or ballast command paths without an autopilot call-site | Estimate: 0 us runtime.
- [x] Task 15 `[HOMEOSTASIS_ADAPTATION]` | Justification: `ResolveDockingProgress01` disables high-end Hermite smoothing when `SignalBusRegistry.SystemStress01 > 0.8` and falls back to basic inertial Bezier progress | Alternative rejected: keeping overkill math under frame pressure | Estimate: saves the high-tier smoothing overhead under stress; cheap devices stay on LOD 0.
- [x] Task 16 `[AUTOMATIC_HANDOFF]` | Justification: when progress crosses 0.95, docking emits `DockingCompleteSignal` with AUP dock position, forward vector, owner/request ids, and flags | Alternative rejected: hard-coupling moonpool/WFC animation code into the docking module | Estimate: one signal per docking sequence.
- [x] Task 17 `[ABORT_LOGIC]` | Justification: if actual transport pose deviates more than 5 m from the active spline target, docking dumps blackbox telemetry, publishes `DockingFailedSignal`, releases the control lock, and returns the body to its cached state | Alternative rejected: clamping the vehicle back silently after a large external knock | Estimate: one squared-distance/deviation check per active dock.
- [BLOCKED BY DEPENDENCY] Task 18 `[FINAL_VALIDATION]` | Justification: focused `dotnet build Hecton8.Core.csproj --no-restore` still exits 1 in unrelated files, while output shows no `VehicleDockingModule`, `DockingAutopilotService`, `H8Memory`, or docking signal errors | Alternative rejected: reverting other agents' `ArchitectEyeVisualizer` or `EcosystemPopulationBalancer` edits | Estimate: 0 us runtime.

## Compile Status

- [BLOCKED BY DEPENDENCY] Compile verification | Justification: latest full `dotnet build Hecton8.Core.csproj --no-restore` exits 1 with five unrelated current-worktree errors: `ArchitectEyeVisualizer.cs` missing `VaultProbeUtility.IsFinite`, `EcosystemPopulationBalancer.cs` missing `SignalBusRegistry`, missing `EntityDeathSignal`, and invalid ref return usage; output contains no docking/H8Memory errors | Alternative rejected: claiming green from static scan or reverting other agents' files | Estimate: 0 us runtime.

## Multiplatform/H-Phi Audit

- [x] ARM64/Quest layout check | Justification: `ActiveSplineData`, `DockingSplineSample`, and `DockTelemetryEntry` use `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = ...)]`; `DockTelemetryEntry` was changed from `Pack = 16` to `Pack = 1, Size = 128` | Alternative rejected: implicit CLR padding or Quest-hostile 16-byte packing | Estimate: 0 us runtime; deterministic binary layout.
- [x] GlobalDataVault eviction check | Justification: docking persistent data now uses `VaultBufferHandle<ActiveSplineData>`, `VaultBufferHandle<DockTelemetryEntry>`, and `VaultBufferHandle<int>` for `VehicleDockingActiveSplines`, `VehicleDockingTelemetryRing`, and `VehicleDockingTelemetryCursor`; static scan found no `NativeArray<T>`/`NativeList<T>` declarations or local persistent native ownership in the audited docking files | Alternative rejected: system-owned persistent native buffers, job-local `NativeArray<T>` lanes, and module-local blackbox ring cursors | Estimate: 0 B/frame; pointer resolve only.
- [x] Signal lane check | Justification: no duplicate `VehicleWakeSignal` was created; docking uses existing typed `SignalBus<WakeGeneratedSignal>`, `SignalBus<FluidImpulseSignal>`, `SignalBus<DockingCompleteSignal>`, and `SignalBus<DockingFailedSignal>` lanes | Alternative rejected: legacy `EventBus`, managed delegates, or orphan signal contracts | Estimate: bounded 10 Hz VFX signal cadence.
- [x] Metal/Steam Deck domain check | Justification: no `.compute`, `.shader`, `.hlsl`, or `.cginc` file exists in the docking automation domain, so there is no docking-owned DirectX-only shader path or 1024-thread-group risk to repair; docking blackbox uses fixed vault memory and writes the dump only on abort/NaN | Alternative rejected: adding cross-domain graphics work from the physics prompt | Estimate: no per-frame I/O and no shader dispatch cost.

## Working Evidence

- New docking automation authority lives at `Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs` with namespace compatibility for existing `Hecton8.Vehicles.Automation` signal code.
- Static scan found no `DockingManager.Instance`; singleton task completed by adding the `GlobalRegistry` service slot and registration API.
- Legacy interpolation target `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs` now uses spline evaluation; no `ResolveRuntimeAupLerp` or local `FastNlerp` remains.
- `CubicBezierJob` now evaluates P0-P3 and tangent in Burst-compatible value math; no schedule/complete path was added.
- Static scan confirms no `Vector3.Lerp`, `Quaternion.Slerp`, `AnimationCurve`, or `math.pow` in the touched docking files.
- Current compensation, low-tier 10 Hz sampling, high-tier zero-jerk progress, wake/fluid signals, completion/failure signals, and deviation aborts are implemented behind existing registry/signal boundaries.
- Latest static debt scan returned no matches for `NativeArray<T>`, `NativeList<T>`, local `new NativeArray`, `Allocator.Persistent`, `Pack = 16`, `EventBus`, managed delegates, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, Unity Lerp/Slerp, `AnimationCurve`, or `math.pow` in the audited docking files.
- `VehicleDockingTelemetryCursor` was added to `GlobalDataVault`, removing the last module-local blackbox cursor from the docking telemetry path.
- `CubicBezierJob` now uses unsafe pointer lanes with explicit lengths instead of `NativeArray<T>` fields.
- Active spline writes now reject owner-hash mismatches, idle telemetry returns before vault pointer resolution, and service shutdown clears only an existing spline buffer instead of allocating one during teardown.
- Latest full build remains dependency-blocked outside docking; no docking/H8Memory errors were returned by the focused filter.
