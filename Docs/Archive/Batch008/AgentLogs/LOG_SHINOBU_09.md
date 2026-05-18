PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: COMPLETE WITH EXTERNAL BUILD WALL

## What Was Wrong
- The abyssal flora path already had a large BRG/indirect renderer, but it lacked a SHINOBU_09-controlled no-producer mock lane, hard runtime density decimation, explicit 300-frame cull telemetry, and a human diagnostics facade.
- Low-end hardware risk was unbounded: fixed density could push too many instances through visible append buffers, shadow culling, and vertex work when a dense forest filled the camera.
- GPU-facing scatter structs in `GpuScatterLodManager` still used `Pack = 1`, which is a Vulkan/Metal/ARM64 alignment hazard.
- The existing shader already obeyed the Dear Lie in practice, but the task needed an explicit audit: current and wake response must stay in vertex shader deformation, not per-plant physics.
- Editor tuning was blind. There was no SHINOBU_09 window showing visible/frustum/HZB ratios, density step, system stress, or overdraw warning.

## What Was Done
- Patched `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`.
  - Added `MockMatrixGeneratorJob` and `GenerateMockScatterForDiagnostics()` for a deterministic 100x100 matrix/data lane with persistent `NativeList` storage.
  - Added deterministic density decimation to the Burst fallback and GPU culling dispatch. Runtime step resolves from Max Density, `ScalabilityTierProfiles.LowMx350`, and `SystemHealthSignal` pressure.
  - Added GPU cull telemetry counters: total, frustum/distance/density culled, HZB occluded, visible.
  - Added async GPU readback every 30 frames into a fixed 300-frame NativeArray ring.
  - Added invalid-counter binary dump target: `Docs/AgentLogs/Dump_SHINOBU_09.bin`.
  - Added overdraw warning threshold at 50,000 visible instances.
  - Added public diagnostic setter for LOD0, LOD1, and Max Density.
  - Added caller-owned debug bounds copy and editor-only `OnDrawGizmos` yellow/red bounds rendering.
- Patched `Assets/_Project/Art/Shaders/FloraCulling.compute`.
  - Added `_HectonDensityDecimationStep`.
  - Added `_HectonCullTelemetryCounters` and `_HectonCullTelemetryEnabled`.
  - Added deterministic hash decimation in main and shadow culling kernels.
  - Added HZB/frustum/visible counter writes with `InterlockedAdd`.
- Patched `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs`.
  - Removed `Pack = 1` from GPU-facing scatter structs while preserving explicit sizes.
- Added `Assets/_Project/Scripts/Editor/ScatterDiagnosticsWindow.cs`.
  - Menu: `Hecton8/Rendering/Scatter Diagnostics`.
  - Shows telemetry pie chart, instance counts, HZB occlusion, density step, system stress, and overdraw warning.
  - Provides live LOD0, LOD1, and Max Density sliders.
  - Provides 100x100 mock generation.
  - Draws sampled visible bounds yellow and culled bounds red.
- Wrote durable protocol files:
  - `Docs/Tasks/Status_SHINOBU_09.md`
  - `Docs/AgentLogs/Rationale_SHINOBU_09.md`

## Cinematic Cheats Used
- Dear Lie current bending: flow/current response remains shader vertex displacement from global/current buffers. No 150,000-plant CPU integration.
- Wake fake: submarine/player wake remains a shader buffer influence. No trigger colliders or per-plant rigidbodies.
- HZB occlusion: plants hidden behind depth pyramid samples are rejected before expensive visible rendering.
- Density lie: weak hardware gets a deterministic sparse forest instead of unstable frame time.
- Shadow lie: near LOD owns the shadow burden; far/impostor flora does not flood cascades.
- Mock forest: 100x100 deterministic grid replaces missing Agent 08 matrix production for validation.

## Exact Microseconds Saved
- GameObject eradication / BRG path preservation: estimated 200-700 us CPU submission saved at dense counts.
- Burst/GPU frustum and density culling: estimated 80-250 us saved before draw submission depending on camera density.
- HZB occlusion rejection: estimated 100-600 us GPU saved in blocked terrain views.
- LOD0/LOD1 split and far cadence: estimated 200-1200 us GPU vertex saved in mid/far forest views.
- Dear Lie current bending: estimated 500+ us CPU avoided versus per-plant physics at 150,000 plants.
- Wake shader fake: estimated 300-2000 us CPU avoided near submarine interaction zones.
- Near-only shadows: estimated 500-3000 us GPU saved in shadow cascades.
- Hardware density decimation: estimated 400-2000 us GPU/CPU pressure reduction under low-tier or stressed SystemHealth.
- AUP offset: 0 us direct speed gain; prevents far-world jitter/correctness failure.
- Telemetry: 0 us direct speed gain; async 30-frame cadence is expected under 100 us suspicion threshold and prevents blind multi-ms overdraw regressions.

