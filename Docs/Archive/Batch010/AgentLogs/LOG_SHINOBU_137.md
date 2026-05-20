# LOG_SHINOBU_137

## 2026-05-19 Terminal Projection Forensics

What was wrong:
- TerminalOS still exposed a Unity-style physical UI boundary: authored terminal screens could be mistaken for World Space Canvas targets, interaction could drift back to raycasters, and the old virtual button DTO path did not prove ARM64 layout or rollback-safe hit state.
- Visual refresh and interaction cadence were coupled too tightly. Thermal pressure should drop texture formatting/update cost, not make gaze clicks sluggish.
- Existing forensic telemetry did not include terminal intersection truth: hovered terminal hash, evaluated count, quality weight, refresh cadence, or intersection timing.
- Human control existed for old layout authoring, but not for the new diegetic projection math, DTO offsets, or runtime quality curve.

What was done:
- Extracted `<AGENT_PROMPT id="SHINOBU_137">` from `Docs/Tasks/CURRENT_BATCH.md` with a raw PowerShell file scan and used only that block for the implementation scope.
- Replaced the owned TerminalOS interaction path with `TerminalPlaneDTO`, `GazeRayDTO`, `TerminalInteractionDTO`, and `ButtonAABBDTO` in `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs`.
- Added deterministic Burst jobs: `MockGazeRayJob`, `CullTerminalsJob`, `TerminalIntersectionJob`, and `EvaluateTerminalButtonsJob`. All use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` and `[NoAlias]` on native buffers.
- Scheduled interaction as a dependency chain: mock gaze -> cull -> ray/plane intersection -> flat button AABB evaluation. The chain writes commands through `SignalBus<TerminalCommandSignal>` and hover UI through `SignalBus<InteractionUiSignal>`.
- Kept persistent data in `GlobalDataVault` buffer IDs 71360-71375. Removed private TerminalOS managed scratch arrays from the owned runtime path: active runtime slots and state buffers are explicit fields, CSV ingest uses stack `Span<byte>`.
- Moved visual complexity into the projected texture path. Added `Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader` for scanline/vignette/glow over the low-res texture array.
- Added `DiegeticUiTunerWindow`, `TerminalOsLayoutValidator`, runtime editor tuning hooks, cold span CSV layout parsing, CharBufferPool token replacement for `^POWER_LEVEL^`, and editor gizmos for terminal planes/buttons/hits.
- Updated `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md` with the TerminalOS projection boundary and legacy owner debt.

Cinematic cheats used:
- "Dear Lie" terminal UI: render the screen once into a low-resolution texture-array slice and sell holographic richness in shader scanlines, emission, vignette, and quality-weighted distortion.
- Mathematical interaction: one ray/plane solve and flat UV AABB checks replace Canvas hierarchy rebuilds, `GraphicRaycaster`, `PhysicsRaycaster`, and `Physics.Raycast`.
- Visual cadence collapse: under thermal pressure, texture formatting/upload cadence falls toward 1 update per 15 frames while interaction math remains frame-responsive.

Exact microseconds saved:
- Static estimate only; Unity profiler run is pending because build was blocked by hardware guard.
- Rejected Canvas rebuild path: expected 50-400 us avoided per dirty terminal update on weak CPU, depending Canvas/TMP hierarchy.
- Rejected raycaster path: expected 10-120 us avoided per gaze evaluation versus UI/physics raycast traversal.
- Flat AABB job: expected 2-8 us for 64 terminals x 2 buttons on desktop-class CPU; Quest 3/MX350 profiler proof pending.

Verification evidence:
- `git diff --check` passed for owned files; only LF->CRLF warnings from Git.
- Static owned TerminalOS scan found no `RenderMode.WorldSpace`, `GraphicRaycaster`, `PhysicsRaycaster`, `Physics.Raycast`, `Canvas.ForceUpdateCanvases`, LINQ, `foreach`, `string.Format`, `TerminalVirtualButtonDTO`, `_virtualButtonsHandle`, or `_lowTier`.
- Static DTO scan found no hot DTO `{ get; set; }` accessors in `TerminalOsTypes.cs`.
- Build verification was not executed. First guard found active `dotnet` processes and CPU 100%; later conditional guard found CPU 82% and exited before invoking `dotnet build`; final recheck still showed CPU 100%.

<SELF_AUDIT agent_id="SHINOBU_137" domain="SUBMARINE_OS_TERMINAL_RENDERER" verification="IMPLEMENTED_PENDING_COMPILE">
  <TWENTY_TASK_RECONCILIATION>
    <TASK id="01" result="PASS_WITH_BOUNDARY">Owned TerminalOS path has no World Space Canvas dependency. External Fabricator/PDA/DiegeticPanel canvas owners remain logged as cross-domain debt, not edited by SHINOBU_137.</TASK>
    <TASK id="02" result="PASS">Owned interaction path uses Burst ray-plane math and flat AABBs. Static scan found no raycasters, `Physics.Raycast`, or `Canvas.ForceUpdateCanvases` in owned TerminalOS files.</TASK>
    <TASK id="03" result="PASS">Hot DTOs expose public fields only. No DTO properties were found in `TerminalOsTypes.cs`.</TASK>
    <TASK id="04" result="PASS">`TerminalInteractionDTO` is explicit 32 bytes with manual pads 20-31. Button, plane, gaze, and telemetry DTOs have fixed explicit sizes.</TASK>
    <TASK id="05" result="PASS">`MockGazeRayJob` writes deterministic fallback gaze data into the vault-backed gaze buffer for CI/editor/no-player cases.</TASK>
    <TASK id="06" result="PASS">`TerminalIntersectionJob` subtracts AUP origin from terminal center, casts local delta to `float3`, guards NaN/division, and writes blittable hit DTOs.</TASK>
    <TASK id="07" result="PASS">`EvaluateTerminalButtonsJob` scans `ButtonAABBDTO` rectangles and emits `TerminalCommandSignal`/`InteractionUiSignal` without GameObject buttons.</TASK>
    <TASK id="08" result="PASS">`Hecton_DiegeticTerminal.shader` sells the screen as holographic projection from the texture-array output instead of CPU Canvas detail.</TASK>
    <TASK id="09" result="PASS">Dirty screen data still uploads through mapped `GraphicsBuffer.LockBufferForWrite` runs; no synchronous Canvas mesh rebuild path was added.</TASK>
    <TASK id="10" result="PASS">`framesBetweenUpdates = round(lerp(1, 15, 1 - GlobalQualityWeight))` gates visual formatting/upload only. Interaction jobs remain frame-responsive.</TASK>
    <TASK id="11" result="PASS">`TerminalPlaneDTO.Flags`, `Power01`, and `Submerged01` support offline/submerged culling. `SetTerminalAvailability` provides the cross-domain route without logistics DTO dependency.</TASK>
    <TASK id="12" result="PASS">`CullTerminalsJob` performs AUP-local distance and view-cone culling before full plane intersection.</TASK>
    <TASK id="13" result="PASS">Interaction DTOs are blittable and Burst jobs use deterministic float mode for rollback-safe state generation.</TASK>
    <TASK id="14" result="PASS">Fully-written working buffers request `NativeArrayOptions.UninitializedMemory`; persistent state/telemetry buffers use clear memory where previous contents matter.</TASK>
    <TASK id="15" result="PASS">Telemetry ring is 300 entries x 64 bytes and dumps to `Docs/AgentLogs/Dump_SHINOBU_137.bin` and `.h8dump` on fault.</TASK>
    <TASK id="16" result="PASS">UI Toolkit `DiegeticUiTunerWindow` exposes runtime distance, view cone, distortion, minimum quality, and live counters.</TASK>
    <TASK id="17" result="PASS">Cold `ReadOnlySpan<byte>` CSV parser mutates text rows and button AABB rows without `string.Split` or LINQ.</TASK>
    <TASK id="18" result="PASS">Editor-only gizmos draw terminal plane, button UV rectangles, and current hover hit from vault buffers.</TASK>
    <TASK id="19" result="PASS">`TryWritePowerLevelTokenLine` scans raw bytes for `^POWER_LEVEL^`, leases `CharBufferPool`, and writes through `Span<char>`/`TryFormat`.</TASK>
    <TASK id="20" result="PASS_PENDING_COMPILE">`TerminalOsSelfAudit` and `TerminalOsLayoutValidator` verify DTO sizes/offsets and canonical ray-plane UV math. Compile verification is pending hardware guard clearance.</TASK>
  </TWENTY_TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION primary_dto="TerminalInteractionDTO" total_bytes="32" alignment="multiple_of_16_and_8">
    <FIELD name="TerminalHash" offset="0" size="4" type="uint" />
    <FIELD name="LocalHitUV" offset="4" size="8" type="float2" />
    <FIELD name="InteractionFlags" offset="12" size="4" type="uint" />
    <FIELD name="Distance" offset="16" size="4" type="float" />
    <FIELD name="_pad0_to__pad11" offset="20" size="12" type="byte_padding" />
    <MATH>4 + 8 + 4 + 4 + 12 = 32 bytes. 32 % 16 = 0. 32 % 8 = 0.</MATH>
    <TELEMETRY_LAYOUT dto="TerminalTelemetryEntry" total_bytes="64" false_sharing="single_cache_line_entry">
      Offsets: Frame 0, TerminalCount 4, DirtyCount 8, DispatchedCount 12, FormatMs 16, UploadUs 20, DispatchUs 24, FaultFlags 28, LayoutHash 32, HoveredTerminalHash 36, LastPower01 40, LastDamage01 44, EvaluatedTerminals 48, FramesBetweenUpdates 52, IntersectionUs 56, GlobalQualityWeight 60.
    </TELEMETRY_LAYOUT>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE_EXPLANATION>
    Below GlobalQualityWeight 0.3, visual work collapses continuously: `_framesBetweenUpdates` approaches 11-15 frames, texture resolution curves toward 256 through `Smooth01`, layout CSV probing backs off through lerped cadence, and mock hologram sway increases via `math.lerp`. The ray-plane/button interaction jobs are deliberately not visually throttled; only batch size trends down through `round(lerp(1, 32, quality))`, preserving deterministic input responsiveness while shedding formatting and upload cost.
  </SCALABILITY_CURVE_EXPLANATION>

  <H_PHI_VAULT_STATUS private_persistent_native_arrays="0" private_native_lists="0" private_native_hashmaps="0">
    <BUFFER id="71360" name="TerminalStates" />
    <BUFFER id="71361" name="ScreenCommands" />
    <BUFFER id="71362" name="GlyphUvs" />
    <BUFFER id="71363" name="TerminalPositions" />
    <BUFFER id="71364" name="TerminalForwards" />
    <BUFFER id="71365" name="DirtyIndices" />
    <BUFFER id="71366" name="TelemetryRing" />
    <BUFFER id="71367" name="MockPower" />
    <BUFFER id="71368" name="MockDamage" />
    <BUFFER id="71369" name="MockPowerStatus" />
    <BUFFER id="71370" name="ButtonAabb" />
    <BUFFER id="71371" name="PanelInstances" />
    <BUFFER id="71372" name="TerminalClickScratch" />
    <BUFFER id="71373" name="TerminalPlanes" />
    <BUFFER id="71374" name="GazeRay" />
    <BUFFER id="71375" name="TerminalInteractions" />
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="MockGazeRayJob" consumes="none" outputs="GazeRays" no_alias="true" />
    <JOB name="CullTerminalsJob" consumes="GazeRays,TerminalPlanes" outputs="TerminalInteractions(candidate/cull)" no_alias="true" dependency="MockGazeRayJob" />
    <JOB name="TerminalIntersectionJob" consumes="GazeRays,TerminalPlanes,TerminalInteractions(candidate)" outputs="TerminalInteractions(hit)" no_alias="true" dependency="CullTerminalsJob" />
    <JOB name="EvaluateTerminalButtonsJob" consumes="TerminalInteractions,TerminalPlanes,ButtonAabb" outputs="SignalBus TerminalCommandSignal and InteractionUiSignal" no_alias="true" dependency="TerminalIntersectionJob" />
    <CHAIN output_handle="_terminalInteractionHandle">No arbitrary main-thread `Complete()` while the chain is running; runtime only finalizes after `IsCompleted`, teardown force-completes only during shutdown.</CHAIN>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    Domain code did not add direct references to sibling runtime assemblies. Routing is through `GlobalRegistry`, `GlobalDataVault`, `SignalBus`, and shared `AbsoluteUniversePosition` contracts. Compile verification is pending because project rules blocked build execution at CPU 100%/82% and active `dotnet` state on the first pass.
  </COMPILE_GUARD>

  <THE_DEAR_LIE_CONFIRMATION>
    The fake is a projected texture-array terminal: CPU formats low-resolution slices and the shader fabricates scanlines, glow, vignette, and projection richness. Before: Canvas/TMP hierarchy rebuild + UI/physics raycast traversal, effectively O(canvas hierarchy + raycaster hits + dirty geometry). After: Burst O(T + B) plane/AABB math plus O(dirty runs) GPU buffer uploads; no per-button GameObjects and no World Space Canvas in the owned TerminalOS path.
  </THE_DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Post-Audit Hardening Pass

What was wrong:
- `UploadStateRun` could leave a mapped `GraphicsBuffer` locked if `ResolveTerminalStatePointer()` returned null after `LockBufferForWrite`. That is a concrete VISUAL_SYNC failure mode, not theoretical polish.
- Current generated `Hecton8.Core.csproj` does not yet include the newly added editor tuner/validator files; a narrow build against that csproj cannot prove those files until Unity regenerates project files.

What was done:
- Wrapped `UploadStateRun` copy logic in `try/finally` so `UnlockBufferAfterWrite<TerminalStateDTO>(count)` always runs.
- Rechecked `LockBufferForWrite` call sites in TerminalOS; no remaining owned call site has an early `return` after lock.
- Rechecked generated csproj membership and recorded the Unity-import proof gap instead of faking compile coverage.

Cinematic Cheats used:
- No new visual fake added in this pass. Existing Dear Lie path remains: low-res texture-array terminal plus shader scanline/glow/vignette projection.

Exact Microseconds saved:
- No steady-state saving claimed. The patch removes a catastrophic GPU upload lock failure path. Static performance estimate remains unchanged: Canvas rebuild avoidance 50-400 us per dirty terminal update, raycaster avoidance 10-120 us per gaze event, pending profiler evidence.

REGRESSION MODEL:
- CPU: no additional loop, branch only on existing upload path; expected no measurable steady-state change.
- GC: no managed allocation added.
- Memory: no new persistent memory.
- Cadence: no refresh-cadence change.
- Correctness: improves mapped-buffer lifetime correctness under Vault fault/editor reload.

HOT PATH IMPACT:
- VISUAL_SYNC state upload now pays `try/finally` control structure around an existing copy. No new allocations, no new Unity object calls, no new cross-domain dependency.

FAILURE MODES:
- Build proof remains blocked by CPU guard and Unity project-file regeneration state.
- Latest guarded build attempt after a 20-second wait reported `BUILD_GUARD_BLOCKED cpu=94 dotnet_or_csc=False`; `dotnet build` did not start.
- Runtime GC/profiler proof remains absent until Unity Editor import/Play Mode/GCMonitor are available.

WHY KEPT/REJECTED:
- Kept the `try/finally` guard because mapped GPU buffers require deterministic unlock.
- Rejected hand-editing Unity generated csproj files because it would not prove actual Unity assembly import state and would add compile-wall churn.

## 2026-05-19 Post-Audit Editor Ref Hardening Pass

What was wrong:
- `TerminalOsDesignerWindow` used `GetTerminalStateRef(index)` for mock edits.
- `GetTerminalStateRef` threw for invalid Vault/index state. That creates a managed exception path in the human tuning facade and can break during editor reload/Vault bootstrap gaps.

What was done:
- Added `TerminalOsRuntime.TrySetTerminalMockState(index, value1, value2)` as the owner-local editor mutation route.
- Switched `TerminalOsDesignerWindow` to the bool-returning setter.
- Kept the legacy ref accessor for unknown callers, but changed the invalid path to set a fault flag and return a static inert ref instead of throwing.
- Static scan of owned TerminalOS runtime/designer files found no `throw new` or `ArgumentOutOfRangeException` after this pass.

Cinematic Cheats used:
- No new fake added in this pass. The existing fake remains the low-resolution terminal texture array plus shader glow/scanline/vignette projection.

Exact Microseconds saved:
- No player-frame saving claimed. This removes editor exception churn and protects dirty-state routing. Runtime hot path remains unchanged.

REGRESSION MODEL:
- CPU: editor-only mutation path now takes a bool branch and a single DTO copy/writeback.
- GC: no allocation added.
- Memory: one static fallback DTO field; no NativeArray/List/HashMap allocation.
- Cadence: no `framesBetweenUpdates` change.
- Compile wall: no new sibling assembly reference.

WHY KEPT/REJECTED:
- Kept a compatibility ref API because concurrent agents may already reference it.
- Rejected direct editor buffer writes because TerminalOS owns dirty routing and Vault access.
- Rejected throw-on-invalid because editor tuning must fail closed, not allocate exception objects.

## 2026-05-19 Verification Guard Pass

What was checked:
- `git diff --check` on owned runtime/editor/shader/docs paths: passed, only line-ending warnings from Git.
- Forbidden owned-path scan for World Space Canvas, raycasters, physics raycast, Canvas force update, legacy virtual button DTO, binary low-tier branch, LINQ, `foreach`, and `string.Format`: no matches.
- DTO/hot-path scan for properties, `Pack=1`, `UnityEngine.Random`, `Time.deltaTime`, and private Native container allocation patterns: no matches.
- Safe-ref scan: only `TrySetTerminalMockState`, `GetTerminalStateRef` declaration, and designer setter call remain; no `throw new`/`ArgumentOutOfRangeException`.

Build guard:
- Latest guarded command returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`.
- `dotnet build` did not start. This is intentional compliance with the hardware-protection rule, not a compile pass.

