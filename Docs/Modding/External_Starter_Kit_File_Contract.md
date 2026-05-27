# HECTON-8 External Starter Kit File Contract

Date: 2026-05-28
Status: CURRENT AUTHORING CONTRACT / ENVELOPE-ONLY RUNTIME / STATIC PROOF REQUIRED
Owner domain: Modding SDK public authoring surface

## Purpose

This file answers the practical public-modder question: what program and what files are needed.

The current answer is deliberately narrow:

- ordinary mod authors do not need the full HECTON-8 Unity project;
- Unity is optional and only useful for advanced asset preview until a standalone Workbench or CLI ships;
- normal authoring starts from `Hecton/Modding/SDK Hub -> Create External Starter Kit`;
- runtime gameplay authority is not managed DLL execution, Harmony, BepInEx, loose AssetBundle loading, loose PNG loading, or loose localization injection;
- runtime gameplay authority is validated 64-byte `FutureCommandEnvelope` data after SDK bake/approval.
- Runtime stays envelope-only.

## Generated Location

The repository includes a versioned starter template at:

```text
ModdingSDK/ExternalStarterKit/
```

The SDK Hub also creates or refreshes that same path non-destructively. Existing files are not overwritten. This gives external authors a normal folder that can be copied, zipped, or validated without opening Unity.

## Required Files

```text
ExternalStarterKit/
  README.md
  mod.h8manifest.json
  mod.json
  Content/
    README.md
    assets.h8manifest.json
  Graphs/
    main.h8graph.json
  Tables/
    settings.h8table.json
  Locales/
    en.h8loc.json
  Generated/
    README.md
  Reports/
    README.md
  Reference/
    README.md
    allowed_opcodes.csv
    kernel_tuning_profiles.csv
  Schemas/
    assets.schema.json
    h8graph.schema.json
    h8mod.authoring.schema.json
    locale.schema.json
    runtime.mod.schema.json
    settings_table.schema.json
  Tools/
    README.md
    build_review_manifest.ps1
    list_allowed_opcodes.ps1
    prepare_mod.ps1
    set_mod_identity.ps1
    validate_structure.ps1
  .vscode/
    settings.json
```

## File Roles

`README.md` is the first screen for random public authors. It states that no Unity project is required for manifest, graph, table, locale, and validation authoring, and that envelope-only runtime is the active boundary.

`mod.h8manifest.json` is the authoring manifest. It names the mod, capabilities, budgets, compatibility, and draft entrypoint files used by Workbench/CLI-style tooling.

`mod.json` is the current loader compatibility manifest. `EntryAssembly` and `EntryType` stay empty in envelope-only packages. A non-empty managed entry is a legacy/internal path and is rejected by current runtime policy.

`Graphs/main.h8graph.json` is the command graph draft. Empty graph means no emitted packets. Non-empty graph nodes must use unique `Id` values and an `Opcode` that matches a hex token or comment alias in `Reference/allowed_opcodes.csv`; reserved opcode constants are not public rights.

`Tables/settings.h8table.json` is the user-facing settings table draft. Runtime truth ownership does not move to the mod.

`Content/assets.h8manifest.json` is an asset declaration draft. File presence is not runtime loading permission. Runtime use requires CRC approval and envelope asset references.

`Locales/en.h8loc.json` is a locale draft. Runtime localization injection is not currently a public mod right.

`Generated/` is for SDK-produced `.h8bin`, manifests, and package outputs. Public authors should not hand-write binary envelope streams.

`Reports/` is for validator, packer, and simulator reports.

`Reference/allowed_opcodes.csv` is the current envelope allowlist snapshot. `Reference/kernel_tuning_profiles.csv` is editor/simulator reference data only; it does not make reserved opcodes public.

The versioned starter template copies of these CSVs must match `Docs/Modding/allowed_opcodes.csv` and `Docs/Modding/kernel_tuning_profiles.csv`. `Validate_Mod_API_Static.ps1` fails if those copies drift.

