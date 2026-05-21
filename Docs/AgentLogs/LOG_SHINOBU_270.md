# LOG_SHINOBU_270

## 2026-05-21 VISOR_AR_STENCIL_RENDERER

What was wrong:
- `SuitHUDPresentationController` defaulted to Canvas/RenderTexture projection (`ModernProjectedSharedRT`) and could create `Suit_HUD_ProjectionSource` with `Canvas`, `GraphicRaycaster`, `SuitHUDV4CanvasOverlay`, and `HectonUIScaler`.
- `ARWaypointOverlay` rendered tactical markers through Canvas slots, Image components, and TMP labels.
- `RuntimeWatchdog.TriggerHudCanvasBuildBatch` still contained a runtime `Canvas.ForceUpdateCanvases()` recovery call.
- AR/HUD values had no 64-byte visor-specific CBuffer contract or black-box ring for last-frame state.

What was done:
- Added `PresentationMode.StencilRenderGraph` and made it the default in `SuitHUDPresentationController`.
- Suppressed overlay/projected Canvas ownership in stencil mode and disabled `VisorHUDController` projection RT mode.
- Converted `ARWaypointOverlay` into a data source when stencil mode is active: it keeps waypoint service registration and AUP collection, but skips Canvas slot creation/mutation and hides existing slots.
- Added `HectonVisorARStencilRendererFeature` with:
  - `StencilPass`: RenderGraph pass that draws helmet-glass mask using `Hecton8/Visor/StencilMask`.
  - `ArPass`: RenderGraph fullscreen resolve after transparents, stencil `Equal` gated.
  - double-buffered `GraphicsBuffer.Target.Constant` upload for HUD and digit params.
  - double-buffered structured target upload for 16 AR targets.
  - bounded `GenerateMockHudData`, `BuildDigitParams`, `ProjectArTargets`, and direct memcpy upload helpers.
- Added `Hecton_VisorAR.shader` with stencil-gated procedural digits, target brackets, scanlines, chroma/curvature, and stress/CO2/O2-driven breath fog.
- Added `VisorHudParamsDTO`, `VisorArTargetDTO`, `VisorHudDigitParamsDTO`, `VisorTelemetryEntry`, and `VisorHudProfileDTO` with explicit layouts.
- Added editor/static tooling:
  - `Visor HUD & AR Tuner`
  - `HectonVisorStencilPreviewGizmo`
  - `HUDCanvasInquisition`
  - `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`
  - `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md`

Cinematic Cheats used:
- Stencil mask instead of transparent Canvas clipping.
- Procedural seven-segment numeric glyphs instead of TMP text meshes.
- 2D screen-space target brackets from AUP-local projection instead of world-space marker objects.
- Breath fog as edge-weighted shader noise instead of particles or translucent quads.
- Continuous `GlobalQualityWeight` curve for curvature, chroma, scanlines, and bracket scale.

Exact Microseconds saved:
- Canvas rebuild path: estimated 250-750 us CPU per HUD churn event on i3/MX350. Profiler proof pending because CPU gate blocked compile/run.
- Runtime ForceUpdate recovery: estimated 300-900 us worst-case CPU spike removed.
- AR marker Canvas/TMP mutation: estimated 20-150 us CPU per update burst removed.
- Transparent HUD overdraw: estimated 80-300 us GPU depending resolution/target count, replaced by one fullscreen stencil-gated pass.
- Upload path: expected 5-20 us stall risk reduction from double-buffered CBuffer writes; no `Shader.SetGlobalVector` or `Shader.SetGlobalFloat` in the visor path.

Verification:
- `git diff --check` on changed files: no whitespace errors.
- Static scan: runtime `Canvas.ForceUpdateCanvases` count in `Assets/_Project/Scripts` excluding Editor is 0.
- Static scan: no `Shader.SetGlobalVector` or `Shader.SetGlobalFloat` in the visor path.
- Compile/build: BLOCKED. `dotnet`/`csc` were not running, but CPU LoadPercentage and Get-Counter both reported 100%, above the explicit >50% forbidden build threshold.