Remaining proof gap:
- Unity import/project regeneration is still required before the new editor tuner/validator files are covered by generated csproj compilation.
- Runtime profiler/GC proof is still pending because no Editor/Play Mode execution occurred in this session.

## 2026-05-19 Compile Wall AUP Recheck

What was checked:
- `AbsoluteUniversePosition` declaration is in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`.
- That file is covered by root `Assets/_Project/Scripts/Hecton8.Core.asmdef`, not a newly referenced sibling runtime assembly.
- `Assets/_Project/Scripts/Hecton8.Core.asmdef` already contains its existing references; this pass did not edit asmdef references.

Decision:
- Kept `using Hecton8.World` in TerminalOS because it is namespace access to the existing AUP type required for local AUP subtraction.
- No direct Logistics/Physics/UI sibling DTO dependency was added for TerminalOS interaction. Power/submerged state remains injected through `SetTerminalAvailability`; clicks route through `SignalBus`.

Exact Microseconds saved:
- No runtime saving claimed. This is compile-wall evidence and prevents a bad churn patch based on namespace misclassification.

## 2026-05-19 Texture Projection Coherency Pass

What was wrong:
- Panel instance `SliceFlags` carried quality/glitch data, but `UpdatePanelInstancesIfNeeded` skipped unchanged transforms. Stationary terminals could keep stale shader fake intensity after `GlobalQualityWeight` or diegetic glitch changes.
- Only state upload had guaranteed mapped-buffer unlock; dirty index, screen command, glyph UV, and panel instance uploads still used straight-line unlocks.

What was done:
- Added last-uploaded panel scalar sentinels for quality and glitch intensity.
- Forced panel instance rewrite/upload when those scalars change, without adding per-frame work while scalars are stable.
- Dirtied material bindings on quality/glitch scalar changes so `_HectonDiegeticGlitchQualityWeight` updates coherently.
- Wrapped every owned `LockBufferForWrite` upload lane in `try/finally`.

Cinematic Cheats used:
- Preserved the existing Dear Lie: low-resolution texture-array terminal plus shader-side scanline/glow/vignette. This pass makes the fake respond to continuous homeostasis quality instead of freezing at the old scalar.

Exact Microseconds saved:
- No savings claimed. Cost added only on scalar-change frames: O(T) DTO rewrite for bound terminal panels. Stable frames keep the previous cost model. Failure-mode removal is mapped GPU buffer lifetime safety.

REGRESSION MODEL:
- CPU: scalar-change frames rewrite up to `TerminalCapacity` panel DTOs; stable frames unchanged.
- GC: no allocation added.
- Memory: two `float` sentinel fields; no native/managed collection allocation.
- Render: material binding dirtied only when quality/glitch changes or existing resource binding changes.
- Compile wall: no asmdef/reference change.

Verification:
- `rg` scan shows every owned `LockBufferForWrite` call has paired `finally` and `UnlockBufferAfterWrite`.
- Forbidden hot-path scan returned no matches for raycasters, World Space Canvas, LINQ, `foreach`, `string.Format`, `Pack=1`, `Time.deltaTime`, `UnityEngine.Random`, or private Native container allocation patterns in owned TerminalOS runtime/types.
- `git diff --check` passed with only Git line-ending warnings.
- Latest guarded build command after this pass returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

## 2026-05-19 Instanced Binding Guard Pass

What was wrong:
- `BindTerminalRenderers` enabled `HECTON_TERMINAL_INSTANCED` but did not explicitly disable it for non-instanced fallback or missing panel buffer.

What was done:
- Material binding now enables the instanced keyword only when `drawPanelsInstanced` is true and `_panelInstanceBuffer` is valid.
- Otherwise the keyword is disabled, keeping fallback `_TerminalSlice` rendering coherent.
- Latest guarded build command after this pass returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

Exact Microseconds saved:
- None claimed. This is render-state correctness, not speed.

## 2026-05-19 Per-Frame Quality Scalar Refresh Pass

What was wrong:
- `RefreshScalabilityPolicy` returned early behind `_nextTierRefreshFrame` before reading `HomeostasisBrain.GlobalQualityWeight`.
- That meant visual cadence and shader scalar quality could stay stale for 30-120 frames during thermal pressure.

What was done:
- Moved cheap scalar/cadence work before the heavy texture-refresh gate.
- Kept texture resolution/resource churn behind `_nextTierRefreshFrame`.
- Quality changes still dirty panel instance upload and material bindings through the existing scalar threshold.

Cinematic Cheats used:
- No new fake added. This makes the existing terminal shader fake react immediately to continuous quality changes.

Exact Microseconds saved:
- No direct saving claimed. Added cost is a few scalar operations per frame; expected value is faster thermal shedding of visual update cadence without RT churn.

## 2026-05-19 Attention Cull Dirty Preservation Pass

What was wrong:
- `BuildDirtyList` cleared `IsDirty` when a terminal failed attention culling.
- A terminal that changed while offscreen could therefore lose its pending texture upload and remain stale when visible again.

What was done:
- Attention cull now skips upload without clearing the dirty flag.
- Non-finite data still clears dirty and raises fault state; valid offscreen data is deferred.

Cinematic Cheats used:
- Same Dear Lie projection. The patch preserves deferred visual truth without paying offscreen upload cost.

Exact Microseconds saved:
- No savings claimed. The bounded cost is keeping up to 64 dirty rows eligible for future scans; this prevents stale terminal textures.

## 2026-05-19 Verification Guard After Quality/Dirty Pass

What was checked:
- `git diff --check` on owned TerminalOS runtime/types/editor/shader/docs paths: passed, only Git line-ending warnings.
- Forbidden pattern scan on owned runtime/types/editor paths: no World Space Canvas, raycaster, `Physics.Raycast`, `Canvas.ForceUpdateCanvases`, LINQ, `foreach`, `string.Format`, `Pack=1`, `UnityEngine.Random`, `Time.deltaTime`, or private Native container allocation matches.
- Source check confirms attention cull no longer clears `IsDirty`; non-finite data and post-upload clear paths remain.
- `ZERO_GC_UI_PIPELINE.md` now states per-frame quality scalar/cadence reads and deferred dirty upload under attention culling.

Build guard:
- Latest guarded command returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`.
- `dotnet build` did not start.

