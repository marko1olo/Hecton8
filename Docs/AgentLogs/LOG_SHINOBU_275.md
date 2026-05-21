# LOG_SHINOBU_275

## 2026-05-21T12:10Z - Screen-Space Wound Decal Compressor

What was wrong:
- Existing `DynamicDecalVaultRuntime` / `DeferredDecalPass` had a useful screen-space skeleton, but its public ABI was still `DecalInstanceDTO` with `MaterialHash`, not the mandated 80-byte `VisorDecalDTO` with `DecalTypeHash` and fixed per-decal lifetime.
- Saturated buffers faded/dropped records instead of deterministic overwrite.
- The renderer assets still serialized `maxDecals` above the mandated 128 cap.
- Visor postprocessing had procedural cracks and blood edge tint, but no explicit torn-edge integration matching screen-space wound projection.
- Static proof for active `DecalProjector` purge did not exist.

What was done:
- Converted visor wound payload to explicit `VisorDecalDTO`: `float4x4 LocalToWorld` offset 0, `uint DecalTypeHash` 64, `float Opacity01` 68, `float BirthTime` 72, `uint Flags` 76; request/profile lifetime is packed into the high bits of `DecalTypeHash`.
- Capped runtime capacity at 8..128 and updated PC/High renderer assets to `HectonVisorWoundFeature`, `Hecton_VisorWounds.shader`, `maxDecals: 128`.
- Added cold `WarmupColdGlobalRoutes()` cache so RenderGraph record uses cached vault/player routes instead of polling `GlobalRegistry`.
- Retained unmanaged `SignalBus<CombatDamageSignal>` / `SignalBus<HighSpeedImpactSignal>` ingestion and AUP localization before float matrix generation.
- Replaced saturation fade/drop behavior with `TotalWritten % capacity` circular overwrite.
- Added `GenerateMockVisorWoundsJob`, `GenerateVisorDecalMatricesJob`, `DecayVisorDecalOpacityJob`, and `VisorWoundMappedUploadJob` naming/contract.
- Reworked `Hecton_DeferredDecal.shader` to consume `_GlobalVisorWounds`; added `Hecton_VisorWounds.shader` wrapper asset for the mandated shader route.
- Added glass crack refraction and procedural blood/burn/acid/scorch projection in the screen-space shader.
- Added torn visor edge mask/refraction/darken/blood coupling to `HectonVisorUberPost.shader`.
- Updated the editor tuner to Screen-Space Visor Wound Tuner and added `Assets/_Project/Data/Decals/visor_decal_profiles.csv`.
- Added `Tools/Decal_Projector_Inquisition.py`; latest static scan wrote PASS into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- Added `Docs/ARCHITECTURE/ScreenSpaceVisorWounds_SHINOBU_275.md`.

Cinematic cheats used:
- Crack visibility uses procedural vein lines and UV perturbation, not physical fracture meshes.
- Torn visor edges use radial serration and edge-band tint, not geometry or particles.
- Glass damage persistence is low-decay visual state; capacity pressure overwrites oldest records instead of simulating repair debris.
- Blood/burn/acid splats are one fullscreen projection pass with bounded DTO records, not decal projector GameObjects.

Exact microseconds saved, estimates pending profiler:
- GameObject/DecalProjector purge: 100-500 us main-thread saved under heavy wound spam.
- Removing saturation fade/drop branch: 5-30 us saved during burst enqueue.
- Aligned 80-byte DTO + direct unsafe copy: 20-80 us saved per upload burst versus defensive copies.
- `LockBufferForWrite` + `MemCpy` instead of `SetData`: 30-150 us saved depending on upload count and driver.
- Cold route cache removes registry lookup from RenderGraph record: 2-10 us saved and removes hidden authority churn.
- Low-tier capacity 8 instead of previous serialized 384/1024 sheds up to 1016 shader record checks on weak devices.

Verification:
- Static inquisition: PASS, 5815 assets scanned, 332 candidates, 0 active GameObject decal violations, 0 active active URP decal renderer features, 2 inactive URP decal renderer feature stubs.
- Source self-audit: no stale `DecalInstanceDTO`, old job names, or `_HectonDeferredDecal*` bindings in owned wound route.
- Compile: NOT RUN. Host CPU sampled at 100 and policy forbids `dotnet build` while CPU >50 or existing compiler load is present.
- Runtime/Profiler: PENDING. No Unity MCP/editor endpoint exposed in this session.

## 2026-05-21T12:28Z - Dispatcher Polish / Forensic Audit

What was wrong:
- `ExecuteVisualSync()` had been removed from `RecordRenderGraph()`, but still ran from `AddRenderPasses()`. That kept SignalBus ingestion, Vault mutation, job scheduling, and mapped upload next to render enqueue instead of a dispatcher-owned phase.
- `Hecton_VisorWounds.shader` initially depended on a `UsePass` wrapper, hiding shader ABI proof behind another shader import path.
- CPU wound AUP localization could fall back to player AUP while the shader reconstructed against the render camera.
- Status/rationale proof artifacts still contained stale wording after the shader and phase polish.

What was done:
- `DeferredDecalPass` now implements `ILateFrameTickable`.
- `AddRenderPasses()` only captures camera context, publishes a previously staged GPU buffer, checks `HasReadableFrame`, and enqueues the RenderGraph pass.
- `LateFrameTick()` runs `DynamicDecalVaultRuntime.ExecuteVisualSync()` with `SystemDispatcher.CurrentFrameDeltaTime`.
- `RecordRenderGraph()` remains a read-only compositor over the last published `GraphicsBuffer`.
- `Hecton_VisorWounds.shader` is now a standalone full pass with `_GlobalVisorWounds`, `_GlobalVisorWoundCount`, and `_GlobalVisorWoundRefractionParams`.
- `ResolveCameraAup(Camera)` now derives render camera AUP from the current runtime origin plus camera transform before falling back to player/current origin.
- Status, rationale, route card, and binary payload ledger were updated to match the dispatcher-late-frame route.

Cinematic cheats used:
- Physical visor fracture remains rejected. Cracks are procedural shard/noise lines plus UV refraction.
- Torn glass edges are an edge-band serration mask in the existing visor mega-shader, not geometry.
- Blood/acid/burn splats remain bounded screen-space records in one fullscreen pass.

Exact microseconds saved, estimates pending profiler:
- Render enqueue/record risk reduction from moving visual sync to dispatcher late-frame: 5-25 us on i3/MX350 under wound spam.
- Standalone shader ABI: no direct frame saving claimed; removes import/pass mismatch risk.
- Camera AUP correction: no direct frame saving claimed; prevents far-origin reprojection defects without extra GPU work.
- Static purge remains 100-500 us main-thread saved versus GameObject/DecalProjector spam.

Verification:
- `rg` source-only check over owned runtime/shader files found no stale `DecalInstanceDTO`, `_HectonDeferredDecal*`, old job names, `UsePass`, or `Time.deltaTime`.
- `python Tools/Decal_Projector_Inquisition.py`: PASS; 5819 scanned assets, 333 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations, 2 inactive URP decal renderer feature stubs.
- Renderer assets point `HectonVisorWoundFeature` at `Hecton_VisorWounds.shader` guid `0a2df57d7a4e4d44a95b1b4c4bfb2750`, `maxDecals: 128`.
- `git diff --check` on owned changed files: no whitespace errors; only existing line-ending warnings.
- Compile: NOT RUN. Host CPU sampled at 77 and policy forbids `dotnet build` while CPU >50.
- Runtime/Profiler/Frame Debugger: PENDING. No Unity MCP/editor endpoint exposed in this session.

<SELF_AUDIT agent_id="SHINOBU_275" domain="Echelon 8 Presentation &amp; UX / Screen-Space Wounds &amp; Decals">
  <TASK_RECONCILIATION>
    <TASK id="01" name="ADVANCED_UI_DECAL_INQUISITION" result="PASS">Audited visor post stack, render feature, vault runtime, renderer assets, and signal route.</TASK>
    <TASK id="02" name="DYNAMIC_DECAL_PROJECTOR_PURGE" result="PASS">Static scanner reports zero active GameObject decal and active URP decal renderer feature violations.</TASK>
    <TASK id="03" name="CS1612_METADATA_STATE_ANNIHILATION" result="PASS">Hot DTOs use raw public fields, explicit layout, unsafe refs/memcpy, no C# properties.</TASK>
    <TASK id="04" name="ARM64_WOUND_LAYOUT_VALIDATION" result="PASS">`VisorDecalDTO` is explicit 80B; editor/runtime guards validate total size and offsets.</TASK>
    <TASK id="05" name="EMERGENCY_MOCK_DAMAGE_DATA" result="PASS">`GenerateMockVisorWoundsJob` emits blood, glass, burn, acid, and scorch request signals.</TASK>
    <TASK id="06" name="BURST_DECAL_MATRIX_GENERATION_KERNEL" result="PASS">`GenerateVisorDecalMatricesJob` localizes AUP and constructs matrices in a Burst job.</TASK>
    <TASK id="07" name="THE_DEAR_LIE_DEFERRED_WOUNDS" result="PASS">One RenderGraph fullscreen visor wound pass replaces object decals; shader is standalone, no `UsePass`.</TASK>
    <TASK id="08" name="CIRCULAR_BUFFER_OVERWRITE_LOGIC" result="PASS">Insertion uses `TotalWritten % capacity` and bounded overwrite.</TASK>
    <TASK id="09" name="DETERMINISTIC_DECAL_DECAY" result="PASS">Opacity decay is type-aware, bounded, and thermal/quality sensitive.</TASK>
    <TASK id="10" name="ASYNCHRONOUS_GPU_BUFFER_UPLOAD" result="PASS">Double-buffered `GraphicsBuffer.LockBufferForWrite` upload is staged from dispatcher late-frame; no `SetData`.</TASK>
    <TASK id="11" name="CONTINUOUS_SCALABILITY_DENT_LIMIT" result="PASS">Active count scales continuously from 8 to 128 through smoothed `GlobalQualityWeight`.</TASK>
    <TASK id="12" name="DEGRADATION_NORMAL_PERTURBATION" result="PASS">Shader crack/refraction intensity scales through quality and `NormalRefractionIntensity`.</TASK>
    <TASK id="13" name="AUP_PRECISION_LOCALIZATION" result="PASS">Camera AUP is subtracted in double precision before float matrix construction.</TASK>
    <TASK id="14" name="ROLLBACK_NETCODE_ISOLATION" result="PASS">Presentation consumes immutable signals only and does not mutate gameplay authority.</TASK>
    <TASK id="15" name="TELEMETRY_DECAL_RECORDER" result="PASS">300-entry `VisorWoundTelemetryEntry` ring and `Dump_SHINOBU_275.bin` fault dump route exist.</TASK>
    <TASK id="16" name="WOUND_TUNER_EDITOR_WINDOW" result="PASS">Editor tuner exposes screen-space wound tuning and mock generation.</TASK>
    <TASK id="17" name="CSV_DECAL_PROFILES_INGESTOR" result="PASS">Zero-copy CSV path and `visor_decal_profiles.csv` exist for cold/editor tuning.</TASK>
    <TASK id="18" name="LIVE_MATRIX_DEBUG_GIZMO" result="PASS">Gizmo reads `VisorDecalDTO` buffer and draws active projection volumes editor-only.</TASK>
    <TASK id="19" name="ARCHITECTURAL_METRIC_VALIDATOR" result="PASS">`Decal_Projector_Inquisition.py` writes PASS into rendering optimization report.</TASK>
    <TASK id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="PASS_STATIC">Static forensic audit is written; Unity import/compile/profiler proof remains blocked/pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT name="VisorDecalDTO" size_bytes="80" alignment="16-byte matrix columns / 4-byte scalar tail">
    <FIELD name="LocalToWorld" offset="0" size="64" type="float4x4"/>
    <FIELD name="DecalTypeHash" offset="64" size="4" type="uint"/>
    <FIELD name="Opacity01" offset="68" size="4" type="float"/>
    <FIELD name="BirthTime" offset="72" size="4" type="float"/>
    <FIELD name="Flags" offset="76" size="4" type="uint"/>
    <MATH>64 + 4 + 4 + 4 + 4 = 80 bytes. 80 is divisible by 16 and keeps the matrix 16-byte aligned; scalar tail fields are 4-byte aligned. No Pack=1.</MATH>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>At quality 0.0..0.3, `Smooth01(GlobalQualityWeight)` drives active upload toward 8 records, thermal pressure accelerates decay, and shader loops break at `_GlobalVisorWoundCount`. At 0.4..0.7, the same lerp admits roughly 32..80 records with moderate refraction. At 1.0, 128 records and full refraction/torn-edge intensity are available. No gameplay truth, DTO layout, save identity, or authority route changes with quality.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault BufferIDs: 71490 `VisorDecalDTO[128]`, 71491 `VisorDecalDTO[128]` upload scratch, 71492 `DecalRuntimeStateDTO[1]`, 71493 `VisorWoundTelemetryEntry[300]`, 71494 `DecalTuningDTO[1]`, 71495 `DecalMaterialProfileDTO[256]`, 71496 `byte[16384]` CSV scratch. Runtime declares no private persistent NativeArray/NativeList/NativeHashMap. One private `NativeQueue&lt;DecalRequestSignal&gt;` is fixed, prewarmed, registered, and presentation-only.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Jobs: `GenerateVisorDecalMatricesJob -> DecayVisorDecalOpacityJob -> BuildDecalUploadBufferJob`; output handle is registered with `H8Memory` and finalized through `DispatcherJobFence.TryFinalizeCompleted` from dispatcher late-frame. Pointer fields use `[NoAlias]` on non-overlapping decal/state/upload lanes; mapped GPU upload uses `[NoAlias]` source/destination and `UnsafeUtility.MemCpy`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime asmdef reference was introduced. Visor files remain under existing `Hecton8.Core.asmdef`; added code imports Core/Core.Contracts/Memory and Unity render packages already referenced by the assembly. Guarded compile was not launched because CPU was 77 percent.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: object decals would be O(N) GameObject/renderer submissions plus material/transform churn per wound. After: O(1) fullscreen pass plus O(min(N, qualityCap)) shader record checks; CPU ingestion is bounded by fixed queue/capacity. Physical fracture meshes, particles, Canvas overlays, and DecalProjector components are rejected.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T15:46Z - Polish Loop 9 / NativeQueue Pending Fence And Route Proof

