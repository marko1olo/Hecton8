# H8 1475 Proof Dependency Graph - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: dependency order for future no-mutation `h8_1475` proof execution across player/HUD, sky/Aegir, Crest/ocean, shoreline, terrain, product-face prefab/material blockers, and P0 audio routing.

CSV companion: `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.csv`.

This graph is not Unity proof, visual acceptance, runtime mix proof, profiler proof, GC proof, memory proof, or build proof. It exists to stop future owners from running the `h8_1475` packet out of order or treating static tables as acceptance.

## Read Before Use

- `taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.md/.csv`
- `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.md/.csv`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md/.csv`
- `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md/.csv`
- `Docs/Reports/RuntimeSystem_20260605/ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md/.csv`

## Dependency Order

| Seq | Dependency node | First owner route | Required artifact | Reject condition |
|---:|---|---|---|---|
| 01 | process_gate_preflight | `ASSET_OWNER_36` | `process_gate.md` | CPU high, busy Unity/import/compiler/build process, or ambiguous state. |
| 02 | static_input_pack | `ASSET_OWNER_36` | `manifest.json` static input list | Missing named input without `MISSING_STATIC_INPUT` note. |
| 03 | proof_root_and_manifest | `ASSET_OWNER_36` | `h8_1475_<session>/manifest.json` | Missing proof root, fake hash, or orphan artifact list. |
| 04 | no_mutation_guard | `ASSET_OWNER_26` | `dirty_state_audit.md` | Any dirty scene, prefab, material, importer, package, or project state. |
| 05 | player_hud_binding_readback | `ASSET_OWNER_36` | player/HUD screenshot, active scene conflict reconciliation, and readback rows | Missing production player binding, unresolved active shell authority, active interactive `ScreenSpaceOverlay` HUD route, or flat nondecision HUD. |
| 06 | sky_aegir_cloud_slot_readback | `ASSET_OWNER_14` | sky/Aegir slot inspector screenshot | Stale/null/ignored slots, orbit-only proof, muddy or smeared Aegir. |
| 07 | crest_ocean_waterline_readback | `ASSET_OWNER_20` | Crest/ocean slot inspector screenshot | Crest clone/wrapper, flat water, repeated foam, or missing active OceanRenderer. |
| 08 | terrain_material_route_readback | `ASSET_OWNER_16` | terrain material slot inspector screenshot | Stale terrain material, crushed/noisy/flat terrain, or fog cover. |
| 09 | product_face_prefab_blocker_readback | `ASSET_OWNER_25` | primitive target inspector screenshot | Visible primitive, blockout, null/package-default/proxy material, or missing LOD. |
| 10 | product_face_material_blocker_readback | `ASSET_OWNER_24` | material blocker readback rows | `foam.png` final use, proxy material, null slot, missing PBR role, or package default. |
| 11 | audio_p0_route_readback | `ASSET_OWNER_28` | audio route rows plus console export | Null MusicDirector mixer or direct Player AudioClip route without owner/release proof. |
| 12 | canonical_screenshot_capture | `ASSET_OWNER_36` | required `h8_1475_*.png` set | Missing canonical screenshot or raw MCP PNG substitution. |
| 13 | frame_stats_profiler_boundary | `ASSET_OWNER_36` | `frame_debugger_stats.md` | Missing render-route artifact without abort note or fake runtime numbers. |
| 14 | final_packet_triage | `ASSET_OWNER_36` | final proof execution report | Acceptance claim without artifacts or any dirty mutation risk. |

## Rules

- If row 01 fails, stop. Do not launch Unity or run readback.
- If row 04 fails, stop. Do not save or repair the dirty object.
- Rows 05-11 are readback and triage only. They do not authorize material assignment, prefab apply, texture import, Addressables changes, or audio import edits.
- Row 12 must use canonical packet screenshots. Raw `Docs/Screenshots/MCP/*.png` files are rejected as acceptance proof.
- Row 13 can only prove render-route evidence. It cannot prove `0 B/frame`, runtime memory, save/load, platform readiness, or build health.
- Row 14 may end as `PENDING_VERIFICATION` or `REJECTED`. It may not claim final readiness without matching Unity, Console, visual, profiler, GC, and memory artifacts.

## Regression Model

- CPU: static graph only; no runtime CPU change.
- GC: no runtime code touched; no `0 B/frame` claim.
- Memory/VRAM: no residency proof; the graph only names future proof artifacts.
- Cadence: no runtime cadence changed.
- Correctness: the dependency order reduces false promotion risk by forcing one proof artifact per owner route.
- Visual: surface, sky/Aegir, ocean, shoreline, photic terrain, HUD, and shallow underwater route remain rejected or pending until canonical screenshots pass the critique checklist.

Final status: `PENDING VERIFICATION`.
