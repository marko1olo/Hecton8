# Asset Proof Artifact Index - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_IMAGE_QA`, `AUDIO_WAVEFORM_QA`.
Scope: contact sheets, waveform sheets, generated source packs, cleanup manifests, and review artifacts.

This index is a map of existing proof-adjacent artifacts. It is not product acceptance. Screenshots, Unity readback, Frame Debugger, profiler, Memory Profiler, GCMonitor, Addressables build, or listening pass evidence are still absent.

## Contact Sheets

| Artifact | Role | Evidence Boundary | Owner Next Action |
|---|---|---|---|
| `Docs/AssetAudit/ContactSheets/water_foam_caustic_contact_sheet.png` | Water, foam, caustic visual triage | Static image review only | Use with `VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`; do not accept `foam.png` as visible contact art. |
| `Docs/AssetAudit/ContactSheets/sky_aegir_cloud_contact_sheet.png` | Sky, Aegir, cloud candidate comparison | Static image review only | Pair with Unity sky/material slot readback before any route claim. |
| `Docs/AssetAudit/ContactSheets/terrain_geology_contact_sheet.png` | Terrain/geology source triage | Static image review only | Use before authoring cleaned PBR stacks; no broad terrain claim from sheet alone. |
| `Docs/AssetAudit/ContactSheets/flora_coral_fauna_contact_sheet.png` | Flora/coral/fauna source triage | Static image review only | Pair with material/LOD/silhouette proof before placement. |
| `Docs/AssetAudit/ContactSheets/ui_textures_contact_sheet.png` | UI sprite candidate triage | Static image review only | Pair with HUD binding, atlas/import proof, and readability proof. |
| `Docs/AssetAudit/ContactSheets/unknown_textures_contact_sheet.png` | Unassigned useful texture sources | Static image review only | Assign owner/material role before route use. |
| `Docs/AssetAudit/ContactSheets/generated_source_only_contact_sheet.png` | Generated/source-only candidate overview | Static image review only | Treat as reference art only. |
| `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png` | Mandatory visual references: surface water, sky, Aegir, coastline, shallows, flora, cockpit | Static reference review only | Use as the comparison floor for h8_1475 and route-owner visual work; it is not scene/import proof. |
| `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv` | Current path continuity for all mandatory reference images | Static path manifest only | Keep all 15 references reachable before any visual critique or owner handoff. |
| `Docs/GeneratedAssets/AegirGasGiantProof/AegirGasGiantProofContactSheet_20260608.png` | Aegir gas giant surface, underwater-up, horizon, phase, fog, storm, and quality fallback proof sheet | Static generated image review only | Pair with Unity material binding, sky/water/lighting readback, and Frame Debugger proof before any runtime acceptance. |

## Diagnostic Screenshot Reviews

| Artifact | Role | Evidence Boundary | Owner Next Action |
|---|---|---|---|
| `Docs/Screenshots/MCP/h8_1914_surface_water_recovery_probe.png` | Surface/ocean diagnostic rejection | Static screenshot review only; editor-only unsaved capture | Use with `SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md`; do not accept as h8_1475 proof. |
| `Docs/Screenshots/MCP/h8_1914_surface_water_recovery_probe.txt` | Surface/ocean diagnostic readback text | Static text capture only; editor-only unsaved capture | Route to ocean/Crest, terrain, and sky owners for future clean readback. |
| `Docs/AssetAudit/SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md` | Surface/ocean visual rejection summary | Static screenshot review only | Use to reject flat slab water, clipped shoreline, billboard Aegir, and temp-haze proof substitution. |
| `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png` | Surface/Crest diagnostic rejection | Static screenshot review only; not h8_1475 | Use to reject checkerboard water plane, black shoreline, weak terrain material, and false acceptance claims. |
| `Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md` | h8_1475 proof-tool risk summary | Static source review only | Use to reject editor-mutated diagnostic probe methods as canonical acceptance proof. |

## Audio Waveform Artifacts

