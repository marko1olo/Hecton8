# Status_DOCKING_AUTOPILOT_SPLINE

Agent: HYDRO_MECHANIC
Prompt ID: DOCKING_AUTOPILOT_SPLINE
Domain: PHYSICS/VEHICLES
Authoritative source: `Docs/Tasks/CURRENT_BATCH.md`
Task Count: 18
Current loop: Phase 2 - Kernel
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

- [x] Task 4 `[BURST_BEZIER_SOLVER]` | Justification: added Burst-compiled `CubicBezierJob` over vault-compatible `NativeArray<ActiveSplineData>` and `NativeArray<DockingSplineSample>` lanes | Alternative rejected: main-thread class solver only, `AnimationCurve`, managed arrays, same-frame schedule/complete policy | Estimate: batchable solver cost target <0.1 ms for 64 active splines; 0 B/frame.
- [x] Task 5 `[TANGENT_MATH]` | Justification: derivative `B'(t)` is evaluated beside position and normalized with fail-closed target-forward fallback for LookRotation consumers | Alternative rejected: nlerping from start rotation to anchor rotation | Estimate: tangent adds ~0.5-1 us per 64-spline batch on i3/MX350; 0 B/frame.
- [x] Task 6 `[AUP_INTEGRITY]` | Justification: `ActiveSplineData.P0/P1/P2/P3` are `double3`, and runtime conversion goes through `AbsoluteUniversePosition.FromAbsolutePosition` | Alternative rejected: float3 control points for submarine-scale authority | Estimate: prevents high-coordinate spline warp; arithmetic cost accepted because active docking count is low.
- [ ] Task 7 `[CURRENT_COMPENSATION]`
- [ ] Task 8 `[LOW_TIER_FAKE]`
- [ ] Task 9 `[HIGH_END_OVERKILL]`
- [ ] Task 10 `[REACTIVE_VFX]`
- [ ] Task 11 `[STP_STABILIZATION]`
- [ ] Task 12 `[NAN_VACCINATION]`
- [ ] Task 13 `[BLACKBOX_LOGGING]`
- [ ] Task 14 `[TRIPLE_STRIKE_REPAIR]`
- [ ] Task 15 `[HOMEOSTASIS_ADAPTATION]`
- [ ] Task 16 `[AUTOMATIC_HANDOFF]`
- [ ] Task 17 `[ABORT_LOGIC]`
- [ ] Task 18 `[FINAL_VALIDATION]`

## Compile Status

- [BLOCKED BY DEPENDENCY] Compile verification | Justification: latest `dotnet build Hecton8.Core.csproj --no-restore` reached unrelated current-worktree errors after docking symbols resolved: missing `Hecton8.AI.Sensory`, missing `TetherFiredSignal`, and missing `AcousticEchoHuntResult` | Alternative rejected: claiming green from static scan or reverting other agents' files | Estimate: 0 us runtime.

## Working Evidence

- New docking automation authority lives at `Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs` with namespace compatibility for existing `Hecton8.Vehicles.Automation` signal code.
- Static scan found no `DockingManager.Instance`; singleton task completed by adding the `GlobalRegistry` service slot and registration API.
- Legacy interpolation target `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs` now uses spline evaluation; no `ResolveRuntimeAupLerp` or local `FastNlerp` remains.
- `CubicBezierJob` now evaluates P0-P3 and tangent in Burst-compatible value math; no schedule/complete path was added.
