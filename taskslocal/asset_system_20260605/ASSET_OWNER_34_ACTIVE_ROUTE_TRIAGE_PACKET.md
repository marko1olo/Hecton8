# Asset Owner 34 - Active Route Triage Packet

ID: `ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET_WRITER`.
Role: static asset route execution packet writer.
Project: `C:\hades\Hecton8`.
Status: `STATIC/PENDING UNITY READBACK`.
Evidence class: `STATIC_DOC + STATIC_SOURCE + STATIC_YAML_SCAN + STATIC_IMAGE_QA + UNITY_BATCHMODE_LOG`.

This packet is directly distributable to a future Unity/readback or asset-owner agent. It triages ACTIVE GUID routes and connects them to P0 material/prefab blockers. It is not Unity proof, runtime proof, visual proof, import proof, profiler proof, GC proof, memory proof, or readiness.

## Source Evidence

- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv`
- `Docs/AssetAudit/PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.md`
- `Docs/AssetAudit/PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv`
- `Docs/AssetAudit/PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.md`
- `Docs/AssetAudit/PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv`
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.md`
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`

Static facts from the evidence:

- `ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv`: 800 triage rows, 655 `P0_ACTIVE_ROUTE_REVIEW`, 145 `P1_SCENE_ROUTE_REVIEW`.
- Owner lanes: `active_world_scriptable_route_owner` 246, `prefab_scene_route_owner` 206, `third_party_or_legacy_integrity_owner` 146, `active_world_material_readback_owner` 136, `asset_route_assignment_owner` 32, `audio_direct_ref_owner` 25, `texture_material_streaming_owner` 8, `mesh_prefab_model_owner` 1.
- Asset families: 340 scriptable/native assets, 206 prefabs, 197 materials, 22 audio, 16 shader/compute, 6 UI/sprite textures, 5 fonts, 3 audio UI, 2 textures, 2 animation/controller, 1 model/mesh source.
- Product-face material table: 124 P0 target rows. Top blockers include foam/contact, `WorldProceduralProxy` flora/coral/kelp contamination, placeholder/blockout/default routes, null or empty texture-role routes, and sky/Aegir readback targets.
- Product-face prefab table: 39 P0 rows. Static counts include 56 built-in primitive refs and 39 rows missing static `LODGroup` tokens. This is STATIC/PENDING UNITY PREFAB READBACK, not acceptance.
- `h8_1475` manifest domains include player/HUD, sky/Aegir, Crest/ocean, terrain/material, product-face, screenshots, no-mutation, proof packet, Frame Debugger/Stats, process gate, dirty state, console/log, scalability, and runtime-claim guards.
- Visual reference report decision: current product-face visual promotion is `REJECTED / PENDING UNITY PROOF`; existing MCP screenshots are diagnostic only and are not acceptance artifacts.

## Why Still Needed

The active GUID triage is broad enough to cause agent drift if sent as a raw CSV. The next owner must map active route rows to concrete P0 blocker families before touching Unity or assets. The current evidence proves referenced routes and failed source gates only. It does not prove active renderer slots, live scene bindings, material effective properties, import settings, LOD state, collider truth, Addressables residency, visual floor, SetPass/batch cost, GC, frame time, or memory.

First-20-minutes route moment improved: bright first surface exit and early route trust across player/HUD, sky/Aegir, ocean/Crest surface, shoreline, photic terrain, tools, resources, transport, flora/coral, and route-readable material identity.

Route blocker removed by this packet: owner ambiguity. Runtime readiness remains blocked.

## Authority Docs

Future owner must obey:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md` only for task-packet/reporting shape; it is not a runtime bible.
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `rendering.md`
- `shaders.md`
- `water.md`
- `streaming.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`

Mandates followed by this packet:

- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Owned Scope For Future Owner

Inspection scope:

- Listed source evidence files.
- The rows and assets named by those CSVs.
- Unity readback only when process gate is clean.
- Existing owner packets referenced by the audit, especially material repair, prefab replacement, audio remediation, and no-mutation readback packets.

Mutation scope:

- None for a readback owner.
- For a repair owner, only the explicit target assets confirmed by Unity readback and owned by the matching material/prefab/audio/source route.
- No raw YAML edits.
- No shared index/summary edits unless explicitly assigned.
- No deletion.