What was wrong:
- `ResetStaticState()` could dispose or reset the request `NativeQueue` before a pending visual-sync job had finalized its dequeue work.
- Public/manual/mock ingress could read `_requests.Count` or call `Enqueue` while `GenerateVisorDecalMatricesJob` was still draining that same queue.
- The route card and architecture note did not document the new fail-closed ingress behavior after the code fix.

What was done:
- `ResetStaticState()` now force-completes pending visual-sync work and unlocks runtime buffers before unregistering or disposing `_requests`.
- `TryEnqueueRequest()` and `GenerateMockVisorWounds()` now fail closed while `_pendingVisualSyncActive` is true, increment dropped-ingress telemetry, and avoid touching queue count or enqueue APIs during the scheduled dequeue window.
- `Docs/ARCHITECTURE/SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS_ROUTE_CARD.md`, `Docs/ARCHITECTURE/ScreenSpaceVisorWounds_SHINOBU_275.md`, and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now describe the pending-job queue ownership rule.

Cinematic Cheats used:
- No physical simulation or object decal fallback was added. A missed wound during a pending visual-sync window is treated as bounded presentation loss and telemetry, not as gameplay truth.

Exact Microseconds saved:
- No frame-time saving claimed. The selected route avoids a synchronous `Complete()` in normal ingress and avoids a second persistent queue allocation. The saving is stability and preservation of async visual sync under slow frames.

Verification:
- Focused static scanner remained PASS at 2026-05-21T15:49:29Z: 5824 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations, 2 inactive URP decal renderer features.
- Focused owned-route scans after the queue patch found no `DecalProjector`, `UnityEngine.Random`, direct `Time.deltaTime`, `Time.time`, `Time.frameCount`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, or `using Hecton8.World`.
- `git diff --check` over owned touched files was clean except CRLF normalization warnings.
- Compile not launched: existing `dotnet build Hecton8.slnx` PID 40460, `VBCSCompiler` PID 30152, and `csc` PID 14260 were active at 2026-05-21T15:53Z; policy requires no compiler process.
- Runtime/Profiler/Frame Debugger proof remains pending; no Unity MCP/editor endpoint is available in this session.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_loop_9_native_queue_fence">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Existing visor post route remains integrated; queue fence affects ingestion safety only.</TASK>
    <TASK id="02" result="PASS_STATIC">Scanner remains 0 active object/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No DTO properties or managed queue wrappers were introduced.</TASK>
    <TASK id="04" result="PASS_STATIC">`VisorDecalDTO` remains explicit 80B with offsets 0/64/68/72/76.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wound generation now respects pending visual-sync queue ownership.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix generation job still owns dequeue during pending execution.</TASK>
    <TASK id="07" result="PASS_STATIC">RenderGraph fullscreen wound pass unchanged.</TASK>
    <TASK id="08" result="PASS_STATIC">Ring overwrite unchanged; ingress race closure does not alter ring semantics.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay semantics unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">Async upload remains pending-job based; disposal/rebind now complete the job before queue reset.</TASK>
    <TASK id="11" result="PASS_STATIC">Quality-scaled active count unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader refraction/crack fake unchanged.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation-only route unchanged; dropped ingress is visual telemetry, not gameplay state.</TASK>
    <TASK id="15" result="PASS_STATIC">Dropped ingress remains visible through telemetry.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner ingress now fails closed during pending jobs.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV profile route unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Editor gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Scanner proof remains current at 15:49:29Z.</TASK>
    <TASK id="20" result="PASS_STATIC">Route docs and ledger now match queue ownership code; compile/runtime proof remains gated.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0; no `Pack=1`; no high-contention atomic counter in this DTO. Lifetime is packed in `DecalTypeHash` high bits.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>At low quality or thermal pressure, capacity trends toward 8 and decay pressure rises; pending ingress drops are telemetry-visible rather than forcing a blocking complete. Mid/high/ultra retain the same owner route and may process more records up to 128 when the dispatcher finalizes the async window.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault rows remain IDs 71490..71496. No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` was introduced. The fixed request `NativeQueue` is presentation ingress only and is not touched while the scheduled dequeue job owns it.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Pending job chain remains `GenerateVisorDecalMatricesJob -> DecayVisorDecalOpacityJob -> BuildDecalUploadBufferJob`; dispatcher finalization or forced reset completes the handle before unlock/reset. Non-overlapping native lanes remain `[NoAlias]`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly reference was added. Build remains gated by CPU policy.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Presentation wounds stay shader-space projection/refraction. Rejected alternatives remain object decals, fracture meshes, Canvas overlays, particles, and blocking queue synchronization in the visual route.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T21:44+04:00 - Polish Loop 12 / Type Atlas Payload Split

Canonical bottom-of-log entry after removing the earlier append-context duplicate.

What was wrong:
- The Loop 11 packed lifetime route preserved `BirthTime@72`, but the low payload byte still conflated wound type and atlas slice.
- Profile atlas data could overwrite the shader type payload, causing wrong procedural branch and decay scale for blood/glass/burn profiles.

What was done:
- Kept `VisorDecalDTO` exactly 80B: `LocalToWorld@0`, `DecalTypeHash@64`, `Opacity01@68`, `BirthTime@72`, `Flags@76`.
- Split `DecalTypeHash`: bits 0..3 wound type, bits 4..7 atlas slice, bits 8..23 lifetime centiseconds.
- Added request payload helpers and updated signal/profile ingress, matrix generation, decay type unpack, and wound shaders.
- Updated route card, architecture note, binary payload ledger, status, rationale, and rendering report.

Cinematic Cheats used:
- No physical splatter mesh, fractured glass geometry, `DecalProjector`, or Canvas overlay. The route remains one screen-space deferred wound pass plus procedural shader cracks/refraction.

Exact Microseconds saved:
- Avoided 84B/96B DTO expansion for a separate atlas field: 512B..2048B less row bandwidth per full 128-record traversal versus an expanded row.
- Retained the previously estimated 100-500 us main-thread saving under wound spam versus N GameObject/DecalProjector submissions. Loop 12 itself is semantic hardening: one nibble pack at ingress and one nibble shift in atlas shader mode.

Verification:
- Scanner PASS at `2026-05-21T17:44:17Z`: 5824 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- Stale ABI and forbidden-route scans returned empty for the owned active route.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` succeeded.
- `git diff --check` reported only CRLF normalization warnings.
- Compile not launched after Loop 12 because `dotnet` PID 24240 and `csc` PID 18692 were active and CPU sampled at 100%.

<SELF_AUDIT agent_id="SHINOBU_275" loop="12_bottom_canonical">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Existing visor post stack audited; active noir shader route and RenderGraph wound pass identified.</TASK>
    <TASK id="02" result="PASS_STATIC">Active GameObject/URP decal routes remain zero by scanner.</TASK>
    <TASK id="03" result="PASS_STATIC">Hot DTOs use explicit raw fields; no auto-properties in the wound payload path.</TASK>
    <TASK id="04" result="PASS_STATIC">Primary DTO is 80B with `BirthTime@72`; no `Pack=1`.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wounds remain unmanaged and NaN-guarded.</TASK>
    <TASK id="06" result="PASS_STATIC">Burst matrix job consumes AUP-localized unmanaged requests.</TASK>
    <TASK id="07" result="PASS_STATIC">Deferred screen-space Dear Lie replaces physical decals.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite remains modulo capacity.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay reads type nibble and packed lifetime; glass persistence remains stable.</TASK>
    <TASK id="10" result="PASS_STATIC">Double-buffered GPU upload and RenderGraph buffer import remain the route.</TASK>
    <TASK id="11" result="PASS_STATIC">Quality-controlled capacity remains continuous 8..128.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader crack/refraction perturbation remains quality-scaled.</TASK>
    <TASK id="13" result="PASS_STATIC">Impact AUP is localized against camera AUP before float math.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation route consumes signals only and does not mutate gameplay truth.</TASK>
    <TASK id="15" result="PASS_STATIC">300-frame telemetry ring remains Vault-backed.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner/gizmo facade remains editor-only.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV profile lifetime/atlas data is preserved through packed payload semantics.</TASK>
    <TASK id="18" result="PASS_STATIC">Matrix debug reads the same `VisorDecalDTO` layout.</TASK>
    <TASK id="19" result="PASS_STATIC">Scanner/report proof rerun after Loop 12.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs updated; compile rerun blocked by active compiler process and CPU policy.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0; no `Pack=1`. Bit math: type bits 0..3, atlas bits 4..7, lifetime centiseconds bits 8..23.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below `GlobalQualityWeight` 0.3, capacity collapses toward 8 records, fade pressure rises, and shader refraction amplitude lerps down while payload ABI remains fixed. Middle tiers raise record count and keep moderate crack/noise response. High/ultra tiers keep up to 128 records and stronger procedural crack/refraction. No binary quality switch was added.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Zero private persistent native arrays were added. Vault BufferIDs remain 71490 instances, 71491 upload scratch, 71492 runtime state, 71493 telemetry ring, 71494 tuning, 71495 material profiles, 71496 CSV scratch.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Matrix, decay, and upload jobs keep `[NoAlias]` on non-overlapping pointer lanes. The route consumes dispatcher late-frame dependency, schedules wound matrix/decay/upload jobs, and records the pending visual-sync handle; no same-frame render readback was introduced.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime assembly dependency was introduced in owned files. The compile state after this loop is host-policy blocked, not an owned compiler error.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Glass cracks and blood/acids are shader-space optical fakes. Before: O(N GameObjects + N renderer submissions + transform/material churn). After: O(1 fullscreen pass + bounded N<=128 buffer records). The Loop 12 split preserves this without adding another buffer or draw path.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T22:14+04:00 - Polish Loop 14 / Noir HDR Clamp And Visual Clock Closure

