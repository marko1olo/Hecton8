# Status: DIEGETIC_TOOL_DISPLAY

Agent: UX_ENGINEER
Domain: ECHELON 8 - PRESENTATION & UX (Interaction and Perception)
Prompt: Zero-GC Tool Screens
Task Count: 19 (batch declares 19 titanium tasks; numbered list contains 18 plus recursive re-verification)
Status: PENDING VERIFICATION

## Hygiene
- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex by id `DIEGETIC_TOOL_DISPLAY`.
- [x] Prompt re-extracted on post-resume pass with flexible CLI regex that accepts `role` and `chat_name` attributes on the XML tag.
- [x] Existing status/rationale files checked: both missing, no stale-batch content detected.
- [x] Relevant mandates identified: `UI_Diegetic_Physical_Interfaces`, `UI_Data_Streaming_ZeroGC_Optimization`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `CORE_Tools_Equipment_Interaction_Raycast_Heat`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `REND_VR_Stencil_Masking`.
- [x] Prompt re-extracted after task group execution with PowerShell regex over `Docs/Tasks/CURRENT_BATCH.md`; neighboring XML prompts ignored.
- [x] Domain checked against `Docs/Actual Domains of Project.txt`; work stayed inside Presentation/UX except required cross-domain signal publication through `GlobalSignals` and equipment state mirrors.

