# LOG_SHINOBU_262

## 2026-05-21T15:36:53+04:00 - Crest Camera Guillotine Source Pass

What was wrong:
- Crest depth cache, planar reflection, and command-buffer routes retained hidden multi-camera assumptions: top-down depth capture, mirrored planar reflection camera, manual command-buffer builder, and prefab-enabled sea-floor/foam simulation flags.
- This violates the one-camera ocean route for Quest/Steam Deck class hardware. Every extra camera is a full scene submission, fill-rate hit, render-target churn source, and tiled-GPU bandwidth failure path.

What was done:
- Added `HectonSinglePassOceanFeature`, `OceanSinglePassRuntime`, explicit DTO contracts, layout validator, editor tuner, camera proliferation scanner, edit tests, route-card documentation, and SHINOBU_262 report section.
- Added `Hidden/Hecton8/OceanDepthFoam` fullscreen RenderGraph pass reading the primary camera depth texture and writing `_H8OceanDepthFoamMask`.
- Added `Hecton_WakeDisplacement.compute`, driven by `PropwashEventDTO` data, to accumulate wake displacement/foam into one RenderGraph-owned texture.
- Injected foam/wake/depth sampling into `Hecton_OceanSurfaceAtmosphere.hlsl` and replaced planar reflection dependence in `Hecton_StormOceanSurface.shader` with cubemap/sky-proxy reflection.
- Disabled Crest realtime depth cache and planar reflection camera paths at source and prefab level. Removed target manual render/RenderTexture tokens and changed guillotine flags to runtime `static readonly` sentinels to avoid unreachable-code compile noise.
- Reworked `BuildCommandBuffer` to stop creating/executing Crest command buffers for this route.
- Added editor/CI mock route: `GenerateMockOceanRenderState()` now publishes a cold 32-byte mock constant buffer and grants a bounded non-Play-Mode RenderGraph frame budget.
- Preserved shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` root data from SHINOBU_265 and added `shinobu_262_camera_guillotine` instead of overwriting another agent artifact.

Cinematic Cheats used:
- Depth Dear Lie: derive shoreline/depth scalar from existing `_CameraDepthTexture`, not a top-down ocean camera.
- Foam Dear Lie: Gerstner Jacobian plus screen-depth shoreline mask plus wake texture, not Crest foam sim cameras.
- Wake Dear Lie: compute ripples from unmanaged propwash events, not wake particle geometry rendered through a hidden camera.
- Reflection Dear Lie: cubemap/sky-proxy reflection blended by Fresnel and quality, not mirrored planar camera rendering.

Exact microseconds saved, pre-profiler estimate:
- Depth cache camera removal: 1000-4000 us on low/mid hardware in terrain-visible ocean frames.
- Planar reflection camera removal: 2000-8000 us where planar reflection would rerender scene geometry.
- Crest command-buffer builder purge: 120-600 us CPU/driver overhead removed in affected frames.
- Foam sim camera replacement: 500-2500 us avoided by shader math.
- Wake camera/particle RT replacement: 600-3000 us avoided by compute texture route.
- Combined ocean-heavy frame estimate: 3000-12000 us saved on i3/MX350/Quest-class hardware before Unity profiler proof.

Verification executed:
- Extracted own XML prompt by CLI regex from `Docs/Tasks/CURRENT_BATCH.md`.
- Read `AGENTS.md`, domain boundaries, selected mandates, global authority docs, and binary payload ledger.
- Static scan: no target `AddComponent<Camera>`, hidden Crest depth/reflection camera render calls, `RenderCameraWithoutCustomPasses(_camDepthCache)`, or target `new RenderTexture` tokens remain in the edited Crest/bridge files.
- Static scan: `OceanSinglePass` runtime has no LINQ, `foreach`, `Camera.main`, scene search, `Shader.SetGlobal*`, or `.Complete()`.
- Brace counts balanced for new C#/shader files.
- `git diff --check` passed on touched files; output only reported existing LF/CRLF normalization warnings.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses as JSON and includes `shinobu_262_camera_guillotine`.
- Compile was not launched: CPU gate sampled 74%, then 100%, then 100%; no dotnet/csc/VBCSCompiler processes were present, but project protocol forbids build at CPU >50%.

<SELF_AUDIT agent_id="SHINOBU_262" domain="Ocean Rendering Pipeline">
  <TASK_RECONCILIATION>
    <TASK id="01" status="[PASS]">Hidden camera source scan and targeted Crest/bridge/prefab decapitation performed.</TASK>
    <TASK id="02" status="[PASS]">Crest command-buffer builder no longer allocates/executes the killed route.</TASK>
    <TASK id="03" status="[PASS]">New DTOs use public fields only; no C# properties in hot DTO path.</TASK>
    <TASK id="04" status="[PASS]">`OceanVisualOverridesDTO` uses explicit 32-byte layout and editor/test guards.</TASK>
    <TASK id="05" status="[PASS]">Mock render state publishes real/mocked CBuffer and allows bounded editor RenderGraph execution.</TASK>
    <TASK id="06" status="[PASS]">Single-camera depth extraction pass implemented from primary camera depth.</TASK>
    <TASK id="07" status="[PASS]">Foam moved to Gerstner Jacobian, shoreline mask, and shader sampling.</TASK>
    <TASK id="08" status="[PASS]">Wake compute pass consumes Propwash event data; no wake camera route.</TASK>
    <TASK id="09" status="[PASS]">Planar reflection camera disabled; shader uses cubemap/sky Dear Lie.</TASK>
    <TASK id="10" status="[PASS]">Wake resolution scales continuously 256..1024 by `GlobalQualityWeight`.</TASK>
    <TASK id="11" status="[PASS]">32-byte CBuffer uploaded through double-buffered `GraphicsBuffer.Target.Constant`; no runtime scalar globals.</TASK>
    <TASK id="12" status="[PASS]">AUP is wrapped in double precision then cast to local float scroll offsets.</TASK>
    <TASK id="13" status="[PASS]">Visual BufferIDs `71895..71902` are presentation-only and outside rollback snapshot authority.</TASK>
    <TASK id="14" status="[PASS]">Vault buffers are requested with `NativeArrayOptions.UninitializedMemory` and overwritten by owner.</TASK>
    <TASK id="15" status="[PASS]">300-entry 64-byte telemetry ring and raw dump path implemented.</TASK>
    <TASK id="16" status="[PASS]">UI Toolkit tuner added for foam threshold, wake lifespan, shoreline fade, telemetry, mock, preview.</TASK>
    <TASK id="17" status="[PASS]">Cold `ReadOnlySpan<byte>` CSV parser writes unmanaged aesthetic DTOs.</TASK>
    <TASK id="18" status="[PASS]">Live wake texture preview implemented through runtime/global texture lookup.</TASK>
    <TASK id="19" status="[PASS]">Camera proliferation scanner and shared JSON report section implemented.</TASK>
    <TASK id="20" status="[PASS]">Self-audit, route-card, static scans, layout tests, and disk logs written; compile/runtime proof pending CPU gate.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    `OceanVisualOverridesDTO`: Size 32 bytes. Offset 0 `float4 FoamAndShadowParams` = 16 bytes. Offset 16 `float4 ShorelineDepthParams` = 16 bytes. Final size 32 = 2 * 16, ARM64 CBuffer aligned.
    `OceanGuillotineTuningDTO`: Size 64 bytes. Offsets 0/16/32 are three `float4` lanes = 48 bytes; offsets 48/52 uints = 8 bytes; offsets 56/60 floats = 8 bytes. Final size 64, one cache line.
    `OceanAestheticProfileDTO`: Size 64 bytes. Scalar fields fill offsets 0..36; offset 40 `float4 Reserved0`; offsets 56/60 uints. Final size 64.
    `OceanRenderTelemetryEntry`: Size 64 bytes. Scalars offsets 0..28; offset 32 `float4 WakeScrollOffset`; offsets 48/52 uints, 56 float, 60 uint. Final size 64, ring rows avoid false-sharing inside linear telemetry writes.
    `OceanMockRenderStateDTO`: Size 64 bytes. Three `float4` lanes at 0/16/32 plus scalar footer at 48..60.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    `GlobalQualityWeight` is sanitized and smoothed by `q*q*(3-2*q)`. Wake texture resolution resolves continuously from 256 to 1024 in 16-pixel quanta, so low quality writes about 65,536 texels and ultra writes 1,048,576 texels. Foam intensity, wake strength, shader event budget, and screen shoreline contribution lerp/smoothstep against the same continuous scalar. Below 0.3, foam collapses toward analytic shoreline/Jacobian proxy and low event budget; mid tier increases texture resolution and event count; high/ultra spends saved camera submissions on sharper wake texture and richer shader reflection/foam without changing DTO shape or gameplay truth.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Persistent native state requested from `GlobalDataVault`: `71895 OceanVisualOverridesDTO[1]`, `71896 OceanGuillotineTuningDTO[1]`, `71897 OceanRenderTelemetryEntry[300]`, `71898 int[1] telemetry cursor`, `71899 OceanAestheticProfileDTO[64]`, `71900 byte[32768] CSV scratch`, `71901 OceanMockRenderStateDTO[1]`, `71902 reserved self-audit`. No SHINOBU_262 runtime private `NativeArray`, `NativeList`, or `NativeHashMap` ownership fields were added. GPU `GraphicsBuffer` objects are cold Unity graphics resources for CBuffer/event upload, double-buffered and released by owner shutdown.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    SHINOBU_262 added no new Burst scheduled kernel, so no `NativeArray` job fields requiring `[NoAlias]` were introduced. VisualSync consumes no `JobHandle` and returns no same-frame `.Complete()`. RenderGraph consumes immutable published `GraphicsBuffer` handles and primary camera depth; output resources are `_H8OceanDepthFoamMask` and `_H8OceanWakeDisplacement`. External Propwash producer remains owner of event ring; SHINOBU_262 copies a bounded snapshot into a graphics upload buffer.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No asmdef file was edited. New runtime files currently live under the existing `Hecton8.Core` assembly scope and add no sibling asmdef reference. Source dependencies are `Hecton8.Core`, `Hecton8.Core.Memory`, existing root-scope `Hecton8.VFX` Propwash DTOs, Unity Collections/Jobs/Mathematics, and URP RenderGraph. Full Unity import compile is pending CPU gate.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: Crest-style ocean depth/foam/reflection routes scale with extra full-scene camera submissions, effectively O(C * scene geometry + RT bandwidth) where C includes hidden cameras. After: one primary camera plus O(screen half-res blit) depth mask, O(wakeTexture texels * quality event budget) compute, and O(ocean vertices/fragments) shader foam/reflection fakes. Removed hidden camera count target: 2 Crest superfluous camera routes.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## Iteration 8 - Compile-Wall Isolation Addendum

What was wrong:
- The first `OceanSinglePass` source drop sat beneath the parent `Assets/_Project/Scripts/Hecton8.Core.asmdef`. That preserved behavior but widened recompilation blast radius for a rendering-only domain.
- `Hecton8.Core.csproj` is Unity-generated and has not yet been regenerated, so a raw `dotnet build Hecton8.Core.csproj` would not prove the new files anyway.

What was done:
- Added `Assets/_Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef`.
- Added explicit references from `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` and `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef`.
- Re-ran static scans after the asmdef split: target Crest camera/render/RT token scan remains clean; `OceanSinglePass` runtime scan remains clean for LINQ, `foreach`, scene search, `Shader.SetGlobal*`, hot private native collections, and `.Complete()`.

Cinematic Cheats used:
- No new visual simulation was added. This iteration protects the existing single-camera Dear Lie route by narrowing assembly ownership.

Exact microseconds saved, pre-profiler estimate:
- Runtime: 0 us, no frame-path behavior changed.
- Editor iteration: expected compile-wall reduction is material but unmeasured until Unity import/regeneration and compiler proof are allowed by CPU gate.

Verification executed:
- Parsed the new runtime asmdef as JSON.
- Parsed editor/test asmdef references and confirmed `Hecton8.Rendering.OceanSinglePass` is present.
- CPU gate after this pass: 100%, no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe`; build remains prohibited by project protocol.

