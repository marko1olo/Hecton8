# LOG - NARRATIVE_LORE_STREAMING_BAKER

## 2026-05-14 - Binary Lore Compiler

STATUS: LORE BAKED / PENDING UNITY VERIFICATION

What was wrong:
- `Docs/Lore/` does not exist in the current workspace, so the literal prompt source directory has no Markdown inputs.
- The active lore bible exists as `Docs/Design/Lore_Bible.md`.
- `Data/Lore/Encyclopedia.h8bin` did not exist before this task.
- `Tools/VerifyLore.py` did not exist before this task.

What was done:
- Added `Tools/VerifyLore.py`.
- Generated `Data/Lore/Encyclopedia.h8bin`.
- Binary layout: 32-byte little-endian `H8LR` header, 16-byte sorted records, absolute 16-byte aligned payload offsets, zlib-compressed Markdown bytes.
- Baked one source: `Docs/Design/Lore_Bible.md`.
- Record: `0x524EEAA6 -> offset 48, compressed length 4545`.
- Blob size: 4593 bytes.

Cinematic Cheats used:
- No physical simulation touched.
- Runtime parsing was avoided; Markdown is precompressed offline into a fixed binary lookup table.
- Existing runtime C# was not modified because the prompt required a blob and verifier, not a new loader contract.

Exact microseconds saved:
- Current task runtime frame impact: 0 us/frame, because no gameplay/runtime C# path was changed.
- Future loader savings: PENDING VERIFICATION; expected benefit is eliminating runtime Markdown directory scans and string-key lookup once a loader consumes the blob.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --list` returned `LORE BAKED`, `VERIFY OK`, and listed `0x524EEAA6 offset=48 length=4545`.
- `python Tools\VerifyLore.py 0x524EEAA6 --output-text .codex-artifacts\NARRATIVE_LORE_STREAMING_BAKER_polish_extract.md` extracted the source text by hash.
- Source and extracted text SHA-256 matched: `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- `python -m py_compile Tools\VerifyLore.py` returned `PY_COMPILE_OK`.
- C# compile guard was environment-blocked: `dotnet` command not found, and standard Windows dotnet install paths were absent.

Regression model:
- CPU: no runtime CPU changed; offline bake only.
- GC: no runtime GC changed; offline Python allocations are irrelevant to gameplay hot paths.
- Memory: new binary data artifact is 4593 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: verified by zlib decompression and byte-for-byte source comparison.

Failure modes:
- Unity runtime does not yet consume `Data/Lore/Encyclopedia.h8bin`; integration remains PENDING.
- `Docs/Lore/` absence is documented. Future `.md` files under `Docs/Lore/` will become the primary source automatically.
- C# build proof is absent due local SDK/toolchain unavailability.

## 2026-05-14 - Continuation Hardening Pass

STATUS: LORE BAKED / OFFLINE VERIFIER HARDENED / C# COMPILE ENVIRONMENT BLOCKED

What was wrong:
- Initial verifier had no regression test file.
- Source path canonicalization was stable for normal repo-relative use, but a stress check showed external absolute `--source-dir` paths could leak machine-specific paths into hashes.
- `dotnet build` was blocked, and the reason needed deeper proof instead of one PATH check.

What was done:
- Added `Tools/test_verify_lore.py`.
- Fixed verifier import-path testability.
- Enforced repo-root source identity in `Tools/VerifyLore.py`.
- Added regression coverage for multi-file bake/extract/verify, sorted records, payload alignment, absolute-vs-relative in-repo hash stability, missing hash lookup, and out-of-repo source rejection.
- Rebuilt `Data/Lore/Encyclopedia.h8bin` after verifier hardening.

Cinematic Cheats used:
- Still no physical simulation touched.
- Runtime work remains pre-baked: compressed binary records replace directory scans and Markdown parsing once a loader consumes the blob.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No profiler-backed runtime savings claimed. Loader integration is not present.

Verification:
- `python -m unittest Tools.test_verify_lore -v` -> 4 tests passed.
- `python Tools\test_verify_lore.py -v` -> 4 tests passed.
- `python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> pass.
- `python Tools\VerifyLore.py --bake --verify-source --list` -> `LORE BAKED`, `VERIFY OK`, `0x524EEAA6 offset=48 length=4545`.
- Extracted `0x524EEAA6` source SHA-256 still matched `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- Raw header inspection: magic `H8LR`, version `1`, header size `32`, record size `16`, table offset `32`, table bytes `16`, payload offset `48`, flags `15`, all alignment remainders `0`, decompressed payload `9890` bytes.

