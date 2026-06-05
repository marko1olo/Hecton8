# Asset Worker Board - 2026-06-05

Status: `ACTIVE`.
Evidence class: `STATIC_DOC`.
Current front: textures, music/audio, meshes, prefabs, materials.
First-20 route moment: first surface exit, bright surface readability, photic shallows, and medium-depth hero route asset proof.

This board is orchestration state, not Unity acceptance. It does not prove import settings, material binding, Addressables residency, runtime mix, VRAM, GC, Frame Debugger, or visual quality.

## Process Gate

- Latest refresh after context resume found no `Unity`, `dotnet`, `csc`, `MSBuild`, `ShaderCompiler`, `ILPP`, or `PackageManager` process in the direct process-name filter.
- CPU counter sample returned `36.02`, `57.50`, then `100.00`.
- Unity/build/import/Play Mode work remains blocked until a fresh sample is clean and no compiler/import process is active.
- Later gate sample after 3219 integration: CPU samples `16`, `14`, `31`; no direct Unity/dotnet/csc/ILPP/ShaderCompiler/PackageManager process. Unity-MCP resources are not exposed to this controller session, so controller did not execute Unity readback.
- Current blocked gate after 3220/3221 integration: CPU sample `76`; active `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler` processes. No Unity/build/import/Play Mode action is allowed from controller.
- Latest Unity readback preflight after packet 10/11/12 integration: CPU sample `73`; no listed Unity/dotnet/compiler/import process. Unity launch/readback remained blocked by CPU policy.
- Latest static-matrix validation gate: CPU sample `100`; active `dotnet`, `Unity`, `Unity.ILPP.Runner`, and `UnityPackageManager`. Unity/build/import/readback remains blocked.
- Latest mesh/prefab matrix validation gate: CPU sample `100`; active `dotnet`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity/build/import/readback remains blocked.
- Latest post-resume asset packet integration gate: CPU sample `39`; no direct `Unity`, `dotnet`, `csc`, `MSBuild`, `Unity.ILPP.Runner`, `UnityPackageManager`, or `UnityShaderCompiler` process in the filtered sample. Static packet integration was completed before any Unity readback attempt.
- Latest post-integration validation gate: CPU sample `100`; active `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity/build/import/readback is blocked.
- Product-face validator launch attempt was aborted by preflight: first sample CPU `51` with no listed busy process; second sample CPU `80` with active `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. No Unity validator log was produced by this controller.
- Integrated generated source pack file inventory: `Docs/AssetAudit/GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.md/.csv`. It maps 26 files under `Docs/GeneratedAssets/AssetSystem_20260605`; 21 rows remain `NOT_IMPORT_READY`.
- Integrated Boyle, Mill, and Averroes static packets 21/22/23. They are owner routing packets only; no Unity/import/prefab/audio runtime proof was produced.
- Local controller completed the token-failed matrix wave: material file technical properties, model import risk matrix, texture duplicate/hash matrix, and audio loudness/source dynamics matrix. Static CSV parse after integration: 29 files, 2094 rows, zero empty cells.
- Local controller completed the static GUID reference graph: `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md/.csv`. CSV parses as 7420 rows.
- Local controller completed active-route GUID triage: `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md/.csv`. CSV parses as 800 rows. Current curated static CSV parse after GUID, P0 target-table, file-map, routing-synthesis, owner-packet-index, visual-reference critique, current visual-reference path continuity, VREF-to-owner requirement matrix, visual source promotion execution queue, h8_1475 shotlist/proof dependency graph, h8_1475 proof-tool integrity blocker routing, visual proof capture guardrail validation, foam-contact decision queue validation, visual reference-vs-current rejection matrix, premium approximation rename triage, audio P0 execution refinement, audio route owner matrix, audio mix-priority decision queue, audio critical cue coverage matrix, Batch31 channel-semantics decision queue, foam-contact source role decision queue, VFX DataVault source-context correction, VFX repair anchor map, DataVault execution-surface recheck, Biolum black-box route decision, visual hero source coverage matrix, large source owner review, product-face execution refinement, waveform stats, static audio ledger, audio remediation matrix, and early owner packet map integration is 62 files, 14648 rows, zero empty cells. Sparse Batch31 import-intent CSV remains outside that zero-empty set.
- Local controller completed unreferenced GUID cleanup-review triage: `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md/.csv`. CSV parses as 3488 rows. It is not deletion authorization.
- Integrated target-table wave for owners 24-33: product-face material P0 rows `124`, prefab P0 rows `39`, audio P0 rows `6`, `h8_1475` readback manifest rows `123`, visual capture gaps `7`.
- Integrated owner packets 34-37 and P0 target-table routing synthesis: active route triage packet, unreferenced cleanup-review packet, h8_1475 proof execution packet, anti-false-proof alignment packet, and `ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md/.csv`.
- Integrated owner packet index: `ASSET_OWNER_PACKET_INDEX_20260605.md/.csv`, 37 rows, with 32 present packet files and output-only IDs 29-33.
- Current post-compaction process gate: CPU sample `76`; active `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity readback/import/build/Play Mode remains blocked.
- Latest post-synthesis process gate: CPU sample `8`; active `dotnet`, `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity readback/import/build/Play Mode remains blocked.
- Latest post-validation process gate: CPU sample `50`; active `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity readback/import/build/Play Mode remains blocked.
- Integrated visual-reference critique checklist: `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md/.csv`, 7 rows, mandatory h8_1475 screenshot reject gates only. It is not visual acceptance.
- Integrated h8_1475 proof dependency graph: `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md/.csv`, 14 rows, no-mutation proof execution order only. It is not Unity proof.
- Integrated audio P0 static execution refinement: `Docs/Reports/AssetSystem_20260605/AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv`, 6 rows, row-level execution order only. It is not runtime mix or listening proof.
- Integrated large source owner review: `Docs/AssetAudit/LARGE_SOURCE_OWNER_REVIEW_20260605.md/.csv`, 5 buckets for large texture/audio source risk. It is not deletion, import, or runtime residency proof.
- Integrated product-face static execution refinement from Planck `019e988f-aa0f-7d10-a626-cc6f57db3548`: `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv`, 14 rows, no Unity proof or visual acceptance.
- Integrated Pauli `019e9890-0a11-72e1-9e42-ca8163629f82` gap scan result: clean scoped CSVs curated; sparse/older sidecars remain excluded.
- Current post-resume process gate: CPU sample `83`; active `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Unity readback/import/build/Play Mode remains blocked.
- Current audio-route/Batch31 reconciliation gate: CPU sample `100`; active `Unity`, `Unity Hub`, and `mcp-for-unity`. Unity readback/import/build/Play Mode remains blocked by CPU policy even without a listed compiler process in the sample.
- Current visual hero/Biolum/VFX static reconciliation gate: CPU sample `59`; active `dotnet` and `Unity Hub`. Unity readback/import/build/Play Mode remains blocked.

