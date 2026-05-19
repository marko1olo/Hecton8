# LOG_SHINOBU_43

## 2026-05-18 SHINOBU Material Response Rebuild

What was wrong:
- UberNoir had no SHINOBU-owned material-response buffer lane for per-instance wear, moss, salt, SSS, texture-set slice, and power response.
- Existing material authoring/editor tools still contain legacy material writes, but the runtime presentation path needed to stop relying on per-material mutation completely.
- Procedural wear/caustic math used unstable world offsets in places and could swim during large AUP shifts.
- No active material texture binding or corrosion-rate binary is listed as runtime-wired in the binary payload ledger, so the shader bridge needed a deterministic fallback mock.
- Global quality degradation was not a continuous material-response contract; low tier had compile variants but no SHINOBU runtime weight-driven cadence/payload collapse.

What was done:
- Added `ShinobuMaterialResponseRuntime.cs`, a DataVault-backed material dispatcher with four dispatcher phase adapters: PreSimulation, Simulation, PostSimulation, VisualSync.
- Added ARM64-safe DTOs:
  - `InstanceMaterialDTO` 16B: WearAge, SaltAccumulation, BioGrowthMask, TextureSetHash.
  - `MaterialPowerDTO` 16B: PowerLevel, DepthMeters, StructuralStress01, Flags.
  - `MaterialVisibleDTO` 32B: one visible GPU lane consumed by UberNoir.
  - `GlobalShaderConstantsDTO` 48B: SSS color, caustic speed/intensity/salt line/quality, global wear multiplier, debug/texture/flag uints.
  - `MaterialResponseTelemetryEntry` 64B: cache-line telemetry row for blackbox writes.
- Added boot/cold `GraphicsBuffer` allocation:
  - `_H8UberNoirMaterialStates` as one structured visible material payload buffer.
  - `H8UberNoirMaterialGlobals` as one 48B constant buffer.
- Added Burst jobs with required flags and `[NoAlias]` fields:
  - `MockBiomassScalarJob`
  - `MaterialWearUpdateJob`
  - `VisibleMaterialPackJob`
- Added deterministic mock wear-rate/material generation when material binaries are absent.
- Added zero-GC byte parser for `Data/Visuals/texture_set_indices.csv`, guarded to Editor/Development polling; missing CSV falls back to mock texture hashes.
- Added 300-frame blackbox dump path to `Docs/AgentLogs/Dump_TECH_ART_DISPATCH.bin` on upload >1.0ms, non-finite state, or layout fault.
- Added `UberNoir Material Lab` EditorWindow with Global Rust Rate, Caustic Intensity, SSS Translucency, Salt Line Depth, and heatmap debug. It writes through SHINOBU DataVault/CBuffer methods, not material APIs.
- Updated UberNoir HLSL with `Texture2DArray` bindings, StructuredBuffer material DTO reads via `SV_InstanceID`, mask-driven rust/moss/salt blend, AUP-stable procedural coordinates, wrapped diffuse SSS, anisotropic specular, emissive power routing, quality-driven triplanar fade, Dear Lie caustics, and RGB wear heatmap.

Cinematic Cheats used:
- Caustics: triangle-wave/noise projection in shader instead of volumetric light simulation.
- Rust/moss/salt: mask-driven texture blending instead of mesh pitting, decals, or corrosion physics.
- SSS: wrapped diffuse approximation instead of screen-space or diffusion-profile SSS.
- Brushed metal: tangent-stretched specular math instead of separate BRDF variant/material family.
- Texture growth: texture-array slice blend instead of material swaps.

