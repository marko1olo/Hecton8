# Status_KINEMATIC_TETHER_EXPERT

Status: PENDING - GLOBAL COMPILE DEPENDENCIES
Domain: Echelon 4 / Tether & Cable Physics
Prompt: KINEMATIC_TETHER_EXPERT
Task Count: 19

Mandates read:
- PHYS_Tether_Cable_Acceleration_Constraints.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

## Phase 1-5 Checklist

- [x] 1. SINGLETON ERADICATION: `rg` found no `TetherManager.Instance` in first-party scripts/scenes/prefabs. DOD: evidence scan. Rejected: adding shim singleton. Estimate: 0.0 us saved direct, avoided global contention.
- [x] 2. SIGNAL MIGRATION: `HeavyTowWinch` now publishes `TetherFiredSignal`; `TetherManager` consumes it and executes attach. DOD: unmanaged signal plus fixed managed sidecar. Rejected: direct singleton attach. Estimate: 1-2 us saved by no scene search.
- [x] 3. ASMDEF ISOLATION: added `Hecton8.Physics.Tethers.Contracts` and referenced it from `Hecton8.Core`. DOD: Unity Bee rsp includes contract assembly and Core reference. Rejected: placing fire signal in monolithic Core only. Estimate: 0.0 us runtime, lower dependency bleed.
- [x] 4. DEAD CODE HUNT: `rg` found no `ConfigurableJoint`, `SpringJoint`, or `HingeJoint` in first-party scripts/scenes/prefabs. DOD: text scan. Rejected: Unity joint fallback. Estimate: 10-50 us avoided per active tether under solver load.
- [x] 5. TETHER S.O.A.: existing runtime uses `NativeArray<float3>` for positions/previous/pinned and now resolves 10 default segments. DOD: source audit plus compile pass to unrelated errors. Rejected: `Transform[]` cable bones as truth. Estimate: 5-15 us saved per tether.

## Phase 6-10 Checklist

- [x] 6. THE WINCH JOB: Burst Verlet constraint now acts only when `distance > restLength`; slack cable does not compress. DOD: self-review found and fixed compression correction. Rejected: spring rod behavior. Estimate: 2-5 us saved by skipping slack corrections.
- [x] 7. TENSION FORCE: peak tension now derives from positive stretch only, then `peakDelta * springStiffness`. DOD: `T = max(0, Distance - MaxLength) * Stiffness`. Rejected: `abs(delta)` compression tension. Estimate: correctness win, no false tow drag.
- [x] 8. NEWTON'S 3RD LAW: payload force and equal opposite player reaction route through `PhysicsForceRouter.QueueForceAtPosition`. DOD: ForceMode.Force on both bodies. Rejected: direct Rigidbody mutation. Estimate: deterministic router path avoids duplicate integration.
- [x] 9. MASS RATIO SCALING: payload force uses `playerMass / (playerMass + payloadMass)` and acceleration clamp. DOD: heavy payloads no longer get full tow force. Rejected: reduced-mass shortcut that hid player reaction. Estimate: lower jitter, 1-3 us saved by simpler math.
- [x] 10. CABLE RENDERING: existing `GraphicsBuffer` upload/render path retained; solver point count feeds visual segment buffers through `GraphicsBufferUploadUtility.UploadNativeArray`. DOD: no CPU mesh rebuild and no fallback `SetData(_visualSegmentPositions)` remains. Rejected: LineRenderer/mesh allocation. Estimate: 20-80 us saved versus per-frame mesh rebuild.

## Phase 11-15 Checklist

