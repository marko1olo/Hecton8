# Rationale_SHINOBU_137

Status: PENDING VERIFICATION
Agent: SHINOBU_137
Domain: SUBMARINE_OS_TERMINAL_RENDERER

## Prompt Extraction
Problem: Need exact assignment isolation inside multi-agent batch.
Solution: Extracted `<AGENT_PROMPT id="SHINOBU_137">` from `Docs/Tasks/CURRENT_BATCH.md` using PowerShell raw file regex, not truncated MCP reading.
Rejected Alternatives: Reading adjacent prompts or inferring domain from chat text would violate strict parsing and create cross-agent contamination.
Scalability potential: Assignment targets terminal UI that remains cheap on weak devices and buys visual overkill via shader treatment on high-tier hardware.
Hardware Impact: Prevents terminal UI from paying Canvas mesh rebuild cost on i3/MX350; exact savings require Unity profiler evidence.

## Mandate Selection
Problem: Terminal renderer crosses UI, Burst DTO layout, AUP math, native buffers, execution phases, and signal lanes.
Solution: Read eight mandate files: UI diegetic interfaces, UI zero-GC streaming, ARM64 runtime layout, AUP determinism, zero-GC policy, native jobs, execution phases, signal segregation.
Rejected Alternatives: Reading all 80 mandate files would add noise; reading only UI files would miss AUP/native/signal constraints.
Scalability potential: Low/Middle/High/Ultra behavior must be a continuous curve via `GlobalQualityWeight`, not tier if-branches.
Hardware Impact: Mandates prioritize flat native buffers and staggered VISUAL_SYNC uploads, reducing CPU stalls on low-end silicon.

## Mandate Conflict Note
Problem: `UI_Diegetic_Physical_Interfaces.txt` contains legacy wording enforcing World Space Canvas, while SHINOBU_137 batch explicitly orders World Space Canvas eradication.
Solution: Treat current agent prompt as the domain-specific migration order: physical terminals become quads/projected textures with mathematical interaction. Preserve useful math/RT pool/shader constraints from the mandate.
Rejected Alternatives: Keeping World Space Canvas would directly fail Tasks 01/02 and retain rebuild/raycaster cost.
Scalability potential: Quad + RT projection scales from low-res fake to high-tier holographic shader overkill.
Hardware Impact: Removes Canvas rebuild and GraphicRaycaster traversal from terminal hot paths; measured microseconds pending profiler.

## Terminal Interaction DTO and AUP Kernel
Problem: Terminal hit state must be memcpy-safe for rollback and must not trigger CS1612 defensive copies in Burst.
Solution: Replaced the old virtual button DTO path with explicit `TerminalInteractionDTO` (32 bytes), `TerminalPlaneDTO` (128 bytes), `GazeRayDTO` (80 bytes), `ButtonAABBDTO` (32 bytes), and a 64-byte telemetry entry. All hot structs use raw public fields. `TerminalIntersectionJob` subtracts gaze AUP from terminal center AUP, casts the local delta to `float3`, then computes UV coordinates in local plane basis.
Rejected Alternatives: `Physics.Raycast`, `GraphicRaycaster`, `DiegeticPanelController.TryProjectRayToCanvas`, and direct world-float intersections were rejected because they route through physics/UI ownership and lose precision at 100km scale.
Scalability potential: Low quality uses larger job batches and lower visual cadence; middle/high/ultra preserve interaction math while increasing visual texture resolution and shader detail continuously through `GlobalQualityWeight`.
Hardware Impact: On i3/MX350-class hardware, avoiding physics/UI raycast fanout is expected to save 10-120 us per gaze event and remove Canvas mesh rebuild spikes. Exact values require Unity profiler after compile clears.

