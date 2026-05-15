# Status_SAVE_HASH_CRYPTOGRAPHER

Agent: SAVE_HASH_CRYPTOGRAPHER
Role: CORE_ENGINEER
Domain: Auxiliary Node (Math/Binary)
Task Count: 6
Status: INTEGRITY SECURED / PYTHON_REFERENCE_FUZZ_VERIFIED / PATH_PINNED_REFERENCE_VERIFIED / PENDING UNITY VERIFICATION

## Prompt Source

Extracted from `Docs/Tasks/CURRENT_BATCH.md` because `CURRENT_BATCH_OSHINO.md` is absent in `C:\Hecton8`.

Latest re-extraction note: `Docs/Tasks/CURRENT_BATCH.md` has since been overwritten by another batch and no longer contains `SAVE_HASH_CRYPTOGRAPHER`. The original extracted directive is preserved below to prevent anti-amnesia loss.

```xml
<AGENT_PROMPT id="SAVE_HASH_CRYPTOGRAPHER" role="CORE_ENGINEER" chat_name="XXHash3 & Bit-Shuffle Design">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the Cryptographer. Target: Auxiliary Node (Math/Binary).
CRITICAL: Save file integrity is vital. We need a bit-shuffling algorithm that makes the `.h8db` and `.sav` files tamper-resistant and cross-platform stable.

[III. PRIMARY OBJECTIVES: 15 TITANIUM TASKS]
-- PHASE 1: XXHASH3 IMPLEMENTATION --
1. PYTHON PROTOTYPE: Write `Tools/Security/ReplayHasher.py`. Implement a bit-perfect XXHash3 (64-bit).
2. BIT-SHUFFLE RULE: Design a 128-bit XOR-mask based on the `WorldSeed` and `AUP.SectorHash`.
3. CROSS-PLATFORM PROOF: Ensure your math yields the same bytes on Python (OSHINO) as it will in Burst C# (SHINOBU).

-- PHASE 2: DATA ALIGNMENT --
4. HEADER SPEC: Write `Docs/Design/Save_Binary_Header.md`. Define the exact byte-offset for the `MasterStateHash`.
5. PADDING MANDATE: Define how DTOs must be padded to avoid IL2CPP alignment crashes.

-- PHASE 3: VERIFICATION --
6. RECURSIVE CHECK: Audit `SaveData.cs` structs. Find the "ARM-Killers" (unaligned types) and flag them for the PHI_VOD surgeon.
STATUS: MUST BE "INTEGRITY SECURED".
</AGENT_PROMPT>
```

## Mandates Loaded

- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `QA_Evidence_Text_Filter_Audit.txt`

## Checklist

- [x] Task 1 - Python prototype `Tools/Security/ReplayHasher.py` | DOD: scalar integer-only XXH3-64 oracle, no dependency on Python `xxhash`; rejected dependency wrapper because replay proof must survive clean machines. Static estimate: saves native plugin/load drift, 5-20 us per offline check setup.
- [x] Task 2 - 128-bit bit-shuffle rule | DOD: domain-separated 128-bit mask from `WorldSeed` and `AUP.SectorHash`, low/high lane contract documented; rejected per-byte permutation because it is slower and easier to mis-port. Static estimate: rotate+xor path is sub-0.1 us in Burst.
- [x] Task 3 - Cross-platform proof contract | DOD: explicit little-endian lane rules plus Python self-test; rejected platform-native struct dumps. Static estimate: prevents endian/IL2CPP mismatch, not a frame-time optimization.
- [x] Task 4 - Header spec `Docs/Design/Save_Binary_Header.md` | DOD: current v9 header offsets recorded and v10 `MasterStateHash` offset fixed at byte 56. Rejected inserting fields before byte 56 because that breaks current readers. Static estimate: no runtime cost.
- [x] Task 5 - Padding mandate | DOD: DTO padding/field restrictions documented for PHI_VOD; rejected raw managed DTO blits. Static estimate: prevents ARM unaligned trap risk; no measured microsecond claim.
- [x] Task 6 - Recursive `SaveData.cs` audit | DOD: flagged managed/string/bool DTOs and confirmed no obvious current `[BinaryBlittableSafe]` 8-byte misalignment by static read. Rejected changing public DTO order in active batch. Static estimate: avoids future crash class, not a runtime saving.

