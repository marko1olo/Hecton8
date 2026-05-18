# Rationale_SHINOBU_20

Status: ULTRA POLISH LOOP 8 COMPLETE / EXTERNAL UNITY WALL  
Agent: SHINOBU_20  
Domain: HABITAT & VEHICLES / STRUCTURAL INTEGRITY

## Self-Audit XML

```xml
<SELF_AUDIT agent="SHINOBU_20">
  <target>Hull deformation and SIP integrity tracker for bases and submarines.</target>
  <expensive_truth_rejected>CPU softbody, damaged-prefab swapping, per-vertex CPU crush meshes.</expensive_truth_rejected>
  <dear_lie>Gameplay truth is SIP numbers and breach bits in native buffers; visual truth is HullDentDTO streamed once-dirty to GPU for shader displacement and scratched metal normals.</dear_lie>
  <hot_path_gc_policy>Zero managed allocations in Tick/LateFrameTick; native buffers come from DataVault/H8Memory; CSV and editor work stay cold.</hot_path_gc_policy>
  <scalability>Low=16 dents and cheap scalar scar; Medium=64 dents; High=256 dents; Ultra=512 dents with stronger normal/albedo response.</scalability>
  <verification_truth>dotnet/Unity/Profiler evidence required; docs-only and static scans remain PENDING VERIFICATION.</verification_truth>
</SELF_AUDIT>
```

## Decisions

### 2026-05-18 - L1 Layout Pragmas And NaN Vaccination

Problem: Static layout audit found the SHINOBU_20 DTOs still used `Pack = 4` in explicit runtime layouts. The field offsets and sizes were correct, but the current ARM64 mandate rejects pack pragmas as a shortcut. A second math audit found several `math.max(NaN, x)` surfaces before vault, telemetry, GPU parameter, and signal writes.

Solution: Removed `Pack = 4` from every SHINOBU_20 explicit runtime/contract DTO while keeping fixed `Size=N` and `FieldOffset` maps. Added finite guards before SIP deduction, peak stress update, repair radius/depth mutation, submarine crush dent extents/radius/depth, max dent depth scan, CSV SIP override, pressure ratio publication, max pressure tracking, and telemetry writes. The telemetry ring now writes sanitized fallback values while fault flags preserve the non-finite event.

Rejected Alternatives: Keeping `Pack = 4` was rejected because the data layout mandate now says explicit size and offsets, not pack pragmas. Allowing NaN to flow into `Shader.SetGlobalVector`, `FluidIncursionSignal`, or `AcousticPingSignal` was rejected because black-box dump after the fact is too late if the render/physics pipeline already consumed poisoned floats. A full project rebuild was rejected; targeted asmdef csc was sufficient and avoids compile-wall spam.

Scalability potential: Low/MX350 avoids NaN-induced fallback stalls and stays on 16 dents. Middle warning remains 64 dents. High/Ultra can still use 512 dents, but the DTO stream is now finite-sanitized before upload so visual overkill cannot inherit corrupted gameplay floats.

Hardware Impact: 0 measured profiler microseconds claimed. Static risk removed: no SHINOBU_20 runtime DTO uses Pack pragmas; no known SHINOBU_20 pressure/dent telemetry write path depends on `math.max(NaN, x)` returning a safe value.

### 2026-05-18 - Hot Registry Polling, Release CSV I/O, And Dent-Cap Hysteresis

Problem: The previous polish pass still allowed `Tick()` to retry initialization and resolve quality tier through `GlobalRegistry`, and `ColdTick()` could poll `integrity_profiles.csv` with synchronous File I/O outside the editor bridge. The dent-cap state also changed instantly on recovery, which can thrash GPU upload and shader loop counts when health signals oscillate.