## 2026-05-19 Button Span Evaluation Tightening Pass

What was wrong:
- `EvaluateTerminalButtonsJob` had `TerminalPlaneDTO.LayoutFirstButton/LayoutButtonCount`, but still scanned the full button buffer for every hovered terminal.

What was done:
- The job now clamps and scans only the terminal-owned button span.
- `TerminalHash` validation remains as a corrupted-layout guard before signal emission.

Cinematic Cheats used:
- No new render fake. This is CPU hot-path tightening for the mathematical interaction layer.

Exact Microseconds saved:
- No measured value claimed. Default emergency layout drops per-hover button checks from up to `ButtonAabbCapacity` (`128`) to `2`.

## 2026-05-19 Interaction Unblocked By Visual Format Pass

What was wrong:
- `LateFrameTick` returned early when `UpdateTerminalTextJob` had not finished.
- That avoided state-buffer races but also skipped gaze intersection and click handling.

What was done:
- Added a `visualPipelineBlocked` path.
- While visual formatting is still running, TerminalOS skips only visual state reads/uploads and state-derived plane availability refresh.
- Gaze intersection, click resolution, and instanced panel drawing continue against the last known terminal plane state.

Cinematic Cheats used:
- No new fake. This preserves the existing Dear Lie visual cadence while keeping mathematical input responsive.

