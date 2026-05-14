# CONTEXTUAL_UX_PROMPTER Status

Prompt ID: CONTEXTUAL_UX_PROMPTER
Agent Identity: UX_ENGINEER
Domain: ECHELON 8 PRESENTATION & UX
Task Count: 15
Status: PENDING VERIFICATION

## Mandates Read
- UI_Diegetic_Physical_Interfaces.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- CTRL_Device_Abstraction_Haptics.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Phase 1: Purge & Isolation
- [x] 1. SINGLETON ERADICATION | Justification: Removed `DiegeticTooltipSystem.ActiveRuntimeInstance`; `RepairTool` resolves the active tooltip through `GlobalRegistry.Renderables` instead of static owner state. | Alternatives Rejected: `TooltipManager.Instance`, `ActiveRuntimeInstance`, scene-wide `FindObjectOfType`. | Estimate: 3-8 us only when diagnostics request a tooltip; normal interact prompt hot path 0 us for singleton lookup.
- [x] 2. SIGNAL MIGRATION | Justification: Added unmanaged `PlayerLookTargetSignal` and published it from the existing player look raycast; tooltip consumes `SignalBus<PlayerLookTargetSignal>` instead of `InteractionEvents`. | Alternatives Rejected: Managed hover listener sidecar, direct `PlayerInteraction` reference, polling collider state from UI. | Estimate: 8-18 us per raycast transition/update; no per-frame managed target dispatch.
- [x] 3. ASMDEF ISOLATION | Justification: Preserved `Hecton8.UI.Diegetic -> Hecton8.UI.Diegetic.Contracts` isolation and added diegetic glyph contract constants under Contracts; runtime component stays scene-compatible in the existing core assembly. | Alternatives Rejected: Moving the MonoBehaviour namespace/assembly, which would break serialized scene references. | Estimate: 0 us runtime.

## Phase 2: Spatial UI Draw
- [x] 4. GLYPH RESOLVER | Justification: Tooltip queries `GlobalRegistry.InputDeterminism.GetState().CurrentInputSchemeHash`; Steam Deck maps Interact to direct TMP sprite index 14 (`pad_west` / X glyph). | Alternatives Rejected: `InputManager.TryGetPreferredBindingPath`, string sprite-name lookup, dictionary lookup in the draw path. | Estimate: 1-3 us on scheme check; 0 dictionary allocations.
- [x] 5. HOVER MATH | Justification: Player look raycast converts the hit anchor to `AbsoluteUniversePosition`; renderer resolves `AUP + float3(0, 0.5f, 0)` back to runtime space. | Alternatives Rejected: Transform-following UI anchors, screen-space projection, stale world-position-only prompts. | Estimate: 4-10 us per signal consume and render-anchor resolve.
- [x] 6. BRG TEXT RENDERING | Justification: Replaced `Graphics.DrawMeshInstanced`/MPB arrays with `Graphics.DrawMeshInstancedIndirect` using persistent compute buffers and camera-facing quad matrices. | Alternatives Rejected: Canvas, per-object TMP text, `DrawMeshInstanced` property-block arrays. | Estimate: 18-45 us for one icon plus short text on i3/MX350; GPU handles quads.
- [x] 7. TMP SPRITE ATLAS | Justification: Shader binds the TMP atlas texture and reads per-instance UV rects from `_TooltipInstances`; icon index is selected before upload by array index, not name lookup. | Alternatives Rejected: TMP rich-text sprite tags, sprite-name scans, separate Canvas icon. | Estimate: 2-5 us CPU for direct index/UV setup on prompt rebuild.

## Phase 3: Formatting & VR
- [x] 8. ZERO-GC SPAN | Justification: Signal carries a prompt hash; bounded `PlayerLookTargetPromptCache` copies into fixed `char[64]`; `"OPEN HATCH"` fallback is preallocated and optional world-space TMP sink uses `SetCharArray`. | Alternatives Rejected: `TMP_Text.text`, string interpolation, Canvas label, managed prompt object in the signal lane. | Estimate: 2-6 us on signal; 0 per-frame text allocations.
- [x] 9. VR DEPTH OFFSET | Justification: XRTouch scheme pushes the resolved anchor 0.1m toward the camera before drawing to reduce stereo-depth clipping. | Alternatives Rejected: ZTest disabled overlay, screen-space VR panel, object surface z-fighting. | Estimate: 1-2 us when XR scheme is active.
- [x] 10. FADE IN/OUT | Justification: Middle+ tiers use 0.2s alpha dither fade in shader; Low tier snaps to avoid dither cost. Target-loss keeps glyph payload alive until alpha reaches zero. | Alternatives Rejected: Animator, coroutine, CanvasGroup alpha, clearing geometry immediately. | Estimate: 3-7 us CPU; shader dither is a cheap visual fake.