Solution: `Tick()` now returns immediately if the runtime is not initialized; retry initialization moved to `ColdTick()`. Runtime quality changes drain typed `ScalabilityChangedEvent` and `SystemHealthIndexSignal` snapshots, not `GlobalRegistry`. Critical/warning downgrades apply immediately, while upgrades require 2.5 seconds of stable desired state. CSV hot-reload remains available only under `UNITY_EDITOR || DEVELOPMENT_BUILD`, preserving Task 19 for designers without release MicroSD polling.

Rejected Alternatives: Per-frame `GlobalRegistry.ScalabilityTierProfileByte` polling was rejected because it breaks the compile-wall/hot-path contract. Release CSV polling was rejected because synchronous `File.Exists`, `GetLastWriteTimeUtc`, and `FileStream` can create Steam Deck MicroSD stalls. Instant high-tier restoration was rejected because it can flip the shader dent loop between 16/64/512 during transient health pressure.

Scalability potential: Low stays at 16 dents and never upgrades above low when the hardware profile is MX350. Middle warning state holds 64 dents for pressure relief. High/Ultra can return to 512 dents only after the system remains stable, buying scar overkill without oscillation.

Hardware Impact: 0 measured profiler microseconds claimed. Static gain is risk removal: no `GlobalRegistry` or File I/O appears inside `Tick()`, release builds do not poll CSV, and dent-cap upgrades avoid repeated forced GPU uploads during health bounce.

### 2026-05-17 - Ultra Polish Structural Layout And Job Fence Audit

Problem: The first pass satisfied behavior but left audit risk: several runtime DTOs used sequential layout, `BaseModuleStateDTO` placed byte fields before later 4-byte fields, submarine crush read `ledger[0]` on the main thread while prior jobs were scheduled to write it, and the black-box dump allocated a payload array on fatal state.

Solution: Converted runtime DTOs and `HabitatModuleDeformationSample` to explicit offset layouts with 8-byte-multiple sizes; moved submarine pressure read into `HullIntegritySubmarineCrushDentJob` through a `[ReadOnly]` ledger dependency; added `submarineRoot` as the local-space fallback; changed fatal telemetry dump to write `.bin` and `.h8dump` in chunks through the existing cold byte buffer; replaced the editor slider closure with a method group; proved arena scratch through a Burst job over allocator-provided native memory.

Rejected Alternatives: `[StructLayout(Pack=1)]` was rejected because it is forbidden for runtime memory and punishes ARM64. Main-thread `ledger[0]` read was rejected because it can violate job safety and use stale pressure. Allocating `new byte[telemetryBytes]` in dump path was rejected because crash reporting should not add avoidable pressure during a fault. Full project rebuild spam was rejected after the asmdef chain passed and the Bee artifact for external `Hecton8.Core.ref.dll` was absent.

Scalability potential: Low/MX350 keeps scalar SIP truth, 16 dents, explicit 32B DTO reads, and no CPU mesh deformation. Middle can use 64 active dents from health warning paths. High/Ultra can upload 512 dents and spend saved CPU on shader scar overkill without changing gameplay truth.

Hardware Impact: i3/MX350 expected gain remains estimated, not profiled: 800-2500 us per heavy damage spike from avoiding prefab swaps, 1000+ us versus CPU mesh deformation, 120-400 us from dirty-only GPU upload, and up to 30x fewer shader dent loop iterations on low tier. The polish pass itself claims 0 runtime microseconds; it reduces ARM64/job-safety/dump-allocation risk.

## Ultra Polish Forensic Self-Audit