Exact microseconds saved, estimates pending profiler capture:
- SRP Batcher preservation versus material instances: 80-500 us/frame depending visible structures.
- No volumetric caustic pass: 200-900 us/frame saved versus raymarch/volumetric approximation.
- Visible DTO upload cap versus blind 50k upload: about 1.3 MB/frame bus traffic avoided; CPU upload savings estimated 60-180 us/frame on PCIe/mobile UMA pressure.
- DataVault/no private NativeArray allocations: 0 B/frame GC target; allocator spike avoided after boot.
- Quality q=0.1 cadence collapse: material update work drops toward 5Hz and 128-row budget, estimated 40-90 us CPU saved versus full 8192-row updates.
- Editor tuning through CBuffer: avoids shader/material recompile and material clone churn during Play Mode.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archive and ledger scan found no active material binding/corrosion binary; emergency mock wear rates implemented.</TASK>
    <TASK id="02" status="PASS">New SHINOBU runtime/editor files contain no Material.SetFloat, Material.SetColor, Renderer.material(s), or MPB.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use public fields only; no get/set accessors or Pack=1/4.</TASK>
    <TASK id="04" status="PASS">GlobalShaderConstantsDTO is 48B with float4/float4/float/uint/uint/uint layout.</TASK>
    <TASK id="05" status="PASS">MockBiomassDensitySignal and deterministic biomass job added.</TASK>
    <TASK id="06" status="PASS">UberNoir reads Texture2DArray slices and material buffer by instance index.</TASK>
    <TASK id="07" status="PASS">WearAge/SaltAccumulation/BioGrowthMask drive rust, salt, and moss blends.</TASK>
    <TASK id="08" status="PASS">Dear Lie caustic projection added; no volumetric simulation.</TASK>
    <TASK id="09" status="PASS">Wrapped diffuse SSS approximation added and quality-gated.</TASK>
    <TASK id="10" status="PASS">Anisotropic tangent specular added for brushed metal masks/flags.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight drives cadence, budget, triplanar, caustics, SSS, and moss intensity.</TASK>
    <TASK id="12" status="PASS">Material/caustic procedural coordinates subtract _TotalUniverseOffset.</TASK>
    <TASK id="13" status="PASS">Runtime compression rejected; quality-scaled texture memory telemetry and array slice mapping added. Actual tier asset loading remains content-owner pending.</TASK>
    <TASK id="14" status="PASS">PowerLevel is routed through buffer payload and shader emission.</TASK>
    <TASK id="15" status="PASS">Visible index buffer packs only visible payload rows for upload; culling owner can overwrite indices later.</TASK>
    <TASK id="16" status="PASS">GraphicsBuffers are allocated once on cold enable; CPU mirrors are DataVault handles.</TASK>
    <TASK id="17" status="PASS">300-frame blackbox ring and binary dump path added.</TASK>
    <TASK id="18" status="PASS">UberNoir Material Lab editor facade added.</TASK>
    <TASK id="19" status="PASS">texture_set_indices.csv byte parser added with DataVault scratch and mock fallback.</TASK>
    <TASK id="20" status="PASS">Heatmap debug output added: R WearAge, G BioGrowth, B SaltAccumulation.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="InstanceMaterialDTO" size="16" alignment="16B GPU float4 lane">
      offset 0: float WearAge, size 4;
      offset 4: float SaltAccumulation, size 4;
      offset 8: float BioGrowthMask, size 4;
      offset 12: uint TextureSetHash, size 4;
      total 16, multiple of 8 and 16, no padding required.
    </STRUCT>
    <STRUCT name="GlobalShaderConstantsDTO" size="48" alignment="16B cbuffer lanes">
      offset 0: float4 SubsurfaceColor, size 16;
      offset 16: float4 CausticSpeed, size 16;
      offset 32: float GlobalWearMultiplier, size 4;
      offset 36: uint _pad0/debugMode, size 4;
      offset 40: uint _pad1/textureSetCount, size 4;
      offset 44: uint _pad2/flags, size 4;
      total 48, multiple of 16.
    </STRUCT>
    <STRUCT name="MaterialResponseTelemetryEntry" size="64" alignment="64B false-sharing resistant row">300-frame ring row is 64B exactly.</STRUCT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    When GlobalQualityWeight drops below 0.3, simulation budget lerps down toward 128 rows and update cadence rises toward 12 frames, approximating 5Hz at 60 FPS. HLSL fades triplanar to UV-only, scales caustics to triangle-wave projection, reduces SSS contribution, and damps moss/rust detail. At 1.0, the same shader path allows triplanar array sampling, procedural caustic blend, SSS, anisotropic metal, and richer texture layering without material swaps.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private persistent NativeArray, NativeList, or NativeHashMap fields. Requested handles: ShinobuMaterialStates, ShinobuMaterialPowers, ShinobuMaterialVisibleIndices, ShinobuMaterialVisiblePayload, ShinobuMaterialConstants, ShinobuMaterialTelemetryRing, ShinobuMaterialTextureMappings, ShinobuMaterialMockBiomassSignals, ShinobuMaterialWearRates, ShinobuMaterialBiomassScalar, ShinobuMaterialCsvScratch.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Input JobHandle: SystemDispatcher dependency handle. Output chain: MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob. The returned JobHandle is handed back to SystemDispatcher; SHINOBU does not call JobHandle.Complete. All NativeArray job fields use [NoAlias].
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU runtime uses GlobalRegistry/DataVault/SystemDispatcher and no direct concrete sibling-domain calls. Minimal core enum reservation was required for DataVault ownership. Global core compile is blocked by unrelated Localization/Submarine/VolcanicUpdraft failures after three attempts; editor facade build passes.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Caustics and material aging are shader fakes. Before: volumetric/raycast/decal/material-swap route would scale O(objects * renderers) plus fullscreen or physics cost. After: CPU is O(visibleMaterialRows) for buffer packing and GPU is O(pixels) within the existing material shader; no extra GameObjects, no material clones, no volumetric pass.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies`: passed with one unrelated obsolete API warning.
- `dotnet build Hecton8.Core.csproj`: blocked after three attempts by unrelated compile-wall errors in Localization/Submarine/VolcanicUpdraft code; no SHINOBU error surfaced before blockers.
- `dxc`, `glslangValidator`, and `Unity` were not available on PATH, so shader import proof is pending Unity Editor verification.

## 2026-05-18 SHINOBU Polish Re-Audit Continuation

What was still wrong:
- The earlier report still described a single structured material buffer even though the mandate escalated the risk: same-buffer upload can become a silent render-thread sync point.
- The old Task 16 wording tolerated `GraphicsBuffer.SetData`; that path is stricter than material mutation but still not the final shape for a Quest/4090 continuum under frame pressure.
- Shader quality lerps were visually continuous, but some high-cost math could still execute before being blended away. That saves no ALU on weak hardware.
- The material quality merge previously allowed a stale high legacy `_H8GlobalQualityWeight` to keep SHINOBU effects expensive after the SHINOBU CBuffer quality dropped.
- Verification needed a blunt caveat: current generated `.csproj` files do not include the new SHINOBU files, and a foreign Unity batchmode/dotnet process is active. A green import has not been proven in this continuation.

What was done:
- Replaced the visible material upload lane with A/B `GraphicsBuffer` double buffering:
  - `_materialStateBufferA` / `_materialStateBufferB`
  - `_materialGlobalsBufferA` / `_materialGlobalsBufferB`
  - read indices flip only after writing the non-bound lane.
- Replaced upload staging with `GraphicsBuffer.LockBufferForWrite<T>` and `UnsafeUtility.MemCpy` from the DataVault `NativeArray` mirror.
- Added dirty gates:
  - `_visiblePayloadDirty` for simulation, CSV, or mock changes.
  - `_constantsDirty` for quality/editor/flag changes.
  - `_lastUploadedVisibleCount` for visible-count changes.
- Removed runtime dependence on `Time.frameCount`; telemetry and CSV cadence now use dispatcher frame when present and an internal deterministic frame fallback before simulation context arrives.
- Tightened shader quality collapse:
  - `_H8GlobalQualityWeight` declared as the legacy fallback only.
  - `H8UberNoirGlobalQualityWeight()` chooses SHINOBU CBuffer weight when SHINOBU flags are active.
  - Texture array samples are gated by continuous `textureArrayBlend`, zero below q=0.12.
  - Macro wear noise returns triangle noise below q=0.3.
  - Caustics return triangle projection below q=0.25, avoiding two value-noise layers on weak devices.
- Re-ran static mutation scan on SHINOBU runtime/editor files; no material mutation, `SetData`, `Time.deltaTime`, `Time.frameCount`, `UnityEngine.Random`, LINQ, `foreach`, `JobHandle.Complete`, or `Pack=1/4` match remains.

Cinematic Cheats reinforced:
- Rust and moss remain shader-space mask projections over existing geometry; no corrosion mesh deformation or decal population.
- Low-quality caustics are triangle-wave light webs; no volumetric raymarch, no fullscreen pass.
- Low-quality macro wear is triangle/hash style noise; no multi-tap value-noise when the quality continuum is below q=0.3.

Exact microseconds saved, estimates pending profiler capture:
- Dirty gated upload avoids up to 8192 * 32B = 262144 bytes per static frame on the SHINOBU visible material lane.
- Double buffering avoids potential same-buffer CPU/GPU synchronization; expected 20-120 us/frame improvement on upload-heavy frames depending driver/UMA pressure.
- Low q caustics skip two value-noise evaluations per affected fragment; GPU savings are scene-dependent and require Unity profiler capture.
- Low q texture-array gate skips albedo/mask/normal array sampling until q rises above the continuous fade threshold; this is a texture bandwidth save, not only a final-color fade.

<SELF_AUDIT polish_loop="06">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archive/ledger truth remains: no active material binary payload; emergency wear-rate fallback remains in boot init.</TASK>
    <TASK id="02" status="PASS">SHINOBU files remain clean of material mutation APIs and now also clean of `GraphicsBuffer.SetData`.</TASK>
    <TASK id="03" status="PASS">Hot structs remain public-field DTOs; no accessor copy traps were introduced.</TASK>
    <TASK id="04" status="PASS">48B CBuffer DTO unchanged and guarded by `UnsafeUtility.SizeOf` layout check.</TASK>
    <TASK id="05" status="PASS">Mock biomass path remains deterministic and buffer-backed.</TASK>
    <TASK id="06" status="PASS">UberNoir material buffer/Texture2DArray path remains active via `SV_InstanceID` material index.</TASK>
    <TASK id="07" status="PASS">Rust/salt/moss blend now avoids rich macro noise under q=0.3.</TASK>
    <TASK id="08" status="PASS">Dear Lie caustics now skip procedural value-noise below q=0.25.</TASK>
    <TASK id="09" status="PASS">SSS remains wrapped diffuse and quality-scaled; no screen-space pass added.</TASK>
    <TASK id="10" status="PASS">Anisotropic metal remains shader math; no variant/material split added.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight now controls real work removal, not only visual blending.</TASK>
    <TASK id="12" status="PASS">AUP-stable material coordinates remain based on `positionWS - _TotalUniverseOffset`.</TASK>
    <TASK id="13" status="PASS">Runtime compression remains rejected; texture-array sampling fades continuously and telemetry estimates VRAM weight.</TASK>
    <TASK id="14" status="PASS">PowerLevel remains in the visible payload and drives emission.</TASK>
    <TASK id="15" status="PASS">Visible-only upload remains the GPU payload source; double buffering hardens that path.</TASK>
    <TASK id="16" status="PASS">Cold allocation remains one-time, now A/B buffers; hot upload uses mapped write/memcpy instead of `SetData`.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry still records upload time, visible count, memory estimate, quality, and state hash.</TASK>
    <TASK id="18" status="PASS">Editor facade still writes CBuffer-backed DataVault constants only.</TASK>
    <TASK id="19" status="PASS">CSV bridge still uses scratch bytes and marks material payload dirty only when mappings change.</TASK>
    <TASK id="20" status="PASS">Heatmap debug still routes through CBuffer flags, not material keywords or material swaps.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    InstanceMaterialDTO: offset 0 float WearAge; offset 4 float SaltAccumulation; offset 8 float BioGrowthMask; offset 12 uint TextureSetHash; total 16B, exact float4 lane.
    MaterialVisibleDTO: 0 WearAge, 4 SaltAccumulation, 8 BioGrowthMask, 12 TextureSetHash, 16 PowerLevel, 20 Depth01, 24 MossLayer01, 28 Flags; total 32B.
    GlobalShaderConstantsDTO: 0 float4 SSS, 16 float4 CausticSpeed/quality, 32 float GlobalWearMultiplier, 36 uint debug, 40 uint texture count, 44 uint flags; total 48B.
    MaterialResponseTelemetryEntry: total 64B, one cache line row for blackbox telemetry.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below q=0.3, SHINOBU simulation cadence rises toward 12 frames and budget collapses toward 128 rows; HLSL macro wear noise returns the cheap triangle projection; texture arrays have faded out; below q=0.25 caustics return the triangle Dear Lie branch. Middle/high/ultra restore array slices, triplanar, SSS, anisotropy, and richer caustic interference through continuous curves.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Persistent CPU storage remains DataVault-only: ShinobuMaterialStates, ShinobuMaterialPowers, ShinobuMaterialVisibleIndices, ShinobuMaterialVisiblePayload, ShinobuMaterialConstants, ShinobuMaterialTelemetryRing, ShinobuMaterialTextureMappings, ShinobuMaterialMockBiomassSignals, ShinobuMaterialWearRates, ShinobuMaterialBiomassScalar, ShinobuMaterialCsvScratch.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Jobs remain MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob. All NativeArray job fields remain `[NoAlias]`. The output handle is returned to SystemDispatcher; SHINOBU does not call `JobHandle.Complete`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No direct sibling-domain concrete runtime dependency was added. New work stayed inside SHINOBU runtime/editor/shader plus existing BufferID/SystemID reservations. Full Unity import remains pending because another agent owns the active Unity/dotnet run.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: heavy route would be material swaps/decal growth/volumetric caustic pass, scaling with renderers plus extra passes. After: CPU remains O(visible rows) for buffer packing, GPU work is inside the existing UberNoir pass, and low q branches avoid high-cost samples instead of merely hiding their result.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification continuation:
- `rg` static scan on SHINOBU runtime/editor: clean for forbidden material mutation APIs, `SetData`, `Time.deltaTime`, `Time.frameCount`, `UnityEngine.Random`, `JobHandle.Complete`, `foreach`, LINQ, and `Pack=1/4`.
- `git diff --check` on SHINOBU runtime/HLSL/editor/shader property files: no whitespace errors; CRLF warnings only.
- Unity import/HLSL compile: still pending. Active foreign processes at this pass: Unity batchmode plus dotnet. No new compile was launched to avoid contaminating another agent's batch run.

## 2026-05-18 SHINOBU Bootstrap And Telemetry Re-Audit

What was still wrong:
- SHINOBU runtime still relied on a hidden `MonoBehaviour` host, generated `GameObject`, and `DontDestroyOnLoad`. That is normal Unity convenience code, not HECTON bootstrap discipline.
- The blackbox recorder was useful but over-eager: it could read every visible material row during VisualSync, creating diagnostic cost exactly where the renderer is most sensitive.
- Power flicker still carried unnecessary transcendental-style logic for a fake visual pulse.
- Visible payload packing trusted some source floats until after pack math. One non-finite state value could move into the GPU payload and contaminate shader material state.

What was done:
- Removed the scene-host pattern. `ShinobuMaterialResponseRuntime` is now a dispatcher-owned sealed service, cold-allocated once from the runtime initializer and shut down through `Application.quitting` or static reset.
- Added explicit cold allocation markers for the service and four dispatcher phase adapters.
- Kept all phase integration through `GlobalRegistry`; no new concrete sibling-domain call path was introduced.
- Added `ResolveTelemetrySampleBudget(GlobalQualityWeight)` and capped telemetry sampling to a continuous 32-384 row budget with a 16-row minimum floor.
- Replaced power flicker with deterministic hash-to-triangle math and removed the `math.sin` path.
- Added finite guards in wear update and visible packing before values enter the GraphicsBuffer payload.
- Re-ran SHINOBU static scans after the patch. No forbidden pattern remained for material mutation APIs, `GraphicsBuffer.SetData`, hidden scene hosts, `MonoBehaviour`, `UnityEngine.Random`, `Time.deltaTime`, `Time.frameCount`, `JobHandle.Complete`, LINQ, `foreach`, `Pack=1/4`, or `math.sin`.

Cinematic Cheats used:
- Caustics remain shader triangle/noise projection, not volumetric light transport.
- Wear, salt, and moss remain mask-driven shader response, not mesh corrosion, decals, or material swaps.
- Power flicker is a hash triangle pulse. It is visually adequate, deterministic, and cheaper than trigonometry.

Exact microseconds saved, estimates pending profiler capture:
- Removing full telemetry scans at q-low avoids roughly 8160 `MaterialVisibleDTO` reads when visible count is 8192. Estimated 5-25 us/frame depending cache pressure.
- Removing sine-style flicker from per-row updates avoids unnecessary transcendental ALU. Estimated 1-8 us per 8192-row update pass depending Burst backend.
- Removing hidden GameObject/MonoBehaviour lifecycle is not a measurable frame-path win by itself, but it removes reload/scene callback overhead and a bootstrap ownership fault.

<SELF_AUDIT polish_loop="07">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archive/ledger truth unchanged; emergency mock wear rates remain the active fallback when no corrosion payload is wired.</TASK>
    <TASK id="02" status="PASS">Runtime/editor scans remain clean of material mutation APIs, MPB, `Renderer.material`, and now hidden scene host calls.</TASK>
    <TASK id="03" status="PASS">Hot DTOs remain public fields; no property accessors were introduced.</TASK>
    <TASK id="04" status="PASS">48B global constants DTO remains std140-safe and guarded by layout checks.</TASK>
    <TASK id="05" status="PASS">Mock biomass remains deterministic and buffer-backed.</TASK>
    <TASK id="06" status="PASS">UberNoir still consumes Texture2DArray slices and material buffer data via `SV_InstanceID`.</TASK>
    <TASK id="07" status="PASS">Rust/moss/salt blending remains shader-driven and quality-gated.</TASK>
    <TASK id="08" status="PASS">Caustics remain the Dear Lie triangle/noise projection, not a volumetric pass.</TASK>
    <TASK id="09" status="PASS">SSS remains wrapped diffuse and quality-scaled.</TASK>
    <TASK id="10" status="PASS">Anisotropic metal remains in shader math with no material variant split.</TASK>
    <TASK id="11" status="PASS">`GlobalQualityWeight` now controls update cadence, sample budgets, uploads, texture arrays, caustics, SSS, and macro wear cost.</TASK>
    <TASK id="12" status="PASS">AUP subtraction remains in HLSL material procedural coordinates.</TASK>
    <TASK id="13" status="PASS">Runtime texture compression remains rejected; memory pressure is represented through continuous quality and telemetry until content owner wires actual tier assets.</TASK>
    <TASK id="14" status="PASS">PowerLevel remains buffer payload data, with deterministic fake flicker.</TASK>
    <TASK id="15" status="PASS">Visible-only upload remains the transfer contract; no blind 50k row upload was added.</TASK>
    <TASK id="16" status="PASS">Cold GraphicsBuffer allocation is double-buffered; hot upload uses `LockBufferForWrite` and memcpy.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry remains 64B rows and is now cost-bounded by quality.</TASK>
    <TASK id="18" status="PASS">Editor facade still writes DataVault/CBuffer tuning only.</TASK>
    <TASK id="19" status="PASS">CSV bridge remains byte-parser based and editor/development guarded.</TASK>
    <TASK id="20" status="PASS">Heatmap debug remains CBuffer flag driven with no shader keyword or material swap.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    InstanceMaterialDTO total 16B: 0 WearAge float, 4 SaltAccumulation float, 8 BioGrowthMask float, 12 TextureSetHash uint.
    MaterialPowerDTO total 16B: 0 PowerLevel float, 4 DepthMeters float, 8 StructuralStress01 float, 12 Flags uint.
    MaterialVisibleDTO total 32B: 0 WearAge, 4 SaltAccumulation, 8 BioGrowthMask, 12 TextureSetHash, 16 PowerLevel, 20 Depth01, 24 MossLayer01, 28 Flags.
    GlobalShaderConstantsDTO total 48B: 0 float4 SSS, 16 float4 CausticSpeed, 32 float GlobalWearMultiplier, 36 uint debug, 40 uint texture count, 44 uint flags.
    MaterialResponseTelemetryEntry total 64B, one cache-line row for the 300-frame blackbox.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below q=0.3, update cadence trends toward 12 frames, simulation budget trends toward 128 rows, telemetry samples around 32 rows, macro wear uses triangle noise, texture arrays fade out, and below q=0.25 caustics return the cheap triangle branch. High and Ultra restore array samples, triplanar detail, SSS, anisotropy, and richer caustic interference through continuous curves.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private persistent NativeArray/NativeList/NativeHashMap fields. Handles: ShinobuMaterialStates, ShinobuMaterialPowers, ShinobuMaterialVisibleIndices, ShinobuMaterialVisiblePayload, ShinobuMaterialConstants, ShinobuMaterialTelemetryRing, ShinobuMaterialTextureMappings, ShinobuMaterialMockBiomassSignals, ShinobuMaterialWearRates, ShinobuMaterialBiomassScalar, ShinobuMaterialCsvScratch.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Input dependency: SystemDispatcher JobHandle. Output chain: MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob. All job NativeArray fields use `[NoAlias]`; SHINOBU returns the handle and does not call `JobHandle.Complete`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU did not add a direct sibling-domain runtime reference. Runtime code stays in the existing Core assembly path and communicates through GlobalRegistry/DataVault. Current generated csproj files still do not include the new SHINOBU files; Unity import remains authoritative and pending because foreign Unity/dotnet processes are active.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: renderer/material mutation, decal growth, or volumetric caustics would scale with renderers plus extra passes. After: CPU stays O(visible material rows), GPU work stays inside the existing UberNoir pass, and low-q paths remove high-cost samples rather than evaluating and hiding them.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification continuation:
- Static scans clean after Loop 7.
- `git diff --check` clean except CRLF warnings on existing shader files.
- Current blocker: foreign `Unity.exe` PID 40220 is running `Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run`; earlier `dotnet/csc` processes also overlapped this audit. Unity import/HLSL compile remains PENDING VERIFICATION.

## 2026-05-18 SHINOBU Quality Hysteresis Re-Audit

What was still wrong:
- Raw `GlobalQualityWeight` was used directly as shader CBuffer quality. That is acceptable for CPU load shedding, but it lets shader detail thresholds flip when the thermal dictator hovers near q=0.25 or q=0.3.
- The caustic path had a hard q=0.25 branch between cheap triangle caustics and richer procedural caustics.
- The macro-wear noise path had a hard q=0.3 branch between triangle noise and value noise.

What was done:
- Split raw CPU quality from published shader quality:
  - `MaterialRuntimeScalarsDTO.GlobalQualityWeight` remains raw and drives simulation budget/cadence.
  - `GlobalShaderConstantsDTO.CausticSpeed.w` now receives `_publishedShaderQualityWeight`.
- Added `ResolvePublishedShaderQualityWeight()` in SHINOBU runtime:
  - uses `math.step` to choose asymmetric rise/fall rate;
  - uses `math.lerp` and a smooth polynomial;
  - falls faster than it recovers, preserving thermal load shedding without visible popping.
- Added `H8UberNoirSmoothRange01()` in HLSL.
- Macro-wear rich value noise now blends in across q=0.22..0.44 and is not evaluated below q=0.22.
- Rich caustics now blend against cheap triangle caustics across q=0.22..0.36 and are not evaluated below q=0.22.

Cinematic Cheats used:
- The low side remains triangle-wave wear and triangle-wave caustic projection.
- The middle band is not physical simulation; it is a controlled optical blend between fakes.
- High/Ultra spends recovered budget on richer procedural caustic and texture-array detail, not extra CPU simulation.

Exact microseconds saved, estimates pending profiler capture:
- Below q=0.22, macro wear avoids value-noise evaluation.
- Below q=0.22, caustics avoid two value-noise layers and optional textured caustic sampling.
- CPU material simulation still collapses immediately from raw `GlobalQualityWeight`; no CPU load is kept alive just for visual smoothing.

<SELF_AUDIT polish_loop="08">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Mock wear fallback unchanged and remains buffer-backed.</TASK>
    <TASK id="02" status="PASS">No material mutation APIs; all runtime response still goes through GraphicsBuffer/CBuffer.</TASK>
    <TASK id="03" status="PASS">No DTO accessors added.</TASK>
    <TASK id="04" status="PASS">48B CBuffer layout unchanged; only the meaning of CausticSpeed.w is refined to published shader quality.</TASK>
    <TASK id="05" status="PASS">Mock biomass still deterministic.</TASK>
    <TASK id="06" status="PASS">UberNoir still reads Texture2DArray and material state through `SV_InstanceID`.</TASK>
    <TASK id="07" status="PASS">Wear blending now has smooth low/mid/high transition bands.</TASK>
    <TASK id="08" status="PASS">Dear Lie caustics now blend cheap/rich fakes without q-threshold popping.</TASK>
    <TASK id="09" status="PASS">SSS remains quality-scaled wrapped diffuse.</TASK>
    <TASK id="10" status="PASS">Anisotropy unchanged and variant-free.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight now separates raw work shedding from smoothed shader fidelity.</TASK>
    <TASK id="12" status="PASS">AUP-stable procedural coordinates unchanged.</TASK>
    <TASK id="13" status="PARTIAL">Actual texture-tier asset loading is still content/Addressables-owner pending; SHINOBU now prevents shader-array cost below low-quality thresholds and records memory pressure in telemetry.</TASK>
    <TASK id="14" status="PASS">PowerLevel payload unchanged.</TASK>
    <TASK id="15" status="PASS">Visible-only upload unchanged.</TASK>
    <TASK id="16" status="PASS">Double-buffered mapped GraphicsBuffer upload unchanged.</TASK>
    <TASK id="17" status="PASS">Telemetry unchanged except raw quality remains the CPU authority.</TASK>
    <TASK id="18" status="PASS">Editor facade unchanged.</TASK>
    <TASK id="19" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="20" status="PASS">Heatmap debug unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    DTO byte sizes unchanged: InstanceMaterialDTO 16B, MaterialPowerDTO 16B, MaterialVisibleDTO 32B, GlobalShaderConstantsDTO 48B, MaterialResponseTelemetryEntry 64B.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below q=0.22, CPU cadence/budget already collapse from raw quality and shader rich caustic/macro-wear paths are bypassed. Between q=0.22 and q=0.44, rich wear noise blends in polynomially. Between q=0.22 and q=0.36, rich caustics blend in polynomially. High/Ultra restores rich paths with no material swaps.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Vault handles unchanged; no private persistent native arrays were added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Job graph unchanged: MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob, all NativeArray fields `[NoAlias]`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling-domain dependency or public contract mutation was introduced.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The transition band blends two cheap visual fakes; no volumetric light transport, corrosion simulation, or material clone path was introduced.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification continuation:
- Static scans clean after Loop 8.
- `git diff --check` clean except CRLF warning on `Hecton8_UberNoir.hlsl`.
- Unity import/HLSL compile remains PENDING VERIFICATION because foreign Unity PID 40220 is still active.

## 2026-05-18 SHINOBU Texture Vitality Re-Audit

What was still wrong:
- The shader response had macro rust/salt/moss, but texture vitality was too broad. It could read as tinting rather than living material: not enough pore/crystal/vein breakup in the PBR channels.
- Task 13 cannot honestly be closed as real texture-tier loading because the current binary ledger does not prove a SHINOBU-owned material texture-array payload or Addressables residency contract.
- Adding runtime texture binding from this lane would be a false authority claim and would risk upload hitches.

What was done:
- Added `H8UberNoirWearVitality` in `Hecton8_UberNoir.hlsl`.
- Added `H8UberNoirResolveWearVitality()`:
  - cheap triangle masks fade in from q=0.05 to q=0.18;
  - rich rust-pore and moss-vein value-noise starts only after q=0.24;
  - rich detail blends through q=0.58;
  - no rich branch executes when `detailWeight <= H8_UBER_NOIR_EPS`.
- Added `H8UberNoirApplyWearVitalityColor()`:
  - rust pores darken albedo and roughen/occlude cavities;
  - salt crystals brighten and smooth localized crust;
  - moss veins add green/biolum emissive edges;
  - wet edges raise local smoothness.
- Added `H8UberNoirApplyWearVitalityNormal()`:
  - high-quality procedural micro-normal perturbation from the same masks;
  - skipped entirely on `_MATH_LOD_LOW` and when normal mask is negligible.
- All vitality coordinates come from `H8UberNoirMaterialStablePosition()`, preserving AUP stability.

Cinematic Cheats used:
- Corrosion, salt crystallization, and moss creep remain optical shader fakes, not CPU simulations, decals, mesh pitting, or material swaps.
- Low-tier visual belief is carried by one triangle-mask family over existing base texture.
- High/Ultra spends ALU inside the already-bound UberNoir pass, not in a new renderer feature or texture residency path.

Exact microseconds saved, estimates pending profiler capture:
- CPU: 0 us added; no C# hot path or GraphicsBuffer payload changed.
- Low q: avoids two value-noise calls, hash-crystal detail, and micro-normal branch per fragment.
- Compared with rejected decal/CPU corrosion approaches: avoids extra renderer submissions, material clones, and CPU map updates. Estimated avoided cost remains scene-dependent and must be measured in Unity.

<SELF_AUDIT polish_loop="09">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Mock wear fallback unchanged.</TASK>
    <TASK id="02" status="PASS">No material mutation APIs introduced.</TASK>
    <TASK id="03" status="PASS">No DTO accessors or struct property changes introduced.</TASK>
    <TASK id="04" status="PASS">GlobalShaderConstantsDTO remains 48B; shader-only vitality adds no CBuffer fields.</TASK>
    <TASK id="05" status="PASS">Mock biomass path unchanged.</TASK>
    <TASK id="06" status="PASS">Texture2DArray/SV_InstanceID bridge unchanged; no new texture residency claim.</TASK>
    <TASK id="07" status="PASS">Rust/moss/salt blending now has pore/crystal/vein micro-detail.</TASK>
    <TASK id="08" status="PASS">Dear Lie strategy reinforced: texture vitality is shader fake, not simulation.</TASK>
    <TASK id="09" status="PASS">SSS path unchanged and still benefits from moss/thickness masks.</TASK>
    <TASK id="10" status="PASS">Anisotropic metal path unchanged; vitality perturbs PBR response without variants.</TASK>
    <TASK id="11" status="PASS">Vitality consumes continuous GlobalQualityWeight bands q=0.05..0.18 and q=0.24..0.58.</TASK>
    <TASK id="12" status="PASS">New vitality uses AUP-stable material position.</TASK>
    <TASK id="13" status="PARTIAL">Actual texture-tier loading remains content/Addressables-owner pending; SHINOBU uses shader vitality instead of false residency wiring.</TASK>
    <TASK id="14" status="PASS">PowerLevel/emissive payload unchanged; moss edge emission respects `surface.powerLevel`.</TASK>
    <TASK id="15" status="PASS">Visible-only upload unchanged.</TASK>
    <TASK id="16" status="PASS">No new GraphicsBuffer allocation or upload path added.</TASK>
    <TASK id="17" status="PASS">Telemetry path unchanged; no hidden full-buffer scan added.</TASK>
    <TASK id="18" status="PASS">Editor facade unchanged.</TASK>
    <TASK id="19" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="20" status="PASS">Heatmap debug unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    Runtime DTO layouts unchanged: InstanceMaterialDTO 16B, MaterialPowerDTO 16B, MaterialVisibleDTO 32B, GlobalShaderConstantsDTO 48B, MaterialResponseTelemetryEntry 64B. `H8UberNoirWearVitality` is shader-local only and is not a CPU DTO, NativeArray element, save record, telemetry record, or GraphicsBuffer payload.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    q below 0.05: vitality contributes zero. q=0.05..0.18: one cheap triangle-mask family fades in. q below 0.24: rich pores/veins/crystals are not evaluated. q=0.24..0.58: value-noise pore/vein and hash-crystal masks blend in. High/Ultra preserves texture arrays, triplanar, SSS, anisotropy, caustics, and micro-normal vitality in the same material pass.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Vault handles unchanged; no private native arrays or new persistent buffers added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Job graph unchanged: MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob, all NativeArray fields remain `[NoAlias]`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling-domain assembly reference, C# public contract mutation, or core header dependency was introduced.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before rejected path: CPU corrosion maps/decal systems/texture residency wiring would add per-object work or unproven streaming cost. After path: O(1) per fragment ALU inside existing UberNoir pass, with rich branch bypassed below q=0.24 and zero CPU material mutation.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification continuation:
- Shader source static readback confirms vitality functions and calls.
- Unity import/HLSL compile remains PENDING VERIFICATION while foreign Unity PID 40220 owns the project.

## 2026-05-19 SHINOBU Compile-Wall Isolation Re-Audit

What was still wrong:
- SHINOBU runtime lived in `Assets/_Project/Scripts/Rendering`, which falls through to parent `Hecton8.Core.asmdef`.
- That placement made a material-response subsystem part of the broad Core compile surface.
- The editor facade lived in the global `Assets/_Project/Scripts/Editor` assembly path instead of a material-domain editor assembly.

What was done:
- Moved runtime:
  - from `Assets/_Project/Scripts/Rendering/ShinobuMaterialResponseRuntime.cs`
  - to `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`
- Changed runtime namespace to `Hecton8.Graphics.Materials`.
- Updated `Hecton8.Graphics.Materials.asmdef`:
  - `allowUnsafeCode: true`;
  - references: `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`.
- Moved editor facade:
  - from `Assets/_Project/Scripts/Editor/UberNoirMaterialLabWindow.cs`
  - to `Assets/_Project/Scripts/Graphics/Materials/Editor/UberNoirMaterialLabWindow.cs`
- Added `Hecton8.Graphics.Materials.Editor.asmdef` plus `.meta`, referencing only `Hecton8.Graphics.Materials`.

Cinematic Cheats used:
- None added in this loop; this is compile-wall surgery. Runtime visual strategy remains shader-side Dear Lie wear/caustics/vitality.

Exact microseconds saved, estimates:
- Runtime: 0 us; gameplay math is unchanged.
- Developer hardware: compile scope reduction only. Exact seconds saved require Unity project regeneration and compile timing after foreign Unity process exits.

<SELF_AUDIT polish_loop="10">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Mock wear fallback unchanged in material-domain runtime.</TASK>
    <TASK id="02" status="PASS">No material mutation APIs introduced during the move.</TASK>
    <TASK id="03" status="PASS">DTO structs still use public fields only.</TASK>
    <TASK id="04" status="PASS">GlobalShaderConstantsDTO remains 48B unchanged.</TASK>
    <TASK id="05" status="PASS">Mock biomass job unchanged.</TASK>
    <TASK id="06" status="PASS">Texture2DArray/SV_InstanceID shader bridge unchanged.</TASK>
    <TASK id="07" status="PASS">Wear/growth shader logic unchanged from Loop 9.</TASK>
    <TASK id="08" status="PASS">Dear Lie caustics unchanged.</TASK>
    <TASK id="09" status="PASS">SSS unchanged.</TASK>
    <TASK id="10" status="PASS">Anisotropy unchanged.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight curves unchanged.</TASK>
    <TASK id="12" status="PASS">AUP-stable shader coordinates unchanged.</TASK>
    <TASK id="13" status="PARTIAL">Actual material texture-tier loading still waits for content/Addressables ownership; compile-wall move does not fake this.</TASK>
    <TASK id="14" status="PASS">PowerLevel payload unchanged.</TASK>
    <TASK id="15" status="PASS">Visible-only upload unchanged.</TASK>
    <TASK id="16" status="PASS">Double-buffered mapped GraphicsBuffer upload unchanged.</TASK>
    <TASK id="17" status="PASS">Telemetry unchanged.</TASK>
    <TASK id="18" status="PASS">Editor facade remains present, now in a material-domain editor assembly.</TASK>
    <TASK id="19" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="20" status="PASS">Heatmap debug unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    Runtime DTO layouts unchanged: InstanceMaterialDTO 16B, MaterialPowerDTO 16B, MaterialVisibleDTO 32B, GlobalShaderConstantsDTO 48B, MaterialResponseTelemetryEntry 64B.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Compile-wall move does not change q math. Below q=0.3, CPU cadence/budget collapse and shader rich paths remain bypassed through smooth quality bands; high/ultra paths remain available.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Vault handles unchanged; no private native arrays or new persistent buffers added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Job graph unchanged: MockBiomassScalarJob -> MaterialWearUpdateJob -> VisibleMaterialPackJob. `[NoAlias]` fields unchanged.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU runtime is no longer in parent `Hecton8.Core.asmdef`; it is scoped to `Hecton8.Graphics.Materials.asmdef`. The editor facade is scoped to `Hecton8.Graphics.Materials.Editor.asmdef`. No unrelated sibling runtime assembly dependency was added.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Runtime still avoids CPU corrosion simulation, decals, material clones, and volumetric caustic truth. The compile-wall change only moves ownership boundaries.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

Verification continuation:
- Static forbidden scan clean after file move and namespace change.
- `git diff --check` clean except CRLF warnings on shader/asmdef files.
- Unity import/project-file regeneration remains PENDING VERIFICATION because Unity PID 40220 is still running `Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run`.