## Active Workers

| ID | Agent | Scope | Write Scope | Status |
|---|---|---|---|---|
| 3212 | Archimedes `019e94f3-4183-7cb2-b92d-b718892e688e` | Texture authoring manifest | `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md` | `INTEGRATED_CLOSED` |
| 3213 | Mencius `019e94f3-8f96-7211-b977-dca619f8333a` | Audio ledger and listening-risk report | `Docs/Audio/audio_asset_ledger.csv`; `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md` | `INTEGRATED_CLOSED` |
| 3214 | Ohm `019e94f3-e639-7d13-952b-128d8fd01ace` | Mesh/prefab static promotion table | `Docs/Reports/AssetSystem_20260605/MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md` | `INTEGRATED_CLOSED` |
| 3215 | Nash `019e94f4-3e9e-7dd2-b6e9-a9bf8c92f96f` | Material-readback static preflight blockers | `Docs/Reports/AssetSystem_20260605/MATERIAL_READBACK_PREFLIGHT_STATIC_BLOCKERS_3215_20260605.md` | `INTEGRATED_CLOSED` |
| 3216 | Plato `019e94fc-73b5-7fc3-bdac-b94c1a8c51f4` | UI sprite route static table | `Docs/Reports/AssetSystem_20260605/UI_SPRITE_ROUTE_STATIC_TABLE_3216_20260605.md` | `INTEGRATED_CLOSED` |
| 3217 | Hegel `019e9502-b014-7c60-9ac6-9a50a4682b6d` | Audio import-policy decision brief | `Docs/Reports/AssetSystem_20260605/AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md` | `INTEGRATED_CLOSED` |
| 3218 | Chandrasekhar `019e9503-0a8a-7850-9cfe-850a2673d0fb` | Addressables static coverage gap | `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md` | `INTEGRATED_CLOSED` |
| 3219 | Parfit `019e9509-f4af-7132-a350-a71c5a8c55c0` | Unity readback execution packet | `taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md` | `INTEGRATED_CLOSED` |
| 3220 | Banach `019e9513-8262-75b3-a7a2-64665c3961a0` | Addressables group plan | `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_GROUP_PLAN_3220_20260605.md` | `INTEGRATED_CLOSED` |
| 3221 | Nietzsche `019e9513-eca3-7d31-8be4-bd8e97e718c2` | Audio policy adoption draft | `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_ADOPTION_DRAFT_3221_20260605.md` | `INTEGRATED_CLOSED` |
| local-addressables | Wegener `019e9516-5e9e-71d0-a2a6-be59bf6b47fe` | Addressables asset-group plan | `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |
| local-audio-policy | Bacon `019e9516-a7df-77d3-9fb6-b3ed0f0399c9` | Audio import-policy exception table | `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |
| 3222 | Dirac `019e951e-b80b-75a0-a960-3f8cea5f8ccc` | Asset planning consolidation | `Docs/Reports/AssetSystem_20260605/ASSET_PLANNING_CONSOLIDATION_3222_20260605.md` | `INTEGRATED_CLOSED` |
| local-visual-mesh-taxonomy | Einstein `019e9529-5524-7600-a82b-ecfcab0b1c45` | Visual/mesh asset taxonomy | `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |
| local-audio-taxonomy | Fermat `019e9529-bc2d-72e1-826e-8068a02e64bd` | Audio/music asset taxonomy | `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |
| local-audio-profile-matrix | Harvey `019e9534-1c22-7161-8b90-36c092924892` | Audio profile/cue route matrix | `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |
| local-texture-family-matrix | Epicurus `019e9534-86b1-7092-a3ea-7fda4e288027` | Texture/material family route matrix | `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md`; `.csv` | `INTEGRATED_CLOSED` |

## Local Controller Scope

- Maintain this board and `Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md`.
- Scan current asset docs/reports for proof-language overclaim.
- Integrate worker outputs only after static review.
- Do not duplicate worker write scopes.

## Local Controller Output

- Created `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_HYGIENE_SWEEP_20260605.md`.
- Proof-language sweep found no banned static-proof wording, runtime-ready, Unity-verified, visual-pass, or final-acceptance claim in the scanned asset-front scope.
- Audio ledger snapshot after 3213: 138 rows; all rows remain `PENDING_OWNER`; `addressable_group` and `addressable_key` remain `PENDING_ADDRESSABLES` for all rows; classes are 84 music, 30 sfx, 12 ambient, 5 ui, 5 player_loop, 2 voice.
- Texture disposition snapshot: 49 material-proof blocked, 6 readback-blocked, 10 clean-PBR-needed, 50 source-only, 1 rejected visible support, 1 source prototype, 7 UI atlas-proof pending, 66 unassigned static source.
- Integrated 3212 texture manifest after downgrading one static-only line from blocker `removed` to blocker `addressed`; post-patch forbidden-claim scan and diff whitespace check passed.
- Integrated 3213 audio ledger/report: `sfx_or_player_loop` split into `sfx` and `player_loop`; policy conflict remains unresolved; no runtime/mix/import acceptance claim accepted.
- Integrated 3214 mesh/prefab table after downgrading one static-only line from blocker `removed` to blocker `addressed`; scan hits remaining are negative/future-proof wording only.
- Integrated 3215 material-readback preflight after downgrading one static-only line from readback ambiguity `removes` to `addresses`; report remains `PENDING UNITY READBACK`.
- Added `taskslocal/asset_system_20260605/ASSET_OWNER_05_UI_SPRITE_ROUTE.md` and spawned 3216 for the P1 UI sprite route blocker.
- Integrated 3216 UI sprite route table: `oxygen-tank.png` is a mask/silhouette referenced by `Suit_HUD_Canvas.prefab`; `ui/OXYGEN.png` is the detailed oxygen icon candidate but is not proven bound; no SpriteAtlas proof under `Assets/_Project`.
- Spawned 3217 for the unresolved audio import/load policy decision brief.
- Spawned 3218 for static Addressables coverage gap mapping.
- Integrated 3217 audio import-policy brief: recommendation is a hybrid exception table; no import edits authorized until stable-doc adoption and proof.
- Integrated 3218 Addressables gap report after downgrading one static-only line from `removes` to `addresses`; `Assets/AddressableAssetsData` exists but has 0 files and no static settings/group/catalog evidence.
- Spawned 3219 for a no-mutation Unity readback execution packet. This does not authorize running Unity while gate is busy.
- Integrated 3219 Unity readback execution packet and closed Parfit. Packet order: scene/sky/Aegir/moons; Crest/ocean/foam; terrain/geology; flora proxy materials; UI oxygen sprites; audio config/prefab refs; Addressables settings/data. Packet is not proof and authorizes no Unity run while the gate is blocked.
- Spawned 3220 Banach for static Addressables group planning. Write scope is a new report only; no Addressables settings/groups/keys may be created.
- Spawned 3221 Nietzsche for audio import/load policy adoption draft. Write scope is a new report only; no stable authority or import settings may be changed.
- Later controller wait found 3220/3221 agent IDs `not_found`; board status downgraded to stale, not active proof.
- Later subagent notifications returned both outputs. Controller closed 3220/3221, reviewed the reports, downgraded one `removes` phrase in 3220 to `addresses`, and reduced 3221 status wording to `PATCH BASIS`. No stable authority, import settings, or Addressables settings were changed.
- Spawned local-addressables Wegener for `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md/.csv`.
- Spawned local-audio-policy Bacon for `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md/.csv`.
- Added `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md/.csv` locally. Matrix parses as 13 rows across P0/P1/P2 and six texture families. Static planning only; no import settings changed.
- Closed local-addressables Wegener. Output remains static plan only; `Assets/AddressableAssetsData` still has no settings/group/catalog proof and no runtime handle/ref-count/release proof.
- Closed local-audio-policy Bacon. Output remains `PENDING_AUTHORITY_DECISION`; no stable authority, import settings, runtime mix, listening pass, Memory Profiler, or `0 B/frame` proof exists.
- Spawned 3222 Dirac to consolidate duplicate Addressables/audio/texture planning artifacts into one future-owner handoff. Write scope is a new static report only.
- Integrated 3222 consolidation and closed Dirac. Controller disposition: AssetAudit Addressables CSV is the row queue, 3220 is lifecycle/proof/naming guidance; audio exception CSV is the row queue, 3221 is patch-basis text only; texture import role matrix blocks source-only texture promotion until Unity/material proof exists.
- Added `Docs/AssetAudit/README.md` as the current asset-front start-here navigator.
- Integrated local-audio-taxonomy Fermat output and closed the worker. CSV parses as 11 rows with evidence classes limited to `STATIC_DOC`, `STATIC_SOURCE`, and `AUDIO_WAVEFORM_QA`.
- Integrated local-visual-mesh-taxonomy Einstein output and closed the worker. CSV parses as 19 rows with evidence classes limited to `STATIC_DOC`, `STATIC_SOURCE`, and `STATIC_IMAGE_QA`.
- Added `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md` to map contact sheets, waveform sheets, generated source packs, and taxonomy artifacts without treating them as acceptance.
- Added `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.md/.csv`. CSV parses as 13 rows and maps existing offline/editor tools; no tool was executed.
- Integrated local-audio-profile-matrix Harvey output and closed the worker. CSV parses as 21 rows with evidence classes limited to `STATIC_DOC`, `STATIC_SOURCE`, and `AUDIO_WAVEFORM_QA`.
- Integrated local-texture-family-matrix Epicurus output and closed the worker. CSV parses as 10 rows with evidence classes limited to `STATIC_DOC`, `STATIC_SOURCE`, and `STATIC_IMAGE_QA`.
- Added `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md/.csv` locally. CSV parses as 7420 rows and maps static GUID reachability for texture, audio, material, model, prefab, scene, vendor-path, and Addressables owner routing. This is not import, runtime, visual, or audio acceptance proof.
- Added `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md/.csv` locally. CSV parses as 800 rows and condenses the GUID graph to P0/P1 active route owner lanes. This is not import, runtime, visual, or audio acceptance proof.
- Added `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md/.csv` locally. CSV parses as 3488 rows and isolates unreferenced cleanup-review candidates with explicit no-deletion authority. This is not import, runtime, visual, audio, or safe-delete proof.

## Action Queue Snapshot

- Source: `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`.
- Priority counts: P0 = 4, P1 = 5, P2 = 2.
- P0 blockers:
  - `water_visual`: rejected foam source serialized-reachable through active world/ocean users.
  - `flora_materials`: `WorldProceduralProxy` flora/coral/kelp materials serialized in active world scene.
  - `audio_routing`: `MusicDirectorConfig_Global.asset` has null music and stinger mixer groups.
  - `audio_lifecycle`: `Player.prefab` has direct AudioClip refs without Addressables/release proof.

## Hard Rejections

- No edits under `Assets/` in this static asset-system phase.
- No raw YAML mutation of `.mat`, `.prefab`, `.unity`, or `.asset`.
- No Crest material clone, wrapper, or runtime override.
- No `VERIFIED`, `READY`, `COMPLETE`, `0 GC`, runtime mix, visual acceptance, or Addressables readiness claims from static scans.
- No promotion of `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` into visible route content.

## Current Integration Checks

- 3212: route-owned pack specs include source paths, channel packing, compression, streaming mips, material slot target, risks, and Low/Middle/High/Ultra consequences.
- 3213: ledger keeps pending owner/Addressables fields and separates unresolved policy from import edits.
- 3214: static prefab table distinguishes candidate pools from hard-rejected visible route pools.
- 3215: readback checklist is exact enough for a future Unity owner and contains no acceptance language.
- 3219: execution packet contains gate checks, artifact paths, read-only actions, reject conditions, no-save/no-apply rules, and stop conditions; runtime proof remains absent.
- local-audio-direct-ref: owner packet maps 28 `Player.prefab` direct `AudioClip` refs to required future classifications; no prefab edit or runtime proof exists.
- local-texture-import-blockers: owner packet maps 109 texture/material blocker rows to safe future execution; no import/material edit or visual proof exists.
- local-musicdirector-routing: owner packet maps null MusicDirector mixer refs, long-bed risks, repeated stingers, and warning-priority gates; no config edit or runtime mix proof exists.
- local-water-foam-contact: owner packet maps rejected reachable `foam.png` into required offline authoring and Unity proof gates; no import/material/screenshot proof exists.
- local-flora-proxy-material: owner packet maps four active-world `WorldProceduralProxy` flora/coral/kelp material blockers into Unity-safe replacement gates; no material/prefab/scene edit or runtime proof exists.
- local-audio-source-folder: folder matrix maps 15 audio source folders, 138 ledger rows, and 28 direct prefab refs by folder; no runtime mix/listening proof exists.
- local-texture-source-folder: folder matrix maps 56 texture source folders, 190 ledger rows, 50 generated/source-only rows, 54 active-build-scene usage rows, 70 visible-route user rows, and 43 proxy/placeholder usage rows; no import/material/screenshot proof exists.
- local-mesh-prefab-source-folder: folder matrix maps 40 prefab folders and 602 prefabs; 221 lack static `LODGroup` token, 183 have built-in primitive mesh refs, and 76 have static `MeshCollider` token; no Unity prefab/material/LOD proof exists.
- local-product-face-prefab: owner packet maps visible product-face primitive mesh replacement gates for construction, held tools, item tools, pickups, transport, buildings, world support, and root prefabs; no prefab/material/Unity proof exists.
- local-sky-aegir-cloud: owner packet maps skybox, Aegir, cloud, moon, importer, screenshot, Frame Debugger, memory, and rejection gates; no Unity route proof exists.

## 2026-06-05 Late Asset Packet Workers

| Worker | Agent ID | Scope | Output | Status |
|---|---|---|---|---|
| Heisenberg | `019e9549-7db2-7a22-adb4-cbcb69036b7a` | Audio direct-ref unwiring packet | `taskslocal/asset_system_20260605/ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Arendt | `019e9549-e5b4-7863-b1db-3fa86ffc1f2d` | Texture/material import blocker packet | `taskslocal/asset_system_20260605/ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Lorentz | `019e954e-529e-71d1-9075-0f6c8406e26c` | MusicDirector audio routing packet | `taskslocal/asset_system_20260605/ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Confucius | `019e954e-b30c-7993-a291-6afbf1fee5d6` | Water foam/contact authoring packet | `taskslocal/asset_system_20260605/ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Gauss | `019e9550-4cc6-70b0-b0df-6a5c6453a88c` | Flora/coral proxy material replacement packet | `taskslocal/asset_system_20260605/ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Volta | `019e9562-346f-7fa0-86f6-f12edf6b4225` | Product-face primitive prefab replacement packet | `taskslocal/asset_system_20260605/ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Franklin | `019e9562-a7f8-7b83-abb0-c0cc883e2165` | Sky/Aegir/cloud source slot proof packet | `taskslocal/asset_system_20260605/ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Banach | `019e956e-6a23-7e20-83a0-e78ae2b8badc` | Addressables asset group execution blocker packet | `taskslocal/asset_system_20260605/ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Plato | `019e956e-fcd2-7423-9163-e612fa9d1971` | Terrain/geology PBR authoring packet | `taskslocal/asset_system_20260605/ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Hubble | `019e956f-7baf-7373-b101-77c3796754a1` | UI oxygen sprite atlas route packet | `taskslocal/asset_system_20260605/ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Huygens | `019e9576-c1df-7333-9087-a67f234c6586` | Product-face validator evidence packet | `taskslocal/asset_system_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Nietzsche | `019e9577-2c48-75e1-8814-cd35b2cadaac` | Audio import authority adoption packet | `taskslocal/asset_system_20260605/ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Turing | `019e9577-9686-7343-8732-22bfa15f0a1b` | Ocean/Crest contact proof packet | `taskslocal/asset_system_20260605/ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Boyle | `019e9585-5b65-73e1-85aa-278171c8c616` | Texture streaming/mip static risk packet | `taskslocal/asset_system_20260605/ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Mill | `019e9585-b70e-71e0-bee4-5a57faf82deb` | Prefab collider/LOD row risk packet | `taskslocal/asset_system_20260605/ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Averroes | `019e9586-204e-7011-b06a-6f58066cb709` | Audio source technical remediation packet | `taskslocal/asset_system_20260605/ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` | `CLOSED_STATIC_PACKET_INTEGRATED` |
| Descartes | `019e9591-b3cd-7340-8eb4-f5d925e42107` | Material serialized risk matrix | `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md/.csv` | `ERRORED_NO_OUTPUT_LOCAL_COMPLETED` |
| Ptolemy | `019e9591-c852-7e22-a962-b99dab70ae97` | Model/FBX import static risk matrix | `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.md/.csv` | `ERRORED_NO_OUTPUT_LOCAL_COMPLETED` |
| Boole | `019e9591-dcf5-79e2-9f8c-f0c524840f82` | Audio loudness/source dynamics matrix | `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.md/.csv` | `ERRORED_NO_OUTPUT_LOCAL_COMPLETED` |
| Meitner | `019e9591-f249-7ab1-9aeb-6d2a3a73eb46` | Texture duplicate/hash route map | `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.md/.csv` | `ERRORED_NO_OUTPUT_LOCAL_COMPLETED` |
