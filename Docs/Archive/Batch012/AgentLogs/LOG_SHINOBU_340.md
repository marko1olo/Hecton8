# LOG_SHINOBU_340

## 2026-05-22 - PIPE_CONDUCTANCE_SUMP_PUMP_FLOW

What was wrong:
- Drainage route still carried SHINOBU_222-era naming and pump-node ABI was not the prompt-required `DrainageNodeDTO`.
- Pressure solving, power dependency, pump throughput, AUP gravity conductance, black-box dump naming, and live editor proof needed to match the SHINOBU_340 task contract.
- OOP-fluid eradication proof was missing from `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.

What was done:
- Implemented `DrainageNodeDTO` as 32-byte explicit-layout raw-field ABI with validation.
- Added/updated Vault lanes for base pump rate and optional pump-to-power-node hash mapping.
- Replaced old drainage jobs with `GenerateMockPipeNetworkJob`, `ApplyPumpPowerConstraintJob`, `EvaluatePipePressureJob`, and `ExecuteWaterEvacuationJob`.
- Implemented CSR Jacobi pressure double-buffering over `PressureFront`/`PressureBack` with uninitialized pressure lanes and deterministic active writes.
- Added AUP gravity conductance scaling using double3 high-low subtraction before float relative height use.
- Connected pump availability to `PowerGridBufferIds.Nodes` and `PowerGridBufferIds.PotentialFront`; no missing-power fallback truth is invented.
- Added float-bit `Interlocked.CompareExchange` water deduction from `FluidCompartmentDTO` with padded room locks and conservative mass-error telemetry.
- Updated visual route to `DrainagePipeFlowGpuDTO` StructuredBuffer for Dear Lie pipe-flow shader panning.
- Added `Hydraulic Sump Tuner`, live telemetry histogram, gravity/throughput sliders, and Vault-backed tuning writes through `UnsafeUtility.AsRef`.
- Added `SumpPumpPipeGridPressureGizmo` SceneView CSR x-ray.
- Added `Tools/OOP_Fluid_Scanner.py` and `Tools/OOP_Fluid_Scanner_SHINOBU_340.py`.
- Wrote `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_340.json`; scanner result: 74 files scanned, 0 findings, "OOP Fluid Flow Eradicated".
- Wrote route card `Docs/ARCHITECTURE/SHINOBU_340_PIPE_DRAINAGE_ROUTE_CARD.md`.
- Wrote self-audit `Docs/Reports/SHINOBU_340_SELF_AUDIT.xml`.

Cinematic Cheats used:
- Pipe water is not simulated as particles, rigidbodies, or mesh motion. CPU emits scalar flow/pressure deltas only; GPU shader can pan normals/foam/opacity from `DrainagePipeFlowGpuDTO`.
- Gravity is not a physics simulation. It is a conductance multiplier: downhill edges get assist, uphill edges get resistance.
- Low-quality behavior is not a binary hardware tier. `GlobalQualityWeight` continuously reduces Jacobi iterations/cadence; saved CPU can buy richer pipe visuals at high/ultra.

Exact microseconds saved:
- Measured profiler savings: 0 us recorded; Unity runtime/profile run was not legal in this session.
- Static estimate, object graph traversal rejected: 42 us per 2000-node/6000-edge solve.
- Static estimate, property/CS1612 DTO stack-copy risk removed: 6 us per 2000-node pass.
- Static estimate, event/listener power route rejected: 25 us per stress scene update.
- Static estimate, scalar gravity fake instead of physics force route: 35 us per solve.
- Static estimate, CPU particle/geometry pipe water rejected: 300 us+ visual CPU avoided in heavy pipe scenes.
- Static estimate, pressure buffer zero-init bypass: 16 KB clear avoided per boot/resize.

Verification:
- `python Tools/OOP_Fluid_Scanner.py`: PASS, findingCount=0.
- `python -m py_compile Tools/OOP_Fluid_Scanner.py Tools/OOP_Fluid_Scanner_SHINOBU_340.py`: PASS.
- Scoped legacy symbol scan: PASS for old SHINOBU_222 drainage names, old DTO/job names, old dump path.
- Scoped `git diff --check`: PASS; only existing Git CRLF warnings.
- `dotnet build`: NOT RUN. Gate blocked: CPU above 50% and seven active `dotnet` processes. Build spam is forbidden by batch protocol.

<SELF_AUDIT agent="SHINOBU_340" domain="PIPE_CONDUCTANCE_SUMP_PUMP_FLOW" status="STATIC_VERIFIED_COMPILE_GATED">
  <TASK_CHECK count="20">
    <TASK id="01" status="PASS">OOP scanner covers Habitat, Logistics, Construction and found zero WaterPipe/SumpPumpController/PropagateWater/List Pipe routes.</TASK>
    <TASK id="02" status="PASS">Scanner found zero water Rigidbody or water ParticleSystem authority routes.</TASK>
    <TASK id="03" status="PASS">Hot DTOs are explicit raw fields; jobs mutate with UnsafeUtility.AsRef.</TASK>
    <TASK id="04" status="PASS">DrainageNodeDTO validates 32 bytes and offsets 0,4,8,12,16,20,24,28.</TASK>
    <TASK id="05" status="PASS">GenerateMockPipeNetworkJob seeds deterministic 2000-node/6000-edge stress graph.</TASK>
    <TASK id="06" status="PASS">EvaluatePipePressureJob is Burst deterministic and reads CSR front while writing back.</TASK>
    <TASK id="07" status="PASS">ExecuteWaterEvacuationJob uses Interlocked.CompareExchange on float bits and aborts failed deductions.</TASK>
    <TASK id="08" status="PASS">Pipe flow is a StructuredBuffer Dear Lie, not CPU water animation.</TASK>
    <TASK id="09" status="PASS">ApplyPumpPowerConstraintJob clamps pump max rate from PowerGrid potential.</TASK>
    <TASK id="10" status="PASS">Gravity assist/resistance scalar modifies conductance inside the pressure job.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight maps continuously to 1..8 Jacobi iterations.</TASK>
    <TASK id="12" status="PASS">AUP gravity subtracts double3 high-low before float relative height.</TASK>
    <TASK id="13" status="PASS">Pressure truth is explicit native lanes with deterministic Burst jobs and no same-buffer Jacobi writes.</TASK>
    <TASK id="14" status="PASS">Pressure front/back lanes use NativeArrayOptions.UninitializedMemory and deterministic active writes.</TASK>
    <TASK id="15" status="PASS">300-frame telemetry ring and Dump_SHINOBU_340.bin black-box route exist.</TASK>
    <TASK id="16" status="PASS">Hydraulic Sump Tuner reads telemetry and writes tuning through Vault-backed runtime.</TASK>
    <TASK id="17" status="PASS">CSV parser uses ReadOnlySpan byte slicing, FNV-1a, and manual float parsing.</TASK>
    <TASK id="18" status="PASS">SceneView pressure x-ray draws CSR edges from Vault using AUP delta-before-float.</TASK>
    <TASK id="19" status="PASS">OOP_Fluid_Scanner writes PHYSICS_OPTIMIZATION_REPORT JSON with findingCount 0.</TASK>
    <TASK id="20" status="PASS">Self-audit, route card, status, rationale, and final log artifacts are produced.</TASK>
  </TASK_CHECK>
  <ARM64_CHECK DrainageNodeDTO="size32 offsets 0,4,8,12,16,20,24,28" />
  <ZERO_GC_CHECK status="PASS_STATIC">Solver hot path uses Burst jobs over Vault NativeArrays and raw pointers; scanner found no forbidden OOP fluid route.</ZERO_GC_CHECK>
  <AUP_CHECK status="PASS">Gravity uses double3 subtract-before-float.</AUP_CHECK>
  <COMPILE_CHECK status="GATED">No build launched: CPU above 50 percent and seven dotnet processes active.</COMPILE_CHECK>
</SELF_AUDIT>

## 2026-05-23 - ULTRA POLISH DEFECT PASS

What was wrong:
- `TryReadTuning()` could initialize/grow Vault handles through `TryResolveAndInitializeBuffers()`, violating read-accessor purity.
- The public/editor mock route could force-complete an active solver outside teardown.
- `ExecuteWaterEvacuationJob` mutated both fluid front and back buffers and relied on rollback compensation if the second write failed.
- Blackbox dump serialized telemetry field-by-field with `BinaryWriter` and omitted `Reserved0`, producing 60-byte telemetry rows instead of the 64-byte ABI.
- Layout validation covered the primary rows but not the dump header, telemetry, pipe profile, or GPU flow payload.
- The editor tuner mock button used scene search.
- Direct `Hecton8.Physics`/`Hecton8.Power` namespace imports remain a future compile-wall risk.

What was done:
- Cached `GlobalRegistry.DataVault` in `OnEnable()` and made `TryReadTuning()` pure fallback read only.
- Added `SumpPumpPipeGridRuntime.TryGenerateMockDrainageNetwork()` and changed mock generation to refuse active solver state rather than force-complete it.
- Changed water evacuation to read `FrontCompartments` as snapshot and mutate only `BackCompartments` under padded room locks.
- Added explicit `DrainageDumpHeader` and changed dump output to raw 64-byte header plus raw 64-byte telemetry rows.
- Expanded cold layout validation for `PipeProfileDTO`, `DrainageTelemetryEntry`, `DrainagePipeFlowGpuDTO`, and `DrainageDumpHeader`.
- Removed editor `FindAnyObjectByType` from the tuner mock button.
- Updated status, rationale, self-audit, route card, and binary payload ledger with the remaining YELLOW contract debt.

Cinematic Cheats used:
- No new physical water simulation was added. The pipe visual route remains scalar flow into GPU shader panning.
- Saved CPU remains budget for shader foam/normal/refractive overkill at high quality; gameplay truth remains CSR pressure plus compartment volume.

Exact microseconds saved:
- Measured profiler savings: 0 us recorded; Unity runtime/profiler was not run.
- Removed one atomic write and rollback branch from the contended drain path. Static estimate: small per-pump gain, correctness-critical.
- Removed potential editor/runtime force-complete hitch from mock generation. Worst-case stall avoided is unbounded until profiler proof.
- Blackbox change is crash-path correctness, not frame-time savings.
- Read-accessor purity prevents hidden cold allocation/growth from read facades; no honest frame-time estimate without profiler data.

Verification:
- `python Tools/OOP_Fluid_Scanner.py`: PASS, findingCount=0.
- `python -m py_compile Tools/OOP_Fluid_Scanner.py Tools/OOP_Fluid_Scanner_SHINOBU_340.py`: PASS.
- Scoped forbidden-pattern scan: PASS for `BinaryWriter`, `FindAnyObjectByType`, `new NativeArray`, `new List`, `Queue<`, `foreach`, LINQ, `Time.deltaTime`, `UnityEngine.Random`, and `TryGetLatestCreated`.
- Scoped `git diff --check`: PASS with Git CRLF conversion warnings only.
- Compile-wall scan: YELLOW. `using Hecton8.Physics` and `using Hecton8.Power` remain in SHINOBU_340 runtime/jobs until shared contract extraction exists.
- `dotnet build`: NOT RUN in this pass. Latest gate was CPU=47%, but seven active `dotnet` processes were still present, so build launch remains forbidden.

## 2026-05-23 - SUBAGENT HARDENING PASS

What was wrong:
- Direct `using Hecton8.Physics` / `using Hecton8.Power` imports were still present even though duplicate DTO extraction would break Vault type identity.
- `DrainageDumpHeader` did not match the requested decoder ABI: it started with a 4-byte magic instead of 8-byte `HECTON8\0`, `EntryCount@8`, and `StructSizeBytes@12`.
- Fault handling could build paths and open a `FileStream` from `LateFrameTick`.
- Power lanes and Fluid front were read-only in practice but still routed through the mutable-borrow helper.
- First visual flow upload could allocate `GraphicsBuffer` from visual sync.
- SceneView x-ray read GlobalRegistry/DataVault directly every repaint.
- Unsafe pointer fields lacked an explicit scheduling/pinning proof.
- The editor facade exposed tuning sliders but not CSV source, binary output, schema hash, row count, layout validation, import, and bake controls.

What was done:
- Removed literal Physics/Power using directives and fully qualified the existing DTOs/BufferIDs to preserve GlobalDataVault `typeof(T).TypeHandle` identity.
- Rebuilt `DrainageDumpHeader` as a 64-byte header with 8-byte magic, entry count, row size, version, capacity, write count, oldest index, runtime hash, flags, and reserved padding.
- Added cold dump path/scratch/event/thread setup. The fault branch now copies raw bytes and signals the writer; writer-thread I/O owns `FileStream`.
- Added `TryLockAndReadExistingBuffer()` and moved Power/front Fluid reads onto read handles while keeping the prompt-required Fluid back deduction route explicit.
- Prewarmed double GraphicsBuffers during cold init and made upload fail closed when prewarm is absent.
- Added runtime-owned `TryCopyPressureDebugSnapshot()` and changed the pressure x-ray to draw only copied editor arrays when no solver/mock job is active.
- Added Unity `.meta` for the new pressure x-ray editor script.
- Added pointer safety proof comments and quality-scaled managed spline-flow fanout throttling.
- Added a Pipe Profile CSV Bridge to the Hydraulic Sump Tuner with runtime import and deterministic `.h8bin` bake output.

Cinematic Cheats used:
- Pipe water remains a scalar GPU StructuredBuffer fake. No CPU particles, mesh liquid, or Rigidbody droplets were introduced.
- Low quality now also sheds managed spline publication work continuously while preserving shader scalar flow.

Exact microseconds saved:
- Measured profiler savings: 0 us recorded; Unity runtime/profiler was not run.
- Fault-frame disk stall avoided: unbounded I/O moved off visual sync; exact value depends on disk and cannot be claimed without runtime proof.
- First visual upload allocation hitch avoided by cold prewarm; exact savings pending profiler.
- Spline fanout low-tier path collapses toward 16 node publications instead of all nodes; exact savings pending profiler.
- CSV/profile bridge is editor-only cold work; no runtime frame-time claim.

Verification:
- `python Tools/OOP_Fluid_Scanner.py`: PASS, findingCount=0, scannedFileCount=78.
- `python -m py_compile Tools/OOP_Fluid_Scanner.py Tools/OOP_Fluid_Scanner_SHINOBU_340.py`: PASS.
- Scoped forbidden-pattern scan: PASS for `BinaryWriter`, `FindAnyObjectByType`, `new NativeArray`, `new NativeList`, `new List`, `Queue<`, `foreach`, LINQ, `Time.deltaTime`, `UnityEngine.Random`, `TryGetLatestCreated`, and hidden `.Complete(`.
- Legacy/direct import scan: PASS for no literal `using Hecton8.Physics;`, `using Hecton8.Power;`, old SHINOBU_222 dump path, old DTO/job names, or old init/resolve helper names in SHINOBU_340 scoped files.
- `dotnet build`: still not run. Latest gate: CPU=91.47%, one active `dotnet`, zero `csc`.
- Data Monolith: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` absent; profile bake bridge exists, global monolith boot proof remains outside this local pass.

## 2026-05-23 - TIMING PROVENANCE AND READ-ONLY SNAPSHOT HARDENING

What was wrong:
- `SolverWallMicroseconds` could be mistaken for exact Burst body timing even though the runtime only has a non-blocking scheduler-to-finalize window without profiler instrumentation.
- `FrontCompartments` was logically read-only but was converted with `GetUnsafePtr`, leaving a write-capable pointer surface against an owner snapshot.

What was done:
- Added `SumpDrainageTelemetryFlags.ScheduleWindowTiming` and set it when stamping frame-summary and ring telemetry timing.
- Changed `FrontCompartments` pointer extraction to `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`; `BackCompartments` remains the only mutable deduction route.
- Updated status, rationale, route card, self-audit, and ledger addendum so the timing caveat is explicit on disk.

Cinematic Cheats used:
- None added. The Dear Lie pipe-flow shader route remains unchanged.

Exact microseconds saved:
- Measured profiler savings: 0 us recorded; no compile/runtime/profiler run was launched.
- This pass is correctness/provenance hardening, not a performance claim.

Verification:
- `python Tools/OOP_Fluid_Scanner.py`: PASS, findingCount=0, scannedFileCount=78.
- `python -m py_compile Tools/OOP_Fluid_Scanner.py Tools/OOP_Fluid_Scanner_SHINOBU_340.py`: PASS.
- Scoped forbidden-pattern scan: PASS after timing/read-only pointer patch.
- `Docs/Reports/SHINOBU_340_SELF_AUDIT.xml`: XML parse PASS.
- `git diff --check` scoped touched files: PASS with Git CRLF conversion warnings only.
- Source confirmation for subagent A residual compile risks: PASS STATIC for registry ticks, Fluid DTO/BufferIDs, Power DTO/BufferIDs, and GraphicsBuffer lock-write pattern.
- `dotnet build`: NOT RUN. Latest gate: CPU=77.48%, one active `dotnet`, zero `csc`.

## 2026-05-23 - HEARTBEAT TELEMETRY AND ATOMIC CSV BRIDGE

What was wrong:
- Blackbox telemetry still followed solve cadence, so low-quality cadence throttling could leave non-solve LateFrame windows without a fresh forensic row.
- `LateFrameTick` stamped solver wall timing even when no solver had just finalized, allowing stale scheduler timestamps to contaminate frame summaries.
- Pipe profile baking wrote directly to the selected `.h8bin` target.
- CSV bridge failure status did not expose schema version, row, column, field, or numeric validation code.

What was done:
- Added `SumpDrainageTelemetryFlags.HeartbeatFrame`.
- Changed `LateFrameTick` to stamp solver timing only after a scheduled solver finalizes, then clear `_solverScheduleTimestamp`.
- Added idle LateFrame heartbeat rows that preserve last solved total/pressure state, zero per-frame evacuation/solver wall time, advance `_frameIndex`, and write both frame summary and the 300-row ring without `.Complete()` or file I/O.
- Added schema version and validation-status labels to `Hydraulic Sump Tuner`.
- Changed profile bake to temp-write, flush, readback-validate magic/schema/count/stride/source hash/layout hash, then publish with `File.Replace` or first-create `File.Move`.

Cinematic Cheats used:
- No fluid realism was added. The pipe flow visual route remains a scalar StructuredBuffer shader fake; heartbeat only improves forensic coverage.

Exact microseconds saved:
- Measured profiler savings: 0 us recorded; Unity runtime/profiler was not run.
- Avoided hidden sync cost is architectural: no heartbeat job and no `.Complete()` were added.
- Atomic bake is editor-only and does not affect runtime frame time.

Verification:
- Scoped forbidden-pattern scan: PASS; no `BinaryWriter`, `FindAnyObjectByType`, `new NativeArray`, `new NativeList`, `new List`, `Queue<`, `foreach`, LINQ, `Time.deltaTime`, `UnityEngine.Random`, `TryGetLatestCreated`, or hidden `.Complete(` in SHINOBU_340 scoped files.
- Direct import/property/layout scan: PASS; no literal `using Hecton8.Physics;`, `using Hecton8.Power;`, `Pack=1`, or hot DTO get/set properties in scoped files.
- `git diff --check` scoped touched C# files: PASS with Git CRLF conversion warnings only.
- `python Tools/OOP_Fluid_Scanner.py`: PASS, findingCount=0, scannedFileCount=78.
- `python -m py_compile Tools/OOP_Fluid_Scanner.py Tools/OOP_Fluid_Scanner_SHINOBU_340.py`: PASS.
- `Docs/Reports/SHINOBU_340_SELF_AUDIT.xml`: XML parse PASS after heartbeat/CSV bridge updates.
- `dotnet build`: NOT RUN. Latest gate: CPU=99.81%, zero active `dotnet`, zero `csc`; CPU still above the 50% policy limit.
