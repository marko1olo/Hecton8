# Status_DOCKING_AUTOPILOT_SPLINE

Agent: HYDRO_MECHANIC
Prompt ID: DOCKING_AUTOPILOT_SPLINE
Domain: PHYSICS/VEHICLES
Authoritative source: `Docs/Tasks/CURRENT_BATCH.md`
Task Count: 18
Current loop: Phase 8 - Final Validation Verified
Runtime status: VERIFIED MASTER GRADE

## Batch Extraction

- [x] Read `AGENTS.md` | Justification: authority spine and zero-GC/tick/registry constraints are project-local | Alternative rejected: chat-only override | Estimate: 0 us runtime.
- [x] Extract `<AGENT_PROMPT id="DOCKING_AUTOPILOT_SPLINE">` cover-to-cover via CLI | Justification: batch protocol requires exact XML extraction | Alternative rejected: using stale missing-prompt audit | Estimate: 0 us runtime.
- [x] Identify relevant mandates | Justification: docking crosses AUP, physics determinism, GlobalRegistry, GlobalDataVault, zero-GC, execution phases, telemetry, and currents | Alternative rejected: editing docking movement in isolation | Estimate: 0 us runtime.

## Phase 1 Checklist

- [x] Task 1 `[PURGE_LERPS]` | Justification: `VehicleDockingModule` no longer uses `ResolveRuntimeAupLerp` or local quaternion nlerp; `DroneCognitionJob` docking no longer uses `math.lerp`; docking evaluates cubic spline position and tangent-facing rotation | Alternative rejected: Unity `Vector3.Lerp`, `Quaternion.Slerp`, `AnimationCurve`, direct linear AUP interpolation, and docking speed blends through `math.lerp` | Estimate: saves ~2-4 us per active dock on i3/MX350 by removing per-frame quaternion blend and repeated linear target solve; 0 B/frame.
- [x] Task 2 `[SINGLETON_KILL]` | Justification: static scan found no `DockingManager.Instance`; added `IDockingAutopilotService` slot, property, register/unregister, heartbeat resolution, and generic `TryGet` mapping in `GlobalRegistry` | Alternative rejected: classic `Instance`, service lookup inside `FixedTick`, or direct hard dependency on a scene singleton | Estimate: 0 us hot-path registry cost; service is cached outside docking tick.
- [x] Task 3 `[DATA_EVICTION]` | Justification: added `ActiveSplineData` with double3 P0-P3 and stored slots in `GlobalDataVault` via `BufferID.VehicleDockingActiveSplines` owned by `SystemID.VehiclesPhysics` | Alternative rejected: private managed arrays, per-dock local NativeArray ownership, and non-vault authority | Estimate: 0 B/frame; fixed vault slot write is sub-1 us per active dock.

## Later Tasks