## Vault Memory and Signal Routing
Problem: Persistent private NativeArrays would violate H-PHI/data sovereignty and fragment memory.
Solution: Requested all owned buffers from `GlobalDataVault` using stable buffer IDs 71360-71375. New plane/button/gaze/interaction working buffers use `NativeArrayOptions.UninitializedMemory` and are fully written before hot use. Removed local managed scratch arrays from the owned runtime path: state buffers are explicit fields, active runtime slots are explicit fields, and CSV ingest uses cold stack `Span<byte>`. Click and UI outcomes route via existing `SignalBus<T>` lanes; offline terminal state is injected through `SetTerminalAvailability` instead of referencing logistics DTOs directly.
Rejected Alternatives: Private `NativeArray` fields, private managed scratch arrays, direct `LogisticsNodeDTO` references, sibling assembly dependencies, and managed event callbacks in the hot path.
Scalability potential: Owner-local state lets low devices shed terminal visual updates while high devices feed richer shader state without compile-wall coupling.
Hardware Impact: Avoids boot-time zero fill for fully-owned buffers and keeps memory contiguous for Burst prefetch. Estimated cold-start saving is small per buffer but removes unpredictable allocator pressure.

## Dear Lie Rendering Path
Problem: High-resolution terminal text and world-space Canvas rebuilds are too expensive for diegetic in-world screens, but interaction must not become sluggish when thermal pressure lowers visual cadence.
Solution: Preserved the existing texture-array compute blit and added `Hecton_DiegeticTerminal.shader` as the projected screen material path. The visual complexity is faked with low-res block output, scanlines, vignette, emission tint, and quality-weighted glow rather than CPU-side UI mesh detail. `framesBetweenUpdates` now gates formatting/texture updates only; gaze/intersection/button jobs remain frame-responsive.
Rejected Alternatives: World Space Canvas, TMP mesh rebuilds, and GameObject button overlays.
Scalability potential: Low quality reduces offscreen resolution and update cadence; high/ultra keep 512-class texture slices and stronger shader glow without changing gameplay truth.
Hardware Impact: Canvas rebuild spikes are replaced with bounded dirty-buffer uploads and a simple shader pass. Expected saving is 50-400 us per number update on weak CPU, with GPU fragment cost traded for immersion.

## Human Control and Debugging
Problem: Designers need terminal tuning without recompiling C#, and QA needs exact intersection evidence.
Solution: Added UI Toolkit `Diegetic UI Tuner`, editor DTO layout validator, cold `ReadOnlySpan<byte>` CSV parser, `OnDrawGizmos` plane/button/hit visualization, and `CharBufferPool` token replacement for `^POWER_LEVEL^`.
Rejected Alternatives: Runtime IMGUI, `string.Split`, `string.Replace`, and debug meshes.
Scalability potential: CSV/tuner are cold/editor-only; runtime keeps hot path unchanged from low to ultra devices.
Hardware Impact: No player-frame cost from editor-only tooling. Token replacement stays O(bytes) with no string allocation.

