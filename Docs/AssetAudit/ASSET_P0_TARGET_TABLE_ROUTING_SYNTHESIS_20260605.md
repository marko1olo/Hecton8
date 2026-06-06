# Asset P0 Target Table Routing Synthesis - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_YAML_SCAN`, `STATIC_IMAGE_QA`, `AUDIO_SOURCE_PROBE`.
Scope: compact crosswalk for five P0 target/readback/capture tables created for asset owners 24-33.

This file does not prove Unity import state, material binding, prefab correctness, audio runtime behavior, visual quality, GC, frame time, memory, or Addressables readiness. It is a dispatch map only.

CSV companion: `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.csv`.

## Source Tables

| Source table | Rows | Current status | First owner packet |
|---|---:|---|---|
| `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv` | 124 | `PENDING UNITY READBACK` | `taskslocal/asset_system_20260605/ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md` |
| `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` | 39 | `PENDING UNITY PREFAB READBACK` | `taskslocal/asset_system_20260605/ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md` |
| `AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.csv` | 6 | `PENDING_VERIFICATION` | `taskslocal/asset_system_20260605/ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md` |
| `H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` | 123 | `PENDING CLEAN PROCESS GATE` | `taskslocal/asset_system_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md` |
| `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` | 7 | `REJECTED / PENDING_H8_1475_PROOF` | `Ocean/Crest owner; Material/texture owner; Sky/Aegir owner; Terrain/geology owner; Underwater VFX/source owner; UI/HUD owner; Product-face prefab owner; Unity proof owner.` |

## Dispatch Rules

- Use the synthesis CSV for owner assignment order only; use the source table for row-level work.
- If Unity process gate is red, do not run readback, importers, builds, scene saves, prefab saves, material edits, or Addressables operations.
- `H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` defines readback scope only. It is not proof by itself.
- `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` is rejection/gap evidence. It is not visual acceptance.
- Every future promotion must close graphics, optimization, and gameplay proof together; static row cleanup alone is insufficient.

## Routing Summary

### product_face_material_repair

- Source table: `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv` (124 rows).
- Primary blockers: flora_coral_kelp_proxy_material_contamination=40; foam_ocean_contact_material_readback_required=29; sky_aegir_material_readback_required=13; tool_placeholder_material_missing_pbr_roles=12
- First action: Run no-mutation Unity material readback only after process gate clears; map exact renderer/material/texture slots before any authoring or import.
- Required proof: Unity material readback; texture import readback for changed maps; material family/channel manifest; h8_1475 screenshots; Frame Debugger or Stats; Console; memory/VRAM evidence.
- Forbidden shortcut: No raw YAML edits; no Crest wrapper or material clone; no foam.png/proxy/blockout/default/null material promotion; no darkness/fog/bloom cover.

### product_face_prefab_primitive_replacement

- Source table: `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` (39 rows).
- Primary blockers: BUILTIN_PRIMITIVE_MESH_REF plus NO_STATIC_LODGROUP_TOKEN in 39 rows; Player.prefab has 17 static primitive refs; product-face tools/items/resources/transport/sky/ocean lanes blocked.
- First action: Run Prefab Stage or scoped Editor API readback of mesh refs, renderer paths, material refs, LODGroup state, colliders, anchors, scripts, and scene overrides.
- Required proof: Prefab readback; authored/offline-generated mesh report; LOD0/LOD1/LOD2 counts; COL_* collider report; material report; silhouette/final/wire/collider/LOD screenshots; Console; Frame Debugger/Stats.
- Forbidden shortcut: No primitive visible mesh with better material; no camouflage by placement/fog; no raw prefab YAML; no MeshCollider truth on visible LOD0.

### audio_routing_import_source_remediation

- Source table: `AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.csv` (6 rows).
- Primary blockers: MusicDirector _musicMixerGroup null=1; _stingerMixerGroup null=1; current Player.prefab P0 direct refs=0, with prior dive_splash and Underwater Ambient fields source-cleared but pending Unity prefab readback and playback/absence proof.
- First action: Unity-read MusicDirectorConfig_Global.asset and Player.prefab direct refs; classify each retained cue by owner, cue id/hash, load phase, release phase, playback route, fallback, and Addressables or fixed-startup exception.
- Required proof: Config/prefab readback; import readback; runtime MusicDirector and player cue capture; listening notes; owner ledger; Addressables or exception route; Profiler/GCMonitor 0 B/frame; memory/residency proof for retained long beds.
- Forbidden shortcut: No mixer routing claim from static refs; no runtime readiness from prefab serialization; no generic streaming SFX; no string cue route; no MasterAudio shortcut; no raw YAML prefab/mixer/import mutation.

### h8_1475_no_mutation_readback_scope

- Source table: `H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` (123 rows).
- Primary blockers: Readback manifest domains: player_hud=23; crest_ocean=16; terrain_material=14; product_face=13; sky_aegir=12; screenshots=9; no_mutation=8; proof_packet=7; process_gate=4; dirty_state=4; frame_debugger_stats=4; scalability=4; console_log=3; runtime_claims=2.
- First action: Wait for clean process gate; create h8_1475 proof packet folder; perform read-only Unity/Editor readback; abort on dirty/save/import/build mutation pressure.
- Required proof: manifest.json; manifest.sha256; copied Unity log; screenshots; Console; Frame Debugger or Stats; exact readback fields; dirty-state report; no-mutation statement.
- Forbidden shortcut: No scene or prefab save; no import; no Addressables build; no project settings; no material assignment; no wrapper; no acceptance from manifest existence alone.

### visual_reference_capture_gap_closure

- Source table: `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` (7 rows).
- Primary blockers: Status REJECTED/PENDING_H8_1475_PROOF=6; REJECTED/H8_1475_MISSING=1; requirements water volume, shoreline foam/contact, Aegir/sky, terrain truth, underwater density, HUD/cockpit, proof packet validity.
- First action: Use h8_1475 readback packet after clean gate; capture canonical views from required reference gaps; compare against mandatory reference floor before any visual promotion.
- Required proof: Canonical screenshot set; manifest and checksum; copied Unity log; Console; Frame Debugger/Stats where relevant; explicit pass/fail comparison against reference requirements.
- Forbidden shortcut: No dark grading, fog, bloom, crop, storm window, flora/rock camouflage, or screenshot-only staging that hides weak base water/sky/terrain/prefab art.

## Continuous Quality Consequences

- Low/compact: preserve bright surface, waterline, sky/Aegir, terrain, HUD, route silhouettes, and cue readability with bounded residency; no primitive, proxy, flat, muddy, or dark-cover fallback.
- Middle: use route-owned PBR/material/prefab/audio stacks only after readback and import proof; keep LOD, collision, and cue ownership stable.
- High: spend recovered budget on richer contact detail, water volume, Aegir/cloud depth, material response, near-field dressing, audio transition discipline, and longer LOD residency after proof.
- Ultra: add capture-grade overdetail only after low/middle proof fields are complete; gameplay truth, save identity, owner route, DTO layout, Addressables keys, and cue IDs do not change.

## Regression Model

- CPU: static dispatch map only; no runtime CPU change.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory/VRAM: source rows and proof requirements only; no residency proof.
- Cadence: no runtime cadence changed.
- Correctness: owner dispatch is clearer; Unity/runtime/visual/audio acceptance remains blocked by clean-gate readback and proof packets.

Final status: `PENDING VERIFICATION`.