<SELF_AUDIT_DELTA agent_id="SHINOBU_262" iteration="8">
  <COMPILE_GUARD status="[PASS]">Runtime code is now isolated in `Hecton8.Rendering.OceanSinglePass.asmdef`. The assembly references Core/Core.Contracts/Core.Memory and Unity/URP packages only. It does not reference sibling runtime assemblies.</COMPILE_GUARD>
  <H_PHI_STATUS status="[UNCHANGED]">Vault buffer ownership and DTO layout unchanged.</H_PHI_STATUS>
  <DEAR_LIE_STATUS status="[UNCHANGED]">One-primary-camera RenderGraph route unchanged; no Crest depth/reflection/wake camera route restored.</DEAR_LIE_STATUS>
  <PENDING_PROOF>Unity import/Console compile and Frame Debugger/Profiler capture remain pending until CPU gate allows compiler/editor verification.</PENDING_PROOF>
</SELF_AUDIT_DELTA>

## Iteration 10 - Unity Meta Identity Addendum

What was wrong:
- New SHINOBU_262 Unity assets had source files but no committed `.meta` identity. That would let Unity generate machine-local GUIDs during import.

What was done:
- Added `.meta` files for the OceanSinglePass folder, runtime scripts, asmdef, hidden depth shader, wake compute shader, editor tools, installer, and edit test.
- Did not touch unrelated untracked Propwash/particle files and did not revert the relocated Crest pipeline validator.

