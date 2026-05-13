# LOG: DIEGETIC_TOOL_DISPLAY

## 2026-05-13 - Zero-GC Tool Screens
What was wrong:
- Tool telemetry was architecturally positioned as floating HUD data instead of a physical tool-screen feed.
- No `ToolStateChangedSignal` lane existed for decoupled tool UI consumption.
- `ToolUI` layer was missing, so any local tool camera would fall back to generic UI filtering.
- Suit HUD prefab still carried `CanvasScaler`, which reintroduces layout/rebuild cost.
- Legacy project-wide `string.Format` remains in scanner/localization/save UI files outside this prompt.

What was done:
- Added `ToolStateChangedSignal` as a 32-byte fixed payload in `GlobalSignals` with queue, latest snapshot, writer, publish, dequeue, and clear paths.
- Published tool state from `ModularEquipmentEngine`, including equipped/visible/low-tier flags and force-holstered shutdown packets.
- Added `ToolDiegeticDisplayController` under `Hecton8.UI.Tools`: local orthographic camera, shared 256 RT rental, emissive screen binding, low-tier static texture fallback, and zero-GC TMP char-buffer writes.
- Added `Hecton_ToolScreenDiegetic.shader`: heat/battery shader bars and critical heat inversion.
- Added `Hecton8.UI.Tools.asmdef`; full Contracts-only isolation is blocked because signal bus/formatter/RT pool still live in `Hecton8.Core`.
- Routed low-tier heat/ammo/distance/battery data into `VisorHUDController` material properties.
- Added `ToolUI` layer at slot 23.
- Removed `CanvasScaler` from `Suit_HUD_Canvas.prefab`; kept root `Canvas` because the current visor presenter still owns a UGUI projection root.

Cinematic cheats used:
- 256x256 local RT instead of full HUD rendering.
- Shader bars from global floats instead of `Image.fillAmount`.
- Triangle-wave critical flash instead of CPU animation.
- Static emissive fallback on MX350/Low tier.
- Latest-signal snapshot instead of scene/UI graph traversal.
- `math.rcp(capacity)` normalization in new battery paths.

Exact microseconds saved:
- Singleton lookup purge: 0 us hot path because no live `WeaponUIManager.Instance` reference existed.
- Signal packet publish: estimated 3-8 us for <=16 tool packets; replaces scene/owner polling.
- Material binding: estimated <5 us only on texture/state changes; no material clone.
- TMP formatting: estimated <10 us for two labels, 0 B GC by static proof.
- Holstered tool camera: 0 us camera cost when holstered; target texture cleared and RT returned.
- Low tier fallback: saves one 256x256 camera pass and approximately 128 KB RGB565 color RT plus depth residency.
- CanvasScaler removal: expected removal of scaler relayout spikes; exact profiler number blocked by current global compile errors.

Verification:
- Prompt and polish mandate extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex.
- Static scan found no `WeaponUIManager`, no hot string formatting APIs, no managed `foreach`, no sqrt/normalize in touched tool-display scope.
- Static scan found no `Image.fillAmount` in `Assets/_Project/Scripts/UI/Tools` or `Hecton_ToolScreenDiegetic.shader`.
- MCP prefab hierarchy confirmed `CanvasScaler` removed from `Suit_HUD_Canvas.prefab`; root RectTransform was restored after MCP zeroed it.
- Unity refresh/compile is blocked by unrelated `HectonFluidEngine` duplicate methods and duplicate `Hecton8.Vehicles.Physics.Contracts` asmdef reference.
- `dotnet build Hecton8.Core.csproj --no-restore` fails with 151 unrelated cross-assembly errors, led by missing fluids/scheduling/memory layout/CCD/audio/acoustic/macro types.

Status:
- PENDING VERIFICATION.

## 2026-05-13 - Holster Transition Guard Pass
What was wrong:
- The active-signal optimization could leave a stale latest packet flagged as equipped if `owner.IsEquipped` flipped false without immediate unregister/force-holster.
- That would keep visor/tool display consumers believing the tool was still active until another lifecycle path corrected it.

What was done:
- Added `_lastPublishedEquippedMask` to `ModularEquipmentEngine`.
- Equipped publishes set the slot bit.
- A later unequipped non-force call emits exactly one disabled transition packet, clears `Active`, and clears the slot bit.
- Steady unequipped slots still publish 0 packets.

Cinematic cheats used:
- The physical screen gets a deterministic shutdown packet without reintroducing inactive-slot spam.

Exact microseconds saved:
- Keeps the tiered delta gate savings: steady inactive cost remains 0 packets.
- Adds one uint bitmask and branch checks; expected CPU cost is below measurement noise.
- Prevents stale visible/equipped state that could keep the tool RT/camera path alive incorrectly.

