# Rationale_SHINOBU_270

Status: RENDERGRAPH FAIL-OPEN WATCHDOG PATCHED / STATIC API RECHECK CLEAN / GENERATED CSPROJ STALE / COMPILE BLOCKED BY ACTIVE COMPILER AND CPU GATE
Domain: ECHELON 8 Presentation & UX / Visor AR (HUD)

## Mandate Selection

Problem: Canvas HUD and AR projection work can easily create managed rebuild spikes, hidden global shader string calls, or duplicate presentation paths.
Solution: Use the visor stencil, UI zero-GC, RenderGraph, zero-GC, ARM64 layout, AUP precision, execution phase, and GlobalRegistry DI mandates as the hard boundary.
Rejected Alternatives: A second Canvas overlay or new service-global lookup path would duplicate presentation ownership and violate hot-path registry/Canvas rules.
Scalability potential: Low uses stencil-gated flat procedural lines; Middle adds scanlines and mild distortion; High adds richer refraction; Ultra spends saved fill-rate on visual-overkill curvature and noise.
Hardware Impact: Expected low-end i3/MX350 gain comes from eliminating Canvas rebuild and transparent overdraw; exact microseconds remain PENDING VERIFICATION until profiler/Frame Debugger proof.

## Decisions

### D001 Prompt Boundary

Problem: CURRENT_BATCH.md contains neighboring agent tasks with overlapping rendering language.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_270">` via PowerShell regex over the full raw file.
Rejected Alternatives: Reading surrounding prompts or using truncated output would contaminate task scope.
Scalability potential: Scope remains Presentation & UX visor rendering only.
Hardware Impact: No runtime impact; prevents architectural drift.

### D002 Existing HUD Ownership

Problem: SuitHUDPresentationController created and maintained `Suit_HUD_ProjectionSource`, a WorldSpace Canvas used as the projected HUD source.
Solution: Added `StencilRenderGraph` presentation mode as the default and suppresses both overlay and projection-source Canvas paths; ARWaypointOverlay remains the waypoint service/data owner but not the renderer.
Rejected Alternatives: Keeping a hidden Canvas as a RenderTexture source still pays layout/rebuild and transparent overdraw debt.
Scalability potential: Low draws flat stencil-gated digits and brackets; Middle adds scanlines/fog; High adds curvature/chroma; Ultra keeps the same authority route and spends more shader ALU.
Hardware Impact: i3/MX350 expected gain 250-750 us CPU on HUD value churn plus lower transparent overdraw; exact proof pending compile/profiler.

### D003 Data Layout

Problem: Shader parameters need ARM64-safe constant-buffer alignment and must not trigger CS1612 defensive copies through C# properties.
Solution: Added explicit 64-byte DTOs with raw public fields: `VisorHudParamsDTO`, `VisorArTargetDTO`, `VisorHudDigitParamsDTO`, and `VisorTelemetryEntry`; layout validator uses `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`.
Rejected Alternatives: Sequential structs, managed view-models, Vector property wrappers, or string-named Shader.SetGlobalVector calls.
Scalability potential: Same DTO feeds low through ultra; quality only changes shader math, not layout or authority.
Hardware Impact: 2-8 us sync overhead avoided and prevents CBuffer misread on ARM64/tiled GPUs.

### D004 AUP Target Projection

Problem: AR brackets will drift at world edges if absolute target/world floats are projected directly.
Solution: `ProjectArTargets` stores waypoint AUP sources, subtracts camera `double3` AUP before casting the local delta to `float3`, and compacts active visible rows before upload.
Rejected Alternatives: `Camera.WorldToViewportPoint`, Transform polling inside RenderGraph, registry lookups from the render pass, or same-frame tiny job dispatch.
Scalability potential: Low can cap targets to 16 flat brackets; high/ultra can enrich bracket animation from the same projected DTOs.
Hardware Impact: 5-35 us CPU projection for 16 targets; main value is precision correctness over 100km scale.

### D005 Dear Lie Text

Problem: Runtime oxygen/depth/pressure text through TMP or Canvas labels mutates managed UI state and can rebuild meshes.
Solution: `BuildDigitParams` packs digit indices into a 64-byte CBuffer and `Hecton_VisorAR.shader` draws procedural seven-segment glyphs with no runtime atlas binding.
Rejected Alternatives: TMP SetText/SetCharArray during gameplay, per-label meshes, per-number textures, unused atlas/noise bindings, or same-frame tiny job dispatch.
Scalability potential: Low uses crisp seven-segment boxes; Middle/High/Ultra increase scanline, curvature, chroma, fog, and bracket richness without changing CPU format.
Hardware Impact: 20-150 us CPU saved during frequent value changes; no hot string allocation.

### D006 Black Box and Tooling

Problem: A renderer that can project NaN coordinates needs forensic proof, not chat claims.
Solution: Added a 300-entry vault telemetry ring and dump path `Docs/AgentLogs/Dump_SHINOBU_270.bin`; added editor tuner, gizmo, and HUD Canvas Inquisition report writer.
Rejected Alternatives: Debug.Log-only diagnostics and manual prefab inspection.
Scalability potential: Low devices use telemetry to prove budget; high-tier devices use tuner/profile data to increase visual overkill without changing gameplay truth.
Hardware Impact: Telemetry write is one 64-byte store/frame; dump only happens on non-finite/crash-class projection fault. Over-budget frames stay in telemetry to avoid render-side disk I/O.

### D007 Canvas Rebuild Watchdog

Problem: Static scan found a remaining runtime `Canvas.ForceUpdateCanvases()` in `RuntimeWatchdog.TriggerHudCanvasBuildBatch`, reachable when HUD canvas heartbeat stalls.
Solution: Retired the forced Canvas rebuild call; the watchdog still publishes the stale-HUD warning but no longer mutates Canvas geometry.
Rejected Alternatives: Keeping ForceUpdate as a "rare recovery" path still violates the no-Canvas-rebuild mandate and can spike CPU when the exact failure occurs.
Scalability potential: Low through Ultra use the same no-rebuild rule; visual resilience comes from the stencil pass, not Canvas recovery.
Hardware Impact: Removes a worst-case full Canvas rebuild spike; microseconds depend on hierarchy size, expected 300-900 us avoided on i3/MX350-class CPU during stale heartbeat.

### D008 Compile Gate

