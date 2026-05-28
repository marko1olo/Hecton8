# Tools

Fast path for a copied starter kit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
```

Normal edit-review loop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1
```

Use `pwsh` instead of `powershell` on macOS/Linux with PowerShell 7. The scripts normalize child paths internally; do not rewrite `Tools/`, `Reports/`, or `.vscode/` paths per platform.

`prepare_mod.ps1` runs identity setup only when `-Id` is provided. Without `-Id` it validates the existing manifests and rebuilds `Reports/review_manifest.json` for the normal edit-review loop.

Run `list_allowed_opcodes.ps1` when editing `Graphs/main.h8graph.json`. It prints every currently allowed graph opcode alias and hex token from `Reference/allowed_opcodes.csv`; use either value in `Nodes[].Opcode`.

Run `validate_structure.ps1` before sending this folder to another tool or author.

This local validator checks only starter-kit structure, canonical IDs, manifest parity, graph opcode allowlist, graph budget parity, exact editor schema mappings, and envelope-only safety. It is not runtime verification.

Run `build_review_manifest.ps1` before submitting a starter folder for review. It runs the structure validator first, then writes `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes. `Generated/` and `Reports/` are excluded from the hash list so reports do not hash themselves. The source side is bounded at `256` files, `4194304` bytes per file, and `33554432` total bytes; oversized source files fail before hashing.

Run `set_mod_identity.ps1` once when you copy the starter kit. It writes the same canonical mod id, display name, author, and version into `mod.h8manifest.json` and `mod.json`, then runs the structure validator.

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1
```