Exact Microseconds saved:
- None claimed. This removes an input latency failure mode without forcing `JobHandle.Complete()` on the main thread.

## 2026-05-19 Editor Text Scan Boundary Pass

What was wrong:
- A broad static scan across runtime and editor files flagged `DiegeticUiTunerWindow` `Label.text` assignments. The previous verification wording was too broad because it implied no `.text` setter existed anywhere in owned files.

What was done:
- Corrected `Docs/Tasks/Status_SHINOBU_137.md` to scope the `.text`/formatted-string ban to owned runtime/types hot paths.
- Added rationale that the UI Toolkit tuner is editor-only and cold, while player runtime terminal formatting remains DTO/span/char-buffer based.

Cinematic Cheats used:
- None. This is verification hygiene, not a rendering change.

Exact Microseconds saved:
- None claimed. The value is report accuracy: editor-only UI Toolkit allocations are not player-frame GC and must not be mixed with hot-path evidence.

Verification:
- `git diff --check` on owned TerminalOS runtime/types/editor/shader/docs paths passed with only Git line-ending warnings.
- Runtime-only forbidden scan returned `RUNTIME_FORBIDDEN_SCAN_CLEAR` for World Space Canvas, raycasters, physics raycast, LINQ, `foreach`, `string.Format`, `.ToString()`, `.text =`, DTO accessors, `Pack=1`, `UnityEngine.Random`, `Time.deltaTime`, private Native container allocations, and throw patterns.
- Editor boundary scan intentionally found `DiegeticUiTunerWindow` cold `Label.text` and `.ToString()` status formatting under `#if UNITY_EDITOR`.
- Guarded build check returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