<SELF_AUDIT>
  <Agent>SHINOBU_270</Agent>
  <TaskCount>20</TaskCount>
  <DTO name="VisorHudParamsDTO" sizeBytes="64">
    <Field name="TargetCoordinates" offset="0" sizeBytes="16" />
    <Field name="VitalStats" offset="16" sizeBytes="16" />
    <Field name="VisorGlitchParams" offset="32" sizeBytes="16" />
    <Field name="QualityAndTime" offset="48" sizeBytes="16" />
  </DTO>
  <VaultBuffers>
    <Buffer id="73180" type="VisorHudParamsDTO" rollback="excluded" />
    <Buffer id="73181" type="ARWaypointOverlay.StencilTargetSourceDTO" rollback="excluded" />
    <Buffer id="73182" type="VisorArTargetDTO" rollback="excluded" />
    <Buffer id="73183" type="VisorHudDigitParamsDTO" rollback="excluded" />
    <Buffer id="73184" type="VisorTelemetryEntry[300]" rollback="excluded" />
    <Buffer id="73185" type="VisorHudProfileDTO" rollback="excluded" />
    <Buffer id="73186" type="CSV scratch bytes" rollback="excluded" />
  </VaultBuffers>
  <ZeroGC>
    <HotPathManagedCanvasMutation>false</HotPathManagedCanvasMutation>
    <ShaderSetGlobalVector>false</ShaderSetGlobalVector>
    <ShaderSetGlobalFloat>false</ShaderSetGlobalFloat>
    <MaterialPropertyBlockArrays>false</MaterialPropertyBlockArrays>
    <KnownColdAllocations>fallback stencil Mesh arrays, GraphicsBuffer creation, editor tooling, optional CSV file open</KnownColdAllocations>
  </ZeroGC>
  <AUPProjection>
    <Rule>target double3 AUP minus camera double3 AUP before float3 cast</Rule>
    <Implementation>ProjectArTargets</Implementation>
  </AUPProjection>
  <QualityScaling>
    <Scalar>HomeostasisBrain.GlobalQualityWeight</Scalar>
    <Uses>curvature,chroma,scanline frequency,target scale,fog blend</Uses>
    <BinaryHardwareSwitches>false</BinaryHardwareSwitches>
  </QualityScaling>
  <BlackBox>
    <RingEntries>300</RingEntries>
    <DumpPath>Docs/AgentLogs/Dump_SHINOBU_270.bin</DumpPath>
    <DumpTriggers>non-finite projection fault</DumpTriggers>
  </BlackBox>
  <CompileStatus>BLOCKED_BY_CPU_GATE_100_PERCENT</CompileStatus>
</SELF_AUDIT>

## VISOR_AR_STENCIL_RENDERER STATIC POLISH LOOP 7

Date: 2026-05-21
Agent: SHINOBU_270
Domain: ECHELON 8 Presentation & UX / Visor AR HUD

What was wrong:
- The visor RenderGraph prep path still had two legacy bridge fallbacks: stress read from `GlobalSignals.TryGetLatestPlayerStressSignal` and camera AUP fallback through `AbsoluteUniversePosition.FromRuntimePosition`.
- That made a visual-only renderer dependent on legacy global signal state when the owner-published player pose snapshot was unavailable.

What was done:
- `HectonVisorARStencilRendererFeature` now derives stress only from owner-published UI scalar snapshots.
- Missing player AUP now clears visual AR target rows, records `TelemetryFlagNoPlayerAup`, and keeps HUD vitals/digits rendering without projecting markers against a fabricated origin.
- AR resolve no longer binds active depth as a sampled texture while also using it as the depth/stencil attachment; stencil equality now uses the attachment only.
- AR resolve pass data now carries RenderGraph-imported `BufferHandle`s only; raw `GraphicsBuffer` binding references are resolved inside the render function.
- Editor/asmdef/API audit covered `VisorHudArTunerWindow`, `HUDCanvasInquisition`, `Hecton8.UI.Editor.asmdef`, root `Hecton8.Core.asmdef`, `IDataVault` generation-handle APIs, and existing RenderGraph constant-buffer binding patterns.

Cinematic Cheats used:
- AR target projection remains a visual fake: O(16) bounded camera-relative math plus shader brackets, not GameObject marker meshes or Canvas labels.
- No physics, raycasts, or origin bridge fallback is used when AUP authority is missing; the visual layer degrades by clearing optional markers.

Exact Microseconds saved:
- Legacy bridge purge estimate: 1-5 us CPU variance reduction on low-end silicon, pending profiler proof.
- Existing Canvas/TMP suppression estimates unchanged: 250-750 us Canvas rebuild avoided per HUD churn event, 20-150 us TMP/text mutation avoided per update burst, 80-300 us GPU overdraw avoided by one stencil-gated resolve.

Verification:
- Static scan confirms `HectonVisorARStencilRendererFeature.cs` has no `GlobalSignals`, `FromRuntimePosition`, `Shader.SetGlobalVector`, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, `BurstCompile`, `IJob`, or `.Run()` tokens.
- Build remains blocked by command discipline: no dotnet/csc process reported; latest CPU gate sampled 73% by CIM, then 65.2%, 100%, 85.8%, 72.3%, 72.5% by processor counter, above the forbidden >50% threshold.

## VISOR_AR_STENCIL_RENDERER BUILD GATE WATCH

Date: 2026-05-21
Agent: SHINOBU_270

What was wrong:
- A short low CPU window appeared, but the follow-up gate check found another compile already running.