`Tools/list_allowed_opcodes.ps1` is the local no-Unity opcode discovery helper. It reads `Reference/allowed_opcodes.csv`, prints the aliases and hex tokens accepted by `Graphs/main.h8graph.json`, rejects malformed or duplicated rows, and supports `-Json` output for future Workbench/CLI screens. It does not authorize reserved opcodes; it only exposes the copied allowlist already validated against the docs source.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json
```

`Schemas/*.schema.json` are portable JSON Schemas for the starter files. They are editor assistance and validation hints only; they do not make runtime capabilities public.

`.vscode/settings.json` maps starter files to those schemas for schema-aware editor autocomplete and early error highlighting. Other editors can use the same files manually. The local validator checks the exact schema URL/fileMatch pairs so a copied kit cannot silently lose editor assistance while still passing validation.

`Tools/prepare_mod.ps1` is the one-command local no-Unity happy path. It runs identity setup, structure validation, and review manifest generation in the correct order. Public tools compose child paths through normalized `Join-Path` segments, not Windows backslash-only child paths. Use `powershell` on Windows or `pwsh` on macOS/Linux with PowerShell 7.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

`Tools/validate_structure.ps1` is a local no-Unity structure validator. It checks required files, JSON parseability, JSON Schema file parseability, exact `.vscode/settings.json` schema URL/fileMatch mapping, canonical `mod.h8manifest.json` and `mod.json` IDs, matching authoring/runtime IDs, canonical runtime dependency IDs, `Compatibility.Runtime = envelope-only`, graph runtime `envelope-only`, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, graph budget parity against `mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`, empty `EntryAssembly`, empty `EntryType`, API version floor, and reference CSV presence.

`Tools/validate_structure.ps1` also validates `Graphs/main.h8graph.json` node `Id` uniqueness, required `Opcode`, opcode token/alias membership in `Reference/allowed_opcodes.csv`, and `MaxEnvelopesPerFrame <= mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`.

`Tools/build_review_manifest.ps1` is bounded: max `256` hashed source files, max `4194304` bytes per source file, max `33554432` total source bytes. `Generated/` and `Reports/` remain excluded. Oversized source files fail before hashing so copied starter kits do not become accidental bulk-package or binary-ingest tools.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1
```

`Tools/set_mod_identity.ps1` is a local no-Unity identity helper. It validates the canonical mod id, writes matching id/name/author/version fields to both manifests, then runs `Tools/validate_structure.ps1` so identity edits fail before package review.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

`Tools/build_review_manifest.ps1` is a local no-Unity review handoff tool. It runs `Tools/validate_structure.ps1` first, then writes `Reports/review_manifest.json` with sorted authoring/tool file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes. `Generated/` and `Reports/` are excluded so reports and package outputs do not hash themselves or masquerade as source inputs. It fails before hashing if a copied kit exceeds `256` source files, `4194304` bytes per source file, or `33554432` total source bytes.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1
```

## Tooling Direction

Low tier authoring: copy the starter folder, run `Tools/prepare_mod.ps1`, use `Tools/list_allowed_opcodes.ps1` while editing graph nodes, edit JSON/CSV in any text editor, then rerun `Tools/validate_structure.ps1` and `Tools/build_review_manifest.ps1` before review handoff. Emit no gameplay packets until validated.

Middle tier authoring: use the Unity SDK Hub to create the starter kit, inspect docs, and run static validation.

High tier authoring: use future Workbench graph/table/asset screens over the same file contract.

Ultra tier authoring: use future Workbench simulation, preview, package diff, and visual-overkill diagnostics over the same runtime envelope boundary.

## Rejection Rules

Reject a public guide or SDK change if it tells authors to:

- copy the full game Unity project as the normal modding workflow;
- build gameplay DLL patches as the normal runtime workflow;
- rely on loose files being loaded by the runtime because they exist in a package folder;
- treat editor tuning profiles as opcode authorization;
- accept non-canonical package/dependency IDs or mismatched `mod.h8manifest.json` and `mod.json` IDs;
- mark runtime support verified without the runtime playbook evidence.
