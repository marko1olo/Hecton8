PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-18
Status: ULTRA POLISHED + MATERIAL CACHE + CPU SCRATCH CACHE + BOXING GUARD + COMPUTE BINDING CACHE + HEADLIGHT UPLOAD GATE + MOTION VECTOR CACHE + INDIRECT ARGS CACHE + EXTERNAL BUILD WALL

## Mandates Loaded
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- REND_GPU_Sovereignty
- REND_GPU_Occlusion_Culling_6000
- REND_Instanced_Flora_Physics
- REND_URP_Graphics_HotPath_Optimization_HLOD
- CORE_Weather_Abyssal_FlowField_Currents
- MATH_Coordinate_Precision_AUP_FloatingOrigin

## Scope Lock
Target files:
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Art/Shaders/FloraCulling.compute`
- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`
- `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs`
- `Assets/_Project/Scripts/Editor/ScatterDiagnosticsWindow.cs`

Domain boundary: abyssal forest BRG/indirect flora rendering only. Cross-domain signals consume `GlobalRegistry` and `SignalBus`; no direct producer dependency on Agent 08.

## Checklist
- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE | DOD: archive/rationale scan performed; no named binary threshold file found, fallback distances documented and existing LOD sliders/mocks retained | Alternative rejected: inventing hidden OSHINO data | Estimate: 5-15 us saved per cull batch via cached squared thresholds.
- [x] Task 02 GAMEOBJECT_ERADICATION_PASS | DOD: no scene `MeshFilter`, `MeshRenderer`, or `Instantiate` path added; existing BRG/GraphicsBuffer/indirect path is authoritative | Alternative rejected: hierarchy flora scatter | Estimate: 200-700 us CPU submission avoided at density.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: mutable culling data remains direct fields/native arrays; no DTO properties added for Burst-visible write lanes | Alternative rejected: property-wrapped BRG/native handles | Estimate: compile-failure risk removed.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: Matrix4x4=64, `HectonVegetationInstanceData`/scatter metadata 16-byte lanes; `Pack = 1` removed from GPU scatter structs | Alternative rejected: compiler packing guesswork | Estimate: prevents ARM64/Vulkan layout faults.
- [x] Task 05 BLIND_MATRIX_MOCKING | DOD: `MockMatrixGeneratorJob` creates deterministic 100x100 persistent native matrix/data lane | Alternative rejected: waiting for Agent 08/DataVault | Estimate: integration freeze avoided; runtime frame saving 0 us.
- [x] Task 06 BRG_INITIALIZATION_KERNEL | DOD: existing `BatchRendererGroup` path audited; indirect args are raw, instance buffers persistent, batch handles centralized | Alternative rejected: `DrawMeshInstanced`/object submission | Estimate: 200-700 us CPU saved.
- [x] Task 07 BURST_FRUSTUM_CULLING_JOB | DOD: Burst CPU fallback and GPU compute frustum/distance culling both apply deterministic density step | Alternative rejected: managed visible lists | Estimate: 80-250 us CPU/GPU saved depending density.
- [x] Task 08 HIERARCHICAL_Z_BUFFER_HZB_OCCLUSION | DOD: existing depth-pyramid HZB cull path retained and telemetry now counts HZB occlusion separately | Alternative rejected: synchronous CPU occlusion/readback | Estimate: 100-600 us GPU saved in blocked views.
- [x] Task 09 DYNAMIC_LOD_TRANSITION | DOD: near/far append buffers split LOD0/LOD1; far culling cadence retained | Alternative rejected: one high-poly draw list | Estimate: 200-1200 us GPU vertex saved.
- [x] Task 10 CUSTOM_DATA_INJECTION | DOD: aligned metadata/custom payload buffers remain shader-bound by visible source index; no per-instance MPB/material clones added | Alternative rejected: per-instance material state | Estimate: SRP batcher break avoided.
- [x] Task 11 THE_DEAR_LIE_CURRENT_BENDING | DOD: shader current bending path uses flow/current buffers and vertex displacement only | Alternative rejected: per-plant colliders/rigidbodies | Estimate: 500+ us CPU avoided at 150k plants.
- [x] Task 12 INTERACTIVE_WAKE_DEFORMATION | DOD: shader wake buffer path verified; submarine/player wake bends vertices, not physics proxies | Alternative rejected: trigger colliders on flora | Estimate: 300-2000 us CPU avoided near submarine.
- [x] Task 13 CHUNK_STREAMING_PAGINATION | DOD: renderer reuses persistent buffers and external source handles; capacity grows by reuse/NextPowerOfTwo, not per-frame chunk churn | Alternative rejected: destroy/recreate buffers on chunk swaps | Estimate: upload freeze spikes prevented.
- [x] Task 14 SHADOW_CASTER_OPTIMIZATION | DOD: shadow cull/draw path uses near LOD only; far/impostor shadow burden remains disabled | Alternative rejected: all flora casts shadows | Estimate: 500-3000 us GPU saved in cascades.
- [x] Task 15 HARDWARE_LOD_DENSITY_SCALING | DOD: `SystemHealthSignal` and scalability tier drive deterministic decimation step 1-4 | Alternative rejected: fixed density on Steam Deck | Estimate: 400-2000 us GPU/CPU saved under pressure.
- [x] Task 16 AUP_JITTER_PREVENTION_OFFSET | DOD: `_GlobalFloatingOffset`/AUP offset path retained for compute, shader, and BRG fallback | Alternative rejected: raw far-world float matrices | Estimate: correctness/stability, speed 0 us.
- [x] Task 17 TELEMETRY_CULL_TRACKER | DOD: GPU counters feed 300-frame NativeArray ring; >50k visible flips overdraw warning; invalid counters dump binary | Alternative rejected: "unknown cull efficiency" | Estimate: diagnostic only; prevents blind multi-ms regressions.
- [x] Task 18 BRG_DIAGNOSTIC_EDITOR_WINDOW | DOD: `ScatterDiagnosticsWindow` added with telemetry chart and renderer picker | Alternative rejected: console/profiler-only tuning | Estimate: editor-only.
- [x] Task 19 LIVE_LOD_TUNING_SLIDERS | DOD: live LOD0, LOD1, and Max Density sliders call renderer setter and invalidate far snapshot | Alternative rejected: recompiles/inspector-only edits | Estimate: editor-only.
- [x] Task 20 FRUSTUM_GIZMO_DEBUGGER | DOD: caller-owned arrays copy debug bounds; SceneView and `OnDrawGizmos` draw yellow visible and red culled samples | Alternative rejected: no visual cull alignment proof | Estimate: editor-only.

