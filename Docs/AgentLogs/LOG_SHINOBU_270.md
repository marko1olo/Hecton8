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

## 2026-05-22 - Stale Watchdog / Hot-Poll Closure

What was wrong:
- The same-frame RenderGraph watchdog closed the pending-frame abort path, but it did not cover a later authorized player-camera frame where the feature was absent or not invoked. That could keep renderer-owned Canvas suppression true from the last successful resolve.
- `SuitHUDV4CanvasOverlay.TryRegisterRuntimeTick()` still reached `GlobalRegistry.Dispatcher` from `SlowTick()` after late-frame and slow-tick registration were already complete.
- `CopyActiveOverlaysTo()` could grow the caller-owned `List<T>` if active overlays exceeded the scratch list capacity.
- `VISOR_AR_STENCIL_RENDERER.md` claimed the mask shader wrote depth/stencil, while `Hecton_VisorStencilMask.shader` has `ZWrite Off`.

What was done:
- `MarkStencilResolveRecorded()` now clears `_pendingStencilPresentationFrame` after proven resolve record.
- `OnEndCameraRendering()` now clears renderer-owned suppression on any authorized player-camera frame that ends without a same-frame resolve record.
- `TryRegisterRuntimeTick()` now exits before `GlobalRegistry.Dispatcher` when local registration state already proves late-frame and slow-tick registration.
- `CopyActiveOverlaysTo()` now treats caller list capacity as a hard limit and returns instead of triggering managed growth.
- Architecture wording now matches the shader: ColorMask 0, Cull Off, ZWrite Off, stencil lane write only, depth attachment used for test/order.

Cinematic Cheats used:
- Runtime presentation remains the same optical fake: stencil-gated fullscreen shader digits, brackets, scanlines, and breath fog. No Canvas/TMP text, CPU particle fog, per-waypoint GameObject renderer, or physical visor simulation was restored.

Exact Microseconds saved:
- Stale watchdog patch: 0 us steady successful frame claimed; correctness guard against blind HUD.
- Hot-poll guard: estimated 1-3 us CPU variance reduction on i3/MX350-class CPUs after registration, pending profiler proof.
- Overlay capacity guard: removes rare managed allocation risk; time saved depends on overlay count and previous `List<T>` growth path.

Verification:
- Case-sensitive targeted forbidden-token scan returned no hits for `GlobalSignals`, `FromRuntimePosition`, `Shader.SetGlobal*`, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, `.Run()`, `.Complete()`, `new NativeArray`, `_CameraDepthTexture`, `foreach`, LINQ `.Select(` / `.Where(`, or `string.Format` in SHINOBU_270 target files.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses.
- `git diff --check` on patched SHINOBU_270 files reports only Git LF-to-CRLF warning for `VISOR_AR_STENCIL_RENDERER.md`.

Build gate:
- Build not launched.
- Active compiler processes found: `csc` PID 24148 and `dotnet` PID 15396.
- CPU gate stayed closed: CIM CPU 84%; processor counter samples 100%, 100%, 100%.
- Compile remains PENDING VERIFICATION because generated `Hecton8.Core.csproj` is still stale for the new SHINOBU_270 renderer/gizmo scripts and the active-compiler/CPU gates are closed.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_270" evidence="STATIC_SOURCE" verification="PENDING_UNITY_IMPORT_AND_BUILD">
  <task_reconciliation note="Tasks 01-20 remain PASS from previous audit; Iteration 24 tightens Task 02 fail-open ownership, Task 19 proof accuracy, and Task 20 self-audit honesty without changing authority routes or DTO identity."/>
  <struct_layout note="No DTO layout changed. Primary 64-byte DTO offsets recorded in the prior SELF_AUDIT remain valid."/>
  <scalability note="No binary hardware route was added. `GlobalQualityWeight` shader math remains continuous; watchdog only controls presentation fail-open when resolve proof is absent."/>
  <h_phi note="No new persistent native container or private array ownership added. Overlay copy now respects caller capacity instead of growing managed storage."/>
  <dependency_graph note="No new jobs and no `.Complete()` added. Renderer still consumes RenderGraph camera resources and outputs the AR resolve only after record proof."/>
  <compile_guard note="No sibling assembly dependency added. Build not run because active compiler and CPU gates are closed, and generated project coverage remains stale."/>
  <dear_lie note="The stencil shader documentation now matches source: stencil lane write only, no depth write, no color write."/>
