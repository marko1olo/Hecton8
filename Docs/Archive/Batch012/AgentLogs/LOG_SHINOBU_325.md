# LOG_SHINOBU_325

## 2026-05-22 - Screen-Space Trauma Decal Resolver

What was wrong:
- Active code still carried SHINOBU_275 visor-wound naming and old `_GlobalVisorWounds` ABI language while the current assignment required SHINOBU_325 trauma decals.
- The inherited local BufferID range `71490..71496` collided with central `H8Memory` auxiliary equipment and propwash GPU IDs.
- Existing docs could steer integrators back to old shader names and colliding buffer IDs.
- Runtime proof had to target `DecalProjector`/Canvas/GameObject spawn, not just shader existence.

What was done:
- Promoted the active route to SHINOBU_325 trauma ABI: `TraumaDecalDTO`, `TraumaWoundTelemetryEntry`, `_GlobalVisorTrauma`, `Dump_SHINOBU_325.bin`, and `H8.VisorTrauma.*` profiler markers.
- Added `Assets/_Project/Art/Shaders/Hecton_VisorTrauma.shader`; it reconstructs scene position from depth and projects blood/crack/burn/acid/scorch trauma from one StructuredBuffer.
- Kept `DeferredDecalPass` as a RenderGraph fullscreen pass with one `CoreUtils.DrawFullScreen` call and double-buffered `GraphicsBuffer.LockBufferForWrite` upload.
- Updated renderer assets to bind `HectonVisorTraumaFeature` and the new shader GUID.
- Moved trauma Vault lanes to `73190..73196`, later extended ingress to `73197..73198`, and documented the old range as rejected.
- Added `Assets/_Project/Data/Decals/visor_trauma_profiles.csv` for cold profile ingestion.
- Added `Tools/Trauma_Projector_Inquisition.py`; it writes `shinobu_325_screen_space_trauma_decal_resolver` into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- Added route card `Docs/ARCHITECTURE/SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_ROUTE_CARD.md`, ledger entry, and supersession notes on stale SHINOBU_275 docs.

Cinematic cheats used:
- Screen-space projection and refraction replace physical blood decals, fracture meshes, projector volumes, and particle splats.
- Depth reconstruction plus camera-relative AUP matrix projection gives the visual lie without scene hierarchy truth.
- Procedural blood/crack/burn/acid/scorch samples are used when no atlas is bound.
- GlobalQualityWeight scales active count and shader richness continuously from Low through Ultra.

Exact microseconds saved:
- DecalProjector/GameObject route removal: estimated 100-350us/frame under combat impact bursts.
- RenderGraph fullscreen route versus compatibility blit/projector stack: estimated 150us/frame.
- `GraphicsBuffer.LockBufferForWrite` staging versus hot `SetData`: estimated 80-300us stall risk removed.
- No hot GlobalRegistry polling in the render pass: estimated 5-20us/frame.
- No Canvas/prefab trauma overlay churn: estimated 100-400us hitch risk removed during dense damage.
- Exact profiler/Frame Debugger timing is pending Unity runtime proof.