## Tasks
- [x] 1. SINGLETON ERADICATION: `rg` found no `WeaponUIManager` / `WeaponUIManager.Instance` in live `Assets/_Project/Scripts`. DOD: static deletion proof. Rejected: compatibility singleton stub. Estimate: 0 us hot path.
- [x] 2. SIGNAL MIGRATION: Added 32-byte `ToolStateChangedSignal` queue/latest lane in `GlobalSignals`, published from `ModularEquipmentEngine`, consumed by `ToolDiegeticDisplayController` and visor fallback. DOD: NativeQueue + latest-sequence signal path. Rejected: direct UI polling of tool owners. Estimate: 3-8 us for <=16 state packets.
- [BLOCKED BY CONTRACT SPLIT] 3. ASMDEF ISOLATION: Created `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef`. Full Contracts-only isolation is blocked because `GlobalSignals`, `ZeroGCFormatter`, and `RenderTexturePool` currently live in `Hecton8.Core`, not `Hecton8.Core.Contracts`. Rejected: duplicating formatter/signal structs in UI assembly. Estimate: 0 us hot path; build graph cleanup pending contract migration.
- [BLOCKED BY HUD CANVAS OWNER] 4. DEAD CODE HUNT: Removed `CanvasScaler` from `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`; MCP hierarchy now shows root components `RectTransform`, `Canvas`, `SuitHUDV4CanvasOverlay`, `HectonUIScaler`. Removing the root `Canvas` is blocked because existing visor projection still owns a UGUI render root. Rejected: raw YAML prefab surgery and deleting Canvas without a replacement presenter. Estimate: avoids CanvasScaler relayout spikes; exact us pending profiler.
- [x] 5. THE TOOL CAMERA: Added `ToolUI` project layer and configured `ToolDiegeticDisplayController` camera as orthographic, clear-black, ToolUI-only fallback mask, 256x256 target. DOD: MCP layer proof plus code read. Rejected: screen-space overlay camera. Estimate: active RT camera only, target <100 us GPU/CPU budget on MX350 when visible.
- [x] 6. MATERIAL BINDING: Controller binds the shared RT to `_ToolScreenTex`, `_EmissionMap`, `_BaseMap`, and `_MainTex` through one persistent `MaterialPropertyBlock`. DOD: no material clone or per-frame asset mutation. Rejected: `renderer.material` clone. Estimate: <5 us only on state/texture changes.
- [x] 7. RENDERGRAPH OPTIMIZATION: Camera depth is forced early and `enabled` is true only for a dirty visible/equipped render frame; holstered/low-tier releases target texture. DOD: code-read gate in `UpdateCameraActivation`. Rejected: empty-frame rendering. Estimate: 0 us on holster, one local camera pass only when dirty.
- [x] 8. SPAN FORMATTING: Ammo, heat, distance, and battery use `ZeroGCFormatter.FastIntToChars` over preallocated `char[]`. DOD: static scan of controller formatting path. Rejected: interpolated strings / `ToString()`. Estimate: <10 us for two labels.
- [x] 9. TMP UPDATE: Tool labels use `TMP_Text.SetCharArray()` only. DOD: static scan found no `SetText`, `new string`, `ToString()`, or `string.Format` in touched tool-display scope. Rejected: TMP string APIs. Estimate: 0 B GC.
- [x] 10. BAR GRAPHS: `Hecton_ToolScreenDiegetic.shader` reads `_ToolHeat01` and draws heat fill in shader. DOD: static scan found no `Image.fillAmount` in `Assets/_Project/Scripts/UI/Tools` or the tool shader. Rejected: UGUI Image fills. Estimate: removes Canvas rebuild lane.
- [x] 11. CRITICAL FLASH: Shader inverts color when `_ToolHeat01 >= 0.9` and `_ToolCriticalFlash01` pulses. DOD: shader code read. Rejected: CPU color animation. Estimate: sub-pixel ALU, no CPU allocation.
- [x] 12. AUP SHIFT SAFETY: Tool UI renders from local camera onto local model material and uses no world-origin-dependent position math. DOD: code read. Rejected: world-space floating labels. Estimate: no floating-origin resync work.
- [x] 13. MATH LOD: Low tier or signal flag disables RT camera, releases RT, binds static emissive fallback, and routes battery/heat/ammo/distance scalars to `VisorHUDController`. DOD: controller + visor code read. Rejected: same RT cost on MX350. Estimate: saves one camera pass and ~0.13 MB color RT plus depth while preserving telemetry.
- [x] 14. ZERO-GC: Hot label path uses cold-allocated `char[96]`, no string APIs, no LINQ, no enumerator allocation. DOD: static scan. Rejected: `string.Format`, interpolation, `StringBuilder` hot path. Estimate: 0 B GC per update.
- [x] 15. VRAM BUDGET: Active display rents one 256x256 RT from `GlobalRegistry.RenderTexturePool`; direct fallback `new RenderTexture` allocation was removed. If the pool is absent, the screen degrades to static emissive and retries the pool every 2 seconds. DOD: static scan found no `new RenderTexture` in the tool controller. Rejected: emergency local RT allocation and per-tool permanent RTs. Estimate: RGB565 color ~128 KB plus D16 depth only for active tool.
- [x] 16. BLACKBOX DUMP: N/A. This is presentation UI, not Physics/Voxel/AI critical state; failure mode is disabled camera/static fallback, not simulation divergence. DOD: rationale recorded. Rejected: fake telemetry buffer for non-critical UI. Estimate: saves native buffer churn.
- [x] 17. VR COUPLING: Controller exposes `_minimumReadableScreenHeightMeters` and clamps orthographic size for arm-length authoring; final OpenXR visual capture is still pending. DOD: code-read configuration. Rejected: corner HUD as primary readout. Estimate: no extra CPU; authoring constraint only.
- [BLOCKED BY LEGACY STRING FORMAT] 18. OMEGA COMPILE CHECK: Touched tool-display scope is clean; project-wide `rg` still finds legacy `string.Format` in `ScannerTool.cs`, `LocalizationManager.cs`, `SaveSlotUI.cs`, and `HectonDiscoveryManager.cs`. Rejected: editing scanner/localization outside assigned UX tool-display domain. Estimate: no new hot-path allocation introduced.
- [x] 19. Recursive re-verification: Prompt re-read from XML tag; holster path publishes `forceHolstered`, controller treats non-equipped signal as disabled, turns camera off, clears target texture, and releases RT. Status remains `PENDING VERIFICATION`.