| Artifact | Role | Evidence Boundary | Owner Next Action |
|---|---|---|---|
| `Docs/AssetAudit/AudioVisual/audio_waveform_contact_sheet_20260605.png` | Cross-cue waveform overview | Waveform image only | Use with `AUDIO_LISTENING_PASS_QUEUE_20260605.csv`; no mix/taste acceptance. |
| `Docs/AssetAudit/AudioVisual/audio_preview_waveform_stats_20260605.csv` | Peak/RMS/static waveform stats | Static waveform stats only | Pair with listening notes, runtime route, and mixer proof. |
| `Docs/AssetAudit/AudioVisual/Underwater_Ambient_waveform.png` | Direct `Player.prefab` ambient risk cue | Waveform image only | Resolve owner/release route and warning ducking before acceptance. |
| `Docs/AssetAudit/AudioVisual/breathing_breath_in_and_out_1_waveform.png` | Player breath loop risk cue | Waveform image only | Treat as player loop, not generic SFX. |
| `Docs/AssetAudit/AudioVisual/inside_suit_sounds_too_loud_waveform.png` | First-person suit mix debt | Waveform image only | Needs listening and mix owner proof. |
| `Docs/AssetAudit/AudioVisual/swimming_onwater_waveform.png` | Player swim loop cue | Waveform image only | Needs latency/start and import exception proof. |
| `Docs/AssetAudit/AudioVisual/Atmos_1_Loop_waveform.png` | Dense ambience bed | Waveform image only | Needs warning/readability and Q70/import policy proof. |
| `Docs/AssetAudit/AudioVisual/spaceship_sounds_ambient_waveform.png` | Ambience bed | Waveform image only | Needs route role and import policy proof. |
| `Docs/AssetAudit/AudioVisual/shelf_1_Abandoned_Depths_waveform.png` | Long first-route music bed | Waveform image only | Needs MusicDirector pause/window and mix proof. |
| `Docs/AssetAudit/AudioVisual/abyss_3_Deep_Trench_Drone_waveform.png` | Tense drone music bed | Waveform image only | Needs tension ownership and mix proof. |
| `Docs/AssetAudit/AudioVisual/stinger_dangerous_1_Iron_Teeth_waveform.png` | Danger stinger | Waveform image only | Needs cooldown/priority proof. |
| `Docs/AssetAudit/AudioVisual/click_sound_waveform.png` | UI click cue | Waveform image only | Needs HUD/UI audibility proof. |
| `Docs/AssetAudit/AudioVisual/VOStub_Chen_Log01_EN_waveform.png` | Placeholder VO stub | Waveform image only | Keep placeholder-blocked until final VO/localization/subtitle proof. |

## Generated Source Packs

| Pack | Role | Disposition | Owner Next Action |
|---|---|---|---|
| `Docs/GeneratedAssets/AssetSystem_20260605/FoamContactPrototype_20260605/` | Initial foam/contact source prototype | `SOURCE_ONLY` | Use as reference for cleaned contact masks; do not import directly. |
| `Docs/GeneratedAssets/AssetSystem_20260605/AegirCloudPrototype_20260605/` | Initial Aegir/cloud source prototype | `SOURCE_ONLY` | Use as reference for gas-giant/cloud authoring; shader response unproven. |
| `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/` | Cleaned foam/contact and Aegir/cloud source direction | `SOURCE_ONLY_USEFUL` | Route through texture authoring/import matrix and Unity readback before any product use. |
| `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md` | Prior texture authoring pack summary | `STATIC_DOC` | Use as supporting context only; current queues and import matrix remain controlling. |
| `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_contact_sheet.png` | Batch31 wet basalt and photic substrate PBR candidate overview | `SOURCE_ONLY_USEFUL` | Useful source direction only; direct import remains blocked by channel semantics and material-target proof. |
| `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_INDEX.md` | Batch31 promotion-prep manifest | `BLOCKED_CHANNEL_SEMANTICS` | Keep blocked until the target shader layout and packed-channel route are proven. |
| `Docs/GeneratedAssets/AegirGasGiantProof/AegirGasGiantProofContactSheet_20260608.json` | Aegir generated proof manifest with source textures, view profiles, phase/fog/cloud/storm coverage, and quality weights | `STATIC_DOC` | Keep as machine-readable proof contract for validator/tooling; it does not prove Unity import or runtime rendering. |

## Taxonomy Artifacts

| Artifact | Role | Row Count | Evidence Boundary |
|---|---|---:|---|
| `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.md` / `.csv` | Audio/music/static routing taxonomy | 11 | `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_WAVEFORM_QA` |
| `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.md` / `.csv` | Visual/mesh/static promotion taxonomy | 19 | `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_IMAGE_QA` |

CSV companion: `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv`.

## Rejection Rules

