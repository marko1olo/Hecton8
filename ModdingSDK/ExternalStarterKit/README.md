# HECTON-8 External Mod Starter Kit

This folder is for public mod authors working outside the HECTON-8 Unity project.

First setup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

Fast first playable mod:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action first-mod -Id com.yourname.firstmod -DisplayName "First HECTON Mod" -Author "YourName" -Version 0.1.0 -Replace
```

After edits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action doctor
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

Dependency edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action dependencies -DependencyAction add -DependencyId com.example.library
```

Optional menu:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The tools normalize child paths internally; do not rewrite the folder layout per platform. In VS Code, change `hecton8.powerShellExecutable` in `.vscode/settings.json` to `pwsh`, then run `Tasks: Run Task` for the same no-Unity actions.

Do you need Unity?

- No Unity project is required for manifest, graph, table, locale, content asset declaration, and validation authoring.
- Read `Docs/capabilities.md` first. It is the current source of truth for what modders can and cannot do with this starter kit; `h8mod.ps1 -Action capabilities` prints it through the shared strict capped UTF-8 reader.
- If you do use the HECTON-8 Unity project, open `Hecton/Modding/External Starter Kit Workbench`; it can create/refresh missing starter files, shows required starter-file health and Capability Matrix, configures manifest capabilities/budgets/dependencies, runs these same tools asynchronously, generates graph/settings/locale/content asset snippets, applies graph/settings/locale/content asset snippets with validation and rollback, exposes Graph Opcode Picker, Parameters JSON, disabled-node, asset kind picker, CRC/byte fields, replace-on-apply controls, package doctor with submission zip integrity, local discovery install, read-only local Mods diagnosis, opens the core contracts, and shows review summary plus review manifest freshness without changing the file contract.
- Unity is also useful for advanced asset preview.
- Do not ship Harmony, BepInEx, or gameplay DLL patches. Current runtime UGC ingress is envelope-only.

Current runtime boundary:

- managed DLL gameplay execution is disabled;
- loose AssetBundle, PNG, and localization runtime ingestion are disabled;
- supported gameplay ingress is validated 64-byte FutureCommandEnvelope packets after SDK bake/approval;
- this starter kit is an authoring skeleton, not a runtime-verification stamp.

Files:

