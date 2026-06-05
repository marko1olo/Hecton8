# H8 1475 Readback Field Manifest - 2026-06-05

Status: `STATIC VERIFIED / PENDING UNITY PROOF`.
Evidence class: `STATIC_DOC`.
Runtime proof: absent.
Unity execution: not performed by this worker.

This file extracts the `h8_1475` no-mutation Unity readback packet into a machine-readable future-owner manifest. The authoritative table is:

`Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.csv`

## Scope

Future Unity owner must capture readback fields for:

- player/HUD production binding;
- sky, Aegir, clouds, and moons;
- Crest/ocean, foam/contact route, and micro-fauna primitive risk;
- terrain/material active route;
- product-face primitive, blockout, package-default, proxy, and placeholder prefabs;
- proof packet metadata;
- dirty-state audit;
- canonical screenshots;
- Unity log/Console export;
- Frame Debugger/Stats.

This manifest improves the first-20-minutes route proof lane: bright first surface exit with readable production player/HUD, sky/Aegir, ocean/Crest surface, shoreline, photic terrain, and product-face tool/resource/transport sources.

Route blocker removed: not a Unity blocker. It removes packet ambiguity before the future Unity owner executes readback.

## Required Process Gate

Unity readback must not start unless all process gates are green:

- CPU total `<= 50 percent`.
- No busy `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, `VBCSCompiler`, package/import/build process.
- Unity is closed or idle.
- No import spinner, script compilation, shader compilation, package resolution, player build, save prompt, dirty prompt, or automatic upgrade/import repair prompt.

Suggested future-owner commands from the packet:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time'
Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,ShaderCompiler,AssetImportWorker,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue
```

If process state is ambiguous, abort. Do not launch Unity to "check anyway".

## No-Mutation Rules

Readback is inspection and capture only:

- No `SaveScene`.
- No `EditorSceneManager.MarkSceneDirty`.
- No `EditorUtility.SetDirty`.
- No `AssetDatabase.SaveAssets`.
- No scene, project, prefab, material, shader, importer, Addressables, package, ProjectSettings, or code save.
- No prefab apply/revert.
- No raw YAML edit.
- No temporary files under `Assets/`.
- No texture binding, material assignment, mesh replacement, terrain layer edit, Crest setting change, canvas render-mode change, or object disable used to make screenshots look better.
- No Crest runtime wrapper, material clone, material instantiation, or override script.

If Unity marks any scene, prefab, material, importer, Addressables setting, or project setting dirty during readback, stop and report the object path. Do not save it.

## Proof Packet Contract

Required future packet root:

`Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`

Minimum packet files:

- `manifest.json`
- `manifest.sha256`
- `UnityLog.txt`
- no-mutation readback report
- console export
- dirty-state audit
- canonical `h8_1475_*.png` screenshots or `ABORTED_<view>.md` notes
- Frame Debugger/Stats report

`manifest.json` must keep acceptance state as `PENDING_VERIFICATION`. Static source gates remain failed until Unity readback proves an accepted active replacement or a non-visual exception.

## CSV Schema

Fields:

- `domain`
- `target_group`
- `readback_field`
- `required_value_or_classification`
- `evidence_artifact`
- `reject_if_missing`
- `notes`

The CSV includes rows for required process gates, no-mutation guards, object/material/slot readbacks, canonical screenshots, render route proof, dirty-state proof, and continuous `GlobalQualityWeight` consequence notes.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`

## Source Inputs

- `AGENTS.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`
- `Docs/QUALITY_GATES.md`
- `quality.md`
- `Docs/README.md`

## Current Verification State

Code/build/Unity import: not run by instruction.
Runtime/Play Mode/profiler/GC/memory: absent.
Visual acceptance: absent.
Result: `PENDING UNITY PROOF`.

Low / Middle / High / Ultra consequences are encoded in CSV `scalability` rows. They do not authorize binary quality switches and do not change gameplay truth, save identity, DTO layout, collision truth, or Crest ownership.
