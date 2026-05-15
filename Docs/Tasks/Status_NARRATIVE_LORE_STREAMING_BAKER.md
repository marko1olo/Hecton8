# NARRATIVE_LORE_STREAMING_BAKER - Binary Lore Compiler

Prompt role: BACKEND_ENGINEER
Domain: ECHELON 8 / PDA Encyclopedia Streaming / Lore Data Backend
Task count: 6 explicit numbered tasks.
Status: LORE BAKED / PYTHON VERIFIED / PENDING UNITY VERIFICATION

Mandates loaded:
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- STRM_ModuleDTO_LZ4_Dictionary.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

Source reality:
- Extracted XML prompt ID: `NARRATIVE_LORE_STREAMING_BAKER`.
- Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains this prompt after batch rotation; scope remains the previously extracted lore compiler assignment recorded here.
- `Docs/Lore/` now exists and contains canonical source `Docs/Lore/Lore_Bible.md`.
- `Docs/Design/Lore_Bible.md` is a redirect stub to the canonical source.
- Output target required by prompt: `Data/Lore/Encyclopedia.h8bin`.
- Sidecar manifest: `Data/Lore/Encyclopedia.manifest.json`.

## Checklist

- [x] Task 1 - DIRECTORY SCAN | DOD: enumerated source `.md` files in literal prompt directory `Docs/Lore/`; canonical source `Docs/Lore/Lore_Bible.md` | Alternatives rejected: baking redirect stubs, archive lore, or Cyrillic-damaged root text | Estimate: 0 us runtime, offline bake only
- [x] Task 2 - HASH TABLE | DOD: generated sorted 16-byte records `uint hash, long offset, int length`; canonical lore hash `0xD1880394` | Alternatives rejected: runtime string keys or unsorted scan table | Estimate: 0 us runtime, fixed binary lookup data only
- [x] Task 3 - COMPRESSION | DOD: zlib-compressed Markdown payload at level 9; compressed record length 10281 bytes for the current 25003-byte source | Alternatives rejected: uncompressed UTF-16 MMF payload from existing lazy proxy | Estimate: 0 us runtime until a loader exists
- [x] Task 4 - BINARY OUTPUT | DOD: generated `Data/Lore/Encyclopedia.h8bin`, 10329 bytes, payload offset 48 | Alternatives rejected: writing into `Assets/StreamingAssets` without prompt authority | Estimate: 0 us runtime, data artifact only
- [x] Task 5 - SELF-AUDIT | DOD: added `Tools/VerifyLore.py` and `Tools/test_verify_lore.py`; direct extraction by hash `0xD1880394` and by source path `Docs/Lore/Lore_Bible.md` both matched current source SHA-256 `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`; 19 regression tests now cover source-path hashing, manifest tamper, manifest-vs-source payload mismatch rejection, missing prompt source fallback rejection, H8DataHash empty-input zero behavior, metadata mismatch rejection, in-memory blob validation, malformed blob rejection, unsorted table rejection, payload overlap rejection, nonzero padding rejection, trailing byte rejection, cwd-independent packaging checks, and current artifact validation | Alternatives rejected: one-off bake without repeatable extraction proof, cwd-sensitive tooling, manifest-only stale-blob proof, or temp-directory-dependent tests | Estimate: 0 us runtime, offline verification only
- [x] Task 6 - BYTE ALIGNMENT | DOD: `Tools/VerifyLore.py --verify-source --list` asserted 32-byte header, 16-byte table record, and 16-byte aligned payload offset 48 | Alternatives rejected: packed variable header that forces reader-side guesswork | Estimate: 0 us runtime, load-time alignment benefit pending runtime loader

## Loop State

Loop 1: mandate intake and source discovery - COMPLETE
Loop 2: bake script and binary generation - COMPLETE
Loop 3: verifier extraction pass - COMPLETE
Loop 4: compile/static validation - COMPLETE FOR PYTHON / DOTNET ENVIRONMENT BLOCKED
Loop 5: polish mandate and final log - COMPLETE

## Verification

- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, manifest written, `CHECK OK`, one record `0xD1880394 offset=48 length=10281`, file length `10329`.
- `python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text .codex-artifacts\NARRATIVE_LORE_STREAMING_BAKER_latest_extract.md` -> source/extract SHA-256 matched `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.
- Raw binary header inspection -> `H8LR`, version `1`, header size `32`, record size `16`, table offset `32`, payload offset `48`, flags `15`, all alignment remainders `0`, file length `10329`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 19 tests passed without bytecode writes or new scratch directories.
- `python -c "import ast, pathlib; ..."` -> `AST OK` for `Tools/VerifyLore.py` and `Tools/test_verify_lore.py`.
- `Push-Location Tools; python ..\Tools\VerifyLore.py --check; Pop-Location` -> `CHECK OK`, proving default source/blob/manifest paths are anchored to the repository root instead of process cwd; regression coverage also calls `read_blob` and `verify_manifest` with repo-relative paths from `Tools`.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed. Default pycache emission remains unreliable in this workspace, so the command redirects bytecode into the ignored artifact cache.
- `python Tools\VerifyLore.py --check` -> `CHECK OK: entries=1 blob=Data\Lore\Encyclopedia.h8bin manifest=Data\Lore\Encyclopedia.manifest.json`.
- `Data\Lore\Encyclopedia.manifest.json` written with `entry_count=1`, canonical id `Docs/Lore/Lore_Bible.md`, hash `0xD1880394`, offset `48`, compressed length `10281`, decompressed length `25003`, source SHA-256 match, blob length `10329`, blob SHA-256 `8FDBAC8752B5DB10B98226D88BC5A27EEDA049207E139E6F2F3FB15ECDBDDC00`.
- `python Tools\VerifyLore.py --verify-source --verify-manifest --list` -> source and manifest verification passed.
- `dotnet build Hecton8.Core.csproj --no-restore ...` -> BLOCKED: `dotnet` command not found; standard `C:\Program Files\dotnet\dotnet.exe` and `C:\Program Files (x86)\dotnet\dotnet.exe` absent.
- Visual Studio private dotnet runtime found at `C:\Program Files\Microsoft Visual Studio\2022\Community\dotnet\net8.0\runtime\dotnet.exe`, but it has no SDK and cannot execute `build`.
- MSBuild exists, but `Hecton8.slnx` references generated `.csproj` files that are absent; MSBuild reported 71 missing project errors including `Hecton8.Core.csproj`.
- Unity project pin is `6000.4.1f1`; no `Unity.exe` found under checked standard install roots, so Unity batchmode compile could not be run.

## Polish Pass

- `<POLISH_MANDATE>` tag was absent from `Docs/Tasks/CURRENT_BATCH.md`.
- Self-audit found absolute source paths would have changed canonical hashes; fixed by hashing repo-relative source paths and rejecting non-ASCII source paths.
- Second self-audit found source directories outside the repo could leak absolute path hashes; fixed by rejecting out-of-repo sources and added regression coverage.
- Third self-audit removed the prompt-path deviation by moving the active lore bible into `Docs/Lore/Lore_Bible.md`, leaving a redirect at `Docs/Design/Lore_Bible.md`, updating `Docs/README.md`, and rebaking.
- Fourth self-audit added a deterministic manifest sidecar and path-based extraction so operators do not need to hand-copy numeric hashes.
- Fifth self-audit added `--verify-manifest` plus a tamper test that rejects stale manifest SHA-256 data.
- Sixth self-audit added blob length/SHA-256 manifest checks plus a regression test that rejects appended-byte blob tampering.
- Seventh self-audit removed the obsolete `Docs/Design/Lore_Bible.md` fallback after canonical promotion so a missing `Docs/Lore` source now fails instead of silently baking a redirect stub.
- Eighth self-audit moved regression test scratch repositories under `.codex-artifacts/tmp` and removed `TemporaryDirectory` cleanup dependency because the current sandbox denies Python temp cleanup outside/directly under generated temp roots.
- Ninth self-audit compared `Tools/VerifyLore.py` hash behavior against `Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs` and preserved the engine contract that empty input returns `0`.
- Tenth self-audit rebaked after the concurrent lore source changed; current source SHA-256 is `A734EF38913EBE80474F71BBD355FFA1CB08EAD3195DA0331D4A336FEA1D1402`, current blob SHA-256 is `69E1631622B2FBCEAEE39A83A6F9CAEAC8715BCCE5F37C336F63F9E9D026B71B`.
- Eleventh self-audit split byte-level blob parsing, source verification, manifest construction, and manifest verification into pure helpers so tests no longer need disposable filesystem repositories.
- Twelfth self-audit added `--check` as the single packaging gate for source-vs-blob and manifest-vs-blob verification.
- Thirteenth self-audit added explicit record-table bounds and payload-overlap rejection to the blob parser, plus bad-magic and overlap regression tests.
- Fourteenth self-audit rebaked after another concurrent lore source update; current source SHA-256 is `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`, current blob SHA-256 is `8FDBAC8752B5DB10B98226D88BC5A27EEDA049207E139E6F2F3FB15ECDBDDC00`.
- Fifteenth self-audit added parser rejection for nonzero alignment padding and trailing bytes after the final payload.
- Sixteenth self-audit hardened the nonzero-padding regression so it searches for a guaranteed alignment gap instead of assuming one specific zlib output length.
- Seventeenth self-audit added `Data/Lore/README.md` as an operator runbook outside `Docs/Lore` so verification commands and binary layout are documented without becoming compiled lore content.
- Eighteenth self-audit made verifier CLI and imported helper paths repository-root anchored, added atomic `.tmp` + replace writes for blob/manifest/extracted Markdown, documented the operator behavior, and added cwd-independent plus unsorted-record regression tests.
- Nineteenth self-audit made manifest generation and manifest verification reject blob payloads that do not match the current source files, even if the manifest metadata matches the stale blob.
- Re-ran bake, source verification, list, extraction SHA-256 check, regression tests, AST syntax parsing, and py_compile with redirected pycache after polish fixes.
