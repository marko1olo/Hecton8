# Rationale - NARRATIVE_LORE_STREAMING_BAKER

## Decision 001 - Use Prompt ID For This Run's State

Problem: The user supplied role `BACKEND_ENGINEER` and prompt ID `NARRATIVE_LORE_STREAMING_BAKER`; `Docs/Tasks/Status_BACKEND_ENGINEER.md` already contains stale work for `ITEM_RECIPE_GRAPH_AUDITOR`.
Solution: Use the extracted XML prompt ID for state, rationale, and final log files so this lore bake does not overwrite unrelated economy audit state.
Rejected Alternatives: Reusing `Status_BACKEND_ENGINEER.md` would mix two separate prompts and violate batch hygiene.
Scalability potential: Low/Middle/High/Ultra unaffected; this is offline agent-state isolation.
Hardware Impact: 0 us/frame on i3/MX350 because no runtime code is touched.

## Decision 002 - Treat `Docs/Design/Lore_Bible.md` As The Active Source Fallback

Problem: The prompt requires scanning `Docs/Lore/*.md`, but `Docs/Lore/` is absent in the current workspace. The user asked to compile the Markdown lore bible, and `Docs/Design/Lore_Bible.md` is the only active non-archive Markdown lore bible found.
Solution: Bake `Docs/Design/Lore_Bible.md` when `Docs/Lore/` has no `.md` files, while recording the missing prompt path in status and final log.
Rejected Alternatives: Baking archived lore audit files would pull stale evidence snapshots into runtime data; creating an empty blob would satisfy directory literalism but fail the user's "lore bible" request.
Scalability potential: Low tier streams one compressed lore document; Middle/High/Ultra can add more Markdown shards under `Docs/Lore/` later without changing the record format.
Hardware Impact: Expected load-time and disk impact are negligible for one compressed Markdown payload; runtime impact remains 0 us/frame until a loader is introduced.

## Decision 003 - Single Blob With Fixed Header And Sorted Records

Problem: Existing `LoreMmfEncyclopedia` reads separate index/payload files with UTF-16 payloads, but the prompt requires one `Data/Lore/Encyclopedia.h8bin` with zlib-compressed content and `uint hash -> long offset, int length` records.
Solution: Define a compact little-endian H8 lore blob: 32-byte fixed header, 16-byte sorted records, 16-byte aligned compressed payload offsets, and zlib payloads.
Rejected Alternatives: Reusing the existing MMF layout would violate the zlib and single `.h8bin` output requirements; adding runtime C# loader code would expand scope beyond the prompt.
Scalability potential: Low uses sparse direct binary search over sorted records; High/Ultra can memory-map or prefetch records without changing authored hashes.
Hardware Impact: Header scan is O(log n) on fixed records and saves disk bytes through zlib; estimated runtime savings are PENDING VERIFICATION because no Unity loader was implemented in this task.

## Decision 004 - Keep Runtime C# Untouched

Problem: The prompt asks for a compiled binary blob and verifier, not a new Unity loader. Existing lore runtime code uses a separate MMF index/payload format and would require public integration decisions to consume this new `.h8bin`.
Solution: Add an offline Python verifier/baker and generate the requested data artifact without changing runtime C# APIs.
Rejected Alternatives: Modifying `LoreMmfEncyclopedia` now would create a cross-domain runtime contract not requested by the batch prompt; adding duplicate runtime loaders would increase maintenance surface before integration authority exists.
Scalability potential: Low can ship the tiny compressed blob; Middle/High/Ultra can later map the same fixed records into a streaming reader or prefetch cache without rebaking content.
Hardware Impact: 0 us/frame on i3/MX350 in this task; future loader work can target O(log n) record lookup plus zlib inflate only on user-requested lore pages.

## Decision 005 - Classify Dotnet Build As Environment-Blocked

Problem: The workflow requires compile verification, but this shell cannot find `dotnet`, and standard Windows dotnet install paths are absent.
Solution: Run Python bytecode compilation and blob self-verification, then record the C# build guard as environment-blocked instead of claiming compile proof.
Rejected Alternatives: Reporting a stale prior compile would violate evidence rules; attempting to modify PATH without an SDK path would be fake progress.
Scalability potential: Low/Middle/High/Ultra unaffected because runtime C# was not changed.
Hardware Impact: 0 us/frame on i3/MX350; this is local toolchain availability, not gameplay behavior.

## Decision 006 - Stabilize Source Path Hashing During Polish

