# Status_PHYSICS_FLUIDS

Prompt: PHYSICS_FLUIDS
Role: HYDRO_ENGINEER
Domain: Hydro-Dynamics / Fluid Physics
Task Count: 20
Batch Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Mandates Loaded
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Fluid_Incursion_Interior.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_Rsqrt_i3_SIMD.txt

## State Machine Checklist
- [x] Task 1 - 3D VECTOR NOISE SAMPLING | DOD: existing 32x32x32 curl `Texture3D` plus unmanaged `NativeArray<float3>` lookup verified in `SamplePrebakedVectorCurrent` using masked AUP cell coordinates | Rejected: runtime CPU curl/noise and fluid solve | Estimate: 20-80 us saved per 100 sampled bodies, PENDING VERIFICATION
- [x] Task 2 - MATH LOD BUOYANCY | DOD: exact-normal flag remains only for hero/player high-tier path; debris keeps `DominantAxisOrDefault` | Rejected: universal exact `math.normalize` | Estimate: 5-15 us saved per 500 debris, PENDING VERIFICATION
- [x] Task 3 - TRIANGLE WAVE CURRENT FAKE | DOD: 3D vector sample intensity is modulated by deterministic `FastTriangleSigned(time * timeScale)` | Rejected: sine/noise allocation or CPU turbulence field | Estimate: 3-8 us saved per frame versus trig/noise, PENDING VERIFICATION
- [x] Task 4 - PROPWASH CONE CHEAT | DOD: `ApplyThrusterFlow` uses squared distance, dot/axial cone test, and falloff; no displacement field | Rejected: simulated propwash volume/fluid displacement | Estimate: 10-30 us saved per active thruster cluster, PENDING VERIFICATION
- [x] Task 5 - WHIRLPOOL CROSS-PRODUCT FAKE | DOD: tangential flow now uses `math.cross(up,toCenter)` with `rsqrt`, centripetal force, radiusSq gate | Rejected: particle vortex or `math.distance`/sqrt radius | Estimate: 2-6 us saved per whirlpool sample, PENDING VERIFICATION
- [x] Task 6 - CAPPED QUADRATIC DRAG | DOD: drag uses `-relativeVelocity * max(1, approxSpeed) * dragScalar` and `ClampVectorMagnitude(maxQuadraticDragForcePerKg * mass)` | Rejected: exact speed sqrt and uncapped force spikes | Estimate: 4-12 us saved in 100-body drag pass, PENDING VERIFICATION
- [x] Task 7 - DEEP-SUBMERGED EARLY OUT | DOD: CPU `WaveQueryJob` and GPU buoyancy skip Gerstner when base depth exceeds object height + wave envelope + 5m | Rejected: wave sampling fully submerged bodies | Estimate: 8-25 us saved in deep object sets, PENDING VERIFICATION
- [x] Task 8 - THERMOCLINE Z-SHEAR | DOD: deep layer density multiplier defaults to 1.5 and constant Z shear is applied per kg under the halocline threshold | Rejected: simulated stratified-fluid layers | Estimate: 3-10 us saved versus dynamic layer solve, PENDING VERIFICATION
- [x] Task 9 - BOUNDED BFS INTERIOR FLOOD | DOD: `InteriorFloodBfsJob` hard-caps `MaxFloodNodesPerFrame = 5` with edge budget | Rejected: full graph traversal every frame | Estimate: 15-60 us saved on compartment-heavy submarines, PENDING VERIFICATION
- [x] Task 10 - AUP-SYNCHRONIZED TIDE | DOD: global water level triangle wave now resolves phase from `AbsoluteUniverseTime` with finite fallback | Rejected: scene-local `Time.time` tide authority | Estimate: 0 us frame-time target, deterministic sync gain, PENDING VERIFICATION
- [x] Task 11 - ACOUSTIC SPLASH QUEUE | DOD: water-entry crossings publish `ImpactSignal` via `GlobalSignals` native queue from fluid and submarine splash paths | Rejected: direct audio calls/string events | Estimate: <2 us per impact burst, PENDING VERIFICATION
- [x] Task 12 - CPU-GPU GERSTNER SYNC | DOD: CPU and GPU read the same `WeatherRuntimeSnapshot` Gerstner wave params; GPU constants are grouped in `HectonGpuBuoyancyConstants` | Rejected: shader-only duplicate wave state | Estimate: sync correctness, not frame-time; PENDING VERIFICATION
- [x] Task 13 - RECIPROCAL MAX | DOD: GPU buoyancy uses `rcp(max(...))` for wave number and submersion reciprocal paths | Rejected: raw scalar divisions in compute shader | Estimate: 1-4 us per large GPU dispatch, PENDING VERIFICATION
- [x] Task 14 - CACHE ALIGNMENT | DOD: `BuoyancyParams` has explicit `StructLayout(Size = 96)`, a 32-byte multiple | Rejected: implicit packing | Estimate: 1-3 us from stride/cache predictability in large arrays, PENDING VERIFICATION
- [x] Task 15 - VISCOSITY LUT | DOD: persistent 16-sample smoothstep LUT feeds viscosity region sampling in Burst | Rejected: dynamic viscosity curves in hot path | Estimate: 2-8 us in viscosity-heavy regions, PENDING VERIFICATION
- [x] Task 16 - HASH TELEMETRY | DOD: hydro emergency reset/non-finite paths emit numeric hashes through `GlobalTelemetryBus` | Rejected: string-context logs in reset/hot paths | Estimate: 1-3 us plus 0B GC risk avoided, PENDING VERIFICATION
- [x] Task 17 - RSQRT FOOTPRINT | DOD: footprint fallback uses `safeValue * math.rsqrt(max(epsilon, safeValue))`, not `math.sqrt(footprintArea)` | Rejected: scalar sqrt footprint | Estimate: 1-3 us per fallback rebuild, PENDING VERIFICATION
- [x] Task 18 - LCG SPLASH HASHING | DOD: submarine splash events use AUP/sample-index LCG hashing for deterministic gain; `UnityEngine.Random` absent from splash generation | Rejected: nondeterministic random splash jitter | Estimate: 1-3 us and deterministic replay gain, PENDING VERIFICATION
- [x] Task 19 - LATE-SWAP SCHEDULE | DOD: `FixedTick` schedules jobs early and drains only through nonblocking `DispatcherJobSwap.TryComplete(..., false)` in post-fixed swap window | Rejected: immediate blocking `.Complete()` in fixed tick | Estimate: 50-200 us stall avoidance during spikes, PENDING VERIFICATION
- [x] Task 20 - OMEGA COMPILE CHECK [BLOCKED BY DEPENDENCY] | DOD: `HectonUnderwaterVisuals` interface methods are present and earlier post-hydro build succeeded; latest compile blocked by unrelated `GlobalSignals`, `ConstructionManager`, `FaunaBrain` errors | Rejected: out-of-domain core/construction/fauna edits | Estimate: compile integration only, PENDING VERIFICATION

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.txt. Status and rationale files were absent; clean state initialized. No code touched yet.
- Loop 1: Re-extracted PHYSICS_FLUIDS from CURRENT_BATCH.md after task 3 boundary. Tasks 1-5 checked. Compile gate: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` succeeded with 0 errors and 0 warnings after hydro namespace fix.
- Loop 2: Read checklist, re-extracted PHYSICS_FLUIDS at task 6 and task 9 boundaries, then checked tasks 6-10. Compile evidence remains the post-patch `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` success with 0 errors and 0 warnings.
- Loop 3: Read checklist, re-extracted PHYSICS_FLUIDS at task 12 boundary, then checked tasks 11-15. Compile evidence remains `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` success with 0 errors and 0 warnings; shader constant-buffer syntax still needs Unity import/compiler verification.
- Loop 4: Read checklist, re-extracted PHYSICS_FLUIDS at task 15 and task 18 boundaries, then checked tasks 16-19. Task 20 marked blocked by dependency after latest compile failed in out-of-domain files: `GlobalSignals.cs` missing signal types, `ConstructionManager.cs` missing `OnOriginShift`, `FaunaBrain.cs` missing `FaunaTier1LodProxyEntry`.
- Loop 5: OMEGA_POLISH parsed and executed. Diff-level anti-bloat scan found no added sqrt, random, managed foreach, string formatting, `.ToString()`, or vector-distance debt. Restored exact `math.normalize` only for the high-tier hero/player exact-normal branch. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` failed outside hydro with 47 warnings and 11 errors in construction/save systems; status remains PENDING VERIFICATION.