Cinematic Cheats used:
- None. This is asset identity hygiene for the existing single-pass ocean route.

Exact microseconds saved, pre-profiler estimate:
- Runtime: 0 us.
- Editor/import: avoids GUID churn and broken renderer/shader references; wall-clock savings require Unity import proof.

Verification executed:
- Static search confirmed SHINOBU_262 meta GUIDs are present in the SHINOBU_262 asset set.
- Scoped `git diff --check` for meta files and OceanSinglePass assets returned no errors.

<SELF_AUDIT_DELTA agent_id="SHINOBU_262" iteration="10">
  <UNITY_META_STATUS status="[PASS]">SHINOBU_262 Unity asset identity is now stable via committed `.meta` files.</UNITY_META_STATUS>
  <OWNERSHIP_STATUS status="[PASS]">Only SHINOBU_262 assets received new `.meta` files; unrelated untracked files were left untouched.</OWNERSHIP_STATUS>
  <PENDING_PROOF>Unity import must still validate these GUIDs and regenerate csproj when CPU gate opens.</PENDING_PROOF>
</SELF_AUDIT_DELTA>

## Iteration 9 - URP Renderer Feature Installer Addendum

What was wrong:
- Asset scan showed `Assets/_Project/Data/*_Renderer.asset` did not yet contain `HectonSinglePassOceanFeature`. Without installation, URP can skip the new RenderGraph pass even though the source exists.