- [x] 11. WINCH REELING: `HeavyTowWinch.TargetLength`, `SetTargetLength`, and `AdjustTargetLength` drive runtime rest length with finite guards and reset on release. DOD: tether samples owner target every fixed step. Rejected: immutable attach-time rest length and accepting NaN reel input. Estimate: 0.5-1 us overhead, user-facing control gained.
- [x] 12. SNAPPING: existing snap threshold path preserved; emits `TetherSnappedSignal` and impact feedback. DOD: source audit. Rejected: silent release on overload. Estimate: no runtime change.
- [x] 13. AUP SHIFT SAFETY: Verlet positions, previous positions, and pinned positions shift through `TetherVerletOriginShiftJob.Run`. DOD: origin-shift audit. Rejected: only shifting transforms. Estimate: avoids post-shift corrective spikes.
- [x] 14. MATH LOD: Low/MX350/Unknown uses 3 segments; Mid/High/Ultra uses 10. DOD: `ResolveVerletSegmentCount`. Rejected: always 10/16 segments. Estimate: 3-8 us saved per low-tier tether.
- [x] 15. ZERO-GC: hot solver uses existing persistent NativeArrays and direct `Run`; no `Schedule().Complete()` remains in tether solver files. DOD: `rg "Schedule\\(|\\.Complete\\("` clean for tether solver. Rejected: per-frame job handle churn. Estimate: 3-8 us saved per active tether.

## Phase 16-19 Checklist

- [x] 16. TELEMETRY: `TetherManager` writes 300-frame `NativeArray<TetherManagerTelemetryEntry>` with ActiveTethers/PeakTension and binary dump on nonfinite tension. DOD: blackbox circular buffer. Rejected: Debug.Log-only diagnostics. Estimate: 0.5-1 us fixed cost.
- [x] 17. EVENT BUS: extreme tow publishes `VehicleCommandSignalFlags.TowLoadLimit` through `VehicleCommandSignalBus` with a 3-frame cooldown and publish-return check. DOD: event bus path, no direct vehicle dependency, no fixed-step command spam. Rejected: direct vehicle component call and unbounded repeated publishes. Estimate: 1-2 us and no object lookup.
- [x] 18. CROSS-DOMAIN AUDIT: Leviathan spine/tentacle IK files do not reference tether/winch systems; bio-cable visuals are separate world cable IK. DOD: targeted `rg` audit. Rejected: adding IK hard dependency. Estimate: no runtime change.
- [x] 19. OMEGA COMPILE CHECK: [BLOCKED BY DEPENDENCY] Unity Roslyn compiled `Hecton8.Physics.Tethers.Contracts.rsp` clean after the second audit patch. `Hecton8.Core.rsp` now reaches one unrelated `GasDynamicsSolver` interface error. `dotnet build Hecton8.Core.csproj` remains red because the generated IDE project is stale/missing many active asmdefs, including the new tether contract. DOD: exact Unity Bee response compiler plus required dotnet build attempt. Rejected: editing foreign domains or generated csproj. Estimate: tether compile diagnostics are clear in Bee path.

## Loop Log

- Loop 1: Extracted XML prompt from `CURRENT_BATCH.md`, counted 19 tasks, read AGENTS/domain/mandates. Found assigned domain: Echelon 4 / Tether & Cable Physics.
- Loop 2: Audited existing tether implementation. Found no singleton and no Unity Joint use; existing Verlet/GPU/snap/AUP systems were present but missing fire signal, math LOD count, tow command signal, and manager blackbox.
- Loop 3: Implemented signal-driven attach, contract asmdef, winch target length, manager drain/blackbox, vehicle command flag, and force mass scaling.
- Loop 4: Ran Unity/Bee compilation path. MCP was unstable, so used Unity Roslyn response files directly. Contract compiled clean; Core reached unrelated dependency failures. No tether compile diagnostics remained.
- Loop 5: Re-read solver and found compression correction bug. Fixed Burst constraint to enforce only positive stretch. Re-ran Unity Roslyn Core; same unrelated dependency wall, no tether diagnostics.
- Loop 6: Re-read prompt from `CURRENT_BATCH.md`, re-audited tether code, and tightened stale fire-signal pruning, finite target-length guards, upload path policy, and tow-load command throttling. Static scans stayed clean. Unity Bee contract compile succeeded; Unity Bee Core is blocked by `GasDynamicsSolver` outside tether domain.