## Safety Overrides

- No deletion. This triage contains P0/P1 referenced rows. Do not delete `.cs`, `.shader`, `.asset`, `.mat`, `.prefab`, `.unity`, `.meta`, texture, audio, or model files from this packet.
- No source-binding shortcuts. Generated/source textures or meshes are not final binding until cleaned, imported, role-separated, material-bound, read back in Unity, visually captured, and memory/profiler risk is reported.
- No visual promotion without Unity proof. Static docs, CSVs, YAML scans, batchmode logs, and screenshots outside a valid proof packet are not acceptance.
- No Crest wrapper, runtime material clone, material instantiation, override script, or direct `_WD_*` wave-data binding shortcut. Crest canonical asset route only.
- No fog, darkness, bloom, vignette, storm grade, camera crop, rocks, flora, or route placement camouflage to hide bad water, terrain, sky, Aegir, shoreline, primitive meshes, proxy materials, or missing texture roles.
- No raw YAML edits to `.mat`, `.prefab`, `.unity`, `.asset`, or `.meta`.
- No `Resources.UnloadUnusedAssets`, destructive cleanup, broad refactor, package change, ProjectSettings change, or public API change.
- All static-only findings must be reported as `STATIC/PENDING UNITY READBACK`.

## Unity Gate Before Any Unity Work

Unity/readback work must not start unless:

- CPU total is `<= 50 percent`.
- No busy `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, `VBCSCompiler`, package resolution, import, compile, player build, shader compile, save prompt, dirty prompt, or upgrade/import repair prompt exists.
- Unity is closed or idle.

Suggested process gate:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time'
Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,ShaderCompiler,AssetImportWorker,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue
```

If process state is ambiguous, abort and mark `BLOCKED_PROCESS_GATE`. Do not launch Unity to check anyway.

## Execution Tasks

1. Load only the authority docs and evidence files listed in this packet. Acceptance proof: report the exact files read. Fallback: if any required evidence file is missing, mark that file `BLOCKED_MISSING_EVIDENCE` and continue only with existing evidence.
2. Parse `ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv` by `route_priority`, `owner_lane`, `asset_family`, `owner_scope`, and `asset_path`. Acceptance proof: row counts match 800 total, 655 P0, 145 P1, or the report labels any mismatch `STATIC_DELTA/PENDING REVIEW`.
3. Create a working triage table outside `Assets/` under the final proof/report location, not as a shared index edit. Acceptance proof: table records `triage_id`, priority, lane, family, scope, asset path, reference counts, source CSV, and status. Fallback: no table generation if target folder cannot be created outside `Assets/`.
4. Sort P0 rows first by owner lane in this order: material readback, prefab scene route, texture/material streaming, audio direct refs, scriptable/native route, third-party/legacy integrity, route assignment, mesh/model. Acceptance proof: report why each lane is first-20 relevant or not.
5. Mark all rows as `STATIC/PENDING UNITY READBACK` before any Unity step. Acceptance proof: no row may say `VERIFIED`, `ACCEPTED`, `READY`, `0 B/frame`, or runtime clean from static evidence.
6. Cross-link top active material GUID rows to product-face material blockers: `Mat_HectonSky.mat`, `WorldProceduralProxy/MAT_family_*`, `MAT_Aegir*`, `MAT_AegirSky_Master.mat`, Crest `Ocean.mat`, foam/contact, VFX leak plume, radar/instrument pulse, null texture-role rows. Acceptance proof: each row has blocker family and next owner lane.

Checkpoint 1 after task 6: stop if the triage table cannot reproduce source counts or if static rows were promoted beyond `STATIC/PENDING UNITY READBACK`. Report `BLOCKED_STATIC_TRIAGE_INTEGRITY`; do not open Unity.