</SELF_AUDIT_ADDENDUM>

## SHINOBU_270 POLISH ADDENDUM - REPORT ARTIFACT REVERIFICATION

What was wrong:
- The live `CURRENT_BATCH.md` extraction initially failed because the exact-tag parser ignored `role` and `chat_name` attributes on the `SHINOBU_270` prompt.
- `HUDCanvasInquisition` source had been patched to emit `generatedProjectStale`, but the existing shared rendering report artifact did not yet expose those generated-project fields.
- The older report text still implied renderer `Create()` shader warmup, which is no longer the owned route.

What was done:
- Re-extracted `<AGENT_PROMPT id="SHINOBU_270" role="VISOR_AR_STENCIL_RENDERER" chat_name="SHINOBU_270">` with an attribute-aware CLI regex. Result: `PROMPT_BYTES=17478`, `TASK_COUNT=20`.
- Added `shinobu_270_visor_ar_stencil` to `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` without deleting neighboring agent objects.
- The new report section marks `generatedProjectIncludesRendererFeature=false`, `generatedProjectIncludesStencilPreviewGizmo=false`, and `generatedProjectStale=true`.
- Corrected the older shader warmup report wording to the bootstrap `BootstrapController.shaderVariantCollections` route; renderer `Create()` does not own shader warmup.

Cinematic Cheats used:
- No Canvas revival, TMP text path, or CPU physics route was added. The report continues to prove the Dear Lie route: stencil-gated fullscreen visor shader, procedural digits, AUP-local AR brackets, and shader-side fog/scanline/glitch.

Exact Microseconds saved:
- Runtime: 0 us changed in this pass. This is evidence hygiene.
- Review/build time saved: prevents a false `dotnet build Hecton8.Core.csproj` proof while the generated project omits the new renderer/gizmo scripts.

Verification:
- JSON parses after the report patch.
- Scoped scan returned no hot-path C# property tokens, LINQ, foreach, string formatting/interpolation, `Shader.SetGlobal*` static calls, `Canvas.ForceUpdateCanvases`, `GlobalSignals`, `FromRuntimePosition`, `TryGetLatestCreated`, persistent `NativeArray`, `.Run()`, or `.Complete()` in the SHINOBU_270 target files.
- `Hecton8.Core.csproj` still includes `HectonVisorFluidDistortionFeature.cs` and still omits `HectonVisorARStencilRendererFeature.cs` / `HectonVisorStencilPreviewGizmo.cs`.
- Build not launched: no compiler rows were visible in the latest process sample, but CPU was 87% by CIM and 91.87%, 67.06%, 79.33% by processor counter samples.

## SHINOBU_270 POLISH ADDENDUM - SHADER MATH-LOD TAP SHEDDING

What was wrong:
- `Hecton_VisorAR.shader` used `quality` to visually blend chromatic aberration, but still paid the two red/blue offset texture samples at `GlobalQualityWeight=0`.
- The AR target loop was `[unroll]` over all 16 rows even when the uploaded active target count was lower.

What was done:
- Added `chromaWeight = smoothstep(0.06, 1.0, quality)` and a branch that skips the two aberration taps when the continuous admission weight is effectively zero.
- Replaced the fixed unrolled target loop with a loop bounded by `_HectonVisorQualityAndTime.z`, the active target count uploaded by `BuildAndUploadFrame`.
- Updated `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md` to document the low-quality chroma tap admission and active-target loop bound.

Cinematic Cheats used:
- Still no Canvas/TMP text, no CPU projector objects, and no physics simulation. Visual richness remains shader-side: procedural digits, scanlines, bracket linework, fog, and chroma.

Exact Microseconds saved:
- GPU: avoids up to two fullscreen color texture taps per visor AR pixel on survival-tier quality and avoids inactive target-row reads/bracket math when target count is below 16.
- CPU: 0 us changed in this pass.
- Exact GPU us remains PENDING VERIFICATION until Unity Frame Debugger/GPU capture.

## 2026-05-22 - RenderGraph Suppression Fail-Open Watchdog