## 2026-05-19 Agent-Owned Black Box Dump Path Pass

What was wrong:
- TerminalOS black-box dump constants still wrote `Dump_TERMINAL_SURGEON.bin` and `.h8dump`, which is a stale owner route for SHINOBU_137.

What was done:
- Runtime dump constants now target `Docs/AgentLogs/Dump_SHINOBU_137.bin` and `Docs/AgentLogs/Dump_SHINOBU_137.h8dump`.
- Rationale/status now record the forensic ownership correction.

Cinematic Cheats used:
- None. This is black-box route ownership.

Exact Microseconds saved:
- None claimed. The value is crash-forensic correctness and avoiding cross-agent dump ambiguity.

## 2026-05-19 GPU Dirty Clear Confirmation Pass

What was wrong:
- `LateFrameTick` cleared dirty terminal flags after upload/dispatch calls even if the graphics resource, mapped upload, state buffer, or compute dispatch path failed.
- `UploadStateRun` assumed a non-null state buffer after `ResolveStateBuffer`.

What was done:
- Dirty-index/state upload methods now return success booleans.
- `UploadStateRun` fails closed on null buffers and rejected memory copies.
- `IsDirty` is cleared only when upload succeeds and compute dispatch reports the full dirty count.

Cinematic Cheats used:
- Preserves the Dear Lie texture projection while preventing skipped GPU work from erasing the pending visual truth.