What was wrong:
- Removing the manual ACES curve was not enough. The active `Hecton_VisorGlitchACES.shader` still clamped the color path with `saturate(color)`, which compressed linear HDR before the URP Volume ACES owner.
- The active Noir partial used `Time.frameCount`, `Time.timeAsDouble`, and `Time.time`. Once Noir became the active serialized visor trauma path, this invalidated the focused SHINOBU_275 timing proof.

What was done:
- Replaced color-path clamps with finite/non-negative guards so raw linear HDR above 1.0 reaches URP Tonemapping. Scalar masks, UVs, quality weights, and safety gates still use `saturate`.
- Replaced direct Unity Time reads in `HectonVisorUberPostFeature.Noir.cs` with `TimeSliceScheduler.CurrentFrameId`, owner-local cold fallback frame IDs, and finite `SystemDispatcher.CurrentFrameDeltaTime` accumulation for wrapped visual phase.
- Updated `DEEP_SEA_NOIR_POST_PROCESSOR_SHINOBU_235.md`, `ScreenSpaceVisorWounds_SHINOBU_275.md`, the SHINOBU_275 route card, the binary payload ledger, status, and rationale.

Cinematic Cheats used:
- Still no physical fracture mesh, blood particle truth, fluid splatter simulation, Canvas overlay, or `DecalProjector`. Stress and visor trauma remain one pre-tonemap shader fake plus the screen-space wound pass.

Exact Microseconds saved:
- No measured frame-time saving claimed. Removing the color clamp deletes a small per-pixel clamp chain, but the real gain is preserving HDR contrast and eliminating a false timing proof. The prior 100-500 us estimate versus GameObject/DecalProjector spam remains unchanged and still awaits profiler proof.

Verification:
- Focused owned-route scan returned no `DecalProjector`, `UnityEngine.Random`, direct Unity `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, `using Hecton8.World`, `saturate(color)`, `AcesFitted`, or `ACESFilm` in the SHINOBU_275 runtime/pass/active shader files.
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T18:14:13Z`; 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json`: valid; SHINOBU_275 report timestamp is `2026-05-21T18:14:13Z`.
- `git diff --check` over touched owned files reported only CRLF normalization warnings.
- Compile not launched: CPU sampled at 100%; no `dotnet`/`csc`/`VBCSCompiler` processes were active, but AGENTS policy blocks build when CPU is above 50%.
- Runtime/Profiler/Frame Debugger proof remains pending; no Unity editor/MCP endpoint is available in this session.

<SELF_AUDIT agent_id="SHINOBU_275" loop="14_noir_hdr_clock">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Active visor post stack proof now includes the serialized Noir shader route without Unity Time or pre-ACES HDR clamp.</TASK>
    <TASK id="02" result="PASS_STATIC">Scanner remains 0 active object/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No DTO properties or managed wrappers were added.</TASK>
    <TASK id="04" result="PASS_STATIC">`VisorDecalDTO` remains explicit 80B with `BirthTime@72`.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wound route unchanged.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix generation route unchanged; timing patch affects Noir presentation phase only.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie projection remains screen-space; Noir stays pre-tonemap.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay route unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">Double-buffered GPU upload unchanged.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous quality scaling unchanged; no new binary branch or shader variant.</TASK>
    <TASK id="12" result="PASS_STATIC">Crack/torn-edge Noir shaping preserves HDR and remains quality-scaled.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">No gameplay truth, rollback state, save state, or authority route changed.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry route unchanged; scanner/report proof refreshed.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner route unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV routes unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Metric validator rerun and report timestamp updated.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs synchronized; compile gate blocked by CPU policy.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0; no `Pack=1`; no layout change in Loop 14.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, active wound count remains near 8 and fade/refraction pressure remains cheap; Noir grain/glitch/crack admission still follows continuous quality curves. Middle/high/ultra keep HDR headroom for stronger stress chroma and torn-edge/crack highlights before the single URP ACES owner. Quality does not alter payload identity or authority.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault rows remain 71490..71496. Loop 14 introduced no new persistent native allocation, no new Vault buffer, and no private `NativeArray`/`NativeList`/`NativeHashMap`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Wound jobs and `[NoAlias]` lanes unchanged. Noir timing uses dispatcher frame/delta scalars before writing the existing Noir constants CBuffer; no new job dependency or `.Complete()` was introduced.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime assembly reference. Compile was intentionally not launched because CPU was 100% under the explicit guard.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: physical or UI trauma would be O(N) object/UI submissions and material churn. After: one wound projection pass plus one pre-tonemap Noir fake; Loop 14 prevents the fake from being tone-compressed locally and keeps final color ownership in URP Volume.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>


## 2026-05-21T22:00+04:00 - Polish Loop 13 / Audit Closure And Tonemap Boundary

What was wrong:
- Read-only subagent audit found the route card did not name the retained `GlobalSignals` AUP bridge, even though `DynamicDecalVaultRuntime` uses `CurrentRuntimeOriginAup()` and `TryRuntimePositionToAup()` for camera/runtime-position localization.
- `DeferredDecalPass` inspector tooltip still said the CPU writes `DecalTypeHash` as a resolved atlas slice, contradicting the canonical bit ABI.
- A historical self-audit XML block in this LOG still used the old lifetime field name at offset 72; the active XML requires `BirthTime@72`.
- `Hecton_VisorGlitchACES.shader` applied a manual ACES curve before URP post-processing. Active Volume profiles already enable Tonemapping mode `2` with ACES preset `3`, so this was a double-tonemap risk.

What was done:
- Documented the retained `GlobalSignals.CurrentRuntimeOriginAup()` / `GlobalSignals.TryRuntimePositionToAup()` lane as a read-only AUP bridge: Core origin owner, visual-sync/camera cadence, cached player/current-origin fallback, no direct queue publishing, telemetry fault on non-finite matrices.
- Corrected the atlas tooltip to state `DecalTypeHash` bits 0..3 are wound type and bits 4..7 are atlas slice.
- Repaired the stale LOG XML to `BirthTime@72` and removed the earlier duplicate Loop 12 append, leaving one canonical bottom Loop 12 report.
- Removed the local fragment tonemap helper and its call from `Hecton_VisorGlitchACES.shader`; the shader now performs grade/glitch/crack shaping before URP Volume owns final ACES.
- Updated `ScreenSpaceVisorWounds_SHINOBU_275.md`, the SHINOBU_275 route card, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `DEEP_SEA_NOIR_POST_PROCESSOR_SHINOBU_235.md`, status, and rationale.

Cinematic Cheats used:
- No physical fracture mesh, fluid splatter, Canvas overlay, or `DecalProjector` was introduced. The wound still rides one screen-space projection/refraction pass and the Noir mega-shader still fakes stress with grain, block glitch, chroma phase, torn edges, and crack masks.

Exact Microseconds saved / risk removed:
- Removed one fragment rational ACES approximation before URP post. The exact GPU saving needs Frame Debugger/profiler proof, but the obvious cost removed is a per-pixel divide chain and saturate before the real tonemap.
- Avoided a new AUP owner interface or damage route. Documentation now matches the existing bridge instead of adding compile-wall surface.
- Prevented future inspector-driven payload misuse and ABI drift by making editor text match the bit layout.

Verification:
- Stale scan: no stale local-tonemap, legacy tooltip, lifetime-at-offset-72, or duplicate Loop 12 evidence remains in the owned route.
- AUP bridge scan: code and docs now both name `GlobalSignals.CurrentRuntimeOriginAup()` / `TryRuntimePositionToAup()` with the bridge semantics.
- Forbidden owned-route scan: no `DecalProjector`, direct `Time.*`, `UnityEngine.Random`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, or `using Hecton8.World`.
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T17:59:59Z`; 5824 scanned assets, 335 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json`: valid.
- `git diff --check`: no whitespace errors; CRLF normalization warnings only.
- Compile not launched: CPU sampled at 98.65%, violating the AGENTS build gate.

<SELF_AUDIT agent_id="SHINOBU_275" loop="13_audit_closure">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Visor post stack evidence now includes the AUP bridge and final-tonemap boundary.</TASK>
    <TASK id="02" result="PASS_STATIC">Scanner remains 0 active GameObject/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No hot DTO properties or managed payload fields were added.</TASK>
    <TASK id="04" result="PASS_STATIC">`VisorDecalDTO` proof is `BirthTime@72` in code and LOG XML.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock/data ingress unchanged; atlas tooltip now matches packed payload.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix AUP localization keeps read-only GlobalSignals bridge plus cached fallback.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie deferred wounds remain screen-space only.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay payload semantics unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">RenderGraph/GPU upload route unchanged.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous 8..128 quality capacity unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Noir crack/torn-edge integration is pre-tonemap and avoids double ACES.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP precision route is documented with owner/cadence/fallback.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation-only SignalBus damage ingress unchanged.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry/fault route unchanged and now documents AUP non-finite faults.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor facade tooltip no longer contradicts runtime ABI.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV/profile payload route unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Matrix debug layout proof unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Scanner/report proof rerun after Loop 13.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Self-audit refreshed; compile gate blocked by CPU 98.65%.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `LocalToWorld@0` 64B, `DecalTypeHash@64` 4B, `Opacity01@68` 4B, `BirthTime@72` 4B, `Flags@76` 4B; total 80B; 80 % 16 = 0; no `Pack=1`. Type bits 0..3, atlas bits 4..7, lifetime bits 8..23.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below 0.3 quality the same ABI trends toward 8 active records, reduced refraction, faster fade, and no extra tonemap pass. Mid/high/ultra increase record count and shader crack/glitch richness continuously while URP Volume remains the single final ACES owner.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs remain 71490..71496. No new persistent private `NativeArray`, `NativeList`, or `NativeHashMap` was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job chain unchanged: matrix -> decay -> upload with `[NoAlias]` pointer lanes and dispatcher-owned pending handle finalization. AUP bridge is read-only on the CPU staging side, not a job aliasing lane.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly reference was added. One Core.Contracts comment was corrected to match the existing pre-tonemap CBuffer route; compile was not launched because CPU remained above policy.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Wounds and visor stress are still optical fakes. Complexity remains O(1 fullscreen pass + bounded N<=128 records) instead of O(N GameObjects/renderers/transforms/material submissions).</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
## 2026-05-21T21:31+04:00 - Polish Loop 11 Verification Addendum

What was wrong:
- The first Loop 11 log entry still marked scanner/JSON/diff/compile verification as pending.

What was done:
- Reran stale ABI scans over active code, active architecture docs, status, and rationale.
- Reran `python Tools\Decal_Projector_Inquisition.py`; report timestamp is `2026-05-21T17:29:28Z` with 0 active GameObject/URP decal violations.
- Validated `Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` with `python -m json.tool`.
- Ran `git diff --check` over owned changed files; no whitespace errors, only CRLF normalization warnings.
- Ran one targeted compile-wall probe after CPU/compiler gates opened.

Cinematic Cheats used:
- No route changed from shader projection/refraction to physical simulation. The ABI correction did not add object decals or mesh fracture.

Exact Microseconds saved:
- Still no new frame-time saving claim for Loop 11. The practical saving is avoiding an 84B/96B DTO expansion while keeping lifetime behavior.

Verification:
- Active shader ABI scan: no shader lifetime struct field, no lifetime field read from `wound`, no unmasked atlas slice modulo.
- Active docs/runtime scan: `BirthTime@72` documented; request/profile `LifetimeSeconds` fields remain intentionally before packing.
- Forbidden owned-route scan: no `DecalProjector`, `UnityEngine.Random`, direct `Time.deltaTime`, `Time.time`, `Time.frameCount`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, or `using Hecton8.World`.
- Compile probe: `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` failed on external `ContentRuntimeServices.cs` missing `VRAMMonitor`, `VRAMPressureMonitor`, and `AssetLifecycleGovernor`; no SHINOBU_275 wound-route file appeared.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_loop_11_verification_addendum">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Visor post route remains integrated; no duplicate visual system added.</TASK>
    <TASK id="02" result="PASS_STATIC">Scanner PASS at 17:29:28Z with 0 active object/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">DTO/property scan unchanged; hot payloads are raw fields.</TASK>
    <TASK id="04" result="PASS_STATIC">80B layout is now `BirthTime@72` per XML.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock lane still feeds lifetime through request/profile then pack.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix job writes birth frame and packed lifetime without changing AUP math.</TASK>
    <TASK id="07" result="PASS_STATIC">RenderGraph wound pass consumes masked type payload.</TASK>
    <TASK id="08" result="PASS_STATIC">Ring overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay unpacks lifetime from high hash bits.</TASK>
    <TASK id="10" result="PASS_STATIC">Mapped upload ABI unchanged; compile probe found no owned errors.</TASK>
    <TASK id="11" result="PASS_STATIC">Quality/thermal curve unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader crack/refraction uses birth phase and low-byte type mask.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Rollback/gameplay authority unchanged.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry/dump stride unchanged.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV profile lifetime still affects decay through packed payload.</TASK>
    <TASK id="18" result="PASS_STATIC">Editor gizmo reads the corrected 80B layout.</TASK>
    <TASK id="19" result="PASS_STATIC">Report JSON is valid and scanner PASS is current.</TASK>
    <TASK id="20" result="PASS_STATIC">Final static audit updated; Unity runtime/profiler proof still pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `LocalToWorld@0` 64B, `DecalTypeHash@64` 4B, `Opacity01@68` 4B, `BirthTime@72` 4B, `Flags@76` 4B; total 80B; 80 % 16 = 0.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Low quality keeps the same DTO and owner route while active count trends to 8 and fade pressure rises. Mid/high/ultra increase active rows and refraction strength continuously up to 128 rows.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs remain `71490..71496`; no new persistent private native container was introduced.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job chain and `[NoAlias]` lanes unchanged; no new hot-path completion was added.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Compile remains blocked externally by content VRAM types. SHINOBU_275 files did not appear in errors.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Still one screen-space deferred pass with shader crack/refraction fakery instead of object decals or physical glass fracture.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T21:28+04:00 - Polish Loop 11 / BirthTime ABI Restoration With Packed Lifetime

What was wrong:
- The extracted SHINOBU_275 XML mandates `VisorDecalDTO.BirthTime` at offset 72, but the prior lifetime restoration had reused that field as a lifetime scalar.
- HLSL atlas/type selection previously needed explicit payload masking after lifetime entered the high bits; otherwise atlas slice selection could drift.
- Route docs and the binary payload ledger still needed to describe the exact packed lifetime contract, not just the 80B stride.

What was done:
- Restored `BirthTime@72` in `DynamicDecalVaultRuntime.cs`, `Hecton_VisorWounds.shader`, and the deprecated safety shader `Hecton_DeferredDecal.shader`.
- Added packed lifetime helpers: low nibble of `DecalTypeHash` remains wound type, bits 4..7 carry atlas slice, and bits 8..23 store sanitized lifetime centiseconds for `DecayVisorDecalOpacityJob`.
- Updated shader procedural noise to use `BirthTime` as the phase source; shader branch masks type bits 0..3 and atlas sampling reads slice bits 4..7.
- Synchronized `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, the route card, the architecture note, `Status_SHINOBU_275.md`, and `Rationale_SHINOBU_275.md`.

