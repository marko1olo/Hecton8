# UI Sprite Route Static Table - 3216 - 2026-06-05

Status: `PENDING_VERIFICATION`
Owner role: Asset Worker 3216 - UI Sprite Route Static Owner
First-20 route moment: addresses HUD/resource icon source confusion before first exit, suit oxygen readout, tool use, and early inventory decisions.

## Evidence Boundary

Evidence classes used:

- `STATIC_SOURCE`: file existence, `.meta` importer text, audit CSV rows, static YAML GUID references.
- `STATIC_IMAGE_QA`: contact sheet/manual image inspection and local pixel sampling.

Evidence classes not produced:

- No Unity import/readback.
- No SpriteAtlas packing proof.
- No Addressables ownership proof.
- No prefab/scene mutation.
- No Play Mode, Frame Debugger, Profiler, GCMonitor, or runtime UI binding proof.
- No HUD readiness, atlas readiness, runtime binding, or final acceptance claim.

Mandates followed:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `localization.md`

Static inputs:

- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/ContactSheets/ui_textures_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/unknown_textures_contact_sheet.png`
- `Assets/_Project/Art/Sprites/**`

## Sprite Route Table

| Path | Apparent role | Finished-source vs mask/prototype | Static visual note | Import/atlas proof needed | Route candidate/disposition |
|---|---|---|---|---|---|
| `Assets/_Project/Art/Sprites/ui/OXYGEN.png` | Oxygen inventory/HUD icon source candidate | Finished-looking source candidate | Detailed blue oxygen canister in circular instrument frame. Pixel sample: 1024x1024, 100% nonzero alpha, 56.36% nonblack RGB. Audit explicitly names this as the detailed oxygen icon candidate. | Unity importer readback, atlas ownership, platform compression, runtime HUD/inventory binding, no hot-path UI allocation proof, compact readability screenshot. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Use as detailed oxygen icon source candidate. Not HUD-ready. Static search did not find this GUID bound in `Suit_HUD_Canvas.prefab`. |
| `Assets/_Project/Art/Sprites/oxygen-tank.png` | Legacy oxygen HUD silhouette/source mask | Mask/silhouette, not finished colored icon | Contact sheet reads black/empty. Pixel sample: 512x512, 26.43% nonzero alpha, 0% nonblack RGB. YAML usage map and prefab reference prove it is reachable from `Suit_HUD_Canvas.prefab`. | Explicit mask role if retained, tint/material proof, prefab replacement/reroute proof if colored icon is required, runtime no-black-icon HUD screenshot. | Treat as mask/silhouette only. Do not treat as final oxygen icon. `UI_SOURCE_ATLAS_PROOF_PENDING` in CSV is not visual acceptance. |
| `Assets/_Project/Art/Sprites/ui/BATTERY.png` | Battery/power resource icon | Finished-looking source candidate | Detailed industrial battery/power cell icon. Pixel sample: 1024x1024, 100% alpha, 80.86% nonblack RGB. | Sprite import readback, atlas packing, HUD/inventory binding, quantity/localized label route, 0 B UI update proof. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Candidate only. |
| `Assets/_Project/Art/Sprites/ui/COPPER.png` | Copper/salvage resource icon | Finished-looking source candidate | Detailed copper cable coil/source item icon. Pixel sample: 1024x1024, 100% alpha, 61.25% nonblack RGB. | Sprite import readback, atlas packing, inventory binding, localization label/quantity route, 0 B UI update proof. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Candidate only. |
| `Assets/_Project/Art/Sprites/ui/CUTTER.png` | Cutter/tool icon | Finished-looking source candidate | Detailed hard-surface cutter/tool icon with warning striping and emissive muzzle language. Pixel sample: 1024x1024, 100% alpha, 40.61% nonblack RGB. | Tool route ownership, atlas packing, selected/disabled/charge states, input binding, no hot-path allocation proof. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Candidate only. |
| `Assets/_Project/Art/Sprites/ui/MICRO.png` | Microchip/electronics resource icon | Finished-looking source candidate | Detailed circuit-board/electronics module icon. Pixel sample: 1024x1024, 100% alpha, 67.43% nonblack RGB. | Sprite import readback, atlas packing, inventory binding, localized label/quantity route, compact readability proof. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Candidate only. |
| `Assets/_Project/Art/Sprites/ui/TITANIUM.png` | Titanium/mineral resource icon | Finished-looking source candidate | Detailed blue mineral chunk icon. Pixel sample: 1024x1024, 100% alpha, 73.41% nonblack RGB. | Sprite import readback, atlas packing, inventory binding, localized label/quantity route, compact readability proof. | `UI_SOURCE_ATLAS_PROOF_PENDING`. Candidate only. |
| `Assets/_Project/Art/Sprites/cardiogram.png` | Suit vitals/cardiogram mask or HUD graph support | Mask/prototype source | Direct image view is black on black. Pixel sample: 512x512, 54.3% nonzero alpha, 0% nonblack RGB. Static usage map and prefab YAML reference `Suit_HUD_Canvas.prefab`. | Explicit graph-mask role, tint/material proof, vitals owner-data route, cadence, stale/fault state, 0 B text/graph update proof. | `UNASSIGNED_STATIC_SOURCE`. May be HUD support mask, not a finished icon. |
| `Assets/_Project/Art/Sprites/ring.png` | Reticle/progress/oxygen ring control shape | Simple mask/control primitive | White circular ring, 601x616, 17.27% nonzero alpha and nonblack RGB. Referenced multiple times in `Suit_HUD_Canvas.prefab`. | Control role manifest, fill behavior proof, atlas packing, compact readability, no decorative empty-ring misuse. | `UNASSIGNED_STATIC_SOURCE`. Control mask candidate only, not a finished HUD icon. |
| `Assets/_Project/Art/Sprites/thunder.png` | Power/electric/warning silhouette support | Mask/prototype source | Direct image view is black on black. Pixel sample: 512x512, 23.56% nonzero alpha, 0% nonblack RGB. Static usage map and prefab YAML reference `Suit_HUD_Canvas.prefab`. | Explicit mask/tint role, warning severity/color semantics, localization/accessibility pairing, runtime warning proof. | `UNASSIGNED_STATIC_SOURCE`. Mask only unless a UI owner assigns and proves the role. |

## Relevant Art/TEXTURES Rows

These are not HUD icon sprites. They are support/source rows that could affect UI, visor, menu, or instrument material work.

| Path | Apparent role | Finished-source vs mask/prototype | Static visual note | Import/atlas proof needed | Route candidate/disposition |
|---|---|---|---|---|---|
| `Assets/_Project/Art/TEXTURES/menuview.png` | Menu/background concept source | Prototype/source only | Cockpit/window scene art in unknown texture sheet. | UI/menu route owner, import role, mips policy, localization/readability overlay proof. | `UNASSIGNED_STATIC_SOURCE`. Not HUD-ready. |
| `Assets/_Project/Art/TEXTURES/gameart.png` | Menu/key art/source image | Prototype/source only | Underwater scene/key art in unknown texture sheet. | Route owner, import role, mips policy, loading/menu binding proof. | `UNASSIGNED_STATIC_SOURCE`. Not HUD-ready. |
| `Assets/_Project/Art/TEXTURES/FLOOR.png` | Possible panel/terminal substrate source | Material source, not icon | Rusted metal floor/panel texture. | Material role, compression, tiling, UI carrier proof if used as panel surface. | `UNASSIGNED_STATIC_SOURCE`. Not sprite route. |
| `Assets/_Project/Art/TEXTURES/FLOOR1.png` | Possible panel/terminal substrate source | Material source, not icon | Diamond-plate metal texture. | Material role, compression, tiling, UI carrier proof if used as panel surface. | `UNASSIGNED_STATIC_SOURCE`. Not sprite route. |
| `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png` | Visor wet-glass support mask | Packed mask source | Audit maps it to `Mat_Visor_Glass[_WaterDropletMaskTex]` and `Player.prefab`. | Unity material readback, Frame Debugger/RenderGraph proof, HUD readability under distortion, import type proof. | `UNASSIGNED_STATIC_SOURCE`. Visor support only. |
| `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png` | Visor runoff normal/support | Packed mask/normal source | Audit maps it to `Mat_Visor_Glass[_WaterRunoffNormalTex]` and `Player.prefab`. | Unity normal/import proof, material readback, compact readability, GPU cost proof. | `UNASSIGNED_STATIC_SOURCE`. Visor support only. |
| `Assets/_Project/Art/TEXTURES/ORGANIC.png` | Organic/electric material/source texture | Prototype/source only | Blue electric/organic pattern in unknown sheet. | Route owner, material role, import type, binding proof. | `UNASSIGNED_STATIC_SOURCE`. Do not use as HUD icon. |

## Static Reference Notes

- `Suit_HUD_Canvas.prefab` statically references `oxygen-tank.png`, `cardiogram.png`, `ring.png`, and `thunder.png`.
- `Suit_HUD_Canvas.prefab` static search did not show the `ui/OXYGEN.png` GUID.
- No `.spriteatlas` or `.spriteatlasv2` file was found under `Assets/_Project` by strict extension scan.
- All listed sprite `.meta` files use `textureType: 8`, `spriteMode: 1`, `sRGBTexture: 1`, `enableMipMap: 0`, `streamingMipmaps: 0`, `alphaIsTransparency: 1`, Standalone `textureFormat: 25`, and Standalone `textureCompression: 1`.
- `assetBundleName` is empty in the inspected sprite `.meta` files. This is not Addressables proof.

## HUD/UI Runtime Blockers

1. Sprite import proof: static `.meta` text is not Unity importer readback. Need Unity/API proof for sprite type, alpha, platform compression, mip policy by role, and any mask/normal/sRGB exceptions.
2. Atlas ownership: no strict-extension SpriteAtlas asset found under `Assets/_Project`; standalone 1024 PNG source candidates need atlas owner, packing proof, platform format proof, and residency budget.
3. Wrong oxygen binding risk: static prefab/YAML evidence points `Suit_HUD_Canvas.prefab` at `oxygen-tank.png`, while the detailed candidate is `ui/OXYGEN.png`. This is a static route blocker until a UI owner reroutes or explicitly keeps the mask role.
4. Text/localization/font atlas interaction: inventory labels, oxygen warnings, quantities, and tool states need stable LocIDs, font atlas coverage, expansion proof, missing-key behavior, and zero-GC `TMP_Text.SetCharArray` style runtime updates where repeated.
5. Runtime UI binding: source icons need owner-data routes for oxygen, power, tool state, inventory count, stale/fault display, and input affordance. UI must not own gameplay truth.
6. Zero-GC proof: no Play Mode/Profiler/GCMonitor evidence exists for sprite swaps, text updates, quantity formatting, warning cadence, atlas loading, or UI state changes.
7. Addressables/async upload: no group/key/release proof exists. Upload budgets must follow the global scalability route, not per-sprite ad hoc settings.
8. Accessibility/readability: colored icons still need shape/text pairing, compact-tier readability, long localized labels, and colorblind-safe warning semantics.

## Scalability Consequences

- Low/compact: use atlas-packed, compressed, readable symbols with fixed critical state hierarchy; masks may tint cheaply only if the shape remains legible. No black oxygen icon.
- Middle: allow detailed resource/tool icons, stable atlas residency, and clear selected/disabled/fault states.
- High: add richer material treatment, subtle glass/panel response, and smoother transitions around the same icon truth.
- Ultra: add dense instrument detail and layered carrier material only around stable readable text/icons. Do not change warning priority, gameplay truth, string IDs, or binding ownership.

## Regression Model

- CPU: no runtime code changed. Future UI binding must prove no hot-path hierarchy search, sprite churn, or dynamic atlas load spikes.
- GC: no runtime proof. Future HUD/inventory route must provide 0 B/frame evidence for repeated text/icon updates.
- Memory/VRAM: source PNGs are standalone; atlas/residency proof is absent. 1024 UI source candidates are acceptable only after route-owned atlas/import budgeting.
- Cadence: no runtime cadence changed. Future oxygen/tool/inventory readouts need justified update cadence and hysteresis where state can flap.
- Correctness: this report reduces false promotion risk by separating finished-looking source icons from mask/prototype sprites, especially `ui/OXYGEN.png` versus `oxygen-tank.png`.

Final status: `PENDING_VERIFICATION`.