## Iterative Loops

- Loop 1: Prompt/mandate extraction; established domain and missing `CURRENT_BATCH_OSHINO.md` fallback.
- Loop 2: Save header/source scan; found current `SaveFileHeader` is 56 bytes while older doc said 52.
- Loop 3: XXH3 oracle implementation; pinned official scalar constants and default secret.
- Loop 4: Header/bit-shuffle document pass; fixed `MasterStateHash` byte offset and endian rules.
- Loop 5: DTO audit/report pass; flagged ARM-killer managed DTOs and evidence boundary.
- Loop 6: Local anti-bloat/polish pass; no `<POLISH_MANDATE>` tag exists in `Docs/Tasks/CURRENT_BATCH.md`, so fixed XXH3 branch vectors and shuffle vectors were embedded into `ReplayHasher.py` self-test.
- Loop 7: Professional self-review pass; added executable `master` command, corrected `MasterStateHash` preimage to bind the full current header prefix/body except circular result fields, and reran external XXH3/fuzz verification.
- Loop 8: Self-review correction pass; caught stale placeholder master vector in `ReplayHasher.py` self-test, patched it to the CLI-emitted deterministic lanes, reran syntax/self-test/master/reference fuzz, and probed local compiler/editor availability.
- Loop 9: Implementation upgrade pass; added isolated C# `SaveMasterHashV10` helper, added `SaveMasterHashV10Result` binary layout sentinel, validated 128-bit rotate formulas externally, and kept the active v9 writer untouched.
- Loop 10: Header implementation upgrade pass; added concrete `SaveFileHeaderV10`, helper overloads for compute/fill/validate, full header offset sentinels, and independent packed-layout validation.
- Loop 11: Static C# helper guard pass; added `Tools/Security/ValidateSaveMasterHashCSharp.py` to compare C# domain bytes/constants/layout sentinels against `ReplayHasher.py`. DOD: `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=8`; rejected relying on prose-only parity while Unity compile is unavailable.
- Loop 12: Static guard self-review pass; first execution exposed a parser defect: method extraction matched a call-site and suffix writers were forced to start at index 0. DOD: declaration-only extraction plus explicit `expected_start` validation, then `SAVE_MASTER_HASH_CSHARP_GUARD=PASS`; rejected weakening the domain-byte check.
- Loop 13: Guard expansion and compiler probe pass; expanded `ValidateSaveMasterHashCSharp.py` to verify all V10 header manifest sentinels and `SaveFileHeaderV10` field order, then ran Roslyn `csc.exe` directly for parser-level C# validation.
- Loop 14: Evidence self-review pass; corrected `Rationale_SAVE_HASH_CRYPTOGRAPHER.md` decision ordering and reran Python syntax, Python XXH3 self-test, and C# helper static guard.
- Loop 15: C# parity guard hardening pass; added exact `SaveFileHeaderV10` forwarding validation, circular-field exclusion checks, stored-lane comparison checks, Unity.Mathematics `uint2` lane assembly check, and Python master preimage length check.
- Loop 16: Preimage writer sequence hardening pass; added static extraction of the C# `BuildMasterPreimage` write order and verified it matches the canonical Python/header preimage fields.
- Loop 17: Anti-amnesia recovery pass; current `CURRENT_BATCH.md` no longer contains `SAVE_HASH_CRYPTOGRAPHER`, so the original extracted XML directive was embedded into this status file.
- Loop 18: Cursor offset hardening pass; added static extraction of `BuildMasterPreimage` cursor advances and proved the C# writer ends at byte `80`.
- Loop 19: Shuffle mask byte-order hardening pass; added static extraction of `DeriveShuffleMask` operations and proved low/high mask preimage byte ends `36/44`.
- Loop 20: Rotate edge-vector hardening pass; embedded 128-bit rotate vectors for shifts `0/1/63/64/65/127` and inverse checks into `ReplayHasher.py self-test`.
- Loop 21: C# rotate branch hardening pass; added static checks for `Rotl128` and `Rotr128` shift `0/64/<64/>64` formulas.
- Loop 22: External compile boundary recheck; confirmed `dotnet` and Unity are unavailable, MSBuild exists, and `Hecton8.slnx` fails before source compilation because Unity-generated `.csproj` files are missing.
- Loop 23: Little-endian primitive writer guard pass; added exact static body checks for C# `WriteU16`, `WriteU32`, and `WriteU64` so byte-order drift fails offline.
- Loop 24: Signed lane packing hardening pass; added Python oracle self-test vectors for two's-complement little-endian `long` lanes and 128-bit lane hex roundtrip parsing.
- Loop 25: Hash64 convention guard pass; added static validation that `SaveMasterHashV10.Hash64` and existing `SaveBinaryStorage.Hash64` both assemble the full `uint2` lane instead of collapsing to low 32 bits.
- Loop 26: Stackalloc buffer contract guard pass; replaced weak stackalloc count check with exact declarations for the master preimage and shuffle mask buffers.
- Loop 27: Master hash edge-fixture hardening pass; replaced the single master hash self-test with four frozen fixtures covering mixed signed sector, zero metadata, max signed edges, and opposite signed lanes.
- Loop 28: Internal API boundary guard pass; added static validation that V10 result/header/helper declarations remain `internal` until Unity import/load verification can approve active API expansion.
- Loop 29: Active writer isolation guard pass; added static validation that `SaveBinaryStorage` remains v9/56-byte header and does not reference V10 helper/header before verified integration.
- Loop 30: Reusable external-reference verifier pass; added `Tools/Security/VerifyReplayHasherReference.py`, compared `ReplayHasher.py` against optional `xxhash` 3.7.0 in `.codex_tmp`, then removed the temp package.
- Loop 31: Reference verifier discoverability pass; added the optional `xxhash` install and verifier commands to `Docs/Design/Save_Binary_Header.md`.
- Loop 32: Result constructor lane-order guard pass; added static validation that `SaveMasterHashV10Result` constructor assigns plain/stored lanes in canonical order and that `Compute` constructs it with `(plainLo, plainHi, storedLo, storedHi)`.
- Loop 33: Blit-safe attribute guard pass; added static validation that both V10 binary structs keep `[BinaryBlittableSafe]` paired with their packed `StructLayout`.
- Loop 34: Master preimage byte fixture pass; added exact 80-byte mixed-case `MasterStateHash` preimage hex to `ReplayHasher.py self-test` for direct byte-order diagnostics.
- Loop 35: Shuffle mask preimage byte fixture pass; added exact low/high shuffle-mask preimage hex checks to `ReplayHasher.py self-test`.
- Loop 36: Reference verifier path pinning pass; made `--xxhash-path` mandatory, added a module-file containment check so globally installed `xxhash` cannot satisfy the proof, documented the contract, and reran isolated reference fuzz.
- Loop 37: Reference verifier malformed-module guard pass; made the containment check fail cleanly when a contaminated `xxhash` module has no `__file__`.
- Loop 38: Reference verifier cached-module eviction pass; evicted `sys.modules["xxhash"]` before import so embedded-process calls cannot reuse a preloaded host module.
- Loop 39: Reference verifier embedded cleanup pass; restored host `sys.path` and previous `sys.modules["xxhash"]` after success and failure paths.
- Loop 40: Evidence consistency pass; corrected stale status text from Decisions `1-37` to `1-41` and aligned the latest-suite evidence list with the current cleanup-guard run.
- Loop 41: Reference verifier temp-helper cleanup pass; removed newly loaded helper modules whose `__file__` resolves under `--xxhash-path` after embedded verifier execution.
- Loop 42: Reference verifier API-shape guard pass; rejected temp-path `xxhash` modules that do not expose callable `xxh3_64_intdigest`.
- Loop 43: Reference verifier digest-shape guard pass; rejected non-integer and out-of-range `xxh3_64_intdigest` results before formatting/comparison.
- Loop 44: Replay digest-shape guard pass; rejected non-integer and out-of-range `ReplayHasher.py` digest results before formatting/comparison.
- Loop 45: Reference verifier mismatch failure pass; converted vector mismatches from uncaught `AssertionError` tracebacks into controlled CLI exit code `1` with the exact mismatch message.
- Loop 46: Shuffle pair shape guard pass; rejected malformed `shuffle_hash128` and `unshuffle_hash128` lane pairs before indexing or inverse comparison.
- Loop 47: Exact-int verifier guard pass; rejected Python `bool` values for reference digests, replay digests, shuffle lanes, and unshuffle lanes.