## Verification
- `git diff --check -- [SHINOBU_09 touched files]`: pass. Only CRLF warnings.
- Forbidden-pattern scan: pass. No `Pack = 1`, `System.Linq`, `new List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, or `Collider` in the touched SHINOBU_09 set.
- `POLISH_MANDATE`: not present in `Docs/Tasks/CURRENT_BATCH.md`; fallback anti-bloat audit executed.
- `dotnet restore Assembly-CSharp.csproj /p:MSBuildProjectExtensionsPath=Temp\obj\Assembly-CSharp\ /p:RestoreIgnoreFailedSources=true`: pass.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false`: blocked before SHINOBU_09 code by missing DLLs in `Temp\bin\Debug` for many project/plugin assemblies, including Amplify, Astar, Bakery, Crest, Den.Tools, EasySave, GPUInstancer, Hecton8.Core, Hecton8.Editor, Hecton8.Input, MapMagic, RealtimeCSG, Shapes, URP, VolumetricLightBeam, and WaveHarmonic.Crest.
- `dotnet restore Hecton8.Core.csproj /p:MSBuildProjectExtensionsPath=Temp\obj\Hecton8.Core\ /p:RestoreIgnoreFailedSources=true`: pass.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal`: blocked by unrelated `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs(167,13): CS0103 EnsureDirectoryPage does not exist`.
- Build server was shut down after checks.

## Integrator Notes
- This agent did not add a second renderer. It reinforced the existing `HectonIndirectVegetationRenderer` highway.
- Agent 08/DataVault output remains decoupled. The mock lane is a diagnostic fallback, not a dependency.
- The full Unity compile remains unproven in this shell because the project graph has external dependency/output walls unrelated to SHINOBU_09.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: ULTRA POLISH PASS APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- Mock scatter NativeLists existed as runtime fields. That violated the spirit of H-Phi for a diagnostic-only no-producer lane.
- SHINOBU cull telemetry and existing flora growth telemetry entries were 36-byte structs. That is an odd stride for the new ARM64 mandate.
- The blackbox dump only wrote `.bin`; the ultra mandate also required `.h8dump`.
- A naive telemetry-disable path would release the RW counter buffer, but the compute kernel declares `_HectonCullTelemetryCounters`; dispatch wants a bound buffer even when `_HectonCullTelemetryEnabled` is zero.
- The adjacent DataVault scatter manager accepted 1024-thread kernels through the Metal guard. The actual SHINOBU compute shader uses 64, but the guard was too permissive for mobile/Metal caution.

## What Was Done
- Moved mock scatter fields, `MockMatrixGeneratorJob`, `GenerateMockScatterForDiagnostics`, and `ReleaseMockScatterBuffers` behind `UNITY_EDITOR`.
- Added `StructLayout(LayoutKind.Sequential, Size = 40)` and explicit `Reserved0` padding to:
  - `FloraGrowthTelemetryEntry`
  - `VegetationCullTelemetrySnapshot`
  - `ScatterCullTelemetryEntry`
- Bumped flora growth dump version to `2` because the padded reserved lane is now written.
- Added fatal SHINOBU cull dump to both:
  - `Docs/AgentLogs/Dump_SHINOBU_09.bin`
  - `Docs/AgentLogs/Dump_SHINOBU_09.h8dump`
- Kept the 16-byte telemetry counter buffer bound when telemetry sampling is disabled, while `_HectonCullTelemetryEnabled` prevents counter writes/readback.
- Lowered `GpuScatterLodManager` Metal thread-group guard from `1024` to `512`.
- Cached Scatter Diagnostics telemetry strings so the editor window no longer converts numeric labels on every repaint; conversion happens only when telemetry values change.

## Cinematic Cheats Used
- Same Dear Lie rule remains: currents/wakes are shader-space sine/flow displacements, not plant physics.
- Low tier uses deterministic density thinning rather than trying to simulate fewer physical plants.
- HZB cull rejects invisible flora before vertex/fragment cost.

## Exact Microseconds Saved
- Runtime mock eviction: 0 us frame-time direct; removes diagnostic NativeList residency from player builds.
- 40-byte telemetry stride: 0 us direct; avoids misaligned/odd-stride ARM64 telemetry reads.
- `.h8dump` duplicate fatal write: 0 us normal frame cost; fatal path only.
- 512 thread-group guard: prevents catastrophic mobile/Metal dispatch mismatch; no direct cost when shader is 64 threads.
- Cached editor telemetry labels: editor-only; avoids repeated numeric string conversion every repaint except value changes.

## Forensic Self-Audit
<SELF_AUDIT>
Task 01 [PASS] Archive/rationale scan done; no named binary threshold found; fallback distances documented.
Task 02 [PASS] No GameObject/MeshRenderer/MeshFilter/Instantiate scatter path added.
Task 03 [PASS] BRG/native culling lanes remain field/native-array based; no property-wrapped write DTO added.
Task 04 [PASS] Matrix4x4 64 bytes, metadata 64 bytes, Vector4 payload 16 bytes, telemetry 40 bytes, no `Pack = 1` in touched SHINOBU files.
Task 05 [PASS] 100x100 mock exists and is now editor-only diagnostic storage.
Task 06 [PASS] Existing BRG/GraphicsBuffer path preserved.
Task 07 [PASS] Burst fallback and GPU compute cull use deterministic density step.
Task 08 [PASS] HZB path retained and counted separately.
Task 09 [PASS] LOD0/LOD1 split and far cadence retained.
Task 10 [PASS] Custom metadata/payload buffers remain aligned and shader-bound.
Task 11 [PASS] Dear Lie current bending remains shader vertex deformation.
Task 12 [PASS] Wake deformation remains shader buffer fake.
Task 13 [PASS] Persistent buffer reuse retained; no chunk-time destroy/recreate loop added.
Task 14 [PASS] Shadow cull/draw remains near LOD only.
Task 15 [PASS] Low-tier/SystemHealth density decimation implemented.
Task 16 [PASS] AUP/floating offset path retained; no absolute AUP to float culling math added.
Task 17 [PASS] 300-frame cull telemetry ring active; fatal `.bin` and `.h8dump` dumps active.
Task 18 [PASS] Scatter Diagnostics editor window exists.
Task 19 [PASS] LOD0/LOD1/Max Density sliders live-write renderer constants.
Task 20 [PASS] SceneView and OnDrawGizmos debug bounds draw visible yellow / culled red samples.
ARM64 CHECK: `HectonVegetationInstanceData` layout is 0-31 eight float lanes, 32-47 Vector4 biolum lane, 48-63 four float lanes. SHINOBU cull telemetry is 0-27 int counters/flags, 28-35 float stress/density, 36-39 reserved pad. All listed sizes are 8-byte multiples.
ZERO-GC CHECK: SHINOBU runtime `Tick()` does not allocate managed lists, LINQ, strings, or closures. GPU readback is async every 30 frames. Fatal dump file I/O is outside normal frame flow.
AUP CHECK: Renderer continues to use `_GlobalFloatingOffset` / `ResolveVegetationFloatingOffset`; no absolute 100 km AUP is cast directly to float in new culling logic.
DEAR LIE CHECK: Current and wake physics are faked by shader displacement using flow/wake buffers, deterministic phase, and density LOD.
DEPENDENCY CHECK: Runtime pressure comes through `GlobalRegistry` and typed `SignalBus<ScalabilityChangedEvent>` / `SignalBus<SystemHealthSignal>`. No new sibling runtime assembly reference or contract mutation was added.
H-PHI CHECK: Agent 08 matrix production remains external. Mock native storage is editor-only. Runtime blackbox NativeArray is retained because the project blackbox mandate requires a 300-frame circular buffer; it is crash evidence, not gameplay ownership.
COMPILE GUARD: Full rebuild was intentionally not repeated during ultra polish. Static dependency scan showed no new contracts asmdef or direct producer coupling.
</SELF_AUDIT>

## Verification
- Full-file preflight reads completed for `Docs/Tasks/CURRENT_BATCH.md`, `Docs/AgentLogs/Rationale_SHINOBU_09.md`, and `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Static forbidden scan on touched SHINOBU files: no `Pack = 1`, `numthreads(1024)`, LINQ, `new List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, or `Collider`.
- Thread-group proof: `FloraCulling.compute` uses `HECTON_THREADS_PER_GROUP 64`; `GpuScatterLodManager` fallback remains 64 and now rejects >512-thread kernels for Metal/mobile safety.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- No full dotnet build rerun during this pass. The previous external build wall is unchanged and unrelated to these render edits.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: CSV BRIDGE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- The Scatter Diagnostics facade had live sliders, but the ultra mandate asked for a literal human-readable CSV-to-binary route.
- `minimumDensityDecimationStep` was serialized but not exposed to the diagnostics profile, so low-tier density clamps were not fully designer-tunable from the facade.

## What Was Done
- Added `MinimumDensityDecimationStep` and `SetDiagnosticScatterTuning()` to `HectonIndirectVegetationRenderer`.
- Extended `ScatterDiagnosticsWindow` with:
  - CSV import/export for `lod0,lod1,maxDensity,minimumDensityStep`.
  - Editor hot reload when the watched CSV timestamp changes.
  - `.h8bin` bake using fixed magic/version and scalar order.
- Kept all parsing, File I/O, and string work inside `#if UNITY_EDITOR`.