Problem: The first verifier implementation hashed `Path.as_posix()` directly. If called with an absolute source path, the same lore file would get a different hash.
Solution: Resolve source paths relative to the current repository when possible and reject non-ASCII source paths under the existing `H8DataHash` ASCII ID contract.
Rejected Alternatives: Hashing file contents would break the prompt's table-key intent and make content edits invalidate external references; storing source names in the blob would expand the required record contract.
Scalability potential: Low/Middle/High/Ultra can rebake from relative or absolute invocations without hash drift.
Hardware Impact: 0 us/frame on i3/MX350; this is offline bake determinism.

## Decision 007 - Reject Out-Of-Repo Lore Sources

Problem: A CLI stress check with an absolute temporary `--source-dir` produced hashes based on absolute temp paths, which would be nondeterministic across machines.
Solution: Treat repository-relative source identity as mandatory and reject sources outside the current repository root.
Rejected Alternatives: Allowing absolute paths would poison stable hash IDs; hashing only file names would create collisions for same-name files in different lore subfolders.
Scalability potential: Low/Middle/High/Ultra all get stable hashes independent of machine install paths.
Hardware Impact: 0 us/frame on i3/MX350; offline validation prevents bad content IDs before runtime.

## Decision 008 - Use Regression Tests Instead Of Runtime Loader Scope Creep

Problem: The user demanded certainty after the initial bake, but C# runtime integration and Unity compile proof are blocked by absent generated projects/toolchain.
Solution: Add Python regression coverage for the actual deliverable: multi-file bake/extract/verify, sorted records, alignment, absolute-vs-relative in-repo hash stability, missing hash lookup, and out-of-repo source rejection.
Rejected Alternatives: Adding a C# loader without compile proof would increase unverified runtime surface; creating Unity editor tooling would exceed the prompt and still fail without Unity batchmode proof.
Scalability potential: Low gets deterministic compressed pages; Middle/High/Ultra can expand source count without format changes and without path-dependent hash drift.
Hardware Impact: 0 us/frame on i3/MX350 in this task; tests are offline only.

## Decision 009 - Promote Lore Bible To `Docs/Lore`

Problem: The first implementation baked the active lore bible from `Docs/Design/Lore_Bible.md` because `Docs/Lore/` did not exist, but the batch directive explicitly names `Docs/Lore/`.
Solution: Move the active lore bible to `Docs/Lore/Lore_Bible.md`, leave a small redirect at `Docs/Design/Lore_Bible.md`, add the Lore bundle to `Docs/README.md`, and rebake from the literal prompt source directory.
Rejected Alternatives: Continuing to rely on fallback logic would leave a task-path deviation; duplicating the full lore bible in both folders would create two editable sources and future drift.
Scalability potential: Low/Middle/High/Ultra now share one canonical source path and stable hash namespace for future lore shards under `Docs/Lore/`.
Hardware Impact: 0 us/frame on i3/MX350; this is source governance and offline bake determinism.

## Decision 010 - Add Manifest Sidecar And Source-Path Extraction

Problem: A numeric-only extractor works, but it forces operators to copy hashes from console output and leaves no stable sidecar index for auditing the blob after handoff.
Solution: Write `Data/Lore/Encyclopedia.manifest.json` on bake and allow extraction by `--source-path`, while keeping the binary table contract unchanged.
Rejected Alternatives: Embedding variable-length path strings inside the `.h8bin` would bloat the runtime artifact and violate the simple `uint hash -> offset,length` table mandate.
Scalability potential: Low reads the same compact blob; Middle/High/Ultra tooling can prefetch or diff manifests without touching runtime data.
Hardware Impact: 0 us/frame on i3/MX350; manifest is offline metadata and not a runtime requirement.

## Decision 011 - Verify Manifest Integrity Explicitly

Problem: Writing a manifest is insufficient if later edits can stale it without a failing verification command.
Solution: Add `--verify-manifest` and a regression test that corrupts a manifest SHA-256 and expects rejection.
Rejected Alternatives: Trusting manifest JSON after bake would create a false audit trail; embedding the manifest in the binary would bloat the runtime payload.
Scalability potential: Low/Middle/High/Ultra can validate large lore sets offline before packaging.
Hardware Impact: 0 us/frame on i3/MX350; verification is offline only.

## Decision 012 - Record Blob Digest In Manifest

