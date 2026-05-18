# LOG_SHINOBU_20

## 2026-05-17 - Hull Integrity DTO Deformation Pass

What was wrong:
- Structural destruction lacked an isolated SHINOBU_20-owned SIP ledger and GPU dent DTO path.
- The expensive failure mode was prefab swapping or CPU mesh deformation during damage spikes.
- Runtime/editor files initially sat outside asmdefs, which would have hidden references to non-auto-referenced Core/Memory/contracts assemblies.

What was done:
- Added raw 32B `HullDentDTO`, 16B `BaseIntegrityLedgerDTO`, raw-field `BaseModuleStateDTO`, local blind mocks, Burst SIP aggregation, hydrostatic pressure, repair, submarine crush, and black-box telemetry jobs.
- Added `HullIntegrityRuntime` with DataVault buffers, boot-only MemClear, 512-entry dent ring semantics, dirty-only double `GraphicsBuffer` upload, hardware-tier dent caps, flood/acoustic/deformation SignalBus outputs, local-space dent storage, CSV SIP overrides, and fault dump to `Docs/AgentLogs/Dump_HULL_INTEGRITY.bin`.
- Added `Hull Integrity Tuner` EditorWindow and Scene View dent/normal gizmos.
- Extended UberNoir with `_HectonHullDentDTOBuffer`, `_HectonHullDentDTOParams`, shader vertex dent displacement, fallback legacy dent support, and dent-depth scar normal/albedo/smoothness/metallic blending.
- Added narrow runtime/editor asmdefs so this domain compiles without pulling sibling runtime assemblies.

Cinematic cheats used:
- Dear Lie: SIP and breach bits are the gameplay truth; crushed metal is shader displacement and normal blending from DTOs.
- Hydrostatic pressure is scalar math, not softbody or Rigidbody pressure simulation.
- Mock depth uses deterministic triangle-wave pressure proof, not scene physics or random object motion.
- Low/MX350 tier caps GPU dent loops to 16 entries; high tier can spend saved cycles on 512 visible dents.

Exact microseconds saved:
- Prefab swap removal: estimated 800-2500 us saved per heavy damage spike.
- CPU mesh/softbody removal: estimated 1000+ us saved versus mutating hull vertices on CPU.
- Dirty-only GPU upload: estimated 120-400 us saved during impact bursts versus per-frame array upload.
- Burst SIP aggregation over native buffers: estimated 40-70 us saved per 500-module pass versus managed Update summation.
- Hardware dent cap: up to 30x fewer vertex-loop dent iterations on MX350/critical health tier.
- Profiler proof remains blocked until the existing Unity instance/build wall is cleared.

Verification:
- Targeted runtime Roslyn compile: PASS, 0 errors, 2 serialized-field warnings.
- Targeted editor Roslyn compile: PASS, 0 errors.
- Unity batchmode: BLOCKED because another Unity instance has the project open.
- Full `Hecton8.Core.csproj`: BLOCKED outside SHINOBU_20 by `GlobalWorldSampler`, `BinaryLayoutManifest`, and `EcosystemRuntimeInstaller` errors.
- Static forbidden-pattern scan: PASS for no damaged prefab swaps, MeshCollider, Instantiate, GameObject allocation, managed hot containers, Debug.Log, scene find, coroutines, or Update/LateUpdate/FixedUpdate in the SHINOBU_20 surface.

## 2026-05-17 - Ultra Polish Forensic Pass

What was wrong:
- Several DTOs were byte-size-correct but not audit-grade: sequential layout made ARM64 field offsets implicit, and `BaseModuleStateDTO` placed byte fields before later 4-byte fields.
- Submarine crush read `ledger[0]` on the main thread before the scheduled pressure job completed. That is a job-safety defect and could use stale pressure.
- Fatal telemetry dump used a temporary payload byte array. Fault handling should not allocate a full telemetry copy.
- Editor tuner slider callback used a lambda. Editor-only, but still unnecessary.