- Contact sheets cannot prove in-game visual quality.
- Waveform sheets cannot prove mix, taste, routing, audio-thread safety, or runtime memory behavior.
- Generated packs cannot be imported as final art without authoring, import, material, screenshot, and memory proof.
- Taxonomies and queues cannot create Addressables readiness or stable authority.
- Mandatory visual reference sheets are comparison targets only. They do not prove the current scene is correct.
- h8_1914 diagnostic screenshots are negative evidence only. They cannot substitute for the canonical h8_1475 proof packet.

## Manual Image QA Notes

- Water/foam contact sheet: `foam.png` reads as a flat tiled turquoise pattern. It is rejected for visible shoreline/contact art. Droplet/runoff sources can support masks/details only after authored material response.
- Sky/Aegir/cloud sheet: `clouds0_diff` and `Aegir_storms` are useful band/mask sources; the baked Aegir disc remains soft/toy-like as a hero object. Dark panorama sources do not satisfy bright surface-route sky quality.
- Terrain/geology sheet: wet basalt and sand source pools contain useful material direction, but several generated basalt variants show repeated macro-rock islands and uneven channel quality. Clean PBR authoring remains required.
- Flora/coral/fauna sheet: imported coral/kelp source stacks have usable raw forms, but streaming mips are disabled in static metadata and several detail/mask channels look placeholder-flat. Proxy material contamination remains a blocker.
- UI texture sheet: `ui/OXYGEN.png` is a detailed source candidate; `oxygen-tank.png` is a black mask/silhouette and cannot be treated as the colored HUD oxygen icon without a proven mask/tint route.
- Unknown texture sheet: `FLOOR`, mineral seep, organic lightning, menu view, and bubble/soft-plume sources can support specific owners. Fog line/weather sheets remain support-only; no unknown row can enter a route without owner, material role, import proof, and route screenshot proof.
- Generated-source sheet: generated wet-basalt/sand sets are reference material only. Repetition, synthetic normals/MRAO, and source-cleanup debt block direct final import.
- Mandatory visual reference sheet: surface target requires bright ocean readability, authored shoreline, large readable Aegir, detailed sky, rich photic shallows, dense attached flora/coral, and cockpit/instrument readability. Current h8_1914 imagery fails this floor.
- Batch31 PromotionPrep sheet: wet basalt and photic substrate candidates are visibly stronger than checkerboard/placeholder terrain. Wet basalt still carries turquoise source-contamination/watermark-like streak risk; seabed and shell-sand are bright/detail-rich but near-duplicate until a biome/material role split is proven. Still static source only; MRAO/channel semantics and Unity import/material readback remain blocked.
- AegirGasGiantProofContactSheet_20260608.png: generated contact sheet covers surface, underwater-up, horizon, crescent, heavy-fog/cloud, storm-emission, and low-quality fallback views. It is accepted only as static renderer/proof-tool evidence; Unity scene material binding, sky/water/lighting consumption, Frame Debugger, and runtime screenshots remain required.
- AegirGasGiantProofContactSheet_20260608.json: manifest carries source texture paths and `qualityWeight` coverage for high and low tiers. It is a validator contract, not product acceptance.

## Manual Waveform QA Notes

- `shelf_1_Abandoned_Depths`: dense/loud long-bed preview. It needs MusicDirector pause/window and mix proof before first-route use.
- `Atmos_1_Loop`: steady dense ambience. It needs warning/readability proof and import-policy resolution.
- `breathing_breath_in_and_out_1`: hot peak and long quiet tails. It must be treated as a player loop, not generic SFX.
- `inside_suit_sounds_too_loud`: steady first-person loop risk. It needs listening and mix owner proof.
- `Underwater_Ambient`: direct player-prefab ambient risk cue. It needs owner/release and ducking proof before route acceptance.
- `VOStub_Chen_Log01_EN`: too quiet to establish final VO loudness. Keep placeholder-blocked.
- `click_sound` and `swimming_onwater`: low-level waveform evidence only. They still need route-specific audibility and latency proof.

## Next Owner Use

1. Use `ASSET_ACTION_QUEUE_20260605.csv` for priority.
2. Use `VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`, `AUDIO_LISTENING_PASS_QUEUE_20260605.csv`, and `MESH_PREFAB_REVIEW_QUEUE_20260605.csv` for pass order.
3. Use this index only to find existing proof-adjacent artifacts.
4. Produce new evidence only through the correct owner route and process gate.

Final status: `PENDING VERIFICATION`.