7. Cross-link active prefab route rows to product-face prefab blockers. Required groups: `Player.prefab`, `Tools/Held`, `Items/Tools`, `Resources/Pickups`, `Transport`, `Sky_System`, `Ocean_Crest`. Acceptance proof: every linked prefab row states built-in primitive risk, missing LODGroup static risk, material route risk, or `PENDING_PREFAB_READBACK`.
8. For `Ocean_Crest.prefab`, separate accepted hidden Crest input primitives from the P0 visible `SargassumMicroFaunaBoids.boidMesh` built-in `Plane` route. Acceptance proof: no task tells an owner to replace Crest hidden data-input primitives without a separate Crest owner decision.
9. For Crest `Ocean.mat`, preserve third-party asset integrity. Acceptance proof: route says inspect/read back canonical material binding and visible slots only; no wrapper, clone, runtime override, or material instantiation.
10. For `WorldProceduralProxy` flora/coral/kelp material rows, mark visible promotion blocked until active renderers, final non-proxy material family, alpha/dither path, LOD/silhouette proof, and import settings are read back. Acceptance proof: all proxy-linked rows remain P0 blocker candidates.
11. For foam/contact rows, require shoreline/ocean visible slot readback and screenshot proof. Acceptance proof: no foam/contact row can be accepted from serialized reachability or texture presence alone.
12. For sky/Aegir rows, require live skybox/scene binding, material slot readback, shader property effectiveness classification, and screenshots. Acceptance proof: no Aegir/sky row is accepted from material names, texture names, or oversized planet composition.

Checkpoint 2 after task 12: if material/prefab links are incomplete, mark remaining rows `PENDING_OWNER_MAPPING` and do not mutate assets. If a future owner needs Unity, run the process gate first.

13. Parse `H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` and attach required readback fields to each blocker family. Acceptance proof: blocker rows cite required domains: player_hud, sky_aegir, crest_ocean, terrain_material, product_face, screenshots, no_mutation, proof_packet, dirty_state, console_log, frame_debugger_stats, process_gate, scalability, runtime_claims.
14. Define the `h8_1475` proof root as `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`. Acceptance proof: packet requirements include `manifest.json`, `manifest.sha256`, `UnityLog.txt`, no-mutation readback report, console export, dirty-state audit, canonical screenshots or `ABORTED_<view>.md`, and Frame Debugger/Stats report.
15. Require `manifest.json` to keep acceptance state `PENDING_VERIFICATION`. Acceptance proof: task output forbids `ACCEPTED` in manifest from readback alone.
16. Require `manifest.sha256` to hash the actual `manifest.json`; no invented hashes. Acceptance proof: report command/path used to generate the checksum.
17. Add canonical screenshot requirements for bright first surface exit, shoreline/waterline, underwater 0.5m/photic shallows, 20-50m route, Aegir/sky, player/HUD/tool view, product-face pickup/transport where relevant. Acceptance proof: missing screenshots require `ABORTED_<view>.md` with reason, not silent omission.
18. Add Frame Debugger/Stats requirements for shader names, material asset paths, keywords, SetPass, batches, material instance count, SRP Batcher/GPU Resident Drawer risk, Crest visible-slot use, and render route proof. Acceptance proof: no visual promotion without this evidence.

Checkpoint 3 after task 18: if `h8_1475` packet cannot be produced completely, final state is `PENDING UNITY PROOF`; do not accept or promote product-face visuals.

19. For texture/material targets, require import readback: sRGB, texture type, compression, mipmaps, streaming mips, max size, read/write state, platform override, role classification, and channel semantics. Acceptance proof: each changed map has albedo/base, normal/detail-normal, packed MRAO/mask, emission/wetness/contact/alpha role where used.
20. For prefab/mesh targets, require Prefab Stage or scoped Editor API readback: renderer paths, mesh refs, material refs, LODGroup state, colliders, scripts, pivots, sockets, anchors, active/inactive state, and scene overrides. Acceptance proof: collider report distinguishes `COL_*` proxy, trigger, gameplay collision, visual mesh, and any MeshCollider misuse.
21. For audio-direct rows, require owner handoff to the audio remediation route. Acceptance proof: no sound route gets deleted or unwired from static GUID evidence; runtime mix and import settings remain `PENDING AUDIO READBACK`.
22. For scriptable/native assets, require owner-local classification before mutation. Acceptance proof: no ScriptableObject runtime mutation or route assignment change is made from CSV presence.
23. For third-party or legacy rows, classify as integrity review, not cleanup. Acceptance proof: no move, strip, delete, or package change; Crest/Feel/legacy assets remain quarantined for named owner decisions.
24. For texture/material streaming rows, require Addressables/import/residency proof. Acceptance proof: no resident-memory, compact VRAM, texture budget, or async upload claim without Unity/Memory Profiler or player capture artifacts.