What was done:
- Converted `HullDentDTO`, `BaseIntegrityLedgerDTO`, `BaseModuleStateDTO`, mocks, tuning DTO, and `HabitatModuleDeformationSample` to explicit offset layouts with 8-byte-multiple sizes.
- Moved submarine pressure read into `HullIntegritySubmarineCrushDentJob` via a read-only ledger dependency, preserving the job chain.
- Wired `submarineRoot` into dent/AUP root fallback and removed the unused-root warning path.
- Changed black-box dump to write both `Dump_HULL_INTEGRITY.bin` and `Dump_HULL_INTEGRITY.h8dump` using chunked writes through the existing cold CSV/dump buffer.
- Moved arena scratch proof into a Burst job over allocator-provided native memory.
- Replaced the UI Toolkit slider lambda with a method callback.

Cinematic cheats used:
- The Dear Lie remains: SIP scalars and breach bits are gameplay truth; metal crush is GPU displacement and scar shading.
- Hydrostatic crush stays `density * gravity * depth * gradient`; no softbody, mesh collider, or Rigidbody pressure simulation.
- Low tier still caps visible dents to 16; high tier can spend the saved frame time on 512 shader dents.

Exact microseconds saved:
- Polish pass: 0 claimed runtime us; this pass removed correctness and audit risk.
- Existing static estimates remain unchanged: 800-2500 us saved per heavy damage spike by avoiding prefab swaps; 1000+ us versus CPU mesh deformation; 120-400 us by dirty-only GPU upload; up to 30x fewer dent-loop iterations on MX350.
- Profiler and GCMonitor proof remain not run.

Forensic evidence:
- Struct layout: `HullDentDTO` 32B offsets 0 Position, 12 Radius, 16 Normal, 28 Depth. `BaseModuleStateDTO` 64B offsets 0 NodeId, 4 ModuleHash, 8 LocalCenter, 20 LocalNormal, 32 BaseSIP, 36 CurrentSIP, 40 ReinforcementMultiplier, 44 DepthMeters, 48 BreachFrame, 52 Stress01, 56 PeakStress01, 60 Reserved0, 62 Flags, 63 ModuleKind.
- H-Phi: persistent arrays are DataVault buffers through `BufferID.HullIntegrity*`; no private `NativeArray` fields in the runtime.
- Blackbox: 300-frame telemetry ring is vault-backed and dumps `.bin` plus `.h8dump` on non-finite state.
- Dependency guard: runtime asmdef references Core/Memory/contracts only; external domains are SignalBus lanes or mocks.
- Compile guard: Unity Roslyn `Hecton8.Habitat.Deformation.Contracts.rsp` exit 0; runtime temp rsp exit 0 after replacing missing external `Hecton8.Core.ref.dll` with current `Library/ScriptAssemblies/Hecton8.Core.dll`; `Hecton8.Habitat.Deformation.Editor.rsp` exit 0.

## 2026-05-18 - Loop 7 Hot-Path Sovereignty Polish

What was wrong:
- `Tick()` could retry initialization and indirectly touch `GlobalRegistry` if initialization had failed.
- Quality-tier resolution still had a per-frame `GlobalRegistry.ScalabilityTierProfileByte` path.
- CSV hot-reload used synchronous File I/O from `ColdTick()` without an Editor/Development build gate.
- Dent-cap restoration had no hysteresis, so warning/critical recovery could flip GPU dent caps and force uploads during health-signal bounce.

What was done:
- `Tick()` now exits when uninitialized; retry initialization moved to `ColdTick()`.
- Hardware profile changes now drain typed `ScalabilityChangedEvent` snapshots, and health pressure drains `SystemHealthIndexSignal`.
- Dent caps apply immediate downgrade for warning/critical pressure and require 2.5 seconds before upgrading back to higher caps.
- CSV profile polling is compiled only for `UNITY_EDITOR || DEVELOPMENT_BUILD`; release builds keep the fixed parser code out of the disk-poll path.
- Re-ran targeted Contracts/Runtime/Editor Unity Roslyn asmdef chain; all exit 0.

Cinematic cheats used:
- The Dear Lie remains unchanged: gameplay damage is SIP scalar and breach bits; visible crush is GPU `HullDentDTO` displacement and scar normal blending.
- Warning state spends less shader loop budget by capping visible dents to 64; low/MX350 and critical state stay at 16.
- High/Ultra restoration is delayed, so saved cycles buy stable visual overkill instead of oscillating uploads.

