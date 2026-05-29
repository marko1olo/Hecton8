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

The SDK Hub also creates or refreshes that same path non-destructively. Existing files are not overwritten. The External Starter Kit Workbench opens, creates/refreshes, shows required-file health from the same required-file list as `Tools/validate_structure.ps1`, shows a Manifest Contract panel for allowlisted capability metadata and capped budgets, shows a Dependency Contract panel for synchronized `mod.h8manifest.json` and `mod.json` dependency edits, shows a Graph Contract Preview for `Graphs/main.h8graph.json` against `Reference/allowed_opcodes.csv` and the authoring budget, shows an Authoring Data Preview for `Tables/settings.h8table.json`, `Content/assets.h8manifest.json`, `Content/Assets/`, and `Locales/en.h8loc.json`, generates validated graph/settings/locale/content asset snippets through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, `Tools/create_locale_entry_snippet.ps1`, and `Tools/create_asset_entry_snippet.ps1`, applies graph/settings/locale/content asset snippets through `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, `Tools/apply_locale_entry_snippet.ps1`, and `Tools/apply_asset_entry_snippet.ps1` with duplicate checks, graph budget repair, content byte budget repair, CRC/byte proof, temp-write replacement, post-write validation, and rollback on failure, configures manifest capabilities/budgets through `Tools/configure_manifest_contract.ps1`, configures dependencies through `Tools/configure_dependencies.ps1`, builds reviewed submission zips through `Tools/build_submission_package.ps1`, shows current submission package path/freshness for `Generated/<mod-id>_submission.zip`, runs starter tools asynchronously, shows failed starter tool runs as Editor error UI, runs `Tools/validate_structure.ps1` directly for fast checks, opens the core file/API contracts, and validates this same path by reusing the Hub generator; it does not create a second format. The Workbench also shows review manifest freshness by comparing `Reports/review_manifest.json` with starter source files while excluding `Generated/` and `Reports/`. This gives external authors a normal folder that can be copied, zipped, validated without opening Unity, or inspected through the project-integrated Workbench.

## Required Files

```text
ExternalStarterKit/
  README.md
  h8mod.ps1
  mod.h8manifest.json
  mod.json
  Content/
    README.md
    Assets/
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
    apply_asset_entry_snippet.ps1
    apply_graph_node_snippet.ps1
    apply_locale_entry_snippet.ps1
    apply_settings_row_snippet.ps1
    build_review_manifest.ps1
    build_submission_package.ps1
    configure_dependencies.ps1
    configure_manifest_contract.ps1
    create_first_mod.ps1
    install_local_mod.ps1
    diagnose_local_mods.ps1
    create_asset_entry_snippet.ps1
    create_locale_entry_snippet.ps1
    create_graph_node_snippet.ps1
    create_settings_row_snippet.ps1
    list_allowed_opcodes.ps1
    prepare_mod.ps1
    set_mod_identity.ps1
    validate_structure.ps1
  .vscode/
    settings.json
    tasks.json