Problem: Verification is required, but batch rules forbid launching dotnet when CPU is under work over 50%.
Solution: Checked for dotnet/csc processes and sampled CPU; no dotnet/csc were running, but CPU LoadPercentage and Get-Counter both reported 100%, so build was blocked.
Rejected Alternatives: Running dotnet anyway or claiming compile success without evidence.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; protects other 20+ agents from build contention.

### D009 RenderGraph Resolve Correctness

Problem: A stencil-gated overlay writing into a fresh destination texture left pixels outside the visor mask undefined because the shader never copied the source color there.
Solution: Use one RenderGraph resolve pass with two fullscreen subdraws: pass 0 copies camera color unconditionally, pass 1 applies the AR overlay only through stencil equality.
Rejected Alternatives: Clearing destination to black, rendering only stencil pixels into a fresh texture, or returning to a Canvas/RawImage composite.
Scalability potential: Low through Ultra keep the same route; quality changes only shader ALU for curvature, scanlines, chroma, and fog.
Hardware Impact: Correctness fix; cost is one fullscreen copy subdraw already required to preserve the camera color, replacing far more expensive Canvas overdraw.

### D010 BufferID Collision Repair

Problem: The initial SHINOBU_270 local Vault IDs `70680..70686` collided with `H8Memory.ShinobuExosuit*` lanes.
Solution: Moved visor presentation buffers to owner-local `73180..73186` and documented the collision repair in the binary payload ledger.
Rejected Alternatives: Extending the global enum during a multi-agent batch or sharing exosuit-owned IDs.
Scalability potential: Same range serves Low, Middle, High, and Ultra because quality never changes DTO identity.
Hardware Impact: Prevents cross-domain Vault corruption; runtime microsecond impact 0.

### D011 Phase Isolation and Snapshot Consumption

Problem: Render prep called `ARWaypointOverlay.CopyStencilTargetSources`, and that method previously resolved owners and recollected scene/runtime waypoint state.
Solution: `CopyStencilTargetSources` now copies only the latest owner-phase `_runtimeWaypoints` snapshot; collection remains in `Tick` and `SlowTick`.
Rejected Alternatives: GlobalRegistry/bootstrap lookups or transform collection during RenderGraph setup.
Scalability potential: Low devices avoid hidden render-side scene work; high-tier devices can increase AR visual richness without changing the owner route.
Hardware Impact: Removes render-side lookup/mutation risk; expected 5-30 us CPU variance reduction on i3/MX350 when waypoint ownership is active.

### D012 Tiny Job Rejection

Problem: The prior copy, digit, mock, and 16-target projection code used synchronous `.Run()` job wrappers, which creates same-frame dispatch overhead without enough batch size to amortize it.
Solution: Replaced these with direct local math and `UnsafeUtility.MemCpy` into double-buffered `GraphicsBuffer.LockBufferForWrite` mappings.
Rejected Alternatives: Keeping tiny Burst jobs to satisfy aesthetics, or adding hidden `.Complete()` windows.
Scalability potential: Low saves dispatch overhead; Middle/High/Ultra spend the saved CPU on shader-side overkill, not job plumbing.
Hardware Impact: Expected 2-10 us CPU overhead avoided per visor frame; exact profiler proof pending.

### D013 Fault Dump Discipline

Problem: Dumping the 300-frame black box on ordinary projection budget breaches would perform managed path creation and synchronous disk I/O from the render-side fault path.
Solution: Non-finite projection still triggers `Dump_SHINOBU_270.bin`; over-budget frames remain telemetry flags in the fixed ring for later inspection.
Rejected Alternatives: File I/O on every over-budget render frame or Debug.Log diagnostics.
Scalability potential: Low-tier devices can exceed the 0.1ms suspicion threshold without incurring disk stalls; high-tier devices still get fault forensics on NaN/crash-class states.
Hardware Impact: Avoids worst-case render hitch from disk I/O; dump cost only occurs on non-finite fault.

### D014 Import and Editor Memory Boundary

Problem: New SHINOBU_270 assets lacked `.meta` files, and the editor gizmo retained a private persistent `NativeArray`.
Solution: Added fixed Unity meta GUIDs and converted the gizmo to an `#if UNITY_EDITOR` Temp allocator scope for Scene View diagnostics only.
Rejected Alternatives: Letting Unity generate unstable GUIDs or retaining runtime persistent scratch outside the Vault.
Scalability potential: Runtime path has zero gizmo memory; editor proof remains available without player memory ownership.
Hardware Impact: Runtime impact 0; prevents player builds from carrying the editor preview buffer.

### D015 Compile Gate Recheck

Problem: A compile is now useful after structural fixes, but CPU gate still forbids dotnet when load exceeds 50%.
Solution: Rechecked processes and CPU. No dotnet/csc process was reported, but CPU was 88% by CIM and 83.7-100% by the latest performance counter samples, so compile remains blocked.
Rejected Alternatives: Launching dotnet under load or reporting compile success without evidence.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; protects local hardware and parallel agents.

### D016 Legacy Bridge Purge

Problem: The visor renderer still had two hot visual fallbacks touching legacy global signal routes: stress read through `GlobalSignals.TryGetLatestPlayerStressSignal` and camera AUP fallback through `AbsoluteUniversePosition.FromRuntimePosition`.
Solution: Stress is now derived only from owner-published UI scalar snapshots. If the cached player pose AUP is unavailable, the renderer clears visual AR targets, records `TelemetryFlagNoPlayerAup`, and still renders HUD vitals without projecting false world markers.
Rejected Alternatives: Reading `GlobalSignals` during RenderGraph prep, fabricating camera AUP from runtime transform/origin bridge, or silently projecting against default world origin.
Scalability potential: Low devices avoid hidden bridge variance; Middle/High/Ultra keep the same visual path and spend quality only in shader ALU, not authority lookups.
Hardware Impact: Expected 1-5 us CPU variance reduction on i3/MX350-class hardware; larger value is authority hygiene and removing a render-prep dependency on legacy bridge state.

### D017 Compile Gate Third Check

Problem: After bridge purge, build verification is useful, but command discipline forbids dotnet under active CPU load.
Solution: Rechecked process and CPU gates. No dotnet/csc process was reported. The latest gate showed CIM 73% and processor counter samples 65.2%, 100%, 85.8%, 72.3%, 72.5%, so the compile gate remains closed.
Rejected Alternatives: Running dotnet build on a transient low sample or reporting compile success without evidence.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; prevents contention with the 20+ parallel-agent workload.