What was done:
- Added `Assets/_Project/Editor/SinglePassOceanRendererFeatureInstaller.cs`.
- The installer creates exactly one `HectonSinglePassOceanFeature` sub-asset for PC, PC High, Mobile, and Quest renderer assets.
- It binds `Hidden_Hecton_OceanDepthFoam.shader`, `Hecton_WakeDisplacement.compute`, and `BeforeRenderingTransparents`.
- It forces `m_RequireDepthTexture` true on the matching URP assets because the depth Dear Lie depends on the primary camera depth texture.
- It adds a build guard with callback order `-262` that installs and verifies the feature before builds.

Cinematic Cheats used:
- No new simulation. This guarantees URP executes the existing primary-camera depth Dear Lie and wake compute Dear Lie instead of silently falling back to Crest camera routes.

Exact microseconds saved, pre-profiler estimate:
- Installer itself: editor/build-time only, 0 runtime us.
- Runtime preservation: keeps the previous 3000-12000 us ocean-heavy frame saving estimate viable by ensuring the replacement route is serialized into renderer assets.

Verification executed:
- Static scan found no LINQ, `foreach`, scene search, hidden camera, dynamic RenderTexture, or CommandBuffer allocation tokens in the installer.
- Brace count for installer is balanced at 54/54.
- `git diff --check` for the installer returned no errors.
- CPU gate remains blocked at the last sample, so Unity import/install verification is still pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_262" iteration="9">
  <RENDERER_INSTALLATION status="[SOURCE_READY]">`SinglePassOceanRendererFeatureInstaller` will install and verify one `HectonSinglePassOceanFeature` per target renderer asset after Unity import.</RENDERER_INSTALLATION>
  <DEPTH_TEXTURE_GUARD status="[SOURCE_READY]">Installer/build guard forces URP `m_RequireDepthTexture` true for target pipeline assets.</DEPTH_TEXTURE_GUARD>
  <PENDING_PROOF>Actual renderer sub-asset serialization requires Unity import/Editor execution, still blocked by CPU gate.</PENDING_PROOF>
</SELF_AUDIT_DELTA>
