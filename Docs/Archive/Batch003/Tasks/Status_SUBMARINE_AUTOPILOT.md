# Status_SUBMARINE_AUTOPILOT

Prompt: SUBMARINE_AUTOPILOT
Agent: HYDRO_MECHANIC
Domain: ECHELON 6 HABITAT & VEHICLES / Submarine Navigation Auto-Level
Task Count: 18
State: PENDING VERIFICATION

Mandates read:
- CORE_Submarine_Vehicles_Kinematics_AUP
- PHYS_Physics_Integrity_Determinism_ForceMode
- PHYS_Determinism_Multithreaded_Body_Solving
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- ARCH_Global_Registry_ServiceLocator_DI_Init
- DBG_Telemetry_Crash_Reporting_PostMortem
- LOGI_Energy_Networks_Power_Grid_Graph_Flow

## Loop 1: Tasks 1-5
- [x] Task 1 - Singleton eradication. DOD: search proved no `Submarine.Instance`; added `ISubmarineState` registry slot and `GlobalRegistry.Get<ISubmarineState>()` path. Rejected: legacy static singleton. Estimate: 3 us cold registry lookup only.
- [x] Task 2 - Signal migration. DOD: mounted input publishes `VehicleCommandSignal`. Rejected: direct `Submarine.Dive()` call; no such call exists. Estimate: 7 us per command flush.
- [x] Task 3 - ASMDEF isolation. DOD: controller consumes signal bus and has no UI/Input namespace. Rejected: direct input polling in vehicle system. Estimate: 0 us added to controller hot path.
- [x] Task 4 - Rotation Lerp purge. DOD: no submarine `Mathf.Lerp` rotation path added; auto-level torque is PID via router. Rejected: kinematic pitch correction. Estimate: 12 us PID job.
- [x] Task 5 - Ballast SOA tanks. DOD: `NativeArray<float> BallastFill01` with four tanks. Rejected: four MonoBehaviour tank objects. Estimate: 2 us tank update.

## Loop 2: Tasks 6-10
- [x] Task 6 - Mass distribution. DOD: local CoM from base plus tank weighted positions. Rejected: Unity center-of-mass guess. Estimate: 5 us.
- [x] Task 7 - Buoyancy injection. DOD: ballast mass routed into `SubmarineFluidDynamics` hydrodynamic mass path. Rejected: direct buoyancy simulation particles. Estimate: 4 us.
- [x] Task 8 - Burst PID job. DOD: `IJob` targets `float3(0,1,0)`. Rejected: `Update()` PID. Estimate: 12 us.
- [x] Task 9 - PID torque router. DOD: `PhysicsForceRouter.QueueTorque`. Rejected: controller-owned `Rigidbody.AddTorque`. Estimate: 3 us enqueue.
- [x] Task 10 - Pitch command ballast. DOD: command pitch biases front/back ballast. Rejected: direct pitch rotation. Estimate: 4 us.

## Loop 3: Tasks 11-15
- [x] Task 11 - Leviathan impact recovery. DOD: combat impact resets PID integral. Rejected: letting windup recover naturally. Estimate: 1 us event path.
- [x] Task 12 - Pump power. DOD: power grid drain gates pump delta. Rejected: free ballast changes. Estimate: 3 us.
- [x] Task 13 - Audio venting. DOD: ballast blow emits `AirRelease` procedural audio ping. Rejected: silent ballast. Estimate: 2 us event path.
- [x] Task 14 - HUD telemetry. DOD: expose `BallastFill01.AsReadOnly()`. Rejected: copying arrays to UI. Estimate: 0 us copy.
- [x] Task 15 - AUP shift safety. DOD: origin shift resets derivative path. Rejected: derivative spike after floating origin. Estimate: 1 us event path.

## Loop 4: Tasks 16-18
- [x] Task 16 - Math LOD. DOD: Low/MX350 uses master ballast scalar. Rejected: same four-tank solve on toaster tier. Estimate: saves 4 us.
- [BLOCKED BY DEPENDENCY] Task 17 - Omega compile. DOD: full build attempted; blocked by pre-existing Bootstrap/Cartography/VRAM/Narrative compile errors outside this domain. Rejected: fake green build. Estimate: N/A.
- [x] Task 18 - Telemetry. DOD: 300-frame blackbox writes `PID_IntegralWindup`; dump on NaN. Rejected: no postmortem trail. Estimate: 3 us.

## Loop 5: Strict Iteration
- [x] Re-read original XML prompt after implementation.
- [x] Re-read changed code for dependency leaks and GC allocations.
- [x] Compile pass 1.
- [BLOCKED BY DEPENDENCY] Fix compile failures if any: no new SUBMARINE_AUTOPILOT errors surfaced after adding new files to `Hecton8.Core.csproj`; build blocked by unrelated contracts.
- [x] Mark final status `PENDING VERIFICATION`.

## Compile Evidence
- `dotnet build Hecton8.slnx`: timed out at 124 seconds.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: blocked by `Hecton8.Bootstrap.Contracts.csproj` errors in `BootstrapStatus.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false`: blocked by existing `Hecton8.Cartography`, `VRAMMonitor`, and `HectonNarrativeDirector` interface errors; no `SUBMARINE_AUTOPILOT` file errors reported after csproj include was added.
- Omega rerun `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false /clp:ErrorsOnly`: blocked by `HectonNarrativeDirector.cs` brace errors at lines 1212 and 1230, outside assigned domain.

