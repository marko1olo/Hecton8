# Asset Owner 17 - UI Oxygen Sprite Atlas Packet

Status: `PENDING_VERIFICATION`
Evidence class: `STATIC_DOC` + `STATIC_SOURCE` + `STATIC_IMAGE_QA` only
Write scope: this packet only
First-20 route moment: HUD oxygen readability before first exit, suit breath risk, tool use, and early inventory decisions.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `localization.md`
- `accessibility.md`
- `Docs/AssetAudit/README.md`
- `Docs/Reports/AssetSystem_20260605/UI_SPRITE_ROUTE_STATIC_TABLE_3216_20260605.md`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.md`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md`

## Evidence Boundary

This packet maps future execution. It does not prove Unity import state, SpriteAtlas packing, Addressables ownership, scene/prefab binding, Play Mode behavior, Frame Debugger state, Profiler state, GCMonitor state, or player HUD quality.

No files under `Assets/` were edited. No Unity, build, import, Play Mode, raw YAML, SpriteAtlas, Addressables, prefab, material, scene, or project-setting action is included.

## Static Route Facts

| Source | Static role | Future owner action |
|---|---|---|
| `Assets/_Project/Art/Sprites/oxygen-tank.png` | Legacy HUD mask/silhouette. Static QA says black RGB with partial alpha, and static prefab evidence points `Suit_HUD_Canvas.prefab` at this source. | Keep only as tintable mask/silhouette if a UI owner explicitly proves that role; otherwise reroute HUD oxygen icon usage away from it. It must not be treated as the detailed oxygen icon. |
| `Assets/_Project/Art/Sprites/ui/OXYGEN.png` | Detailed oxygen canister candidate. Static QA says detailed colored oxygen icon source, but static search did not find it bound in `Suit_HUD_Canvas.prefab`. | Promote only after importer readback, SpriteAtlas ownership, HUD/inventory binding, compact readability, localization/accessibility, and allocation-safe update proof are produced by the future owner. |

## Required Route Decision

Future owner must choose one route and write the decision into the edited artifact:

- `Mask route`: `oxygen-tank.png` remains a mask only. It needs explicit tint/material role, alpha silhouette proof, no-black-icon HUD capture, and an explanation for why the detailed icon is not used in that HUD slot.
- `Detailed icon route`: HUD/inventory oxygen source changes to `ui/OXYGEN.png`. It needs prefab/scene binding proof through Unity API readback, SpriteAtlas packing proof, compact readability capture, disabled/fault state capture, and no hot-path allocation evidence for repeated HUD updates.
- `Dual route`: `oxygen-tank.png` provides fill/ring/silhouette behavior while `ui/OXYGEN.png` provides detailed inventory/tool/resource iconography. It needs separate owner names, update cadence, color semantics, and atlas packing proof for both roles.

## SpriteAtlas, Import, And Readback Gates

Future owner must not rely on `.meta` text alone. Required artifacts:

- Unity API readback for `textureType`, sprite mode, alpha, sRGB, mip policy, platform compression, max size, and read/write state for both oxygen sources.
- SpriteAtlas asset path, included sprites, platform packing format, padding, tight-packing policy, and atlas residency owner.
- Addressables or scene lifetime owner if the atlas becomes a runtime dependency; include handle/release route or static scene residency route.
- Async upload budget statement tied to the global tier decision: 64 MB/1 ms low, 128 MB/2 ms middle, 256 MB/4 ms high/ultra unless measured evidence forces a different owner decision.
- Texture memory and reserved memory evidence after import/atlas work. Static source size is not residency evidence.

## HUD Readability Gates

Future owner must produce screenshots or captures for:

- `oxygen-tank.png` mask route: alpha/tint result over dark water, lit metal, fog, emergency light, and visor distortion.
- `ui/OXYGEN.png` detailed icon route: oxygen icon legible at compact scale, 720p, low render scale, normal visor state, warning state, and disabled/fault state.
- Oxygen icon paired with numeric reserve, warning shape/icon, and non-color cue. Color alone is rejected.
- HUD oxygen state remains readable when visor dirt, wet lens, scanlines, cracks, or pressure degradation are active.
- Text does not clip for long localized labels and remains distinguishable from decorative instrument markings.

## Diegetic Visor Style Constraints