## Verification Blocker
Problem: Build verification is required, but project rules forbid launching dotnet when CPU is under work or another dotnet/csc is running.
Solution: Checked processes and CPU before build. First pass found multiple `dotnet` processes and CPU load 100%, so no build was launched. A later guarded build attempt rechecked immediately before execution; no `dotnet/csc` processes were present, but CPU had climbed to 82%, so the guard exited before invoking `dotnet build`. Repeat guards during post-audit showed CPU load 100%, then 94% after a 20-second wait. After packed dirty byte preservation, the guard cleared at CPU 21 with no compiler processes, and `dotnet build .\Hecton8.Core.csproj --no-restore` started. It failed before SHINOBU_137 code with `CS2001` because `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is deleted in the worktree while still referenced by the generated csproj.
Rejected Alternatives: Editing generated csproj files or restoring a deleted Construction-domain file would violate domain ownership. Launching repeated builds after the same external missing-source error would waste hardware.
Scalability potential: N/A.
Hardware Impact: Prevented extra compile pressure while CPU was saturated; the one allowed build stopped at an external deleted source before SHINOBU_137 compile proof.

## Post-Audit GPU Upload Guard
Problem: `UploadStateRun` locked a mapped `GraphicsBuffer` and then returned early if `ResolveTerminalStatePointer()` failed, leaving `UnlockBufferAfterWrite` uncalled.
Solution: Wrapped the state upload body in `try/finally` so the buffer unlock is guaranteed after every `LockBufferForWrite`, including the fault path. This is still zero-GC and stays inside the owned TerminalOS VISUAL_SYNC path.
Rejected Alternatives: Ignoring the fault as "unlikely" or adding a managed guard object. The former can poison GPU upload state; the latter adds managed machinery where a structured `finally` is enough.
Scalability potential: Low/Middle/High/Ultra all use the same bounded unlock path; no tier fork is introduced.
Hardware Impact: No steady-state frame gain claimed. Prevents a mapped-buffer stall/failure under vault-loss or editor reload races, which is more important than a microsecond estimate.

## Generated Project Scope
Problem: `dotnet build Hecton8.Core.csproj` cannot verify files that Unity has not yet regenerated into the csproj.
Solution: Checked current csproj membership. It includes `TerminalOsTypes.cs`, `TerminalOsRuntime.cs`, and `TerminalOsDesignerWindow.cs`; it does not yet include the new editor validator/tuner files. Full proof therefore needs Unity import/csproj regeneration plus console check when CPU guard allows.
Rejected Alternatives: Hand-editing generated Unity csproj files; that creates churn and does not reflect Unity's actual assembly graph.
Scalability potential: N/A.
Hardware Impact: Avoids unnecessary generated-file churn and compile wall pressure.

## Editor Ref Accessor Hardening
Problem: `TerminalOsDesignerWindow` previously depended on `GetTerminalStateRef(index)`, and the runtime accessor threw on invalid Vault/index state. That is tolerable for a small tool, but it violates the batch's no-fake-report posture because editor tuning can fail before the runtime Vault is online or after a domain reload.
Solution: Added `TrySetTerminalMockState(index, value1, value2)` as the editor-facing mutation route. It validates Vault and index, clamps finite values, writes the DTO back to the vault buffer, and marks the terminal dirty. The old ref accessor is preserved for compatibility but now records a fault and returns a static inert ref instead of throwing.
Rejected Alternatives: Removing `GetTerminalStateRef` outright would risk breaking unknown multi-agent callers. Letting the editor touch the native buffer directly would leak ownership and bypass dirty-index routing. Keeping managed throws would preserve a cold-path failure that is easy to eliminate.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this pass protects the human tuning facade without creating a tier fork or a runtime dependency.
Hardware Impact: No player-frame microsecond saving claimed. The measurable win is eliminating editor exception churn and preserving VISUAL_SYNC dirty routing through the owner-local runtime API.

## Compile Wall AUP Recheck
Problem: TerminalOS files import `Hecton8.World` for `AbsoluteUniversePosition`, and namespace-only review could misread that as a new sibling runtime dependency.
Solution: Located the type declaration at `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`. That file is under the root `Assets/_Project/Scripts/Hecton8.Core.asmdef` assembly, not under a new TerminalOS-owned sibling runtime reference. The existing asmdef references were not changed.
Rejected Alternatives: Removing AUP to avoid a namespace concern would violate Task 12 precision rules. Adding a direct World runtime reference would violate compile-wall routing. Reporting the namespace as proof of a dependency would be false.
Scalability potential: Keeping AUP preserves low-tier and ultra-tier precision behavior without float-world jitter.
Hardware Impact: No direct microsecond gain; prevents an unnecessary architecture churn patch and preserves localized float math after AUP subtraction.

## Panel Scalar Upload Coherency
Problem: The projected-terminal shader fake consumes quality/glitch scalars from panel instance `SliceFlags` and material properties, but the instance upload path only rewrote entries when transform matrices changed or a manual dirty flag was set. A stationary terminal could therefore keep stale visual quality after homeostasis changes.
Solution: Added last-uploaded quality/glitch sentinels and force panel instance rewrite/upload when either scalar changes. `RefreshScalabilityPolicy` and `ApplyDiegeticGlitchIntensity` also dirty material bindings so `_HectonDiegeticGlitchQualityWeight` stays coherent with `GlobalQualityWeight`.
Rejected Alternatives: Updating shader scalars every frame would add needless main-thread material churn. Waiting for transform changes makes thermal scalability visually stale. Adding a separate managed event would create unnecessary ownership coupling.
Scalability potential: Low devices now visibly shed glow/scanline richness when quality drops; middle/high/ultra can ramp the projected fake back up without object rebuilds or shader variant swaps.
Hardware Impact: Adds O(T) panel DTO rewrite only when scalar values change or when an existing dirty upload is already required. Stable frames pay no extra per-terminal work.

## Complete Mapped Upload Unlock Guard
Problem: State upload had a guaranteed unlock, but dirty-index, screen-command, glyph-UV, and panel-instance upload paths still relied on straight-line copy completion after `GraphicsBuffer.LockBufferForWrite`.
Solution: Wrapped every owned mapped upload copy in `try/finally` so `UnlockBufferAfterWrite` executes for each buffer lane.
Rejected Alternatives: Assuming `UnsafeMemoryCopyGuard` and logging never fail is weak fault handling. A disposable managed helper would be unnecessary and less explicit.
Scalability potential: Low/Middle/High/Ultra all share the same deterministic buffer lifetime rule.
Hardware Impact: No steady-state saving claimed. The value is preventing GPU upload lane poisoning and editor/runtime stalls under exceptional fault paths.

## Instanced Keyword Binding Guard
Problem: The terminal material could keep `HECTON_TERMINAL_INSTANCED` enabled after instanced rendering was disabled or before the panel instance buffer existed. That leaves the shader in the StructuredBuffer path when fallback rendering expects `_TerminalSlice`.
Solution: `BindTerminalRenderers` now enables the keyword only when `drawPanelsInstanced` is true and `_panelInstanceBuffer` is valid; otherwise it explicitly disables the keyword.
Rejected Alternatives: Trusting sticky material state is unsafe in editor workflows. Creating a separate material per path would add asset/material churn.
Scalability potential: Weak devices can fall back to non-instanced/material-slice rendering without stale shader state; high tiers keep instanced StructuredBuffer rendering.
Hardware Impact: No per-frame cost beyond binding dirtied state. Prevents wrong-path rendering and avoids debugging stalls caused by stale material keywords.

## Per-Frame Quality Scalar Refresh
Problem: `RefreshScalabilityPolicy` exited before reading `HomeostasisBrain.GlobalQualityWeight` while the texture refresh gate was closed. That made terminal visual cadence and shader scalar response lag by 30-120 frames under thermal changes.
Solution: Moved the cheap scalar read, `_framesBetweenUpdates` calculation, cached tier update, and scalar dirty flags before the heavy texture-resolution gate. Texture reallocation/resource work still obeys `_nextTierRefreshFrame`.
Rejected Alternatives: Updating render textures every frame would churn GPU resources. Keeping the early return violates the continuous scalability contract because thermal shedding becomes stale.
Scalability potential: Low/Middle/High/Ultra now breathe through the same scalar every frame while expensive RT changes remain amortized.
Hardware Impact: Adds a few scalar math ops per frame. Avoids stale high-cost visual cadence during rapid thermal pressure without forcing RT rebuilds.

## Attention Cull Dirty Preservation
Problem: `BuildDirtyList` cleared `TerminalStateDTO.IsDirty` when a terminal failed the camera attention cull. If power/text changed while offscreen, the texture update was lost and the terminal could remain visually stale when the player looked back.
Solution: Attention cull now defers upload by leaving `IsDirty` set. The dirty row is reconsidered on later frames and uploads when it passes the view/range cull.
Rejected Alternatives: Clearing the flag saves a negligible future branch but violates visual truth. Forcing all offscreen uploads wastes the culling benefit.
Scalability potential: Low devices still skip offscreen uploads; high tiers still upload promptly when visible. No binary quality branch is introduced.
Hardware Impact: Worst-case retained dirty flags add a bounded 64-row scan in TerminalOS. This is cheaper than uploading invisible textures and safer than losing updates.

## Button Span Evaluation Tightening
Problem: `EvaluateTerminalButtonsJob` scanned the entire `ButtonAABBDTO` buffer for every hovered terminal even though `TerminalPlaneDTO` already stores the owned button slice.
Solution: The job now clamps `LayoutFirstButton/LayoutButtonCount` against `ButtonCount` and scans only that span. It still validates `TerminalHash` before emitting signals to catch corrupted layout rows.
Rejected Alternatives: Keeping the full-buffer scan is simpler but violates the owner-local "one fact -> one route" rule and wastes predictable work. Removing the hash check would be faster but weaker against bad CSV/layout data.
Scalability potential: Low devices pay only per-terminal local button count; high tiers can afford richer button layouts without full-buffer scans per hit.
Hardware Impact: Mock terminals go from up to 128 AABB checks to 2 checks per hovered terminal. Exact microseconds still require profiler proof.

## Interaction Unblocked By Visual Format
Problem: `LateFrameTick` returned early while `UpdateTerminalTextJob` was still running. That protected the terminal state buffer from races, but it also skipped gaze intersection and click handling, violating the requirement that interaction remain responsive while visual formatting cadences down.
Solution: A pending visual format job now sets a `visualPipelineBlocked` flag. Visual state reads/uploads and state-derived plane availability refresh are skipped until the format job completes, but gaze intersection, click resolve, and panel draw continue against the last known plane state.
Rejected Alternatives: Completing the format job on the main thread would stall the frame. Scheduling interaction directly against a state buffer being written would race. Disabling interaction while visuals update creates input latency spikes.
Scalability potential: Low devices can run visual formatting at reduced cadence without freezing terminal interaction; high tiers still get same-frame visual refresh when the job completes fast.
Hardware Impact: No microsecond saving claimed. The fix removes latency spikes and avoids main-thread blocking while preserving race safety.

## Editor Text Scan Boundary
Problem: A broad forbidden-pattern scan across runtime and editor files flagged `Label.text` assignments in `DiegeticUiTunerWindow`. Treating that as runtime GC evidence would be a false report, while rewriting editor UI Toolkit status display to satisfy a hot-path grep would be cargo-cult architecture.
Solution: Kept the editor-only `#if UNITY_EDITOR` status label path as a cold human facade and corrected verification language to scope `.text`/formatted-string bans to player runtime/types. Runtime terminal formatting still uses raw DTOs, `ReadOnlySpan<byte>`, and leased char buffers where applicable.
Rejected Alternatives: Removing the tuner status line would reduce designer observability. Forcing an unmanaged text pipeline into an editor window would add complexity outside the player hot path without saving frame time.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; editor diagnostics remain available for tuning the continuous quality curve.
Hardware Impact: No player-frame cost. The only allocation risk is editor-only UI Toolkit status refresh, which is excluded from player builds and does not affect Quest/i3/MX350 runtime budgets.