### D018 RenderGraph Depth Attachment Discipline

Problem: The AR resolve pass declared active depth as both a sampled texture and a depth/stencil attachment, even though the shader does not sample depth.
Solution: Removed the redundant `_CameraDepthTexture` binding and texture read declaration; depth remains attached only for stencil equality.
Rejected Alternatives: Keeping read/write ambiguity or adding fake depth sampling to justify the declaration.
Scalability potential: Low through Ultra keep the same stencil route; quality still scales through shader ALU only.
Hardware Impact: Prevents RenderGraph validation/resource hazard. Runtime savings are small, but it removes driver ambiguity and a needless resource declaration.

### D019 RenderGraph Buffer Handle ABI Alignment

Problem: The AR resolve pass imported GPU buffers and declared `UseBuffer`, but `PassData` still stored raw `GraphicsBuffer` references for binding.
Solution: `PassData` now stores the imported `BufferHandle`s only and converts them to `GraphicsBuffer` inside the render function, matching existing Hecton Noir/Ocean RenderGraph patterns.
Rejected Alternatives: Duplicating raw buffer references in pass data or relying on external object lifetime while RenderGraph tracks handles separately.
Scalability potential: Quality scaling remains shader-only; the binding ABI is identical for Low through Ultra.
Hardware Impact: Runtime microsecond impact is effectively 0; this removes lifetime ambiguity and reduces RenderGraph validation risk.

### D020 Build Gate Blocked by Existing Compiler

Problem: Compile verification is still pending, but the build gate must not start a competing compiler when another agent is already compiling.
Solution: Rechecked the gate after a brief low CPU window. A `dotnet` process PID 33144 and `csc` process PID 31492 were active, and processor counter samples were 98.5%, 66.5%, 54.5%, so SHINOBU_270 did not launch build. A later process query returned no compiler rows, but CPU counter remained above threshold at 55.2%, 63.1%, 57.3%, 66.9%, 82.1%. A final gate watch in this pass found a new `dotnet` PID 12412 and `csc` PID 32352 with CPU samples 75.1%, 71.4%, 84.6%, 91.8%, 83.9%. The latest follow-up process query returned no compiler rows, but CPU samples were still 97.7%, 100%, 79.3%.
Rejected Alternatives: Killing another agent's compiler, launching a second dotnet build, or claiming compile proof from static scans.
Scalability potential: No runtime impact.
Hardware Impact: Prevents shared workstation IO/CPU contention and avoids build-state interference.

### D021 Subagent RenderGraph Audit Closure

Problem: The renderer could leave Canvas fallback suppressed when the feature was disabled or when pass prerequisites failed, and stencil read/write masks were not guaranteed to match when another stencil lane shared bits.
Solution: `OnDisable`/`Dispose` now clear stencil suppression flags, and `AddRenderPasses` enables suppression only after material, Vault handle, mask mesh, frame upload, and pass setup prerequisites succeed. `Hecton_VisorAR.shader` now exposes `_StencilReadMask`, and the feature writes it from the configured stencil write mask.
Rejected Alternatives: Enabling suppression in `OnEnable`, hard-coding `ReadMask 255`, or silently hiding legacy fallback when no AR pass can enqueue.
Scalability potential: Low through Ultra keep one stencil route; quality still changes shader ALU only, not presentation ownership.
Hardware Impact: Correctness fix; expected runtime microsecond impact 0, prevents black/no-HUD failover states.

### D022 RenderGraph Resource and Fallback Mask Discipline

Problem: The stencil pass declared a color attachment despite `ColorMask 0`, the AR resolve declared depth/stencil as writeable while only stencil-testing, and the generated fallback visor mesh could be rejected by `Cull Front`.
Solution: Stencil pass now binds only depth/stencil, AR resolve binds depth/stencil read-only, and `Hecton_VisorStencilMask` uses `Cull Off` for the cheap ColorMask 0 pass.
Rejected Alternatives: Overstating RenderGraph writes, relying on fallback winding, or keeping authored-shell culling assumptions for generated flat geometry.
Scalability potential: On weak devices this avoids extra resource hazards; on high-tier devices the same mask supports richer shader overkill without changing routing.
Hardware Impact: Removes unnecessary color attachment declaration and stencil rejection risk; GPU timing impact is neutral to small positive pending Frame Debugger proof.

### D023 ARWaypoint and SuitHUD Runtime Bridge Fences

Problem: `ARWaypointOverlay` still retried vegetation bridge resolution from solve cadence, and `SuitHUDV4CanvasOverlay` could revive Canvas bindings after scene load or use legacy runtime-origin conversion while stencil mode owned presentation.
Solution: Waypoint vegetation bridge is now cold/bootstrap/origin-shift or `MapMagicVegetationRuntime` hot-swap only; Tick and SlowTick do not poll registry. Suit HUD scene-load binding exits when stencil mode is active, and stencil suppression disables the legacy proxy-light AUP path.
Rejected Alternatives: Retrying `ActiveRuntimeInstance`/`GlobalRegistry` in Tick or allowing after-scene-load Canvas component creation under stencil ownership.
Scalability potential: Low devices get less solve variance; high/ultra keep waypoint data richness from owner snapshots, not scene searches.
Hardware Impact: Expected 1-5 us CPU variance reduction for waypoint solve and prevents hidden Canvas work spikes.

### D024 Legacy HUD Text Growth Fence

Problem: Long localization/template text could drive `EnsureCharCapacity` into `new char[capacity]` from legacy Canvas HUD refresh paths.
Solution: Metric display buffers are pre-sized to fixed 256-char staging, runtime `EnsureCharCapacity` refuses to grow arrays during play, and template/text copy helpers truncate into existing caller-owned buffers.
Rejected Alternatives: Hot-path array growth for rare long strings or assuming localization input always fits 64 chars.
Scalability potential: Low devices avoid rare GC spikes; high-tier visuals are unaffected because stencil text is shader-side digits.
Hardware Impact: Removes rare managed allocation from legacy HUD refresh; expected spike avoidance depends on localized string length.

### D025 Raw Black-Box Dump and Report Honesty

