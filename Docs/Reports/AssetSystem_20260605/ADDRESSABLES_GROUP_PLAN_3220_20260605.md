# Addressables Group Plan - Asset Worker 3220 - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence boundary: `STATIC_DOC` / `STATIC_SOURCE` only.
Runtime proof: absent. No Unity run, import, Addressables window readback, Addressables build, Play Mode, Memory Profiler, Frame Debugger, GCMonitor, player build, or runtime screenshot proof is claimed.
Write scope: this report only. No `Assets/` files, Addressables settings, groups, labels, keys, catalogs, project settings, prefabs, scenes, materials, or import settings were created or changed.

First-20-minutes route moment: addresses an Addressables planning blocker for bright surface exit, Aegir/sky/moons, ocean/shoreline contact, photic terrain/flora, suit oxygen HUD, first-exit audio, and candidate prefab pools. It does not prove those assets are runtime-safe.

## Mandates And Evidence Used

- `AGENTS.md`
- `streaming.md`
- `performance.md`
- `data.md`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`

Static findings carried forward:

- `Assets/AddressableAssetsData` exists but static scan found `0` files. No settings, group, profile, schema, catalog, entry, key, or label evidence exists on disk.
- Audio ledger has `138` rows: `84` music, `12` ambient, `5` player_loop, `5` ui, `30` sfx, `2` voice. All rows still have `owner=PENDING_OWNER`, `addressable_group=PENDING_ADDRESSABLES`, and `addressable_key=PENDING_ADDRESSABLES`.
- `Player.prefab` direct `AudioClip` references are blockers until owner/load/release exception proof exists.
- `MusicDirectorConfig_Global.asset` has null mixer group fields in static evidence. MusicDirector profile references prove serialized route intent only, not Addressables ownership, release, mix, or cadence.
- Static material, prefab, scene, and audio reachability is not residency proof.

## Non-Acceptance Boundary

This plan is a future implementation proposal. It is not permission to create groups yet.

Reject all readiness claims until the future Unity owner produces the readback artifacts listed in this report and a later implementation owner proves:

- Addressables settings asset, groups, schemas, labels, and entries exist on disk.
- Heavy groups use `RequestedAssetAndDependencies` unless a measured exception exists.
- Loaded handle count, owner list, ref-count behavior, release ledger, RAM, VRAM, texture mip residency, and pressure behavior are recorded.
- Unity Console/import state, visual screenshots, Frame Debugger where visual, and runtime load/release paths exist.

## Group Naming And Key Law

Human-readable Addressables keys should use stable string keys and be hashed by runtime code only after Unity assignment exists:

- String key pattern: `h8/<domain>/<route>/<family>/<asset_slug>[/lod<0-3>][/locale_<tag>]`
- Runtime key derivation proposal: `FNV1a32(guid_string + biome_id_byte + lod_level_nibble)` per streaming mandate. This is a future runtime route, not implemented by this report.
- Group name pattern: `H8_<DOMAIN>_<ROUTE>_<CONTENT>`
- Label pattern: `h8.route.first20`, `h8.domain.<domain>`, `h8.biome.<biome>`, `h8.priority.tier<n>`, `h8.owner.<owner>`, `h8.loadmode.requested_dependencies`
- Do not create labels until Unity readback proves the target assets and future owner creates Addressables settings intentionally.

## Proposed Group Taxonomy

All resident budgets are placeholders. They are admission fields for the future owner, not measured values.

| Proposed group | Asset-front category | Owner | Load phase | Release phase | Resident budget placeholder | Key convention | Proposed labels | Streaming / packed / local / remote policy | Proof required before adoption |
|---|---|---|---|---|---|---|---|---|---|
| `H8_SKY_SURFACE_CELESTIAL` | sky / Aegir / cloud / moon candidate materials and textures | Surface sky and celestial material owner, with Streaming owner for lifecycle | Loading screen before `02_HECTON_WORLD`; warm preload before first surface exit; no surprise hot load during camera reveal | Scene unload; pressure may reduce mips and far decorative layers only, not remove readable sky/Aegir route | `PENDING_MEMORY_PROFILER_MB_SKY_CELESTIAL`; compact slice must fit texture budget before group creation | `h8/sky/surface/<material_or_texture_slug>[/lod<n>]` | `h8.domain.sky`, `h8.route.first20`, `h8.priority.tier5`, `h8.loadmode.requested_dependencies` | Streaming allowed. Local packed bundles allowed. Remote not allowed for first-20 route. `AllPackedAssetsAndDependencies` rejected unless group is split and Memory Profiler proves bounded resident size. | ASSET_OWNER_06 sky/Aegir readback tables, Game/Scene screenshots, Frame Debugger sky/Aegir pass report, Console export, then Addressables group/key/settings readback. |
| `H8_OCEAN_CREST_CONTACT` | ocean / Crest / foam / contact masks and material support | Ocean/Crest material owner; Streaming owner for lifecycle. No custom Crest wrapper owner is accepted. | Loading screen before `02_HECTON_WORLD`; near-surface/contact preload before shoreline/surface route | Scene unload; biome/route exit after hysteresis; pressure may downgrade foam/contact mip residency, not unload required surface contact | `PENDING_MEMORY_PROFILER_MB_OCEAN_CONTACT` | `h8/ocean/contact/<foam_or_mask_slug>[/lod<n>]` | `h8.domain.ocean`, `h8.domain.crest`, `h8.route.first20`, `h8.priority.tier2`, `h8.loadmode.requested_dependencies` | Streaming allowed. Local only. Remote not allowed. Packed all-dependencies rejected for Crest/ocean/contact until measured resident memory exists. | ASSET_OWNER_06 Crest/foam screenshots, active material/slot/scalar table, Frame Debugger Crest/foam pass report, Console export, plus Addressables coverage matrix. |
| `H8_TERRAIN_PHOTIC_GEOLOGY_PBR` | terrain/geology PBR stacks, wet basalt, sand/shell, rock material candidates | Terrain/geology material owner; Streaming owner for residency | `02_HECTON_WORLD` load screen and depth/route-gated photic preload | Scene unload; biome boundary with hysteresis; pressure downgrades mips/LOD residency before broad unload | `PENDING_MEMORY_PROFILER_MB_TERRAIN_PBR`; must state texture/mip budget share | `h8/terrain/photic/<family>/<asset_slug>[/lod<n>]` | `h8.domain.terrain`, `h8.domain.geology`, `h8.biome.photic`, `h8.priority.tier2`, `h8.loadmode.requested_dependencies` | Streaming required. Local only for first-20. Remote not allowed. `AllPackedAssetsAndDependencies` rejected for terrain/PBR stacks without resident texture proof. | ASSET_OWNER_06 terrain/geology Game/Scene screenshots, terrain receiver/material/shader/slot table, sampled rock prefab table, Frame Debugger/Stats notes, Console export, then Addressables group/key readback. |
| `H8_FLORA_PHOTIC_REPLACEMENT_CANDIDATES` | flora/proxy replacement candidates, baked flora/coral/kelp material candidates | Flora/prefab material owner; Streaming owner for lifecycle | Photic biome preload only after visible route readback proves candidates; never from proxy pool by default | Scene unload; biome exit with hysteresis; pressure releases distant decorative flora before route silhouettes | `PENDING_MEMORY_PROFILER_MB_FLORA_PHOTIC`; must include mesh/material/texture split | `h8/flora/photic/<family>/<prefab_or_material_slug>[/lod<n>]` | `h8.domain.flora`, `h8.biome.photic`, `h8.route.first20`, `h8.priority.tier4`, `h8.loadmode.requested_dependencies` | Streaming allowed. Local packed by biome allowed only after budget. Remote not allowed for first-20. `AllPackedAssetsAndDependencies` rejected for flora/HLOD groups without memory proof. | ASSET_OWNER_06 flora/proxy Game/Scene screenshots, object/material/visibility table, sampled candidate prefab table, proxy material Frame Debugger report if visible, Console export, Addressables coverage matrix. |
| `H8_AUDIO_MUSIC_DIRECTOR` | audio music | MusicDirector/audio owner; Streaming owner for handles | MusicDirector profile preload during loading screen; next-track double-buffer only from owned cue route | Scene unload, profile/context exit, or pressure policy. Release must follow handle ledger; do not rely on direct clip refs | `PENDING_AUDIO_MEMORY_MB_MUSIC`; active/reserve bank count required | `h8/audio/music/<profile>/<cue_id>` | `h8.domain.audio`, `h8.audio.music`, `h8.priority.tier3`, `h8.loadmode.requested_dependencies` | Streaming allowed for long music through owner route. Local allowed. Remote rejected for first-20. Packed all-dependencies rejected for music libraries unless tiny profile subset has resident memory proof. | ASSET_OWNER_06 audio config Inspector screenshot, mixer/profile/direct-clip/import readback table, Console export, audio ledger owner/key update proposal, later runtime MusicDirector handle/release/memory proof. |
| `H8_AUDIO_AMBIENT_BIOME` | audio ambient banks and pressure beds | Audio ambience owner; Streaming owner for handles | Starting biome bank during loading screen; one active bank max under pressure until proof says otherwise | Biome/context exit with hysteresis; pressure releases inactive banks before survival/UI audio | `PENDING_AUDIO_MEMORY_MB_AMBIENT`; bank breadth and active voices required | `h8/audio/ambient/<biome_or_context>/<cue_id>` | `h8.domain.audio`, `h8.audio.ambient`, `h8.biome.<name>`, `h8.priority.tier3`, `h8.loadmode.requested_dependencies` | Streaming allowed for long ambience after authority conflict is resolved. Local allowed. Remote rejected for first-20. Packed all-dependencies rejected for ambient banks without resident memory proof. | ASSET_OWNER_06 audio readback artifacts, import/load-type table, ledger owner/group/key assignments, later listening/mix/DSP/memory proof. |
| `H8_AUDIO_PLAYER_LOOP_CORE` | player_loop: breath, suit, swim, player-critical loops | Player audio owner; Streaming owner only if owner route proves no direct-ref lifecycle leak | Tier 0/1 startup or loading screen before player control; no runtime stall | Player teardown/scene unload only. Pressure must not remove breath/survival readability; may reduce secondary layer breadth | `PENDING_AUDIO_MEMORY_MB_PLAYER_LOOP`; must record always-hot exception or Addressables handle policy | `h8/audio/player_loop/<cue_id>` | `h8.domain.audio`, `h8.audio.player_loop`, `h8.priority.tier0`, `h8.route.first20`, `h8.loadmode.requested_dependencies` | Streaming generally suspicious for player-critical loops; use compressed/always-hot exception only with owner ledger. Local only. Packed allowed only if tiny always-hot subset is proven bounded. | ASSET_OWNER_06 Player.prefab direct-clip screenshot, direct-ref/import-readback table, owner exception proof or reroute decision, Console export, later 0 B/frame playback and handle/release proof. |
| `H8_AUDIO_UI_CORE` | UI audio cues | UI/audio owner | Bootstrap or main menu loading phase; available before HUD/menu interaction | Scene/app teardown; pressure does not remove survival warnings | `PENDING_AUDIO_MEMORY_MB_UI_CORE`; tiny always-hot cap required | `h8/audio/ui/<cue_id>` | `h8.domain.audio`, `h8.audio.ui`, `h8.priority.tier0`, `h8.route.first20` | Local only. Streaming rejected for short UI. Packed all-dependencies may be allowed only after tiny always-hot budget proof, otherwise requested dependencies. | ASSET_OWNER_06 audio readback table, UI cue owner route, import/load decision, Console export, later UI warning audibility and 0 B/frame proof. |
| `H8_UI_SPRITES_CORE_HUD` | UI sprites, oxygen HUD source candidates and atlas candidates | UI/HUD owner; Streaming owner only for lifecycle if not core-resident | Bootstrap or HUD load before player control | HUD teardown/scene unload; pressure may not remove oxygen readability | `PENDING_UI_TEXTURE_MB_CORE`; atlas size and mip/import status required | `h8/ui/hud/<sprite_slug>` | `h8.domain.ui`, `h8.ui.hud`, `h8.priority.tier0`, `h8.route.first20` | Local only. Remote rejected. Streaming optional; always-hot core atlas acceptable only with budget. Packed all-dependencies allowed only for tiny core atlas with proof. | ASSET_OWNER_06 oxygen prefab Inspector screenshot, HUD/prefab preview screenshot, sprite binding/import/atlas table, Console export, later UI runtime readability proof. |
| `H8_WORLD_PREFAB_POOL_GEOLOGY` | prefab candidate pools: rocks/geology ProceduralFinals | Mesh/prefab owner; Streaming owner for lifecycle and pool release | Photic/geology route preload after readback proves candidate pool validity | Biome/route unload with hysteresis; pressure releases distant LOD0 first and keeps silhouettes/proxies | `PENDING_PREFAB_MEMORY_MB_GEOLOGY_POOL`; mesh/material/texture split required | `h8/prefab/geology/<family>/<prefab_slug>[/lod<n>]` | `h8.domain.prefab`, `h8.domain.geology`, `h8.biome.photic`, `h8.priority.tier4`, `h8.loadmode.requested_dependencies` | Streaming required. Local only for first-20. Packed all-dependencies rejected until pool memory and LOD residency are measured. | ASSET_OWNER_06 terrain/geology sampled rock prefab table, material/shader/slot proof, Frame Debugger/Stats notes, Console export, Addressables coverage matrix, later async instantiate/pool release proof. |
| `H8_WORLD_PREFAB_POOL_FLORA` | prefab candidate pools: Nature/Flora/Baked and BioForge/Shallows candidates | Flora/prefab owner; Streaming owner for lifecycle and pool release | Photic flora preload after proxy replacement candidates pass visual/material readback | Biome exit with hysteresis; pressure reduces density and releases far decorative pools before route silhouettes | `PENDING_PREFAB_MEMORY_MB_FLORA_POOL`; mesh/material/texture split required | `h8/prefab/flora/photic/<family>/<prefab_slug>[/lod<n>]` | `h8.domain.prefab`, `h8.domain.flora`, `h8.biome.photic`, `h8.priority.tier4`, `h8.loadmode.requested_dependencies` | Streaming required. Local only for first-20. Packed all-dependencies rejected until pool memory, LODs, and material bindings are measured. | ASSET_OWNER_06 flora/proxy sampled candidate prefab table, proxy visibility/material table, Frame Debugger if visible, Console export, Addressables coverage matrix, later async instantiate/pool release proof. |

## Explicit Heavy-Group Load Mode Rejection

`AllPackedAssetsAndDependencies` is rejected for heavy mixed groups: sky/Aegir/cloud, ocean/Crest/contact, terrain PBR, flora/HLOD, audio music, audio ambient, and prefab pools.

Allowed exception path:

- group is tiny and always-hot;
- resident memory cap is written before creation;
- Unity Memory Profiler or platform memory capture proves resident and committed memory;
- release ledger states why full packed residency is cheaper than requested dependencies;
- compact budget stays under the 1800 MB VRAM ceiling and 900 MB texture budget where textures are involved.

Until that proof exists, proposed heavy groups must use `RequestedAssetAndDependencies`.

## Direct Reference Blockers

### Player.prefab AudioClip refs

Direct `Player.prefab` `AudioClip` refs are not accepted streaming ownership. They are blockers for `H8_AUDIO_PLAYER_LOOP_CORE`, `H8_AUDIO_UI_CORE`, and any SFX/player loop route touched by the prefab.

Required before acceptance:

- object/component path for each direct ref;
- clip path, duration, import load type, compression, quality, and mono/stereo status;
- owner exception or reroute decision;
- handle/ref-count/release proof if Addressables-owned;
- 0 B/frame playback proof for runtime route;
- no generic string event route.

### MusicDirector profile refs

MusicDirector profile references are serialized route evidence only. They are blockers until `_musicMixerGroup` and `_stingerMixerGroup` are non-null or an owner records the explicit failure route.

Required before acceptance:

- active `MusicDirectorConfig_Global.asset` readback;
- mixer group fields;
- profile/cue table;
- Addressables key ownership per cue;
- runtime MusicDirector cadence and release proof;
- memory/residency proof for active and preloaded banks.

## Continuous GlobalQualityWeight Consequences

Use `GlobalQualityWeight` as a continuous scalar, not as binary tier branches. Tier names below are planning lanes, not `if (isLowEnd)` switches.

| Lane | Consequence for this group plan |
|---|---|
| Low / compact | Preserve surface sky, Aegir silhouette, ocean contact readability, photic terrain identity, oxygen HUD, breath/player loops, and return-route silhouettes. Reduce mip residency, speculative preload radius, decorative flora density, active ambient-bank breadth, and far prefab residency smoothly. No flat/dark replacement art. |
| Middle | Keep full first-20 gameplay truth and disciplined presentation. Permit route-owned PBR stacks, clean foam/contact masks, MusicDirector-owned music/ambient cues, UI sprite atlas ownership, and modest prefab/flora density only after group/key/readback proof. |
| High | Spend extra budget on longer mip/LOD residency, richer Aegir/cloud detail, stronger water/contact response, denser near-field geology/flora, and cleaner audio transitions after memory/frame proof. Owner routes and DTO/save truth do not change. |
| Ultra | Extend visual overkill through richer sky/cloud/Aegir residency, shoreline breakup, material layering, dense route dressing, and wider music/ambient preload only while Memory Profiler, frame, and release-ledger proof remain green. No bypass of owner keys, release ledgers, or compact-lane proof. |

Continuous planning knobs for future implementation:

- `prefetch_radius = lerp(compact_radius, ultra_radius, saturate(GlobalQualityWeight))` with pressure multiplier and hysteresis.
- `speculative_slots = floor(lerp(0, max_slots, GlobalQualityWeight * GlobalQualityWeight))`.
- `mip_target_bias = lerp(compact_bias, ultra_bias, GlobalQualityWeight)` clamped by VRAM pressure.
- `audio_bank_breadth = lerp(min_required_banks, max_profile_banks, GlobalQualityWeight)` with player-critical cues pinned.
- `decorative_prefab_density = base_density * smoothstep(0.0, 1.0, GlobalQualityWeight)` with route silhouettes preserved.

## Regression Model

- CPU: this report changes no runtime code. Future groups risk CPU spikes during load dispatch, catalog lookup, async instantiation, and release if owners create mixed heavy groups or unbounded load slots. Required proof: profiler markers for dispatch/release and no main-thread stall claim without capture.
- GC: this report changes no runtime code and makes no `0 B/frame` claim. Future lifecycle code must prove no hot-path managed allocations, no string key construction in Tick, no coroutine load loops, and no persistent handle-result caching without ref-count ownership.
- Memory/VRAM: current state has no Addressables residency proof. Main risk is overpacking heavy sky/ocean/terrain/flora/audio/prefab assets and blowing compact 1800 MB VRAM / 900 MB texture budget. Required proof: loaded handle count, owner list, RAM, VRAM, texture mip residency, committed/resident distinction, pressure behavior.
- Cadence: this report changes no runtime cadence. Future implementation must define load, preload, release, pressure audit, and hysteresis cadence. Immediate state flipping and same-frame load/release loops are rejected.
- Correctness: this plan reduces false acceptance risk by separating static reachability from future owner/key/residency proof. Correctness remains blocked until one fact has one owner, one route, and one proof artifact per group.

## Next Owner Handoff Before Creating Groups

Before any future owner creates Addressables settings, groups, labels, entries, keys, or catalogs, these ASSET_OWNER_06 artifacts must exist under the approved artifact roots and must show no stop condition:

- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_scene_sky_aegir_game_surface_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_scene_sky_aegir_scene_selected_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_scene_sky_aegir_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_scene_sky_aegir_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_crest_foam_game_surface_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_crest_foam_scene_selected_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_crest_foam_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_crest_foam_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_terrain_geology_game_photic_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_terrain_geology_scene_selected_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_terrain_geology_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_terrain_geology_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_flora_proxy_game_photic_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_flora_proxy_scene_selected_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_flora_proxy_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_flora_proxy_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_ui_oxygen_prefab_inspector_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_ui_oxygen_hud_preview_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_ui_oxygen_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_audio_config_inspector_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_audio_player_prefab_refs_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_audio_config_prefab_refs_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_addressables_groups_window_<timestamp>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_addressables_settings_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_ADDRESSABLES_READBACK_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_CONSOLE_<timestamp>.txt` after each relevant Unity readback segment or a combined console export that explicitly covers scene load, prefab readback, asset readback, and Addressables window opening.

If any stop report exists instead, such as `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_STOP_<timestamp>.md`, group creation remains blocked until the failure is corrected and readback reruns cleanly.

## Final Disposition

This plan converts the 3218 static gap into future group/key/owner structure only. It does not create Addressables data and does not prove runtime readiness.

Current status: `PENDING VERIFICATION`.