## Agent-Owned Black Box Dump Path
Problem: Runtime black-box dump constants still used `Dump_TERMINAL_SURGEON.*`, which violates the agent-owned forensic route and can merge SHINOBU_137 evidence with a stale or different owner.
Solution: Renamed dump targets to `Docs/AgentLogs/Dump_SHINOBU_137.bin` and `Docs/AgentLogs/Dump_SHINOBU_137.h8dump`.
Rejected Alternatives: Keeping the generic/stale dump path would make crash evidence ambiguous. Adding a runtime-formatted path would allocate or add string machinery for a constant owner fact.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; the black-box route stays O(1) and fixed-size.
Hardware Impact: No steady-state microsecond gain. The change protects forensic ownership and avoids cross-agent overwrite ambiguity.

## GPU Dirty Clear Confirmation
Problem: `LateFrameTick` cleared `TerminalStateDTO.IsDirty` after calling the upload and dispatch methods even when graphics resources, mapped upload, or compute dispatch had failed or been skipped. That can lose a valid terminal texture update silently.
Solution: `UploadDirtyPayloads`, `UploadDirtyIndices`, `UploadDirtyStates`, and `UploadStateRun` now return success booleans. Dirty rows are cleared only when upload succeeds and `DispatchDirtyScreens` returns the full dirty count. Null graphics/state buffers fail closed and leave dirty flags set for retry.
Rejected Alternatives: Clearing after best-effort upload is faster in the false-success path but breaks visual truth. Forcing a main-thread GPU sync would be worse; the success contract is enough.
Scalability potential: Low devices can defer dirty uploads under missing/stalled graphics resources without losing updates; high/ultra behavior remains immediate when dispatch succeeds.
Hardware Impact: Adds a few predictable bool branches on dirty frames only. No steady-state allocation or per-clean-frame cost.