What was wrong:
- Subagent audit found a real fail-closed route: `AddRenderPasses` enabled visor stencil presentation before the AR resolve `RecordRenderGraph` path proved output.
- Existing abort cleanup handled explicit `RecordRenderGraph` failures, but compatibility/no-graph/drop paths could leave Canvas hidden with no AR resolve.

What was done:
- Removed pre-record `SetStencilPresentationActive(true)` from `AddRenderPasses`.
- Added a pending player-camera frame token.
- `ArPass.RecordRenderGraph` now calls `MarkStencilResolveRecorded()` only after creating the resolve texture and assigning `resourceData.cameraColor`.
- Added a cold `RenderPipelineManager.endCameraRendering` watchdog that clears suppression if the authorized player camera ends without a matching resolve record.
- Updated route docs, payload ledger, status, and rationale.

Cinematic Cheats used:
- Runtime visuals remain the same Dear Lie route: stencil mask plus shader-side seven-segment digits, scanlines, fog, and compacted AR brackets.
- No Canvas/TMP route was restored as an active renderer; Canvas is only fail-open fallback when RenderGraph proof is absent.

Exact Microseconds saved:
- Steady successful frame: 0 us claimed from the watchdog patch.
- Protected savings: 250-750 us Canvas rebuild/overdraw avoidance is now applied only to proven RenderGraph frames, not to unproven/no-output frames.

Verification:
- Targeted forbidden-token scan stayed clean after the patch.
- `git diff --check` on the renderer reported only Git LF-to-CRLF warning.
- Compile remains PENDING VERIFICATION until Unity regenerates/imports SHINOBU_270 scripts and the build gate is legally open.

Post-watchdog gate:
- Generated `Hecton8.Core.csproj` still includes `SuitHUDV4CanvasOverlay.cs`, `ARWaypointOverlay.cs`, and `HectonVisorFluidDistortionFeature.cs`, but not `HectonVisorARStencilRendererFeature.cs` or `HectonVisorStencilPreviewGizmo.cs`.
- Build not launched.
- Active compiler processes were present: `csc.exe`, multiple `dotnet.exe` processes, and `VBCSCompiler.exe`.
- CPU gate was closed: CIM CPU 100%; processor counter samples were effectively 100%.
- Later gate still had active `dotnet.exe` and `VBCSCompiler.exe` rows; CPU samples were CIM 35% and processor counter 25.38%, 51.50%, 22.33%, so active compiler plus one over-threshold counter sample kept the build gate closed.
- Compile remains PENDING VERIFICATION; Unity project regeneration/import is still required before external `dotnet build` covers the new SHINOBU_270 renderer scripts.

## 2026-05-22 - Generated Project Static Gate

What was wrong:
- The stale generated `Hecton8.Core.csproj` problem was documented, but the SHINOBU_270 editor proof facade did not expose it as a repeatable report field.

What was done:
- `HUDCanvasInquisition` now reads `Hecton8.Core.csproj` cold and checks exact `Compile Include` coverage for `HectonVisorARStencilRendererFeature.cs` and `HectonVisorStencilPreviewGizmo.cs`.
- The shared rendering report section now emits `generatedProjectIncludesRendererFeature`, `generatedProjectIncludesStencilPreviewGizmo`, and `generatedProjectStale`.

Cinematic Cheats used:
- Runtime route unchanged: shader-side digits/fog/brackets behind stencil. This patch is editor proof only.

Exact Microseconds saved:
- Runtime: 0 us.
- Proof protection: prevents stale generated project coverage from being mistaken for compile evidence.

## 2026-05-22 - AR Target Upload MemCpy Patch

What was wrong:
- The log claimed direct `UnsafeUtility.MemCpy` for mapped GPU uploads, but `ArPass.UpdateGpuPayload` still copied `VisorArTargetDTO` rows with bounded per-row C# loops.
- This was not a gameplay truth bug, but it was an evidence mismatch in Task 11's upload route.

What was done:
- Replaced the target row copy/clear loops with `CopyTargetsToMappedBuffer`.
- Active target rows copy through `UnsafeUtility.MemCpy`.
- Unused mapped rows clear through `UnsafeUtility.MemClear`.
- No DTO layout, BufferID, shader resource name, RenderGraph resource declaration, stencil lane, telemetry ABI, or rollback exclusion changed.

