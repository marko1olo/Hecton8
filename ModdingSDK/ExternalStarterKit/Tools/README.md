# Tools

Fast path for a copied starter kit:

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

Submission handoff:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The scripts normalize child paths internally; do not rewrite `Tools/`, `Reports/`, or `.vscode/` paths per platform.

The root `h8mod.ps1` launcher is the preferred no-Unity entry point for humans. It delegates to these `Tools/*.ps1` scripts, prints `Docs/capabilities.md` for capability discovery, and does not add a second validation contract.

`prepare_mod.ps1` runs identity setup only when `-Id` is provided. Without `-Id` it validates the existing manifests and rebuilds `Reports/review_manifest.json` for the normal edit-review loop.

Run `list_allowed_opcodes.ps1` when editing `Graphs/main.h8graph.json`. It prints every currently allowed graph opcode alias and hex token from `Reference/allowed_opcodes.csv`; use either value in `Nodes[].Opcode`.

Run `create_graph_node_snippet.ps1` when you want a safe starter node object. It writes `Generated/graph_node_snippet.json` after validating the node id and opcode against `Reference/allowed_opcodes.csv`; it never rewrites `Graphs/main.h8graph.json`.

Run `apply_graph_node_snippet.ps1` after generating a graph node snippet. It inserts the clean node into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph/manifest envelope budget to one when needed, writes through temp files, restores previous graph/manifest files if validation fails, and then runs `validate_structure.ps1`.

Run `create_settings_row_snippet.ps1` when you want a safe settings row object. It writes `Generated/settings_row_snippet.json` after validating the setting id, kind, and typed default value; it never rewrites `Tables/settings.h8table.json`.

Run `create_locale_entry_snippet.ps1` when you want a safe locale key/value object. It writes `Generated/locale_entry_snippet.json` after validating the key and localized value; it never rewrites `Locales/en.h8loc.json`.

Run `apply_settings_row_snippet.ps1` after generating a settings row snippet. It inserts the clean row into `Tables/settings.h8table.json`, strips snippet-only notes, rejects duplicate setting ids unless `-Replace` is explicit, writes through a temp file, restores the previous table if validation fails, and then runs `validate_structure.ps1`.

Run `apply_locale_entry_snippet.ps1` after generating a locale entry snippet. It inserts the key/value into `Locales/en.h8loc.json`, rejects duplicate locale keys unless `-Replace` is explicit, writes through a temp file, restores the previous locale file if validation fails, and then runs `validate_structure.ps1`.

Run `validate_structure.ps1` before sending this folder to another tool or author.

This local validator checks only starter-kit structure, canonical IDs, manifest parity, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, graph opcode allowlist, graph node cap, graph budget parity, exact editor schema mappings, and envelope-only safety. It is not runtime verification.

Run `build_review_manifest.ps1` before submitting a starter folder for review. It runs the structure validator first, then writes `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes. `Generated/` and `Reports/` are excluded from the hash list so reports do not hash themselves. The source side is bounded at `256` files, `4194304` bytes per file, and `33554432` total bytes; oversized source files fail before hashing.

Run `build_submission_package.ps1` when you need one artifact to hand off. It runs prepare, then writes `Generated/<mod-id>_submission.zip` with the reviewed starter sources plus `Reports/review_manifest.json`. It writes the replacement to a temp zip first and restores the previous submission zip if final replacement fails. This is a review/submission package only; it does not claim runtime loading.

Run `set_mod_identity.ps1` once when you copy the starter kit. It writes the same canonical mod id, display name, author, and version into `mod.h8manifest.json` and `mod.json`, then runs the structure validator.

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action validate
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action review
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action capabilities
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes-json
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action node-snippet -NodeId node.spawn_item -Opcode SpawnItem
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-node-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setting-snippet -SettingId setting.example_toggle -SettingKind bool -SettingDefault false
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action locale-snippet -LocaleKey text.example_line -LocaleValue "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-setting-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-locale-snippet
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_graph_node_snippet.ps1 -Id node.spawn_item -Opcode SpawnItem
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_graph_node_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_settings_row_snippet.ps1 -Id setting.example_toggle -Kind bool -Default false
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_locale_entry_snippet.ps1 -Key text.example_line -Value "Your localized text"
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_settings_row_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_locale_entry_snippet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_submission_package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1
```
