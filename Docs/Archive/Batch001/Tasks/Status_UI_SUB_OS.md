# Status_UI_SUB_OS

Agent: UI_SUB_OS
Role: VEHICLE_ENGINEER
Domain: HABITAT & VEHICLES / SUBMARINE OS HUD
Batch Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Anti-Amnesia
- [x] Prompt extracted with CLI from CURRENT_BATCH.md | DOD: regex extracted only `<AGENT_PROMPT id="UI_SUB_OS">`; neighboring prompts ignored | Alternative rejected: MCP/basic read | Estimate: 1200 us.
- [x] Domain checked | DOD: read `Docs/Actual Domains of Project.txt`; mapped to Echelon 6 vehicle systems plus Echelon 8 presentation surface | Alternative rejected: broad UI edits outside submarine OS | Estimate: 900 us.
- [x] Relevant mandates loaded | DOD: loaded UI zero-GC, diegetic UI, stencil, URP hot path, zero-GC, AUP, cinematic cheat, audio SPSC, telemetry, frame-time mandates | Alternative rejected: coding from memory | Estimate: 4200 us.
- [x] Existing HUD/render code audited | DOD: inspected `HectonSubmarineOS`, `HectonSubmarineOsDisplay`, `SubmarineSonarHoloMapRenderer`, and stencil shaders | Alternative rejected: replacing systems wholesale | Estimate: 7800 us.
- [x] Prompt re-extracted after task batch | DOD: CLI extraction from CURRENT_BATCH.md after task 20 edits | Alternative rejected: stale prompt memory | Estimate: 1100 us.