Verification status:
- Static source archaeology complete.
- `Tools/Trauma_Projector_Inquisition.py` PASS at 2026-05-22T16:43:40Z: 5915 assets scanned, 338 candidate assets, 0 active trauma GameObject/Canvas/DecalProjector violations, 2 inactive URP decal renderer features reported.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` validates with `python -m json.tool`.
- Targeted `git diff --check` is clean for owned files after removing shader-variant trailing whitespace; only Git LF/CRLF warnings remain.
- Active source stale-token scan is clean for old `VisorDecalDTO`, `_GlobalVisorWound*`, `SHINOBU_275`, `Dump_SHINOBU_275`, and `(BufferID)7149` in owned runtime/shader/renderer assets.
- Compile guard found active `dotnet` processes `16552` and `19716`; guarded build was not launched.
- Unity import, shader import, GCMonitor, Frame Debugger, and Play Mode proof pending.

## 2026-05-22 - Ultra-Polish Vault Ingress Correction

What was wrong:
- The request ingress still used a private persistent `NativeQueue<DecalRequestSignal>`. Prewarming did not make it Vault-owned. Under the strengthened H-Phi mandate, this was not acceptable.

What was done:
- Added `DecalRequestQueueStateDTO`, explicit 64 bytes, with `WriteIndex@0`, `ReadIndex@4`, `PendingCount@8`, `Capacity@12`, counters at `16..28`, and 32 bytes of explicit padding at `32..63`.
- Added Vault BufferID `73197` for `DecalRequestSignal[1024]`.
- Added Vault BufferID `73198` for `DecalRequestQueueStateDTO[1]`.
- Replaced private `NativeQueue` enqueue/drain with fixed Vault ring writes and Burst drain inside `GenerateTraumaDecalMatricesJob`.
- Reworked mock trauma generation so `GenerateMockTraumaWoundsJob` writes reserved request ring slots directly by index.

Cinematic Cheats used:
- Unchanged: trauma remains a screen-space/depth reconstruction lie, not physical decals or fracture geometry.

Exact Microseconds saved:
- No direct profiler claim. Expected gain is allocator sovereignty and avoiding NativeQueue allocator fragmentation; frame-time impact is pending runtime proof.

Verification status:
- `Tools/Trauma_Projector_Inquisition.py` PASS at 2026-05-22T17:47:29Z: 5919 assets scanned, 338 candidates, 0 active trauma GameObject/Canvas/DecalProjector violations, 2 inactive URP decal renderer features reported.
- `python -m json.tool Docs\Reports\RENDERING_OPTIMIZATION_REPORT.json` validates after rewriting the report without BOM.
- Targeted active source stale-token scan is clean for `NativeQueue<DecalRequestSignal>`, `_requests`, `RequestQueuePrewarmCapacity`, old SHINOBU_275 runtime names, `_GlobalVisorWound*`, and `(BufferID)7149`.
- Targeted `git diff --check` is clean for owned SHINOBU_325 files; Git still warns that several existing LF files will be CRLF-normalized when touched.
- Compile guard found multiple active `dotnet` processes; no build was launched.

Additional correction:
- `TryEnqueueRequest` and `GenerateMockTraumaWounds` now count request-ring lock/resolve failures as dropped ingress. This prevents blackbox under-reporting during Vault contention.
- `Tools/Trauma_Projector_Inquisition.py` now reads the shared JSON report with `utf-8-sig` and writes plain UTF-8, so another agent's BOM cannot break `json.tool` validation.
- Subagent RenderGraph audit found `RasterCommandBuffer.SetGlobalTexture(int, Texture2DArray)` on the optional trauma atlas. `_BlitTexture` and `_CameraDepthTexture` remain legal `TextureHandle` bindings; the atlas is now set on the material before the RenderGraph render function.

<SELF_AUDIT>
  <Agent>SHINOBU_325</Agent>
  <Domain>Echelon 8 Presentation & UX / SCREEN_SPACE_TRAUMA_DECAL_RESOLVER</Domain>
  <TaskCount>20</TaskCount>
  <TaskReconciliation>
    <Task id="01" status="PASS">Archaeology scanner and manual source pass identify active visor trauma route; no duplicate owner introduced.</Task>
    <Task id="02" status="PASS">Runtime trauma projector/object route purged from owned active path; scanner reports zero active Canvas/GameObject/DecalProjector violations.</Task>
    <Task id="03" status="PASS">Hot DTOs use explicit public fields and pointer/ref writes; stale property/old DTO scan clean in owned path.</Task>
    <Task id="04" status="PASS">`ValidateDecalInstanceLayout` checks `TraumaDecalDTO=80`, `DecalRequestSignal=64`, and `DecalRequestQueueStateDTO=64` plus key offsets.</Task>
    <Task id="05" status="PASS">`GenerateMockTraumaWoundsJob` writes deterministic synthetic request rows into Vault-backed request ring slots.</Task>
    <Task id="06" status="PASS">`GenerateTraumaDecalMatricesJob` consumes request rows, subtracts camera AUP, builds local matrices, and writes ring slots with `[NoAlias]` pointers.</Task>
    <Task id="07" status="PASS">`Hecton_VisorTrauma.shader` uses `_GlobalVisorTrauma` and depth reconstruction in one fullscreen RenderGraph draw.</Task>
    <Task id="08" status="PASS">`TotalWritten % capacity` and `CurrentWriteIndex` maintain O(1) overwrite semantics.</Task>
    <Task id="09" status="PASS">`DecayTraumaDecalOpacityJob` uses deterministic opacity decay and persistent glass floor without coroutines.</Task>
    <Task id="10" status="PASS">Double `GraphicsBuffer` upload uses `LockBufferForWrite` and no hot `SetData` in owned active path.</Task>
    <Task id="11" status="PASS">`GlobalQualityWeight` maps active upload/evaluation count from 8 to configured overkill capacity through `math.lerp`/smooth curve.</Task>
    <Task id="12" status="PASS">Shader crack/refraction intensity is a continuous parameter, not a shader keyword fork.</Task>
    <Task id="13" status="PASS">Impact AUP remains double until camera-relative float matrix construction; shader subtracts camera world vector after depth reconstruction.</Task>
    <Task id="14" status="PASS">Route card and ledger state visual-only exclusion from save, Merkle, and rollback truth.</Task>
    <Task id="15" status="PASS">`TraumaWoundTelemetryEntry[300]` in Vault records active/new/upload/cpu/quality/drop data and dumps fixed 64B rows.</Task>
    <Task id="16" status="PASS">UI Toolkit tuner exists under editor route with tuning sliders and direct Vault DTO write path.</Task>
    <Task id="17" status="PASS">Cold CSV profile ingest uses `ReadOnlySpan<byte>`, scratch Vault bytes, FNV/hash parse, and fixed profile DTOs.</Task>
    <Task id="18" status="PASS">Editor gizmo reads trauma matrices through owner debug acquisition path without spawned debug objects.</Task>
    <Task id="19" status="PASS">`Tools/Trauma_Projector_Inquisition.py` writes JSON proof and validates after BOM-safe read/write.</Task>
    <Task id="20" status="PASS">Self-audit found BufferID collision and private `NativeQueue`; both corrected and documented.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>
    <DTO name="TraumaDecalDTO" sizeBytes="80" alignment="16-byte multiple">
      <Field name="LocalToWorld" offset="0" bytes="64" />
      <Field name="DecalTypeHash" offset="64" bytes="4" />
      <Field name="Opacity01" offset="68" bytes="4" />
      <Field name="BirthTime" offset="72" bytes="4" />
      <Field name="Flags" offset="76" bytes="4" />
      <Math>64 + 4 + 4 + 4 + 4 = 80; 80 % 16 = 0; no Pack=1; no managed fields.</Math>
    </DTO>
    <DTO name="DecalRequestSignal" sizeBytes="64" alignment="cache-line">
      <Field name="ImpactAup double3" offset="0" bytes="24" />
      <Field name="Normal float3" offset="24" bytes="12" />
      <Field name="RadiusMeters" offset="36" bytes="4" />
      <Field name="ProjectionDepthMeters" offset="40" bytes="4" />
      <Field name="LifetimeSeconds" offset="44" bytes="4" />
      <Field name="MaterialHash" offset="48" bytes="4" />
      <Field name="Flags" offset="52" bytes="4" />
      <Field name="StableSeed" offset="56" bytes="4" />
      <Field name="SourceFrame" offset="60" bytes="4" />
      <Math>24 + 12 + 7*4 = 64; double lane starts at offset 0; scalar lanes are 4-byte aligned.</Math>
    </DTO>
    <DTO name="DecalRequestQueueStateDTO" sizeBytes="64" alignment="cache-line false-sharing guard">
      <Field name="WriteIndex" offset="0" bytes="4" />
      <Field name="ReadIndex" offset="4" bytes="4" />
      <Field name="PendingCount" offset="8" bytes="4" />
      <Field name="Capacity" offset="12" bytes="4" />
      <Field name="Counters" offset="16" bytes="16" />
      <Field name="ExplicitPadding" offset="32" bytes="32" />
      <Math>32 bytes live counters + 32 bytes padding = 64; one cache line for contested queue state.</Math>
    </DTO>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Below `GlobalQualityWeight=0.3`, `ResolveMaxActiveDecals` curves toward `LowTierCapacity` (default 8), `ResolveDecayRate` increases fade pressure, and the shader evaluates only the newest uploaded decals. Refraction richness is scaled by `_GlobalVisorTraumaRefractionParams` instead of branching into separate shader variants. Middle tiers raise the active window smoothly; Ultra allows 128 rows and richer atlas/procedural contribution. DTO layout, BufferIDs, signal route, and rollback exclusion do not change with quality.
  </ScalabilityCurve>
  <HPhiVaultStatus>
    No private persistent `NativeQueue`, `NativeArray`, `NativeList`, or `NativeHashMap` remains in the active trauma route. Boot/cold init requests Vault IDs `73190` instances, `73191` upload scratch, `73192` runtime state, `73193` telemetry, `73194` tuning, `73195` material profiles, `73196` CSV scratch, `73197` request ring, and `73198` request state. Handles are released on rebind/reset/dispose through `ReleaseDynamicDecalVaultHandles`.
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    `GenerateMockTraumaWoundsJob` owns one `[NoAlias]` request pointer and is force-completed only in the cold editor/test mock path. Runtime visual sync schedules `GenerateTraumaDecalMatricesJob -> DecayTraumaDecalOpacityJob -> BuildDecalUploadBufferJob`, registers the final handle with `H8Memory`, and finalizes only through `DispatcherJobFence.TryFinalizeCompleted`; pending handles are carried across frames. All non-overlapping job pointers use `[NoAlias, NativeDisableUnsafePtrRestriction]`; read-only request/upload sources use `[ReadOnly]`.
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    Owned active source depends on Core/Core.Contracts/Core.Memory and uses `SignalBus<T>` payloads, not sibling runtime assembly references. Build was not launched because multiple `dotnet` processes were present under the user CPU/compiler guard.
  </CompileGuard>
  <RenderGraphAbiGuard>
    The pass render function binds only `TextureHandle` resources through `RasterCommandBuffer.SetGlobalTexture`, the imported trauma `GraphicsBuffer`, and scalar/vector constants. The optional static `Texture2DArray` atlas is material-local and no longer uses the known-red `RasterCommandBuffer.SetGlobalTexture(int, Texture)` overload.
  </RenderGraphAbiGuard>
  <DearLieConfirmation>
    Blood, acid, burn, scorch, dent, and crack presentation is screen-space projection/refraction over depth-reconstructed scene position. Before: object/projector stack scales O(N) GameObjects/components/material/culling overhead plus draw pressure. After: CPU ingress is bounded O(min(requests, maxActive)) Burst work and rendering is one fullscreen draw with O(activeDecals) fragment evaluation, where activeDecals is continuously quality-capped.
  </DearLieConfirmation>
</SELF_AUDIT>

## 2026-05-22 - RenderGraph ABI Addendum And Final Static Proof Pass

What was wrong:
- Read-only RenderGraph subagent found one concrete ABI hazard: the optional `Texture2DArray` trauma atlas was being bound through `RasterCommandBuffer.SetGlobalTexture(int, Texture2DArray)`, a known-red overload shape in this repository.

What was done:
- `_BlitTexture` and `_CameraDepthTexture` remain RenderGraph `TextureHandle` bindings.
- `_GlobalVisorTraumaAtlas` is now bound as a material texture before the RenderGraph render function.
- The render function now binds only graph-visible textures, the imported `_GlobalVisorTrauma` `GraphicsBuffer`, and scalar/vector constants before one fullscreen draw.
- Status and route-card proof were updated to record this ABI guard.

Cinematic Cheats used:
- Unchanged: all blood, acid, burn, scorch, dent, and glass crack presentation remains a screen-space/depth reconstruction projection. No `DecalProjector`, Canvas, physical fracture mesh, spawned quad, or per-impact GameObject route was introduced.

Exact Microseconds saved:
- No new frame-time claim. The RenderGraph change removes a compile/import blocker, not an extra runtime pass. The prior expected savings remain: 0.2-1.0ms under dense trauma versus object/projector stacks, pending Unity profiler proof.

Verification status:
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` validates with `python -m json.tool`.
- SHINOBU_325 JSON entry reports PASS at `2026-05-22T17:47:29Z`: 5919 assets scanned, 338 candidates, 0 active trauma GameObject/Canvas/DecalProjector violations, 2 inactive URP decal renderer features.
- Stale-token scan over owned active runtime/shader route found no `NativeQueue<DecalRequestSignal>`, `_requests`, `RequestQueuePrewarmCapacity`, old `_GlobalVisorWound*`, `SHINOBU_275`, or `(BufferID)7149` hits.
- RenderGraph overload scan found no `data.DecalAtlas`, `Texture2DArray DecalAtlas`, `SetGlobalTexture(ShaderConstants.DecalAtlasId, ...)`, or hot `SetData(` hits in the owned runtime files.
- `git diff --check` reports no whitespace errors for owned docs after this addendum. Earlier owned source check was also clean except Git LF/CRLF normalization warnings.
- Build was not launched. Process guard currently shows CPU average `93`, active `csc` process `12776`, and active `dotnet` process `17476`, so the user CPU/compiler guard still blocks `dotnet build`.
- Unity runtime import, GC allocation capture, Frame Debugger one-draw proof, and shader import proof remain pending runtime verification.