What was done:
- SHINOBU_270 did not launch `dotnet build`.
- Active compiler processes observed: `dotnet` PID 33144 and `csc` PID 31492.
- Latest processor counter samples: 98.5%, 66.5%, 54.5%.
- Follow-up process query returned no compiler rows, but CPU stayed above threshold at 55.2%, 63.1%, 57.3%, 66.9%, 82.1%.

Rejected Alternatives:
- Killing another agent's compiler.
- Starting a competing build.
- Reporting compile proof from static verification.

## 2026-05-21 VISOR_AR_STENCIL_RENDERER POLISH ADDENDUM

What was wrong:
- First local Vault range `70680..70686` collided with `H8Memory.ShinobuExosuit*`.
- AR resolve rendered stencil-only into a fresh texture; pixels outside the visor mask were undefined.
- `ARWaypointOverlay.CopyStencilTargetSources` recollected waypoint state during render prep.
- Tiny synchronous `.Run()` job wrappers were used for bounded copy/digit/mock/projection work.
- New assets lacked fixed Unity `.meta` files.
- Editor gizmo retained a private persistent `NativeArray`.
- `SuitHUDV4CanvasOverlay` still had a legacy `Shader.SetGlobalVector` analog-jitter route.

What was done:
- Moved SHINOBU_270 visual Vault lanes to `73180..73186` and documented the collision repair in the binary payload ledger.
- `Hecton_VisorAR.shader` now runs an unconditional source copy subpass before the stencil-equal AR overlay subpass inside the same RenderGraph resolve pass.
- RenderGraph now imports active `GraphicsBuffer` resources and declares them with `builder.UseBuffer(..., AccessFlags.Read)`.
- Target rows are compacted before upload, and the shader uses per-row active flags instead of prefix-count masking.
- Target projection gates far clip, non-finite values, and behind-camera coordinates before writing DTO rows.
- `CopyStencilTargetSources` now copies only the latest owner-phase snapshot. Collection remains in `Tick`/`SlowTick`.
- Tiny jobs were replaced by direct local math and `UnsafeUtility.MemCpy` into double-buffered mapped GPU buffers.
- Telemetry dump is reserved for non-finite/crash-class projection fault. Budget breaches remain in the 300-frame ring.
- Added fixed `.meta` files for the new script and shader assets.
- Editor gizmo is editor-only and uses Temp scoped native scratch.
- Legacy Canvas analog-jitter global shader writes were removed from `SuitHUDV4CanvasOverlay`.

Cinematic Cheats used:
- Helmet-glass stencil mask instead of Canvas clipping and transparent HUD quads.
- Seven-segment procedural digits instead of TMP text meshes or runtime strings.
- AUP-local screen brackets instead of in-world marker objects.
- Edge-weighted shader noise fog instead of particles.
- Continuous `GlobalQualityWeight` drives curvature, chroma, scanlines, fog blend, and bracket scale.

Exact Microseconds saved:
- Tiny job dispatch rejection: estimated 2-10 us CPU per visor frame, pending profiler proof.
- Render-prep owner mutation removal: estimated 5-30 us CPU variance reduction when waypoint ownership is active.
- Canvas rebuild suppression remains estimated 250-750 us CPU per HUD churn event.
- Runtime `Canvas.ForceUpdateCanvases` recovery removal remains estimated 300-900 us worst-case CPU spike.
- TMP/Canvas marker mutation removal remains estimated 20-150 us CPU per update burst.
- Transparent HUD overdraw replacement remains estimated 80-300 us GPU depending resolution and marker count.