- Oxygen UI is an instrument, not decoration.
- Carrier must be helmet glass, suit HUD, cockpit panel, wrist/tool display, or another named physical presentation surface.
- UI presentation must not own oxygen truth. It reads immutable owner snapshots, cached interfaces, typed packets, or documented DataVault lanes.
- Stale, missing, or faulted oxygen data must show stale/fault state. No invented safe value.
- Amber means caution/service, red means fatal/urgent only, cyan means measurement, off-white means readable label/copy.
- Visual degradation may affect noncritical regions only. It must not hide oxygen, pressure, route, tool state, warnings, or interaction affordance.

## Localization And Accessibility Checks

Future owner must provide:

- Stable LocID list for oxygen label, oxygen warning, reserve units, stale state, fault state, and disabled reason.
- Fallback language behavior and missing-key visual behavior.
- Long-string expansion check for German/Finnish-like text and CJK/RTL/fallback risk note.
- Font atlas coverage note for HUD-critical glyphs and units.
- UI scale check at 720p and compact render scale.
- Colorblind-safe oxygen warning through shape, label, icon, cadence, audio, or haptic redundancy.
- Reduced flashing and reduced visor degradation behavior that preserves oxygen state clarity.
- Keyboard/gamepad/controller navigation proof if the oxygen route appears in interactive inventory/tool UI.

## Zero-GC HUD Text Boundary

Future owner must keep repeated HUD oxygen text and quantities on an allocation-safe path:

- baked integer localization keys or stable hashes;
- preallocated char buffers;
- numeric `TryFormat` route;
- `TMP_Text.SetCharArray` for repeated readouts;
- cached owner interface or snapshot source;
- no `TMP_Text.text =` in repeated HUD updates;
- no interpolated strings, string concatenation, `string.Format`, runtime hierarchy path building, scene search, or hot `GlobalRegistry` polling.

Static source review cannot prove this boundary. It only names the boundary future work must test.

## UI Atlas Proof Artifacts

Future owner packet or implementation report must attach:

- SpriteAtlas asset path and inspected contents.
- Unity importer readback for both oxygen sources.
- Atlas packing/settings screenshot or API dump.
- HUD prefab/scene binding readback for the oxygen icon slot.
- Compact, middle, high, and ultra visual captures using the same gameplay truth and string ids.
- Texture memory/reserved memory evidence after atlas/import changes.
- Profiler/GCMonitor evidence for repeated oxygen text/icon updates.
- Frame Debugger evidence if atlas/material/UI renderer changes affect draw calls, SetPass, masks, or render texture use.

## Rejection Gates

Reject future execution if any of these remain true:

- `oxygen-tank.png` is used as a final colored oxygen icon without explicit mask/tint proof.
- `ui/OXYGEN.png` is treated as HUD-bound from static docs alone.
- No SpriteAtlas asset, packing proof, or atlas owner exists for the detailed oxygen source.
- Static `.meta` text is used as Unity import proof.
- Oxygen readout clips, turns black, loses silhouette, or depends on color alone.
- HUD oxygen presentation owns or mutates oxygen gameplay truth.
- Repeated oxygen text/icon updates use runtime string formatting, scene search, hierarchy search, or hot registry polling.
- Localization expansion, missing-key behavior, UI scale, color redundancy, or reduced flashing/degradation paths are missing.
- Compact lane becomes flat, unreadable, or visually cheaper than the project floor.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` drives presentation detail only. It must not change oxygen truth owner, warning priority, LocIDs, input semantics, save identity, or update authority.

- Low: compressed atlas residency, strong silhouette, static readable icon, stable text, no dependency on animation or glow, no black oxygen slot.
- Middle: detailed icon, clear warning/fault states, stable atlas residency, modest carrier material.
- High: richer visor glass response, smoother warning transition, subtle instrument dirt around readable oxygen state.
- Ultra: layered screen artifacts, denser instrument detail, and cinematic carrier material around unchanged oxygen truth and text ids.

Quality scaling must be continuous. No binary low/high branch may decide whether oxygen remains readable.

## Regression Model

- CPU: static packet only; no runtime CPU result claimed. Future route must show no hot hierarchy search, sprite churn, or dynamic atlas load spike.
- GC: static packet only; no runtime allocation result claimed. Future route must produce HUD update evidence for repeated text/icon changes.
- Memory/VRAM: no import or residency changed. Future atlas route must prove texture memory, upload budget fit, and reserved memory after import.
- Cadence: no runtime cadence changed. Future oxygen readout should target instrument cadence with hysteresis for warning/fault state changes.
- Correctness: packet reduces route confusion by separating mask/silhouette source from detailed icon source; oxygen gameplay truth remains outside UI.

Final status: `PENDING_VERIFICATION`.