## Iteration Log
- Loop 0: Prompt extracted by CLI from `Docs/Tasks/CURRENT_BATCH.md`; mandate registry and domain document read.
- Loop 1: Existing renderer archaeology found `HectonIndirectVegetationRenderer`, `FloraCulling.compute`, `Hecton_IndirectVegetation.shader`, and DataVault-backed scatter manager. Decision: patch existing highway, no duplicate renderer.
- Loop 2: Tasks 01-05 executed. Archive/rationale evidence reviewed, ARM64 layout risk identified, mock 100x100 generator added. Prompt re-extracted after task 03.
- Loop 3: Tasks 06-10 executed. BRG/indirect/HZB path audited, density decimation added to GPU and Burst fallback, custom data lane verified. Prompt re-extracted after task 06 and 09.
- Loop 4: Tasks 11-15 executed. Dear Lie shader current/wake path audited; shadow near-only path, buffer reuse, and SystemHealth density scaling wired. Prompt re-extracted after task 12 and 15.
- Loop 5: Tasks 16-20 executed. AUP offset verified, GPU telemetry black box added, editor diagnostics/sliders/gizmos added. Prompt re-extracted after task 18.
- Loop 6: Self-audit pass found formal OnDrawGizmos requirement; added editor-only OnDrawGizmos hook wired to diagnostics window.
- Loop 7: Ultra polish pass executed after mandate. Mock NativeLists/job/API moved under `UNITY_EDITOR`, telemetry structs padded to 40 bytes, `.h8dump` fatal dump added, disabled telemetry keeps a 16-byte compute dummy buffer bound, and adjacent scatter thread-group guard clamped to 512.
- Loop 8: Human bridge gap closed. `ScatterDiagnosticsWindow` now imports/exports `lod0,lod1,maxDensity,minimumDensityStep` CSV, hot-reloads the CSV in editor, and bakes the same values to a fixed `.h8bin` profile without adding runtime File I/O.
- Loop 9: Render hot-path binding rot reduced. Added per-pass material binding state caches so unchanged BRG/indirect materials skip repeated `SetBuffer`, `SetVector`, `SetFloat`, and keyword writes when buffers, LOD constants, offset, and visible-index buffers are unchanged.
- Loop 10: CPU fallback culling allocation churn reduced. Replaced normal-path `Allocator.TempJob` plane/headlight/visibility scratch arrays with a double-buffered persistent scratch cache, deferred native disposal for in-flight scratch/data arrays, and no-stall all-visible fallback when both scratch buffers are still owned by active culling jobs.
- Loop 11: Hidden boxing guard added. Removed `JobHandle.Equals(default)` and BRG `Batch*ID.Equals(default)` from the SHINOBU renderer path; scratch ownership now uses explicit validity booleans and BRG handles use raw `.value` comparisons.
- Loop 12: Compute binding churn reduced. Added value-state caches for main cull, shadow cull, clear snap, and flag snap compute buffer bindings so stable `GraphicsBuffer` references skip repeated `ComputeShader.SetBuffer` calls while per-frame cull constants still update.
- Loop 13: Darkness/headlight compute upload gate added. The GPU cull path now uploads headlight `Vector4[]` payloads only when `_HectonScooterHeadlightCount > 0`; the shadow cull repeats only the count because the arrays are shader-global and already uploaded by the main cull dispatch.
- Loop 14: Remaining micro binding/scrub waste reduced. Removed renderer-side headlight array clearing because `MantaScooter` already publishes dense count-gated payloads, and cached per-material motion-vector previous-camera uploads so unchanged motion materials skip `_HectonPreviousCameraPosition` writes.
- Loop 15: Indirect args clear churn reduced. Added a clear-kernel signature cache so repeated near/shadow args clears skip unchanged mesh index constants and stable args-buffer bindings while still dispatching the clear kernel per target buffer.