```

Required exact file paths include `Content/Assets/README.md`, `Tools/create_asset_entry_snippet.ps1`, `Tools/apply_asset_entry_snippet.ps1`, `Tools/configure_manifest_contract.ps1`, `Tools/configure_dependencies.ps1`, `Tools/create_first_mod.ps1`, `Tools/install_local_mod.ps1`, `Tools/diagnose_local_mods.ps1`, and `.vscode/tasks.json`; the static validator checks these strings and the SDK Hub `BuildStarterKitTemplateFile` route so generated kits prefer the checked-in docs, manifests, schemas, tools, and VS Code files before C# fallbacks instead of drifting away from content asset authoring, first-mod onboarding, local install discovery, local Mods diagnosis, dependency editing, manifest contract support, and VS Code task authoring.

## File Roles

`README.md` is the first screen for random public authors. It states that no Unity project is required for manifest, graph, table, content asset, locale, and validation authoring, and that envelope-only runtime is the active boundary.

`h8mod.ps1` is the root no-Unity launcher for humans. It exposes `menu`, `first-mod`, `install-local`, `diagnose-local`, `dependencies`, `setup`, `validate`, `review`, `prepare`, `submission`, `opcodes`, `opcodes-json`, `node-snippet`, `apply-node-snippet`, `setting-snippet`, `locale-snippet`, `apply-setting-snippet`, `apply-locale-snippet`, `asset-snippet`, `apply-asset-snippet`, `manifest-contract`, and `capabilities` actions, delegates to the existing `Tools/*.ps1` scripts, and is not a runtime activation contract. `first-mod` creates a bounded first playable package draft by setting identity, enabling graph authoring metadata, creating/applying one graph node, one setting, and one locale entry, validating, and rebuilding `Reports/review_manifest.json`. `install-local` copies the reviewed source set plus review manifest into `Mods/<mod-id>` for loader discovery only after byte/SHA-256 verification. `diagnose-local` inspects a project or built-game `Mods` folder, mirrors recursive loader `mod.json` discovery, checks loader caps, duplicate IDs, missing dependencies, dependency cycles, load order, and review hashes, and reports the exact envelope-only disable reason without mutating runtime files. `dependencies` delegates to `Tools/configure_dependencies.ps1` and synchronizes dependency IDs across both manifests with validation and rollback. `node-snippet` accepts `-NodeParametersJson` and `-NodeDisabled` for bounded graph node parameter and enabled-state authoring; the parameter object accepts strict JSON and a flat CLI fallback like `{Quantity:3,Item:demo}` for shells that strip quotes. `asset-snippet` accepts `-AssetCrc32 auto` and `-AssetBytes -1` to compute proof from an existing file under `Content/Assets/`. `manifest-contract` delegates to `Tools/configure_manifest_contract.ps1` for allowlisted manifest capability/budget edits.

`mod.h8manifest.json` is the authoring manifest. It names the mod, dependencies, capabilities, budgets, compatibility, and draft entrypoint files used by Workbench/CLI-style tooling.

`mod.json` is the current loader compatibility manifest. `Dependencies` must match `mod.h8manifest.json` in the same order. `EntryAssembly` and `EntryType` stay empty in envelope-only packages. A non-empty managed entry is a legacy/internal path and is rejected by current runtime policy.

`Graphs/main.h8graph.json` is the command graph draft. Empty graph means no emitted packets. Non-empty graph nodes must use unique `Id` values and an `Opcode` that matches a hex token or comment alias in `Reference/allowed_opcodes.csv`; reserved opcode constants are not public rights.

`Tables/settings.h8table.json` is the user-facing settings table draft. It uses `Schema = hecton8.settings_table.draft.v1`, `Rows[]`, canonical row `Id`, lower-case `Kind` (`bool`, `int`, `float`, `string`, `enum`), and a `Default` value matching that kind. Runtime truth ownership does not move to the mod.

`Content/assets.h8manifest.json` is an asset declaration draft. Files referenced by this draft live under `Content/Assets/`. File presence is not runtime loading permission. Runtime use requires CRC approval and envelope asset references.

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

`Tools/create_graph_node_snippet.ps1` is the local no-Unity graph authoring helper. It validates a node id, validates the opcode alias or hex token against `Reference/allowed_opcodes.csv`, validates top-level `ParametersJson` as a JSON object capped at 64 entries with canonical keys, accepts a flat CLI fallback like `{Quantity:3,Item:demo}` when a shell strips JSON quotes, supports `-Disabled`, and writes `Generated/graph_node_snippet.json`. It does not edit `Graphs/main.h8graph.json`; authors apply it with `h8mod.ps1 -Action apply-node-snippet` or copy the generated JSON object into `Nodes[]`, then run `h8mod.ps1 -Action validate`.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action node-snippet -NodeId node.spawn_item -Opcode SpawnItem -NodeParametersJson '{Quantity:1}' -NodeDisabled
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_graph_node_snippet.ps1 -Id node.spawn_item -Opcode SpawnItem
```

`Tools/apply_graph_node_snippet.ps1` inserts `Generated/graph_node_snippet.json` into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph and authoring manifest `MaxEnvelopesPerFrame` to one when a first node needs a valid envelope budget, writes graph and manifest through temp files, validates the starter kit after replacement, and restores previous graph/manifest files if validation fails.

`Tools/create_settings_row_snippet.ps1` is the local no-Unity settings authoring helper. It validates a canonical setting id, supported kind, and typed default value, then writes `Generated/settings_row_snippet.json`. It does not edit `Tables/settings.h8table.json`; authors apply it with `h8mod.ps1 -Action apply-setting-snippet` or copy the generated row object into `Rows[]`, then run `h8mod.ps1 -Action validate`.

`Tools/create_locale_entry_snippet.ps1` is the local no-Unity locale authoring helper. It validates a canonical locale key and non-empty text value, then writes `Generated/locale_entry_snippet.json`. It does not edit `Locales/en.h8loc.json`; authors apply it with `h8mod.ps1 -Action apply-locale-snippet` or copy the generated key/value into `Strings`, then run `h8mod.ps1 -Action validate`.

`Tools/apply_settings_row_snippet.ps1` inserts `Generated/settings_row_snippet.json` into `Tables/settings.h8table.json`, strips snippet-only notes, rejects duplicate setting ids unless `-Replace` is explicit, writes through a same-folder temp file, validates the starter kit after replacement, and restores the previous table if validation fails.

`Tools/apply_locale_entry_snippet.ps1` inserts `Generated/locale_entry_snippet.json` into `Locales/en.h8loc.json`, rejects duplicate keys unless `-Replace` is explicit, writes through a same-folder temp file, validates the starter kit after replacement, and restores the previous locale file if validation fails.

`Tools/create_asset_entry_snippet.ps1` is the local no-Unity content asset helper. It validates a canonical asset id, one of `raw_texture`, `audio_clip`, or `data_blob`, a starter-relative path under `Content/Assets/`, the extension allowed for that kind, and a CRC32/byte-count pair. When the file exists, use `-Crc32 auto -Bytes -1` to compute proof from bytes on disk. It writes `Generated/asset_entry_snippet.json` and never mutates `Content/assets.h8manifest.json`.

`Tools/apply_asset_entry_snippet.ps1` inserts `Generated/asset_entry_snippet.json` into `Content/assets.h8manifest.json`, verifies the referenced `Content/Assets/` file, recomputes CRC32 and byte length, rejects duplicate asset ids unless `-Replace` is explicit, raises `mod.h8manifest.json` `Budgets.MaxAssetBytes` when the current content byte total needs it, writes manifest files through temp replacements, validates the starter kit after replacement, and restores previous files if validation fails.

`Tools/configure_manifest_contract.ps1` configures `mod.h8manifest.json` `Capabilities` and `Budgets` through a bounded offline route. It accepts only public capability metadata (`cap.graph.command_draft`, `cap.settings.table`, `cap.locale.en`, `cap.content.asset_manifest`, `cap.review.submission_package`), caps `MaxEnvelopesPerFrame` at `256`, caps `MaxAssetBytes` at `33554432`, refuses to lower budgets below the current graph/content requirements, writes through a temp file, validates after write, and restores the previous manifest if validation fails. Capabilities are review metadata, not runtime permissions.

`Tools/configure_dependencies.ps1` configures dependency IDs through a bounded offline route. It accepts `list`, `add`, `remove`, and `clear`, writes `Dependencies` to `mod.h8manifest.json` and `mod.json` together, rejects invalid IDs, duplicate IDs, and self-dependencies, validates after write, and restores both manifests if validation fails. Dependencies are package ordering metadata used by loader diagnosis; they are not runtime code execution rights.

`Tools/create_first_mod.ps1` is the no-Unity onboarding helper. It composes the already-bounded identity, manifest contract, graph snippet/apply, settings snippet/apply, locale snippet/apply, validation, and review-manifest tools. `-Replace` makes the first-mod pass rerunnable without duplicating the same starter sample IDs. `-BuildSubmission` also writes `Generated/<mod-id>_submission.zip`. It does not grant managed DLL execution or loose asset loading.

`Tools/install_local_mod.ps1` is the no-Unity local discovery installer. It runs `prepare`, reads `Reports/review_manifest.json`, verifies every reviewed source file by byte length and SHA-256, stages the copy under the target Mods folder, swaps `Mods/<mod-id>` only after staging succeeds, restores the previous copy on failure, and also copies `Reports/review_manifest.json`. Use `-ProjectRoot <HECTON-8 project root>` for the Unity project path or `-ModsRoot <Mods folder>` for a built game folder. This is not runtime permission: managed entry and loose content ingestion remain blocked by the current loader boundary.

`Tools/diagnose_local_mods.ps1` is the no-Unity read-only local Mods inspector. It resolves `ProjectRoot/Mods` or explicit `-ModsRoot`, mirrors recursive loader `mod.json` discovery and current loader caps for manifest bytes, manifest discovery count, top-level DLLs, bundles, and `lang_*.json`, validates `mod.json`, verifies installed `Reports/review_manifest.json` byte counts and SHA-256 hashes when present, resolves duplicate IDs, missing dependencies, dependency cycles, and load order, and emits `hecton8.local_mods_diagnosis.v1` JSON with package status, review status, dependency status, graph summary, and boundary reason. A valid reviewed local copy is still `DISABLED_BY_RUNTIME_BOUNDARY` until engine-owned envelope/bake approval routes grant actual gameplay effects; this tool diagnoses what the loader will see, not what runtime will execute.

Run them from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setting-snippet -SettingId setting.example_toggle -SettingKind bool -SettingDefault false
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action locale-snippet -LocaleKey text.example_line -LocaleValue "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-node-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-setting-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-locale-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action asset-snippet -AssetId asset.example_blob -AssetKind data_blob -AssetPath Content/Assets/example.bytes -AssetCrc32 auto -AssetBytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-asset-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action manifest-contract -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action diagnose-local -ProjectRoot <HECTON-8 project root>
```

`Schemas/*.schema.json` are portable JSON Schemas for the starter files. They are editor assistance and validation hints only; they do not make runtime capabilities public.

`.vscode/settings.json` maps starter files to those schemas for schema-aware editor autocomplete and early error highlighting, and carries `hecton8.powerShellExecutable` so authors can switch from Windows PowerShell to `pwsh` without editing every task. Other editors can use the same files manually. The local validator checks the exact schema URL/fileMatch pairs so a copied kit cannot silently lose editor assistance while still passing validation.

`.vscode/tasks.json` is the no-Unity button surface for VS Code users. It exposes setup, validate, prepare, submission, local discovery install, local Mods diagnosis, dependency list/add/remove/clear, capability/opcode discovery, graph/settings/locale/content asset snippet creation/apply, disabled graph node creation, explicit graph/settings/locale/asset replace applies, and manifest contract configuration through `Tasks: Run Task`. Every task routes through root `h8mod.ps1` and `${config:hecton8.powerShellExecutable}`; direct `Tools/*.ps1` task entries are rejected by the local validator so the task surface cannot bypass the launcher, validation, review flow, or envelope-only runtime boundary.
`.vscode/tasks.json` also exposes `HECTON-8: create first playable mod`, which routes through `h8mod.ps1 -Action first-mod -Replace` so a copied starter folder can produce a valid graph/settings/locale/review draft from VS Code without manual JSON insertion.

`Tools/prepare_mod.ps1` is the one-command local no-Unity happy path underneath the root launcher. With `-Id`, it runs identity setup, structure validation, and review manifest generation in the correct order. Without identity arguments, it validates the existing manifests and rebuilds `Reports/review_manifest.json` for the normal edit-review loop. Public tools compose child paths through normalized `Join-Path` segments, not Windows backslash-only child paths. Use `powershell` on Windows or `pwsh` on macOS/Linux with PowerShell 7.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action validate
```

`Tools/validate_structure.ps1` is a local no-Unity structure validator. It checks required files, JSON parseability, JSON Schema file parseability, exact `.vscode/settings.json` schema URL/fileMatch mapping, `.vscode/tasks.json` version/labels/inputs/launcher routing, canonical `mod.h8manifest.json` and `mod.json` IDs, matching authoring/runtime IDs, matching `DisplayName`/`Name`, `Author`, and `Version` values, semantic package versions, canonical runtime dependency IDs, allowlisted manifest capabilities, manifest budget caps, settings row schema/ID/kind/default type constraints, content asset schema/kind/path/extension/CRC32/byte-count/duplicate/cap/budget constraints, locale schema/code/key/value constraints, `Compatibility.Runtime = envelope-only`, graph runtime `envelope-only`, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, graph budget parity against `mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`, empty `EntryAssembly`, empty `EntryType`, API version floor, and reference CSV presence.

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

`Tools/build_submission_package.ps1` is a local no-Unity review package tool. It runs prepare, validates the review manifest schema/runtime, and writes `Generated/<mod-id>_submission.zip` with reviewed starter sources plus `Reports/review_manifest.json`. It writes to a temp zip first and restores the previous submission zip if final replacement fails. It rejects unsafe output paths and does not install anything into runtime `Mods/`; use `h8mod.ps1 -Action install-local` for the separate discovery-copy path.

Run it from the starter kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action install-local -ProjectRoot ..\.. -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_submission_package.ps1
```

## Tooling Direction

Low tier authoring: copy the starter folder, run `h8mod.ps1 -Action first-mod -Id ... -Replace` for a valid first graph/settings/locale/review draft, then use `h8mod.ps1 -Action capabilities`, `h8mod.ps1 -Action manifest-contract`, `h8mod.ps1 -Action dependencies`, `h8mod.ps1 -Action opcodes`, `h8mod.ps1 -Action node-snippet -NodeParametersJson '{}'`, `h8mod.ps1 -Action apply-node-snippet`, `h8mod.ps1 -Action setting-snippet`, `h8mod.ps1 -Action locale-snippet`, `h8mod.ps1 -Action apply-setting-snippet`, `h8mod.ps1 -Action apply-locale-snippet`, `h8mod.ps1 -Action asset-snippet -AssetCrc32 auto -AssetBytes -1`, and `h8mod.ps1 -Action apply-asset-snippet` while editing graph/settings/locale/content data, edit JSON/CSV in any text editor, then rerun `h8mod.ps1 -Action submission` before review handoff. For local loader discovery in the project, run `h8mod.ps1 -Action install-local -ProjectRoot <project root> -Replace`, then `h8mod.ps1 -Action diagnose-local -ProjectRoot <project root>` to inspect the installed `Mods/<mod-id>` state, dependency blockers, duplicate IDs, cycles, and load-order preview; emit no gameplay packets until engine approval/bake routes exist.

Middle tier authoring: use the Unity SDK Hub to create the starter kit, open the External Starter Kit Workbench, check required-file health, Capability Matrix, Manifest Contract, graph contract preview, authoring data preview including content asset manifest state, generate graph/settings/locale/content asset snippets, apply graph/settings/locale/content asset snippets through the bounded tools, inspect review manifest freshness/docs, run `Tools/validate_structure.ps1`, read failed tool output as Editor error UI, build the submission package, and open the current `Generated/<mod-id>_submission.zip` from the same screen.

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