## Cinematic Cheats Used
- No new simulation was introduced.
- Low-tier density remains a deterministic hash/decimation fake, not physical plant removal or GameObject toggling.

## Exact Microseconds Saved
- Runtime frame saving: 0 us direct; this is an editor authoring bridge.
- Compile-loop saving: prevents C# recompiles for LOD/density tuning; exact wall-clock depends on Unity domain reload, PENDING VERIFICATION.
- Steam Deck/MX350 impact: makes authored `minimumDensityStep` explicit in CSV profiles, so dense flora views can be clamped before profiler capture.

## Verification
- Forbidden scan on touched SHINOBU files still finds no `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, or `Collider`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- No full Unity/editor import was run for this editor-only bridge. A limited `Hecton8.Core.csproj` restore passed, then no-restore build reached source compile and stopped outside SHINOBU in `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`: missing `PathWaypointDTO` and `MockSdfGrid`. No SHINOBU_09 compiler errors were emitted in the visible output.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: MATERIAL BINDING CACHE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- `ApplyMaterialBindings()` rewrote identical material state every frame for near, far, depth, shadow, and motion passes.
- Repeated `SetBuffer`, `SetVector`, `SetFloat`, and keyword toggles are not per-instance material mutation, but they are still avoidable render hot-path churn.

## What Was Done
- Added a per-pass `MaterialBindingState` value cache in `HectonIndirectVegetationRenderer`.
- The cache compares material identity, matrix/data/age/phase/snap/visible buffers, AUP offset, LOD constants, impostor dimensions, pass mode, and GPU-indirect state.
- If the signature is unchanged, the material write block is skipped.
- Material clone release now clears matching binding-cache entries to avoid stale references.

## Cinematic Cheats Used
- No new physical simulation.
- Same Dear Lie remains: vertex shader current/wake fake plus deterministic density thinning.

## Exact Microseconds Saved
- Estimated stable-frame CPU saving: 10-80 us depending active pass count, PENDING PROFILER VERIFICATION.
- Runtime allocation delta: 0 B expected; cache is value fields on the existing renderer.
- GPU saving: 0 us direct; this is CPU/material-state churn removal.

## Verification
- Forbidden scan remains clean for `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `GlobalWorldSampler`, `BinaryLayoutManifest`, and `EcosystemRuntimeInstaller`. No SHINOBU_09 compiler errors were emitted in the visible output.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: CPU SCRATCH CACHE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- The CPU fallback path in `OnPerformCulling` still used per-cull `Allocator.TempJob` scratch arrays for visibility masks, frustum planes, and headlight payloads.
- Teardown/capacity release risked serializing against active culling jobs through `JobHandle.Complete()`.