Verification:
- SHINOBU_270 XML prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` after polish.
- Targeted static scan found no `Shader.SetGlobalVector`, `Shader.SetGlobalFloat`, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, `BurstCompile`, `IJob`, `.Run()`, runtime persistent `NativeArray`, or unused Font/Fog texture binding in the touched visor/UI path.
- Targeted BufferID scan found `73180..73186` only in SHINOBU_270 source/docs.
- Targeted `.meta` scan found no missing meta for the new SHINOBU_270 C# and shader assets.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses through `ConvertFrom-Json`.
- `git diff --check` on SHINOBU_270 touched files reports no whitespace errors, only CRLF normalization warnings.
- Compile/build: BLOCKED. No dotnet/csc process was reported, but CPU gate sampled 88% by CIM and latest performance counter samples at 83.7-100%, above the forbidden >50% threshold.

<SELF_AUDIT>
  <Agent>SHINOBU_270</Agent>
  <Domain>ECHELON 8 Presentation &amp; UX / Visor AR HUD</Domain>
  <TaskCount>20</TaskCount>
  <TaskReconciliation>
    <Task id="01" status="PASS">Existing HUD/visor/AR Canvas paths audited; `SuitHUDV4CanvasOverlay` integrated as suppressed cold/editor surface instead of duplicated UI.</Task>
    <Task id="02" status="PASS">Runtime Canvas rebuild and `ForceUpdateCanvases` paths removed/suppressed in the HUD route.</Task>
    <Task id="03" status="PASS">Hot DTOs use raw unmanaged fields and explicit layout; no DTO C# properties.</Task>
    <Task id="04" status="PASS">Layout validator checks sizes and editor offsets for HUD and target source rows.</Task>
    <Task id="05" status="PASS">Mock HUD data generator exists as bounded local math; tiny job wrapper rejected after polish.</Task>
    <Task id="06" status="PASS">RenderGraph stencil mask pass writes helmet-glass stencil through `Hecton_VisorStencilMask`.</Task>
    <Task id="07" status="PASS">AR resolve uses a RenderGraph fullscreen pass with source copy plus stencil-equal overlay.</Task>
    <Task id="08" status="PASS">Numeric text is procedural digit math, no TMP or runtime string mutation.</Task>
    <Task id="09" status="PASS">AUP target projection localizes double3 target minus camera before float projection and compacts visible rows.</Task>
    <Task id="10" status="PASS">Shader complexity scales continuously through `GlobalQualityWeight`.</Task>
    <Task id="11" status="PASS">GPU upload is double-buffered `GraphicsBuffer.LockBufferForWrite` plus direct `UnsafeUtility.MemCpy`.</Task>
    <Task id="12" status="PASS">Breath fog is shader-side edge/noise fake driven by stress/vitals.</Task>
    <Task id="13" status="PASS">AUP precision boundary is preserved before all float math.</Task>
    <Task id="14" status="PASS">Visual lanes `73180..73186` are rollback/Merkle/save excluded.</Task>
    <Task id="15" status="PASS">300-frame telemetry ring exists; non-finite projection dumps `Dump_SHINOBU_270.bin`.</Task>
    <Task id="16" status="PASS">UI Toolkit tuner exists and reads/mutates Vault profile/telemetry lanes.</Task>
    <Task id="17" status="PASS">Cold CSV parser writes visor profile DTOs through Vault scratch.</Task>
    <Task id="18" status="PASS">Editor-only stencil preview gizmo exists without runtime persistent native ownership.</Task>
    <Task id="19" status="PASS">HUD Canvas Inquisition report exists and report JSON was refreshed.</Task>
    <Task id="20" status="PASS">Static self-audit, layout math, scans, and compile gate proof recorded.</Task>
  </TaskReconciliation>
  <StructLayoutVerification>
    <DTO name="VisorHudParamsDTO" sizeBytes="64">
      <Field name="TargetCoordinates" offset="0" sizeBytes="16" />
      <Field name="VitalStats" offset="16" sizeBytes="16" />
      <Field name="VisorGlitchParams" offset="32" sizeBytes="16" />
      <Field name="QualityAndTime" offset="48" sizeBytes="16" />
      <Padding bytes="0" />
    </DTO>
    <DTO name="VisorArTargetDTO" sizeBytes="64">
      <Field name="ScreenAndFlags" offset="0" sizeBytes="16" />
      <Field name="ColorAndPulse" offset="16" sizeBytes="16" />
      <Field name="LocalMetersAndDistance" offset="32" sizeBytes="16" />
      <Field name="ShapeParams" offset="48" sizeBytes="16" />
      <Padding bytes="0" />
    </DTO>
    <DTO name="VisorHudDigitParamsDTO" sizeBytes="64">
      <Field name="OxygenDigits" offset="0" sizeBytes="16" />
      <Field name="DepthDigits" offset="16" sizeBytes="16" />
      <Field name="PressureDigits" offset="32" sizeBytes="16" />
      <Field name="WarningDigits" offset="48" sizeBytes="16" />
      <Padding bytes="0" />
    </DTO>
    <DTO name="VisorTelemetryEntry" sizeBytes="64">
      <Field name="FrameIndex" offset="0" sizeBytes="4" />
      <Field name="Flags" offset="4" sizeBytes="4" />
      <Field name="TargetCount" offset="8" sizeBytes="4" />
      <Field name="QualityWeight" offset="12" sizeBytes="4" />
      <Field name="ProjectionMicroseconds" offset="16" sizeBytes="4" />
      <Field name="EstimatedGpuMicroseconds" offset="20" sizeBytes="4" />
      <Field name="FirstTargetDepthMeters" offset="24" sizeBytes="4" />
      <Field name="StateHash" offset="28" sizeBytes="4" />
      <Field name="Oxygen01" offset="32" sizeBytes="4" />
      <Field name="Co201" offset="36" sizeBytes="4" />
      <Field name="FogIntensity01" offset="40" sizeBytes="4" />
      <Field name="StencilScale" offset="44" sizeBytes="4" />
      <Field name="LayoutHash" offset="48" sizeBytes="4" />
      <Field name="VaultGeneration" offset="52" sizeBytes="4" />
      <Field name="CameraPixelWidth" offset="56" sizeBytes="4" />
      <Field name="CameraPixelHeight" offset="60" sizeBytes="4" />
      <Padding bytes="0" />
    </DTO>
    <DTO name="ARWaypointOverlay.StencilTargetSourceDTO" sizeBytes="80">
      <Field name="PositionAup" offset="0" sizeBytes="48" />
      <Field name="Color" offset="48" sizeBytes="16" />
      <Field name="Flags" offset="64" sizeBytes="4" />
      <Field name="StableId" offset="68" sizeBytes="4" />
      <Field name="Reserved0" offset="72" sizeBytes="4" />
      <Field name="Reserved1" offset="76" sizeBytes="4" />
      <Alignment multipleOf="16" result="true" />
    </DTO>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Below quality 0.3, chroma offset approaches zero, curvature displacement stays near the minimum, scanline frequency uses the low endpoint, bracket scale uses the lower lerp value, and fog blend uses the low multiplier. Middle devices receive smooth interpolation. High and Ultra spend saved Canvas CPU/GPU budget on stronger curvature, chroma, scanlines, fog, and bracket pulse without changing DTO layout or authority.
  </ScalabilityCurve>
  <HPhiVaultStatus>
    <PrivatePersistentNativeArraysInRenderer>false</PrivatePersistentNativeArraysInRenderer>
    <VaultBuffer id="73180" type="VisorHudParamsDTO[1]" owner="SystemID.UI" />
    <VaultBuffer id="73181" type="StencilTargetSourceDTO[16]" owner="SystemID.UI" />
    <VaultBuffer id="73182" type="VisorArTargetDTO[16]" owner="SystemID.UI" />
    <VaultBuffer id="73183" type="VisorHudDigitParamsDTO[1]" owner="SystemID.UI" />
    <VaultBuffer id="73184" type="VisorTelemetryEntry[300]" owner="SystemID.UI" />
    <VaultBuffer id="73185" type="VisorHudProfileDTO[16]" owner="SystemID.UI" />
    <VaultBuffer id="73186" type="byte[16384]" owner="SystemID.UI" />
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    <BurstJobsInFinalVisorPath>false</BurstJobsInFinalVisorPath>
    <Reason>Tiny bounded payloads rejected; direct local math and direct MemCpy are cheaper than same-frame job dispatch.</Reason>
    <JobHandlesConsumed>none</JobHandlesConsumed>
    <JobHandlesProduced>none</JobHandlesProduced>
    <RenderGraphBuffers>Hud/Digit/Target GraphicsBuffers imported and declared read-only through BufferHandle UseBuffer.</RenderGraphBuffers>
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    No new asmdef or sibling runtime assembly reference was introduced. Existing Visor/UI scripts remain in the project root assembly shape; compile-wall isolation remains a broader repo debt, not expanded by this lane.
  </CompileGuard>
  <DearLieConfirmation>
    Before: Canvas/TMP marker/text route scales with UI hierarchy dirtiness and text mesh rebuilds. After: O(16) local projection plus one stencil mask draw and one resolve pass; digits/fog/brackets are shader fakes. No particles, no world marker GameObjects, no TMP runtime text meshes.
  </DearLieConfirmation>
  <CompileStatus>BLOCKED_BY_CPU_GATE_CIM_88_PERCENT_COUNTER_83_7_TO_100_PERCENT</CompileStatus>
</SELF_AUDIT>

## SHINOBU_270 POLISH ADDENDUM - BUILD GATE WATCH

Date: 2026-05-21

What was wrong:
- Prior LOG tail reflected an earlier CPU-only compile block and not the later compiler contention window plus latest CPU-only gate.

What was done:
- Re-read `Status_SHINOBU_270.md`, `Rationale_SHINOBU_270.md`, and re-extracted the `SHINOBU_270` XML prompt from `CURRENT_BATCH.md`.
- Re-ran the targeted forbidden-token scan on `HectonVisorARStencilRendererFeature.cs` and `Hecton_VisorAR.shader`; no `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, `BurstCompile`, `IJob`, `.Run()`, persistent runtime `NativeArray`, or depth-sampling residue was found.
- Re-parsed `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`; JSON remains valid and records the final compiler-contention gate state.