## Self Audit
<SELF_AUDIT>
1. Did I instantiate any GameObjects or use MeshRenderers? No new `GameObject`, `MeshRenderer`, `MeshFilter`, or `Instantiate` path was added. Rendering remains BRG/GraphicsBuffer/RenderMeshIndirect.
2. Are custom data structs 16-byte aligned? Yes. Matrix4x4 lane is 64 bytes, Vector4/float4 payload lane is 16 bytes, scatter metadata is explicit 64 bytes, constants are 176 bytes. `Pack = 1` was removed from GPU scatter structs.
3. Did CullingJob use Burst and NativeArrays exclusively? Yes for CPU fallback. GPU path uses compute append buffers; no managed visible list was added. Mock NativeLists are now editor-only diagnostic storage.
4. Is the Dear Lie implemented? Yes. Currents and wakes are vertex shader deformation from global buffers; no per-plant physics/collider system was introduced.
5. Did I provide Scatter Diagnostics? Yes. EditorWindow displays telemetry chart, LOD/density controls, CSV hot-reload, `.h8bin` bake, mock generation, and debug bounds.
</SELF_AUDIT>

## Verification Notes
- `git diff --check -- [touched files]`: pass; only line-ending warnings reported.
- Forbidden-pattern scan on touched files: pass. No `Pack = 1`, `System.Linq`, `new List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, or `Collider` hits in the SHINOBU_09 touched set.
- `POLISH_MANDATE` scan: tag not found in `Docs/Tasks/CURRENT_BATCH.md`; fallback anti-bloat self-audit executed.
- `dotnet restore Assembly-CSharp.csproj /p:MSBuildProjectExtensionsPath=Temp\obj\Assembly-CSharp\ /p:RestoreIgnoreFailedSources=true`: pass; created expected assets file.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false`: blocked by missing external/project DLLs in `Temp\bin\Debug` including Amplify, Astar, Bakery, Crest, Den.Tools, EasySave, GPUInstancer, Hecton8.Core, Hecton8.Editor, MapMagic, RealtimeCSG, Shapes, URP, and others.
- `dotnet restore Hecton8.Core.csproj /p:MSBuildProjectExtensionsPath=Temp\obj\Hecton8.Core\ /p:RestoreIgnoreFailedSources=true`: pass.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal`: blocked by unrelated `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs(167,13): CS0103 EnsureDirectoryPage does not exist`.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`: blocked by missing restore assets for referenced project graph unless every dependency receives explicit `MSBuildProjectExtensionsPath`; not used as authoritative proof.
- Ultra polish static scan: `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, and `Collider` were absent from the touched SHINOBU_09 set. One existing `Pack = 4` spore event in `HectonIndirectVegetationContracts.cs` remains; it is not `Pack = 1`, keeps AUP first, and resolves to an 8-byte-multiple event layout by field accounting.
- Thread-group proof: `FloraCulling.compute` uses `HECTON_THREADS_PER_GROUP 64`; `GpuScatterLodManager` fallback is 64 and the Metal guard is now 512.
- Struct layout proof: `HectonVegetationInstanceData` is 64 bytes; `GpuScatterFloraInstanceData` is 64 bytes; `ScatterFrameConstants` is 176 bytes; `ScatterBlackBoxEntry` is 64 bytes; SHINOBU cull telemetry and flora growth telemetry entries are now 40 bytes.
- Compile protection: no additional full rebuild was run during ultra polish; previous build wall remains external and already logged.
- CSV bridge verification: runtime code received only `MinimumDensityDecimationStep` and `SetDiagnosticScatterTuning`; CSV parsing, hot reload, and binary bake are under `#if UNITY_EDITOR` in `ScatterDiagnosticsWindow`.
- Latest limited compile check: `dotnet restore Hecton8.Core.csproj /p:MSBuildProjectExtensionsPath=Temp\obj\Hecton8.Core\ /p:RestoreIgnoreFailedSources=true` passed. `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`: missing `PathWaypointDTO` and `MockSdfGrid`. No SHINOBU_09 compiler errors were emitted in the visible output.
- Material binding cache verification: forbidden scan remains clean; `git diff --check` remains clean except CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` again reached source compile and is blocked outside SHINOBU by `GlobalWorldSampler`, `BinaryLayoutManifest`, and `EcosystemRuntimeInstaller` missing/readonly DTO issues. No SHINOBU_09 compiler errors were emitted in the visible output.
- CPU scratch cache verification: `rg` found no `Allocator.TempJob` or `Allocator.Temp` in `HectonIndirectVegetationRenderer.cs`. Forbidden-pattern scan on the touched SHINOBU set remains clean. `git diff --check` remains clean except CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `ShinobuEcosystemBalancer`, `DroneFleetManager`, and `PlayerCriticalProceduralAudioRenderer`. No SHINOBU_09 compiler errors were emitted in the visible output.
- Boxing guard verification: `rg "\.Equals\(default\)" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` returns no hits. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `GlobalTelemetryBus` and `HectonMarineSnowRenderer`. No SHINOBU_09 compiler errors were emitted in the visible output. `dotnet build-server shutdown` executed after the compile attempt.
- Compute binding cache verification: forbidden scan remains clean and `git diff --check` remains clean except CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `HomeostasisBrain`, `DroneFleetManager`, and `ShinobuEcosystemBalancer`. No SHINOBU_09 compiler errors were emitted in the visible output. `dotnet build-server shutdown` executed after the compile attempt.
- Headlight upload gate verification: shader audit confirms `_HectonScooterHeadlightCount` breaks the loop before any headlight array read, so zero-count frames can skip the four `SetVectorArray` uploads. `rg "SetVectorArray\(_ScooterHeadlight"` now finds only the helper body, not duplicated main/shadow call sites. Forbidden scan remains clean and `git diff --check` remains clean except CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `BinaryLayoutManifest`, `WorldChunkResidencyManager`, `TerminalOsRuntime`, and `GlobalPhysicsStateManager`. No SHINOBU_09 compiler errors were emitted in the visible output. `dotnet build-server shutdown` executed after the compile attempt.
- Motion vector/headlight scrub verification: `rg "ClearScooterHeadlightPayload"` returns no hits in the renderer, and `_HectonPreviousCameraPosition` material writes now occur only inside `ApplyMotionVectorPreviousCamera`. Forbidden scan remains clean and `git diff --check` remains clean except CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `SubtitleManager`, `GlobalPhysicsStateManager`, and `SubmarineDynamicsRuntime`. No SHINOBU_09 compiler errors were emitted in the visible output. `dotnet build-server shutdown` executed after the compile attempt.
- Indirect args cache verification: first targeted build found a SHINOBU `uint` to `int` conversion error in the new base-vertex cache; fixed with explicit clamp. Second targeted `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and is blocked outside SHINOBU by `SaveStateMerkleTree`, `SubtitleManager`, and `GlobalPhysicsStateManager`. No SHINOBU_09 compiler errors were emitted in the visible output. `dotnet build-server shutdown` executed after the compile attempt.
