# HECTON-8 External Mod Starter Kit

This folder is for public mod authors working outside the HECTON-8 Unity project.

Fast path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The tools normalize child paths internally; do not rewrite the folder layout per platform.

Do you need Unity?

- No Unity project is required for manifest, graph, table, locale, and validation authoring.
- Unity is only useful for advanced asset preview before a standalone Workbench or CLI exists.
- Do not ship Harmony, BepInEx, or gameplay DLL patches. Current runtime UGC ingress is envelope-only.

Current runtime boundary:

- managed DLL gameplay execution is disabled;
- loose AssetBundle, PNG, and localization runtime ingestion are disabled;
- supported gameplay ingress is validated 64-byte FutureCommandEnvelope packets after SDK bake/approval;
- this starter kit is an authoring skeleton, not a runtime-verification stamp.

Files:

- `mod.h8manifest.json`: authoring manifest for Workbench/CLI style tools.
- `mod.json`: loader compatibility manifest; `EntryAssembly` and `EntryType` stay empty in envelope-only mode.
- `Graphs/main.h8graph.json`: command graph draft. Empty graph emits no packets. Non-empty nodes must use opcode hex tokens or comment aliases from `Reference/allowed_opcodes.csv`.
- `Tables/settings.h8table.json`: user-facing config table draft.
- `Content/assets.h8manifest.json`: CRC/asset declaration draft. Runtime use requires approval.
- `Locales/en.h8loc.json`: locale draft. Runtime injection is not a public right yet.
- `Generated/`: SDK-produced binary output goes here. Do not hand-write `.h8bin` files.
- `Reports/`: validator, review, and future package reports go here.
- `Reference/`: copied opcode and tuning CSV references from the project docs.
- `Schemas/`: JSON Schemas for editor autocomplete and schema-aware validation.
- `.vscode/settings.json`: optional VS Code JSON schema mapping for the starter files. The local validator checks the expected schema URL/fileMatch pairs.
- `Tools/prepare_mod.ps1`: one-command no-Unity setup that writes identity, validates, and builds the review manifest.
- `Tools/list_allowed_opcodes.ps1`: local no-Unity graph helper that prints the allowed opcode aliases and hex tokens accepted by `Graphs/main.h8graph.json`.
- `Tools/validate_structure.ps1`: local no-Unity structure validator for required files, canonical IDs, manifest parity, graph opcode allowlist checks, graph budget parity, envelope-only flags, and managed-entry disablement.
- `Tools/build_review_manifest.ps1`: local no-Unity review manifest builder that validates first, then writes `Reports/review_manifest.json` with sorted file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes for submission/review. It rejects more than `256` source files, any source file over `4194304` bytes, or more than `33554432` total source bytes before hashing.
- `Tools/set_mod_identity.ps1`: local no-Unity identity helper that safely writes matching mod id/name/author/version values into both manifests, then validates the folder.
