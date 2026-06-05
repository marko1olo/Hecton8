# Asset Owner 07 - Tool And Route Execution Packet

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Owner role: future Unity/tool execution owner for asset-front work.
Scope: choose and run the correct existing asset tool or Unity readback route after the process/tooling gate is clean.

This packet authorizes no action by itself. It is a handoff. It does not prove Unity import state, material binding, visual quality, Addressables residency, runtime audio behavior, memory, frame time, or GC.

## Gate Before Any Execution

Do not run Unity, editor tools, importers, builds, Addressables operations, scene saves, prefab edits, or material edits unless all are true:

- CPU samples are below 50 percent.
- No active `dotnet`, `csc`, `MSBuild`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, or `UnityShaderCompiler` process is busy.
- Unity/MCP or direct Unity editor tooling is available.
- The target scene/prefab/material/import setting is not dirty from another owner.
- A proof output path under `Docs/` is defined before the action.
- The owner can stop without saving if Unity dirties unrelated assets.

Current controller limitation: MCP resources/templates were empty and `.kiro/settings/mcp.json` had no configured MCP servers in this session. If still true, do not claim Unity readback.

## Required First Reads

1. `Docs/AssetAudit/README.md`
2. `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`
3. `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.md`
4. `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md`
5. `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv`
6. `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv`
7. `taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`

## Execution Order

| Order | Front | First Action | Tool/Route | Proof Required |
|---:|---|---|---|---|
| 1 | foam/contact | Read back active Crest/ocean/foam slots | `ASSET_OWNER_06` plus `ShorelineFoamGraftEditorTools` only after readback | Game/Scene screenshots, material slot table, Frame Debugger notes |
| 2 | proxy flora/coral/kelp | Read back active route materials and visible users | `ASSET_OWNER_06`, then flora material/mesh validators | Scene screenshots, final/non-proxy material proof, LOD/silhouette table |
| 3 | MusicDirector/direct refs | Read back mixer refs and direct `Player.prefab` refs | Audio profile route matrix, Unity readback, no import mutation | Config table, prefab ref table, mixer route notes, listening proof plan |
| 4 | sky/Aegir/cloud | Read back skybox/Aegir material slots | `ASSET_OWNER_06`, then sky atlas source route if needed | Bright surface screenshot, material slot table, Frame Debugger notes |
| 5 | terrain/geology | Read back terrain/geology materials and candidate prefabs | Geology final validators after readback | Terrain screenshot, material/texture table, LOD/collider proof |
| 6 | UI oxygen | Read back HUD sprite binding and atlas/import state | UI route table, IconBaker only as source prep | HUD screenshot, sprite/import/atlas table |
| 7 | Addressables | Read current settings/groups/labels only | Addressables plan, no creation until owner decision | Settings/group/catalog table, handle/release plan |

## Tool Selection Rules

- Texture import/meta work starts from `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`; do not use `BatchImportTextures.py --write-meta` unless Unity-created metas exist and the owner has approved exact targets.
- Source texture cleanup starts under `Docs/GeneratedAssets`; do not write temporary art into `Assets`.
- Sky/water editor tools are allowed only after readback proves exact active slots; do not wrap or clone Crest materials.
- Flora/geology/prefab tools are replacement/proof candidates, not proof themselves.
- Audio tools and waveform sheets prioritize owners; they do not prove mix, latency, or audio-thread safety.
- Addressables plans cannot create settings/groups/keys without stable owner decision and Unity proof.

## Required Proof Format

Every execution output must state:

- What was touched or read.
- Whether `Assets` changed. If yes, exact path list and owner reason.
- Evidence class.
- P0/P1 route blocker addressed or still blocked.
- Low/Middle/High/Ultra consequence.
- Regression model: CPU, GC, memory/VRAM, cadence, correctness.
- Runtime proof absent or present, with artifact paths.

## Stop Conditions

Stop and report without saving if:

- Unity compile/import starts.
- CPU/process gate turns red.
- Unity marks unrelated assets dirty.
- Target material/prefab/scene needs a public API or project-setting change.
- Crest material route would require a runtime wrapper/clone.
- The visual route would rely on darkness, fog, bloom, or low-detail proxy content.

Final status: `PENDING VERIFICATION`.