Problem: Entry-level manifest checks prove payload content, but a stale or appended `.h8bin` can still parse unless the whole blob identity is checked.
Solution: Add `blob_length` and `blob_sha256` to the manifest, verify both, and add a regression test that appends one byte to the blob and expects rejection.
Rejected Alternatives: Ignoring trailing bytes would allow packaging drift; adding checksum fields into the binary header would mutate the already verified runtime table contract.
Scalability potential: Low/Middle/High/Ultra can validate the whole artifact before packaging without increasing runtime payload size.
Hardware Impact: 0 us/frame on i3/MX350; whole-blob digest validation is an offline packaging guard.

## Decision 013 - Remove Obsolete Design Fallback

Problem: After promoting the lore bible into `Docs/Lore/Lore_Bible.md`, the verifier still contained the earlier fallback to `Docs/Design/Lore_Bible.md`. That could silently bake the redirect stub if `Docs/Lore` disappeared.
Solution: Remove the fallback path and add a regression test proving that a missing `Docs/Lore` source fails instead of baking `Docs/Design`.
Rejected Alternatives: Keeping fallback compatibility would preserve a known prompt-path deviation; duplicating lore in both folders would create source drift.
Scalability potential: Low/Middle/High/Ultra all use the same canonical lore source namespace with no hidden alternate source tree.
Hardware Impact: 0 us/frame on i3/MX350; this is offline source validation.

## Decision 014 - Sandbox-Safe Regression Scratch Roots

Problem: The current workspace sandbox denies Python `TemporaryDirectory` cleanup and default `%TEMP%` writes, causing false test failures unrelated to the lore compiler.
Solution: Allocate regression scratch repositories under `.codex-artifacts/tmp` with direct directory creation and no cleanup dependency; rerun tests with `python -B` to avoid bytecode writes.
Rejected Alternatives: Requesting broader filesystem approval for unit tests would make local proof less reproducible; leaving tests tied to `%TEMP%` would fail in the current execution mode.
Scalability potential: Low/Middle/High/Ultra unaffected; this only makes offline verification portable in restricted shells.
Hardware Impact: 0 us/frame on i3/MX350; test harness only.

## Decision 015 - Use AST Parse When Pycache Rename Is Blocked

Problem: `python -m py_compile` now fails on `[WinError 5]` during `.pyc` atomic rename, while the modules import and the regression suite runs successfully.
Solution: Use `ast.parse` for syntax-only proof and `python -B -m unittest` for behavioral proof without bytecode emission.
Rejected Alternatives: Treating pycache rename denial as a code failure would be inaccurate; forcing cleanup or ACL changes is outside the batch task.
Scalability potential: Low/Middle/High/Ultra unaffected; no runtime code changed.
Hardware Impact: 0 us/frame on i3/MX350; this is a local verification path adjustment.

## Decision 016 - Preserve Engine Hash Empty-Input Contract

Problem: A hardening pass briefly treated empty string FNV-1a as the pure FNV offset basis, but `Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs` explicitly returns `0` for empty input.
Solution: Keep `Tools/VerifyLore.py` aligned to `H8DataHash.ComputeFnv1A32` and add a regression test proving empty input returns `0`.
Rejected Alternatives: Using pure FNV behavior would diverge from the engine data contract; changing C# hash behavior is outside this prompt and would break existing data assumptions.
Scalability potential: Low/Middle/High/Ultra all preserve one stable hash namespace across offline bake and future runtime lookup.
Hardware Impact: 0 us/frame on i3/MX350; this prevents data identity drift.

## Decision 017 - Rebake After Concurrent Lore Source Change

Problem: `Docs/Lore/Lore_Bible.md` changed during concurrent batch work, invalidating the previous source SHA-256, compressed length, blob length, and manifest digest.
Solution: Rebake `Data/Lore/Encyclopedia.h8bin`, rewrite the manifest, re-extract by source path, and update status/logs to the current source SHA-256 `A734EF38913EBE80474F71BBD355FFA1CB08EAD3195DA0331D4A336FEA1D1402`.
Rejected Alternatives: Keeping the older 4593-byte blob would ship stale lore; manually editing manifest numbers would be fake verification.
Scalability potential: Low streams one compact blob; Middle/High/Ultra can add more lore shards under the same deterministic table format.
Hardware Impact: 0 us/frame in this offline task; blob size is now 9897 bytes, still negligible for MX350 storage and memory budgets.

## Decision 018 - Split Byte-Level Verification Helpers

