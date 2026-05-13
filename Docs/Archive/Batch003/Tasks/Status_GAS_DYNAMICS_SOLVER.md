# GAS_DYNAMICS_SOLVER Status

Prompt source: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="GAS_DYNAMICS_SOLVER">`
Agent role: HABITAT_ARCHITECT
Domain: Gas Dynamics (Dalton's Law)
Task count: 19
Status: PENDING VERIFICATION

Mandates loaded before coding:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `PHYS_Fluid_Incursion_Interior.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Checklist

- [x] 1. SINGLETON ERADICATION | DOD: removed `HectonAtmosphereManager.Instance` facade and added `IGasDynamicsSolver` registry slot. | Rejected: reusing visual `GlobalRegistry.Atmosphere` or adding another static singleton. | Estimate: 4-8us saved per gas query by direct slot lookup; larger win is dependency containment.
- [x] 2. SIGNAL MIGRATION | DOD: no `Player.Instance.Kill()` reference; solver emits unmanaged `ToxicitySignal` packets from a `NativeQueue`. | Rejected: direct Physiology/Player calls from gas code. | Estimate: 15-40us saved per toxic tick versus managed delegate/string path.
- [x] 3. ASMDEF ISOLATION | DOD: new solver depends on Core contracts, World dispatcher primitives, Burst/Collections only; no direct Logistics/Physiology concrete refs. | Rejected: concrete `BaseModule`, `Player`, or `SurvivalPhysiologyScalarJob` field dependencies. | Estimate: 0us hot-path direct, prevents compile-order drag.
- [x] 4. DEAD CODE HUNT | DOD: scanned `SubmarineAtmosphereSystem.cs`; removed stale "O2 is a 0..100 tank, not chemistry" claim and left partial-pressure conversion locals intact. | Rejected: destructive deletion of current Dalton conversion math. | Estimate: avoids one architecture regression, no hot-path cost.
- [x] 5. GAS S.O.A. LAYOUT | DOD: `GasDynamicsSolver` owns `NativeArray<float> RoomO2`, `RoomCO2`, and `RoomPressure` plus nitrogen/back buffers. | Rejected: AoS room objects, dictionaries, managed per-room classes. | Estimate: 80-180us saved per 64-room solve on i3/MX350 versus managed room objects.
- [x] 6. PARTIAL PRESSURE MATH | DOD: `ResolveDaltonPressureKPa` returns O2+CO2+N2 after finite clamps. | Rejected: exponential/curve pressure model. | Estimate: 20-50us saved per 64 rooms versus libm curves.
- [x] 7. DIFFUSION KERNEL | DOD: Burst job diffuses each gas independently across bulkhead edges only when `BulkheadSealed == 0`, conserving pair totals. | Rejected: particle gas, NavMesh flood, or full CFD. | Estimate: 300-900us saved per 128 edges on i3/MX350.
- [x] 8. CONSUMPTION JOB | DOD: occupied room consumes O2 and emits CO2 based on `PlayerStress01` and heart rate/BPM. | Rejected: global player oxygen drain detached from room id. | Estimate: 15-30us saved by folding metabolism into gas pass.
- [x] 9. SCRUBBER EFFICIENCY | DOD: `RoomCO2` is reduced only when `TrySetScrubberPowered(room, true)` sets the logistics power bit. | Rejected: direct `PowerGrid`/`ConstructionManager` reads from Burst gas code. | Estimate: 5-20us saved and no concrete dependency.
- [x] 10. GAS TOXICITY COUPLING | DOD: CO2 threshold produces unmanaged `ToxicitySignal` packets for Physiology consumption. | Rejected: kill calls, exceptions, or managed UI warnings from gas. | Estimate: 15-40us saved on toxic ticks.
- [x] 11. NITROGEN NARCOSIS LINK | DOD: pressure over 4 atm generates `narcosis01` in `ToxicitySignal` and room snapshot/UI. | Rejected: nonlinear narcosis curves and direct physiology mutation. | Estimate: 10-25us saved versus separate physiology polling.
- [x] 12. FIRE OXYGEN DRAIN | DOD: `InternalFire` drains `RoomO2` at 5x fire rate and adds the same amount to `RoomCO2`. | Rejected: particle/fire chemistry simulation. | Estimate: 60-150us saved per burning room versus VFX-driven gas.
- [x] 13. BREACH DECOMPRESSION | DOD: `Breached` rooms force O2=0, CO2=0, N2=`AmbientPressure`, so `P_total=AmbientPressure`; default ambient seeds to standard room pressure and can be updated via `TrySetAmbientPressure`. | Rejected: iterative decompression solver or direct Habitat Integrity dependency. | Estimate: 80-250us saved per breach event.
- [x] 14. VISOR HUD SYNC | DOD: active room writes O2 fraction, O2 kPa, CO2 kPa, pressure kPa, and narcosis to `UIStateStore`. | Rejected: formatted strings and direct HUD refs. | Estimate: 20-60us saved per HUD tick.
- [x] 15. HULL STRESS COUPLING | DOD: `ResolveEffectiveDepthStress01` reduces depth stress by high internal pressure via scalar relief. | Rejected: real hull finite-element stress model. | Estimate: 500us+ avoided versus structural simulation.
- [x] 16. AUP SHIFT SAFETY | DOD: solver accepts only local integer room ids and bulkhead indices; no world/AUP coordinates. | Rejected: transform/world-position coupling. | Estimate: avoids all origin-shift bookkeeping.
- [x] 17. MATH LOD | DOD: Low/MX350 tier uses 2.0s ColdTick; Mid uses 0.5s; High/Ultra use 0.1s. | Rejected: fixed 10Hz on toaster hardware. | Estimate: 70-95% gas CPU reduction on Low tier.
- [x] 18. ZERO-GC | DOD: hot gas math is Burst/NativeArray/NativeQueue; targeted scan found only black-box file/log error path, guarded for dev log. | Rejected: string formatting, managed events, LINQ, per-room managed objects. | Estimate: 0B GC per gas tick target.
- [x] 19. OMEGA COMPILE CHECK [BLOCKED BY DEPENDENCY] | DOD: `math.exp` scan found no runtime usage under `Assets/_Project/Scripts` excluding Editor; four compile attempts made. | Rejected: claiming clean compile while external `Memory/Determinism/Cartography/IDataVault` dependencies are missing. | Estimate: linear gas curves save 20-50us per solve.