Exact Microseconds saved:
- None claimed. Adds dirty-frame bool branches; prevents stale terminal textures after resource or upload faults.

Verification:
- `git diff --check` on the patched runtime/status/rationale/log paths passed with only Git line-ending warnings.
- Runtime-only forbidden scan returned `RUNTIME_FORBIDDEN_SCAN_CLEAR`.
- Guarded build check after this patch returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

## 2026-05-19 Editor Asmdef Unsafe Reference Pass

What was wrong:
- `TerminalOsLayoutValidator` depends on `UnsafeUtility`, but `Hecton8.UI.Editor.asmdef` did not explicitly reference `Unity.Collections`.

What was done:
- Added `Unity.Collections` to `Hecton8.UI.Editor.asmdef`.

Cinematic Cheats used:
- None. This preserves editor-side DTO layout proof.

Exact Microseconds saved:
- None claimed. Editor-only compile/reference correction; no runtime frame impact.

Verification:
- `Hecton8.UI.Editor.asmdef` parsed as valid JSON with references `Hecton8.Core,Unity.Collections,Unity.Mathematics`.
- `git diff --check` on patched asmdef/runtime/docs paths passed with only Git line-ending warnings.
- Runtime-only forbidden scan returned `RUNTIME_FORBIDDEN_SCAN_CLEAR`.
- Guarded build check after this patch returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