Compile/toolchain findings:
- No root `.csproj` files exist; only `Hecton8.slnx` remains.
- `dotnet` is not on PATH; standard SDK paths are absent.
- Visual Studio private dotnet runtime exists but has no SDK and cannot run `build`.
- MSBuild exists, but `Hecton8.slnx` references generated `.csproj` files that are missing; MSBuild fails with 71 missing project errors.
- Unity project version is `6000.4.1f1`, but no `Unity.exe` was found under checked standard install roots. Unity batchmode compile could not be run.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: binary remains 4593 bytes; added Python test file is tooling only.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: now covered by repeatable regression tests plus byte-for-byte extraction.

Failure modes:
- Runtime C# integration remains absent by scope, not by accident.
- Full Unity verification remains blocked by missing generated project files / SDK / Unity executable in this shell.
- If `Docs/Lore/` is later created, it becomes primary source and changes the record set by design.

## 2026-05-14 - Canonical Source Path Pass

STATUS: LORE BAKED FROM `Docs/Lore/`

What was wrong:
- The earlier artifact was technically valid but still depended on fallback source discovery because `Docs/Lore/` was absent.
- The batch explicitly named `Docs/Lore/`; fallback behavior was not enough after further scrutiny.

What was done:
- Moved the active lore bible to `Docs/Lore/Lore_Bible.md`.
- Replaced `Docs/Design/Lore_Bible.md` with a redirect stub to prevent silent old-path edits.
- Updated `Docs/README.md` to list the Lore bundle.
- Rebuilt `Data/Lore/Encyclopedia.h8bin` from the literal prompt directory.

Cinematic Cheats used:
- No runtime simulation touched.
- Offline pre-bake remains the chosen data path; no runtime Markdown scan or parser was introduced.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- Loader savings remain PENDING VERIFICATION.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --list` -> `LORE BAKED`, `VERIFY OK`, `0xD1880394 offset=48 length=4545`.
- `python Tools\VerifyLore.py 0xD1880394 --output-text .codex-artifacts\NARRATIVE_LORE_STREAMING_BAKER_canonical_extract.md` extracted source bytes.
- Source and extracted SHA-256 matched: `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- Raw header inspection: magic `H8LR`, version `1`, header size `32`, record size `16`, table offset `32`, table bytes `16`, payload offset `48`, flags `15`, all alignment remainders `0`, decompressed payload `9890` bytes.
- `python -m unittest Tools.test_verify_lore -v` -> 4 tests passed.
- `python Tools\test_verify_lore.py -v` -> 4 tests passed.
- `python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> pass.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: binary remains 4593 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: literal prompt source directory now exists and is the primary source.

Failure modes:
- Runtime C# integration remains absent by scope and unverified by missing toolchain.
- `Docs/Design/Lore_Bible.md` is no longer source content; it is a redirect only.

## 2026-05-15 - Manifest And Operator Extraction Pass

STATUS: LORE BAKED / MANIFEST WRITTEN / OFFLINE TESTED

What was wrong:
- Numeric-hash extraction alone was correct but hostile to handoff. Operators would need to copy `0xD1880394` manually.
- The `.h8bin` had no sidecar index recording canonical source path, hash, payload lengths, and source digest.

What was done:
- Extended `Tools/VerifyLore.py` to write `Data/Lore/Encyclopedia.manifest.json` during bake.
- Added `--source-path` extraction to resolve a repository-relative Markdown path to the same stable FNV-1a hash.
- Extended `Tools/test_verify_lore.py` to cover manifest writing and source-path hash lookup.
- Rebaked `Data/Lore/Encyclopedia.h8bin`.

Cinematic Cheats used:
- No runtime simulation touched.
- Runtime artifact remains a compact pre-baked binary table; manifest is offline metadata.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime savings claimed without a Unity loader/profiler path.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --list` -> `LORE BAKED`, `MANIFEST WRITTEN`, `VERIFY OK`.
- `python -m unittest Tools.test_verify_lore -v` -> 5 tests passed.
- `python Tools\test_verify_lore.py -v` -> 5 tests passed.
- `python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> pass.
- `python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text ...` and `python Tools\VerifyLore.py 0xD1880394 --output-text ...` produced files with the same SHA-256 as `Docs/Lore/Lore_Bible.md`: `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- Manifest entry: `0xD1880394`, canonical id `Docs/Lore/Lore_Bible.md`, offset `48`, compressed length `4545`, decompressed length `9890`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 4593 bytes; manifest is offline metadata.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: hash extraction and source-path extraction both prove the same original Markdown bytes.

