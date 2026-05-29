# HECTON-8 External Mod Starter Kit

This folder is for public mod authors working outside the HECTON-8 Unity project.

First setup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

After edits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
```

Submission handoff:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission
```

Optional menu:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The tools normalize child paths internally; do not rewrite the folder layout per platform. In VS Code, change `hecton8.powerShellExecutable` in `.vscode/settings.json` to `pwsh`, then run `Tasks: Run Task` for the same no-Unity actions.

Do you need Unity?

- No Unity project is required for manifest, graph, table, locale, content asset declaration, and validation authoring.
- Read `Docs/capabilities.md` first. It is the current source of truth for what modders can and cannot do with this starter kit.
- If you do use the HECTON-8 Unity project, open `Hecton/Modding/External Starter Kit Workbench`; it can create/refresh missing starter files, shows required starter-file health and Capability Matrix, configures manifest capabilities/budgets, runs these same tools asynchronously, generates graph/settings/locale/content asset snippets, applies graph/settings/locale/content asset snippets with validation and rollback, exposes Graph Opcode Picker, Parameters JSON, disabled-node, asset kind picker, CRC/byte fields, and replace-on-apply controls, opens the core contracts, and shows review summary plus review manifest freshness without changing the file contract.
- Unity is also useful for advanced asset preview.
- Do not ship Harmony, BepInEx, or gameplay DLL patches. Current runtime UGC ingress is envelope-only.

Current runtime boundary:

- managed DLL gameplay execution is disabled;
- loose AssetBundle, PNG, and localization runtime ingestion are disabled;
- supported gameplay ingress is validated 64-byte FutureCommandEnvelope packets after SDK bake/approval;
- this starter kit is an authoring skeleton, not a runtime-verification stamp.

Files:

- `h8mod.ps1`: root no-Unity launcher for setup, validate, review, prepare, submission package build, opcode discovery, manifest capability/budget configuration, graph/settings/locale/content asset snippets, graph/settings/locale/content asset snippet apply, and capability-matrix display. `node-snippet` accepts `-NodeParametersJson` and `-NodeDisabled`; parameters accept strict JSON or a flat CLI fallback like `{Quantity:3,Item:demo}`. `asset-snippet` accepts `-AssetCrc32 auto` and `-AssetBytes -1` when the file exists. It delegates to `Tools/*.ps1` and is not a runtime install contract.
- `Docs/capabilities.md`: current capability matrix for public authors: supported authoring surfaces, forbidden runtime rights, and expansion route.
- `mod.h8manifest.json`: authoring manifest for Workbench/CLI style tools.
- `mod.json`: loader compatibility manifest; `EntryAssembly` and `EntryType` stay empty in envelope-only mode.
- `Graphs/main.h8graph.json`: command graph draft. Empty graph emits no packets. Non-empty nodes must use opcode hex tokens or comment aliases from `Reference/allowed_opcodes.csv`.
- `Tables/settings.h8table.json`: user-facing config table draft. Rows use canonical `Id`, lower-case `Kind` (`bool`, `int`, `float`, `string`, `enum`), and a matching `Default` value.
- `Content/assets.h8manifest.json` and `Content/Assets/`: CRC/asset declaration draft. Use `asset-snippet` and `apply-asset-snippet` to avoid hand-editing entries. Runtime use requires approval.
- `Locales/en.h8loc.json`: locale draft. `Locale` uses `xx` or `xx-YY`; string keys use the same canonical id form as other starter data. Runtime injection is not a public right yet.
- `Generated/`: SDK-produced binary output goes here. Do not hand-write `.h8bin` files.
- `Reports/`: validator, review, and future package reports go here.
- `Reference/`: copied opcode and tuning CSV references from the project docs.
- `Schemas/`: JSON Schemas for editor autocomplete and schema-aware validation.
- `.vscode/settings.json`: optional VS Code JSON schema mapping plus `hecton8.powerShellExecutable` override for the task runner. The local validator checks the expected schema URL/fileMatch pairs and rejects invalid settings/locale data before review packaging.
- `.vscode/tasks.json`: VS Code Tasks surface for setup, validate, prepare, submission, capability/opcode discovery, snippet creation/apply, disabled graph node creation, explicit replace apply actions, and manifest contract edits. Tasks route through `h8mod.ps1` only; they do not bypass validation or create runtime rights.
- `Tools/prepare_mod.ps1`: one-command no-Unity setup/review loop. With `-Id` it writes identity, validates, and builds the review manifest; without `-Id` it validates existing manifests and rebuilds the review manifest.
- `Tools/build_submission_package.ps1`: local no-Unity submission packer. It runs prepare, then writes `Generated/<mod-id>_submission.zip` containing the reviewed starter sources plus `Reports/review_manifest.json`. It writes to a temp zip first and restores the previous submission zip if final replacement fails. This is a review handoff artifact, not a runtime install stamp.
- `Tools/configure_manifest_contract.ps1`: local no-Unity manifest helper that enables/disables public authoring capabilities and sets capped budgets with validation and rollback. Capabilities are review metadata, not runtime rights.
- `Tools/list_allowed_opcodes.ps1`: local no-Unity graph helper that prints the allowed opcode aliases and hex tokens accepted by `Graphs/main.h8graph.json`.
- `Tools/create_graph_node_snippet.ps1`: local no-Unity graph helper that writes `Generated/graph_node_snippet.json` from a validated node id, allowed opcode, optional `ParametersJson` object or flat CLI fallback, and optional disabled state; it does not mutate `Graphs/main.h8graph.json`.
- `Tools/apply_graph_node_snippet.ps1`: local no-Unity graph helper that inserts `Generated/graph_node_snippet.json` into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph/manifest envelope budget to one when needed, validates after the atomic temp-write, and restores previous graph/manifest files on failure.
- `Tools/create_settings_row_snippet.ps1`: local no-Unity settings helper that writes `Generated/settings_row_snippet.json` from a validated setting id, kind, and typed default; it does not mutate `Tables/settings.h8table.json`.
- `Tools/create_locale_entry_snippet.ps1`: local no-Unity locale helper that writes `Generated/locale_entry_snippet.json` from a validated locale key and text value; it does not mutate `Locales/en.h8loc.json`.
- `Tools/apply_settings_row_snippet.ps1`: local no-Unity settings helper that inserts `Generated/settings_row_snippet.json` into `Tables/settings.h8table.json`, rejects duplicates unless `-Replace` is explicit, validates after the atomic temp-write, and restores the previous table on failure.
- `Tools/apply_locale_entry_snippet.ps1`: local no-Unity locale helper that inserts `Generated/locale_entry_snippet.json` into `Locales/en.h8loc.json`, rejects duplicates unless `-Replace` is explicit, validates after the atomic temp-write, and restores the previous locale file on failure.
- `Tools/create_asset_entry_snippet.ps1`: local no-Unity content helper that writes `Generated/asset_entry_snippet.json` from a canonical asset id, kind, `Content/Assets/` path, CRC32, and byte length. Use `-Crc32 auto` and `-Bytes -1` to compute them from an existing file.
- `Tools/apply_asset_entry_snippet.ps1`: local no-Unity content helper that inserts `Generated/asset_entry_snippet.json` into `Content/assets.h8manifest.json`, verifies the file CRC/bytes, rejects duplicate asset ids unless `-Replace` is explicit, raises `MaxAssetBytes`, validates after the atomic temp-write, and restores previous files on failure.
- `Tools/validate_structure.ps1`: local no-Unity structure validator for required files, canonical IDs, manifest parity, content asset manifest path/CRC/byte/budget constraints, graph opcode allowlist checks, graph node cap, graph budget parity, envelope-only flags, and managed-entry disablement.
- `Tools/build_review_manifest.ps1`: local no-Unity review manifest builder that validates first, then writes `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes for submission/review. It rejects more than `256` source files, any source file over `4194304` bytes, or more than `33554432` total source bytes before hashing.
- `Tools/set_mod_identity.ps1`: local no-Unity identity helper that safely writes matching mod id/name/author/version values into both manifests, then validates the folder.