## Omega Polish Evidence
- Static scan over `SubmarineAutoLevelBallastController.cs` and `VehicleCommandSignals.cs`: no `foreach`, `string.Format`, `.ToString(`, `math.sqrt`, `math.normalize`, `math.normalizesafe`, `math.length(`, string interpolation, direct `AddTorque`, `Mathf.Lerp`, `Submarine.Instance`, `Submarine.Dive`, `Submarine.Ascend`, `Hecton8.Input`, or `Hecton8.UI`.
- PID integral clamp verified: `math.clamp(integral, -IntegralClamp, IntegralClamp)` remains in the Burst job.
- Status remains `PENDING VERIFICATION` because global compile dependencies are red.

## Continuation Recheck
- [x] Removed mounted tick-path auto-level component lookup; discovery now occurs during cold drive-reference resolution. DOD: `PublishVehicleCommandSignal` no longer calls `TryGetComponent`. Rejected: retrying lookup every rider tick. Estimate: saves 1-3 us during mounted piloting.
- [x] Cached pump power dependency with registry hot-swap listener. DOD: pump work uses `_powerGrid`; registry access remains enable-time/hot-swap only. Rejected: per-pump `GlobalRegistry.PowerGrid` poll. Estimate: saves under 1 us per pump request.
- [x] Added math LOD hysteresis. DOD: Low/MX350 mode changes require `mathLodSwitchHoldSeconds` before flipping. Rejected: immediate LOD flapping. Estimate: prevents visible ballast response jitter; no measurable extra cost.
- [x] Rechecked center-of-mass ownership. DOD: `SubmarineFluidDynamics` runs in `PriorityLayer.Environment`; auto-level runs in `PriorityLayer.Player`, so ballast CoM is applied after hydrodynamic flood CoM for the final fixed-step rigidbody value. Rejected: cross-domain rewrite of flood CoM solver.
- [x] Hardened vehicle command target ids. DOD: publisher caches a nonzero command target id during reference refresh; bus and listener reject zero-id commands and listener compares cached ids only. Rejected: tick-path id fallback and zero-id broadcast commands. Estimate: saves under 1 us and prevents multi-sub command bleed.
- [x] Cleared ballast mass coupling on teardown. DOD: controller disable/unregister pushes zero ballast mass into `SubmarineFluidDynamics` and clears cached power service. Rejected: leaving stale hydrodynamic cargo mass after component disable. Estimate: no hot-path cost.
- [x] Guarded singleton read-model slot. DOD: secondary auto-level controllers skip `GlobalRegistry.SubmarineState` publication instead of hijacking the active read model, then claim it through hot-swap if the active owner unregisters; command bus operation remains per-target. Rejected: throwing on second enabled submarine controller. Estimate: cold lifecycle only.
- [x] Hardened cold auto-level resolver. DOD: mounted transport only marks the auto-level controller bridge resolved after it finds or installs the controller, allowing later cold retries when `SubmarineCoreDirector` appears after transport initialization. Rejected: permanently caching a missing optional controller. Estimate: cold lifecycle only; protects dynamic prefab composition.
- [x] Expanded tuning validation. DOD: PID gains, combat thresholds, audio vent threshold, and LOD hold time are clamped in `OnValidate`. Rejected: relying only on inspector attributes. Estimate: editor/cold path only.
- [x] Removed fixed-step scalability registry polling. DOD: auto-level math LOD uses `_desiredLowMathLod`, seeded cold and refreshed from `ScalabilityEvents`; `AdvanceMathLod` no longer reads `GlobalRegistry.*`. Rejected: per-fixed `GlobalRegistry.ScalabilityTier` / `MathPrecision` reads. Estimate: saves under 1 us and aligns with snapshot policy.
- [x] Closed watchdog math-precision fallback gap. DOD: controller now implements `ISlowTickable` and refreshes cached scalability/math precision on an explicitly budgeted slow cadence, covering `FrameTimeWatchdog` precision degradation paths that do not emit `ScalabilityEvents`. Rejected: restoring per-fixed registry polling or adding a broad core event bus without integration scope. Estimate: under 1 us every slow tick.
- [x] Replaced air-release wall-clock throttle. DOD: vent audio cooldown is fixed-step countdown driven by passed `fixedDeltaTime`; telemetry frame uses `_tickCount`. Rejected: `Time.time`/`Time.frameCount` in the controller fixed-step chain. Estimate: deterministic state, no measurable CPU gain.
- [x] Replaced mounted impact wall-clock throttle. DOD: `MountablePlayerTransport` sweep-impact feedback cooldown now advances from `fixedDeltaTime`; no `Time.time` remains in the touched vehicle command path. Rejected: wall-clock throttle in physics cadence. Estimate: deterministic cooldown, no measurable CPU gain.
- [x] Hardened command bus dispatch exit. DOD: `_isDispatching` is cleared in `finally` before next-frame promotion. Rejected: leaving bus in dispatch mode after listener fault. Estimate: no normal-path allocation.
- [x] Re-extracted `SUBMARINE_AUTOPILOT` from `Docs/Tasks/CURRENT_BATCH.md` with attribute-aware XML tag parsing after continuation edits.
- [x] Honored latest instruction: no new `dotnet build` was launched in this continuation pass.