Cinematic Cheats used:
- No fracture mesh, physical fluid, Canvas overlay, spawned quad, or `DecalProjector` was introduced. The visual effect remains one screen-space projection pass plus shader-side crack/refraction fakery.

Exact Microseconds saved:
- No new frame-time saving claimed for this corrective loop. The avoided alternative was expanding `VisorDecalDTO` to 84/96 bytes, which would increase upload bandwidth for every active wound row; packed lifetime preserves the 80B stride and adds only bounded bit ALU.

Verification pending in this entry:
- Loop 11 static scanner, JSON validation, and diff hygiene must be rerun after this append.
- Compile remains blocked by the external missing-type wall documented in Decision 027 unless a later targeted build shows an owned SHINOBU_275 error.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_loop_11_birthtime_packed_lifetime">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Existing visor post stack remains integrated; active shader ABI corrected.</TASK>
    <TASK id="02" result="PASS_STATIC">No object decal route added by ABI fix.</TASK>
    <TASK id="03" result="PASS_STATIC">Hot DTO remains raw-field unmanaged; no C# properties added.</TASK>
    <TASK id="04" result="PASS_STATIC">`VisorDecalDTO` is 80B: matrix 0, type 64, opacity 68, birth 72, flags 76.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock request lifetime still reaches decay through packed hash; mock matrices unchanged.</TASK>
    <TASK id="06" result="PASS_STATIC">Burst matrix generation still writes camera-relative matrices and now writes `BirthTime` frame value.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie pass still consumes `_GlobalVisorWounds`; shader masks low type byte after packed lifetime.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite remains unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay keeps per-request/profile lifetime by unpacking `DecalTypeHash` high bits.</TASK>
    <TASK id="10" result="PASS_STATIC">GPU upload ABI remains 80B rows; no buffer resize or `SetData` fallback added.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous 8..128 active-count and thermal fade pressure unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader crack/refraction remains procedural; phase source is `BirthTime` instead of lifetime.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation-only SignalBus consumer route unchanged.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry ring unchanged; packed lifetime changes no dump stride.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner route unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV lifetime ingestion retained via packed lifetime, not DTO expansion.</TASK>
    <TASK id="18" result="PASS_STATIC">Editor matrix debug reads the same 80B row shape with `BirthTime@72`.</TASK>
    <TASK id="19" result="PENDING_RERUN">Static scanner must be rerun after Loop 11.</TASK>
    <TASK id="20" result="PASS_STATIC">Self-audit updated with current ABI; compile/runtime proof remains gated.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0; no `Pack=1`; no high-contention atomic counter in this DTO.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>At quality below 0.3, active projection trends toward 8 records and thermal fade pressure rises, so packed lifetime records clear faster without changing authority or layout. Mid tiers process intermediate counts. High/ultra process up to 128 records and stronger refraction/crack masks. No binary hardware switch was added.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault rows remain IDs 71490..71496. No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` was introduced. Packed lifetime uses an existing unmanaged scalar and does not create a shadow state owner.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job chain remains `GenerateVisorDecalMatricesJob -> DecayVisorDecalOpacityJob -> BuildDecalUploadBufferJob -> VisorWoundMappedUploadJob`; non-overlapping native lanes remain `[NoAlias]`. No hidden `Complete()` was introduced in the normal hot path.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly reference was added. Owned C# imports remain contract/core routed; compile gate is still blocked externally until the shared missing-type errors are fixed.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Algorithm remains O(activeDecals) inside one fullscreen pass and O(requests) for bounded Burst ingestion. Rejected object decals would be O(wounds) GameObject/renderer submissions plus hierarchy/material churn.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T15:56Z - Polish Loop 10 / Editor-Only Debug Acquire Surface

What was wrong:
- `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead` was an explicit Vault lock/unlock debug lane, but it was still visible from the runtime type in player builds.
- The route card described editor/gizmo readers as pure snapshot accessors, which did not match the live SceneView matrix gizmo implementation.

What was done:
- `DynamicDecalVaultRuntime.TryAcquireDecalBufferRead` and `ReleaseDecalBufferRead` are now compiled only under `UNITY_EDITOR`.
- `Docs/ARCHITECTURE/SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS_ROUTE_CARD.md` and `Docs/ARCHITECTURE/ScreenSpaceVisorWounds_SHINOBU_275.md` now state that public runtime `TryGet*` APIs are pure snapshots and the acquire/release buffer view is editor-only debug plumbing.

Cinematic Cheats used:
- No runtime debug GameObject, mesh, or overlay was added. Matrix proof stays in SceneView editor drawing over the same screen-space wound records.

Exact Microseconds saved:
- Runtime player cost remains 0 us for this debug surface. No frame-time saving is claimed; the value is removing an accidental player-build Vault lock API and preserving the pure read-accessor contract.

Verification:
- Focused owned-route forbidden scan returned empty after the editor-only acquire patch.
- `Tools/Decal_Projector_Inquisition.py` PASS at 2026-05-21T15:58:09Z: 5824 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations, 2 inactive URP decal renderer features.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` validates with `python -m json.tool`.
- `git diff --check` over the edited wound-route files is clean except the existing CRLF normalization warning on `DynamicDecalVaultRuntime.cs`.
- Compile-wall using audit: owned wound-route C# files import only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Contracts.Signals`, and `Hecton8.Core.Memory`; no direct `World/Gameplay/Physics/UI` sibling-domain using was found.
- DTO/job hygiene audit: no DTO auto-properties, no `Pack=1`, explicit fixed layouts remain 80/64/64/32/64/32 bytes, six mathematical jobs retain mandated Burst flags, and pointer/native lanes use `[NoAlias]` where aliasing affects vectorization.
- Hot-path managed-call audit: no direct `Time.*` in owned wound runtime/feature; `File.*` is confined to cold CSV profile load, `Debug.Log*` to editor-only layout validation, and forced completion to reset/cold mock paths.
- Shader ABI/warmup audit: `DeferredDecalPass` imports the wound `GraphicsBuffer`, declares `UseBuffer(Read)`, binds `_GlobalVisorWounds`/count/refraction globals, renderer assets bind GUIDs `0a2df57d7a4e4d44a95b1b4c4bfb2750` and `2b2a9f18d90f4b35b8b4f9d1a8e23501`, and `HectonDeferredCaustics.shadervariants` contains both. Owned wound shaders have no `multi_compile`, `shader_feature`, or `UsePass`.
- Original XML prompt re-extracted after Loop 10 using a CLI regex that handles attributes after the `id` field; task count remains 20.
- Compile attempt: CPU gate opened at 40% with no `dotnet.exe`/`csc.exe`; targeted `Hecton8.Core.csproj` build used `--disable-build-servers` and `UseSharedCompilation=false`.
- Compile result: blocked by 13 unrelated `CS0246` errors in `TerminalOsRuntime.cs`, `ContentRuntimeServices.cs`, `BulkheadContainmentJobs.cs`, `ScannerTool.cs`, and `RepairTool.cs`. No SHINOBU_275 wound-route file appeared in compiler errors.
- Runtime/Profiler/Frame Debugger proof remains pending; no Unity MCP/editor endpoint is available in this session.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_loop_10_editor_only_acquire">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Visor post integration unchanged.</TASK>
    <TASK id="02" result="PASS_STATIC">No object decal route was introduced.</TASK>
    <TASK id="03" result="PASS_STATIC">No DTO properties or managed runtime wrappers were introduced.</TASK>
    <TASK id="04" result="PASS_STATIC">`VisorDecalDTO` layout unchanged.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock ingress route unchanged.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix generation route unchanged.</TASK>
    <TASK id="07" result="PASS_STATIC">RenderGraph pass unchanged.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular ring unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">Upload route unchanged.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous quality capacity unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader fake unchanged.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation-only authority unchanged.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry route unchanged.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner keeps live matrix access, now behind editor-only compile guard.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV route unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Live matrix gizmo remains editor-only and no longer exposes its acquire method to player runtime.</TASK>
    <TASK id="19" result="PASS_STATIC">Scanner rerun PASS at 2026-05-21T15:58:09Z with 0 active GameObject/URP decal violations.</TASK>
    <TASK id="20" result="PASS_STATIC">Route card and architecture note now match C# accessor semantics.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO` remains 80B: `LocalToWorld@0` 64B, `DecalTypeHash@64` 4B, `Opacity01@68` 4B, `BirthTime@72` 4B, `Flags@76` 4B. Total 80B; 80 % 16 = 0; no `Pack=1`. Lifetime is packed into `DecalTypeHash` high bits.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No quality route changed. Low quality trends to 8 active records and stronger decay pressure; high/ultra keep up to 128 records and stronger shader refraction/crack response.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs 71490..71496 unchanged. No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job graph unchanged: `GenerateVisorDecalMatricesJob -> DecayVisorDecalOpacityJob -> BuildDecalUploadBufferJob`, with `[NoAlias]` on non-overlapping lanes.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime dependency added. Targeted guarded build reached the compiler and is blocked by unrelated missing-type errors outside SHINOBU_275 files.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Debug proof remains SceneView line drawing over screen-space records; runtime wounds remain shader-space masks/refraction instead of physical decals.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T15:30Z - Polish Reentry / Lifetime ABI Ledger Closure