- [x] Task 4 `[BURST_BEZIER_SOLVER]` | Justification: added Burst-compiled unsafe `CubicBezierJob` over vault-compatible pointer lanes with explicit lengths, so docking automation declares no `NativeArray<T>` storage lanes | Alternative rejected: main-thread class solver only, `AnimationCurve`, managed arrays, local `NativeArray<T>` declarations, same-frame schedule/complete policy | Estimate: batchable solver cost target <0.1 ms for 64 active splines; 0 B/frame.
- [x] Task 5 `[TANGENT_MATH]` | Justification: derivative `B'(t)` is evaluated beside position and normalized with fail-closed target-forward fallback for LookRotation consumers | Alternative rejected: nlerping from start rotation to anchor rotation | Estimate: tangent adds ~0.5-1 us per 64-spline batch on i3/MX350; 0 B/frame.
- [x] Task 6 `[AUP_INTEGRITY]` | Justification: `ActiveSplineData.P0/P1/P2/P3` are explicit-layout `double3`, runtime conversion goes through `AbsoluteUniversePosition.FromAbsolutePosition`, and headless drone docking control points were promoted from `float3` to `double3` | Alternative rejected: float3 control points for submarine-scale authority or drone return corridors | Estimate: prevents high-coordinate spline warp; arithmetic cost accepted because active docking count is low.
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
- [x] Task 18 `[FINAL_VALIDATION]` | Justification: isolated focused `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 0 with 0 warnings and 0 errors; latest log is `Docs/AgentLogs/Build_DOCKING_AUTOPILOT_SPLINE_latest.txt` | Alternative rejected: claiming green from stale logs, killing other agents' build processes, or broad-changing world simulation beyond compile-surface triage | Estimate: 0 us runtime.

## Compile Status

- [x] Compile verification | Justification: latest isolated focused build writes `Hecton8.Core.dll` to `Temp/bin_docking/Debug`, exits 0, and reports `0 Warning(s)` / `0 Error(s)` in `Docs/AgentLogs/Build_DOCKING_AUTOPILOT_SPLINE_latest.txt` | Alternative rejected: stale build evidence or unisolated output while other agents are active | Estimate: 0 us runtime.

## Multiplatform/H-Phi Audit

- [x] ARM64/Quest layout check | Justification: `ActiveSplineData` and `DockingSplineSample` use explicit `Pack = 1` field offsets, `DockTelemetryEntry` uses `Pack = 1, Size = 128`, and `DockingRequestSignal`/`DockingCompleteSignal`/`DockingFailedSignal` now use explicit `Pack = 1, Size = 80` offsets with zeroed tail padding | Alternative rejected: implicit CLR padding, sequential signal tail assumptions, or Quest-hostile 16-byte packing | Estimate: 0 us runtime; deterministic binary layout.
- [x] GlobalDataVault eviction check | Justification: docking persistent data now uses `VaultBufferHandle<ActiveSplineData>`, `VaultBufferHandle<DockTelemetryEntry>`, and `VaultBufferHandle<int>` for `VehicleDockingActiveSplines`, `VehicleDockingTelemetryRing`, and `VehicleDockingTelemetryCursor`; static scan found no `NativeArray<T>`/`NativeList<T>` declarations or local persistent native ownership in the audited docking files | Alternative rejected: system-owned persistent native buffers, job-local `NativeArray<T>` lanes, and module-local blackbox ring cursors | Estimate: 0 B/frame; pointer resolve only.
- [x] Signal lane check | Justification: no duplicate `VehicleWakeSignal` was created; docking uses existing typed `SignalBus<WakeGeneratedSignal>`, `SignalBus<FluidImpulseSignal>`, `SignalBus<DockingCompleteSignal>`, and `SignalBus<DockingFailedSignal>` lanes | Alternative rejected: legacy `EventBus`, managed delegates, or orphan signal contracts | Estimate: bounded 10 Hz VFX signal cadence.
- [x] Metal/Steam Deck domain check | Justification: no `.compute`, `.shader`, `.hlsl`, or `.cginc` file exists in the docking automation domain; cross-domain `DroneCulling.compute` now consumes a compact float/uint culling payload with `numthreads(64,1,1)` and no double fields, while docking blackbox uses fixed vault memory and writes the dump only on abort/NaN | Alternative rejected: uploading double-control `HeadlessDroneState` directly into HLSL or adding cross-domain raymarch/POM/SSS from the physics prompt | Estimate: no per-frame I/O; GPU culling payload shrinks versus full drone state upload.

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
- Active spline writes now reject owner-hash mismatches, telemetry records the full idle/docking/docked heartbeat, and service shutdown clears only an existing spline buffer instead of allocating one during teardown.
- Headless drone docking now stores P0-P3 as `double3`, uses `Pack = 1` state/command/task layouts, and replaces remaining drone cognition `math.lerp` calls with explicit fused linear blends.
- `DroneCulling.compute` now reads `DroneCullingState` instead of full `HeadlessDroneState`, keeping double docking control points off Metal/Vulkan shader buffers.
- `RecordDockTelemetry` now records idle/docking/docked heartbeat samples into the 300-frame vault ring instead of skipping idle frames.
- Omega polish: `TryResolveExistingActiveSplines` now uses no-create generation/handle checks and cannot create the spline vault buffer during shutdown.
- Omega polish: `SanitizeDockingSettings` now preserves the serialized docking duration within `[0.05, 8]` instead of hard-resetting every dock to `DefaultDockingDurationSeconds`.
- Omega polish: docking request/complete/failure signal contracts now use explicit 80-byte field maps and publishers zero `ReservedTail`, so ARM64/Quest layout does not rely on sequential padding behavior.
- Cross-domain compile triage: `LockstepStateValidator` now declares the missing lockstep/glitch signal capacities and lane hashes to match `GlobalSignals`; latest build no longer reports `LockstepStateValidator` errors.
- Latest isolated focused build succeeds with 0 warnings and 0 errors.
- Final static scans: docking core has no Unity Lerp/Slerp/MoveTowards/AnimationCurve/math.pow/local native-storage/EventBus/delegate/string.Format/update-loop matches; drone docking scan has no interpolation helper matches; layout scan has no `Pack = 16` or unpacked sequential matches in the audited docking files.