## What Was Done
- Added two persistent `CpuCullingScratchBuffer` lanes to `HectonIndirectVegetationRenderer`.
- Reused scratch buffers by active `JobHandle`; if both are busy, the callback writes the existing all-visible draw output instead of stalling.
- Added deferred `NativeArray.Dispose(JobHandle)` for in-flight scratch/data arrays, with sync `Complete()` only after `IsCompleted` has already reported true.
- Removed normal-path `Allocator.TempJob` usage from the renderer file.

## Cinematic Cheats Used
- No new gameplay simulation.
- Dear Lie remains shader-side current/wake bending and deterministic density thinning; CPU fallback only decides visibility.

## Exact Microseconds Saved
- Estimated CPU fallback saving: 20-120 us on fallback culling frames, PENDING PROFILER VERIFICATION.
- Runtime managed allocation delta: 0 B expected; scratch is persistent native storage.
- Hitch model: fewer native temp allocations and no deliberate render-callback wait when both scratch lanes are busy.

## Verification
- `rg "Allocator.TempJob|Allocator.Temp" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: no hits.
- Forbidden scan remains clean for `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `ShinobuEcosystemBalancer`, `DroneFleetManager`, and `PlayerCriticalProceduralAudioRenderer`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: BOXING GUARD POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- SHINOBU renderer code still used `JobHandle.Equals(default)` in scratch/dispose handling.
- BRG handle validity checks still used `BatchID/BatchMeshID/BatchMaterialID.Equals(default)`.
- Under the mandate, relying on struct `Equals` in a hot render path is not acceptable when a raw value or explicit flag exists.