```xml
<SELF_AUDIT agent="SHINOBU_20" phase="ultra_polish">
  <task_01 status="PASS">Emergency mock integrity exists when binary SIP data is absent.</task_01>
  <task_02 status="PASS">No damaged-prefab replacement path; shader dent DTO is the visual lie.</task_02>
  <task_03 status="PASS">BaseModuleStateDTO exposes raw fields and AsRef helper; no DTO properties.</task_03>
  <task_04 status="PASS">BaseIntegrityLedgerDTO explicit 16B layout.</task_04>
  <task_05 status="PASS">Mock WFC, combat, depth, breach, and repair signals keep temporal blindness.</task_05>
  <task_06 status="PASS">Burst SIP aggregation mutates vault module data.</task_06>
  <task_07 status="PASS">Hydrostatic scalar pressure sets breach bits deterministically.</task_07>
  <task_08 status="PASS">HullDentDTO vault ring uploads dirty-only to double GraphicsBuffer.</task_08>
  <task_09 status="PASS">UberNoir consumes StructuredBuffer dents for deformation/scars.</task_09>
  <task_10 status="PASS">Flood/compromise outputs use typed SignalBus lanes.</task_10>
  <task_11 status="PASS">Repair job reduces dent depth, no prefab restoration.</task_11>
  <task_12 status="PASS">Submarine crush shares dent DTO path and reads pressure inside the dependent job.</task_12>
  <task_13 status="PASS">Low-tier dent cap is 16; high tier can render 512.</task_13>
  <task_14 status="PASS">Acoustic groan is pressure-ratio signal, not AudioSource churn.</task_14>
  <task_15 status="PASS">Dents stay local; AUP conversion is only used for outbound signals.</task_15>
  <task_16 status="PASS">Vault buffers use boot MemClear; runtime arrays are from DataVault.</task_16>
  <task_17 status="PASS">300-frame telemetry ring dumps .bin and .h8dump on non-finite state.</task_17>
  <task_18 status="PASS">Editor facade writes unmanaged tuning block in Play Mode.</task_18>
  <task_19 status="PASS">Cold CSV parser uses a fixed byte buffer and no Split/Dictionary.</task_19>
  <task_20 status="PASS">SceneView gizmo visualizes dent DTOs editor-only.</task_20>
  <arm64_layout>All SHINOBU_20 DTOs use LayoutKind.Explicit with fixed Size=N and no Pack pragmas. HullDentDTO 32B offsets 0/12/16/28; BaseLedger 16B offsets 0/4/8/12; BaseModule 64B offsets 0,4,8,20,32,36,40,44,48,52,56,60,62,63; MockCombat 64B offsets 0,12,16,28,32,36,40,44,48,52,56,60; Telemetry 64B explicit; HabitatModuleDeformationSample 32B explicit.</arm64_layout>
  <zero_gc_tick>Tick/LateFrameTick use spans, SignalBus snapshots, vault NativeArrays, jobs, and GraphicsBuffer LockBufferForWrite. Tick does not call GlobalRegistry, File I/O, LINQ, foreach, string formatting, ToString, boxing closure, Instantiate, GameObject allocation, GetComponent, scene find, Material.SetFloat, or SetData. The only byte array is the documented cold CSV/dump buffer; CSV polling is Editor/Development only.</zero_gc_tick>
  <aup>Damage dents are local float3; outbound fluid/acoustic AUP is built after local-to-runtime conversion, avoiding direct absolute double-to-float truncation in dent math.</aup>
  <dear_lie>Hydrostatic destruction is scalar SIP pressure and a weakest-node bit; crushed metal is deferred to GPU shader displacement and normal/albedo scars.</dear_lie>
  <dependency>Runtime asmdef references Core/Memory/contracts only; fluid/audio/combat/logistics communication is through typed SignalBus or local mocks.</dependency>
  <h_phi>Persistent native arrays are vault buffers via BufferID.HullIntegrity*; no private NativeArray fields in HullIntegrityRuntime.</h_phi>
  <blackbox>300-entry HullIntegrityTelemetryEntry ring is vault-backed and dumps on NaN/non-finite state.</blackbox>
  <compile_guard>Contracts/Runtime/Editor Unity Roslyn asmdef chain exits 0. Full Unity/project/profiler proof remains externally blocked and is not claimed.</compile_guard>
</SELF_AUDIT>
```

## Earlier Decisions

### 2026-05-17 - Initial Domain Gate

Problem: Structural integrity needs base and submarine damage without hard dependency on unfinished combat/WFC agents.