Verification:
- `validate_script` on `Assets/_Project/Scripts/ModularEquipmentEngine.cs`: 0 errors, 0 warnings.
- `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs`: 0 errors, 0 warnings.
- `validate_script` on `Assets/_Project/Scripts/Visor/VisorHUDController.cs`: 0 errors, 0 warnings.
- Static anti-bloat scan over touched tool-display scope plus `ModularEquipmentEngine.cs` found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity script refresh reached idle; console remains blocked outside this task by duplicate `HectonUnderwaterVisuals.cs` methods and a Burst follow-on resolving `Hecton8.Prologue.Space`.

Status:
- PENDING VERIFICATION.

## 2026-05-13 - Tiered Active Signal Delta Pass
What was wrong:
- After inactive-slot gating, the equipped tool still published one `ToolStateChangedSignal` every tick even when no display-visible value changed.
- That forced latest-sequence churn and woke the diegetic controller dirty path without buying visible fidelity.

What was done:
- Added tier-aware publish deltas before `GlobalSignals.Publish`.
- Exact changes to tool hash, flags, status, ammo, or tool type still publish immediately.
- Battery, heat, and durability use scalar thresholds: Low/MX350/Unknown 0.02, Mid 0.01, High 0.005, Ultra 0.0025.
- Distance publishes on 0.5 meter change.
- Force-holster packets bypass the drop gate.

Cinematic cheats used:
- Low-end telemetry lies in coarse percent steps while the static emissive/visor fallback remains readable.
- High-end keeps finer scalar motion without increasing RT size, camera count, or canvas work.

Exact microseconds saved:
- Unchanged equipped tool steady state drops from one signal packet per tick to 0 packets.
- Expected saving is the previous 3-8 us active packet cost plus avoided controller dirty-state checks on stable frames.

Verification:
- `validate_script` on `Assets/_Project/Scripts/ModularEquipmentEngine.cs`: 0 errors, 0 warnings.
- `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs`: 0 errors, 0 warnings.
- Static anti-bloat scan over touched tool-display scope plus `ModularEquipmentEngine.cs` found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity script refresh reached idle; console remains blocked outside this task by `HectonUnderwaterVisuals.cs(7413)` syntax error and a Burst follow-on resolving `Hecton8.Core` for `Hecton8.Vehicles.VFX`.

Status:
- PENDING VERIFICATION.

## 2026-05-13 - Active Signal Gating Pass
What was wrong:
- `ModularEquipmentEngine.Tick()` refreshed mirrors for every tracked tool, and each mirror refresh published `ToolStateChangedSignal`.
- `ToolDiegeticDisplayController` consumes the latest signal. A later inactive slot could therefore overwrite the equipped tool packet and make the physical screen shut off while the active tool remained in-hand.

What was done:
- Patched `PublishToolStateChanged()` to skip normal packets when the owner is not equipped.
- Kept explicit force-holster/failure/unregister packets so shutdown still propagates.
- Force-holster packets now set `Disabled` and clear `Active` in the emitted status mask.

Cinematic cheats used:
- Latest-snapshot UI stays cheap by reducing producer noise instead of draining/filtering the whole NativeQueue in presentation.

Exact microseconds saved:
- Signal traffic drops from up to `MaxTrackedTools` packets per tick to the equipped tool plus explicit shutdown packets.
- Estimated saving is a few microseconds per inactive tracked tool per tick and prevents false 0-us camera shutdowns caused by stale inactive latest packets.

Verification:
- `validate_script` on `Assets/_Project/Scripts/ModularEquipmentEngine.cs`: 0 errors, 0 warnings.
- `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs`: 0 errors, 0 warnings.
- Previous same-pass `validate_script` on `Assets/_Project/Scripts/Visor/VisorHUDController.cs`: 0 errors, 0 warnings.
- Static anti-bloat scan over touched tool-display scope plus `ModularEquipmentEngine.cs` found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity script refresh reached idle; console remains blocked outside this task by `GlobalDataVault.cs` memory/defrag symbol errors.

Status:
- PENDING VERIFICATION.

## 2026-05-13 - Status/Fault Display Pass
What was wrong:
- The tool signal carried `StatusMask` and `ToolTypeId`, but the physical tool screen did not consume them. Fault state was invisible unless inferred indirectly from heat/battery.

What was done:
- Added deterministic status buckets over `ToolRuntimeStatusMasks`.
- Wrote compact zero-GC status tokens into the existing secondary TMP char buffer: `OK`, `PWR`, `HOT`, `BRK`, `DPT`, `OFF`.
- Added cached shader globals `_ToolFault01` and `_ToolTypeHue01`.
- Updated `Hecton_ToolScreenDiegetic.shader` to tint/pulse fault states and apply tool-type hue only through the existing Mid/High/Ultra overkill scalar.
- Wired `_minimumReadableScreenHeightMeters` into orthographic size clamping so the readability field is not dead authoring data.