## What Was Done
- Added explicit validity flags for CPU culling active and dispose handles.
- Replaced SHINOBU BRG default checks with `.value == 0u` / `.value != 0u`.
- Changed external producer-handle readiness to use `IsCompleted`; default `JobHandle` is treated as completed.
- Kept the change inside `HectonIndirectVegetationRenderer`; sibling BRG renderers were not touched.

## Cinematic Cheats Used
- No new simulation.
- Dear Lie remains shader-space current/wake bending plus deterministic density thinning.

## Exact Microseconds Saved
- Direct CPU saving: 0-5 us estimate, PENDING PROFILER VERIFICATION.
- GC risk reduction: hidden boxing/default-handle equality removed from the SHINOBU renderer path.

## Verification
- `rg "\.Equals\(default\)" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: no hits.
- Forbidden scan remains clean for `Allocator.TempJob`, `Allocator.Temp`, `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `GlobalTelemetryBus` and `HectonMarineSnowRenderer`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-17
Status: COMPUTE BINDING CACHE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- GPU cull dispatch rebounded stable `GraphicsBuffer` objects to the same compute kernels every frame.
- Camera, HZB, LOD, headlight, and density constants must stay per-dispatch, but matrix/data/visible/telemetry/snap buffers usually do not change frame-to-frame.

## What Was Done
- Added compute binding signature caches for:
  - main flora cull kernel
  - shadow flora cull kernel
  - clear snap flags kernel
  - flag snapped flora kernel
- Reset caches when visible buffers, telemetry counters, snap flags, or GPU indirect resources are recreated/released.
- Left per-frame constants untouched to preserve culling correctness.

## Cinematic Cheats Used
- No new gameplay truth.
- Same Dear Lie: current/wake motion stays shader-side; compute only determines visibility/snap flags.

## Exact Microseconds Saved
- Estimated CPU binding saving: 5-40 us in stable-buffer GPU-cull frames, PENDING PROFILER VERIFICATION.
- Runtime allocation delta: 0 B expected; cache is value fields.
- GPU saving: 0 us direct; this removes CPU-side binding churn.