Problem: The telemetry dump used `BinaryWriter`, and the JSON report could claim full HUD purge while legacy `GraphicRaycaster` source tokens still existed behind stencil fences.
Solution: `DumpTelemetryOnce` now writes a fixed 32-byte little-endian header and raw 64-byte `VisorTelemetryEntry` rows with `ReadOnlySpan<byte>`. `HUDCanvasInquisition` now includes `GraphicRaycaster` script tokens in the purge verdict, and the JSON report marks full managed purge as false while documenting runtime stencil takeover.
Rejected Alternatives: Managed field-by-field dump writer and a report that overstates the static source state.
Scalability potential: Fault dumps stay deterministic across low/high hardware; designers see honest remaining legacy debt instead of a false green state.
Hardware Impact: Crash path only; raw dump is 19.2 KB plus 32-byte header for the 300-frame ring.

### D026 Verification Gate After Subagent Hardening

Problem: Compile verification is useful after the subagent hardening patches, but AGENTS.md forbids starting a build when CPU is above 50% or another compiler is active.
Solution: Re-ran static scans, JSON parse, diff whitespace check, and build gate sampling. The targeted visor/SuitHUD forbidden scan returned no `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, persistent runtime `NativeArray`, or depth texture residue. JSON parses. `git diff --check` returned only LF-to-CRLF warnings. Process query returned no dotnet/csc/VBCSCompiler rows, but CPU was 100% by CIM and all five counter samples were 100%, so build was not launched.
Rejected Alternatives: Launching dotnet build under full CPU load or claiming compile proof from static checks.
Scalability potential: No runtime impact.
Hardware Impact: Protects parallel agents and avoids IO contention on a saturated workstation.

### D027 Shared Report Upsert Facade

Problem: `HUDCanvasInquisition.Run()` could regenerate `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` as a single SHINOBU_270 object, deleting neighboring proof sections from other agents in the shared rendering report.
Solution: Replaced destructive root write with an editor-only top-level section upsert under `shinobu_270_visor_ar_stencil`; if the shared file is missing or malformed the writer creates a minimal object containing only the SHINOBU_270 section. Current static JSON still parses after this source patch.
Rejected Alternatives: Keeping the overwrite because Task 19 names the shared report, or moving to a dedicated-only report and leaving the shared aggregate unmaintained.
Scalability potential: Runtime Low/Middle/High/Ultra paths are unchanged; this is a cold editor facade that preserves multi-agent proof data.
Hardware Impact: Runtime impact 0 us. Editor scan cost remains cold and bounded by asset/script file count; no gameplay hot path is touched.

### D028 Build Gate After Report Facade

Problem: Compile verification is still required, but the workstation remains saturated after the report-facade patch.
Solution: Rechecked compiler processes and CPU. The first check found an active `dotnet.exe` compiling `Hecton8.Core.csproj`; the follow-up check found no compiler rows, but CPU remained 100% by CIM and 100% for all five processor counter samples. Build remains blocked.
Rejected Alternatives: Starting a competing build during another compiler window, or launching after compiler exit while CPU remained fully saturated.
Scalability potential: No runtime impact.
Hardware Impact: Protects shared local CPU/IO for the parallel-agent batch.

### D029 Subagent P0 Compile Closure

Problem: Read-only subagent audit found two likely compile/import blockers: `RasterCommandBuffer.SetGlobalConstantBuffer` was called with nameID-first order, and `HUDCanvasInquisition` used `UnityEngine.UI.GraphicRaycaster` while `Hecton8.UI.Editor.asmdef` did not reference `UnityEngine.UI`.
Solution: Verified local SRP source and switched SHINOBU_270 RenderGraph binding to `SetGlobalConstantBuffer(GraphicsBuffer, int, int, int)`. Removed direct `UnityEngine.UI` import from the editor inquisition and counted `GraphicRaycaster` by component type full name instead of widening the editor asmdef.
Rejected Alternatives: Adding a new asmdef reference for one cold scanner, trusting `Shader.SetGlobalConstantBuffer` overload order for `RasterCommandBuffer`, or waiting for a full build under a closed CPU gate.
Scalability potential: Runtime Low/Middle/High/Ultra visuals unchanged; compile-wall surface is smaller because the cold editor scanner does not add a UI package edge.
Hardware Impact: Runtime microsecond impact 0. Import/build blocker risk reduced before Unity compile proof.

### D030 Renderer-Owned Suppression Fail-Open

Problem: Default-true stencil suppression and presentation-controller-owned runtime toggles could leave both Canvas HUD and AR Canvas slots hidden if the RenderGraph feature was absent, import-broken, or failed Vault/material/frame upload after a previous good frame. An initial patch that cleared suppression at the start of every successful `AddRenderPasses` would have caused per-frame Canvas release/re-suppress churn.
Solution: `ARWaypointOverlay` and `SuitHUDV4CanvasOverlay` now reset stencil flags false. `SuitHUDPresentationController` only drives these flags in editor preview; runtime suppression is owned by `HectonVisorARStencilRendererFeature`. The feature enables suppression only after Game/Base camera, materials, DataVault handles, mask mesh, and frame upload are valid; it clears suppression on concrete failure paths and on disable/dispose.
Rejected Alternatives: Keeping fail-closed defaults, letting the presentation controller suppress without renderer proof, or toggling false/true every successful frame.
Scalability potential: Low devices get a visible legacy fallback when the stencil renderer cannot prove readiness; Middle/High/Ultra retain the single RenderGraph stencil route once frame prep succeeds.
Hardware Impact: Prevents blind-HUD failure state and avoids unnecessary Canvas enable/disable churn on successful frames. Runtime performance remains renderer-owned when active.

### D031 Build Gate After Subagent P0 Closure

Problem: Compile verification is still pending after P0 closure, but AGENTS.md forbids starting dotnet when CPU exceeds 50% or another compiler is active.
Solution: Re-ran targeted forbidden scan, JSON parse, diff check, and build gate. Static scan returned empty for `GlobalSignals`, `FromRuntimePosition`, shader global setters, `Canvas.ForceUpdateCanvases`, `TryGetLatestCreated`, Burst/job/tiny-run wrappers, persistent runtime `NativeArray`, and depth texture residue in SHINOBU_270 target files. JSON parses. Diff check reports only LF-to-CRLF warnings. One build gate found active `dotnet.exe` PID 38292 building `Hecton8.Core.csproj` and `csc.exe` PID 26708; the latest gate returned no compiler rows, but CPU remained 100% by CIM and 98.5%, 100%, 100% by processor counter samples.
Rejected Alternatives: Launching a competing dotnet build, killing another agent's compiler, building under saturated CPU, or claiming compile proof from static/source scans.
Scalability potential: No runtime impact.
Hardware Impact: Protects local CPU/IO for the parallel batch; compile proof remains PENDING VERIFICATION.

### D032 Editor Gizmo AUP Bridge Removal

Problem: `HectonVisorStencilPreviewGizmo` was editor-fenced but still used `AbsoluteUniversePosition.FromRuntimePosition`, which weakens the audit trail for the SHINOBU_270 AUP mandate and shows up in targeted source scans when editor proof files are included.
Solution: Converted the gizmo camera AUP calculation to `HectonFloatingOrigin.CurrentTotalOffsetDouble + runtimeCameraPosition` in double precision, then `AbsoluteUniversePosition.FromAbsolutePosition`.
Rejected Alternatives: Leaving the bridge because it is editor-only or excluding the gizmo from the scan.
Scalability potential: Runtime Low/Middle/High/Ultra path unchanged; editor proof now mirrors the same local-AUP discipline as the RenderGraph projection path.
Hardware Impact: Runtime impact 0 us; editor-only math remains bounded to Scene View gizmo draw.

### D033 Final Build Gate Snapshot

Problem: Compile proof is still the missing verification artifact after SHINOBU_270 source and report hardening, but AGENTS.md forbids starting dotnet when CPU is above 50% or another compiler is active.
Solution: Rechecked the gate before reporting. No `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` rows were present, but CPU remained 100% by CIM and 100%, 100%, 100% by processor counter samples, so build was not launched.
Rejected Alternatives: Launching `dotnet build` under saturated CPU, killing unrelated compiler work, or presenting static scans as compile proof.
Scalability potential: Runtime Low/Middle/High/Ultra behavior unchanged; this preserves batch workstation throughput.
Hardware Impact: Prevents additional IO and CPU contention on a fully loaded shared machine. Compile remains PENDING VERIFICATION.

### D034 Player-Camera Ownership and Abort Fail-Open

Problem: Read-only subagent audit found that broad Game/Base camera acceptance could let minimap/capture/spectator cameras suppress the HUD Canvas fallback, and `RecordRenderGraph` early exits could leave suppression active after a previous good frame.
Solution: `HectonVisorARStencilRendererFeature` now requires `IPlayerRuntimeContext.PlayerCamera` reference equality before runtime stencil takeover. Stencil and AR `RecordRenderGraph` aborts for backbuffer or invalid source/depth resources clear renderer-owned suppression, preserving fail-open Canvas fallback.
Rejected Alternatives: Treating all Game/Base cameras as equivalent, or clearing/re-suppressing Canvas every successful frame to cover abort cases.
Scalability potential: Low devices keep a visible fallback when player-camera proof or graph resources are absent; Middle/High/Ultra retain the same single RenderGraph route once proof exists.
Hardware Impact: Runtime microsecond impact is effectively 0. The change prevents a blind-HUD state and avoids per-frame Canvas toggling churn on successful frames.

### D035 Stencil Lane Isolation and Cold Shader Warmup

Problem: Defaulting stencil masks to `255` writes every stencil bit and risks contaminating neighboring effects; AR shader instancing variants also created unnecessary import/warmup surface for a fullscreen pass.
Solution: SHINOBU_270 reserves stencil bit 0 only (`ReadMask=1`, `WriteMask=1`), coerces legacy serialized `255` writer masks to lane 1, removes unused instancing pragmas from the fullscreen shader, and adds `Hecton_VisorAR_Stencil.shadervariants`. D038 supersedes the initial renderer-owned warmup route and moves the collection to bootstrap prewarm.
Rejected Alternatives: Keeping `255` for convenience, creating per-camera materials, or accepting runtime shader variant creation during visor activation.
Scalability potential: Low through Ultra keep identical stencil identity; `GlobalQualityWeight` only scales shader ALU richness, not stencil ownership or DTO layout.
Hardware Impact: Prevents cross-pass stencil corruption. Warmup is cold-load only; runtime microsecond impact is 0 and first-use hitch risk is reduced.

### D036 Editor Preview NativeArray Token Purge

Problem: The editor-only stencil preview gizmo still allocated a Temp `NativeArray`, which was not a runtime hot-path fault but kept a forbidden allocation token inside the SHINOBU_270 scan surface.
Solution: Added a `Span<StencilTargetSourceDTO>` snapshot overload to `ARWaypointOverlay.CopyStencilTargetSources` and converted `HectonVisorStencilPreviewGizmo` to a fixed `stackalloc` span for its 16-row editor preview.
Rejected Alternatives: Excluding editor files from the scan, retaining Temp `NativeArray` because it was disposed, or adding a private persistent preview buffer.
Scalability potential: Runtime Low/Middle/High/Ultra route unchanged; editor proof remains allocation-free for the inspected target rows.
Hardware Impact: Runtime impact 0 us. Editor preview avoids a tiny Temp allocation and removes a static compliance defect.

### D037 Verification Gate After Player-Camera Polish

Problem: Compile verification is still pending after the player-camera, stencil-lane, warmup, and editor-preview patches, but the AGENTS.md build gate still forbids dotnet while CPU exceeds 50%.
Solution: Re-ran JSON parse, targeted forbidden-token scans, diff whitespace check, git status on owned files, and build gate sampling. JSON parses; forbidden scans returned no hits for the tracked SHINOBU_270 risk tokens; diff check reported only LF-to-CRLF warnings. Process query returned no compiler rows, but CPU was 100% by CIM and 98.84%, 99.81%, 100% by processor counter samples.
Rejected Alternatives: Running `dotnet build` under saturated CPU, ignoring the editor `new NativeArray` scan hit, or presenting static scans as compile proof.
Scalability potential: Runtime behavior unchanged; proof route protects multi-agent workstation throughput.
Hardware Impact: Prevents extra compiler contention on a fully loaded machine. Compile remains PENDING VERIFICATION.

### D038 Bootstrap-Owned Shader Warmup Route

Problem: Subagent audit found `HectonVisorARStencilRendererFeature.Create()` calling `ShaderVariantCollection.WarmUp()`. URP renderer-feature creation can happen on renderer reload or first runtime activation, so this was a shader-stutter route outside the existing bootstrap/loading-screen warmup lane.
Solution: Removed the feature-local SVC field and `WarmUp()` call. Serialized `Assets/_Project/Art/Shaders/Variants/Hecton_VisorAR_Stencil.shadervariants` into `Assets/_Project/Scenes/00_BOOTSTRAP.unity` under `BootstrapController.shaderVariantCollections`, which is already handed to `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` during the boot prewarm phase before gameplay scene activation. Updated route docs and ledger to name the single warmup owner.
Rejected Alternatives: A `Resources.Load` warmup, renderer-asset SVC duplication, or a new bootstrap API were rejected because they create a second owner/route or add compile-wall surface. Keeping `Create()` warmup was rejected because it can hitch active gameplay.
Scalability potential: Low/Middle/High/Ultra all use the same curated variants; `GlobalQualityWeight` only changes shader math at runtime, not variant identity or warmup ownership.
Hardware Impact: Steady-frame impact 0 us. On i3/MX350-class hardware this removes a potential first-activation shader compile hitch from the render lifecycle and confines the cost to the existing loading/prewarm window.

### D039 Editor Gizmo Stack Scratch Cap

Problem: The editor-only visor preview used a fixed 16-row `stackalloc` of `StencilTargetSourceDTO`, roughly 1280 bytes per SceneView gizmo draw. It was not GC and not in player builds, but it exceeded the local small-stack guidance and was easy to cargo-cult into runtime code later.
Solution: Capped the preview to three targets (`PreviewTargetCapacity = 3`) while leaving the runtime renderer at the full 16-row DTO capacity.
Rejected Alternatives: A private persistent editor array, reintroducing `NativeArray`, or keeping the full 16-row editor preview.
Scalability potential: Runtime Low/Middle/High/Ultra route unchanged; editor preview remains a bounded diagnostic, not an authority path.
Hardware Impact: Runtime impact 0 us. Editor stack scratch drops from about 1280B to about 240B per SceneView draw.

### D040 Verification Gate After Bootstrap Warmup Patch

Problem: The renderer-owned warmup removal and bootstrap-scene SVC edit needed proof without violating the build gate.
Solution: Re-ran targeted forbidden-token scans over visor/UI/shader files, RenderGraph/stencil sanity scan, SVC scene GUID scan, JSON parse, custom trailing-whitespace scan, and build gate sampling. The forbidden scans returned clean, `00_BOOTSTRAP.unity` component script GUID `37290befeffd3d94796e62b9097c7db9` matches `BootstrapController.cs.meta`, the same component contains guid `27027027027027027027027027027027` in `shaderVariantCollections`, JSON parses, and custom whitespace scan returned no trailing whitespace. No compiler process was present, but CPU stayed 90% by CIM and 63.74%, 64.3%, 93.08%, 100%, 100% by processor counter samples.
Rejected Alternatives: Launching `dotnet build` under saturated CPU, accepting the stale renderer `WarmUp()` route, or relying only on the subagent static report.
Scalability potential: Runtime Low/Middle/High/Ultra visuals unchanged; warmup ownership is now a single bootstrap route independent of quality weight.
Hardware Impact: Protects shared workstation CPU/IO. Compile proof remains PENDING VERIFICATION until CPU is below 50% and no compiler process is active.

### D041 Final Build Gate Resample

Problem: A compile would be useful after static clean scans, but AGENTS.md forbids starting `dotnet build` when CPU exceeds 50% or another compiler is active.
Solution: Resampled the gate after the final static scans. No `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` process was present, but CPU was 100% by CIM and 100%, 100%, 100% by processor counter samples. Build was not launched.
Rejected Alternatives: Starting a compiler under saturated CPU or claiming compile proof from static source scans.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION.

### D042 Static API Recheck After Bootstrap Warmup

Problem: After context compaction and another user mandate, the source had to be revalidated from disk instead of trusting chat memory. The specific risk was a silent RenderGraph or shader-binding ABI mismatch that a static forbidden-token scan would not catch.
Solution: Rechecked owned-file git state, local SRP package signatures, existing in-repo RenderGraph post passes, shader CBUFFER names, structured-buffer id binding, bootstrap scene/SVC GUID route, JSON parse, and whitespace. `RasterCommandBuffer` exposes `SetGlobalTexture(int, TextureHandle)`, `SetGlobalBuffer(int, GraphicsBuffer)`, `SetGlobalConstantBuffer(GraphicsBuffer, int, int, int)`, and `CoreUtils.DrawFullScreen(RasterCommandBuffer, ...)`; SHINOBU_270 uses those exact call shapes. C# property ids match shader `CBUFFER_START(HectonVisorHudParams)`, `CBUFFER_START(HectonVisorDigitParams)`, and `_HectonVisorArTargets`.
Rejected Alternatives: Replacing RenderGraph bindings with legacy `Shader.SetGlobal*`, touching unrelated dirty files, or waiting for a build before checking deterministic local API shape.
Scalability potential: Runtime Low/Middle/High/Ultra route unchanged. This protects the single RenderGraph stencil path; quality remains a continuous shader parameter, not a variant or route switch.
Hardware Impact: Runtime 0 us. It prevents silent blank AR resolve/import failure that would otherwise fall back to Canvas or hide HUD proof.

### D043 Active Compiler Build Gate

Problem: Compile proof is still pending, but AGENTS.md forbids launching `dotnet build` when another `dotnet`/`csc` is active or CPU is above 50%.
Solution: Sampled the gate after static API recheck. Active `dotnet` PID 10784 and `csc` PID 25392 were present, with CPU 100% by CIM and 86.74%, 94.06%, 96.76% by processor counter samples. Build was not launched.
Rejected Alternatives: Launching a competing compiler, killing another agent's compiler, or claiming compile proof from static scans.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION until no compiler process is active and CPU is below 50%.

### D044 Generated C# Project Staleness

Problem: The current generated `Hecton8.Core.csproj` is an explicit compile list and does not include `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs` or `Assets/_Project/Scripts/Visor/HectonVisorStencilPreviewGizmo.cs`, while it does include older visor files such as `HectonVisorFluidDistortionFeature.cs`. A `dotnet build Hecton8.Core.csproj` against this stale project would not compile the new SHINOBU_270 scripts and would be false evidence.
Solution: Marked the compile proof as requiring Unity AssetDatabase/project-file regeneration or an equivalent Unity import path before the generated project can verify the new files. The generated `.csproj` is left untouched because its header says it is overwritten by Unity generation.
Rejected Alternatives: Manually editing the generated `.csproj`, claiming stale `dotnet` proof, or adding a separate project-file dependency edge for one agent's files.
Scalability potential: Runtime Low/Middle/High/Ultra route unchanged. This is verification hygiene: the single stencil RenderGraph route remains the only intended renderer, but proof must come from the actual Unity import graph.
Hardware Impact: Runtime 0 us. Prevents a false compile report that could ship uncompiled or editor-unimported scripts.

### D045 Active Compiler Gate After Csproj Audit

Problem: A build gate resample was needed after discovering stale project-file coverage, but AGENTS.md still forbids launching a compiler beside another active compiler.
Solution: Resampled compiler and CPU gates. Active `dotnet` PID 12844 and `csc` PID 29340 were present; CPU was 93% by CIM and 65.84%, 86.14%, 34.97%, 38.23%, 46.6% by processor counter samples. Build was not launched.
Rejected Alternatives: Launching a competing compiler, killing another agent's compiler, or bypassing the active-compiler gate because some CPU counter samples fell below 50%.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION until project files regenerate/import the new scripts and the build gate is legally open.

### D046 Active Compiler Gate Watch

Problem: The build gate was resampled again after documentation was updated, but the workstation was still inside another active C# compilation window.
Solution: Active `dotnet` PID 30716 and `csc` PID 14152 were present; CPU was 73% by CIM and 60.81%, 67.67%, 50.23% by processor counter samples. Build was not launched.
Rejected Alternatives: Starting a competing compiler, killing unrelated build work, or treating a borderline 50.23% sample as permission while an active compiler process exists.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION.

### D047 Visor Vault Descriptor Release Route

Problem: `HectonVisorARStencilRendererFeature` requested visual-only `VaultGenerationHandle<T>` lanes from `GlobalDataVault`, then dropped local descriptors through `ClearVaultHandles()` on DataVault replacement and feature disposal. Clearing a descriptor is not ownership release; it can leave Vault reference counts alive, block compaction, and make hot-swap state ambiguous.
Solution: Added `ReleaseVaultHandles(IDataVault)` and a typed `ReleaseVaultHandle<T>()` helper. Dispose, DataVault service replacement, and cold service rebind now call `IDataVault.ReleaseBuffer(in handle)` for all seven owned SHINOBU_270 descriptors before tombstoning them. Removed the clear-only helper so the local pattern is release-first.
Rejected Alternatives: Broad `ReleaseOwnerBuffers(SystemID.UI)` was rejected because UI owns neighboring lanes. Keeping descriptor-only clear was rejected because it hides native ownership. Manual generated `.csproj` edits were rejected because the compile project is Unity-generated and stale.
Scalability potential: Low/Middle/High/Ultra render behavior is unchanged. This protects the same continuous `GlobalQualityWeight` shader route by preventing cold lifecycle leaks during renderer reloads and DataVault hot-swap.
Hardware Impact: Steady-frame impact 0 us. On i3/MX350-class hardware this prevents accumulated native buffer residency and compaction pressure during repeated renderer reloads; exact memory delta requires Unity Memory Profiler proof.

### D048 Build Gate After Vault Lifecycle Patch

Problem: Compile proof is still pending after the Vault lifecycle patch, but AGENTS.md forbids starting `dotnet build` while another compiler is active or CPU exceeds 50%.
Solution: Re-ran targeted forbidden-token scan and diff check, then sampled build gates. The scan returned no hits for SHINOBU_270 risk tokens. `git diff --check` reported only Git LF-to-CRLF warning. Active `dotnet` PID 34832 and `csc` PID 15644 were present; CPU was 100% by CIM and 90.05%, 82.47%, 86.72% by processor counter samples. Build was not launched.
Rejected Alternatives: Launching a competing compiler, killing unrelated compiler work, building against stale generated project files, or claiming compile proof from static scans.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION until project files regenerate/import SHINOBU_270 scripts and the build gate is legally open.

### D049 Documentation Verification Gate After Vault Patch

Problem: The source patch and documentation updates needed a final verification pass, but compile is still gated by active compiler work and generated-project staleness.
Solution: Ran `git diff --check` on patched source/docs; it returned only Git LF-to-CRLF warning for the ledger, no whitespace defects. Re-sampled compiler and CPU gates: active `dotnet` PID 22280 and `csc` PID 13460 were present; CPU was 43% by CIM but 85.74%, 72.45%, 95.18% by processor counter samples. Build was not launched.
Rejected Alternatives: Treating one low CIM sample as permission while another compiler was active, launching a second compiler, or building the stale generated `Hecton8.Core.csproj`.
Scalability potential: No runtime route change.
Hardware Impact: Protects shared workstation CPU/IO. Compile remains PENDING VERIFICATION.

### D050 AR Target Upload MemCpy Correction

Problem: `ArPass.UpdateGpuPayload` uploaded HUD and digit constant buffers through `UnsafeUtility.MemCpy`, but copied the 16-row AR target structured buffer with per-row C# assignment loops. The route was bounded and cheap, but it contradicted the logged Task 11 claim that all mapped GPU payloads used direct MemCpy.
Solution: Added `CopyTargetsToMappedBuffer`, which clamps the source count, copies active `VisorArTargetDTO` rows with `UnsafeUtility.MemCpy`, and clears inactive mapped rows with `UnsafeUtility.MemClear`. DTO layout, BufferIDs, shader binding names, RenderGraph declarations, stencil lane, and rollback exclusion are unchanged.
Rejected Alternatives: Keeping the loop because the count is only 16 was rejected because the code and evidence log would stay inconsistent. Reintroducing a Burst job was rejected because this is still a tiny same-frame visual payload and would add dispatch overhead.
Scalability potential: Low devices reduce CPU copy variance while retaining flat stencil linework; Middle/High/Ultra keep the same continuous `GlobalQualityWeight` shader overkill path and target capacity.
Hardware Impact: Expected i3/MX350 gain is small, roughly 1-3 us CPU variance reduction pending profiler proof. The larger impact is eliminating a false proof artifact in the upload path.

### D051 RenderGraph Suppression Proof Gate

Problem: A read-only subagent found that `AddRenderPasses` enabled stencil presentation before RenderGraph proved that the AR resolve pass had been recorded. Existing abort cleanup handled backbuffer/invalid-resource exits inside `RecordRenderGraph`, but compatibility/no-graph/drop paths could leave Canvas suppression active from a pending frame without a resolve.
Solution: `AddRenderPasses` now writes only `_pendingStencilPresentationFrame`. `ArPass.RecordRenderGraph` calls `MarkStencilResolveRecorded()` only after it creates the resolve texture and assigns `resourceData.cameraColor`. A cold-registered `RenderPipelineManager.endCameraRendering` watchdog clears the pending token and releases Canvas suppression when the authorized player camera reaches end-camera without a matching resolve record.
Rejected Alternatives: Keeping pre-record suppression, restoring Canvas/TMP as an active parallel renderer, or relying only on `RecordRenderGraph` abort hooks. A per-frame polling MonoBehaviour was rejected because it would add another hot owner.
Scalability potential: Low devices keep fail-open safety if RenderGraph cannot prove output; Middle/High/Ultra still use the same stencil shader route and continuous `GlobalQualityWeight` ALU curve once resolve proof exists. DTO layout, BufferIDs, save identity, rollback exclusion, and shader resource names are unchanged.
Hardware Impact: Steady successful frame cost is effectively 0 us aside from a cold event delegate and two integer frame-token writes. It prevents a blind HUD fail-closed state while preserving the 250-750 us Canvas rebuild/overdraw avoidance only on proven RenderGraph frames.

### D052 Verification Gate After Suppression Watchdog

Problem: The fail-open watchdog patch changed renderer lifetime/control flow and needed fresh proof, but the generated Unity project remains stale and the workstation is actively compiling other work.
Solution: Re-ran the targeted forbidden-token scan, diff whitespace check, generated-project inclusion check, compiler-process gate, and CPU gate. The scan returned no visor/UI/shader risk-token hits. `git diff --check` reported only Git LF-to-CRLF warnings. `Hecton8.Core.csproj` still contains `SuitHUDV4CanvasOverlay.cs`, `ARWaypointOverlay.cs`, and `HectonVisorFluidDistortionFeature.cs`, but not `HectonVisorARStencilRendererFeature.cs` or `HectonVisorStencilPreviewGizmo.cs`.
Rejected Alternatives: Launching `dotnet build` while active `csc.exe`/`dotnet.exe`/`VBCSCompiler.exe` processes exist, manually editing the generated `.csproj`, or treating stale generated project coverage as compile proof.
Scalability potential: Runtime route unchanged: Low fail-opens to Canvas only when RenderGraph proof is absent; Middle/High/Ultra keep shader-side visor overkill once proof exists.
Hardware Impact: Runtime 0 us. Build was not launched because active compilers were present. Initial gate sampled 100% CPU with `csc.exe`/`dotnet.exe`/`VBCSCompiler.exe`; later gate still had active `dotnet.exe`/`VBCSCompiler.exe` and a 51.50% processor counter sample. This protects shared IO/CPU and keeps compile proof PENDING VERIFICATION.

### D053 Generated Project Coverage Gate

Problem: Subagent P2 correctly identified that external Roslyn/MSBuild paths can miss new SHINOBU_270 scripts while `Hecton8.Core.csproj` is stale. A one-off `Select-String` proof is not enough; the editor report needs a repeatable visible gate.
Solution: Extended `HUDCanvasInquisition` to read generated `Hecton8.Core.csproj` cold, check exact `Compile Include` coverage for `HectonVisorARStencilRendererFeature.cs` and `HectonVisorStencilPreviewGizmo.cs`, and emit `generatedProjectIncludesRendererFeature`, `generatedProjectIncludesStencilPreviewGizmo`, and `generatedProjectStale` into the shared rendering report section.
Rejected Alternatives: Editing the Unity-generated `.csproj`, broad global compliance mutation during a domain task, or treating a build against stale project files as proof.
Scalability potential: Runtime Low/Middle/High/Ultra behavior unchanged; the gate protects verification integrity only.
Hardware Impact: Runtime 0 us. Editor report cost is cold file read and string search only when the inquisition menu is run.

### D054 Shared Report Artifact Refresh

Problem: The source for `HUDCanvasInquisition` emitted generated-project staleness fields, but the existing `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` artifact had not been regenerated and therefore did not expose the stale-project proof the CTO actually reads.
Solution: Added a preserved top-level `shinobu_270_visor_ar_stencil` object to the shared report with `generatedProjectIncludesRendererFeature=false`, `generatedProjectIncludesStencilPreviewGizmo=false`, and `generatedProjectStale=true`. The patch also corrected the older shader warmup wording from renderer `Create()` warmup to the bootstrap `BootstrapController.shaderVariantCollections` prewarm route.
Rejected Alternatives: Running Unity editor menu work without an editor session, overwriting the shared report root, editing generated `.csproj`, or claiming stale `dotnet build` coverage.
Scalability potential: Runtime Low/Middle/High/Ultra route unchanged. This is evidence hygiene: the continuous `GlobalQualityWeight` shader path and fail-open Canvas suppression proof remain the same.
Hardware Impact: Runtime 0 us. Prevents false build/report evidence from consuming review time or masking missing Unity project-file import.

### D055 Shader Math-LOD Tap Shedding

Problem: The AR shader visually blended chromatic aberration by `quality`, but still sampled the red and blue offset taps when `GlobalQualityWeight` was zero. It also unrolled all 16 AR target rows every pixel even when the CPU had uploaded fewer active target DTOs.
Solution: Added a continuous `smoothstep(0.06, 1.0, quality)` chroma admission weight. When that weight is effectively zero, the shader skips the two aberration texture samples; once admitted, the visible aberration still ramps by the smooth weight. Replaced the fixed unrolled 16-row loop with a loop bounded by `_HectonVisorQualityAndTime.z`, the uploaded active target count.
Rejected Alternatives: Keeping dead low-quality taps, adding shader variants, using a hardware-tier keyword, or tying target loop count to a binary low/high route.
Scalability potential: Low survival quality pays one camera-color sample and active-target rows only. Middle gradually admits chroma and richer target animation. High/Ultra spend the saved Canvas cost on full chroma, scanlines, fog, and all active bracket rows without changing DTO layout or authority.
Hardware Impact: Low quality can avoid two fullscreen texture samples per visor AR pixel plus up to sixteen inactive target row reads/bracket evaluations. Exact GPU microseconds remain PENDING VERIFICATION until Frame Debugger/GPU capture.