## Compile Gate
- Unity refresh/compile completed, but the project is blocked by unrelated errors:
  - `Assets/_Project/Scripts/HectonFluidEngine.cs`: duplicate `EnsureFluidAdvectionState`, `IsFluidAdvectionReady`, `UploadAdvectedBubble`, `ResolveSpawnJitter`.
  - `Assets/_Project/Scripts/Hecton8.Core.asmdef`: duplicate reference `Hecton8.Vehicles.Physics.Contracts`.
  - MCP validation regex timeout in package tooling.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q` was re-run after the hardening pass and failed with 153 unrelated cross-assembly errors. Leading blockers remain missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `BinaryBlittableSafe`, `MacroSwarm`, tether/acoustic contracts, and macro database/job admission classes. No local tool-display compiler error is proven by the available output.
- Post-resume `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs` returned 0 errors / 0 warnings.
- Post-resume Unity script refresh timed out after 60 seconds waiting for editor readiness; immediate console read returned 0 error/warning entries. Status remains `PENDING VERIFICATION`, not verified.
- Follow-up `validate_script` after the invisible-RT residency patch could not run because Unity MCP returned `no_unity_session` twice. Static scan stayed clean; full validation remains pending.
- Latest `validate_script` after the status/fault display patch returned 0 errors / 0 warnings for `ToolDiegeticDisplayController.cs`.
- Unity script refresh returned to idle, then console read reported 8 current blockers outside the tool-display source: duplicate `H8MacroDatabaseService.ReadRootNodeOffsetIfOpen`, missing `ElapsedMillisecondsSince` in `GlobalDataVault`, shader errors in `Hecton_OrbitalDropReentryPlasma.shader` and `HectonVisorUberPost.shader`, a Burst follow-on failing to resolve `Hecton8.UI.Tools` while assemblies are not fully emitted, and a SourceAssetDB timestamp mismatch. Status remains `PENDING VERIFICATION`.
- Latest active-signal gating patch validation: `validate_script` returned 0 errors / 0 warnings for `ModularEquipmentEngine.cs` and `ToolDiegeticDisplayController.cs`; previous same-pass validation returned 0 errors / 0 warnings for `VisorHUDController.cs`. `GlobalSignals.cs` validation still times out in the MCP regex validator because the file is large.
- Unity script refresh returned to idle after the active-signal gating patch. Console read is still blocked outside this task, now led by `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` missing memory/defrag symbols and assembly references. No touched tool-display file is named in the latest console error set.
- Latest tiered signal-delta patch validation: `validate_script` returned 0 errors / 0 warnings for `ModularEquipmentEngine.cs` and `ToolDiegeticDisplayController.cs`.
- Unity script refresh returned to idle after the tiered signal-delta patch. Console now reports 2 blockers outside this task: `HectonUnderwaterVisuals.cs(7413)` syntax error and a Burst follow-on failing to resolve `Hecton8.Core` referenced by `Hecton8.Vehicles.VFX`. No touched tool-display file is named.
- Latest holster-transition patch validation: `validate_script` returned 0 errors / 0 warnings for `ModularEquipmentEngine.cs`, `ToolDiegeticDisplayController.cs`, and `VisorHUDController.cs`.
- Unity script refresh returned to idle after the holster-transition patch. Console now reports blockers outside this task: duplicate methods in `HectonUnderwaterVisuals.cs` and a Burst follow-on failing to resolve `Hecton8.Prologue.Space`. No touched tool-display file is named.

## Strict Iteration Evidence
- Loop 1: scanned singleton and prompt boundaries; no `WeaponUIManager` found.
- Loop 2: read signal publisher/consumer paths; holster state emits a non-equipped signal.
- Loop 3: read camera/RT binding path; found missing `ToolUI` layer, then added layer slot 23.
- Loop 4: inspected prefab hierarchy; removed only `CanvasScaler`, kept required root `Canvas`.
- Loop 5: scanned zero-GC and shader bar/flash paths; touched tool-display scope clean, legacy project-wide `string.Format` remains blocked.
- Loop 6: re-read tool controller/shader after completion; removed direct RT allocation fallback, cached RT pool ownership, added pool-missing retry cooldown, and added tiered shader overkill gated by scalability tier.
- Loop 7: re-read controller residency behavior; added 0.75s invisible-release hysteresis so equipped-but-not-renderable screens return their pooled RT instead of retaining VRAM indefinitely.
- Loop 8: re-read status-mask and tool-type data flow; consumed `_statusMask` for zero-GC status tokens plus shader fault intensity, consumed `_toolTypeId` for high-tier hue variation, and fixed readable-size camera clamping.
- Loop 9: re-read producer tick flow; found every tracked tool published each tick, so later unequipped slots could overwrite the active latest signal. Patched producer gating to publish only equipped/current state or explicit force-holster/failure packets.
- Loop 10: re-read active-packet cadence; added tier-aware delta gating so unchanged active packets stop before `GlobalSignals.Publish`. Low/MX350 requires 2 percent scalar delta, Mid 1 percent, High 0.5 percent, Ultra 0.25 percent.
- Loop 11: re-read equip/unequip lifecycle after the delta gate; found a stale-equipped risk if a tool becomes unequipped without immediate unregister. Added `_lastPublishedEquippedMask` and one-shot disabled transition packets.

## Omega Polish
- [x] Extracted `<POLISH_MANDATE id="OMEGA_POLISH">` only after tasks 1-18 were checked or blocked.
- [x] Anti-bloat scan over touched display scope found no managed `foreach`, `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, `.normalized`, `string.Format`, `$"..."`, or `.ToString()`.
- [x] Division audit: new battery normalization uses `math.rcp(capacity)` in `ModularEquipmentEngine` and `VisorHUDController`; legacy division in `ModularEquipmentEngine.ReadBatteryNormalized` was not touched because it predates this task.
- [x] Prefab edit audit found MCP zeroed the root RectTransform during `CanvasScaler` removal; root scale, anchors, size, and pivot were restored manually while keeping `CanvasScaler` removed.
- [x] `dotnet build Hecton8.Core.csproj --no-restore -v:q` executed per mandate after hardening and failed with 153 unrelated cross-assembly errors. Leading blockers: missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, missing `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `BinaryBlittableSafe`, `MacroSwarm`, tether/acoustic contracts, and macro database/job admission classes.
- [x] Post-resume anti-bloat scan over `ToolDiegeticDisplayController.cs` and `Hecton_ToolScreenDiegetic.shader` found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- [x] Shader now keeps material properties inside `UnityPerMaterial` CBUFFER and uses `_ToolVisualOverkill01` for Mid/High/Ultra visual spend while Low/MX350 stays static fallback.
- [x] Invisible residency audit: when equipped but not visible/renderable, the controller disables the camera immediately and releases the pooled RT after 0.75s; holstered and low-tier paths still release immediately.
- [x] Status/fault audit: `_statusMask` now renders as `OK/PWR/HOT/BRK/DPT/OFF` through `SetCharArray()` and drives `_ToolFault01`; `_toolTypeId` drives `_ToolTypeHue01` only in the overkill shader path.
- [x] Latest static anti-bloat scan over touched tool-display scope found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- [x] Active-signal audit: `ModularEquipmentEngine.PublishToolStateChanged` now skips non-equipped non-force updates, preventing inactive tracked tools from overwriting the latest active signal. Force-holster packets explicitly set `Disabled` and clear `Active`.
- [x] Active-packet cadence audit: unchanged active packets are dropped via tier-aware scalar thresholds before queue/latest publication; force-holster packets bypass the drop gate.
- [x] Holster-transition audit: previously published equipped slots now emit one disabled packet if `owner.IsEquipped` becomes false without a force-holster call, then clear their slot bit to avoid repeat spam.