Solution: Use typed existing signal lanes where present, define mock combat/WFC/depth DTOs for temporal blindness, keep persistent state in GlobalDataVault buffers.

Rejected Alternatives: Direct calls into combat, WFC, fluid, audio, or prefab controllers. Standard Unity damaged-prefab replacement is slow, creates asset coupling, and violates the prompt.

Scalability potential: Low uses minimal math and 16 GPU dents; Middle expands active dents; High/Ultra spend saved CPU on more GPU dent response and scar shading.

Hardware Impact: Expected low-end i3/MX350 gain comes from replacing CPU mesh/prefab churn with native SIP aggregation and dirty double-buffered GraphicsBuffer upload. Exact microseconds remain PENDING VERIFICATION.

### 2026-05-17 - Mandate Selection

Problem: Damage, GPU upload, Burst jobs, and shader deformation cross multiple failure surfaces.

Solution: Bound implementation to mandates for hull damage feedback, cinematic cheat first, zero GC, native memory/job system, HectonArenaAllocator, MX350 GPU kernels, GPU sovereignty, and execution phases.

Rejected Alternatives: Single MonoBehaviour Update loop with managed arrays, direct shader Vector array uploads every frame, or Rigidbody-driven crush physics.

Scalability potential: Mandates force Math LOD and visual currency: cheap integrity truth on toaster hardware, richer shader dent blending on top-tier devices.

Hardware Impact: Expected savings are from no prefab swap, no CPU softbody, no managed damage collections, and dirty-only GPU writes. Exact microseconds remain PENDING VERIFICATION.

### 2026-05-17 - Tasks 01-05 Emergency Integrity Kernel

Problem: The required legacy binary SIP layout was absent from live StreamingAssets, and archive evidence only confirmed prior habitat scalar pressure/flood black-box patterns.

Solution: Implement GenerateEmergencyMockIntegrity with raw unmanaged module slots: glass-like modules at 10 SIP, titanium corridors at 100 SIP, reinforced bulkheads at 150 SIP * 1.45. MockDepthSignal uses deterministic triangle-wave depth, not Unity Random.

Rejected Alternatives: Waiting for WFC/Submarine/Combat agents; damaged prefab swaps; managed dictionaries of module health; Unity Random depth fuzz.

Scalability potential: Low tier gets the same scalar SIP truth with fewer GPU dents. High/Ultra can spend saved CPU on 512 visible dents and scarred metal normal blending.

Hardware Impact: Expected low-end gain is 800-2500 us during damage spikes by removing prefab swaps and CPU mesh deformation. Exact profiler proof remains PENDING VERIFICATION.

### 2026-05-17 - Core Runtime and Shader DTO Path

Problem: Existing legacy hull dent path is a 16-slot float4 shader array owned by vehicle VFX; replacing it would break repair and cockpit consumers.

Solution: Add a parallel SHINOBU_20 HullDentDTO path: 32-byte DTO, vault ring semantics, double GraphicsBuffer with LockBufferForWrite, global StructuredBuffer, and UberNoir fallback to the legacy float4 array when DTOs are inactive.

Rejected Alternatives: Mutating BufferID.HullDents from float4 to HullDentDTO; uploading Vector arrays every frame; CPU softbody/mesh writes; direct references to fluid/audio/logistics systems.

Scalability potential: Low/MX350 and critical health cap active DTOs to 16; warning state caps to 64; high tier can upload 512 and buy richer normal/albedo damage.

Hardware Impact: Low-end saves vertex loop work by up to 30x. Dirty-only upload prevents PCIe waste. Exact microseconds remain PENDING VERIFICATION.

### 2026-05-17 - Compile Gate 1

Problem: `dotnet build Hecton8.Core.csproj --no-restore` could not start because `Temp/obj/Hecton8.Core/project.assets.json` was missing.

Solution: Ran `dotnet restore Hecton8.Core.csproj`, then repeated build.

Rejected Alternatives: Reporting compile status without restoring the missing generated assets file; modifying unrelated project files.