## 2026-05-19 Packed Dirty Byte Preservation Pass

What was wrong:
- `TerminalStateDTO.IsDirty` is packed at byte offset 7, the high byte of `BackgroundColor`.
- `UpdateTerminalTextJob` rewrote `BackgroundColor` and could clear a pre-existing dirty flag when the terminal's formatted values were unchanged.

What was done:
- The Burst job now preserves the pending dirty byte around `BackgroundColor` writes before applying new semantic dirty decisions.
- DTO stride and HLSL state ABI remain unchanged at 48 bytes.

Cinematic Cheats used:
- None. This protects the compact texture-projection upload truth.

Exact Microseconds saved:
- Avoids a 48->64 byte state expansion. Added cost is one byte load/store per formatted terminal.

Verification:
- Runtime-only forbidden scan returned `RUNTIME_FORBIDDEN_SCAN_CLEAR`.
- `git diff --check` on patched TerminalOS types/status/rationale/log paths passed with only Git line-ending warnings.
- Guarded build check after this patch returned `BUILD_GUARD_BLOCKED cpu=69 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.

## 2026-05-19 Guarded Build Attempt: External Deleted Source

What was wrong:
- The hardware guard eventually cleared at `cpu=21` with no `dotnet/csc` processes, so `dotnet build .\Hecton8.Core.csproj --no-restore` was allowed.
- Build failed before reaching SHINOBU_137 code: `CS2001` missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
- `git status` reports that file as deleted outside the TerminalOS domain.

What was done:
- Recorded the compile wall as external deleted-source blockage.
- Did not edit generated csproj files.
- Did not restore or recreate the Construction-domain file.

Cinematic Cheats used:
- None. This is compile-wall reporting.

Exact Microseconds saved:
- None. Avoided repeated doomed builds after a deterministic missing-source failure.

## 2026-05-19 Static Upload Dirty Flag Confirmation Pass

What was wrong:
- Layout, glyph UV, and panel instance upload paths cleared dirty flags after mapped copy attempts even if `UnsafeMemoryCopyGuard` rejected the copy.
- Panel scalar sentinels could advance after a failed panel instance upload, making later quality/glitch changes look already uploaded.

What was done:
- `UploadScreenCommands`, `UploadGlyphUvs`, and `UploadPanelInstances` now return copy success.
- Static dirty flags clear only after copied data is confirmed.
- Panel quality/glitch sentinels commit only after panel instance upload succeeds.

Cinematic Cheats used:
- Preserves the shader-side Dear Lie by making the projected panel scalar upload truthful under fault/reload cases.

Exact Microseconds saved:
- None claimed. The change adds one bool branch on dirty upload paths and prevents stale static buffer state.

Verification:
- `git diff --check` on the patched runtime/status/rationale/log paths passed with only Git line-ending warnings.
- Runtime-only forbidden scan returned `RUNTIME_FORBIDDEN_SCAN_CLEAR`.
- Guarded build check after this patch returned `BUILD_GUARD_BLOCKED cpu=100 dotnet_or_csc=False process_count=0`; `dotnet build` did not start.