What was wrong:
- Superseded note corrected: runtime and shaders now use `VisorDecalDTO.BirthTime` at offset 72; request/CSV lifetimes affect decay through packed `DecalTypeHash` high bits.
- Superseded historical context: docs previously described offset 72 as `BirthTime` while code had drifted; current code/docs now intentionally use `BirthTime@72` and packed lifetime.
- That stale proof text could cause an integrator to reintroduce visual-phase semantics and silently discard profile lifetime behavior.

What was done:
- Re-extracted the SHINOBU_275 XML prompt with a targeted CLI regex after context reentry.
- Re-read task-relevant mandates: ARM64 layout, SignalBus lanes, URP RenderGraph hot path, descriptor binding, zero-GC, and cinematic-cheat policy.
- Patched the binary payload ledger and route card so `VisorDecalDTO=80` is documented as `LocalToWorld@0`, `DecalTypeHash@64`, `Opacity01@68`, `BirthTime@72`, and `Flags@76`, with lifetime packed in `DecalTypeHash`.
- Re-ran focused stale-token and forbidden-route scans against active SHINOBU_275 docs/runtime/shaders.
- Re-ran the decal projector inquisition scanner.

Cinematic Cheats used:
- No physics path was added. Lifetime remains a scalar controlling shader-fake wound opacity/refraction persistence in the bounded screen-space pass.

Exact Microseconds saved:
- No direct frame-time saving claimed for documentation closure.
- Prevents an ABI regression where CSV/request lifetimes would be ignored, which would keep stale wounds alive too long on low-tier devices or remove persistent glass behavior on high-tier devices.

Verification:
- Superseded historical scan expected no visual-birth tokens before the XML ABI restoration. Current ABI intentionally uses `BirthTime@72` and still has no `ResolveVisualBirthSeconds` or `CurrentTime`.
- Focused owned-route `rg` found no `DecalProjector`, `UnityEngine.Random`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, or `using Hecton8.World`.
- `python Tools/Decal_Projector_Inquisition.py`: PASS at 2026-05-21T15:28:15Z; 5824 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations, 2 inactive URP decal renderer feature stubs.
- `git diff --check` over owned changed files: no whitespace errors; CRLF normalization warnings only.
- Compile not launched: CPU gate sampled 100% at 2026-05-21T15:30Z with no active `dotnet`/`csc`; policy requires CPU <=50.
- Runtime/Profiler/Frame Debugger proof remains pending; no Unity MCP/editor endpoint is available in this session.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_reentry_lifetime_abi">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Visor post stack and active shader route remain audited; documentation was resynchronized with the active route.</TASK>
    <TASK id="02" result="PASS_STATIC">Scanner remains PASS with 0 active GameObject/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">`VisorDecalDTO` remains raw-field unmanaged; no hot payload properties were introduced.</TASK>
    <TASK id="04" result="PASS_STATIC">80B layout proof now matches code/docs: offset 72 is `BirthTime`; lifetime is packed in `DecalTypeHash`.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wound lane still writes finite unmanaged requests with per-request lifetime.</TASK>
    <TASK id="06" result="PASS_STATIC">Matrix generation remains Burst/AUP-localized; no compile/runtime proof yet.</TASK>
    <TASK id="07" result="PASS_STATIC">RenderGraph fullscreen wound pass remains the object-decal replacement.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite route unchanged: `TotalWritten % capacity`.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay now has matching docs for per-decal lifetime scaling.</TASK>
    <TASK id="10" result="PASS_STATIC">Double-buffer mapped upload route unchanged; runtime proof pending.</TASK>
    <TASK id="11" result="PASS_STATIC">Capacity remains continuous 8..128 via quality.</TASK>
    <TASK id="12" result="PASS_STATIC">Crack/refraction shader fake remains quality-scaled.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization route unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Presentation-only SignalBus consumption unchanged.</TASK>
    <TASK id="15" result="PASS_STATIC">300-frame telemetry/dump route unchanged.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner route unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV profile lifetime now has code and docs agreement.</TASK>
    <TASK id="18" result="PASS_STATIC">Editor matrix gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Scanner refreshed at 15:28:15Z.</TASK>
    <TASK id="20" result="PASS_STATIC">Self-audit/log updated; compile/runtime proof still gated.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B. 80 % 16 = 0; no `Pack=1`; no false-sharing counter use. Lifetime is packed in `DecalTypeHash` high bits.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, active upload count trends toward 8, thermal pressure increases decay pressure, and shader refraction amplitude shrinks. Mid quality admits more rows smoothly. High/ultra admits up to 128 rows and stronger crack/refraction response. No DTO layout, owner route, or save/rollback identity changes with quality.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Persistent rows remain Vault IDs 71490..71496: instances, upload scratch, runtime state, telemetry ring, tuning, material profiles, CSV scratch. No private persistent NativeArray/List/HashMap was introduced.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Consumes dispatcher late-frame context plus `SignalBus<CombatDamageSignal>` / `SignalBus<HighSpeedImpactSignal>` snapshots. Outputs pending visual-sync `JobHandle` to the registered H8 memory/job fence; jobs keep `[NoAlias]` on non-overlapping DTO/state/upload lanes.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime assembly reference was added. Build was not launched because CPU was 100%.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Wounds remain shader projection/refraction. Rejected object decals/fracture meshes/Canvas/particles stay O(N) hierarchy or draw-submission routes; active path is one fullscreen pass plus bounded O(k) shader record checks where k = quality-scaled active count.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T15:03Z - Polish Loop 7 / Pure Read Accessors And Deprecated Shader Guard

What was wrong:
- `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry` still acquired DataVault locks and resolved native buffers. A method named `TryGet*` must be pure in this project; lock state mutation from an editor telemetry UI is still mutation.
- The deprecated `Hecton_DeferredDecal.shader` route still had one HLSL `normalize` call even though active assets now bind `Hecton_VisorWounds.shader`. Owned deprecated code can still be copied or rebound later, so it cannot keep a NaN exception.

What was done:
- Added immutable snapshots for tuning, runtime state, and latest telemetry inside `DynamicDecalVaultRuntime`.
- Snapshot writes occur only in owner phases already mutating authoritative buffers: default tuning seed, tuning write, visual-sync finalize, GPU upload telemetry write, telemetry push, and fault marking.
- Replaced the deprecated shader's `normalize(localPosition.xy + bias)` with explicit `dot -> max(0.0001) -> rsqrt`.
- Re-ran targeted owned-route hygiene scan and the decal projector inquisition scanner.

Cinematic Cheats used:
- No simulation was added. Cracks and wound edges remain shader-space optical fakes driven by the same bounded GPU payload.

Exact Microseconds saved:
- Snapshot accessors remove rare lock contention/debug-side mutation risk; no steady-frame saving claimed.
- Deprecated shader NaN guard is stability proof, not a frame-time claim.
- Targeted owned-route `rg` found no forbidden `DecalProjector`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, or `new NativeArray` hits.
- `python Tools/Decal_Projector_Inquisition.py` PASS at 2026-05-21T15:16:03Z; 5824 scanned assets, 335 candidates, 0 active violations, 2 inactive URP decal renderer feature stubs.
- Compile not launched: CPU gate sampled 100% / 100% / 100% at 2026-05-21T15:03Z. No `dotnet`/`csc` process was active, but policy requires CPU <=50%.
- Renderer GUID proof rerun: PC and PC_High renderer assets bind wound shader GUID `0a2df57d7a4e4d44a95b1b4c4bfb2750` and active noir shader GUID `2b2a9f18d90f4b35b8b4f9d1a8e23501`; deprecated shader path is `Hidden/Hecton8/Deprecated/DeferredDecal_SHINOBU275_DO_NOT_BIND`.
- `git diff --check` over owned changed files reports no whitespace errors; only CRLF normalization warnings.
- Rebind hygiene: `ResetColdStorageForRebind()` now resets telemetry cursor and cached camera position when it drops Vault-backed buffers.
- Compile gate resampled at 2026-05-21T15:09Z: CPU 100% / 100% / 100%, active `dotnet` PID 8956 and `csc` PID 2408. No build launched.
- Per-decal lifetime restored without ABI expansion: `VisorDecalDTO` offset 72 is `BirthTime`; request/CSV lifetime reaches the decay job through packed `DecalTypeHash` high bits. Shader procedural noise uses the birth frame as a stable visual seed.
- Targeted scan after lifetime restore found no stale offset-72 visual phase token in owned code/shaders, no owned-route forbidden tokens, and no `git diff --check` whitespace errors beyond CRLF warnings.
- Tuning revision overflow guarded: `WriteTuning()` wraps `uint.MaxValue` to revision 1 instead of 0, so seed/default detection never misclassifies a valid tuning row.
- Compile gate resampled at 2026-05-21T15:19Z: CPU 100% / 100% / 100%, no active `dotnet`/`csc`, but policy requires CPU <=50%. No build launched.

## 2026-05-21T14:48Z - Polish Loop 6 / Editor Facade And GUID Proof

What was wrong:
- Shader files were untracked and needed `.meta` GUID proof against renderer assets.
- Task 18 matrix proof still leaned on a runtime `MonoBehaviour` gizmo surface.
- Visor runtime carried a direct `Hecton8.World` namespace import.

What was done:
- Verified `Hecton_VisorGlitchACES.shader.meta` GUID `2b2a9f18d90f4b35b8b4f9d1a8e23501` and `Hecton_VisorWounds.shader.meta` GUID `0a2df57d7a4e4d44a95b1b4c4bfb2750` match serialized renderer references.
- Added a `SceneView.duringSceneGui` matrix gizmo to `ScreenSpaceDecalTunerWindow`.
- Wrapped `DynamicDecalGizmoVisualizer` in `UNITY_EDITOR`, leaving no player-build scene-component proof surface.
- Routed runtime AUP conversion through `GlobalSignals.TryRuntimePositionToAup` instead of importing `Hecton8.World`.
- Added `Hecton_VisorWounds` and `Hecton_VisorGlitchACES` pass-0 variants to bootstrap-referenced `HectonDeferredCaustics.shadervariants`.
- Verified old gizmo component GUID `149ddecab0f64e6a9d14914900000150` has no `.unity`/`.prefab`/`.asset` references under `Assets/_Project`.
- Replaced HLSL `normalize` in wound crack refraction and legacy visor edge refraction with explicit guarded `rsqrt`.

Cinematic Cheats used:
- No new simulation. The editor draws the same screen-space wound matrices already sent to the GPU, bounded 1..128, with no scene decals.

Exact Microseconds saved:
- Player runtime gizmo cost reduced to 0 us.
- Namespace cleanup is compile-wall hygiene with no frame-time claim.
- Shader warmup change moves first-use compile cost into existing boot warmup; no steady-frame saving claimed.
- Editor gizmo player-build cost remains 0 us; no scene reference means no missing-script payload from the editor-only wrapper.
- HLSL NaN guard has no claimed frame saving; it prevents zero-vector refraction offsets from producing non-finite shader state.
- Focused static hygiene scan found no active owned-route `DecalProjector`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, or `new NativeArray` hits.
- `python Tools/Decal_Projector_Inquisition.py` PASS at 2026-05-21T14:54:30Z; 5820 scanned assets, 334 candidates, 0 active violations, 2 inactive URP decal renderer feature stubs.
- Compile not launched: latest CPU gate sampled 100% with active `dotnet` PID 5188 and `csc` PID 14988; policy requires CPU <=50 and no compiler process.

## 2026-05-21T14:34Z - Polish Reentry / RenderGraph ABI And Timing Hygiene

