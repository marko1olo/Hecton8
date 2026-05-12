# Status - KINEMATICS_HYDRO_DRAG

Prompt ID: KINEMATICS_HYDRO_DRAG  
Role: HYDRO_MECHANIC  
Domain: Echelon 4 - Hydrodynamic Drag & Buoyancy  
Status: PENDING VERIFICATION

## Mandates Loaded

- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `PHYS_Tether_Cable_Acceleration_Constraints.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Task Checklist

- [x] 1. Inventory mass sync | DOD: event/global scalar lane plus `SetSubmarineCargoMassKilograms` seam | Alternative rejected: concrete inventory storage dependency | Estimate: 15-35 us/tick saved
- [x] 2. Draft calculation | DOD: cargo mass included in Rigidbody target mass and Gerstner/sample draft offset | Alternative rejected: fake downward force | Estimate: 8-20 us/tick saved
- [x] 3. Directional cross-section drag | DOD: Burst job dot-products local forward/right/up; lateral coefficient = forward * 5 | Alternative rejected: Unity linear damping | Estimate: 20-60 us/tick saved
- [x] 4. Angular hydro-drag | DOD: torque packet uses `-angularVelocity * AngularDragCoefficient * waterDensity * submersion` | Alternative rejected: `Rigidbody.angularDamping` | Estimate: 10-25 us/tick saved
- [x] 5. Ballast blowing | DOD: `BlowBallast/TryBlowBallast` shifts buoyancy and burns compressed-air reserve | Alternative rejected: invented logistics component reference | Estimate: 5-15 us/event saved
- [x] 6. Pitch/roll stability | DOD: mass-scaled righting torque from `math.cross(up, worldUp)` | Alternative rejected: transform auto-level snapping | Estimate: 10-25 us/tick saved
- [x] 7. Crush depth mass penalty | DOD: below safe crush depth uses 0.85 buoyancy scale | Alternative rejected: global gravity/water-density mutation | Estimate: 6-12 us/tick saved
- [x] 8. Player suit weight | DOD: full inventory load resolves upward swim multiplier to 0.6x | Alternative rejected: per-item swim mass iteration | Estimate: 5-15 us/tick saved
- [x] 9. Math LOD cargo scalar | DOD: cached `CargoMassScalar` and mass cache refresh, no item iteration in hydro loop | Alternative rejected: scanning SOA every fixed tick | Estimate: 15-35 us/tick saved
- [x] 10. Surfacing breach VFX | DOD: upward exit > 15 m/s emits `ImpactSignal` | Alternative rejected: continuous surface VFX polling | Estimate: 8-18 us/tick saved
- [x] 11. Towing kinematics | DOD: tether tension vector converts to hydro acceleration packet | Alternative rejected: Unity joints/direct tether dependency | Estimate: 12-30 us/event saved
- [x] 12. Cavitation rumble | DOD: full thrust + speed < 2 m/s triggers haptics/audio rumble with cooldown | Alternative rejected: continuous audio source mutation | Estimate: 8-20 us/event saved
- [x] 13. Zero-GC Burst solver | DOD: velocity/torque integration in `HydroKinematicDragJob`; output consumed in post-fixed swap cadence | Alternative rejected: `Update()` solver or same-frame `Schedule().Complete()` | Estimate: 20-60 us/tick saved
- [x] 14. Rigidbody drag recon | DOD: `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md` written; core submarine prefab damping zeroed | Alternative rejected: blind edits to fauna/resource/transport prefabs | Estimate: determinism gain, CPU not primary
- [x] 15. Omega compile check | DOD: Burst drag solver source uses `float3`/`math` only; `SubmarineFluidDynamics.cs` Unity validator = 0 diagnostics | Alternative rejected: claiming full project compile while unrelated errors remain | Estimate: verification hygiene

## Strict Iteration Log

Loop 0:
- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`.
- AGENTS.md, domain map, and task mandates read.
- Status/rationale files created.

Loop 1 - Tasks 1-5:
- Implemented inventory mass sync, cargo draft, directional drag, angular drag, and ballast blow.
- Verification: source inspection and `SubmarineFluidDynamics.cs` validation path prepared.

Loop 2 - Tasks 6-10:
- Implemented pitch/roll righting, crush-depth buoyancy scale, player upward swim mass penalty, cached cargo scalar, and surfacing breach signal.
- Verification: code reread for hot-path allocations and domain coupling.

Loop 3 - Tasks 11-15:
- Implemented towing acceleration injection, cavitation feedback, Burst job cadence, drag recon, and `float3` solver check.
- Verification: recon scan found `PFB_Submarine_Core.prefab` damping issue.

Loop 4 - Evidence repair:
- Fixed `PFB_Submarine_Core.prefab` `m_AngularDamping: 0.05` to `0`.
- Wrote `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`.

Loop 5 - AAA mandate repair:
- Added fixed 300-frame hydro black box and binary dump path `Docs/AgentLogs/Dump_KINEMATICS_HYDRO_DRAG.bin`.
- Ran OMEGA polish after checklist completion: removed redundant `sqrt/rsqrt` righting torque math and removed unconditional job-axis normalization.
- Verified `SubmarineFluidDynamics.cs` with Unity MCP `validate_script`: 0 diagnostics after polish.

## Verification State

- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: Unity MCP `validate_script`, basic, 0 errors / 0 warnings after black-box and polish edits.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`: Unity MCP validator timed out in regex engine on large file; no Unity console error currently points to this file.
- Unity console compile blockers are unrelated to this hydro batch: `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs` missing Burst symbols and `Assets/_Project/Scripts/SaveBinaryStorage.cs` Burst `catch` filter error.
- `dotnet build Assembly-CSharp.csproj /p:HectonSkipAssemblyProjectReferences=true /p:BuildProjectReferences=false /m:1` ran for OMEGA polish and failed on missing Unity-generated metadata assemblies; log: `Docs/AgentLogs/KINEMATICS_HYDRO_DRAG_dotnet_polish.log`.
- Required prompt status remains `PENDING VERIFICATION` until unrelated compile blockers are cleared and a full Unity compile can complete.

## Evidence Pointers

- Directional drag code: `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`, `HydroKinematicDragJob.Execute`.
- Player upward swim mass code: `Assets/_Project/Scripts/HectonPlayerMovement.cs`, `ResolveInventoryUpwardSwimMultiplierFromLoad`.
- Recon report: `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`.
- Rationale: `Docs/AgentLogs/Rationale_KINEMATICS_HYDRO_DRAG.md`.
- Final log: `Docs/AgentLogs/LOG_KINEMATICS_HYDRO_DRAG.md`.