## Task Checklist
- [x] 1. UI overdraw elimination stencil mask | DOD: cockpit glass shader writes stencil with `ColorMask 0`; monitor and sonar shaders require stencil equal and use `Blend One Zero` | Alternative rejected: transparent stacked canvas/hologram backgrounds | Estimate: 18 us/frame saved on MX350 fill path.
- [x] 2. 3D sonar holo-map via DrawMesh | DOD: `SubmarineSonarHoloMapRenderer` samples `VoxelDynamicNavGridRuntime` height/nav data and renders by `Graphics.DrawMesh` | Alternative rejected: secondary cameras and raycast sonar | Estimate: 70 us/frame saved when visible.
- [x] 3. Math LOD for sonar | DOD: Low uses 8 cells at 10 Hz with interpolation off; High/Ultra use denser grids and interpolation | Alternative rejected: fixed high-frequency hologram updates | Estimate: 95 us/update saved on Low.
- [x] 4. Off-screen UI culling | DOD: camera dot-product gates sampling, mesh upload, and late-frame registration | Alternative rejected: always-updating hidden monitors | Estimate: 60 us/frame saved when looking away.
- [x] 5. [BLOCKED BY DEPENDENCY] Blip occlusion fake | DOD: verified no public EcosystemDirector wall-distance/occlusion contract exists on `IEcosystemDirectorService` | Alternative rejected: physics raycasts or inventing cross-domain API | Estimate: 0 us/frame; integrator needs distance field contract.
- [x] 6. Radar wave sweep | DOD: sonar ping sets `_HectonSubOsSonarSweep`; shader uses triangle pulse and cutout gate | Alternative rejected: particle/raycast sweep geometry | Estimate: 35 us/frame saved.
- [x] 7. Vocal Warning System queue | DOD: VWS now routes one-based event IDs through `IAudioService.QueueAudioEvent(in AudioEvent)` | Alternative rejected: direct `PlayStatic2D` clip playback from OS hot path | Estimate: 12 us/event plus zero managed clip dispatch.
- [x] 8. VWS bitmask tzcnt scan | DOD: active warning flags processed with `math.tzcnt` and `activeMask &= activeMask - 1u` | Alternative rejected: seven sequential boolean checks | Estimate: 4 us/warning scan saved under many flags.
- [x] 9. Engine heat 1D texture bar | DOD: dynamic `HEAT xx%` string removed; engine heat is an opaque fixed-width bar updated by rect width | Alternative rejected: TMP percentage text churn | Estimate: 8 us/update saved.
- [x] 10. Auto-level stabilizer Awaitable | DOD: `SubmarineStationKeepingController.AutoLevelWhenControlsReleasedAsync` arms pitch/roll correction and awaits without coroutine | Alternative rejected: coroutine station keeping | Estimate: 0 GC/frame.
- [x] 11. Speedometer dominant-axis knots | DOD: hull speed uses dominant-axis approximation and converts to knots; no `math.length` | Alternative rejected: sqrt magnitude | Estimate: 5 us/sample saved.
- [x] 12. Interior lighting modes global material property | DOD: sets `_SubInteriorLightingState` for normal, power save, emergency | Alternative rejected: per-material string/property updates | Estimate: 20 us/render pass saved.
- [x] 13. Power grid heatmap | DOD: listens to `PowerGridTelemetryEvents`, applies brownout/emission state budgeted across modules | Alternative rejected: per-frame module search | Estimate: 80 us/frame avoided during brownout.
- [x] 14. [BLOCKED BY DEPENDENCY] Distance to landmark approximate AUP | DOD: `IQuestSystem` exposes quest flags/IDs only; no objective or landmark AUP target is available | Alternative rejected: guessing world landmark from unrelated systems | Estimate: 0 us/frame; integrator needs quest target AUP contract.
- [x] 15. Internal atmosphere gauge SetCharArray | DOD: O2, CO2, pressure values are staged in char buffers using `Span<char>`/`TryFormat` and `SetCharArray()` | Alternative rejected: TMP `.text` and interpolation | Estimate: 20 us/refresh saved.
- [x] 16. Zero-alloc Canvas allocation note | DOD: explicit note: Unity UGUI Canvas can allocate/rebuild internally and cannot be fully bypassed here; code avoids strings and `ForceUpdate` but Canvas internals remain engine-owned | Alternative rejected: false zero-allocation claim | Estimate: unknown engine-side.
- [x] 17. Cache TMP_Text | DOD: TMP refs cached after cold setup; no runtime `GetComponent<T>()` in update paths | Alternative rejected: runtime component lookup | Estimate: 3 us/refresh saved.
- [x] 18. No Canvas.ForceUpdate | DOD: source scan found no `Canvas.ForceUpdateCanvases()` in UI_SUB_OS files | Alternative rejected: forced layout rebuild | Estimate: avoids layout spike.
- [x] 19. Low-power CRT flicker shader | DOD: monitor and sonar stencil shaders multiply emission/color by cheap low-power noise below 15 percent power | Alternative rejected: C# per-element flicker updates | Estimate: 25 us/frame CPU saved.
- [x] 20. 32-byte UI/Burst structs | DOD: `HectonSubmarineOsSnapshot` and `SubmarineOsEventPayload` are 64 bytes, two 32-byte lanes | Alternative rejected: unpadded event payloads | Estimate: cache-aligned event reads.

## Verification
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:BuildProjectReferences=false` - PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` - BLOCKED before UI verification by non-UI/core errors in `VoxelDeltaProcessor.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` - BLOCKED by unrelated core errors in `GlobalSignals.cs` and `ConstructionManager.cs`, plus one duplicate-using warning.

## Iteration Log
- Loop 1: Tasks 1-5 audited against stencil/DrawMesh/LOD/culling; task 5 blocked by missing EcosystemDirector distance contract.
- Loop 2: Tasks 6-10 audited and patched; VWS moved to queued audio events; auto-level Awaitable verified in station keeping controller.
- Loop 3: Tasks 11-15 audited; heat text replaced with 1D bar; CO2/pressure path uses char buffers.
- Loop 4: Tasks 16-20 audited; Canvas allocation caveat recorded; low-power flicker added to shaders; UI event structs padded to 64 bytes.
- Loop 5: Omega polish scan; no unconditional sqrt/normalize in touched C# paths; stencil shaders remain opaque/cutout; narrow assembly compile passed.