Problem: Regression tests created disposable repository trees, but the current sandbox can create some directories it later cannot delete, leaving ignored scratch debris and making repeated proof noisy.
Solution: Add pure helpers for blob parsing, entry-vs-blob verification, manifest construction, and manifest data verification; rewrite tests to exercise those paths in memory and read the current production artifact without creating new scratch trees.
Rejected Alternatives: Continuing to create temp directories would keep producing local cleanup failures; broad filesystem permission changes are outside this prompt.
Scalability potential: Low/Middle/High/Ultra all get stronger offline verification without changing runtime data layout.
Hardware Impact: 0 us/frame on i3/MX350; this only hardens offline tooling.

## Decision 019 - Add Single Packaging Check Command

Problem: Operators had to remember multiple verifier flags to prove the source, manifest, and blob agree.
Solution: Add `--check`, which runs source verification and manifest verification against the current blob in one command.
Rejected Alternatives: Relying on manual multi-flag command sequencing increases handoff error; embedding more metadata into `.h8bin` would bloat the runtime artifact.
Scalability potential: Low/Middle/High/Ultra packaging can gate lore blobs with one deterministic command before handoff.
Hardware Impact: 0 us/frame on i3/MX350; this is an offline packaging guard.

## Decision 020 - Reject Malformed Blob Payload Intervals

Problem: The blob parser validated individual payload bounds but did not explicitly reject overlapping payload intervals or truncated record tables.
Solution: Add record-table bounds validation and a sorted interval pass that rejects any payload overlap. Add regression coverage for bad magic and overlapping record offsets.
Rejected Alternatives: Letting zlib decompression fail later would produce weaker diagnostics and could hide malformed table data behind a decompressor error.
Scalability potential: Low/Middle/High/Ultra loaders get a stricter offline packaging gate without changing runtime layout.
Hardware Impact: 0 us/frame on i3/MX350; this validation is offline tooling only.

## Decision 021 - Rebake After Second Concurrent Lore Source Change

Problem: `Docs/Lore/Lore_Bible.md` changed again during continuation, changing the source digest, decompressed length, compressed payload length, and blob digest.
Solution: Rebake through `python Tools\VerifyLore.py --bake --check --list`, re-extract by source path, and update persistent status/logs to source SHA-256 `6B529A808B25D18DA276747DB9149C61BACDF90A33DCC667FC85375DE13E69CD`.
Rejected Alternatives: Keeping prior `9897`-byte blob data would leave the runtime artifact stale against the current lore source.
Scalability potential: Low/Middle/High/Ultra continue using one stable record-table format while content changes safely rebake.
Hardware Impact: 0 us/frame in this offline task; current blob is 10329 bytes, still negligible for target storage and memory budgets.

## Decision 022 - Reject Nonzero Padding And Trailing Bytes

Problem: Alignment gaps between payloads were expected to be zero padding, but the parser did not enforce that. It also allowed unreferenced trailing bytes if the manifest check was skipped.
Solution: Add explicit zero-padding validation between payload intervals and reject any byte after the final payload. Add regression coverage for both cases.
Rejected Alternatives: Allowing unreferenced bytes would make `--list` and extraction less strict than `--check`; relying only on the manifest digest would leave standalone blob parsing weaker.
Scalability potential: Low/Middle/High/Ultra get deterministic binary packaging with no hidden payload bytes.
Hardware Impact: 0 us/frame on i3/MX350; parser hardening is offline tooling only.

## Decision 023 - Make Padding Test Independent Of One Zlib Length

Problem: The nonzero-padding regression originally assumed the first small compressed payload would leave an alignment gap before the second payload.
Solution: Search up to 32 deterministic payload variants until a real alignment gap exists, then corrupt that gap.
Rejected Alternatives: Depending on a single zlib output length would make the test fragile across Python/zlib builds.
Scalability potential: Low/Middle/High/Ultra unaffected; this keeps the offline guard stable across machines.
Hardware Impact: 0 us/frame on i3/MX350; test harness only.

## Decision 024 - Put Operator Runbook Outside Lore Source

Problem: The binary layout and verification commands were recorded in status/log files, but handoff operators need a local runbook beside the artifact. Placing that runbook under `Docs/Lore` would make it source content and pollute the blob.
Solution: Add `Data/Lore/README.md` with the binary layout, required commands, active record, and warning not to place notes under `Docs/Lore`.
Rejected Alternatives: Putting the runbook in `Docs/Lore` would be compiled into the encyclopedia; relying on chat or status logs alone is weak handoff.
Scalability potential: Low/Middle/High/Ultra packaging can verify and extract lore data without searching agent logs.
Hardware Impact: 0 us/frame on i3/MX350; documentation only.

## Decision 025 - Anchor Verifier Paths To Repository Root

