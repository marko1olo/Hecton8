# HECTON-8 External Starter Kit File Contract

Date: 2026-05-28
Status: CURRENT AUTHORING CONTRACT / ENVELOPE-ONLY RUNTIME / STATIC PROOF REQUIRED
Owner domain: Modding SDK public authoring surface

## Purpose

This file answers the practical public-modder question: what program and what files are needed.

The current answer is deliberately narrow:

- ordinary mod authors do not need the full HECTON-8 Unity project;
- Unity is optional for external authors, but when they use the project the integrated entry point is `Hecton/Modding/External Starter Kit Workbench`;
- normal authoring starts from `Hecton/Modding/SDK Hub -> Create External Starter Kit` or the Workbench over the same starter folder;
- runtime gameplay authority is not managed DLL execution, Harmony, BepInEx, loose AssetBundle loading, loose PNG loading, or loose localization injection;
- runtime gameplay authority is validated 64-byte `FutureCommandEnvelope` data after SDK bake/approval.
- Runtime stays envelope-only.

## Generated Location

The repository includes a versioned starter template at:

```text
ModdingSDK/ExternalStarterKit/
```

The SDK Hub also creates or refreshes that same path non-destructively. Existing files are not overwritten. The External Starter Kit Workbench opens, creates/refreshes, shows required-file health from the same required-file list as `Tools/validate_structure.ps1`, shows a Graph Contract Preview for `Graphs/main.h8graph.json` against `Reference/allowed_opcodes.csv` and the authoring budget, shows an Authoring Data Preview for `Tables/settings.h8table.json` and `Locales/en.h8loc.json`, generates validated graph/settings/locale snippets through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, and `Tools/create_locale_entry_snippet.ps1`, applies graph/settings/locale snippets through `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, and `Tools/apply_locale_entry_snippet.ps1` with duplicate checks, graph budget repair, temp-write replacement, post-write validation, and rollback on failure, builds reviewed submission zips through `Tools/build_submission_package.ps1`, shows current submission package path/freshness for `Generated/<mod-id>_submission.zip`, runs starter tools asynchronously, shows failed starter tool runs as Editor error UI, runs `Tools/validate_structure.ps1` directly for fast checks, opens the core file/API contracts, and validates this same path by reusing the Hub generator; it does not create a second format. The Workbench also shows review manifest freshness by comparing `Reports/review_manifest.json` with starter source files while excluding `Generated/` and `Reports/`. This gives external authors a normal folder that can be copied, zipped, validated without opening Unity, or inspected through the project-integrated Workbench.

## Required Files

```text
ExternalStarterKit/
  README.md
  h8mod.ps1
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
    apply_graph_node_snippet.ps1
    apply_locale_entry_snippet.ps1
    apply_settings_row_snippet.ps1
    build_review_manifest.ps1
    build_submission_package.ps1
    create_locale_entry_snippet.ps1
    create_graph_node_snippet.ps1
    create_settings_row_snippet.ps1
    list_allowed_opcodes.ps1
    prepare_mod.ps1
    set_mod_identity.ps1
    validate_structure.ps1
  .vscode/
    settings.json