Cinematic cheats used:
- Fault urgency is a shader tint/pulse from a scalar, not a CPU animation or extra UI layer.
- Tool type variation is cheap color bias on the 256 surface, not alternate materials or texture swaps.

Exact microseconds saved:
- Status text stays inside the existing `SetCharArray()` path: 0 B GC and no new TMP object.
- Fault/type rendering adds cached scalar updates only on dirty state; no material clone and no new camera pass.
- Low/MX350 bypass remains intact through fallback and overkill gates; high-end spends only a few fragment ALU instructions on the tiny surface.

Verification:
- `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs`: 0 errors, 0 warnings.
- Static anti-bloat scan over touched tool-display scope found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity script refresh reached idle, but console still reports unrelated blockers: duplicate `H8MacroDatabaseService.ReadRootNodeOffsetIfOpen`, missing `ElapsedMillisecondsSince` in `GlobalDataVault`, shader errors in orbital-drop and visor-post shaders, a Burst follow-on failing to resolve `Hecton8.UI.Tools` while assemblies are not fully emitted, and a SourceAssetDB timestamp mismatch.

Status:
- PENDING VERIFICATION.

## 2026-05-13 - Invisible Residency Pass
What was wrong:
- Equipped-but-not-renderable tool screens disabled the camera but could retain the pooled 256x256 RT indefinitely while the screen renderer stayed culled.

What was done:
- Added `InvisibleReleaseSeconds = 0.75f` and `_notRenderableSeconds` to `ToolDiegeticDisplayController`.
- Holstered, not-visible, and low-tier paths still release RT immediately.
- Renderer-cull-only non-renderability now releases after 0.75s and binds static emissive fallback, avoiding permanent residency without rent/return churn on culling flicker.

Cinematic cheats used:
- Short hysteresis preserves perceived continuity while silently dropping the live RT when the player cannot see the physical screen.

Exact microseconds saved:
- Camera cost remains 0 us when hidden because the camera is disabled immediately.
- VRAM residency drops by one 256 RGB565+D16 RT after 0.75s hidden/cull time.
- Added CPU cost is one float accumulator plus simple branches in the existing UI tick; no allocation.

Verification:
- Static scan stayed clean: no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity MCP `validate_script` could not run after this patch because the Unity session returned `no_unity_session` twice. Status remains pending.

Status:
- PENDING VERIFICATION.
- Blocked items: full Contracts-only asmdef split, root HUD Canvas deletion, project-wide legacy `string.Format`, profiler/OpenXR proof until global compile blockers clear.

## 2026-05-13 - Post-Resume Hardening Pass
What was wrong:
- The tool controller still contained a private `new RenderTexture` fallback for a missing global RT pool.
- Non-low visual scaling was under-specified; Mid/High/Ultra used the same shader cost.
- The XML prompt extraction regex was too strict for the current batch file because the tag includes `role` and `chat_name` attributes.

What was done:
- Removed direct RT construction from `ToolDiegeticDisplayController`.
- Cached the `RenderTexturePool`, tracked the owner pool for returns, and made pool-missing state degrade to static emissive with a 2-second retry cooldown.
- Added `_ToolVisualOverkill01` global tier scalar and shader-only grid/data sweep for Mid/High/Ultra. Low/MX350 remains 0 and fallback-safe.
- Re-extracted the `DIEGETIC_TOOL_DISPLAY` XML block with a flexible CLI regex that accepts tag attributes.

Cinematic cheats used:
- Missing pool no longer creates a private render target; it lies gracefully with static emissive.
- High-end overkill is a few ALU shader marks on a 256 surface, not more cameras or higher RT resolution.

Exact microseconds saved:
- Pool-missing fallback: avoids one cold private RT allocation and any hidden VRAM residency outside the pool; steady-state retry cost is one registry read every 2 seconds while visible.
- Low/MX350 visual path: unchanged at 0 RT camera pass and 0 overkill shader scalar.
- High/Ultra visual spend: CPU remains cached global-float updates only on dirty state; GPU cost is bounded to the 256x256 tool surface.

Verification:
- `validate_script` on `Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs`: 0 errors, 0 warnings.
- Static scan over touched tool display scope found no `new RenderTexture`, `string.Format`, `.ToString()`, `SetText`, `new string`, managed `foreach`, sqrt/normalize, or `Image.fillAmount`.
- Unity script refresh requested; editor did not report ready within 60 seconds. Immediate console read returned 0 error/warning entries, so this remains pending rather than verified.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q` failed with 153 unrelated cross-assembly errors led by missing fluids, scheduling, memory layout, CCD, audio propagation, tether/acoustic, macro database, and save-layout contracts.

Status:
- PENDING VERIFICATION.