Problem: The verifier used process cwd for relative path identity, so launching `Tools/VerifyLore.py` from `Tools/` or another operator shell could break default paths or alter source-path hashing. Direct helper calls still needed the same guarantee after the CLI path was fixed.
Solution: Define the repository root from `Tools/VerifyLore.py`, resolve default blob/manifest/source paths from that root, make `read_blob` and `verify_manifest` resolve repo-relative paths internally, keep manifest labels repository-relative, add `.tmp` + atomic replace writes for generated files, and add regression coverage for cwd-independent `--check`, helper usage, and unsorted record-table rejection.
Rejected Alternatives: Telling operators to always run from repo root is a process dependency, not a compiler guarantee; hashing cwd-relative paths would corrupt stable lore IDs; direct writes risk partial blobs if interrupted.
Scalability potential: Low uses deterministic one-file packaging from any shell; Middle/High/Ultra can add more Markdown shards without path drift, and atomic replacement prevents stale half-written package data.
Hardware Impact: 0 us/frame on i3/MX350; this is offline tooling. Low-tier runtime still reads the same 10329-byte blob, while high-tier future loaders can rely on stable IDs and strict binary ordering.

## Decision 026 - Redirect Python Bytecode Cache For Compile Proof

Problem: Default `python -m py_compile` previously failed because workspace pycache atomic rename returned `[WinError 5] Access denied`, even though AST parsing and unit tests passed.
Solution: Use `PYTHONPYCACHEPREFIX=.codex-artifacts\pycache` for compile proof so bytecode emission goes into the ignored artifact cache rather than tool directories with unreliable rename permissions.
Rejected Alternatives: Dropping py_compile proof would leave syntax proof dependent only on AST parsing; changing filesystem ACLs is outside this batch and would mutate the developer machine.
Scalability potential: Low/Middle/High/Ultra unaffected; this is local verification hygiene for the offline compiler.
Hardware Impact: 0 us/frame on i3/MX350; no runtime code or data layout changed.

## Decision 027 - Make Manifest Verification Prove Current Source Bytes

Problem: `--check` compared source payloads against the blob, but `--verify-manifest` could still pass if a manifest truthfully described a stale blob whose payload no longer matched the current Markdown source.
Solution: Make `build_manifest_data` and `verify_manifest_data` reject payload mismatches against the active `SourceEntry` bytes, then add regressions where a stale blob and matching manifest are rejected against current source.
Rejected Alternatives: Requiring operators to always remember `--check` would keep `--verify-manifest` weaker than its help text; trusting only SHA-256 values stored in the manifest would let stale packages self-certify.
Scalability potential: Low/Middle/High/Ultra all get stricter offline package gates as lore shards scale; stale shard detection remains content-byte exact before any future runtime loader sees the blob.
Hardware Impact: 0 us/frame on i3/MX350; this is offline validation. Runtime artifact size and layout remain unchanged.

## Decision 028 - Bind Manifest Labels To Verification Paths

Problem: Manifest payload and digest checks were strict, but `blob` and `source_dir` labels were still advisory. A sidecar copied from another package path could pass if the bytes matched, weakening operator auditability.
Solution: Pass expected repository-relative blob and source directory labels from `verify_manifest` into `verify_manifest_data`, reject label mismatches, and add regressions for wrong blob and wrong source directory labels.
Rejected Alternatives: Ignoring labels would make the manifest less useful as a handoff contract; embedding path labels in the binary would bloat the runtime artifact and exceed the prompt's fixed record table.
Scalability potential: Low/Middle/High/Ultra packaging keeps sidecars tied to exact source roots as future lore shards scale across subdirectories.
Hardware Impact: 0 us/frame on i3/MX350; this is offline verification. Runtime blob size and layout remain unchanged.

## Decision 029 - Reject Ambiguous Extraction Inputs

Problem: The CLI accepted both a positional numeric hash and `--source-path` in one extraction command, then silently preferred the source path. That creates operator ambiguity and can hide a bad copied hash.
Solution: Add a parser-level error when both selectors are present and add a regression test that expects nonzero `SystemExit`.
Rejected Alternatives: Keeping path precedence would make extraction less auditable; choosing numeric hash precedence would surprise users who supplied `--source-path`; allowing both only when they match would add unnecessary blob reads before argument validation.
Scalability potential: Low/Middle/High/Ultra packaging keeps extraction commands unambiguous as the lore table grows beyond one record.
Hardware Impact: 0 us/frame on i3/MX350; CLI validation only, runtime blob unchanged.