Failure modes:
- Runtime C# consumption is still not implemented by the explicit XML task.
- Unity/C# compile proof remains blocked by the previously logged missing SDK/generated-project/editor executable state.

## 2026-05-15 - Manifest Integrity Verification Pass

STATUS: LORE BAKED / MANIFEST VERIFIED / TAMPER TESTED

What was wrong:
- The manifest sidecar existed, but there was no explicit verifier command to reject stale or tampered manifest data.

What was done:
- Added `--verify-manifest` to `Tools/VerifyLore.py`.
- Added a regression test that corrupts the manifest SHA-256 and requires `verify_manifest()` to reject it.
- Re-ran bake and validation commands.

Cinematic Cheats used:
- No runtime simulation touched.
- Manifest validation remains an offline packaging gate.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime savings claimed.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --verify-manifest --list` -> `LORE BAKED`, `MANIFEST WRITTEN`, `VERIFY OK`, `MANIFEST VERIFY OK`.
- `python -m unittest Tools.test_verify_lore -v` -> 6 tests passed.
- `python Tools\test_verify_lore.py -v` -> 6 tests passed.
- `python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> pass.
- `python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text ...` output SHA-256 matched source: `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- Raw header still reports `H8LR`, version `1`, header size `32`, record size `16`, table offset `32`, table bytes `16`, payload offset `48`, flags `15`, all alignment remainders `0`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 4593 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: manifest, source, and blob must now agree or verification fails.

Failure modes:
- Runtime loader remains out of explicit prompt scope.
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.

## 2026-05-15 - Blob Digest Manifest Pass

STATUS: LORE BAKED / BLOB DIGEST VERIFIED / APPEND TAMPER TESTED

What was wrong:
- Manifest integrity checked source payload metadata, but the whole `.h8bin` artifact lacked a manifest-level length/hash guard.

What was done:
- Added `blob_length` and `blob_sha256` to `Data/Lore/Encyclopedia.manifest.json`.
- Extended `--verify-manifest` to check the whole blob length and SHA-256.
- Added a regression test that appends one byte to a valid blob and requires manifest verification to reject it.
- Rebaked `Data/Lore/Encyclopedia.h8bin`.

Cinematic Cheats used:
- No runtime simulation touched.
- Whole-blob digest remains offline packaging metadata, not runtime overhead.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime savings claimed.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --verify-manifest --list` -> `LORE BAKED`, `MANIFEST WRITTEN`, `VERIFY OK`, `MANIFEST VERIFY OK`.
- `python -m unittest Tools.test_verify_lore -v` -> 7 tests passed.
- `python Tools\test_verify_lore.py -v` -> 7 tests passed.
- `python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> pass.
- `python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text ...` output SHA-256 matched source: `5D2A97CB5F45F240F1798BEE3C242E2B4369D072B0B0E5966CD30DB156E9C856`.
- Blob SHA-256: `2D7A0D3D1B2308DC1CC01FC7524CA45FD74838F2D17AA325AA37FC85D218CF03`.
- Raw header still reports `H8LR`, version `1`, header size `32`, record size `16`, table offset `32`, table bytes `16`, payload offset `48`, flags `15`, all alignment remainders `0`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 4593 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: source, manifest entries, and whole binary artifact must now agree or verification fails.

Failure modes:
- Runtime loader remains out of explicit prompt scope.
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.

## 2026-05-15 - Canonical Source Fail-Fast And Sandbox Verification Pass

STATUS: LORE BAKED / PYTHON VERIFIED / PENDING UNITY VERIFICATION

What was wrong:
- `Tools/VerifyLore.py` still had the initial `Docs/Design/Lore_Bible.md` fallback even after the canonical source was moved to `Docs/Lore/Lore_Bible.md`.
- The empty-input hash path needed explicit regression coverage against `H8DataHash.ComputeFnv1A32`, which intentionally returns `0` for null/empty input.
- The lore source changed during concurrent batch work; stale status data still reported the prior 9890-byte source and 4545-byte compressed payload.
- The current sandbox denies Python `%TEMP%` writes/cleanup and `.pyc` atomic rename, producing false failures in the regression harness and `py_compile`.

What was done:
- Removed the `Docs/Design` fallback so missing `Docs/Lore/*.md` fails instead of silently baking the redirect stub.
- Preserved the engine-compatible empty-input hash result of `0` and added a regression test for it.
- Expanded `Tools/test_verify_lore.py` from 7 to 9 tests with coverage for H8DataHash empty-input behavior and missing `Docs/Lore` fail-fast behavior.
- Moved test scratch repos to `.codex-artifacts/tmp` and reran verification with `python -B`.
- Rebuilt `Data/Lore/Encyclopedia.h8bin` and `Data/Lore/Encyclopedia.manifest.json`.

Cinematic Cheats used:
- No runtime simulation touched.
- Offline compression and fixed binary offsets only.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- Runtime loader savings remain PENDING UNITY VERIFICATION because Unity and C# build proof are still environment-blocked.

Verification:
- `python Tools\VerifyLore.py --bake --verify-source --verify-manifest --list` -> `LORE BAKED`, `VERIFY OK`, `MANIFEST VERIFY OK`, record `0xD1880394 offset=48 length=9849`.
- `python -m unittest Tools.test_verify_lore -v` -> 9 tests passed.
- `python -B -m unittest Tools.test_verify_lore -v` -> 9 tests passed without bytecode writes.
- AST syntax parse -> `AST OK`.
- Source/extract SHA-256 -> `A734EF38913EBE80474F71BBD355FFA1CB08EAD3195DA0331D4A336FEA1D1402` both sides.
- Blob SHA-256 -> `69E1631622B2FBCEAEE39A83A6F9CAEAC8715BCCE5F37C336F63F9E9D026B71B`.
- Raw header -> `H8LR`, version 1, header 32, record size 16, table offset 32, payload offset 48, file length 9897, all 16-byte alignment checks 0.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob is 9897 bytes for the current 23960-byte lore source.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: missing canonical lore sources now fail; redirect fallback is gone.

Failure modes:
- `python -m py_compile` cannot emit pycache in the current sandbox because `.pyc` atomic rename is denied.
- Runtime loader remains out of explicit prompt scope.
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.

## 2026-05-15 - In-Memory Verifier And Single Check Gate

STATUS: LORE BAKED / PYTHON VERIFIED / CHECK GATE ADDED

What was wrong:
- Regression tests still created disposable directory trees. The current sandbox can deny deletion of generated directories, so repeated tests produced noisy ignored scratch state.
- Manifest verification checked payload data, but metadata fields such as compression and record layout were not explicitly rejected on tamper.
- Operators had to combine several flags manually to prove the package was coherent.

What was done:
- Added pure byte-level helpers in `Tools/VerifyLore.py`: `parse_blob`, `verify_entries_against_blob`, `build_manifest_data`, and `verify_manifest_data`.
- Rewrote `Tools/test_verify_lore.py` to run in memory for synthetic multi-file, tamper, and manifest tests.
- Added a production-artifact test that verifies `Data/Lore/Encyclopedia.h8bin` and `Data/Lore/Encyclopedia.manifest.json` against current `Docs/Lore`.
- Added manifest metadata checks for hash algorithm, compression, alignment, and record layout.
- Added `--check` as the one-command source/manifest/blob packaging gate.

Cinematic Cheats used:
- No runtime simulation touched.
- Offline zlib compression and fixed binary offsets remain the only data-path change.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime loader performance is claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> rebaked 9897-byte blob, manifest written, `CHECK OK`, one record `0xD1880394 offset=48 length=9849`.
- `python Tools\VerifyLore.py --check` -> `CHECK OK`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 11 tests passed.
- AST syntax parse -> `AST OK`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 9897 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: tests now cover pure blob parsing, manifest metadata tamper, whole-blob tamper, payload tamper, and current artifact/source parity.

Failure modes:
- Old ignored scratch directories from earlier sandbox-denied cleanup may remain locally; the current tests no longer create new scratch trees.
- `python -m py_compile` remains blocked by pycache atomic rename denial in this sandbox.
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.

## 2026-05-15 - Malformed Blob Guard And Latest Source Rebake

STATUS: LORE BAKED / MALFORMED BLOB GUARDED / PYTHON VERIFIED

What was wrong:
- The parser rejected bad headers and out-of-range slices, but it did not explicitly reject overlapping payload intervals or truncated record tables with clear verifier errors.
- `Docs/Lore/Lore_Bible.md` changed again during continuation, making the previous blob and manifest stale.

What was done:
- Added explicit record-table bounds validation to `parse_blob`.
- Added payload interval overlap detection after record parsing.
- Added regression tests for bad magic and overlapping payload records.
- Rebaked `Data/Lore/Encyclopedia.h8bin` and rewrote `Data/Lore/Encyclopedia.manifest.json` against the current source.

Cinematic Cheats used:
- No runtime simulation touched.
- Offline zlib compression and fixed binary offsets remain the data-path implementation.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- Runtime loader cost remains PENDING UNITY VERIFICATION.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python Tools\VerifyLore.py --check` -> `CHECK OK`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 13 tests passed.
- AST syntax parse -> `AST OK`.
- Source/extract SHA-256 -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD` both sides.
- Blob SHA-256 -> `8FDBAC8752B5DB10B98226D88BC5A27EEDA049207E139E6F2F3FB15ECDBDDC00`.
- Raw header -> `H8LR`, version 1, header 32, record size 16, table offset 32, payload offset 48, file length 10329, all 16-byte alignment checks 0.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob is 10329 bytes for the current 25003-byte lore source.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: malformed binary tables now fail before extraction; current blob, manifest, and source agree.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Invalid Hash Diagnostic Guard

STATUS: LORE BAKED / BAD HASH TRACEBACK REMOVED

What was wrong:
- A malformed numeric hash exited nonzero but printed a raw Python traceback.
- Operator-facing extraction failures should be deterministic parser diagnostics, not implementation stack traces.

What was done:
- Wrapped hash conversion failures in a clear `Invalid hash value` error.
- Routed positional hash parsing through `argparse` error handling.
- Added regression coverage proving invalid hashes exit nonzero without `Traceback` in stderr.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- CLI validation only; no runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python Tools\VerifyLore.py NOT_A_HASH` -> rejected with parser error, no traceback.
- `python -B -m unittest Tools.test_verify_lore -v` -> 23 tests passed.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed.
- Source/extract SHA-256 match -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: malformed hash input now fails before blob extraction with a controlled diagnostic.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Ambiguous Extraction Rejection

STATUS: LORE BAKED / CLI SELECTOR AMBIGUITY REJECTED

What was wrong:
- The extractor accepted both a positional numeric hash and `--source-path` in the same command.
- The previous behavior silently preferred the path, which could hide a bad copied hash in operator verification.

What was done:
- Added parser-level rejection for commands that supply both selectors.
- Added regression coverage for the nonzero parser exit.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python Tools\VerifyLore.py 0xD1880394 --source-path Docs\Lore\Lore_Bible.md` -> rejected with parser error.
- `python -B -m unittest Tools.test_verify_lore -v` -> 22 tests passed.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed.
- Source/extract SHA-256 match -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: extraction selector intent is now single-source and fail-fast.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Repository-Root Path Hardening

STATUS: LORE BAKED / CWD-INDEPENDENT VERIFIER / STRICT TABLE GUARD VERIFIED

What was wrong:
- The verifier previously resolved relative paths from process cwd. Running from `Tools/` or another shell could break default source/blob paths and could change canonical source hash behavior.
- Blob and manifest writes were direct writes, which is weaker than atomic package replacement.
- Record sorting was checked, but there was no explicit regression test that corrupted table order and proved rejection.

What was done:
- Added `REPO_ROOT` anchored path resolution in `Tools/VerifyLore.py`.
- Kept CLI defaults as repository-relative labels while resolving actual IO from the repo root.
- Made `read_blob` and `verify_manifest` resolve repo-relative helper paths internally, so imported usage is not cwd-sensitive.
- Added atomic `.tmp` + replace writes for blob, manifest, and extracted Markdown output.
- Added regression tests for cwd-independent `--check` and unsorted record-table rejection.
- Updated `Data/Lore/README.md` to document cwd-independent operation and atomic writes.

Cinematic Cheats used:
- No runtime simulation touched.
- Offline deterministic binary packaging remains the delivery mechanism.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No Unity runtime loader was added, so no new frame-time cost exists from this pass.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `Push-Location Tools; python ..\Tools\VerifyLore.py --check; Pop-Location` -> `CHECK OK` from a subdirectory.
- `python -B -m unittest Tools.test_verify_lore -v` -> 17 tests passed.
- AST syntax parse -> `AST OK`.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed.
- Source/extract SHA-256 match -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.
- Blob SHA-256 remains `8FDBAC8752B5DB10B98226D88BC5A27EEDA049207E139E6F2F3FB15ECDBDDC00`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: verifier CLI and imported helpers now behave from subdirectories, preserve stable repo-relative lore IDs, write package outputs atomically, and reject unsorted record tables.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Artifact Runbook Handoff

STATUS: LORE BAKED / RUNBOOK ADDED / SOURCE SCAN CLEAN

What was wrong:
- The binary layout and required verification commands were present in task logs, but not beside the produced artifact.
- Placing operator notes under `Docs/Lore` would compile those notes into the encyclopedia blob.

What was done:
- Added `Data/Lore/README.md` with blob path, manifest path, binary layout, required verification commands, active record, extraction command, and a warning that `Docs/Lore` is source-only.

Cinematic Cheats used:
- No runtime simulation touched.
- Documentation only.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.

Verification:
- `python Tools\VerifyLore.py --hash-source` -> only `0xD1880394 Docs/Lore/Lore_Bible.md`.
- `python Tools\VerifyLore.py --check` -> `CHECK OK`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 15 tests passed.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob unchanged.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: runbook lives outside the scanned source directory.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Padding And Trailing Byte Guard

STATUS: LORE BAKED / STRICT BLOB PARSER VERIFIED

What was wrong:
- Alignment padding between payloads was generated as zero bytes, but the parser did not enforce zeroed padding.
- Standalone blob parsing allowed trailing unreferenced bytes after the last payload; manifest digest verification caught this, but `--list`/extraction should reject it too.

What was done:
- Added zero-padding validation for payload alignment gaps.
- Added trailing-byte rejection after the final payload.
- Added regression tests for nonzero padding and trailing-byte corruption.
- Hardened the padding regression so it searches deterministic payload variants for a real alignment gap instead of assuming one zlib compressed length.
- Rebaked through the stricter parser.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain the implementation.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python Tools\VerifyLore.py --check` -> `CHECK OK`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 15 tests passed.
- AST syntax parse -> `AST OK`.
- Source SHA-256 -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.
- Blob SHA-256 -> `8FDBAC8752B5DB10B98226D88BC5A27EEDA049207E139E6F2F3FB15ECDBDDC00`.
- Raw header -> `H8LR`, version 1, header 32, record size 16, table offset 32, payload offset 48, file length 10329, all 16-byte alignment checks 0.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: blob parser now rejects bad magic, overlap, nonzero padding, and trailing bytes before payload extraction.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Manifest Source Payload Guard - Bottom Append

STATUS: LORE BAKED / MANIFEST SOURCE-BYTE VERIFIED / LOG ORDER CORRECTED

What was wrong:
- `--verify-manifest` could previously validate a manifest that truthfully described a stale blob, because manifest-vs-blob metadata was checked before source-byte equality.
- Earlier patching inserted the first report for this pass above the current tail instead of appending it.

What was done:
- `build_manifest_data` now rejects blob payload bytes that do not match the active source entry.
- `verify_manifest_data` now rejects stale blob payloads even when manifest metadata and manifest SHA-256 match that stale blob.
- Added regression tests for stale payload rejection during manifest generation and manifest verification.
- Appended this report at the bottom of the log to restore chronological handoff order.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain the implementation.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python Tools\VerifyLore.py --verify-manifest --verify-source --list` -> source and manifest verification passed.
- `python Tools\VerifyLore.py --check` -> `CHECK OK`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 19 tests passed.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed.
- Source/extract SHA-256 match -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: manifest validation now proves blob payload bytes against current Markdown source, not only manifest-vs-blob metadata consistency.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Manifest Label Binding

STATUS: LORE BAKED / MANIFEST LABELS VERIFIED

What was wrong:
- Manifest payload and digest checks were strict, but the sidecar `blob` and `source_dir` labels were not bound to the paths being verified.
- A copied sidecar could pass if bytes matched while still pointing operators at the wrong package/source labels.

What was done:
- `verify_manifest` now passes expected repository-relative blob and source directory labels into `verify_manifest_data`.
- `verify_manifest_data` rejects manifest blob path mismatches and source directory mismatches when expected labels are supplied.
- Added regressions for wrong blob label and wrong source directory label.
- Appended this report at the bottom of the log after correcting the patch insertion point.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- No runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py --bake --check --list` -> `LORE BAKED`, `CHECK OK`, record `0xD1880394 offset=48 length=10281`.
- `python -B -m unittest Tools.test_verify_lore -v` -> 21 tests passed.
- `$env:PYTHONPYCACHEPREFIX='.codex-artifacts\pycache'; python -m py_compile Tools\VerifyLore.py Tools\test_verify_lore.py` -> passed.
- Source/extract SHA-256 match -> `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: manifest verification now binds content, blob label, and source directory label to the current package.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Ambiguous Extraction Input Guard

STATUS: LORE BAKED / AMBIGUOUS CLI REJECTED

What was wrong:
- The extractor accepted a positional numeric hash and `--source-path` in the same command.
- That silently preferred `--source-path`, hiding a copied or stale numeric hash.

What was done:
- Added a parser-level error when both extraction selectors are present.
- Added regression coverage for the ambiguous hash plus `--source-path` command.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- CLI validation only; no runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py 0xD1880394 --source-path Docs\Lore\Lore_Bible.md` -> rejected with parser error.
- `python -B -m unittest Tools.test_verify_lore -v` -> 22 tests passed.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: extraction commands now have exactly one selector, avoiding silent precedence.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Invalid Hash Parser Error

STATUS: LORE BAKED / INVALID HASH TRACEBACK REMOVED

What was wrong:
- Invalid positional hash input raised a raw `ValueError`.
- That could show a Python traceback instead of a clean CLI parser error.

What was done:
- `parse_hash` now wraps invalid numeric parsing with an explicit `Invalid hash value` message.
- Extraction routes that validation failure through `parser.error`.
- Added a regression proving `NOT_A_HASH` exits nonzero without printing `Traceback`.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- CLI validation only; no runtime loader cost claimed.

Verification:
- `python Tools\VerifyLore.py NOT_A_HASH` -> rejected with parser error and no traceback.
- `python -B -m unittest Tools.test_verify_lore -v` -> 23 tests passed.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: malformed hash input now fails at argument handling with deterministic diagnostics.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.

## 2026-05-15 - Hash Range Guard

STATUS: LORE BAKED / HASH RANGE STRICT

What was wrong:
- Numeric hash parsing masked negative and overflow values into uint32.
- That could turn a bad operator input into a valid but unintended lore record selector.

What was done:
- `parse_hash` now rejects values outside `0..0xFFFFFFFF`.
- Added regression coverage for `-1`, `0x100000000`, and `4294967296`.

Cinematic Cheats used:
- No runtime simulation touched.
- Fixed binary layout and zlib payloads remain unchanged.

Exact microseconds saved:
- Current runtime frame impact remains 0 us/frame.
- CLI validation only; no runtime loader cost claimed.

Verification:
- `python -B -m unittest Tools.test_verify_lore -v` -> 24 tests passed.

Regression model:
- CPU: no runtime code touched.
- GC: no runtime allocation path touched.
- Memory: runtime blob remains 10329 bytes.
- Cadence: no Tick/Update/FixedUpdate path touched.
- Correctness: invalid numeric hash selectors cannot wrap into valid IDs.

Failure modes:
- Unity/C# compile proof remains blocked by missing local toolchain/generation state.
- Runtime loader remains out of explicit prompt scope.