Checkpoint 4 after task 24: if any owner lane lacks proof route, mark `PENDING_OWNER_PROOF_ROUTE`. If Unity/import/compile is dirty or blocked, mark `BLOCKED_UNITY_GATE` and stop.

25. Produce the final owner report with sections: what was wrong, what was triaged, Unity/proof state, files inspected, rows changed or generated, blockers, residual risks, and exact next owner packets. Acceptance proof: every non-trivial claim has evidence class, artifact path, command/tool, date, and residual risk.

## BLOCKED / PENDING Rules

- `BLOCKED_PROCESS_GATE`: CPU above 50 percent, busy Unity/import/compile/build/shader/package process, save prompt, dirty prompt, or ambiguous process state.
- `BLOCKED_MISSING_EVIDENCE`: required source evidence file is absent.
- `BLOCKED_STATIC_TRIAGE_INTEGRITY`: CSV counts or required columns cannot be reproduced.
- `BLOCKED_UNITY_GATE`: Unity cannot be opened or inspected cleanly without mutation, compilation, import, or prompt risk.
- `BLOCKED_BY_DEPENDENCY`: an owner packet, route bible, prefab, material, or readback target required for a later task is absent.
- `NOT_APPLICABLE`: Unity readback proves the static row is not active, not visible, or is a documented non-visual exception.
- `PENDING_OWNER_MAPPING`: the row is real but no safe owner lane has been proven.
- `PENDING UNITY READBACK`: static route exists but Unity has not read back active slots.
- `PENDING UNITY PROOF`: readback exists but screenshots, Frame Debugger/Stats, Console/log, manifest/checksum, or dirty-state proof is incomplete.
- `REJECTED_VISUAL_FLOOR`: screenshot/readback proves active route is flat, primitive, muddy, placeholder, proxy, blockout, crayon-like, hidden by fog/darkness, or below the reference floor.

## Proof Packet Requirements

Future owner output must include:

- `h8_1475` proof root under `Docs/Screenshots/HectonProofPackets/`.
- `manifest.json` with session id, Unity version, active scene sequence, process gate values, artifact list, screenshot list, Unity log path, console export path, readback report path, Frame Debugger/Stats paths, dirty-state result, mutation result, and `PENDING_VERIFICATION`.
- `manifest.sha256` generated from the actual manifest.
- Copied Unity log and Console export.
- No-mutation readback report.
- Dirty-state audit.
- Canonical screenshots or explicit abort notes.
- Frame Debugger/Stats report.
- Row-level status table mapping GUID triage rows to P0 blocker families and owner lanes.
- Regression model: CPU, GC, memory/VRAM, cadence, correctness, hot-path impact, failure modes, why kept/rejected.

## Rejection Gates

- Any static-only finding reported above `STATIC/PENDING UNITY READBACK` is rejected.
- Any runtime readiness claim without Unity/player/profiler/GC/memory proof is rejected.
- Any visual promotion without valid `h8_1475` proof packet is rejected.
- Any deletion from this packet is rejected.
- Any raw YAML edit is rejected.
- Any Crest wrapper/clone/override shortcut is rejected.
- Any direct source-binding shortcut is rejected.
- Any use of fog/darkness/bloom/camera framing/placement camouflage to hide bad art is rejected.
- Any binary quality switch is rejected; consequences must scale through continuous `GlobalQualityWeight`.

## Continuous GlobalQualityWeight Consequences

- Low/compact: preserve bright readable water, sky/Aegir, shoreline silhouettes, product-face identity, material role correctness, and non-primitive silhouettes. Reduce residency, density, update cadence, LOD distance, and shadow eligibility smoothly; never expose proxy/default/blockout routes.
- Middle: require route-owned PBR stacks, clean active bindings, stable dither/LOD proof, and readable first-20 route composition.
- High: spend saved budget on richer wet-edge detail, stronger Aegir/cloud depth, flora/coral detail normals, longer near-field residency, and denser route dressing after proof.
- Ultra: allow capture-grade overdetail, layered material response, longer LOD residency, richer shoreline/organic density, and sky/ocean overkill only after low-tier readability and proof pass. Gameplay truth, save identity, DTO layout, collision truth, and owner route do not change.

Final disposition: `STATIC/PENDING UNITY READBACK`.