Cinematic Cheats used:
- No new runtime cheat added in this addendum. Existing route remains procedural seven-segment digits, shader-side visor fog/scanlines/brackets, and stencil-only AR resolve instead of Canvas/TMP/particle simulation.

Exact Microseconds saved:
- This addendum saves 0 runtime us; it prevents a false build-proof claim and avoids launching a competing build under load.

Build gate:
- Build not launched.
- Final observed compiler contention from the previous gate watch: `dotnet` PID 12412 and `csc` PID 32352 active, CPU samples 75.1%, 71.4%, 84.6%, 91.8%, 83.9%.
- Latest follow-up process query returned no compiler rows, but CPU samples were still 97.7%, 100%, 79.3%.
- Decision: compile remains blocked by AGENTS.md gate because CPU stayed above 50%; no competing build was launched.

## SHINOBU_270 POLISH ADDENDUM - SUBAGENT AUDIT HARDENING

Date: 2026-05-21

What was wrong:
- RenderGraph suppression flags stayed enabled on renderer disable/dispose, so fallback Canvas state could remain hidden after a failed/no-pass renderer.
- `Hecton_VisorAR.shader` hard-coded `ReadMask 255` while the stencil writer had a configurable write mask.
- Generated fallback visor mesh could be rejected by `Cull Front`.
- `ARWaypointOverlay` still had a runtime retry route for vegetation bridge resolution.
- `SuitHUDV4CanvasOverlay` could auto-bind Canvas components after scene load while stencil presentation owned the HUD.
- Legacy HUD text staging could grow `char[]` arrays for long localized/template input.
- `Dump_SHINOBU_270.bin` used `BinaryWriter` instead of raw span row dumping.
- The report claimed full managed HUD purge while legacy raycaster tokens still exist behind stencil fences.