## Phase 4: Safety & LOD
- [x] 11. AUP SHIFT SAFETY | Justification: Tooltip consumes `AupShiftSignal`; cached runtime anchors are shifted and AUP is resolved through current floating-origin offset every render. | Alternatives Rejected: stale `Transform.position`, delayed re-query, screen projection. | Estimate: 1-3 us per shift packet.
- [x] 12. MATH LOD | Justification: `GlobalRegistry.ScalabilityTierProfileByte == 0` snaps alpha and disables shader dither; higher tiers keep 0.2s dither fade. | Alternatives Rejected: one-size fade, Animator quality tiers, GPU dither on low silicon. | Estimate: Low saves 3-7 us CPU/GPU equivalent versus fade/dither path.
- [x] 13. EXECUTION PHASE | Justification: Target resolution now runs in `ILateFrameTickable.LateFrameTick` via `GlobalRegistry.TryRegisterLateFrameTickable(..., PriorityLayer.UI)` after main simulation lanes and before `GlobalSignals.ClearPostSimulationSnapshots`; drawing runs through `IRenderable.Render` registered in `GlobalRegistry.Renderables`. | Alternatives Rejected: `Update`, early UI `IUpdatable`, direct render from signal producer, Canvas rebuild phase. | Estimate: No native Unity message dispatch; work stays in project dispatcher VISUAL_SYNC lanes.
- [x] 14. ZERO-GC | Justification: Persistent arrays, `ComputeBuffer`s, `NativeArray` black box, direct ASCII glyph table, direct sprite index table, and prompt hash cache; no per-frame string/dictionary lookup in render. | Alternatives Rejected: TMP rich-text, binding strings, sprite name lookup, per-frame allocations, managed prompt object in signal. | Estimate: 0 managed allocations in prompt render path; measured profiler proof unavailable.
- [x] 15. OMEGA COMPILE CHECK | Justification: Static shader orientation verified: quad winding front faces `-Z`; direct billboard matrix writes local `+Z` to camera forward so local `-Z` faces the camera; filtered `dotnet build` shows no touched-file errors. | Alternatives Rejected: assuming old shader orientation, ignoring compile filters. | Estimate: 0 us runtime; verification-only.