Scalability potential: None. This is verification hygiene.

Hardware Impact: None. Build is blocked outside SHINOBU_20 by missing `MockNarrativeTriggerSignal` and `ShinobuLogisticsRouter`.

### 2026-05-17 - Assembly Isolation Correction

Problem: `Assembly-CSharp.csproj` did not include the new hull integrity runtime/editor files, and those files would not legally see `Hecton8.Core.Memory` or the deformation contracts because project asmdefs use `autoReferenced:false`.

Solution: Added narrow `Hecton8.Habitat.Deformation.asmdef` and `Hecton8.Habitat.Deformation.Editor.asmdef`. Runtime references only bootstrap/core/core contracts/core memory/deformation contracts plus Unity Burst/Collections/Jobs/Mathematics/Profiling. Editor references only the runtime assembly and Mathematics.

Rejected Alternatives: Leaving the files in predefined Assembly-CSharp was rejected because it hides missing references until Unity import. Adding direct references to sibling gameplay/audio/fluid/logistics runtime assemblies was rejected because the mandate requires contracts and GlobalRegistry/SignalBus decoupling.

Scalability potential: No frame cost. The compile graph stays bounded so future low/high tier code can consume the DTO path without pulling sibling domains into the runtime assembly.

Hardware Impact: None at runtime. Build/import impact is lower than dragging sibling runtime assemblies into the deformation surface.

### 2026-05-17 - Targeted Verification

Problem: Unity batchmode cannot open the project while another Unity instance owns `C:\hades\Hecton8`, and full `Hecton8.Core.csproj` builds are currently blocked by unrelated moving dependencies.

Solution: Built targeted validation assemblies through Roslyn using Unity generated references and `Library/ScriptAssemblies`: `SHINOBU_20_RuntimeValidation.dll` and `SHINOBU_20_EditorValidation.dll`. Runtime compile passed with only serialized-field warnings; editor compile passed with zero diagnostics.

Rejected Alternatives: Claiming a green Unity compile was rejected. Modifying unrelated `GlobalWorldSampler`, `BinaryLayoutManifest`, or ecosystem installer files was rejected because those are outside the hull integrity domain.

Scalability potential: Validation confirms the deformation code can compile in isolation; true frame-time proof still requires Unity Play Mode and profiler once the external build wall is cleared.

Hardware Impact: No measured profiler numbers yet. Static estimates remain: 800-2500 us saved on damage spikes by avoiding prefab swaps, 1000+ us saved versus CPU mesh deformation, and up to 30x lower shader dent-loop work on MX350 via the 16-entry cap.

## Polish Self-Audit

```xml
<SELF_AUDIT agent="SHINOBU_20" phase="polish">
  <damaged_prefabs_or_mesh_colliders>false; static scan found no Damaged prefab terms, MeshCollider, Instantiate, or new GameObject in the SHINOBU_20 surface.</damaged_prefabs_or_mesh_colliders>
  <hull_dent_dto_layout>32 bytes: float3 Position at 0..11, float Radius at 12..15, float3 Normal at 16..27, float Depth at 28..31; LayoutKind.Explicit, Size=32, no Pack pragma, runtime UnsafeUtility.SizeOf gate.</hull_dent_dto_layout>
  <cs1612_policy>BaseModuleStateDTO uses raw fields and an UnsafeUtility.AsRef helper; no get/set properties exist on array DTO structs.</cs1612_policy>
  <dependency_mocking>MockWFCBaseArray, MockCombatDamageSignal, partial MockDepthSignal, MockRepairLaserSignal, and MockHullBreachSignal are local; real coupling uses SignalBus and GlobalRegistry.</dependency_mocking>
  <editor_facade>Hull Integrity Tuner exists as a UI Toolkit EditorWindow with Play Mode vault read/write sliders and Scene View dent gizmos.</editor_facade>
  <verdict>PASS_WITH_EXTERNAL_BUILD_WALL</verdict>
</SELF_AUDIT>
```