What was done:
- `HectonVisorARStencilRendererFeature` clears stencil suppression on disable/dispose and enables it only after pass prerequisites and frame upload succeed.
- AR shader now exposes `_StencilReadMask`; feature sets it from the configured stencil write mask.
- Stencil pass no longer binds a color target; AR resolve uses read-only depth/stencil; stencil mask shader uses `Cull Off`.
- `ARWaypointOverlay` no longer polls bridge state from Tick/SlowTick; it resolves cold or via `MapMagicVegetationRuntime` hot-swap.
- `SuitHUDV4CanvasOverlay` scene-load Canvas bootstrap exits under stencil mode, proxy-light AUP conversion is fenced while stencil is active, and `GlobalSignals` reads were removed from this file.
- Legacy HUD buffers now pre-size metric display staging and truncate writes into fixed buffers during play instead of growing arrays.
- Fault dump now writes a 32-byte little-endian header plus raw 64-byte telemetry rows via `ReadOnlySpan<byte>`.
- `HUDCanvasInquisition` and `RENDERING_OPTIMIZATION_REPORT.json` now report that runtime stencil takeover is active but full managed source purge is false until legacy raycaster tokens are deleted.

Cinematic Cheats used:
- No physical HUD simulation was added. The route remains one stencil mask plus shader-side procedural digits, fog, scanlines, and brackets.

Exact Microseconds saved:
- AR waypoint bridge retry removal: estimated 1-5 us CPU variance saved when vegetation bridge is unresolved.
- Hidden Canvas bootstrap fence: prevents one scene-load Canvas component binding path and later rebuild risk; exact spike depends on scene hierarchy.
- Legacy char growth fence: removes rare managed allocation spikes from long localized text in the disabled Canvas path.
- RenderGraph access/cull fixes are correctness/resource-hazard reductions; profiler proof still pending.

Verification:
- Targeted scan found no `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, `BurstCompile`, `IJob`, `.Run()`, persistent runtime `NativeArray`, or depth texture residue in the visor/stencil/SuitHUD target set.
- `git diff --check` on touched files returned only existing LF-to-CRLF normalization warnings.
- Unity import, shader import, RenderGraph execution, Frame Debugger, profiler/GCMonitor, and compile proof remain pending behind the build gate.

Build gate:
- Build not launched.
- Process query returned no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` rows.
- CPU gate stayed closed: CIM CPU 100%; processor counter samples 100%, 100%, 100%, 100%, 100%.
- Decision: compile remains blocked by AGENTS.md because CPU is above 50%.

## SHINOBU_270 POLISH ADDENDUM - SHARED REPORT FACADE HARDENING

Date: 2026-05-21

What was wrong:
- `HUDCanvasInquisition.Run()` could overwrite the shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with a single SHINOBU_270 object.
- That would delete neighboring proof objects such as `jacobianFoam`, `shinobu_267_flora_ambient_sway`, `shinobu_278_coop_input_prediction`, and `shinobu_275_screen_space_wound_decal_compressor`.

What was done:
- The editor scanner now builds the SHINOBU_270 report object and upserts it as top-level key `shinobu_270_visor_ar_stencil`.
- Existing shared-report body is preserved; only a prior SHINOBU_270 section with the same key is replaced.
- Malformed or absent shared report falls back to a minimal object containing the SHINOBU_270 section, rather than pretending a merge occurred.

Cinematic Cheats used:
- No runtime visual change. The HUD route remains stencil mask plus shader-side digits, fog, scanlines, and AR brackets.

Exact Microseconds saved:
- Runtime: 0 us. This is editor-only evidence preservation.
- Integration: prevents cross-agent report-loss churn and avoids rerunning other validators to reconstruct deleted proof objects.