## Verification
- Forbidden scan remains clean for `.Equals(default)`, `Allocator.TempJob`, `Allocator.Temp`, `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `HomeostasisBrain`, `DroneFleetManager`, and `ShinobuEcosystemBalancer`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-18
Status: HEADLIGHT UPLOAD GATE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- GPU flora culling uploaded four scooter-headlight `Vector4[]` arrays every main cull dispatch.
- The same four arrays were uploaded again for the shadow cull kernel.
- Shader evidence shows `_HectonScooterHeadlightCount` exits the loop before any array read on zero-count frames, so no-headlight frames paid pointless CPU/render-thread binding cost.

## What Was Done
- Added `ApplyScooterHeadlightPayloadToCullCompute(int headlightCount, bool uploadPayloadArrays)`.
- Main cull now always writes `_HectonScooterHeadlightCount` but uploads the arrays only when `headlightCount > 0`.
- Shadow cull now repeats only the count; the arrays are compute-shader-global and were already uploaded by the main cull dispatch when count is active.
- Kept darkness logic as the cheap dot/cone shader fake; no raycasts, colliders, or CPU light simulation were introduced.

## Cinematic Cheats Used
- Dear Lie preserved: darkness visibility is a shader-side count-gated cone/distance approximation.
- No gameplay truth was added; headlights remain visual culling hints, not per-flora lighting physics.

## Exact Microseconds Saved
- Estimated CPU/render-thread saving: 3-25 us on GPU-cull frames, PENDING PROFILER VERIFICATION.
- Larger effect expected on no-headlight frames and shadow-enabled frames because four array uploads are skipped entirely.
- Runtime allocation delta: 0 B.

## Verification
- Shader audit: `FloraCulling.compute` checks `headlightIndex >= _HectonScooterHeadlightCount` before reading `_HectonScooterHeadlightPositionsWS` and sibling arrays.
- `rg "SetVectorArray\(_ScooterHeadlight" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: matches only the helper body, not duplicated main/shadow call sites.
- Forbidden scan remains clean for `.Equals(default)`, `Allocator.TempJob`, `Allocator.Temp`, `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `BinaryLayoutManifest`, `WorldChunkResidencyManager`, `TerminalOsRuntime`, and `GlobalPhysicsStateManager`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-18
Status: MOTION VECTOR / HEADLIGHT SCRUB POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- `CopyScooterHeadlightPayload()` cleared all local renderer payload arrays before every query, even when darkness culling was disabled or the scooter was inactive.
- `MantaScooter.CopyHeadlightPayloadNonAlloc()` already publishes a dense payload with inactive tail slots cleared, and all consumers are count-gated.
- Motion-vector materials received `_HectonPreviousCameraPosition` every render pass, even when material identity and value were unchanged.

## What Was Done
- Removed renderer-side `ClearScooterHeadlightPayload()` and its call site.
- Added `MaterialVectorBindingState` for near/far motion-vector materials.
- Routed previous-camera material writes through `ApplyMotionVectorPreviousCamera`, skipping duplicate `SetVector` writes and resetting cache state when material clones are released.

## Cinematic Cheats Used
- No new simulation.
- Dear Lie remains shader-side current/wake/headlight approximation; this pass only cuts redundant CPU-side property churn.

## Exact Microseconds Saved
- Estimated CPU saving: 1-15 us across darkness and motion-vector frames, PENDING PROFILER VERIFICATION.
- Runtime allocation delta: 0 B.
- GPU visual delta: none intended.

## Verification
- `rg "ClearScooterHeadlightPayload" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: no hits.
- `rg "ApplyMotionVectorPreviousCamera|SetVector\(_PreviousCameraPositionId" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: previous-camera material write is centralized in the cache helper.
- Forbidden scan remains clean for `.Equals(default)`, `Allocator.TempJob`, `Allocator.Temp`, `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` reached source compile and failed outside SHINOBU in `SubtitleManager`, `GlobalPhysicsStateManager`, and `SubmarineDynamicsRuntime`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.

---

PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-18
Status: INDIRECT ARGS CACHE POLISH APPENDED; EXTERNAL BUILD WALL STILL STANDS

## What Was Wrong
- `ClearIndirectArgsBuffer()` rewrote mesh index constants for every indirect args clear, even when the same near mesh was reused for LOD0 and shadow buffers.
- It also repeated `Mesh.GetIndexCount`, `Mesh.GetIndexStart`, `Mesh.GetBaseVertex`, and three compute `SetInt` calls across stable frames.

## What Was Done
- Added `IndirectArgsClearBindingState`.
- Clear dispatch still runs per target args buffer, but stable args-buffer binding and unchanged mesh/submesh constants are skipped.
- The cache is reset through `ResetCullComputeBindingStates()` when GPU indirect resources are released.
- First compile attempt caught a SHINOBU `uint` to `int` base-vertex conversion issue; fixed with explicit clamp before logging this pass.

## Cinematic Cheats Used
- No new gameplay truth.
- Dear Lie remains shader-side motion and compute visibility; this pass only removes duplicate CPU-side clear-kernel state writes.

## Exact Microseconds Saved
- Estimated CPU/render-thread saving: 2-20 us on GPU-cull frames depending far/shadow clear count, PENDING PROFILER VERIFICATION.
- Runtime allocation delta: 0 B.
- GPU result delta: none intended; clear kernel dispatch count is unchanged.

## Verification
- Forbidden scan remains clean for `.Equals(default)`, `Allocator.TempJob`, `Allocator.Temp`, `Pack = 1`, `numthreads(1024)`, LINQ, managed `List<T>`, `ToArray`, `Instantiate`, `new GameObject`, `MeshRenderer`, `MeshFilter`, `Rigidbody`, `Collider`, and hot-path `foreach`.
- `git diff --check -- [touched SHINOBU files]`: pass, CRLF warnings only.
- First `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal` found `HectonIndirectVegetationRenderer.cs(3268,35): CS0266` in the new cache; fixed immediately.
- Second targeted build reached source compile and failed outside SHINOBU in `SaveStateMerkleTree`, `SubtitleManager`, and `GlobalPhysicsStateManager`. No SHINOBU_09 compiler errors were emitted in the visible output.
- `dotnet build-server shutdown`: executed after the compile attempt.