Cinematic Cheats used:
- Runtime route unchanged: stencil mask plus shader-side procedural digits, breath fog, scanlines, and compacted AR brackets. No Canvas/TMP layout, CPU particles, or per-target GameObjects were restored.

Exact Microseconds saved:
- Estimated 1-3 us CPU variance reduction on i3/MX350-class hardware pending profiler proof.
- Primary gain is proof correctness: the code now matches the claimed mapped-buffer upload route.

Verification:
- Targeted visor/UI/shader forbidden scan returned no hits for `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, `.Complete()`, persistent runtime `NativeArray`, or `_CameraDepthTexture`.
- `git diff --check` on the changed renderer and SHINOBU_270 docs reported only Git LF-to-CRLF warning.
- Build not launched: generated `Hecton8.Core.csproj` remains stale for new SHINOBU_270 script entries, and compile proof remains PENDING VERIFICATION until Unity regenerates/imports project files and the CPU/compiler gate is legal.

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

## 2026-05-21 - Static API Recheck / Active Compiler Gate

What was wrong:
- Context had been compacted and the previous dirty-state memory no longer matched the current git index; source truth needed to be taken from disk.
- Static forbidden-token scans do not prove RenderGraph command overloads, CBUFFER names, or SVC scene routing.
- Compile proof is still blocked by workstation policy.

What was done:
- Confirmed SHINOBU_270 owned files are tracked and clean; current dirty files are unrelated agent domains and were not touched.
- Rechecked local SRP APIs: `RasterCommandBuffer.SetGlobalTexture(int, TextureHandle)`, `SetGlobalBuffer(int, GraphicsBuffer)`, `SetGlobalConstantBuffer(GraphicsBuffer, int, int, int)`, and `CoreUtils.DrawFullScreen(RasterCommandBuffer, ...)` exist and match the renderer feature.
- Rechecked C# to shader ABI: `HectonVisorHudParams`, `HectonVisorDigitParams`, and `_HectonVisorArTargets` names align with shader CBUFFER/StructuredBuffer declarations.
- Rechecked bootstrap/SVC route: `00_BOOTSTRAP.unity` points at `BootstrapController` GUID `37290befeffd3d94796e62b9097c7db9` and SVC GUID `27027027027027027027027027027027`.
- Re-ran JSON parse and diff whitespace check.
- Reused subagent Locke for a read-only scoped import/RenderGraph/SVC audit; returned no P0/P1/P2 findings.

Cinematic Cheats used:
- No Canvas/TMP renderer was restored. Active visual path remains one stencil mask draw plus shader-side seven-segment digits, scanlines, fog, and AR brackets.

Exact Microseconds saved:
- Runtime: 0 us from this audit pass.
- Protected savings: prevents an import/API mismatch from forcing Canvas fallback or blanking the HUD; preserves earlier 250-750 us Canvas rebuild/overdraw avoidance only when the stencil renderer proves readiness.

Build gate:
- Build not launched.
- Active compiler processes found: `dotnet` PID 10784 and `csc` PID 25392.
- CPU gate stayed closed: CIM CPU 100%; processor counter samples 86.74%, 94.06%, 96.76%.
- Compile remains PENDING VERIFICATION until CPU is below 50% and no compiler process is active.

## 2026-05-21 - Generated Project Verification Limitation

What was wrong:
- The generated `Hecton8.Core.csproj` is an explicit Unity compile list and is currently stale for SHINOBU_270.
- It contains `Assets\_Project\Scripts\Visor\HectonVisorFluidDistortionFeature.cs`, but does not contain `HectonVisorARStencilRendererFeature.cs` or `HectonVisorStencilPreviewGizmo.cs`.
- A `dotnet build Hecton8.Core.csproj` against this state would not compile the new SHINOBU_270 scripts and would be false verification.

What was done:
- Recorded the project-file staleness in status and rationale.
- Left `Hecton8.Core.csproj` untouched because it is generated and says not to modify it directly.
- Resampled the build gate after the audit.

Cinematic Cheats used:
- Runtime route unchanged: stencil mask draw plus shader-side seven-segment digits, scanlines, fog, and AR brackets. No Canvas/TMP renderer was restored.

Exact Microseconds saved:
- Runtime: 0 us from this verification pass.
- Proof protection: prevents a stale-project build from hiding import/compile defects in the actual Unity script graph.

Build gate:
- Build not launched.
- Active compiler processes found: `dotnet` PID 12844 and `csc` PID 29340.
- CPU gate stayed closed by CIM CPU 93%; processor counter samples were 65.84%, 86.14%, 34.97%, 38.23%, 46.6%.
- Compile remains PENDING VERIFICATION until Unity regenerates/imports the new script project entries and no compiler process is active under the CPU gate.

## 2026-05-21 - Build Gate Watch / Active Compiler Window

What was wrong:
- Compile proof is still pending, but another C# compilation window remained active after the generated-project audit.

What was done:
- Resampled compiler and CPU gates.
- Did not launch `dotnet build`.

Cinematic Cheats used:
- Runtime route unchanged: stencil mask draw plus shader-side procedural digits/fog/brackets.

Exact Microseconds saved:
- Runtime: 0 us from this gate watch.
- Workstation protection: avoids competing compiler CPU/IO contention.

Build gate:
- Build not launched.
- Active compiler processes found: `dotnet` PID 30716 and `csc` PID 14152.
- CPU gate stayed closed: CIM CPU 73%; processor counter samples 60.81%, 67.67%, 50.23%.
- Compile remains PENDING VERIFICATION.

## 2026-05-22 - Vault Descriptor Lifecycle Patch

What was wrong:
- `HectonVisorARStencilRendererFeature` dropped local `VaultGenerationHandle<T>` descriptors without releasing the underlying Vault references on dispose, DataVault service replacement, or cold service rebind.
- The old private helper encoded the wrong local pattern: clear metadata first, native ownership unresolved.

What was done:
- Added release-first descriptor cleanup for the seven SHINOBU_270 visual lanes: HUD params, source targets, projected targets, digit params, telemetry ring, profile DTOs, and CSV scratch.
- Dispose now releases owned Vault descriptors and nulls `_dataVault`.
- DataVault hot-swap and cold service rebind release old descriptors before binding the new vault.
- Removed the stale `ClearVaultHandles()` helper.
- Updated status, rationale, architecture docs, and payload ledger.

Cinematic Cheats used:
- Runtime route unchanged: one cheap stencil mask draw plus shader-side procedural digits, scanlines, breath fog, and AR brackets. No Canvas/TMP text renderer, CPU particles, or physical fog simulation was restored.

Exact Microseconds saved:
- Steady frame: 0 us claimed; this is lifecycle ownership hardening.
- Cold reload/hot-swap: prevents stale native refcounts and compaction blockers. Exact memory/time delta requires Unity Memory Profiler and DataVault telemetry proof.

Verification:
- Targeted visor/UI/shader forbidden scan returned no hits for `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, `.Complete()`, persistent runtime `NativeArray`, or `_CameraDepthTexture`.
- `git diff --check` on the patched renderer reported only Git LF-to-CRLF warning.
- Local source confirms `ReleaseVaultHandles()` calls `IDataVault.ReleaseBuffer(in handle)` only when the handle has a nonzero BufferID and Generation, then tombstones the descriptor.