## Iteration Log
- Loop 0: Prompt extracted. Domain and mandates identified. No code touched.
- Loop 1: Implemented tasks 1-5. Prompt re-extracted after task 3. Compile attempt: `dotnet build Hecton8.Core.csproj` is blocked by pre-existing project reference/type failures; filtered output showed no errors in touched files. Unity MCP compile unavailable at `127.0.0.1:8088`.
- Loop 2: Implemented tasks 6-10. Prompt re-extracted around tasks 6 and 9. Re-read tooltip render/fade code and fixed premature geometry clearing on fade-out. Filtered `dotnet build` output again: no touched-file errors surfaced, while full project remains blocked by unrelated references.
- Loop 3: Implemented tasks 11-15. Added explicit AUP shift cache adjustment, Low tier dither bypass, `IRenderable` draw phase, zero-GC glyph index buffers, and static quad orientation proof.
- Loop 4: Re-read the tooltip file for forbidden Canvas/singleton/Update/coroutine/SetActive patterns. Hits are comments or the explicit `TextMeshProUGUI` rejection guard only.
- Loop 5: Re-read shader and indirect draw code after converting atlas selection to an integer glyph index plus `_TooltipUvRects` buffer. Filtered compile output still has no touched-file errors; Unity MCP compile remains unreachable.
- Loop 6: OMEGA polish pass. Re-read assignment, re-read touched files, removed the prompt-cache namespace collision, preserved hash-only sidecar storage under `Hecton8.Core.PlayerLookTargetPromptCache`, and re-ran filtered compile. Result: no touched-file errors in `GlobalSignals`, `PlayerLookTargetPromptCache`, `PlayerInteraction`, `DiegeticTooltipSystem`, `RepairTool`, or tooltip shader references.
- Loop 7: Patient recheck pass. Re-read tooltip, interaction, signal, shader, and cache code. Replaced direct `promptHash & 63` prompt-cache placement with bounded lookup/free-slot/rollover storage to avoid unrelated prompts evicting each other. Re-ran touched-file build filter; no touched-file errors emitted.
- Loop 8: Render hot-path hardening. Split icon/text indirect draws into separate instance and args buffers to avoid GPU-side buffer overwrite hazards, skipped blank quads for spaces, dirty-gated material texture/buffer/SDF/dither state, replaced shader hash dither with a 4x4 Bayer LUT, clamped glyph-index reads to 0-127, and removed shader `round()`/division notation from the glyph path.
- Loop 9: CPU matrix polish. Replaced per-glyph `Quaternion.LookRotation` + `Matrix4x4.TRS` with direct billboard matrix column writes from camera right/up/forward. Re-ran quiet compile after the shared-workspace `-1` build anomaly; result `DOTNET_EXIT=0`.
- Loop 10: Execution-phase correction. Active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `CONTEXTUAL_UX_PROMPTER`, so the persisted status/rationale are the durable assignment record. Moved signal consume/fade resolve from UI `IUpdatable.Tick` to `ILateFrameTickable.LateFrameTick`, reused `SystemDispatcher.CurrentFrameDeltaTime`, and routed registration through `GlobalRegistry.TryRegisterLateFrameTickable`. Also wired tooltip scheme/glyph constants to `Hecton8.UI.Diegetic.Contracts`.
- Loop 11: SRP camera gate. Re-read render dispatcher behavior and added `ResolveRenderCamera()` so auxiliary camera passes do not queue duplicate tooltip draws for the interaction camera. First compile attempt hit transient `CS0006` missing `Temp/bin/Debug` metadata DLLs; immediate retry after metadata repopulated returned `DOTNET_EXIT=0`.

## Verification Notes
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary | Select-String ...`: no output for touched-file filter after final cache collision fix.
- Re-run after Loop 7 cache upgrade used the same filter and emitted no touched-file matches.
- Captured re-run after Loop 8 returned `DOTNET_EXIT=0` and no touched-file filter matches.
- Quiet re-run after Loop 9 returned `DOTNET_EXIT=0` and no touched-file compiler matches.
- Quiet re-run after Loop 10 returned `DOTNET_EXIT=0`.
- Post camera-gate compile first returned `DOTNET_EXIT=1` with `CS0006` missing Unity-generated metadata DLLs in `Temp/bin/Debug`; a targeted file check showed the metadata was repopulated, and immediate retry returned `DOTNET_EXIT=0`.
- Loop 10 static scan on touched tooltip/cache/interaction files found no `foreach`, `string.Format`, `.ToString(`, interpolated strings, managed collection construction, LINQ markers, exact sqrt/normalize calls, tooltip `TryRegisterUpdatable(..., PriorityLayer.UI)`, or tooltip `public void Tick(`. The remaining `public void Tick(float deltaTime)` match is `PlayerInteraction`, the existing producer lane.
- `git diff --check` on the tooltip renderer, shader, and prompt cache passed with only repository CRLF warnings.
- Static scan on touched C# files returned no matches for `foreach`, `string.Format`, `.ToString(`, interpolated strings, `new List`, `new Dictionary`, LINQ markers, `Mathf.Sqrt`, `math.sqrt`, or `math.normalize`.
- Broad unfiltered `dotnet build Hecton8.Core.csproj` did not complete within the tool timeout in the current dirty multi-agent workspace; stale child processes from that verification run were stopped only when command lines proved they belonged to this `Hecton8.Core.csproj` build.
- Unity MCP script refresh failed: HTTP transport to `http://127.0.0.1:8088/mcp` was unreachable. Editor console verification is therefore pending.
- Later Unity MCP placeholder calls were unavailable from the current tool surface, so no Editor refresh/console read was possible.
- Status remains `PENDING VERIFICATION` as required by the extracted prompt.