## Verification

- Python syntax: `python -m compileall .\Tools\Security\ReplayHasher.py` -> PASS.
- Python self-test: `python .\Tools\Security\ReplayHasher.py self-test` -> PASS (`SELFTEST_OK`), including fixed zero-seed branch vectors, seeded branch vectors, signed `long` lane byte vectors, 128-bit lane hex roundtrip, exact 80-byte master preimage hex, exact low/high shuffle-mask preimage hex, shuffle mask vector, shuffle output vector, shuffle inverse validation, 128-bit rotate edge vectors, rotate inverse validation, and four master hash vectors.
- Master CLI vector: `python .\Tools\Security\ReplayHasher.py master ...` -> PASS, expected `stored_le=6d24c9a87e8ec3322681980ad2b6b28c`.
- Reference comparison: `python -B .\Tools\Security\VerifyReplayHasherReference.py --xxhash-path .\.codex_tmp\xxhash_check --fuzz-count 128` -> PASS (`XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`).
- Reference path guard: verifier execution without `--xxhash-path` exits with argparse code `2` and reports the required argument; globally installed `xxhash` can no longer satisfy the proof path, preloaded `sys.modules["xxhash"]` is evicted before import, a module without `__file__` is rejected with a controlled error, a module without callable `xxh3_64_intdigest` is rejected, non-integer/out-of-range reference and replay digests are rejected, Python `bool` is rejected for digests and 128-bit lanes, malformed shuffle/unshuffle lane pairs are rejected before indexing, digest mismatches return controlled exit code `1`, newly loaded helper modules from `--xxhash-path` are removed, and embedded calls restore host Python import state afterward.
- C# implementation surface: `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs` added. Static review: stackalloc-only preimage/mask buffers, manual little-endian writes, no managed arrays, no `BitConverter`, no strings in hash generation.
- Concrete V10 header: `SaveFileHeaderV10` added with size `72`; `MasterStateHashLo/Hi` offsets `56/64`.
- Binary layout sentinel: `BinaryLayoutManifest.VerifySaveLayouts()` now asserts `SaveFileHeaderV10` size/offsets and `SaveMasterHashV10Result` size `32` with offsets `0/8/16/24`.
- Header layout proof: independent packed-struct check -> PASS (`V10_HEADER_LAYOUT_OK`).
- Rotate formula proof: independent Python check of C# lane formulas -> PASS (`ROT128_FORMULA_OK`).
- C# static guard: new helper and manifest edit -> PASS (`CS_STATIC_GUARD_OK`).
- C# helper executable static guard: initial parser attempt failed on call-site/suffix extraction; corrected parser and reran `python -B Tools\Security\ValidateSaveMasterHashCSharp.py` -> PASS (`SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`).
- Roslyn parser-level probe: direct `csc.exe` on `SaveMasterHashV10.cs` reached semantic reference errors only (`Hecton8.Core.Memory.Layout`, `Unity.Mathematics`, `BinaryBlittableSafe` missing in standalone compile); no syntax diagnostics were emitted before missing-reference failure.
- Evidence trail self-review: `Rationale_SAVE_HASH_CRYPTOGRAPHER.md` now records Decisions 1-49 in numeric order; the earlier post-fix `python -m py_compile` pass is superseded by the latest AST syntax pass because bytecode writes are blocked.
- Latest syntax guard: `python -m py_compile` is blocked by `[WinError 5]` while replacing `Tools\Security\__pycache__\ReplayHasher.cpython-314.pyc`; AST parsing without `.pyc` writes passed for all owned Python tools (`PY_AST_OK files=3`).
- Prompt re-extraction check: latest regex extraction from `Docs\Tasks\CURRENT_BATCH.md` returned `PROMPT_NOT_FOUND`; current file has been replaced with a different batch. This is logged as batch churn, not a task-code failure.
- Temp dependency hygiene: `.codex_tmp\xxhash_check` -> removed after reference verification (`XXHASH_TMP_REMOVED`).
- Latest owned verification suite: `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, `XXHASH_MODULE_FILE_GUARD=PASS`, `XXHASH_API_SHAPE_GUARD=PASS`, `XXHASH_DIGEST_TYPE_GUARD=PASS`, `XXHASH_DIGEST_RANGE_GUARD=PASS`, `XXHASH_DIGEST_BOOL_GUARD=PASS`, `REPLAY_DIGEST_TYPE_GUARD=PASS`, `REPLAY_DIGEST_RANGE_GUARD=PASS`, `REPLAY_DIGEST_BOOL_GUARD=PASS`, `SHUFFLE_PAIR_TYPE_GUARD=PASS`, `SHUFFLE_PAIR_RANGE_GUARD=PASS`, `SHUFFLE_PAIR_BOOL_GUARD=PASS`, `UNSHUFFLE_PAIR_TYPE_GUARD=PASS`, `UNSHUFFLE_PAIR_RANGE_GUARD=PASS`, `UNSHUFFLE_PAIR_BOOL_GUARD=PASS`, `XXHASH_MISMATCH_CONTROLLED_FAILURE_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_SUCCESS_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_FAILURE_GUARD=PASS`, `XXHASH_TEMP_HELPER_MODULE_CLEANUP_GUARD=PASS`, `RATIONALE_ORDER_OK decisions=49`, `XXHASH_TMP_REMOVED`, `git diff --check` passed with line-ending warnings only.
- Omega polish extraction: `<POLISH_MANDATE>` tag not found in `Docs/Tasks/CURRENT_BATCH.md`; local anti-bloat pass completed on owned artifacts.
- C# compile attempt: `dotnet build .\Hecton8.slnx --no-restore` -> BLOCKED, `dotnet` not found on PATH; latest `Get-Command dotnet` also returned `DOTNET_NOT_FOUND`.
- Additional compiler probes: `csc` and `mcs` -> BLOCKED, not found on PATH.
- MSBuild direct probe: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe .\Hecton8.slnx /t:Build /p:RestorePackages=false /restore:false /nologo /clp:ErrorsOnly` -> BLOCKED before source compilation; `.slnx.metaproj` reports missing Unity-generated project files including `Assembly-CSharp.csproj`, `Hecton8.Core.csproj`, `Unity.RenderPipelines.Universal.Runtime.csproj`, and many third-party `.csproj` files.
- Unity editor compile/import attempt: `Get-Command Unity` -> `UNITY_NOT_FOUND`; `C:\Program Files\Unity\Hub\Editor` -> directory absent.
- Unity compile/import: PENDING VERIFICATION; C# source was added but no local compiler/editor is available in this shell.
- IL2CPP/ARM proof: PENDING VERIFICATION; DTO audit is static source review only.
