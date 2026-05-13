# Status_THERMODYNAMICS_LEAD

Status: PENDING VERIFICATION
Agent: THERMODYNAMICS_LEAD
Role: PHYSICS_PROGRAMMER
Domain: Abyssal Thermodynamics & Ice / Thermodynamics (Heat Diffusion)
Task Count: 19
Prompt Source: Docs/Tasks/CURRENT_BATCH.md
Compile State: BLOCKED BY DEPENDENCY after 3 attempts

## Mandates Read

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- LOGI_Energy_NetworkS_Power_Grid_Graph_Flow.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt

## Checklist

- [x] Task 1: SINGLETON ERADICATION - `ThermalManager.Instance` / `AbyssalThermalManager.Instance` references absent by `rg`; thermodynamics binds through `IThermodynamicsService` / `GlobalRegistry`. DOD: contract binding. Rejected: static instance. Estimate: 1-2 us saved per lookup path by avoiding object search.
- [x] Task 2: SIGNAL MIGRATION - Added 64-byte `TemperatureChangedSignal(AUP, Temp)` and native lane publish. DOD: explicit-layout unmanaged signal. Rejected: C# event. Estimate: 0 alloc, 0.5-1.5 us per publish.
- [x] Task 3: ASMDEF ISOLATION - Added `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` referencing `Hecton8.Core.Contracts`. DOD: isolated assembly marker. Rejected: moving heavy manager into new asmdef during dirty concurrent work. Estimate: compile isolation only, runtime 0 us.
- [x] Task 4: DEAD CODE HUNT - `rg` found no thermal `OnTriggerStay`/`TemperatureZone`; remaining `OnTriggerStay` hits are non-thermal docking/sargassum code. DOD: source scan. Rejected: collider-based thermal zones. Estimate: avoids broad physics callbacks.
- [x] Task 5: THERMAL S.O.A. - Created 32x32x32 `NativeArray<float>` read/write/source grid plus visual projection. DOD: SOA native buffers. Rejected: Texture3D logic. Estimate: bounded 32768-cell cold tick, 0 per-frame grid cost.
- [x] Task 6: DIFFUSION JOB - `ThermalMapJacobiJob` performs 6-neighbor Jacobi; `Insulation01` from voxel SDF damps transfer. DOD: Burst job data-only loop. Rejected: Nav/physics overlap insulation. Estimate: 350-700 us per 0.2Hz tick on mid tier; 0 on Low/MX350.
- [x] Task 7: GEYSER INJECTION - `PersistentWorldRegistry` owns bounded active thermal vent records; geology writes them; thermodynamics syncs by revision and injects at least +200C into grid sources. DOD: PWR snapshot, O(16). Rejected: direct-only geology dependency. Estimate: under 5 us on sync, 0 if revision unchanged.
- [x] Task 8: BRINE POOL FREEZING - Ambient resolves to -2C below runtime depth -1000m for actors and idle grid fills. DOD: deterministic branch. Rejected: region trigger volumes. Estimate: single compare per sample.
- [x] Task 9: ICE OVERLAY - `HectonVisorUberPost.shader` blends procedural frost when `_HectonUberLocalTemperature < 0`. DOD: screen-space visual fake. Rejected: simulated ice geometry. Estimate: a few ALU ops, no texture allocation.
- [x] Task 10: SUBMARINE SLOWDOWN - `ISubmarineRuntimeContext.SetThermalSpeedMultiplier` clamps and multiplies resolved thrust; temp < -5C sets 0.7. DOD: interface decoupling. Rejected: rotor rigidbody drag. Estimate: O(1), sub fixed tick only.
- [x] Task 11: HULL CONTRACTION - Rapid 100C to -5C transition emits packet-native thermal `CombatDamageSignal`. DOD: signal bus, no direct integrity reference. Rejected: calling structural grid concrete. Estimate: one native signal push per shock.
- [x] Task 12: O2 FREEZING - `GasDynamicsSolver` stores room temp and halves scrubber CO2 removal below 0C. DOD: native room temperature lane. Rejected: room MonoBehaviour polling. Estimate: one float read + branch per room.
- [x] Task 13: AUP SHIFT SAFETY - `AupShiftSignal` reaches `SignalBus`; thermal grid origin and cached vents shift logically. DOD: AUP signal snapshot. Rejected: rebuild from scene positions every shift. Estimate: O(vents), no per-frame cost.
- [x] Task 14: ZERO-GC - Diffusion uses persistent `NativeArray<float>` and scheduled job; no managed allocation in Jacobi execution. DOD: native SOA + no LINQ. Rejected: per-tick arrays. Estimate: 0 bytes GC in diffusion path.
- [x] Task 15: MATH LOD - Low/MX350 bypasses grid and uses direct DistanceSq nearest vent thermal sample. DOD: `UsesThermalGrid()` gate. Rejected: balanced middle-only solution. Estimate: saves 32768-cell rebuild/job on toaster hardware.
- [x] Task 16: SAVE DELTA - Non-ambient thermal cells are staged as RLE runs and checksummed through `SaveBinaryStorage.TryStageThermalGridRleDelta`. DOD: compact delta lane. Rejected: save full 128KB+ dense map every tick. Estimate: 100-250 us per completed cold tick, no managed allocation.
- [x] Task 17: AUDIO CUES - Thermal shock publishes `AcousticPingSignal` on a dedicated metal-creak channel. DOD: native acoustic signal. Rejected: direct audio manager call. Estimate: one native signal push per shock.
- [x] Task 18: TELEMETRY - Player ambient temperature is recorded in the fixed 300-frame thermal blackbox; dumps to `Docs/AgentLogs/Dump_THERMODYNAMICS_LEAD.bin` on NaN. DOD: circular NativeArray telemetry. Rejected: text logging every frame. Estimate: one struct write per sample.
- [BLOCKED BY DEPENDENCY] Task 19: OMEGA COMPILE CHECK - Burst compilation cannot be verified. `dotnet build Hecton8.Core.csproj` fails before isolated thermal validation due missing unrelated assemblies/types: `Hecton8.Core.Memory.Layout`, `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `SoundEmissionSignal`, `BrineLayerSample`, `TetherFiredSignal`, and inventory algorithms. Unity MCP reports no Unity session. Logs: `Docs/AgentLogs/Build_THERMODYNAMICS_LEAD_01.log`, `_02.log`, `_03.log`.

## Loop Log

- Loop 0: Prompt extracted with CLI from `Docs/Tasks/CURRENT_BATCH.md`. Domain and mandates read before code.
- Loop 1 Tasks 1-5: Purged singleton pattern, created signal, asmdef marker, scanned trigger code, expanded thermal grid to 32^3. Verified with `rg`. Compile attempt 1 failed on unrelated missing assemblies.
- Loop 2 Tasks 6-9: Re-read thermal manager and shader code; patched wrong combat signal namespace; registered temperature signal lane; added SDF insulation, PWR vent source, +200C source, deep ambient grid fill, shader frost. Prompt re-extracted with CLI.
- Loop 3 Tasks 10-12: Re-read submarine and gas contracts; added thermal speed multiplier and gas room temperature scrubber scale. Rejected rigidbody drag and direct room component calls.
- Loop 4 Tasks 13-16: Re-read AUP signal path and save storage; added AUP bus publish, logical grid shift, zero-GC persistent arrays, low tier bypass, RLE staging. Compile attempt 2 failed on same dependency wall.
- Loop 5 Tasks 17-19: Re-read thermal shock path and blackbox; verified acoustic ping and telemetry dump path; compile attempt 3 failed on same dependency wall. Task 19 marked blocked by dependency.

## Verification Commands

- `rg -n "ThermalManager\\.Instance|AbyssalThermalManager\\.Instance" Assets/_Project/Scripts -g '*.cs'` => no matches.
- `rg -n "OnTriggerStay|TemperatureZone" Assets/_Project/Scripts -g '*.cs'` => only non-thermal docking/sargassum hits.
- `rg -n "PersistentThermalVentRecord|RegisterActiveThermalVent|SyncPersistentThermalVents|ThermalVentInjectionDeltaCelsius|TemperatureChangedSignal|TryGetThermalGridReadback|TrySetRoomTemperatureCelsius" Assets/_Project/Scripts -g '*.cs'` => expected thermal paths present.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false /clp:Summary` => failed 3/3 due unrelated dependency wall.

## Integrator Note

Restore or regenerate missing assemblies before asking for Burst proof: `Hecton8.Core.Memory.Layout`, `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `Hecton8.Inventory.Algorithms`, ground radar contracts, tether contracts, brine layer contracts, and audio propagation contracts. Unity MCP also needs a live editor session for `validate_script` / console validation.