- `h8mod.ps1`: root no-Unity launcher for first playable mod creation, package doctor, local discovery install, local Mods diagnosis, dependency editing, setup, validate, review, prepare, submission package build, opcode discovery, manifest capability/budget configuration, graph/settings/locale/content asset snippets, graph/settings/locale/content asset snippet apply, and capability-matrix display. `first-mod` sets identity, enables the graph authoring capability, creates/applies one graph node, one setting, one locale entry, validates, and builds `Reports/review_manifest.json`. `doctor` validates the starter structure, checks current source files against review manifest freshness, verifies submission zip freshness and case-exact zip entry hashes against the review manifest, and prints next actions without mutating files. `install-local` copies the reviewed package to `Mods/<mod-id>` for loader discovery only after hash/byte verification. `diagnose-local` inspects a local project or game `Mods/` folder recursively, reports manifest/review/runtime-boundary status, duplicate IDs, missing dependencies, dependency cycles, and load-order preview without mutating files. `dependencies` edits dependency IDs in both manifests, rejects invalid/self/duplicate dependencies, writes strict UTF-8 without BOM, validates, and rolls back on failure. `node-snippet` accepts `-NodeParametersJson` and `-NodeDisabled`; parameters accept strict JSON or a flat CLI fallback like `{Quantity:3,Item:demo}`. `asset-snippet` accepts `-AssetCrc32 auto` and `-AssetBytes -1` when the file exists. It delegates to `Tools/*.ps1`, preserves their exit codes, nested parent tools preserve child exit codes, and it is not a runtime activation contract.
- `Docs/capabilities.md`: current capability matrix for public authors: supported authoring surfaces, forbidden runtime rights, and expansion route.
- `mod.h8manifest.json`: authoring manifest for Workbench/CLI style tools. `Dependencies` mirrors `mod.json` and should be edited through `h8mod.ps1 -Action dependencies`.
- `mod.json`: loader compatibility manifest; `EntryAssembly` and `EntryType` stay empty in envelope-only mode. `Dependencies` is loader metadata, not runtime authority.
- `Graphs/main.h8graph.json`: command graph draft. Empty graph emits no packets. Non-empty nodes must use opcode hex tokens or comment aliases from `Reference/allowed_opcodes.csv`.
- `Tables/settings.h8table.json`: user-facing config table draft. Rows use canonical `Id`, lower-case `Kind` (`bool`, `int`, `float`, `string`, `enum`), and a matching `Default` value.
- `Content/assets.h8manifest.json` and `Content/Assets/`: CRC/asset declaration draft. Use `asset-snippet` and `apply-asset-snippet` to avoid hand-editing entries. Runtime use requires approval.
- `Locales/en.h8loc.json`: locale draft. `Locale` uses `xx` or `xx-YY`; string keys use the same canonical id form as other starter data. Runtime injection is not a public right yet.
- `Generated/`: SDK-produced binary output goes here. Do not hand-write `.h8bin` files.
- `Reports/`: validator, review, and future package reports go here.
- Top-level folder names are case-exact: `Content`, `Docs`, `Generated`, `Graphs`, `Locales`, `Reference`, `Reports`, `Schemas`, `Tables`, `Tools`, and `.vscode`. Do not rename them to `content`, `reports`, `generated`, or other case variants; validation, review, submission, local install, local diagnosis, doctor, and Workbench health treat those as invalid portable package layout.
- `Reference/`: copied opcode and tuning CSV references from the project docs.
- `Schemas/`: JSON Schemas for editor autocomplete and schema-aware validation.
- `.vscode/settings.json`: optional VS Code JSON schema mapping plus `hecton8.powerShellExecutable` override for the task runner. The local validator checks the expected schema URL/fileMatch pairs and rejects invalid settings/locale data before review packaging.
- `.vscode/tasks.json`: VS Code Tasks surface for first playable mod creation, package doctor, local discovery install, local Mods diagnosis, setup, validate, prepare, submission, capability/opcode discovery, snippet creation/apply, disabled graph node creation, explicit replace apply actions, and manifest contract edits. Tasks route through `h8mod.ps1` only; they do not bypass validation or create runtime rights.
- `Tools/strict_json_io.ps1`: shared no-Unity JSON/text ingestion helper. Tool scripts use it to stream under hard byte caps, reject invalid UTF-8, and parse JSON only after the cap and encoding are proven.
- `Tools/prepare_mod.ps1`: one-command no-Unity setup/review loop. With `-Id` it writes identity, validates, and builds the review manifest; without `-Id` it validates existing manifests and rebuilds the review manifest. Core starter tools cap external JSON/text reads through `strict_json_io.ps1` before parsing so oversized or invalid-UTF-8 copied files fail before object allocation.
- `Tools/build_submission_package.ps1`: local no-Unity submission packer. It runs prepare with exact `Reports/review_manifest.json`, verifies review byte/SHA-256 rows, then writes `Generated/<mod-id>_submission.zip` containing the reviewed starter sources plus `Reports/review_manifest.json`. It writes to a temp zip first, restores the previous submission zip if final replacement fails, and keeps the final zip timestamp at or after the rebuilt review manifest so `doctor` does not mark a fresh handoff stale. Package entries are case-exact and duplicate or case-fold duplicate source paths fail before zip write. This is a review handoff artifact, not a runtime install stamp.
- `Tools/run_doctor.ps1`: local no-Unity package readiness doctor. It is read-only: validates the starter structure, compares current source files with `Reports/review_manifest.json` hashes, checks submission zip freshness, opens the zip without extraction, verifies packaged source entries and exact-cased `Reports/review_manifest.json` against expected hashes, rejects reserved top-level folder case variants, case-fold duplicate paths plus duplicate/unsafe/unreviewed entries, summarizes counts, and prints exact next actions before handoff. It exits `0` only for `ready`, `2` for `needs_review`, and `1` for `invalid`; the root launcher preserves those exit codes for VS Code/CI.
- `Tools/install_local_mod.ps1`: local no-Unity discovery installer. It runs prepare, requires exact `Reports/review_manifest.json`, verifies source files against byte counts and exact lower-case SHA-256 hashes, rejects reserved folder case variants plus duplicate/case-fold duplicate review entries, then atomically copies the reviewed source set plus the review manifest into `Mods/<mod-id>`. This is loader discovery only; managed entry and loose content ingestion stay disabled.
- `Tools/diagnose_local_mods.ps1`: local no-Unity, read-only Mods inspector. It resolves `ProjectRoot/Mods` or `-ModsRoot`, recursively mirrors loader `mod.json` discovery, applies the same loader caps for manifests, top-level DLLs, bundles, and `lang_*.json`, validates `mod.json`, verifies installed exact `Reports/review_manifest.json` hashes when present, rejects non-lowercase SHA-256 proof and duplicate/case-fold duplicate review entries, marks missing or invalid review proof as `INVALID`, resolves duplicate IDs, missing dependencies, dependency cycles, and load order, then prints whether each package is invalid or discoverable but disabled by the envelope-only runtime boundary.
- `Tools/configure_dependencies.ps1`: local no-Unity dependency helper. It edits `Dependencies` in `mod.h8manifest.json` and `mod.json` together, rejects invalid IDs, duplicates, and self-dependencies, writes through temp files as strict UTF-8 without BOM, validates after write, and restores both manifests on failure.
- `Tools/configure_manifest_contract.ps1`: local no-Unity manifest helper that enables/disables public authoring capabilities and sets capped budgets with validation and rollback. Capabilities are review metadata, not runtime rights.
- `Tools/create_first_mod.ps1`: local no-Unity onboarding helper. It runs the bounded identity, manifest contract, graph snippet/apply, settings snippet/apply, locale snippet/apply, validation, and review-manifest tools in sequence. `-Replace` makes the starter onboarding rerunnable for the same sample IDs. `-BuildSubmission` also writes the review zip.
- `Tools/list_allowed_opcodes.ps1`: local no-Unity graph helper that prints the allowed opcode aliases and hex tokens accepted by `Graphs/main.h8graph.json`.
- `Tools/create_graph_node_snippet.ps1`: local no-Unity graph helper that writes exact starter-relative `Generated/*.json` output from a validated node id, allowed opcode, optional `ParametersJson` object capped before parse or flat CLI fallback, and optional disabled state; it does not mutate `Graphs/main.h8graph.json`.
- `Tools/apply_graph_node_snippet.ps1`: local no-Unity graph helper that caps snippet/graph/manifest JSON reads before parsing, inserts `Generated/graph_node_snippet.json` into `Graphs/main.h8graph.json`, rejects duplicate node ids unless `-Replace` is explicit, raises graph/manifest envelope budget to one when needed, validates after the atomic temp-write, and restores previous graph/manifest files on failure.
- `Tools/create_settings_row_snippet.ps1`: local no-Unity settings helper that writes exact starter-relative `Generated/*.json` output from a validated setting id, kind, and typed default; it does not mutate `Tables/settings.h8table.json`.
- `Tools/create_locale_entry_snippet.ps1`: local no-Unity locale helper that writes exact starter-relative `Generated/*.json` output from a validated locale key and text value; it does not mutate `Locales/en.h8loc.json`.
- `Tools/apply_settings_row_snippet.ps1`: local no-Unity settings helper that caps snippet/table JSON reads before parsing, inserts `Generated/settings_row_snippet.json` into `Tables/settings.h8table.json`, rejects duplicates unless `-Replace` is explicit, validates after the atomic temp-write, and restores the previous table on failure.
- `Tools/apply_locale_entry_snippet.ps1`: local no-Unity locale helper that caps snippet/locale JSON reads before parsing, inserts `Generated/locale_entry_snippet.json` into `Locales/en.h8loc.json`, rejects duplicates unless `-Replace` is explicit, validates after the atomic temp-write, and restores the previous locale file on failure.
- `Tools/create_asset_entry_snippet.ps1`: local no-Unity content helper that writes exact starter-relative `Generated/*.json` output from a canonical asset id, kind, portable `Content/Assets/` path, CRC32, and byte length. It rejects ADS/colon, rooted, empty-segment, dot, and dot-dot paths before CRC probing. Use `-Crc32 auto` and `-Bytes -1` to compute proof from an existing file.
- `Tools/apply_asset_entry_snippet.ps1`: local no-Unity content helper that caps snippet/manifest JSON reads before parsing, inserts `Generated/asset_entry_snippet.json` into `Content/assets.h8manifest.json`, verifies the file CRC/bytes, rejects duplicate asset ids unless `-Replace` is explicit, raises `MaxAssetBytes`, validates after the atomic temp-write, and restores previous files on failure.
- `Tools/validate_structure.ps1`: local no-Unity structure validator for required files, canonical IDs, manifest parity, capped root JSON/text reads, content asset manifest path/CRC/byte/budget constraints, graph opcode allowlist checks, graph node cap, graph budget parity, envelope-only flags, and managed-entry disablement. Manual asset manifest paths use the same portable `Content/Assets/` path rules as asset snippets.
- `Tools/build_review_manifest.ps1`: local no-Unity review manifest builder that validates first, then writes exact-cased `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit source limits, and lowercase SHA-256 hashes for submission/review. It excludes only exact `Generated/` and `Reports/`, rejects reserved top-level folder case variants, rejects more than `256` source files, any source file over `4194304` bytes, more than `33554432` total source bytes, and duplicate or case-fold duplicate source paths before hashing.
- `Tools/set_mod_identity.ps1`: local no-Unity identity helper that safely writes matching mod id/name/author/version values into both manifests, strict-reads the written JSON, validates the folder, and restores both manifests if validation fails.
