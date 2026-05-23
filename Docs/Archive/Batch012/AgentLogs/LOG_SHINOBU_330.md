# SHINOBU_330 Execution Log

## 2026-05-22 - Fluid Incursion CSR BFS Flood Distributor

What was wrong:
- `BaseModule` retained managed dry-zone flood authority: `Dictionary<ulong, BuoyancyObject>` plus flood-state `EnterDryZone`/`ExitDryZone` calls.
- `FluidCompartmentDTO` was still documented/partially treated as the old 32-byte room row with stale field names in downstream code.
- HFI CSR graph lacked a separate edge conductance lane and per-edge transfer remainder lane.
- Old submarine flood paths still wrote Rigidbody mass/COM/inertia from flood-derived state.
- `HabitatIntegrityManager` still owned local flood accumulation instead of publishing an unmanaged incursion signal.
- First static BufferID allocation attempt collided with existing IDs `70799` and `70800`.

What was done:
- Converted `FluidCompartmentDTO` to explicit 64-byte layout: `double3 LocalCenterOfMass@0`, `NodeHashID@24`, `CurrentWaterVolume@28`, `MaxWaterVolume@32`, `WaterLevelHeight01@36`, `Flags@40`, padding through byte 63.
- Added `ShinobuFluidEdgeConductivity=73330` and `ShinobuFluidTransferRemainders=73331` with duplicate-ID check proof.
- Added deterministic Burst mock injection job and CSR BFS equalization with scalar conductance, double Y head math, continuous quality cadence/budget, and signed milliliter transfer quantization.
- Updated CSV/parser, sump pump drain code, fuzzer layout check, director gizmos, docs, and binary payload ledger for the 64B DTO.
- Converted `HabitatIntegrityManager` into `SignalBus<FluidIncursionSignal>` producer.
- Preserved vehicle owner route: HFI publishes `SubmarineFloodStateSignal`/`PhysicsEventPayload`; vehicle dynamics consumes mass into existing AddedMass tensor jobs.
- Removed flood-derived Rigidbody mass/COM/inertia writes from `SubmarineFluidDynamics`; retained only dry restore writes as non-flood authority.
- Added `Tools/OOP_Water_Trigger_Scanner_SHINOBU_330.py`, dedicated report `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_330.json`, shared physics report entry, and `Docs/Reports/SHINOBU_330_SELF_AUDIT.xml`.

Cinematic cheats used:
- No interior water mesh/plane truth. BaseModule disables authored water object authority; shader/global-buffer scalar waterline owns presentation.
- CPU solves only scalar water volume/mass. GPU/visor side handles tint/fog/wobble style presentation.
- Physics tilt route is mathematical AddedMass signal consumption, not Rigidbody water components.

Proof artifacts:
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_330.json`: `findingCount=0`, `legacyWaterAuthorityEradicated=true`, `scannedFileCount=17`.
- `Docs/Reports/SHINOBU_330_SELF_AUDIT.xml`: XML validated with PowerShell `[xml]`.
- Focused `git diff --check`: clean, CRLF warnings only.
- BufferID proof: exactly one `73330` owner and one `73331` owner in `H8Memory.cs`.

Exact microseconds saved:
- Exact measured runtime microseconds: not available. Compile/profiler was blocked by active local `dotnet`/MSBuild nodes and CPU >50%.
- Static target retained from task/rationale: remove object water broadphase/component path; CSR target is under 100 us for 5000 nodes pending profiler.
- Static estimate recorded: managed graph traversal removal target 35 us per 1000 rooms; DTO raw-field mutation target 4 us per 5000 nodes. These are estimates, not profiler facts.

Compile status:
- Build not launched. Gate samples: CPU 100 with active `dotnet build Assembly-CSharp.csproj`/`csc.exe`; later CPU 66/54/66 with seven `dotnet` MSBuild node processes still present.
- This is marked blocked by active compiler/CPU gate, not passed.

## 2026-05-22 - Airlock/Buoyancy Dry-Zone Tail Removal

What was wrong:
- `BaseAirlock` still cached `BuoyancyObject` and called `EnterDryZone`/`ExitDryZone` during player transitions.
- `BuoyancyObject` still owned a dry-zone ref-count and public dry-zone mutation API even after compartment water truth moved to HFI CSR/Vault state.
- The static scanner did not include `BaseAirlock` or `BuoyancyObject`, so it could miss this legacy route.

What was done:
- Removed `BaseAirlock` `BuoyancyObject` fields, lookup, snap cache, and dry-zone mutation calls.
- Removed `BuoyancyObject` dry-zone ref-count plus `EnterDryZone`/`ExitDryZone`; `IsInDryZone` is now a false compatibility read for out-of-domain consumers.
- Expanded `OOP_Water_Trigger_Scanner_SHINOBU_330.py` to scan `BaseAirlock` and `BuoyancyObject`.

Cinematic cheats used:
- Airlock keeps only audio/event transition presentation. No managed buoyancy suppression participates in compartment water.

Proof artifacts:
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_330.json`: `findingCount=0`, `legacyWaterAuthorityEradicated=true`, `scannedFileCount=19`.
- Broad source scan found no runtime `EnterDryZone`, `ExitDryZone`, `_dryZoneRefCount`, water-plane Transform writes, or `waterVolume.SetActive(true)` matches.
- Focused `git diff --check`: clean, CRLF warnings only.

Exact microseconds saved:
- Exact profiler microseconds remain pending compile/profiler availability.
- Removed airlock `TryGetComponent<BuoyancyObject>` and dry-zone ref-count writes from transition path; this is correctness/architecture cleanup more than a frame-time hotspot.

Compile status:
- Build not launched after this patch. Gate sample: `CPU=100` with active `VBCSCompiler.exe` (`C:\Program Files\dotnet\sdk\10.0.202\Roslyn\bincore\VBCSCompiler.exe`).