Verification:
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` still parses through `python -m json.tool`.
- Targeted visor/SuitHUD forbidden-token scan remains empty.
- `git diff --check` on `HUDCanvasInquisition.cs` is clean.

Build gate:
- Build not launched.
- Initial gate found active `dotnet.exe` compiling `Hecton8.Core.csproj`.
- Follow-up gate found no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` rows, but CPU stayed 100% by CIM and 100%, 100%, 100%, 100%, 100% by counter samples.
- AGENTS.md build gate remains closed.
## SHINOBU_270 POLISH ADDENDUM - SUBAGENT P0 CLOSURE

What was wrong:
- `HectonVisorARStencilRendererFeature` used the `Shader.SetGlobalConstantBuffer` argument order against `RasterCommandBuffer`; local SRP source requires `GraphicsBuffer` first.
- `HUDCanvasInquisition` directly referenced `UnityEngine.UI.GraphicRaycaster` from `Hecton8.UI.Editor.asmdef`, widening editor import risk.
- Runtime stencil suppression could fail closed if the feature was absent/import-broken or if frame prep failed after a previous good frame.

What was done:
- Swapped RenderGraph constant-buffer binding to `SetGlobalConstantBuffer(GraphicsBuffer, nameID, offset, size)`.
- Removed direct `UnityEngine.UI` dependency from `HUDCanvasInquisition`; cold editor scan now counts GraphicRaycaster by component type full name.
- Moved runtime suppression ownership to `HectonVisorARStencilRendererFeature`: defaults reset false, presentation controller only drives editor preview, the renderer enables suppression only after successful Game/Base frame prep and clears it on concrete failure paths.
- Removed the editor gizmo's `FromRuntimePosition` bridge; Scene View target rays now derive camera AUP from `HectonFloatingOrigin.CurrentTotalOffsetDouble` plus local runtime camera position in double precision.

Cinematic cheats used:
- No TMP/Canvas text route was restored for the active path. Fallback is only fail-open safety when renderer proof is absent; the active route remains procedural seven-segment digits and stencil-gated shader AR.

Microseconds saved / protected:
- 0 runtime us from the compile-ABI patch itself.
- Protected 250-750 us Canvas rebuild/overdraw savings on successful RenderGraph frames by avoiding per-frame release/re-suppress churn.
- Prevented a blind-HUD failure mode when renderer proof is absent; this is correctness, not a perf claim.

