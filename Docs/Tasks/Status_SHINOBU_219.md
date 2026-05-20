# SHINOBU_219 Status

Agent: SHINOBU_219
Domain: VISUAL_PRESSURE_AGING_SHADER
Batch Prompt Source: Docs/Tasks/CURRENT_BATCH.md
Task Count: 20
Status Hygiene: fresh file created; no stale prior batch data detected.

## Mandates Read Before Coding

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_GPU_Sovereignty.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Iteration Loop 0 - Intake

- [x] Prompt extraction | DOD: CLI regex extracted only `<AGENT_PROMPT id="SHINOBU_219">` from cover to cover. | Rejected: neighboring prompt memory bleed. | Estimate: 450 us
- [x] Domain boundary read | DOD: `Docs/Actual Domains of Project.txt` read; work scoped to Echelon 8 presentation/rendering. | Rejected: gameplay truth edits outside renderer facade. | Estimate: 320 us
- [x] Mandate selection | DOD: 8 task-relevant mandates read before code. | Rejected: coding from prompt alone. | Estimate: 880 us

## Task Checklist

- [x] Task 01 MATERIAL_MUTATION_INQUISITION | DOD: `rg` scan found no local `BaseCorrosion.cs`/`GlassFracture.cs`; added `Visual_Aging_Inquisition` report path for `.material`/MPB regression detection. | Rejected: per-renderer material mutation and MPB aging. | Estimate: 650 us scan + 0 us hot path
- [x] Task 02 DYNAMIC_DECAL_CORROSION_PURGE | DOD: `BaseDegradationSystem` no longer stores corrosion/crack decal state or activates authoring aging decals; UberNoir owns rust/crack visuals. | Rejected: hidden/deactivated decal children. | Estimate: 12-80 us/event saved, PENDING PROFILER
- [x] Task 03 CS1612_METADATA_STATE_ANNIHILATION | DOD: `VisualAgingParamsDTO`, tuning/runtime/telemetry structs are raw unmanaged fields, no properties. | Rejected: sequential DTO properties. | Estimate: 0.010-0.026 us/entry Burst generation, PENDING PROFILER
- [x] Task 04 ARM64_AGING_LAYOUT_VALIDATION | DOD: `ValidateLayout()` checks 64-byte DTO sizes and VisualAging lane offsets 0/16/32/48. | Rejected: implicit C# sequential packing. | Estimate: cold editor check only
- [x] Task 05 EMERGENCY_MOCK_AGING_DATA | DOD: `GenerateMockAgingDataJob` writes deterministic high-depth/high-stress aging payload into Vault buffer. | Rejected: managed scene test arrays/spawned fake modules. | Estimate: 512-entry low/mock path ~5.1-13.3 us static
- [x] Task 06 BURST_AGING_PARAMETER_KERNEL | DOD: `ProcessAgingParametersJob` Burst `IJobParallelFor` reads `IntegrityStateDTO`/node AUP/temperature and writes Vault `VisualAgingParamsDTO`. | Rejected: BaseModule polling/render component state. | Estimate: 0.010-0.026 us/entry static
- [x] Task 07 THE_DEAR_LIE_SHADER_INTEGRATION | DOD: UberNoir reads `_GlobalBaseAgingParams` by instance/material index and blends rust/salt/algae. | Rejected: CPU knowing rust placement. | Estimate: 0 extra draw calls
- [x] Task 08 SPATIAL_GROWTH_PROPAGATION | DOD: `H8UberNoirAgingGrowthMask` uses localized AUP plus procedural growth masks for weld/corner spread. | Rejected: uniform blink-on rust. | Estimate: low path cheap triangle; high path 2 noise samples/pixel
- [x] Task 09 GLASS_MICRO_FRACTURE_SIMULATION | DOD: `H8UberNoirApplyGlassMicroFracture` blends dithered cracks from stress/glass mask. | Rejected: crack meshes/decal projectors. | Estimate: 0 CPU geometry cost
- [x] Task 10 ASYNCHRONOUS_GPU_BUFFER_UPLOAD | DOD: VisualSync double-buffered `GraphicsBuffer.LockBufferForWrite` + `UnsafeUtility.MemCpy` uploads DTOs. | Rejected: `SetData`/per-renderer mutations. | Estimate: 32-256 KB/frame payload, PENDING PROFILER
- [x] Task 11 CONTINUOUS_SCALABILITY_NOISE_OCTAVES | DOD: `HomeostasisBrain.GlobalQualityWeight` controls active count and shader noise blend weights. | Rejected: hardware binary switches. | Estimate: 16-256 telemetry sample budget; shader ALU scales continuously
- [x] Task 12 TEMPERATURE_CORROSION_BOOST | DOD: optional read of `ThermodynamicsTemperatureFrontMirror`; mock temperature fallback preserves isolation. | Rejected: owning thermodynamics state. | Estimate: <1 us/512 entries static
- [x] Task 13 AUP_PRECISION_IGNORE_AND_LOCALIZE | DOD: CPU subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble`, clamps local 8192 m window, passes float3 only. | Rejected: absolute GPU AUP. | Estimate: double subtract/clamp/cast per entry
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: no `VisualAging*` references in Networking/Save Merkle scan; render buffers `71240-71246` only. | Rejected: hashing visual presentation. | Estimate: saves up to 256 KB/snapshot at full payload
- [x] Task 15 TELEMETRY_AGING_RECORDER | DOD: 300-entry telemetry ring records average stress, max depth proxy, glass fracture count, CPU estimate, upload us, hashes; fault dump path set. | Rejected: string logs/hot allocations. | Estimate: bounded 16-256 samples/frame
- [x] Task 16 AGING_TUNER_EDITOR_WINDOW | DOD: UI Toolkit `Abyssal Base Aging Tuner` sliders write Vault tuning DTOs; editor graph reads runtime counters without hot-path renderer mutation. | Rejected: inspector-only serialized fields and material sliders. | Estimate: cold editor only; 0 us hot path
- [x] Task 17 CSV_AGING_PROFILES_INGESTOR | DOD: `environmental_aging_rules.csv` cold-loads into `NativeArray<byte>` scratch, parses byte tokens/FNV-1a hashes, mutates tuning DTO without managed token strings. | Rejected: `string.Split`, LINQ CSV parsing, ScriptableObject recompiles. | Estimate: cold 4096-byte cap, 0 us hot path
- [x] Task 18 LIVE_AGING_PREVIEW_GIZMO | DOD: Scene overlay and `VisualPressureAgingGizmoVisualizer.OnDrawGizmos` read Vault aging DTOs and draw color-coded rings. | Rejected: runtime helper GameObjects or full graphics-pipeline dependency. | Estimate: editor/gizmo only, 128-ring cap
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: `Visual_Aging_Inquisition` writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with project counts and aging-scope pass/fail: "Instance Material Mutations Purged". | Rejected: chat-only proof. | Estimate: cold editor scan only
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static audit confirms 64-byte DTO lanes, BufferIDs `71240-71246`, `LockBufferForWrite`, asmdef DTO/editor package dependencies, no rollback references, and build gate status. | Rejected: claiming profiler/build proof not produced. | Estimate: 1200 us static scans + CPU gate sample

## Iteration Loop 1 - Tasks 1-5

- [x] Extracted prompt again after first task batch using CLI line extraction. | DOD: `CURRENT_BATCH.md` lines 1510-1574 reread. | Rejected: relying on compressed memory. | Estimate: 420 us
- [x] Static sanitation scan. | DOD: forbidden authoring decal mutation tokens absent from `BaseDegradationSystem`. | Rejected: visual hiding only. | Estimate: 480 us
- [!] Compile verification deferred. | Reason: `Get-Counter` reported CPU 100%; no `dotnet/csc/VBCSCompiler` process found, but build launch is forbidden above 50% CPU. | Integrator note: rerun build when CPU gate clears.

## Iteration Loop 2 - Tasks 6-10

- [x] Kernel to GPU path readback. | DOD: confirmed Burst jobs, `_GlobalBaseAgingParams`, and `LockBufferForWrite` references exist. | Rejected: direct render component dependencies. | Estimate: 510 us static scan
- [x] Compile gate checked. | DOD: no `dotnet/csc/VBCSCompiler`; CPU was 100%, build forbidden. | Rejected: violating CPU gate. | Estimate: 1000 ms counter sample

## Iteration Loop 3 - Tasks 11-15

- [x] Prompt re-extract after third-task cadence. | DOD: CLI line extraction reread SHINOBU_219 block. | Rejected: compressed-memory drift. | Estimate: 420 us
- [x] Rollback exclusion scan. | DOD: Networking/Save Merkle search found no visual aging DTO/buffer integration. | Rejected: adding presentation to rollback. | Estimate: 900 us
- [!] Compile verification deferred. | Reason: prior CPU gate still blocks build; retry pending after next CPU sample. | Integrator note: no build run yet.

## Iteration Loop 4 - Tasks 16-18

- [x] Prompt re-extract after third-task cadence. | DOD: CLI line extraction reread SHINOBU_219 block before closing editor/CSV/gizmo tasks. | Rejected: relying on compressed-memory task list. | Estimate: 420 us
- [x] Tuner/CSV/gizmo path verified. | DOD: `rg` confirmed tuner window, cold CSV path, and `OnDrawGizmos` entry points. | Rejected: runtime material UI controls. | Estimate: 620 us static scan
- [x] Architecture docs updated. | DOD: `CINEMATIC_CHEATS_LEDGER.md` now records shader-aging fake as static source route only. | Rejected: runtime-proof language without profiler. | Estimate: documentation-only

## Iteration Loop 5 - Tasks 19-20

- [x] Static inquisition surface verified. | DOD: `Visual_Aging_Inquisition` delegates to project-counting report generator; aging-scope pass/fail remains gated on `BaseDegradationSystem`/UberNoir route. | Rejected: whole-project `.material` noise as failure condition for unrelated systems. | Estimate: cold editor only
- [x] Self-audit static scans completed. | DOD: forbidden BaseDegradation aging decal tokens absent; shader buffer/function tokens present; runtime/editor asmdefs now expose required DTO and Unity package references; new Unity C# assets have stable `.meta`; rollback/save scan returned no VisualAging references. | Rejected: declaring Unity compile/profiler proof. | Estimate: 1200 us static scans
- [x] Final report appended. | DOD: `Docs/AgentLogs/LOG_SHINOBU_219.md` contains what was wrong, what was done, cinematic cheats, microsecond estimates, verification, and `<SELF_AUDIT>`. | Rejected: chat-only completion report. | Estimate: documentation-only
- [!] Compile verification deferred. | Reason: no `dotnet/csc/VBCSCompiler` process was active, but `Get-Counter` reported CPU 100%; project rule forbids build launch above 50% CPU. | Integrator note: run build when CPU gate clears.

## Iteration Loop 6 - Ultra Polish Mandate

[ANALYSIS]
Target: remove SHINOBU_219 compile-wall and Vault-handle debt introduced by direct structural Runtime consumption.
Affected systems: `VisualPressureAgingRuntime`, `Hecton8.Graphics.Materials.asmdef`, `Hecton8.Habitat.Deformation.Contracts`, `StructuralIntegrityCalculatorTypes`, Habitat Deformation editor asmdef.
Zero GC proof: no new hot-path managed collection; persistent visual Vault state now stores `VaultGenerationHandle<T>` descriptors only, resolving phase-local `NativeArray<T>` views.
State check: visual runtime has no private persistent `NativeArray`, `NativeList`, or `NativeHashMap`; own GraphicsBuffers remain cold GPU resources and are released on shutdown.
Rule quote: "New manager code must not persist `VaultBufferHandle<T>`, `NativeArray<T>`, `NativeSlice<T>`, or raw Vault pointers across frames."

- [x] Compile-wall route correction. | DOD: `Hecton8.Graphics.Materials.asmdef` now references `Hecton8.Habitat.Deformation.Contracts`, not `Hecton8.Habitat.Deformation`; structural DTO ABI moved to the contracts assembly so Vault type hash remains identical for owner and visual consumer. | Rejected: local mirror DTOs, because `GlobalDataVault` type hash includes `typeof(T).TypeHandle` and would fail under collection checks. | Estimate: runtime 0 us; compile-wall risk reduction only
- [x] VaultGenerationHandle migration for SHINOBU_219. | DOD: static scan found no `VaultBufferHandle<`, `.Resolve(`, `GetBufferHandle`, or `TryGetBuffer(` in `VisualPressureAgingRuntime.cs`; phase code resolves local `NativeArray<T>` views through `TryResolveHandle`. | Rejected: legacy pointer-bearing handle persistence. | Estimate: runtime metadata resolution cost only; pointer-staleness risk removed
- [x] External structural input read path hardened. | DOD: structural states, AUPs, tuning, and temperature mirror are acquired through local `VaultGenerationHandle<T>` descriptors and buffer locks, then downgraded to mock data if unavailable. | Rejected: fallback-only mock path, because Task 06 requires real Vault strength scalars when present. | Estimate: 0 extra allocations; optional lock/unlock only
- [x] Habitat editor direct contract reference patched. | DOD: `Hecton8.Habitat.Deformation.Editor.asmdef` now references Contracts directly after DTO relocation. | Rejected: relying on transitive runtime references that Unity asmdef compilation may not expose. | Estimate: editor compile safety only
- [x] Quality-scalar NaN vaccination. | DOD: `ResolveGlobalQualityWeight()` now falls back to `0.0f` before `math.saturate` when Homeostasis emits non-finite data. | Rejected: letting NaN propagate into active count, telemetry, and shader runtime vector. | Estimate: one finite check per phase access
- [x] Static guard rerun. | DOD: no direct Graphics asmdef reference to `Hecton8.Habitat.Deformation`, no SHINOBU_219 legacy Vault handles, no DTO properties, no `Pack=1`; `git diff --check` reports only CRLF normalization warnings. | Rejected: build launch under CPU 100%. | Estimate: static scan ~1400 us + CPU gate sample 1000 ms
- [!] Compile verification still deferred. | Reason: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100; no `dotnet/csc/VBCSCompiler` process was active. Project rule forbids build launch above 50% CPU. | Integrator note: run Unity import/build once CPU gate clears.

## Iteration Loop 7 - Vault Lock Hardening

[ANALYSIS]
Target: close remaining cold/editor Vault race windows after the descriptor migration.
Affected systems: `VisualPressureAgingRuntime.TryReadEditorTuning`, `WriteDefaults`, `ApplyPendingEditorTuningImmediate`, `MonitorCsv`, and `VisualSyncTick`.
Zero GC proof: no new managed hot-path allocations; changes add only `TryLockBuffer`/`TryUnlockBuffer` gates around existing method-local `NativeArray<T>` resolves.
State check: all SHINOBU_219 persistent state remains `VaultGenerationHandle<T>` descriptors plus GPU buffers; no private `NativeArray`, `NativeList`, or `NativeHashMap` added.

- [x] Editor tuning read fenced. | DOD: `TryReadEditorTuning` now locks `VisualPressureAgingTuning` and `VisualPressureAgingRuntime` before resolving and releases both in `finally`. | Rejected: editor facade reading runtime counters while `VisualSyncTick` writes them. | Estimate: cold editor only; 0 us player hot path
- [x] Default/tuning/CSV writes fenced. | DOD: default hydration locks tuning, mock temperature, and runtime; pending editor tuning locks tuning; CSV hot reload locks CSV scratch and tuning. | Rejected: unlocked cold writes because cold-path races can still poison the Vault ABI. | Estimate: cold/dev only; no shader ABI cost
- [x] VisualSync runtime counter write fenced. | DOD: `VisualSyncTick` locks `VisualPressureAgingRuntime` around runtime DTO write and releases in `finally`; GPU buffer upload route is unchanged. | Rejected: assuming editor reads never overlap render sync. | Estimate: one Vault lock pair per visual sync
- [x] Static guard rerun after lock patch. | DOD: no `VaultBufferHandle<`, `.Resolve(`, `GetBufferHandle`, `TryGetBuffer(`, `Pack=1`, DTO properties, hot LINQ/foreach/material mutation/decal tokens, `UnityEngine.Random`, `Time.deltaTime`, `Time.frameCount`, or private native collection allocations in SHINOBU_219 scoped files; direct Graphics-to-Habitat-Runtime asmdef scan clean; `git diff --check` returned exit 0 with CRLF warnings only. | Rejected: broad unrelated-project scans for this fenced change. | Estimate: static scan ~2400 us
- [!] Compile verification still deferred. | Reason: CPU gate sample returned `CpuPercent=100` and no active compiler process; project rule forbids launching build above 50% CPU. | Integrator note: run Unity import/build only after CPU gate clears.

## Iteration Loop 8 - Vault Acquisition Hot-Path Collapse

[ANALYSIS]
Target: remove repeated owner-lane `GetGenerationHandle` calls from every dispatcher phase.
Finding: `GlobalDataVault.GetGenerationHandle` calls `TryEnsureVaultBuffer`, which validates/grows and can sanitize payloads. SHINOBU_219 was invoking this route in PreSimulation, Simulation, VisualSync, editor read, and gizmo preparation.
DOD correction: valid generation descriptors now resolve through `TryResolveOrAcquire`; `GetGenerationHandle<T>` is only reached on cold miss, stale generation, or undersized lane.

- [x] Repeated Vault acquisition removed from normal phases. | DOD: `EnsureVaultState` now resolves current descriptors first, refreshes via `TryGetGenerationHandle<T>` when possible, and calls `GetGenerationHandle<T>` only through one helper fallback. | Rejected: unconditionally reacquiring owned buffers each phase. | Estimate: avoids 7 repeated `TryEnsureVaultBuffer` metadata/sanitize routes per phase, profiler pending
- [x] Descriptor freshness and owner guard added. | DOD: `TryResolveOrAcquire` checks `TryGetBufferGeneration` before resolving a cached descriptor and `IsHandleValid` now requires `SystemID.GraphicsMaterials`, preventing wrong-owner BufferID collisions from becoming visual facts. | Rejected: resolving stale descriptors first and relying on black-box fault path for normal hot-swap recovery. | Estimate: static correctness; no fake microsecond claim
- [x] Static guard rerun after acquisition patch. | DOD: direct `GetGenerationHandle<VisualAging...>` calls are gone; only generic helper fallback remains; zero-GC/material/decal/DTO/Vault legacy scans remain clean; `git diff --check` for runtime file exit 0. | Rejected: build launch under CPU 93.636%. | Estimate: static scan ~1400 us
- [!] Compile verification still deferred. | Reason: CPU gate sample returned `CpuPercent=93.636` and no active compiler process; project rule forbids launching build above 50% CPU. | Integrator note: run Unity import/build only after CPU gate clears.

## Iteration Loop 9 - Shader Quality Branch Detox

[ANALYSIS]
Target: remove binary compile-time quality residue from the SHINOBU_219 aging shader math.
Finding: `H8UberNoirAgingGrowthMask` and `H8UberNoirApplyGlassMicroFracture` still had `_MATH_LOD_LOW` forks in the aging section, and global quality fallback treated non-finite quality as ultra.
DOD correction: aging detail now uses continuous `H8UberNoirSmoothRange01` weights and cheap zero-weight early-outs; invalid global quality falls to `0.0`.

- [x] Aging shader binary LOD fork removed. | DOD: the aging growth and glass micro-fracture functions no longer contain `_MATH_LOD_LOW`, `shader_feature`, or `multi_compile` tokens in their local segment; detail blends by quality scalar. | Rejected: compile-time low/high fork for SHINOBU_219 aging detail. | Estimate: low quality skips 2 noise taps for rust growth and 2 noise taps + radial fracture branch for glass
- [x] Shader quality NaN fallback corrected. | DOD: `H8UberNoirGlobalQualityWeight` now collapses non-finite `_H8GlobalQualityWeight` to `0.0` instead of `1.0`. | Rejected: spending visual-overkill ALU during fault input. | Estimate: one scalar fallback change; no variant cost
- [x] Static shader guard rerun. | DOD: aging segment scan clean for `_MATH_LOD_LOW`, `shader_feature`, `multi_compile`, `IsLow`, and `lowEnd`; `git diff --check` for shader/runtime exit 0 with CRLF warnings only. | Rejected: shader compiler/build under CPU gate. | Estimate: static scan ~900 us
- [!] Shader compile verification deferred. | Reason: build gate still above 50% CPU in prior sample and no Unity shader import was launched. | Integrator note: verify shader import and Frame Debugger when CPU gate clears.

## Iteration Loop 10 - Vault Quality Payload Shader Binding

[ANALYSIS]
Target: make SHINOBU_219 aging shader detail consume the uploaded Vault quality payload, not only the broader UberNoir quality resolver.
Affected systems: `Hecton8_UberNoir.hlsl`, SHINOBU_219 docs/logs only.
Zero GC proof: HLSL-only runtime path change; no C# collection, renderer material mutation, MPB, or managed allocation surface added.
State check: DTO layout, BufferIDs, Vault descriptors, and dispatcher phases are unchanged; shader ABI still uses `_GlobalBaseAgingParams` plus `_GlobalBaseAgingRuntime`.
Rule quote: "NO BINARY SWITCHES: shader procedural calculations must scale continuously based on `HomeostasisBrain.GlobalQualityWeight`."

- [x] Uploaded quality lane wired into aging shader. | DOD: `H8UberNoirVisualAgingQualityWeight` blends `H8UberNoirGlobalQualityWeight()` toward `_GlobalBaseAgingRuntime.z` / `StressAndMicroFractures.w` through continuous payload availability weight. | Rejected: driving rust/glass detail solely from wider UberNoir material quality. | Estimate: 4 scalar ops + one smoothstep in shader path; profiler pending
- [x] Shader payload NaN vaccination widened. | DOD: `H8UberNoirFiniteSaturate4` sanitizes `RustAndCorrosion`, `SaltAndBiomass`, and `StressAndMicroFractures`; `DepthAndPressure.w` now falls back to `0.0` on non-finite data. | Rejected: trusting `saturate(NaN)` to protect shader math. | Estimate: finite checks only; no new texture samples or variants
- [x] Static guard rerun after quality binding. | DOD: helper tokens present; active aging functions line slice is clean for `_MATH_LOD_LOW`, `shader_feature`, `multi_compile`, `IsLow`, `lowEnd`, and `isLowEnd`; scoped SHINOBU forbidden-token scan returned no matches; `git diff --check` exit 0 with CRLF warnings only. | Rejected: broad unrelated shader cleanup. | Estimate: static scan ~1800 us
- [!] Shader compile/build verification deferred. | Reason: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100 and no active `dotnet/csc/VBCSCompiler` processes were found; project rule forbids build launch above 50% CPU. | Integrator note: verify Unity shader import, Frame Debugger, and GCMonitor when CPU gate clears.

## Iteration Loop 11 - First Payload Upload Fence

[ANALYSIS]
Target: prevent first `VisualSync` from advertising or uploading one uninitialized visual-aging row before Simulation/PostSimulation has produced a payload.
Affected systems: `VisualPressureAgingRuntime.VisualSyncTick`, `WriteDefaults`, Vault handle release, SHINOBU_219 docs/logs.
Zero GC proof: no managed containers, no per-renderer material mutation, no MPB, and no new NativeArray ownership; uses existing Vault method-local views and existing double-buffered `GraphicsBuffer`.
State check: `VisualAgingParamsDTO` layout, BufferIDs, shader ABI, and job structs unchanged.
Rule quote: "All operations must occur over unmanaged `NativeArray` structures and pre-allocated `GraphicsBuffer` views."

- [x] First-frame payload enable fenced. | DOD: added `_hasGeneratedPayload`; `VisualSync` now sets `_GlobalBaseAgingRuntime.x/y` to `0/0` until `PostSimulationTick` confirms a scheduled payload completed and `_activeCount > 0`. | Rejected: forcing `uploadCount=1` for an `UninitializedMemory` Vault row. | Estimate: removes one possible 64 B stale upload; runtime cost one bool branch
- [x] Default hydration clears first row and upload counters. | DOD: `WriteDefaults` locks `VisualPressureAgingParams`, zeroes `output[0]`, resets `_activeCount`, `_uploadedCount`, `_hasGeneratedPayload`, and marks GPU upload dirty. | Rejected: assuming shader-side finite guards are sufficient for ungenerated CPU payload. | Estimate: cold/default path only
- [x] Vault release invalidates payload readiness. | DOD: `ReleaseVaultHandles` now clears generated-payload readiness and upload counters alongside generation descriptors. | Rejected: keeping a true payload flag after Vault descriptor release. | Estimate: teardown/hot-swap only
- [x] Static guard rerun after upload fence. | DOD: `_hasGeneratedPayload` and shader runtime vector tokens verified; scoped SHINOBU forbidden-token scan returned no matches; runtime/shader `git diff --check` exit 0 with shader CRLF warning only. | Rejected: Unity import under CPU/compiler gate. | Estimate: static scan ~1600 us
- [!] Compile verification still deferred. | Reason: CPU gate returned `CpuPercent=100` and compiler processes were active (`csc`, `dotnet`); project rule forbids build launch above 50% CPU or while compiler processes are running. | Integrator note: run Unity import/build after CPU and compiler gates clear.

## Iteration Loop 12 - Hot Registry Lookup Fence

[ANALYSIS]
Target: remove `GlobalRegistry.DataVault` fallback lookup from SHINOBU_219 dispatcher phases.
Affected systems: `VisualPressureAgingRuntime.ResolveVault`, editor/gizmo cold calls, SHINOBU_219 docs/logs.
Zero GC proof: no managed container or allocation surface added; change is one bool parameter and branch.
State check: `_vault` remains cached at cold initialize; hot PreSimulation/Simulation/VisualSync now fail closed when cache is absent.
Rule quote: "Do not query the `GlobalRegistry` inside hot execution loops. Cache the required interfaces at the boot phase."

- [x] Hot phase registry fallback removed. | DOD: `PreSimulationTick`, `ScheduleSimulation`, `VisualSyncTick`, and hot pending tuning apply call `ResolveVault()` with default `allowRegistryLookup=false`; registry fallback exists only when explicitly requested. | Rejected: silently repairing missing `_vault` through `GlobalRegistry.DataVault` inside dispatcher phases. | Estimate: removes one possible service-locator lookup per phase fault path
- [x] Cold/editor lookup preserved. | DOD: editor tuning reads, gizmo read acquire/release, and explicit editor tuning write call `ResolveVault(true)` or `ApplyPendingEditorTuningImmediate(true)`. | Rejected: breaking editor facade recovery when runtime cache is not yet populated. | Estimate: editor/cold only
- [x] Static guard rerun after registry fence. | DOD: `ResolveVault(true)` appears only in static/editor bridge calls; phase methods call default resolver; scoped SHINOBU forbidden-token scan returned no matches; runtime `git diff --check` exit 0. | Rejected: editing core registry/dispatcher surface. | Estimate: static scan ~900 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=59.044` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun gate and Unity import/build after CPU drops below 50%.

## Iteration Loop 13 - Gizmo Payload Readiness Fence

[ANALYSIS]
Target: prevent the editor gizmo facade from exposing visual-aging rows before the first generated payload exists.
Affected systems: `VisualPressureAgingRuntime.TryAcquireAgingBufferRead`, SHINOBU_219 docs/logs.
Zero GC proof: no new managed collections or native allocations; the change is a readiness branch and count clamp after an existing Vault lock.
State check: GPU upload path, DTO layout, BufferIDs, shader ABI, and Burst jobs are unchanged.
Rule quote: "File I/O, CSV parsing, string splitting, or JSON parsing in Tick, FixedTick, LateFrameTick, Burst jobs, or renderer upload paths" remains untouched; this is an editor gizmo read fence only.

- [x] Gizmo read path fails closed before payload generation. | DOD: `TryAcquireAgingBufferRead` now returns true only when `_hasGeneratedPayload` is true and the resolved Vault params view is created/non-empty. | Rejected: drawing from `NativeArrayOptions.UninitializedMemory` rows during editor preview. | Estimate: one bool branch in editor-only gizmo route
- [x] Gizmo active count clamped to resolved view. | DOD: `activeCount = math.min(_activeCount, aging.Length)` before exposing the locked view. | Rejected: trusting stale `_activeCount` after Vault resize/rebind. | Estimate: one integer min in editor-only route
- [x] Static guard rerun after gizmo fence. | DOD: scoped SHINOBU forbidden-token scan returned no matches; `TryAcquireAgingBufferRead` line slice shows the `_hasGeneratedPayload` gate; trailing whitespace scan returned no matches. | Rejected: broad unrelated project scan. | Estimate: static scan ~1000 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=83.378` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build after CPU drops below 50% and no compiler process is active.

## Iteration Loop 14 - Construction Crack Decal Surface Removal

[ANALYSIS]
Target: remove the dead `BaseDegradationSystem.GlobalCrackDecal*` compatibility surface so Construction no longer advertises a crack-decal route for habitat aging.
Affected systems: `BaseDegradationSystem`, SHINOBU_219 docs/logs.
Zero GC proof: removed two cold `List<>` allocations and a dirty flag; no new allocation path added.
State check: rupture gameplay state, breach jet VFX, fluid aftermath decals, pressure compression, and parasite collapse logic are unchanged.
Rule quote: "Previous implementations of environmental damage relied on instantiating dynamic decals... Your mission is to eradicate unmanaged material states."

- [x] Dead crack-decal compatibility lists removed. | DOD: `_globalCrackDecalMatrices`, `_globalCrackDecalAtlasIndices`, and `_globalDecalBufferDirty` were deleted from `BaseDegradationSystem`. | Rejected: keeping empty legacy lists as a harmless shim; they are an advertised dynamic-decal route. | Estimate: removes two cold list allocations and one stale API surface
- [x] Dead global decal rebuild API removed. | DOD: `GlobalCrackDecalMatrices`, `GlobalCrackDecalAtlasIndices`, `RebuildGlobalDecalBuffer`, `MarkGlobalDecalBufferDirty`, and `RebuildGlobalDecalBufferIfDirty` no longer exist and have no consumers. | Rejected: no-op rebuild methods that imply runtime decal ownership. | Estimate: static surface reduction only
- [x] Static guard rerun after Construction sanitation. | DOD: `rg` found no `GlobalCrackDecal`, `_globalCrackDecal`, rebuild, dirty, or compatibility tokens in source; trailing whitespace scan returned no matches. | Rejected: editing SHINOBU_149/Visor dynamic-impact decals, because those are hull impact/fluid effects outside visual pressure-aging ownership. | Estimate: static scan ~1100 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=94.052` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build after CPU drops below 50% and no compiler process is active.

## Iteration Loop 15 - Structural Profile Decal Atlas Residue Removal

[ANALYSIS]
Target: remove unused rupture decal atlas authoring fields from `StructuralIntegrityProfile`.
Affected systems: `StructuralIntegrityProfile`, SHINOBU_219 docs/logs.
Zero GC proof: removed one serialized int per variant and one unused constant/property; no runtime allocation path added.
State check: structural thresholds and material variant identity remain unchanged.
Rule quote: "The shader will procedurally render salt buildup, corrosion, rust, and glass micro-fractures in a single, non-breaking pass."

- [x] Rupture decal atlas field removed from authoring profile. | DOD: `DefaultRuptureDecalAtlasIndex`, `ruptureDecalAtlasIndex`, and `RuptureDecalAtlasIndex` were deleted; constructor/default variants now carry only variant, span, and HP. | Rejected: preserving unused atlas indices in structural authoring. | Estimate: serialized residue removal only
- [x] Authoring tooltip corrected. | DOD: profile tooltip now states visual pressure aging is procedural in UberNoir, not decal-atlas driven. | Rejected: leaving the profile to imply a crack/rust decal atlas path. | Estimate: documentation-in-source only
- [x] Static guard rerun after profile cleanup. | DOD: source scan found no rupture decal atlas tokens in `Assets/_Project/Scripts/Construction`; `git diff --check` returned exit 0 with CRLF warnings only; trailing whitespace scan returned no matches. | Rejected: touching `DynamicDecalVaultRuntime` or impact/fluid decal owners. | Estimate: static scan ~900 us
- [!] Compile verification deferred. | Reason: latest gate remained above 50% CPU in prior sample (`CpuPercent=94.052`) with no compiler processes; build launch remains forbidden. | Integrator note: rerun Unity import/build after CPU drops below 50% and no compiler process is active.

## Iteration Loop 16 - CSV Hot-Path Eviction and Shader Quality Variant Sanitation

[ANALYSIS]
Target: remove remaining SHINOBU-owned frame-path file I/O and binary shader LOD hooks from the procedural aging route.
Affected systems: `VisualPressureAgingRuntime`, `VisualPressureAgingTunerWindow`, `Hecton8_UberNoir.hlsl`, SHINOBU_219 docs/logs.
Zero GC proof: runtime CSV disk read no longer executes from `PreSimulationTick`; manual editor reload is cold-only. Shader aging quality is driven by float `quality` thresholds and `smoothstep` ranges, not `_MATH_LOD_LOW` inside the visual-aging blocks.
State check: Vault buffer IDs, DTO layout, Burst jobs, GPU upload ABI, and Construction rupture gameplay state are unchanged.
Rule quote: "The parser must run cold" and "NO BINARY SWITCHES."

- [x] CSV poll removed from dispatcher hot path. | DOD: `PreSimulationTick` now only resolves cached Vault state and applies pending editor tuning; `CsvPollCadenceFrames` and `MonitorCsv` were deleted. | Rejected: 96-frame `File.GetLastWriteTimeUtc` polling in PreSimulation, because it is still frame-path disk I/O. | Estimate: removes one hot conditional plus possible filesystem probe every 96 frames
- [x] Cold editor CSV reload added. | DOD: `TryReloadEditorCsv()` calls `ReloadCsvFromDisk(vault, true)` only from the UI Toolkit tuner button `Reload CSV Profiles`; the allocation-free `ReadOnlySpan<byte>` parser and Vault scratch lane remain intact. | Rejected: deleting the CSV bridge entirely, because Task 17 requires human-tunable profiles. | Estimate: no gameplay-frame cost
- [x] Aging shader binary LOD hooks removed from SHINOBU blocks. | DOD: visual aging albedo array sampling, macro noise, rust POM UV, rust corrosion, and surface aging path now collapse through `H8UberNoirSmoothRange01`/quality gates instead of `_MATH_LOD_LOW` branches in the inspected ranges. | Rejected: compile-time low shader variant for aging visuals; it creates a binary quality surface. | Estimate: low quality exits before RustDetail samples/POM/triplanar/high surface work; high quality keeps overkill path
- [x] Static guard rerun after loop 16. | DOD: scoped scans found no `CsvPollCadenceFrames`, `MonitorCsv`, frame-path CSV/File calls, forbidden material mutation/decal atlas tokens, or binary LOD tokens inside SHINOBU aging ranges; trailing whitespace scan returned no matches; `git diff --check` returned exit 0 with CRLF warnings only. | Rejected: broad failure on unrelated global UberNoir `_MATH_LOD_LOW` lighting/caustic sections outside SHINOBU_219 ownership. | Estimate: static scan ~1400 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=98.693` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build after CPU drops below 50% and no compiler process is active.

## Iteration Loop 17 - Vault Lock Fence and Payload Quality Continuity

[ANALYSIS]
Target: eliminate remaining SHINOBU-owned lock-order ambiguity and make rust POM/detail use the same Vault-derived quality scalar as the rest of the aging shader.
Affected systems: `VisualPressureAgingRuntime`, `VisualPressureAgingTunerWindow`, `Hecton8_UberNoir.hlsl`, SHINOBU_219 docs/logs.
Zero GC proof: gameplay/runtime paths still allocate no managed arrays/strings; changes are lock ordering, shader argument routing, and editor-only report widening.
State check: BufferIDs `71240..71246`, DTO sizes, Burst jobs, and GPU buffer ABI are unchanged.
Rule quote: "one fact -> one owner -> one route -> one proof" and "NO BINARY SWITCHES."

- [x] VisualSync Vault reads lock-fenced. | DOD: `VisualSyncTick` now locks `VisualPressureAgingParams`, `VisualPressureAgingRuntime`, `VisualPressureAgingTelemetryRing`, and `VisualPressureAgingTelemetryCursor` before GPU upload/runtime write/dump reads, then unlocks in reverse order. | Rejected: reading params/telemetry/cursor under only the runtime lock. | Estimate: four nonblocking lock probes in VisualSync, prevents undefined owner overlap
- [x] SHINOBU lock order normalized. | DOD: editor read, default hydration, CSV reload, VisualSync, and job locks now follow ascending owned BufferID order for overlapping lanes. | Rejected: cold/editor mixed order (`tuning -> runtime`, `scratch -> tuning`, `tuning -> mock -> params -> runtime`) because it is bad Vault discipline. | Estimate: no steady-state ALU change
- [x] Rust POM/detail consumes payload quality. | DOD: `H8UberNoirResolveRustPomUv` now takes the `quality` computed by `H8UberNoirVisualAgingQualityWeight(visualAging)`, instead of rereading global quality internally. | Rejected: letting Vault row quality affect rust/salt/glass but not RustDetail/POM gating. | Estimate: no extra samples; one argument pass
- [x] Inquisition archaeology widened. | DOD: `VisualPressureAgingInquisition` now reports `BaseCorrosion.cs`, `GlassFracture.cs`, exact `GetComponent<Renderer>().material.SetFloat`, and rust/algae/corrosion/glass aging decal tokens in `Rendering/` and `Construction/`. | Rejected: validator that only counted BaseDegradation/runtime scoped material mutations. | Estimate: editor-only scan cost
- [x] Static guard rerun after loop 17. | DOD: scoped scans found no live legacy aging material/decal tokens in `Rendering`/`Construction`, no SHINOBU aging binary LOD tokens, and no rollback/Merkle references beyond `H8Memory` BufferIDs; trailing whitespace scan returned no matches; `git diff --check` returned exit 0 with CRLF warnings only. | Rejected: treating validator literal search strings as runtime usage. | Estimate: static scan ~1700 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=98.693` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build after CPU drops below 50% and no compiler process is active.

## Iteration Loop 18 - Parallel Forensic Corrections

[ANALYSIS]
Target: integrate the three read-only forensic passes and remove the last SHINOBU-owned payload/validator/readiness weak spots.
Affected systems: `VisualPressureAgingRuntime`, `Hecton8_UberNoir.hlsl`, `VisualPressureAgingTunerWindow`, `VisualPressureAgingGizmoVisualizer`, SHINOBU_219 docs/logs.
Zero GC proof: runtime changes add no managed hot-path containers, no per-renderer material mutation, no MPB route, and no private native collection ownership.
State check: BufferIDs `71240..71246`, `VisualAgingParamsDTO` 64-byte ABI, Burst job types, and rollback exclusion are unchanged.

- [x] Scheduled-job lock leak path closed. | DOD: `ScheduleSimulation` now wraps post-lock scheduling in `try/finally` and releases SHINOBU Vault locks if scheduling exits before `_simulationScheduled` is armed. | Rejected: assuming no exception/early exit between lock acquisition and job registration. | Estimate: fault-path safety only; no steady-frame saving claimed
- [x] Editor/gizmo reads fenced against scheduled writes. | DOD: `TryReadEditorTuning` and `TryAcquireAgingBufferRead` return false while a simulation job still owns scheduled Vault locks. | Rejected: cold editor overlays reading tuning/aging lanes during an outstanding job. | Estimate: editor-only branch
- [x] Payload activation made continuous. | DOD: `_GlobalBaseAgingRuntime.y` ramps on CPU, `H8UberNoirLoadVisualAging` reads payload rows at epsilon-positive blend instead of a `0.5` step, and shader lerps each DTO lane by the runtime blend. | Rejected: half-threshold payload pop. | Estimate: no new texture samples; one epsilon gate
- [x] Rust POM/detail tied to dynamic rust and row quality. | DOD: `H8UberNoirResolveRustPomUv` consumes `dynamicRust` plus the row-aware aging quality scalar, so RustDetail/POM activates from Vault rust rather than only legacy global rust. | Rejected: split quality authority inside one visual-aging path. | Estimate: no extra samples versus existing high path
- [x] Inquisition report made static-proof explicit and non-destructive. | DOD: editor report writes dedicated `Docs/Reports/VISUAL_AGING_INQUISITION_REPORT.json`, writes aggregate report with `STATIC_PASS`/`STATIC_FAIL`, and preserves unrelated prior aggregate contents as escaped `previousReportRaw`. | Rejected: overwriting another agent's aggregate report while claiming runtime proof. | Estimate: editor-only scan/write
- [x] Static guard rerun after loop 18 patch. | DOD: SHINOBU shader line ranges 470-620/1270-1355/1480-1605 returned no `_MATH_LOD_LOW`, `shader_feature`, `multi_compile`, or low-end switch tokens; `Rendering/Construction` legacy aging decal/material scan returned no matches; hot runtime/gizmo scan returned no material/Vault legacy/native collection hits; DTO property/Pack=1 scan returned no matches; `git diff --check` exit 0 with LF/CRLF warnings only; trailing whitespace scan returned no matches. | Rejected: treating validator literal strings as runtime usage. | Estimate: static scan ~2600 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=100` with no `dotnet`, `csc`, or `VBCSCompiler`; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build, shader import, Frame Debugger, and GCMonitor after CPU drops below 50% and no compiler process is active.

## Iteration Loop 19 - Mock Temperature NaN Vaccine

[ANALYSIS]
Target: close the fallback/mock aging path's temperature finite check gap.
Affected systems: `GenerateMockAgingDataJob`, SHINOBU_219 docs/logs.
Zero GC proof: one Burst-local helper and scalar branch only; no managed allocation, no native allocation, no renderer mutation.
State check: DTO layout, BufferIDs, GPU upload ABI, shader ABI, and structural processing path are unchanged.

- [x] Mock temperature fallback finite-check added. | DOD: `GenerateMockAgingDataJob` now resolves `Temperatures[0]` through `ResolveTemperature()` and falls back to `Tuning.MockTemperatureC` if the Vault mock lane is absent or non-finite. | Rejected: trusting the mock temperature lane because fallback profiling must be just as NaN-vaccinated as the structural route. | Estimate: one finite check per mock row
- [x] Telemetry cursor negative-wrap fixed. | DOD: `RecordVisualAgingTelemetryJob` and fault dump readback now wrap negative cursor values back into `[0, Telemetry.Length - 1]` before indexing the 300-frame ring. | Rejected: trusting a Vault cursor lane during black-box fault handling. | Estimate: one modulo and sign branch per telemetry write/dump row
- [x] Runtime using hygiene trimmed. | DOD: removed unused `using System.Diagnostics;`; explicit `Stopwatch` alias remains. | Rejected: leaving stale namespace imports in a compile-wall-sensitive runtime file. | Estimate: compile hygiene only
- [x] Static guard rerun after loop 19. | DOD: targeted temperature/cursor helper scan confirms structural and mock finite fallback helpers plus bounded telemetry cursor wrap; forbidden runtime/gizmo scan clean; SHINOBU shader ranges clean for binary LOD tokens; legacy `Rendering/Construction` aging scan clean; rollback/save scan only finds `H8Memory` BufferIDs; `git diff --check` exit 0 with LF/CRLF warnings only; trailing whitespace scan clean. | Rejected: build launch under CPU gate. | Estimate: static scan ~2600 us
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=100` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build, shader compiler, Frame Debugger, and GCMonitor after CPU drops below 50%.

## Iteration Loop 20 - Duplicate Phase and JSON Proof Fence

[ANALYSIS]
Target: close a duplicate dispatcher phase guard and make static report preservation JSON-safe.
Affected systems: `VisualPressureAgingRuntime`, `VisualPressureAgingTunerWindow`, SHINOBU_219 docs/logs.
Zero GC proof: runtime patch adds two scalar guards only; editor JSON escape is cold static report path.
State check: DTO ABI, BufferIDs, Burst jobs, GPU buffer ABI, and shader ABI unchanged.

- [x] Duplicate schedule guard added. | DOD: `ScheduleSimulation` returns `dependsOn` while `_simulationScheduled` is already true, so an unexpected duplicate phase cannot call `UnlockJobBuffers()` and release Vault locks for an in-flight job. | Rejected: trusting dispatcher ordering as the only guard. | Estimate: one branch in Simulation phase.
- [x] VisualSync stale-schedule fail-closed. | DOD: `VisualSyncTick` returns while `_simulationScheduled` is true, preserving PostSimulation as the only unlock boundary before GPU upload. | Rejected: reading/uploading before post-sim swap if dispatcher order regresses. | Estimate: one branch in VisualSync.
- [x] Static report JSON escape hardened. | DOD: `AppendJsonString` now emits `\u00XX` for remaining control chars below space, so preserved aggregate report text cannot corrupt JSON. | Rejected: assuming old report text only contains `\n`, `\r`, `\t`, `\b`, and `\f`. | Estimate: editor-only.
- [x] Static guard rerun after loop 20. | DOD: `git diff --check` exit 0 with CRLF warnings only; trailing whitespace clean; SHINOBU shader ranges clean; `Rendering/Construction` legacy aging scan clean; runtime/gizmo scans found no `Complete`, private native collection allocation, `foreach`, `LINQ`, renderer/material mutation, `string.Format`, `.ToString`, or interpolation hits. Cold `ResolveVault(true)`/`GlobalRegistry.DataVault` hits are limited to static editor/gizmo facades and initialization cache, not hot loops. | Rejected: broad whole-repo scan noise from generic `Renderer`/format tokens. | Estimate: static scan ~3100 us.
- [!] Compile verification deferred. | Reason: latest gate returned `CpuPercent=50.241` with no compiler processes; build remains forbidden above 50% CPU. | Integrator note: rerun Unity import/build, shader compiler, Frame Debugger, and GCMonitor after CPU drops below 50%.