Exact microseconds saved:
- 0 measured profiler microseconds claimed in this pass.
- Static risk removed: no `GlobalRegistry`, File I/O, byte allocation, LINQ, foreach, string formatting, `ToString`, scene find, Instantiate, `Material.SetFloat`, or `SetData` appears inside `Tick()`.
- Previous estimates remain unproven by profiler: 800-2500 us per heavy damage spike from no prefab swaps; 1000+ us versus CPU mesh deformation; 120-400 us from dirty-only GPU upload; up to 30x fewer dent iterations on low tier.

Forensic evidence:
- Struct layout unchanged and still explicit: `HullDentDTO` 32B offsets 0/12/16/28; `BaseIntegrityLedgerDTO` 16B offsets 0/4/8/12; `BaseModuleStateDTO` 64B offsets 0,4,8,20,32,36,40,44,48,52,56,60,62,63.
- H-Phi unchanged: persistent arrays remain `GlobalDataVault` buffers via `BufferID.HullIntegrity*`; no private `NativeArray` fields in `HullIntegrityRuntime`.
- Blackbox unchanged: 300-frame vault telemetry ring dumps `.bin` and `.h8dump` on non-finite state.
- Compile guard: Contracts csc exit 0; Runtime csc exit 0 with temp rsp replacing missing external `Hecton8.Core.ref.dll`; Editor csc exit 0.
- External wall remains: Unity Play Mode, shader import, GCMonitor, and profiler proof are not claimed.

## 2026-05-18 - Loop 8 L1 Layout And NaN Vaccination Pass

What was wrong:
- SHINOBU_20 runtime/contract DTOs still used `Pack = 4` on explicit layouts. The offsets were explicit, but the mandate rejects pack pragmas as runtime layout shortcuts.
- Several safety paths still trusted `math.max(NaN, x)` before writing pressure, dent, SIP, telemetry, or shader DTO parameters.
- The first post-edit targeted compile failed on a local missing-braces defect in `ResolveMaxDentDepth`.

What was done:
- Removed every `Pack = 4` pragma from SHINOBU_20 explicit DTO layouts while keeping fixed `Size=N` and `FieldOffset` maps.
- Added finite guards before pressure ratio publication, max-pressure tracking, telemetry ring writes, damage SIP deduction, peak stress updates, repair dent mutation, submarine crush dent generation, max dent depth scan, and CSV SIP override.
- Fixed the local compile defect and reran the targeted Contracts/Runtime/Editor asmdef csc chain to exit 0.

Cinematic cheats used:
- The Dear Lie remains intact: scalar SIP and breach bits are gameplay truth; GPU `HullDentDTO` displacement and scar normals carry the crushed metal illusion.
- Low tier still avoids per-dent shader loops beyond the capped visible set; high/ultra can use 512 finite-sanitized dents for visual overkill.

Exact microseconds saved:
- 0 measured profiler microseconds claimed in this pass.
- Static risk removed: no SHINOBU_20 DTO pack pragmas; no known pressure/dent telemetry path relies on NaN behavior before vault/GPU/signal writes.
- Previous unprofiled estimates remain unchanged: 800-2500 us per heavy damage spike from no prefab swaps; 1000+ us versus CPU mesh deformation; 120-400 us from dirty-only GPU upload; up to 30x fewer dent iterations on low tier.

Forensic evidence:
- Struct layout: `HullDentDTO` 32B offsets 0 Position, 12 Radius, 16 Normal, 28 Depth. `BaseIntegrityLedgerDTO` 16B offsets 0 BaseHash, 4 TotalSIP, 8 DepthPressure, 12 BreachedNodeCount. `BaseModuleStateDTO` 64B offsets 0,4,8,20,32,36,40,44,48,52,56,60,62,63. No SHINOBU_20 `Pack =` hits remain.
- H-Phi: persistent arrays remain DataVault buffers through `BufferID.HullIntegrity*`; no private `NativeArray` fields in runtime.
- Blackbox: 300-frame telemetry ring writes sanitized values and fault flags, then dumps `.bin` and `.h8dump` on non-finite state.
- Compile guard: Contracts csc exit 0; Runtime csc exit 0 with temp rsp replacing missing external `Hecton8.Core.ref.dll`; Editor csc exit 0.
- External wall remains: Unity Play Mode, shader import, GCMonitor, profiler, Frame Debugger, and player build proof are not claimed.