Verification:
- Targeted forbidden scan returned no `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, persistent runtime `NativeArray`, or depth texture residue in SHINOBU_270 target files.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses.
- `git diff --check` on patched target files is clean except Git LF-to-CRLF warnings.
- Compile remains PENDING VERIFICATION: one gate found active `dotnet.exe` PID 38292 building `Hecton8.Core.csproj` and `csc.exe` PID 26708; latest gate returned no compiler rows but CPU gate stayed closed at CIM 100% and processor samples 98.5/100/100.

## SHINOBU_270 BUILD GATE SNAPSHOT - FINAL REPORT INPUT

Date: 2026-05-21

What was wrong:
- Compile proof is still required, but AGENTS.md blocks dotnet while CPU exceeds 50%.

What was done:
- Rechecked process and CPU gates after the P0 closure patches.
- Process query returned zero `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` rows.
- CPU remained saturated: CIM CPU 100%; processor counter samples 100%, 100%, 100%.

Cinematic cheats used:
- None. Verification gate only.

Exact Microseconds saved:
- Runtime: 0 us. Workstation contention avoided by not starting another compiler under full CPU load.

Verification:
- Build not launched.
- Compile remains PENDING VERIFICATION until CPU gate falls below 50% and no compiler process is active.

## SHINOBU_270 POLISH ADDENDUM - PLAYER CAMERA STENCIL HARDENING

Date: 2026-05-21

What was wrong:
- Broad Game/Base camera acceptance could let minimap, capture, spectator, or other non-player cameras suppress the HUD Canvas fallback.
- Stencil/AR `RecordRenderGraph` early exits could leave renderer-owned suppression active after a previous good frame.
- Default stencil mask `255` claimed every stencil bit and created avoidable cross-pass contamination risk.
- The fullscreen AR shader carried unused instancing variants and had no explicit SHINOBU_270 warmup artifact.

What was done:
- Runtime stencil takeover now requires strict `IPlayerRuntimeContext.PlayerCamera` reference equality.
- `RecordRenderGraph` aborts for backbuffer/invalid source/depth resources clear stencil suppression to fail open.
- Stencil read/write defaults now reserve bit 0 only; legacy serialized `255` writer masks are coerced to lane 1.
- Removed fullscreen shader instancing pragmas and added `Assets/_Project/Art/Shaders/Variants/Hecton_VisorAR_Stencil.shadervariants`.
- Superseded by the later bootstrap patch: renderer `Create()` no longer owns variant warmup; `00_BOOTSTRAP.unity` serializes the SVC for `GameBootstrapper` boot prewarm.
- Architecture docs and shared rendering report now record player-camera ownership, abort fail-open, stencil lane, and warmup routes.

Cinematic cheats used:
- No Canvas/TMP route was restored. Active presentation remains one helmet stencil mask and shader-side procedural digits, scanlines, fog, and compacted AR brackets.

Exact Microseconds saved:
- Runtime savings from this addendum: 0 us directly.
- Protected savings: prevents non-player camera passes from forcing Canvas suppression churn or blind fallback; preserves the earlier 250-750 us Canvas rebuild/overdraw avoidance only for proven player-camera frames.
- Warmup impact: cold boot only; reduces first-use shader hitch risk without runtime variant creation.

Verification pending:
- Static scans, JSON validation, diff whitespace check, and build-gate sampling are the next required proof steps.

## SHINOBU_270 POLISH ADDENDUM - STATIC VERIFICATION AND BUILD GATE

Date: 2026-05-21

What was wrong:
- The first post-polish forbidden-token scan caught one editor-only `new NativeArray` in `HectonVisorStencilPreviewGizmo`.
- Compile proof remained unavailable under the AGENTS.md CPU/compiler gate.

What was done:
- Added a `Span<ARWaypointOverlay.StencilTargetSourceDTO>` overload to `ARWaypointOverlay.CopyStencilTargetSources`.
- Replaced the gizmo Temp `NativeArray` with a fixed `stackalloc` span for the 16-row editor preview.
- Re-ran JSON validation, targeted forbidden-token scans, RenderGraph/stencil shader sanity scan, `git diff --check`, owned-file git status, and build-gate sampling.

Cinematic cheats used:
- Runtime path unchanged: one stencil mask draw plus shader-side procedural digits, scanlines, fog, and AR brackets. No Canvas/TMP renderer was restored.

Exact Microseconds saved:
- Runtime: 0 us from this verification addendum.
- Editor preview: removes a tiny Temp allocation and one dispose path from Scene View gizmo drawing.
- Build gate: avoids launching a compiler while CPU is saturated, protecting the shared workstation.

Verification:
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses through `python -m json.tool`.
- Targeted visor/UI forbidden scan returned no hits for `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, `.Complete()`, persistent runtime `NativeArray`, Temp `new NativeArray`, or `_CameraDepthTexture`.
- RenderGraph/stencil scan returned no private `OnEnable`, no stencil `SetFloat`, no fullscreen instancing pragmas, no `255` stencil defaults, and no ownerless pass constructors.
- `git diff --check` reports only LF-to-CRLF warnings for tracked touched files, no whitespace errors.

Build gate:
- Build not launched.
- Compiler process query returned no rows.
- CPU gate stayed closed: CIM CPU 100%; latest processor counter samples 98.84%, 99.81%, 100%.
- Compile remains PENDING VERIFICATION until CPU is below 50% and no compiler process is active.
## 2026-05-21 - Bootstrap Shader Warmup Route Patch

What was wrong:
- `HectonVisorARStencilRendererFeature.Create()` still owned `ShaderVariantCollection.WarmUp()`. Renderer-feature creation/reload is not the loading-screen warmup lane and can hitch first visor activation.
- `HectonVisorStencilPreviewGizmo` stackalloced the full 16-row target source DTO set for SceneView preview, about 1280B per draw.

What was done:
- Removed the feature-local SVC field and all renderer-feature `WarmUp()` calls.
- Serialized `Hecton_VisorAR_Stencil.shadervariants` into `Assets/_Project/Scenes/00_BOOTSTRAP.unity` under `BootstrapController.shaderVariantCollections`, using the existing `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` boot prewarm route.
- Updated `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md` and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to name the bootstrap warmup owner and reject renderer-owned warmup.
- Capped the editor preview gizmo scratch span to 3 target rows.

Cinematic Cheats used:
- Still shader-side seven-segment digits and stencil-gated fog/brackets; no Canvas text, TMP mutation, particles, or physical fog simulation were reintroduced.

Exact Microseconds saved:
- Steady frame: 0 us claimed for warmup route; value is hitch containment, not per-frame optimization.
- Editor preview stack scratch: about 1040B less stack per SceneView draw (`16 * 80B` -> `3 * 80B`).
- Potential first-activation shader hitch moved out of renderer lifecycle into existing boot prewarm; exact ms requires Unity/player profiler proof.

Verification state:
- Static recheck after this patch is clean for the targeted scans.
- The edited `00_BOOTSTRAP.unity` component script GUID matches `BootstrapController.cs.meta`; the SVC guid is under `shaderVariantCollections`.
- Legal build gate remains closed: no compiler process, but the final resample returned `100%` CIM and `100%,100%,100%` counter samples.