Build gate:
- Build not launched.
- Active compiler processes found: `dotnet` PID 34832 and `csc` PID 15644.
- CPU gate stayed closed: CIM CPU 100%; processor counter samples 90.05%, 82.47%, 86.72%.
- Compile remains PENDING VERIFICATION; generated `Hecton8.Core.csproj` is still stale until Unity regenerates/imports SHINOBU_270 new scripts.

Follow-up verification:
- `git diff --check` on patched source/docs returned only Git LF-to-CRLF warning for the binary payload ledger.
- Later build gate found active compiler processes again: `dotnet` PID 22280 and `csc` PID 13460.
- CPU samples were mixed but still illegal for build: CIM CPU 43%; processor counter samples 85.74%, 72.45%, 95.18%.
- Build remains PENDING VERIFICATION.

<SELF_AUDIT agent_id="SHINOBU_270" evidence="STATIC_SOURCE" verification="PENDING_UNITY_IMPORT_AND_BUILD">
  <task_reconciliation>
    <task id="01" result="[PASS]" note="HUD/AR archaeology integrated with SuitHUDV4CanvasOverlay and ARWaypointOverlay"/>
    <task id="02" result="[PASS]" note="Canvas runtime suppression is renderer-owned and fail-open"/>
    <task id="03" result="[PASS]" note="primary unmanaged DTOs use raw public fields"/>
    <task id="04" result="[PASS]" note="64-byte explicit layout guards exist"/>
    <task id="05" result="[PASS]" note="mock HUD data path remains visual-only"/>
    <task id="06" result="[PASS]" note="stencil mask pass writes lane bit 0"/>
    <task id="07" result="[PASS]" note="single RenderGraph AR resolve after transparents"/>
    <task id="08" result="[PASS]" note="shader-side seven-segment digit Dear Lie"/>
    <task id="09" result="[PASS]" note="AUP target-camera double subtraction before float projection"/>
    <task id="10" result="[PASS]" note="GlobalQualityWeight drives continuous shader ALU"/>
    <task id="11" result="[PASS]" note="double-buffered GraphicsBuffer LockBufferForWrite upload"/>
    <task id="12" result="[PASS]" note="breath fog is shader scalar/noise, no CPU particles"/>
    <task id="13" result="[PASS]" note="AUP targeting guarded against absolute-float projection"/>
    <task id="14" result="[PASS]" note="73180..73186 excluded from rollback truth"/>
    <task id="15" result="[PASS]" note="300-frame 64-byte telemetry ring and raw dump path"/>
    <task id="16" result="[PASS]" note="editor tuner route exists; runtime unaffected"/>
    <task id="17" result="[PASS]" note="cold CSV profile parser uses scratch lane"/>
    <task id="18" result="[PASS]" note="editor gizmo uses bounded stack span and AUP math"/>
    <task id="19" result="[PASS]" note="HUDCanvasInquisition preserves shared report object"/>
    <task id="20" result="[PASS]" note="static self-audit updated after Vault lifecycle patch"/>
  </task_reconciliation>
  <struct_layout>
    <dto name="VisorHudParamsDTO" size="64" fields="TargetCoordinates@0:16,VitalStats@16:16,VisorGlitchParams@32:16,QualityAndTime@48:16" padding="0" alignment="64-byte"/>
    <dto name="VisorArTargetDTO" size="64" fields="ScreenAndFlags@0:16,ColorAndPulse@16:16,LocalMetersAndDistance@32:16,ShapeParams@48:16" padding="0" alignment="64-byte"/>
    <dto name="VisorTelemetryEntry" size="64" fields="FrameIndex@0:4,Flags@4:4,TargetCount@8:4,QualityWeight@12:4,ProjectionUs@16:4,EstimatedGpuUs@20:4,FirstDepth@24:4,StateHash@28:4,O2@32:4,CO2@36:4,Fog@40:4,StencilScale@44:4,LayoutHash@48:4,VaultGeneration@52:4,CameraW@56:4,CameraH@60:4" padding="0" alignment="64-byte"/>
  </struct_layout>
  <scalability curve="continuous">GlobalQualityWeight scales scanline density, chroma offset, curvature, target scale, and fog blend in shader; below 0.3 it collapses toward flat linework/noise-light projection without changing DTO layout, BufferID, save identity, or rollback authority.</scalability>
  <h_phi status="vault-owned">No private persistent NativeArray/List/HashMap allocations in SHINOBU_270 runtime. Vault BufferIDs 73180..73186 are acquired through generation descriptors and now released via IDataVault.ReleaseBuffer on dispose, DataVault hot-swap, and cold rebind.</h_phi>
  <dependency_graph jobs="no retained Burst jobs">Tiny same-frame jobs were rejected for bounded 16-target visual math; therefore NoAlias is not applicable to retained Burst kernels in this domain. RenderGraph consumes cached player context, DataVault descriptors, camera color/depth/stencil, and outputs the AR resolve plus telemetry rows.</dependency_graph>
  <compile_guard status="PENDING_VERIFICATION">No new sibling runtime assembly reference was added. Hecton8.Core.csproj is generated and stale for new visor scripts; Unity project regeneration/import is required before dotnet proof is meaningful.</compile_guard>
  <dear_lie complexity_before="Canvas/TMP rebuild plus transparent UI overdraw" complexity_after="O(pixels_inside_stencil + targets<=16)">Digits, fog, scanlines, and brackets are shader-side optical fakes; no physical breath fog, CPU text layout, particles, or per-target GameObjects.</dear_lie>
</SELF_AUDIT>