What was wrong:
- Pending visual-sync jobs could remain tied to the next valid camera context instead of being drained at the start of dispatcher `LateFrameTick()`.
- `ExecuteVisualSync()` still had a cold-init escape hatch through `EnsureInitialized()`, which was unacceptable for a visual hot lane.
- The RenderGraph pass needed explicit imported-buffer/depth declarations, not pre-pass material mutation.
- Torn-edge/crack mega-shader work existed in the non-serialized `HectonVisorUberPost.shader` route while PC renderer assets use `Hecton_VisorGlitchACES.shader`.
- Owned runtime still used `Time.time` and `Time.frameCount` for visual phase/frame bookkeeping.

What was done:
- Added pending job drain at the top of `LateFrameTick()` and force-complete on feature dispose.
- `ExecuteVisualSync()` now requires `IsInitializedForRead()`; feature `Create()` and `IGlobalRegistryHotSwapListener` handle cold storage/player/dispatcher rebinds.
- `DeferredDecalPass.RecordRenderGraph()` now imports the `GraphicsBuffer`, declares `UseBuffer(Read)`, declares source/depth texture reads, writes a composite target, and binds `_GlobalVisorWounds` through `RasterCommandBuffer.SetGlobalBuffer`.
- `Hecton_DeferredDecal.shader` was renamed to a deprecated hidden shader path to prevent stale rebinding; `Hecton_VisorWounds.shader` remains the active wound pass.
- Ported torn-edge serration and procedural crack masks into active `Hecton_VisorGlitchACES.shader`.
- Replaced direct owned `Time.*` calls with `TimeSliceScheduler.CurrentFrameId` plus a cold fallback counter.

Cinematic cheats used:
- Helmet cracks remain procedural line masks and UV refraction, not fracture meshes.
- Torn glass edges are shader serration/darken/refraction bands, not geometry.
- Blood/burn/acid/scorch remain a bounded screen-space buffer plus one fullscreen projection pass.

Exact microseconds saved, estimates pending profiler:
- RenderGraph buffer ABI removes hidden material mutation and resource hazard; estimated 5-25 us render-record risk reduction under wound spam.
- Cold init removal from visual sync preserves the previous 2-15 us registry/vault hot-path risk reduction.
- Pending drain avoids same-frame blocking while preventing stale lock retention; no direct frame-time claim until profiler.
- Active mega-shader port removes dead-route shader work; no direct frame-time claim.
- Direct `Time.*` removal is determinism hygiene; no direct frame-time claim.

Verification:
- `python Tools/Decal_Projector_Inquisition.py`: PASS at 2026-05-21T14:34:09Z; 5819 scanned assets, 334 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations, 2 inactive URP decal renderer feature stubs.
- Targeted `rg`: no `DecalInstanceDTO`, `_HectonDeferredDecal`, `UsePass`, `math.normalize`, `Time.deltaTime`, `Time.time`, `Time.frameCount`, `AddBlitPass`, `RenderGraphUtils`, or material `SetBuffer(` tokens in owned runtime/shader route.
- `git diff --check` on owned changed files: no whitespace errors; only line-ending warnings.
- Compile: not launched. Build gate sampled CPU at 100% with no dotnet/csc processes; policy still requires CPU <=50.
- Runtime/Profiler/Frame Debugger: pending; no Unity MCP/editor endpoint available in this session.