## Static Upload Dirty Flag Confirmation
Problem: Layout, glyph UV, and panel instance uploads still cleared their dirty flags after `UnsafeMemoryCopyGuard.ReportRejectedCopy` paths. `UpdatePanelInstancesIfNeeded` also committed the last-uploaded quality/glitch sentinels even if the panel instance buffer copy failed.
Solution: `UploadScreenCommands`, `UploadGlyphUvs`, and `UploadPanelInstances` now return copy success. Dirty flags clear only after `copied == true`, and panel scalar sentinels update only after panel instance upload succeeds.
Rejected Alternatives: Keeping best-effort static uploads would hide stale layout/font/panel scalar state. Forcing a blocking GPU validation step would add main-thread risk without proving more than the mapped copy result.
Scalability potential: Weak devices and editor reload windows keep retrying stale static uploads instead of freezing bad state; high/ultra successful paths are unchanged.
Hardware Impact: Adds one bool and branch per static dirty upload. No clean-frame cost and no allocation.

## Editor Asmdef Unsafe Reference
Problem: `TerminalOsLayoutValidator` uses `Unity.Collections.LowLevel.Unsafe.UnsafeUtility`, but `Hecton8.UI.Editor.asmdef` referenced only `Hecton8.Core` and `Unity.Mathematics`. After Unity regenerates project files, the editor assembly can fail if `Unity.Collections` is not explicitly referenced.
Solution: Added `Unity.Collections` to the editor asmdef reference list. This is an editor-only package reference, not a runtime sibling domain dependency.
Rejected Alternatives: Removing the layout validator would weaken the ARM64 padding proof. Switching to `Marshal` offsets would diverge from the same `UnsafeUtility` layout API used by Burst/native memory code.
Scalability potential: Runtime low/middle/high/ultra behavior unchanged; editor proof of DTO layout remains available.
Hardware Impact: Editor compile graph gains one explicit package reference; no player-frame cost.

## Packed Dirty Byte Preservation
Problem: `TerminalStateDTO.IsDirty` is intentionally packed into the high byte of `BackgroundColor` so the GPU-facing state remains 48 bytes. `UpdateTerminalTextJob` rewrote `BackgroundColor` and could clear a pending dirty flag when text/power percentages did not change.
Solution: The Burst job now reads `pendingDirty = state.IsDirty` before writing `BackgroundColor` and restores that byte before deciding whether current content changes should set dirty to 1.
Rejected Alternatives: Expanding `TerminalStateDTO` to 64 bytes would require HLSL ABI changes and more upload bandwidth. Moving dirty state to a separate buffer would add another Vault/GPU sync route. Leaving it packed without preservation causes silent stale texture loss.
Scalability potential: Low devices keep deferred dirty uploads reliable; high/ultra maintain the compact upload stride.
Hardware Impact: Adds one byte load/store per formatted terminal. Avoids larger 64-byte state uploads and shader ABI churn.