## Iteration Log

- Pass 0: Prompt extracted from active batch with PowerShell raw-read regex. Root `CURRENT_BATCH.md` is absent; active source is `Docs/Tasks/CURRENT_BATCH.md`. Status and rationale files created before runtime code edits.
- Pass 1: Tasks 1-5 executed. Prompt re-extracted after task 3. Compile attempt with `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` is blocked by pre-existing missing namespaces/types (`Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `Hecton8.Cartography`, `IDataVault`, `SystemID`, kinematics signals). No `GasDynamicsSolver.cs` errors were emitted before the dependency wall.
- Pass 2: Tasks 6-10 executed. Prompt re-extracted for tasks 6-10. Second compile attempt remains blocked by the same external dependency wall, now 29 errors after unrelated `GlobalPhysicsStateManager` memory namespace surfaced. No gas-solver file errors emitted.
- Pass 3: Tasks 11-15 executed. Prompt re-extracted for consequence/safety tasks. Third compile attempt remains blocked by the same external dependency wall (`Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `Hecton8.Cartography`, `IDataVault`, kinematics/cartography signals). No gas-solver file errors emitted.
- Pass 4: Tasks 16-19 executed. Prompt re-extracted after task 18. Runtime `math.exp` scan excluding Editor returned no hits. Fourth compile attempt remains blocked by the external dependency wall; task 19 marked `[BLOCKED BY DEPENDENCY]` for compile cleanliness while math policy verification passed.
- Pass 5: OMEGA polish executed from `<POLISH_MANDATE id="OMEGA_POLISH">`. Scanned own solver for sqrt/normalize/foreach/string formatting/AUP/transform coupling. Found only cold allocation and black-box dump file/log paths; wrapped dump failure log for editor/development only. Status remains PENDING VERIFICATION because global compile dependencies are red.
- Pass 6: Patient static recheck after user requested no `dotnet build`. Prompt re-extracted with attribute-aware XML regex. Hardened ambient-pressure ingress, occupied-flag preservation across flag/configure writes, zero-bulkhead-capacity enforcement, standard ambient seeding, diffusion conductance authoring, and telemetry struct layout. Scans found no `RoomPressureFront`, `math.exp`, `math.sqrt`, `math.normalize`, managed formatting, AUP/world-position coupling, or `transform.position` in `GasDynamicsSolver.cs`.
- Pass 7: Native-memory and lifecycle hardening under the repeated no-build order. Added cold `GasDynamicsNativeMemoryAudit`/`TryGetNativeMemoryAudit`, edit-mode `OnEnable` guard, deferred-dispose re-enable tick polling, and registry retry after native storage finalizes. Static scans found no forbidden gas-file symbols; `git diff --check` reported only the existing CRLF warning on `GlobalRegistryContracts.cs`.
- Pass 8: Undefined-state and black-box parse hardening. Converted all primary gas/back/ambient lanes from `UninitializedMemory` to `ClearMemory` so seed-disabled external configuration cannot read undefined partial pressures. Added binary dump header fields: magic, version, entry size, capacity, write index, and tick count. Static scans found no remaining `UninitializedMemory` in the gas solver and no forbidden gas-file symbols.
- Pass 9: Toxicity queue and finite-threshold hardening under the no-build order. The solver now drains old toxicity packets before scheduling the next gas job so the prewarmed `NativeQueue` does not grow from stale undrained signals. CO2 and narcosis thresholds are sanitized once on the main thread before being copied into the Burst job.
