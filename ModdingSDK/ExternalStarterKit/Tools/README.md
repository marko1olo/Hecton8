# Tools

Fast path for a copied starter kit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action first-mod -Id com.yourname.firstmod -DisplayName "First HECTON Mod" -Author "YourName" -Version 0.1.0 -Replace
```

Manual identity-only setup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

Normal edit-review loop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
```

Capability matrix:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action capabilities
```

Manifest capability/budget setup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action manifest-contract -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1
```

Submission handoff:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
```

Local project discovery copy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action install-local -ProjectRoot ..\.. -Replace
```

Local Mods diagnosis:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action diagnose-local -ProjectRoot ..\..
```

Dependency contract:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action dependencies -DependencyAction list
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action dependencies -DependencyAction add -DependencyId com.example.library
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The scripts normalize child paths internally; do not rewrite `Tools/`, `Reports/`, or `.vscode/` paths per platform. In VS Code, change `hecton8.powerShellExecutable` in `.vscode/settings.json` to `pwsh`, then run `Tasks: Run Task` for the same `h8mod.ps1` actions.

The root `h8mod.ps1` launcher is the preferred no-Unity entry point for humans. VS Code tasks call that launcher directly, including first playable mod creation, local discovery install, local Mods diagnosis, dependency editing, disabled graph node creation, and explicit graph/settings/locale/asset replace applies. It delegates to these `Tools/*.ps1` scripts, prints `Docs/capabilities.md` for capability discovery, and does not add a second validation contract.

Run `create_first_mod.ps1` only through `h8mod.ps1 -Action first-mod` unless automation needs the inner tool. It sets identity, enables `cap.graph.command_draft`, creates and applies one `SpawnItem` graph node, one boolean setting, and one locale entry, then validates and builds `Reports/review_manifest.json`. Use `-Replace` for a rerunnable onboarding pass over the same sample IDs. Use `-BuildSubmission` when the first pass should also write the submission zip.

`prepare_mod.ps1` runs identity setup only when `-Id` is provided. Without `-Id` it validates the existing manifests and rebuilds `Reports/review_manifest.json` for the normal edit-review loop.

Run `list_allowed_opcodes.ps1` when editing `Graphs/main.h8graph.json`. It prints every currently allowed graph opcode alias and hex token from `Reference/allowed_opcodes.csv`; use either value in `Nodes[].Opcode`.

Run `configure_manifest_contract.ps1` when you need to declare public authoring capabilities or set explicit starter budgets without hand-editing `mod.h8manifest.json`. It accepts only the public capability allowlist, caps `MaxEnvelopesPerFrame` at `256`, caps `MaxAssetBytes` at `33554432`, refuses to lower budgets below current graph or asset manifest requirements, writes through a temp file, restores the previous manifest if validation fails, and then runs `validate_structure.ps1`. Capabilities are review metadata, not runtime rights.

Run `configure_dependencies.ps1` when you need to declare package dependencies without hand-editing two manifests. It edits `mod.h8manifest.json` and `mod.json` together, accepts `list`, `add`, `remove`, and `clear`, rejects invalid IDs, duplicate IDs, and self-dependencies, writes through temp files, restores both manifests on validation failure, and then runs `validate_structure.ps1`. Dependencies affect loader ordering diagnostics only; they do not grant runtime code execution rights.

Run `create_graph_node_snippet.ps1` when you want a safe starter node object. It writes `Generated/graph_node_snippet.json` after validating the node id, opcode, optional `ParametersJson` object, and optional disabled state against `Reference/allowed_opcodes.csv`; it also accepts a flat CLI fallback like `{Quantity:3,Item:demo}` when a shell strips JSON quotes. It never rewrites `Graphs/main.h8graph.json`.

Run `apply_graph_node_snippet.ps1` after generating a graph node snippet. It inserts the clean node into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph/manifest envelope budget to one when needed, writes through temp files, restores previous graph/manifest files if validation fails, and then runs `validate_structure.ps1`.

Run `create_settings_row_snippet.ps1` when you want a safe settings row object. It writes `Generated/settings_row_snippet.json` after validating the setting id, kind, and typed default value; it never rewrites `Tables/settings.h8table.json`.

Run `create_locale_entry_snippet.ps1` when you want a safe locale key/value object. It writes `Generated/locale_entry_snippet.json` after validating the key and localized value; it never rewrites `Locales/en.h8loc.json`.

Run `apply_settings_row_snippet.ps1` after generating a settings row snippet. It inserts the clean row into `Tables/settings.h8table.json`, strips snippet-only notes, rejects duplicate setting ids unless `-Replace` is explicit, writes through a temp file, restores the previous table if validation fails, and then runs `validate_structure.ps1`.

Run `apply_locale_entry_snippet.ps1` after generating a locale entry snippet. It inserts the key/value into `Locales/en.h8loc.json`, rejects duplicate locale keys unless `-Replace` is explicit, writes through a temp file, restores the previous locale file if validation fails, and then runs `validate_structure.ps1`.

Run `create_asset_entry_snippet.ps1` when you want a safe content asset manifest entry. Put the file under `Content/Assets/`, choose `data_blob`, `raw_texture`, or `audio_clip`, and use `-Crc32 auto -Bytes -1` to compute CRC32 and byte length from the file. It never rewrites `Content/assets.h8manifest.json`.

Run `apply_asset_entry_snippet.ps1` after generating an asset entry snippet. It verifies the referenced `Content/Assets/` file, inserts the clean entry into `Content/assets.h8manifest.json`, rejects duplicate asset ids unless `-Replace` is explicit, raises `mod.h8manifest.json` `Budgets.MaxAssetBytes` when needed, restores previous files if validation fails, and then runs `validate_structure.ps1`.

Run `validate_structure.ps1` before sending this folder to another tool or author.

This local validator checks only starter-kit structure, canonical IDs, manifest parity, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, content asset manifest id/kind/path/byte/CRC/budget constraints, graph opcode allowlist, graph node cap, graph budget parity, exact editor schema mappings, and envelope-only safety. It is not runtime verification.

Run `build_review_manifest.ps1` before submitting a starter folder for review. It runs the structure validator first, then writes `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes. `Generated/` and `Reports/` are excluded from the hash list so reports do not hash themselves. The source side is bounded at `256` files, `4194304` bytes per file, and `33554432` total bytes; oversized source files fail before hashing.

Run `build_submission_package.ps1` when you need one artifact to hand off. It runs prepare, then writes `Generated/<mod-id>_submission.zip` with the reviewed starter sources plus `Reports/review_manifest.json`. It writes the replacement to a temp zip first and restores the previous submission zip if final replacement fails. This is a review/submission package only; it does not claim runtime loading.

Run `install_local_mod.ps1` when you need the current reviewed package visible to a local project or built game Mods folder. It runs prepare, verifies every reviewed source file against `Reports/review_manifest.json` byte counts and SHA-256 hashes, writes through a staging directory, restores the previous `Mods/<mod-id>` copy on failure, and copies `Reports/review_manifest.json` beside the source files. This is loader discovery only; managed entry and loose content ingestion remain disabled.

Run `diagnose_local_mods.ps1` when you need to know what the local project or built game will see under `Mods/`. It is read-only: it mirrors recursive loader `mod.json` discovery, checks the same loader caps for manifest bytes, manifest count, top-level DLLs, bundles, and `lang_*.json` files, validates basic `mod.json` fields, verifies `Reports/review_manifest.json` file hashes when present, resolves duplicate IDs, missing dependencies, dependency cycles, and load order, then prints the exact envelope-only reason for packages that are discoverable but disabled by managed entry or loose-content boundaries.

Run `set_mod_identity.ps1` once when you copy the starter kit. It writes the same canonical mod id, display name, author, and version into `mod.h8manifest.json` and `mod.json`, then runs the structure validator.

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action first-mod -Id com.yourname.firstmod -DisplayName "First HECTON Mod" -Author "YourName" -Version 0.1.0 -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action validate
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action review
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action capabilities
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action manifest-contract -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action dependencies -DependencyAction add -DependencyId com.example.library
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action install-local -ProjectRoot ..\.. -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action diagnose-local -ProjectRoot ..\..
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes-json
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action node-snippet -NodeId node.spawn_item -Opcode SpawnItem -NodeParametersJson '{}'
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-node-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setting-snippet -SettingId setting.example_toggle -SettingKind bool -SettingDefault false
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action locale-snippet -LocaleKey text.example_line -LocaleValue "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-setting-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-locale-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action asset-snippet -AssetId asset.example_blob -AssetKind data_blob -AssetPath Content/Assets/example.bytes -AssetCrc32 auto -AssetBytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-asset-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/configure_manifest_contract.ps1 -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/configure_dependencies.ps1 -Action add -DependencyId com.example.library
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_first_mod.ps1 -Id com.yourname.firstmod -DisplayName "First HECTON Mod" -Author "YourName" -Version 0.1.0 -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_graph_node_snippet.ps1 -Id node.spawn_item -Opcode SpawnItem -ParametersJson '{}'
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_graph_node_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_settings_row_snippet.ps1 -Id setting.example_toggle -Kind bool -Default false
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_locale_entry_snippet.ps1 -Key text.example_line -Value "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_asset_entry_snippet.ps1 -Id asset.example_blob -Kind data_blob -Path Content/Assets/example.bytes -Crc32 auto -Bytes -1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_settings_row_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_locale_entry_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_asset_entry_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_submission_package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/install_local_mod.ps1 -ProjectRoot ..\.. -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/diagnose_local_mods.ps1 -ProjectRoot ..\..
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1
```
