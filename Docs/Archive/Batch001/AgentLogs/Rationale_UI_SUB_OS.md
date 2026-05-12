# Rationale_UI_SUB_OS

Status: PENDING VERIFICATION

## Decision 0 - State Tracking
Problem: UI_SUB_OS had no persistent status or rationale files, violating the batch anti-amnesia protocol.
Solution: Created `Docs/Tasks/Status_UI_SUB_OS.md` and `Docs/AgentLogs/Rationale_UI_SUB_OS.md` before runtime edits.
Rejected Alternatives: Chat-only progress was rejected because context compression is expected and disk logs are authoritative.
Scalability potential: No runtime effect; creates auditable work boundaries before Low/Mid/High/Ultra HUD paths.
Hardware Impact: 0 us/frame.

## Decision 1 - Stencil HUD Path
Problem: Transparent holograms behind glass and water create fill-rate collapse on MX350-class GPUs.
Solution: Use cockpit glass as stencil writer, then require Equal stencil in opaque monitor/sonar shaders with `Blend One Zero`.
Rejected Alternatives: Transparent Canvas groups and alpha-blended hologram backplates were rejected due overdraw.
Scalability potential: Low keeps opaque/cutout stencil. High/Ultra can spend saved fill-rate on brighter sweep/flicker shader effects.
Hardware Impact: Estimated 18-70 us/frame saved depending monitor coverage.

## Decision 2 - Sonar Mesh and Math LOD
Problem: Real raycast sonar is too expensive and unstable for a cockpit display.
Solution: Sample voxel/navigation height data, update Low at 10 Hz with no interpolation, and render via `Graphics.DrawMesh`.
Rejected Alternatives: Physics raycasts, secondary cameras, and full-resolution always-on mesh uploads.
Scalability potential: Low = 8-cell retro grid. Mid = 12-cell. High = 16-cell interpolation. Ultra = 18-cell visual overkill.
Hardware Impact: Estimated 95 us/update saved on Low versus high-frequency dense grid.

## Decision 3 - VWS Event Queue
Problem: The warning system used direct clip playback, bypassing the mandated queued audio path.
Solution: Added one-based audio event IDs and route VWS through `IAudioService.QueueAudioEvent(in AudioEvent)` after bitmask/tzcnt scan.
Rejected Alternatives: `PlayStatic2D` fallback and clip lookup from UI code were rejected; audio runtime owns clip table resolution.
Scalability potential: Low and High share same zero-GC event lane; High can author richer event-table clips without UI code changes.
Hardware Impact: Estimated 12 us/event saved and avoids managed audio dispatch from submarine OS.

## Decision 4 - Engine Heat Bar
Problem: Engine heat was still represented as dynamic text, violating the no-string heat display directive.
Solution: Removed dynamic heat percentage text and added an opaque 1D bar driven by cached heat percent and rect width.
Rejected Alternatives: TMP `HEAT xx%`, string formatting, or per-frame material allocation.
Scalability potential: Low = width-only bar. High/Ultra can use shader color ramps while preserving the same scalar input.
Hardware Impact: Estimated 8 us/refresh saved and no text mesh rebuild for heat.

## Decision 5 - Low-Power CRT Flicker
Problem: Low-power flicker should not dirty UI elements from C#.
Solution: Monitor and sonar stencil shaders multiply color by cheap `frac` scan noise when `_SubInteriorLightingState.z < 0.15`.
Rejected Alternatives: Per-widget C# alpha/color animation and managed noise state.
Scalability potential: Low uses one cheap shader noise term. High/Ultra can increase shader polish without CPU cost.
Hardware Impact: Estimated 25 us/frame CPU work avoided during low-power flicker.

## Decision 6 - Struct Alignment
Problem: UI event payloads cross managed/native/Burst-adjacent boundaries and must not be awkwardly packed.
Solution: Padded `HectonSubmarineOsSnapshot` and `SubmarineOsEventPayload` to 64 bytes, exactly two 32-byte lanes.
Rejected Alternatives: Implicit layout size and scattered padding fields.
Scalability potential: Uniform payload size keeps event reads predictable across device tiers.
Hardware Impact: Better cache-line locality; microsecond gain is workload dependent.

## Decision 7 - Dependency Blocks
Problem: Task 5 requires EcosystemDirector wall-distance data and task 14 requires quest landmark AUP data, but no public contracts expose those values.
Solution: Marked both tasks blocked by dependency instead of inventing direct dependencies or raycast substitutes.
Rejected Alternatives: Physics raycasts, scanning world landmarks, or adding cross-domain APIs without owner coordination.
Scalability potential: Once contracts exist, Low can consume scalar distances; High can add richer blip fade/landmark presentation.
Hardware Impact: 0 us/frame now; avoids adding wrong polling work.

## OMEGA POLISH CHANGES
Problem: Final audit required proving the HUD stayed below the frame-time budget and avoided hidden managed churn.
Solution: Scanned touched files for direct clip playback, `Canvas.ForceUpdateCanvases`, sqrt/normalize usage, dynamic heat strings, and struct alignment; ran narrow Assembly-CSharp build and Hecton8.Core build.
Rejected Alternatives: Claiming full project verification despite unrelated core compile failures.
Scalability potential: Low path now uses stencil, 10 Hz sonar, dominant-axis speed, width-only heat bar, shader flicker. High/Ultra keep interpolation and denser sonar grid.
Hardware Impact: Expected low-end gain comes from avoided overdraw, no secondary camera, no raycast sonar, no heat text rebuild, queued audio events.

Final Git Diff Summary:
- `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`: VWS queued audio IDs, 64-byte payloads, sonar LOD globals already present/audited.
- `Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs`: opaque panel retained, CO2 gauge audited, heat text replaced with 1D bar.
- `Assets/_Project/Art/Shaders/Hecton_SubmarineMonitorOpaqueStencil.shader`: stencil Equal, opaque blend, low-power flicker.
- `Assets/_Project/Art/Shaders/Hecton_SubmarineSonarHoloMapStencil.shader`: stencil Equal, opaque blend, sweep and low-power flicker.
- `Assets/_Project/Art/Shaders/Hecton_SubmarineCockpitGlassStencil.shader`: stencil write mask with ColorMask 0.
- `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`: DrawMesh sonar map, no secondary camera.

Verification:
- PASS: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false`.
- BLOCKED: full `Assembly-CSharp.csproj` build by non-UI/core errors in `VoxelDeltaProcessor.cs`.
- BLOCKED: `Hecton8.Core.csproj` by unrelated `GlobalSignals.cs` missing signal types and `ConstructionManager.cs` origin-shift interface mismatch.
