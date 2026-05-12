# LOG - KINEMATICS_HYDRO_DRAG

## 2026-05-12 - Hydro Drag / True Buoyancy R&D Pass

Status: PENDING VERIFICATION  
Role: HYDRO_MECHANIC  
Domain: Echelon 4 - Hydrodynamic Drag & Buoyancy

### What Was Wrong

- Submarine buoyancy behaved like a balloon: cargo mass was not coupled into hull mass/draft strongly enough.
- Hydro drag was effectively isotropic. Side-slip did not pay the expected cross-section penalty.
- Built-in Unity damping still existed on `PFB_Submarine_Core.prefab`, hiding custom solver tuning behind `Rigidbody` damping.
- Ballast/towing/cavitation had no decoupled hydro-facing API.
- Player upward swim did not apply the requested inventory mass penalty.
- Hydro was critical physics but lacked its own 300-frame binary black box.

### What Was Done

- Added event/global cargo mass sync and cached `CargoMassScalar`.
- Included cargo mass in submarine target mass and draft offset.
- Added Burst `HydroKinematicDragJob` with local-axis dot products:

```csharp
float forwardSpeed = math.dot(velocity, forward);
float lateralSpeed = math.dot(velocity, right);
float verticalSpeed = math.dot(velocity, up);

float3 dragForce =
    (-forward * forwardSpeed * math.abs(forwardSpeed) * math.max(0f, input.ForwardDragCoefficient)) +
    (-right * lateralSpeed * math.abs(lateralSpeed) * math.max(0f, input.LateralDragCoefficient)) +
    (-up * verticalSpeed * math.abs(verticalSpeed) * math.max(0f, input.VerticalDragCoefficient));
```

- Added angular hydro drag and mass-scaled pitch/roll righting torque.
- Added ballast blow command with compressed-air burn and buoyancy bias.
- Added crush-depth buoyancy scale: below safe depth, buoyancy resolves to 85%.
- Added player upward swim mass multiplier: full load resolves to 0.6x upward swim force.
- Added surfacing breach `ImpactSignal` for upward water exit over 15 m/s.
- Added towing tension vector injection into the hydro acceleration packet.
- Added cavitation haptic/audio rumble for full thrust while speed is under 2 m/s.
- Added fixed 300-frame `NativeArray<HydroBlackBoxEntry>` and dump file `Docs/AgentLogs/Dump_KINEMATICS_HYDRO_DRAG.bin`.
- Wrote `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`.
- Fixed `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab` from `m_AngularDamping: 0.05` to `0`.

### Cinematic Cheats Used

- Scalar cargo draft offset instead of fluid displacement solve.
- 5x lateral drag coefficient instead of CFD cross-section sampling.
- 0.85 crush-depth buoyancy scale instead of mesh/hull deformation.
- Ballast buoyancy bias instead of modeling air/water volume exchange.
- Cavitation rumble as event-gated haptics/audio instead of bubble simulation.
- Deterministic breach/splash signal amplification instead of continuous particle physics.
- Cached low-tier `CargoMassScalar` instead of per-item iteration in the hydro loop.

### Microseconds Saved

Exact profiler-measured savings: not available because full Unity compile is currently blocked outside this domain. Reporting measured numbers would be fake.

Engineering estimate for i3/MX350 hot paths:

- Inventory scalar cache vs item iteration: 15-35 us/tick.
- Cargo draft scalar vs per-sample cargo solve: 8-20 us/tick.
- Burst directional drag packet vs main-thread/sample-heavy drag: 20-60 us/tick.
- Angular/righting torque simplification after OMEGA polish: 4-12 us/tick.
- Player swim cached multiplier vs item-level swim checks: 5-15 us/tick.
- Towing/cavitation event lanes vs object search/joint/audio source mutation: 20-50 us/event.

### Verification

- `SubmarineFluidDynamics.cs`: Unity MCP `validate_script` basic = 0 diagnostics after black-box and OMEGA polish edits.
- `HectonPlayerMovement.cs`: Unity MCP validator times out in regex engine on this large file; no current Unity console error references it.
- Full Unity compile is blocked by non-hydro files:
  - `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs`: missing Burst symbols in editor test assembly.
  - `Assets/_Project/Scripts/SaveBinaryStorage.cs`: Burst BC1007, unsupported `catch` filter.
- OMEGA `dotnet build Assembly-CSharp.csproj /p:HectonSkipAssemblyProjectReferences=true /p:BuildProjectReferences=false /m:1` failed because Unity-generated metadata assemblies are missing; log: `Docs/AgentLogs/KINEMATICS_HYDRO_DRAG_dotnet_polish.log`.

### Scoped Final Diff

- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: true buoyancy cargo coupling, Burst directional drag, ballast/towing/cavitation, breach signal, zero damping enforcement, hydro black box.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`: upward swim load multiplier.
- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab`: zeroed built-in angular damping.
- `Docs/Tasks/RECON_KINEMATICS_HYDRO_DRAG.md`: built-in damping recon.
- `Docs/Tasks/Status_KINEMATICS_HYDRO_DRAG.md`: checklist/evidence.
- `Docs/AgentLogs/Rationale_KINEMATICS_HYDRO_DRAG.md`: decisions and OMEGA polish evidence.