```

## File Roles

`README.md` is the first screen for random public authors. It states that no Unity project is required for manifest, graph, table, locale, and validation authoring, and that envelope-only runtime is the active boundary.

`h8mod.ps1` is the root no-Unity launcher for humans. It exposes `menu`, `setup`, `validate`, `review`, `prepare`, `submission`, `opcodes`, `opcodes-json`, `node-snippet`, `apply-node-snippet`, `setting-snippet`, `locale-snippet`, `apply-setting-snippet`, `apply-locale-snippet`, and `capabilities` actions, delegates to the existing `Tools/*.ps1` scripts, and is not a runtime install contract.

`mod.h8manifest.json` is the authoring manifest. It names the mod, capabilities, budgets, compatibility, and draft entrypoint files used by Workbench/CLI-style tooling.

`mod.json` is the current loader compatibility manifest. `EntryAssembly` and `EntryType` stay empty in envelope-only packages. A non-empty managed entry is a legacy/internal path and is rejected by current runtime policy.

`Graphs/main.h8graph.json` is the command graph draft. Empty graph means no emitted packets. Non-empty graph nodes must use unique `Id` values and an `Opcode` that matches a hex token or comment alias in `Reference/allowed_opcodes.csv`; reserved opcode constants are not public rights.

`Tables/settings.h8table.json` is the user-facing settings table draft. It uses `Schema = hecton8.settings_table.draft.v1`, `Rows[]`, canonical row `Id`, lower-case `Kind` (`bool`, `int`, `float`, `string`, `enum`), and a `Default` value matching that kind. Runtime truth ownership does not move to the mod.

`Content/assets.h8manifest.json` is an asset declaration draft. File presence is not runtime loading permission. Runtime use requires CRC approval and envelope asset references.

`Locales/en.h8loc.json` is a locale draft. It uses `Schema = hecton8.locale.draft.v1`, `Locale` in `xx` or `xx-YY` form, and canonical string keys with non-empty text values. Runtime localization injection is not currently a public mod right.

`Generated/` is for SDK-produced `.h8bin`, manifests, and package outputs. Public authors should not hand-write binary envelope streams.

`Reports/` is for validator, packer, and simulator reports.

`Reference/allowed_opcodes.csv` is the current envelope allowlist snapshot. `Reference/kernel_tuning_profiles.csv` is editor/simulator reference data only; it does not make reserved opcodes public.

The versioned starter template copies of these CSVs must match `Docs/Modding/allowed_opcodes.csv` and `Docs/Modding/kernel_tuning_profiles.csv`. `Validate_Mod_API_Static.ps1` fails if those copies drift.

`Tools/list_allowed_opcodes.ps1` is the local no-Unity opcode discovery helper. It reads `Reference/allowed_opcodes.csv`, prints the aliases and hex tokens accepted by `Graphs/main.h8graph.json`, rejects malformed or duplicated rows, and supports `-Json` output for Workbench/CLI screens. It does not authorize reserved opcodes; it only exposes the copied allowlist already validated against the docs source.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json
```

`Tools/create_graph_node_snippet.ps1` is the local no-Unity graph authoring helper. It validates a node id, validates the opcode alias or hex token against `Reference/allowed_opcodes.csv`, and writes `Generated/graph_node_snippet.json`. It does not edit `Graphs/main.h8graph.json`; authors apply it with `h8mod.ps1 -Action apply-node-snippet` or copy the generated JSON object into `Nodes[]`, then run `h8mod.ps1 -Action validate`.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action node-snippet -NodeId node.spawn_item -Opcode SpawnItem
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_graph_node_snippet.ps1 -Id node.spawn_item -Opcode SpawnItem
```

`Tools/apply_graph_node_snippet.ps1` inserts `Generated/graph_node_snippet.json` into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph and authoring manifest `MaxEnvelopesPerFrame` to one when a first node needs a valid envelope budget, writes graph and manifest through temp files, validates the starter kit after replacement, and restores previous graph/manifest files if validation fails.

`Tools/create_settings_row_snippet.ps1` is the local no-Unity settings authoring helper. It validates a canonical setting id, supported kind, and typed default value, then writes `Generated/settings_row_snippet.json`. It does not edit `Tables/settings.h8table.json`; authors apply it with `h8mod.ps1 -Action apply-setting-snippet` or copy the generated row object into `Rows[]`, then run `h8mod.ps1 -Action validate`.

`Tools/create_locale_entry_snippet.ps1` is the local no-Unity locale authoring helper. It validates a canonical locale key and non-empty text value, then writes `Generated/locale_entry_snippet.json`. It does not edit `Locales/en.h8loc.json`; authors apply it with `h8mod.ps1 -Action apply-locale-snippet` or copy the generated key/value into `Strings`, then run `h8mod.ps1 -Action validate`.

`Tools/apply_settings_row_snippet.ps1` inserts `Generated/settings_row_snippet.json` into `Tables/settings.h8table.json`, strips snippet-only notes, rejects duplicate setting ids unless `-Replace` is explicit, writes through a same-folder temp file, validates the starter kit after replacement, and restores the previous table if validation fails.

`Tools/apply_locale_entry_snippet.ps1` inserts `Generated/locale_entry_snippet.json` into `Locales/en.h8loc.json`, rejects duplicate keys unless `-Replace` is explicit, writes through a same-folder temp file, validates the starter kit after replacement, and restores the previous locale file if validation fails.

Run them from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setting-snippet -SettingId setting.example_toggle -SettingKind bool -SettingDefault false
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action locale-snippet -LocaleKey text.example_line -LocaleValue "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-node-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-setting-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-locale-snippet
```

`Schemas/*.schema.json` are portable JSON Schemas for the starter files. They are editor assistance and validation hints only; they do not make runtime capabilities public.

`.vscode/settings.json` maps starter files to those schemas for schema-aware editor autocomplete and early error highlighting. Other editors can use the same files manually. The local validator checks the exact schema URL/fileMatch pairs so a copied kit cannot silently lose editor assistance while still passing validation.

`Tools/prepare_mod.ps1` is the one-command local no-Unity happy path underneath the root launcher. With `-Id`, it runs identity setup, structure validation, and review manifest generation in the correct order. Without identity arguments, it validates the existing manifests and rebuilds `Reports/review_manifest.json` for the normal edit-review loop. Public tools compose child paths through normalized `Join-Path` segments, not Windows backslash-only child paths. Use `powershell` on Windows or `pwsh` on macOS/Linux with PowerShell 7.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action validate
```

`Tools/validate_structure.ps1` is a local no-Unity structure validator. It checks required files, JSON parseability, JSON Schema file parseability, exact `.vscode/settings.json` schema URL/fileMatch mapping, canonical `mod.h8manifest.json` and `mod.json` IDs, matching authoring/runtime IDs, matching `DisplayName`/`Name`, `Author`, and `Version` values, semantic package versions, canonical runtime dependency IDs, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, `Compatibility.Runtime = envelope-only`, graph runtime `envelope-only`, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, graph budget parity against `mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`, empty `EntryAssembly`, empty `EntryType`, API version floor, and reference CSV presence.

`Tools/validate_structure.ps1` also validates `Graphs/main.h8graph.json` node `Id` uniqueness, required `Opcode`, opcode token/alias membership in `Reference/allowed_opcodes.csv`, a 256-node graph cap, and `MaxEnvelopesPerFrame <= mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`.

`Tools/build_review_manifest.ps1` is bounded: max `256` hashed source files, max `4194304` bytes per source file, max `33554432` total source bytes. `Generated/` and `Reports/` remain excluded. Oversized source files fail before hashing so copied starter kits do not become accidental bulk-package or binary-ingest tools.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1
```

`Tools/set_mod_identity.ps1` is a local no-Unity identity helper. It validates the canonical mod id, required display/author text, and semantic version string; writes matching id/name/author/version fields to both manifests; then runs `Tools/validate_structure.ps1` so identity edits fail before package review.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

`Tools/build_review_manifest.ps1` is a local no-Unity review handoff tool. It runs `Tools/validate_structure.ps1` first, then writes `Reports/review_manifest.json` with package identity, sorted authoring/tool file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes. `Generated/` and `Reports/` are excluded so reports and package outputs do not hash themselves or masquerade as source inputs. It fails before hashing if a copied kit exceeds `256` source files, `4194304` bytes per source file, or `33554432` total source bytes.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1
```

`Docs/capabilities.md` is the public capability guide. It lists supported authoring surfaces, forbidden runtime rights, no-Unity and Unity Workbench workflows, and the expansion route for new engine-owned capabilities. `h8mod.ps1 -Action capabilities` prints this guide, and `Tools/validate_structure.ps1` rejects missing capability guide text so the starter does not drift into vague or false modding promises.

`Tools/build_submission_package.ps1` is a local no-Unity review package tool. It runs prepare, validates the review manifest schema/runtime, and writes `Generated/<mod-id>_submission.zip` with reviewed starter sources plus `Reports/review_manifest.json`. It writes to a temp zip first and restores the previous submission zip if final replacement fails. It rejects unsafe output paths and does not install anything into runtime `Mods/`.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_submission_package.ps1
```

## Tooling Direction

Low tier authoring: copy the starter folder, run `h8mod.ps1 -Action setup -Id ...` once, use `h8mod.ps1 -Action capabilities`, `h8mod.ps1 -Action opcodes`, `h8mod.ps1 -Action node-snippet`, `h8mod.ps1 -Action apply-node-snippet`, `h8mod.ps1 -Action setting-snippet`, `h8mod.ps1 -Action locale-snippet`, `h8mod.ps1 -Action apply-setting-snippet`, and `h8mod.ps1 -Action apply-locale-snippet` while editing graph/settings/locale data, edit JSON/CSV in any text editor, then rerun `h8mod.ps1 -Action submission` before review handoff. Emit no gameplay packets until validated.

Middle tier authoring: use the Unity SDK Hub to create the starter kit, open the External Starter Kit Workbench, check required-file health, Capability Matrix, graph contract preview, authoring data preview, generate graph/settings/locale snippets, apply graph/settings/locale snippets through the bounded tools, inspect review manifest freshness/docs, run `Tools/validate_structure.ps1`, read failed tool output as Editor error UI, build the submission package, and open the current `Generated/<mod-id>_submission.zip` from the same screen.

High tier authoring: use the current Workbench for identity/validation/review plus future graph/table/asset screens over the same file contract.

Ultra tier authoring: use future advanced Workbench simulation, preview, package diff, and visual-overkill diagnostics over the same runtime envelope boundary.

## Rejection Rules

Reject a public guide or SDK change if it tells authors to:

- copy the full game Unity project as the normal modding workflow;
- build gameplay DLL patches as the normal runtime workflow;
- rely on loose files being loaded by the runtime because they exist in a package folder;
- treat editor tuning profiles as opcode authorization;
- accept non-canonical package/dependency IDs, non-semantic package versions, or mismatched `mod.h8manifest.json` and `mod.json` identity fields;
- mark runtime support verified without the runtime playbook evidence.