<SELF_AUDIT agent_id="SHINOBU_275" pass="polish_reentry_static">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">UI/post stack and active renderer route audited; active noir shader route corrected.</TASK>
    <TASK id="02" result="PASS">Scanner reports zero active GameObject/URP decal routes.</TASK>
    <TASK id="03" result="PASS">Hot DTOs remain raw-field unmanaged structs; no properties in hot payloads.</TASK>
    <TASK id="04" result="PASS">`VisorDecalDTO` stays explicit 80B with offsets 0/64/68/72/76.</TASK>
    <TASK id="05" result="PASS">Mock wounds emit guarded blood/glass/burn/acid/scorch unmanaged requests.</TASK>
    <TASK id="06" result="PASS">Burst matrix generation localizes AUP against camera before float math.</TASK>
    <TASK id="07" result="PASS">RenderGraph fullscreen wound pass replaces object decals and imports its wound buffer explicitly.</TASK>
    <TASK id="08" result="PASS">Circular overwrite remains `TotalWritten % capacity`.</TASK>
    <TASK id="09" result="PASS">Decay remains Burst, bounded, material-aware, and quality/thermal-sensitive.</TASK>
    <TASK id="10" result="PASS_STATIC">Double-buffer upload path remains mapped `GraphicsBuffer.LockBufferForWrite`; runtime proof pending.</TASK>
    <TASK id="11" result="PASS">Capacity remains continuous 8..128 via `GlobalQualityWeight`.</TASK>
    <TASK id="12" result="PASS">Normal/crack/torn-edge perturbation scales continuously through quality and wound drive.</TASK>
    <TASK id="13" result="PASS">AUP conversion keeps double subtraction before float matrix write.</TASK>
    <TASK id="14" result="PASS">SignalBus consumption is presentation-only; gameplay truth is not mutated.</TASK>
    <TASK id="15" result="PASS">300-frame telemetry and dump path remain present.</TASK>
    <TASK id="16" result="PASS">Editor tuning bridge remains editor-only.</TASK>
    <TASK id="17" result="PASS">CSV profile ingestion remains cold/zero-copy over spans.</TASK>
    <TASK id="18" result="PASS">Editor gizmo reads `VisorDecalDTO` without runtime GameObjects.</TASK>
    <TASK id="19" result="PASS">Static inquisition report refreshed at 14:34:09Z.</TASK>
    <TASK id="20" result="PASS_STATIC">Forensic audit updated; compile/runtime proof still gated.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>Primary DTO `VisorDecalDTO`: `float4x4` offset 0 size 64, `uint DecalTypeHash` offset 64 size 4, `float Opacity01` offset 68 size 4, `float BirthTime` offset 72 size 4, `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0; no Pack=1. Lifetime is packed in `DecalTypeHash` high bits.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Quality 0..0.3 clamps active projection toward 8 records, stronger thermal fade, lower refraction amplitude. Mid tiers admit 32..80 records. High/ultra admits 128 and stronger procedural crack/torn-edge refraction. No binary hardware switch.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault-owned buffers remain IDs 71490..71496 for instances/upload/state/telemetry/tuning/material profiles/CSV scratch. No private persistent NativeArray/List/HashMap is declared; the fixed request queue is a prewarmed presentation ingress lane.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`GenerateVisorDecalMatricesJob -> DecayVisorDecalOpacityJob -> BuildDecalUploadBufferJob`; pending handle is registered and finalized through dispatcher fence. Non-overlapping pointer/native lanes are `[NoAlias]`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime asmdef reference. Compile not launched under CPU/build gate.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Object decals O(N) submissions were replaced by one fullscreen pass plus bounded record checks; physical cracks are shader masks/refraction.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T22:30+04:00 - Polish Loop 15 / Hot Ingress And Tiny Job Closure

What was wrong:
- Runtime public wound ingress still called `EnsureInitialized()`. A first damage impact could trigger cold `GlobalRegistry` polling, `NativeQueue` allocation/prewarm, Vault handle acquisition, and default tuning seed from a producer call.
- Active Noir one-row mock/parameter math was wrapped in synchronous `IJob.Run()` calls. That is not amortized batch work and creates false Burst proof.
- The touched `HectonVisorUberPostFeature.cs` host file still used `Time.frameCount` for reconstruction telemetry, a then-existing fluid path, and depthless-TBDR cache cadence.

What was done:
- `TryEnqueueRuntimeImpact()` and `TryEnqueueAupImpact()` now fail closed on `IsInitializedForRead()` and cannot create cold storage from runtime ingress.
- `HectonVisorUberPostFeature` now implements `ILateFrameTickable`; Noir CBuffer generation/upload runs from `LateFrameTick`, while `AddRenderPasses()` only consumes the last valid double-buffered constant buffer.
- Removed the one-row Noir `IJob.Run()` wrappers and converted their math into direct scalar owner-phase methods.
- Replaced host-file `Time.frameCount` reads with dispatcher frame source through `ResolveNoirFrameId()` / `NoirFrameToIndex()`.
- Updated the SHINOBU_235 Noir note, SHINOBU_275 architecture note, route card, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- No object decals, fracture meshes, Canvas blood, particles, or physical splatter simulation. The wound route remains a bounded buffer plus fullscreen projection; Noir remains a pre-tonemap stress/glitch/crack fake.

Exact Microseconds saved:
- Prevents an unbounded first-impact cold spike on low-end hardware by refusing ingress until the route is already initialized.
- Removes tiny job scheduling/Run overhead for one CBuffer row; no measured CPU number is claimed without Profiler.
- Prior object-decal rejection estimate remains 100-500 us under wound spam, still pending runtime proof.

Verification:
- Focused route scan returned no `DecalProjector`, `UnityEngine.Random`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, `using Hecton8.World`, `saturate(color)`, `AcesFitted`, or `ACESFilm` in the touched wound/noir route and active shaders.
- Tiny Noir job scan returned no `.Run(`, `IJob`, or `BurstCompile` in `HectonVisorUberPostFeature.Noir.cs` / `HectonVisorUberPostFeature.cs`.
- Runtime ingress scan shows `TryEnqueueRuntimeImpact()` and `TryEnqueueAupImpact()` using `IsInitializedForRead()`, not `EnsureInitialized()`.
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T18:30:13Z`; 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json`: valid.
- Compile not launched: CPU sampled at 100%; no compiler processes were active, but AGENTS policy blocks build above 50% CPU.

<SELF_AUDIT agent_id="SHINOBU_275" loop="15_hot_ingress_tiny_job">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">RenderGraph/GPU route re-audited by subagent and local scan.</TASK>
    <TASK id="02" result="PASS_STATIC">Active decal projector violations remain zero by scanner.</TASK>
    <TASK id="03" result="PASS_STATIC">No DTO properties or managed wrappers were added.</TASK>
    <TASK id="04" result="PASS_STATIC">Primary DTO remains explicit 80B; no `Pack=1`.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wound generation remains cold/editor; runtime ingress cannot cold-init.</TASK>
    <TASK id="06" result="PASS_STATIC">Batched wound Burst jobs remain; one-row Noir jobs were removed.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie screen-space route unchanged.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay route unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">Noir and wound GPU uploads remain double-buffered mapped writes; RenderGraph consumes declared buffers.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous quality capacity/fidelity curve unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader perturbation/HDR route unchanged from Loop 14.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">Gameplay truth and rollback authority unchanged; ingress fail-closed affects presentation only.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry route unchanged; scanner/report proof refreshed.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor tuner/mock lanes remain the only cold mock initialization path.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV/profile load remains cold and unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Debug gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Metric validator rerun and report timestamp updated.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs synchronized; compile gate blocked by CPU policy.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0. Loop 15 changed no DTO bytes.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, wound capacity still trends toward 8 records and shader/refraction intensity collapses continuously; runtime ingress now fails closed if the presentation route is cold instead of paying cold initialization. Middle/high/ultra keep larger capacity and richer Noir/wound shader response. Quality still never changes DTO layout, save identity, or authority route.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault BufferIDs remain 71490 instances, 71491 upload scratch, 71492 runtime state, 71493 telemetry ring, 71494 tuning, 71495 material profiles, 71496 CSV scratch. No new private persistent native collection was introduced.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Wound jobs retain their existing `[NoAlias]` lanes and pending visual-sync handle chain. Noir no longer contributes a tiny job dependency; it writes one CBuffer row from owner `LateFrameTick` and RenderGraph reads the published buffer.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime assembly reference was added. Build remains blocked by host CPU guard.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: physical wound truth or object decals would be O(N) scene/render work and a cold first-impact path could allocate. After: cold-initialized bounded buffer plus one fullscreen projection; hot ingress is O(1) fail-closed if not ready. Noir pressure/stress remains direct scalar CBuffer math plus shader fake.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T22:35+04:00 - Polish Loop 16 / Evidence Ordering And Proof Refresh

What was wrong:
- The Loop 15 report existed, but it was not the final block in `LOG_SHINOBU_275.md`; older Loop 7/6/reentry records still followed it.
- That violates the top-old/bottom-new reporting protocol and weakens the evidence trail even when the source patch is valid.

What was done:
- Moved the single Loop 15 report block to EOF, then verified there is no duplicate Loop 15 block.
- Reran the focused forbidden-token scan over the touched wound/noir route and active shaders.
- Reran the tiny Noir job scan over the Noir partial and host feature file.
- Reran the runtime ingress scan for `TryEnqueueRuntimeImpact()` and `TryEnqueueAupImpact()`.
- Reran `python Tools\Decal_Projector_Inquisition.py` and JSON validation for `Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json`.
- Resampled the compile gate instead of launching a build.

Cinematic Cheats used:
- No runtime simulation change. The wound route remains the same screen-space projection fake and Noir remains scalar CBuffer plus shader trauma/glitch fake.

Exact Microseconds saved:
- Evidence-ordering pass is proof-only; no new frame-time saving is claimed.
- Scanner PASS refreshed at `2026-05-21T18:35:14Z`: 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- Focused forbidden scan returned no `DecalProjector`, `UnityEngine.Random`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, `using Hecton8.World`, `saturate(color)`, `AcesFitted`, or `ACESFilm` in the touched route.
- Tiny Noir job scan returned no `.Run(`, `IJob`, or `BurstCompile` in `HectonVisorUberPostFeature.Noir.cs` / `HectonVisorUberPostFeature.cs`.
- Runtime ingress scan shows public runtime/AUP impact ingress gates on `IsInitializedForRead()`; remaining `EnsureInitialized()` calls are cold/editor/mock/CSV/fault lanes.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates.
- Compile not launched: CPU sampled at 100%; no compiler processes were active, but AGENTS policy blocks build above 50% CPU.

## 2026-05-21T22:42+04:00 - Polish Loop 17 / Cached Player Snapshot Host Route

What was wrong:
- `HectonVisorUberPostFeature` still had a direct `Hecton8.Gameplay` namespace import.
- Its shared host runtime-state path still called `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` from render enqueue, then read concrete player context/survival objects as a fallback.

What was done:
- Removed the direct `Hecton8.Gameplay` using from the touched host file.
- Replaced the static player-context fallback with cached `_noirPlayerContext` only.
- Survival status now comes from `IPlayerRuntimeContext.TryGetSurvivalRuntimeState()`.
- Hull stress now comes from `IPlayerRuntimeContext.TryGetMovementStressRuntimeState()`.
- Wet-lens scalar is preserved as a presentation-only read from the cached movement owner exposed by the cached context.
- Updated the route card, architecture note, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- No new simulation. This keeps the existing screen-space visor/noir fake route and removes a static player-context fallback from render enqueue.

Exact Microseconds saved:
- No measured frame-time claim. The concrete saving is removal of a hidden context fallback from render enqueue and one explicit Gameplay namespace edge from the touched host file.
- Exact stale-token scan found no `using Hecton8.Gameplay`, `PlayerRuntimeContextService`, concrete `PlayerRuntimeContext`, `HectonSurvivalSystem`, or explicit `HectonPlayerMovement` in the touched host/noir files.
- Focused forbidden scan returned no `DecalProjector`, `UnityEngine.Random`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, `using Hecton8.World`, `saturate(color)`, `AcesFitted`, or `ACESFilm`.
- Tiny Noir job scan returned no `.Run(`, `IJob`, or `BurstCompile`.
- Scanner PASS refreshed at `2026-05-21T18:42:29Z`: 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- JSON report validates.
- Compile not launched: CPU sampled at 100%; no compiler processes were active, but AGENTS policy blocks build above 50% CPU.

## 2026-05-21T23:04+04:00 - Polish Loop 18 / Concrete Fluid Boundary Removal

What was wrong:
- The touched shared visor host still imported `Hecton8.Physics`.
- It cached `HectonFluidEngine`, handled `GlobalRegistryServiceSlot.FluidRuntime`, and sampled `TrySampleMaelstromWarp()` for a cosmetic pressure/noir response.
- There is no existing contracts-only fluid read model for this maelstrom scalar, so the route was a concrete sibling-domain edge inside a presentation host.

What was done:
- Removed the `Hecton8.Physics` import from `HectonVisorUberPostFeature`.
- Removed the cached `HectonFluidEngine` field and 30-frame fluid rebind path.
- Removed the `GlobalRegistryServiceSlot.FluidRuntime` hot-swap branch from the Noir partial.
- Replaced maelstrom sampling with `ResolvePressureSurgeVisual01()`, a local screen-space pressure/stress scalar derived from existing presentation inputs: ambient pressure, cached hull stress, and continuous low-tier weight.
- Updated the SHINOBU_235 Noir note, SHINOBU_275 architecture note, route card, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- The previous fluid-owned maelstrom sample is not simulated or queried from Physics. The host now uses a cheap screen-space pressure surge fake that feeds the existing Noir stress/trauma path without changing gameplay pressure truth.

Exact Microseconds saved:
- No measured frame-time claim. The concrete gain is compile-wall and authority-boundary hardening: one concrete Physics namespace edge, one cached owner field, and one periodic rebind route are removed from the touched presentation host.
- Runtime/Profiler proof remains pending.

Verification:
- Exact stale-token scan found no `using Hecton8.Gameplay`, `using Hecton8.Physics`, `PlayerRuntimeContextService`, concrete player runtime types, `HectonFluidEngine`, `GlobalRegistryServiceSlot.FluidRuntime`, `TrySampleMaelstromWarp`, `ResolveMaelstromWarp`, or `RefreshFluidBinding` in the touched host/noir files.
- Focused forbidden route scan returned no `DecalProjector`, `UnityEngine.Random`, direct `Time.*`, HLSL `normalize(`, `math.normalize`, `UsePass`, `.SetBuffer(`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, `new NativeArray`, sibling `Hecton8.World/Gameplay/Physics` imports, `HectonFluidEngine`, `GlobalRegistryServiceSlot.FluidRuntime`, `TrySampleMaelstromWarp`, `saturate(color)`, `AcesFitted`, or `ACESFilm`.
- Tiny Noir job scan returned no `.Run(`, `IJob`, or `BurstCompile`.
- Scanner PASS refreshed at `2026-05-21T18:52:59Z`: 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates, and the SHINOBU_275 report timestamp is `2026-05-21T18:52:59Z`.
- `git diff --check` reports only LF-to-CRLF warnings in edited files.
- Compile not launched: CPU sampled at 84%; no compiler processes were active, but AGENTS policy blocks build above 50% CPU.

## 2026-05-21T23:17+04:00 - Polish Loop 19 / Reconstruction Hot-Path Closure

What was wrong:
- `VisorWoundMappedUploadJob` was a fake Burst surface for a direct mapped-buffer copy.
- Reconstruction enqueue still had profile/CSV debt: profile selection locked the Vault profile buffer from the render path, and `AddRenderPasses()` could retry the aesthetic CSV load.
- Reconstruction constants used one mapped constant buffer and AB split was pushed through `Material.SetFloat` during enqueue instead of the RenderGraph command stream.
- Raw color history availability still had a per-enqueue component lookup.
- Legacy `HectonVisorUberPost.shader` kept hard low-tier gates for heat haze, VR comfort, light shafts, water refraction, and droplet refraction.

What was done:
- Deleted `VisorWoundMappedUploadJob`; mapped GPU upload now calls `DynamicDecalVaultRuntime.CopyDecalsToMappedUploadBuffer()` and performs one guarded `UnsafeUtility.MemCpy`.
- Added A/B reconstruction constant buffers and `_activeReconstructionConstantsBuffer`; unchanged constants reuse the active buffer and changed constants write the next mapped buffer.
- Moved reconstruction AB split binding into the reconstruction RenderGraph raster function with `SetGlobalFloat`.
- Removed render-frame aesthetic CSV retry and replaced render-frame Vault profile selection with a fixed 32-row cold-loaded profile snapshot.
- Cached raw color history read access; `TryGetComponent` is limited to camera-change registration.
- Replaced the legacy shader's binary low-tier quality gates with continuous `smoothstep`/`lerp` weights.
- Updated status, rationale, architecture note, route card, and binary payload ledger.

Cinematic Cheats used:
- No physical wound, fluid, droplet, fracture, or light-shaft simulation was added. The path remains a bounded screen-space wound projection plus shader-side visor/noir fakes, with low-tier effects fading continuously rather than switching off.

Exact Microseconds saved:
- No measured profiler claim. Expected low-end savings are from removed render-frame Vault/file/profile work, removed fake mapped-upload job surface, and removed per-enqueue history component lookup. Estimated risk reduction remains 2-20 us on weak devices pending Unity Profiler proof.

Verification:
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T19:12:28Z`; 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- Focused forbidden C# scan returned no `VisorWoundMappedUploadJob`, `TryLockAndSelectAestheticProfile`, `_lastReconstructionAbSplit`, concrete fluid/player fallback tokens, direct Unity `Time.*`, `UnityEngine.Random`, `UsePass`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, or `new NativeArray` in the touched wound/noir route.
- Shader quality scan has no true hard low-tier branch or `step(0.5)` quality gate; the remaining text hit is `smoothstep(0.54...)`, a substring false positive.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates.
- `git diff --check` reports only LF-to-CRLF warnings in edited files.
- Compile not launched: first sample had CPU 49.79% but `dotnet` PID 6956 and `VBCSCompiler` PID 29328 were active; final sample had CPU 57.95% with `VBCSCompiler` PID 29328 still active, so AGENTS compile policy blocked the build.

<SELF_AUDIT agent_id="SHINOBU_275" loop="19_reconstruction_hot_path">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Re-audited visor post, reconstruction, mapped upload, shader, and docs route.</TASK>
    <TASK id="02" result="PASS_STATIC">Decal inquisition scanner reports 0 active object/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No hot DTO properties added; fake mapped job removed.</TASK>
    <TASK id="04" result="PASS_STATIC">Primary `VisorDecalDTO` remains explicit 80B; reconstruction/profile DTOs unchanged.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock lanes unchanged; runtime hot path cannot cold-load CSV.</TASK>
    <TASK id="06" result="PASS_STATIC">Batched wound Burst jobs remain; one-row mapped upload wrapper removed.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie route preserved: shader fakes instead of physical wounds/droplets/light shafts.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay route unchanged; profile selection is now snapshot read.</TASK>
    <TASK id="10" result="PASS_STATIC">Mapped GPU upload and reconstruction CBuffer publication are double-buffered.</TASK>
    <TASK id="11" result="PASS_STATIC">Legacy shader low-tier gates now fade continuously.</TASK>
    <TASK id="12" result="PASS_STATIC">Normal/refraction perturbation remains shader-side; droplet refraction quality is continuous.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">No gameplay truth, rollback, save, or authority route changed.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry route unchanged; scanner/report proof refreshed.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor facade unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV parsing remains cold; render path reads fixed snapshot only.</TASK>
    <TASK id="18" result="PASS_STATIC">Debug gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Metric validator rerun and report timestamp updated.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs synchronized; compile gate blocked by existing compiler processes.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0. Loop 19 changed no primary DTO bytes.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, heat haze, comfort mask bias, light-shaft intensity/sample budget, water refraction admission, droplet refraction, wound capacity, and thermal fade pressure collapse continuously through `smoothstep`, `lerp`, and quality-smoothed runtime state. Middle quality keeps partial shafts/refraction. High/ultra keep richer reconstruction grain/chroma/vignette and full visual-overkill shader response without changing DTO layout or authority route.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Primary wound Vault lanes remain 71490..71496. Reconstruction lanes remain owned by the visor feature; render enqueue no longer locks the aesthetic profile Vault lane and no new persistent private native collection was introduced. The 32-row profile cache is a cold managed snapshot used to avoid render-frame Vault locks.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Wound job dependency chain remains dispatcher-owned visual sync. Mapped upload is a direct guarded copy after `LockBufferForWrite`. Reconstruction constants are A/B buffers consumed by RenderGraph; no same-frame `.Complete()` was added.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime dependency was added. Compile was not launched because compiler processes were already active and the final CPU sample exceeded 50%.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: object decals/light simulations would be O(N) scene/render work and hard shader quality branches snapped visuals. After: one bounded screen-space projection path and shader fakes with continuous quality weights; mapped upload is one O(N_visible) memory copy over a capped 128-row payload.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T23:35+04:00 - Polish Loop 20 / RenderGraph Dispatcher Ownership Closure

What was wrong:
- Read-only subagents found a real owned compile-surface fault: `DeferredDecalPass` called `DynamicDecalVaultRuntime.CopyDecalsToMappedUploadBuffer()`, while the helper was scoped under `GenerateVisorDecalMatricesJob`.
- Reconstruction constant build/upload, Vault mirror writes, telemetry ring writes, and possible dump file creation still ran from `AddRenderPasses()`.
- Visor post scalar/vector/texture data and wound atlas texture were still applied through material mutation outside the RenderGraph render function.
- `HectonVisorUberPost.shader` and `Hecton_BilateralUpsample.shader` still read shader `_Time`.
- Noir color profile cache misses resolved the Vault profile array from the LateFrame hot path.

What was done:
- Moved `CopyDecalsToMappedUploadBuffer()` onto `DynamicDecalVaultRuntime`.
- Changed reconstruction flow so `AddRenderPasses()` stages camera/runtime inputs and consumes the last active CBuffer; `LateFrameTick()` owns reconstruction constant build/upload, Vault mirror write, telemetry write, and black-box dump.
- Moved visor post bindings to `PostPassData` plus `RasterCommandBuffer.SetGlobal*` inside the raster function.
- Moved the legacy shader trauma scalars out of `UnityPerMaterial` so command-buffer globals are the single route.
- Moved wound atlas binding into the wound RenderGraph raster function.
- Replaced shader `_Time` reads with `_HectonUberVisualTime` and `_H8UberNoirVisualTime`, both fed from the dispatcher-wrapped visual clock.
- Added a fixed cold 32-row Noir color profile snapshot for LateFrame profile selection.

Cinematic Cheats used:
- No new physical simulation was added. The system remains a bounded screen-space projection and reconstruction/noir shader fake. Visual time is a dispatcher-owned scalar, not gameplay truth.

Exact Microseconds saved:
- No profiler claim. Expected low-end risk reduction is from removing render-enqueue CBuffer mapping, Vault telemetry locks, material property writes, shader `_Time` dependency, and hot profile Vault resolves. Estimated risk reduction: 5-25 us on weak devices, pending Unity Profiler.

Verification:
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T19:34:06Z`; 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- Focused scans found no `UpdateMaterialParameters`, `Material.SetFloat/SetVector/SetTexture`, `UnityPerMaterial`, shader `_Time`, direct Unity `Time.*`, `VisorWoundMappedUploadJob`, hot `TryResolveHandle(in _noirColorProfileHandle)`, direct sibling `Hecton8.World/Gameplay/Physics` imports, `UnityEngine.Random`, `UsePass`, `AddBlitPass`, `RenderGraphUtils`, `NativeList`, `NativeHashMap`, or `new NativeArray` in the touched owned route.
- Reconstruction build/upload/telemetry calls now occur from `TryUpdateReconstructionConstantsLate()`, not `AddRenderPasses()`.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates.
- `git diff --check` reports only LF-to-CRLF warnings in edited files.
- Compile not launched: CPU sampled at 78.57%; no `dotnet`/`csc`/`VBCSCompiler` processes were active, but AGENTS policy blocks build above 50% CPU.

<SELF_AUDIT agent_id="SHINOBU_275" loop="20_rendergraph_dispatcher_ownership">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Subagent findings reconciled against the owned visor wound/reconstruction route.</TASK>
    <TASK id="02" result="PASS_STATIC">Decal inquisition scanner reports 0 active GameObject/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No hot DTO properties added; mapped helper compile target corrected.</TASK>
    <TASK id="04" result="PASS_STATIC">Primary `VisorDecalDTO` remains explicit 80B; no DTO expansion for visual time.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock lanes unchanged; public/cold routes remain fail-closed.</TASK>
    <TASK id="06" result="PASS_STATIC">Batched Burst wound jobs remain; one-row fake job stays deleted.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie route preserved: shader fakes and bounded fullscreen passes, no object decals or fluid physics.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Decay route unchanged; profile reads use cold snapshots.</TASK>
    <TASK id="10" result="PASS_STATIC">Mapped GPU upload and reconstruction constants are double-buffered; reconstruction publish is dispatcher-owned.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous quality gates remain; no binary low/high quality switch introduced.</TASK>
    <TASK id="12" result="PASS_STATIC">Normal/refraction perturbation remains shader-side with dispatcher visual time.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">No gameplay truth, rollback, save, or authority route changed.</TASK>
    <TASK id="15" result="PASS_STATIC">Telemetry ring remains; writes moved out of render enqueue.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor facade unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV parsing remains cold; Noir/reconstruction profile selection reads fixed snapshots.</TASK>
    <TASK id="18" result="PASS_STATIC">Debug gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Metric validator rerun and report timestamp updated.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs synchronized; compile probe blocked by CPU policy.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorDecalDTO`: `float4x4 LocalToWorld` offset 0 size 64; `uint DecalTypeHash` offset 64 size 4; `float Opacity01` offset 68 size 4; `float BirthTime` offset 72 size 4; `uint Flags` offset 76 size 4. Total 80B; 80 % 16 = 0. Loop 20 changed no primary DTO bytes.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, visor haze, comfort mask weight, light shafts, water/droplet refraction, wound count, profile cadence, and reconstruction detail continue to collapse through continuous weights and dispatcher-owned scalar state. High/ultra retain reconstruction grain/chroma/vignette and wound refraction without a DTO or authority-route change.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new persistent private native collection was introduced. Vault ownership remains for wound DTOs, tuning, telemetry, reconstruction constants, reconstruction telemetry, CSV scratch, and profile import buffers. The new profile arrays are fixed cold managed snapshots used only to avoid hot Vault reads.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Wound visual sync jobs remain dispatcher-owned. Reconstruction constants now publish from `LateFrameTick()` and are consumed as imported `BufferHandle` resources with `UseBuffer(Read)`. No same-frame `.Complete()` was added.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime dependency was added. Compile was not launched because CPU sampled 78.57%, above the 50% AGENTS gate.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: object/material/render-enqueue state work could scale with presentation complexity and hide mutation outside RG. After: one bounded screen-space pass plus command-buffer globals and dispatcher-published CBuffers; CPU remains ignorant of physical fracture/fluid behavior.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21T23:41+04:00 - Polish Loop 21 / Black-Box Dump Writer Hygiene

What was wrong:
- `DynamicDecalVaultRuntime.DumpBlackBox()` still used `BinaryWriter` for `Dump_SHINOBU_275.bin`.
- The dump path is cold, but the evidence artifact format was implicit and managed-wrapper dependent.

What was done:
- Replaced `BinaryWriter` with stack-span writes.
- The dump header is exactly 16 bytes: magic, reason flags, telemetry capacity, telemetry cursor.
- Each telemetry row is exactly 64 bytes: offsets 0..52 mirror `VisorWoundTelemetryEntry`; bytes 56..63 remain zero pad.
- Float fields are encoded with `math.asuint`; all scalar fields are emitted explicitly as little-endian bytes.
- Updated status, rationale, architecture note, route card, and binary payload ledger.

Cinematic Cheats used:
- No simulation change. This pass hardens the forensic proof lane for the existing screen-space wound fake.

Exact Microseconds saved:
- No steady-frame claim. Crash-path wrapper allocation and per-field writer dispatch are removed; expected gameplay frame impact is 0 us.

Verification:
- `rg` found no `BinaryWriter` in `Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs`.
- `python Tools\Decal_Projector_Inquisition.py`: PASS at `2026-05-21T19:41:50Z`; 5825 scanned assets, 336 candidates, 0 active GameObject decal violations, 0 active URP decal renderer feature violations.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates.
- `git diff --check` reports only LF-to-CRLF warnings in edited files.
- Compile not launched: CPU sampled at 100%/83% with `VBCSCompiler` PID 32428 active, then 73% with no compiler process returned.

<SELF_AUDIT agent_id="SHINOBU_275" loop="21_black_box_dump_writer">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS_STATIC">Re-audited owned runtime dump and scanner proof route.</TASK>
    <TASK id="02" result="PASS_STATIC">Decal inquisition scanner reports 0 active object/URP decal violations.</TASK>
    <TASK id="03" result="PASS_STATIC">No hot DTO properties added; dump helper writes raw scalar fields.</TASK>
    <TASK id="04" result="PASS_STATIC">Telemetry row remains explicit 64B; no `Pack=1` or stride change.</TASK>
    <TASK id="05" result="PASS_STATIC">Mock wound data unchanged.</TASK>
    <TASK id="06" result="PASS_STATIC">Burst wound jobs unchanged.</TASK>
    <TASK id="07" result="PASS_STATIC">Dear Lie route unchanged: screen-space wounds and shader fakes only.</TASK>
    <TASK id="08" result="PASS_STATIC">Circular overwrite unchanged.</TASK>
    <TASK id="09" result="PASS_STATIC">Deterministic decay unchanged.</TASK>
    <TASK id="10" result="PASS_STATIC">Mapped GPU upload unchanged.</TASK>
    <TASK id="11" result="PASS_STATIC">Continuous scalability unchanged.</TASK>
    <TASK id="12" result="PASS_STATIC">Shader perturbation unchanged.</TASK>
    <TASK id="13" result="PASS_STATIC">AUP localization unchanged.</TASK>
    <TASK id="14" result="PASS_STATIC">No gameplay authority or rollback route changed.</TASK>
    <TASK id="15" result="PASS_STATIC">Black-box telemetry dump now has explicit little-endian 16B header and 64B rows.</TASK>
    <TASK id="16" result="PASS_STATIC">Editor facade unchanged.</TASK>
    <TASK id="17" result="PASS_STATIC">CSV profile ingestion unchanged.</TASK>
    <TASK id="18" result="PASS_STATIC">Debug gizmo route unchanged.</TASK>
    <TASK id="19" result="PASS_STATIC">Metric validator rerun and report timestamp refreshed.</TASK>
    <TASK id="20" result="PASS_STATIC_COMPILE_BLOCKED">Docs/logs synchronized; compile gate blocked by CPU and active compiler process.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>`VisorWoundTelemetryEntry`: `Frame@0 uint`, `ActiveDecals@4 uint`, `NewDecals@8 uint`, `UploadCount@12 uint`, `GpuUploadMicroseconds@16 float`, `CpuMicroseconds@20 float`, `GlobalQualityWeight@24 float`, `ThermalPressure01@28 float`, `Flags@32 uint`, `StateHash@36 uint`, `DroppedThisFrame@40 uint`, `TotalWritten@44 uint`, `MaxActiveThisFrame@48 uint`, `LastBallisticFrame@52 uint`, `_pad0@56 ulong`; total 64B exactly.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No quality behavior changed in Loop 21. The existing curve still scales active wound count, thermal fade pressure, shader refraction, reconstruction/noir richness, and optional telemetry intensity continuously through `GlobalQualityWeight`.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault lanes unchanged: 71490 instances, 71491 upload scratch, 71492 runtime state, 71493 telemetry ring, 71494 tuning, 71495 material profiles, 71496 CSV scratch. No new persistent private native collection was introduced.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Job graph unchanged. Dump writes occur only from the fault/diagnostic lane after telemetry lock acquisition; no new `Complete()` or same-frame schedule/readback loop was added.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime dependency was added. Compile was not launched because CPU sampled 100%/83% with `VBCSCompiler` PID 32428 active, then 73% with no compiler process returned.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before/after visual complexity is unchanged: screen-space wound projection remains O(N_visible capped at 128), not object decals or physical fracture simulation. Loop 21 only hardens the forensic output format.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
