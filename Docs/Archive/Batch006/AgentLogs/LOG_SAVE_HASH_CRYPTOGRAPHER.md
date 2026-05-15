# LOG_SAVE_HASH_CRYPTOGRAPHER

## 2026-05-14 - XXH3 Save Integrity Design

What was wrong: The active batch file named by the user, `CURRENT_BATCH_OSHINO.md`, is absent in `C:\Hecton8`; the live prompt exists in `Docs/Tasks/CURRENT_BATCH.md`. Existing save header documentation was stale against source: current `SaveBinaryStorage.SaveFileHeader` is 56 bytes, not the older 52-byte note.

What was done: Implemented `Tools/Security/ReplayHasher.py` as a dependency-free scalar XXH3-64 oracle with deterministic little-endian loads, 128-bit seed/sector XOR mask derivation, and reversible 128-bit shuffle/unshuffle helpers. Wrote `Docs/Design/Save_Binary_Header.md` with current header offsets, v10 `MasterStateHash` placement at offsets 56/64, DTO padding rules, and `SaveData.cs` ARM-killer audit notes.

Cinematic Cheats used: No runtime simulation was added. The tamper-resistance layer is a deterministic byte-level fake, not encryption and not a cryptographic MAC. It protects replay/debug integrity and cheap tamper detection without spending frame-time budget.

Exact Microseconds saved: 0 measured runtime microseconds claimed. Static estimate only: the shuffle path is two 64-bit XOR lanes plus fixed rotates, expected sub-0.1 us in Burst cold save/load code. No gameplay hot path was touched.

Verification: `python -m compileall .\Tools\Security\ReplayHasher.py` passed. `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. Isolated reference comparison against Python `xxhash.xxh3_64_intdigest` passed 136 vectors across boundary lengths and seeds. Unity compile/import and IL2CPP ARM execution remain PENDING VERIFICATION because no Unity editor or device run was executed in this pass.

## 2026-05-14 - Local Anti-Bloat Polish

What was wrong: `Docs/Tasks/CURRENT_BATCH.md` does not contain a `<POLISH_MANDATE>` tag, despite the agent protocol requiring it after all core tasks are checked. The original `ReplayHasher.py` self-test was too weak: empty-vector validation plus range checks would miss branch-specific drift.

What was done: Embedded fixed expected values for zero-seed XXH3 boundaries, seeded XXH3 boundaries, shuffle-mask derivation, shuffle output, and inverse recovery into `ReplayHasher.py` self-test. No package dependency was added.

Cinematic Cheats used: None. This is deterministic offline validation, not simulation.

Exact Microseconds saved: 0 measured runtime microseconds. The added checks run only during CLI self-test and consume no frame budget.

Verification: `python -m compileall .\Tools\Security\ReplayHasher.py` passed. `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. Isolated `xxhash.xxh3_64_intdigest` comparison still passed 136 vectors after the self-test hardening.

## 2026-05-14 - Professional Self-Review Pass

What was wrong: The master hash preimage was only documented and did not bind the full current header prefix/body. That was an accuracy gap: two agents could implement different master hashes while both claiming to follow the prose.

What was done: Added the `master` CLI command to `Tools/Security/ReplayHasher.py`, added explicit little-endian pack helpers, removed the misleading chunk-join helper, and updated `Docs/Design/Save_Binary_Header.md` with the executable preimage and expected master vector.

Cinematic Cheats used: The save hardening remains a deterministic tamper-friction fake, not encryption. No physical simulation and no runtime visual system changed.

Exact Microseconds saved: 0 measured runtime microseconds. Static cold-path estimate remains sub-0.1 us for the shuffle itself; the master preimage adds two XXH3-64 lanes in save/load code only.

Verification: `python -m compileall .\Tools\Security\ReplayHasher.py` passed. `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python .\Tools\Security\ReplayHasher.py master ...` returned `stored_le=6d24c9a87e8ec3322681980ad2b6b28c`. External `xxhash.xxh3_64_intdigest` comparison plus randomized shuffle inverse fuzz passed 264 cases.

## 2026-05-14 - Self-Review Correction And Tooling Probe

What was wrong: My first master self-test patch still had stale placeholder lanes. The `master` CLI output was correct, but `self-test` failed until the expected tuple was corrected. Local compile evidence was also too vague without probing the actual tools.

What was done: Replaced the stale master self-test vector with the deterministic CLI-emitted lanes. Probed `dotnet build .\Hecton8.slnx --no-restore` and `where.exe Unity` for local compile/import availability.

Cinematic Cheats used: None. Verification and tooling probe only.

Exact Microseconds saved: 0 runtime microseconds. No Unity runtime code changed.

Verification: `python -m compileall .\Tools\Security\ReplayHasher.py` passed. `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python .\Tools\Security\ReplayHasher.py master ...` returned `stored_le=6d24c9a87e8ec3322681980ad2b6b28c`. External reference/fuzz remained `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK 264 cases`. `dotnet` is not available on PATH; `Unity.exe` is not available on PATH.

## 2026-05-14 - C# Implementation Surface

What was wrong: The prior handoff was still too soft for implementation: Python oracle plus prose did not give the Unity side a concrete zero-GC byte writer and 128-bit rotate implementation.

What was done: Added `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs` with `Compute`, `DeriveShuffleMask`, `ShuffleHash128`, and `UnshuffleHash128`. Added `SaveMasterHashV10Result` layout checks to `Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs`. The active v9 save writer was not changed.

Cinematic Cheats used: This is still deterministic tamper friction, not cryptographic security. No simulation or visual system changed.

Exact Microseconds saved: 0 measured runtime microseconds. Estimated cost remains cold-path only: two master XXH3 lanes plus two shuffle-mask XXH3 lanes and constant-time 128-bit rotate. No gameplay frame path touched.

Verification: `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. Independent rotate formula proof returned `ROT128_FORMULA_OK`. C# static text balance returned `CS_TEXT_BALANCE_OK`. `git diff --check` passed with line-ending warnings only. Local C# compile remains blocked because `dotnet`, `csc`, `mcs`, and `Unity.exe` are not available on PATH.

## 2026-05-14 - V10 Header Layout Upgrade

What was wrong: The C# helper owned the hash math but not the concrete 72-byte header layout. The byte-offset requirement for `MasterStateHash` still needed code-level enforcement.

What was done: Added `SaveFileHeaderV10`, compute/fill/validate overloads, and full `BinaryLayoutManifest` assertions for every V10 header field. The active v9 writer remains untouched.

Cinematic Cheats used: None. This is binary ABI hardening only.

Exact Microseconds saved: 0 measured runtime microseconds. No gameplay frame path touched.

Verification: `python .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. Independent packed layout check returned `V10_HEADER_LAYOUT_OK`. C# static guard returned `CS_STATIC_GUARD_OK`. `git diff --check` passed with line-ending warnings only. Unity/C# compile remains blocked by missing local tooling.

## 2026-05-14 - MSBuild Reality Check

What was wrong: I previously recorded MSBuild as unavailable based only on PATH. That was incomplete evidence.

What was done: Located `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` and ran it directly against `Hecton8.slnx`.

Cinematic Cheats used: None. Tooling verification only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: MSBuild launched but the build failed before compiling project code because `Hecton8.slnx` references missing `.csproj` files such as `Hecton8.Core.csproj`; the checkout contains `.csproj.lscache` files instead. This is a project-generation/tooling blockage, not a compile error from `SaveMasterHashV10.cs`.

## 2026-05-14 - CSharp Helper Static Guard

What was wrong: Unity/C# compile is still blocked, so `SaveMasterHashV10.cs` needed an executable static parity guard instead of relying only on prose and manual review.

What was done: Added `Tools/Security/ValidateSaveMasterHashCSharp.py`. The guard reads `SaveMasterHashV10.cs`, `BinaryLayoutManifest.cs`, and `ReplayHasher.py`; it verifies C# domain bytes, constants, layout sentinels, stackalloc-only buffer shape, and the frozen Python master vector.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents byte-domain drift before save/load integration.

Verification: `python -B Tools\Security\ValidateSaveMasterHashCSharp.py` returns `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=8`. Unity/C# compile remains PENDING VERIFICATION.

## 2026-05-14 - Static Guard Parser Correction

What was wrong: The new C# helper guard initially failed before validation. It matched a method call-site instead of the method declaration, then treated suffix-only domain writers as malformed because their first literal assignment is `target[15]`.

What was done: Tightened method extraction to C# static declarations and added explicit byte-assignment start windows. Full domain writers still require contiguous bytes from zero; shuffle lane suffix writers require contiguous bytes from 15.

Cinematic Cheats used: None. Offline tooling correction only.

Exact Microseconds saved: 0 runtime microseconds. The change prevents a false tooling failure before Unity import is available.

Verification: `python -B Tools\Security\ValidateSaveMasterHashCSharp.py` returns `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=8`; `python -m py_compile Tools\Security\ReplayHasher.py Tools\Security\ValidateSaveMasterHashCSharp.py` passed; `python -B Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`.

## 2026-05-14 - Expanded Static Guard And Roslyn Probe

What was wrong: The static guard still under-counted the ABI sentinels. It only guarded two `SaveFileHeaderV10` offsets plus result lanes, while the actual header ABI has 15 fields.

What was done: Expanded `ValidateSaveMasterHashCSharp.py` to verify all 21 manifest sentinels and exact `SaveFileHeaderV10` field order. Located Visual Studio Roslyn `csc.exe` and ran a direct parser-level compile probe against `SaveMasterHashV10.cs`.

Cinematic Cheats used: None. Offline verification hardening only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: `python -B Tools\Security\ValidateSaveMasterHashCSharp.py` returns `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21`. Direct Roslyn compile reached missing-reference diagnostics for `Hecton8.Core.Memory.Layout`, `Unity.Mathematics`, and `BinaryBlittableSafe`; no syntax diagnostics were emitted before those expected standalone-reference failures.

## 2026-05-14 - Evidence Trail Self-Review

What was wrong: `Rationale_SAVE_HASH_CRYPTOGRAPHER.md` had Decision 14 recorded after Decisions 15 and 16. The implementation evidence was intact, but the decision trail order was sloppy and unacceptable for batch handoff.

What was done: Moved the parser-correction rationale into the correct numeric position after Decision 13 and before the expanded guard/Roslyn decisions. No runtime source was changed in this pass.

Cinematic Cheats used: None. Documentation hygiene only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: `python -m py_compile .\Tools\Security\ReplayHasher.py .\Tools\Security\ValidateSaveMasterHashCSharp.py` passed. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21`.

## 2026-05-14 - Header Forwarding Guard Upgrade

What was wrong: The parity guard did not prove the `SaveFileHeaderV10` overload forwards only the intended non-circular fields. A future accidental inclusion of `HashHeader64` or `MasterStateHash*` could have passed the previous static check.

What was done: Hardened `Tools/Security/ValidateSaveMasterHashCSharp.py` to validate exact 12-field forwarding order, circular-field exclusion, shuffled stored-lane assignment/comparison, Unity.Mathematics `uint2` lane assembly, and Python master preimage length.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. The change prevents integrity drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -m py_compile ...` is blocked by `[WinError 5]` while replacing a `.pyc` file in `Tools\Security\__pycache__`; AST syntax parsing without bytecode writes passed with `PY_AST_OK files=2`.

## 2026-05-14 - Preimage Writer Sequence Guard

What was wrong: The guard proved header overload forwarding, but did not prove the actual C# byte-writer order inside `BuildMasterPreimage`.

What was done: Added extraction of the C# preimage write sequence and locked it to 15 canonical writes: domain, v10 header prefix/body fields, `HashPayload64`, `WorldSeed`, and `SectorHash`.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents byte-order drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`.

## 2026-05-14 - Prompt Snapshot Preservation

What was wrong: Latest XML re-extraction from `Docs\Tasks\CURRENT_BATCH.md` returned `PROMPT_NOT_FOUND`; the file has been replaced by a different batch and no longer contains `SAVE_HASH_CRYPTOGRAPHER`. First snapshot preservation used a compressed task summary, not the exact removed XML.

What was done: Preserved the exact removed XML directive inside `Docs/Tasks/Status_SAVE_HASH_CRYPTOGRAPHER.md` using the removed block visible in `git diff -- Docs\Tasks\CURRENT_BATCH.md`. Did not revert `CURRENT_BATCH.md` because that change is outside my owned work and appears to belong to another active batch.

Cinematic Cheats used: None. Evidence hygiene only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: PowerShell regex extraction returned `PROMPT_NOT_FOUND`; `git diff -- Docs\Tasks\CURRENT_BATCH.md` shows a different batch replacing the earlier agent list.

## 2026-05-14 - Preimage Cursor Offset Guard

What was wrong: The guard proved C# preimage field order but not the `cursor += N` schedule. A wrong advance could silently shift bytes while preserving the same field names.

What was done: Added extraction of the C# preimage cursor operation stream. The guard now locks 26 operations and proves the final preimage byte end is `80`.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents save-hash ABI drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`.

## 2026-05-14 - Shuffle Mask Byte-Order Guard

What was wrong: The parity guard checked shuffle domain strings but not the C# byte order for `WorldSeed`, `SectorHash`, and `maskLo` inside `DeriveShuffleMask`.

What was done: Added extraction of the `DeriveShuffleMask` byte operation stream. The guard now locks 12 operations and proves the low/high mask preimage byte ends are `36/44`.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents Python/Burst bit-shuffle drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`.

## 2026-05-14 - Rotate Edge Vector Hardening

What was wrong: `ReplayHasher.py self-test` validated one shuffle/inverse vector but did not pin 128-bit rotate edge shifts. A future rotate regression at shift `64` or `127` could pass if the shuffle vector did not hit that branch.

What was done: Added fixed `rotl128` vectors for shifts `0/1/63/64/65/127` and inverse `rotr128` validation for each shift.

Cinematic Cheats used: None. Offline math validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents replay-hash math drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44`. AST syntax parsing returned `PY_AST_OK files=2`.

## 2026-05-14 - CSharp Rotate Branch Guard

What was wrong: Python rotate edge vectors were pinned, but the C# parity guard did not lock the `Rotl128`/`Rotr128` branch formulas.

What was done: Added static checks for the C# rotate branches covering shift `0`, shift `64`, `<64`, and `>64` lane formulas for both left and right rotation.

Cinematic Cheats used: None. Offline math validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents Burst-side bit-shuffle drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`.

## 2026-05-14 - External Compile Boundary Recheck

What was wrong: Static validation is green, but Unity/C# compile/import proof is still externally blocked.

What was done: Rechecked tool availability with longer timeouts. `dotnet` is not on PATH. Unity is not on PATH and `C:\Program Files\Unity\Hub\Editor` is absent. Visual Studio MSBuild exists, but `Hecton8.slnx` fails before source compilation because Unity-generated `.csproj` files are missing.

Cinematic Cheats used: None. Verification boundary only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: Direct MSBuild emitted `MSB3202` missing project-file errors from `Hecton8.slnx.metaproj`, including `Assembly-CSharp.csproj`, `Hecton8.Core.csproj`, `Unity.RenderPipelines.Universal.Runtime.csproj`, and many third-party project files. No source compile was reached.

## 2026-05-15 - Little-Endian Primitive Writer Guard

What was wrong: The parity guard proved C# preimage order and offsets, but did not prove the primitive `WriteU16`, `WriteU32`, and `WriteU64` helper bodies. A byte-swap inside one helper would corrupt every master-hash preimage while preserving the same field list.

What was done: Hardened `Tools/Security/ValidateSaveMasterHashCSharp.py` with exact body checks for all three primitive little-endian writers. The guard now locks byte indexes and shift counts for 16-bit, 32-bit, and 64-bit serialization.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents cross-platform save-hash drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Signed Lane Packing Self-Test

What was wrong: Negative `WorldSeed` and `AUP.SectorHash` values depend on exact two's-complement little-endian lane bytes. The code masked those values correctly, but the oracle did not freeze the signed edge cases in self-test.

What was done: Added fixed self-test vectors for `0`, `1`, `-1`, `-987654321`, `long.MinValue`, and `long.MaxValue`, plus a 128-bit little-endian lane hex roundtrip check.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents signed-lane save-hash drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Hash64 Full-Lane Convention Guard

What was wrong: `SaveMasterHashV10` has a local `Hash64` wrapper and existing `SaveBinaryStorage` also has `Hash64` plus an intentional low-lane `Hash32`. A future edit could accidentally collapse the V10 hash to `.x` while tests still looked like "XXH3 was called."

What was done: Extended `Tools/Security/ValidateSaveMasterHashCSharp.py` to read `SaveBinaryStorage.cs` and validate that both full `Hash64` helpers assemble `((ulong)hash.y << 32) | hash.x`.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents integrity-width drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Stackalloc Buffer Contract Guard

What was wrong: The static guard only counted `stackalloc byte[` occurrences. That proved stack allocation style, but not the exact buffer capacities used for master preimage and shuffle-mask hashing.

What was done: Hardened `Tools/Security/ValidateSaveMasterHashCSharp.py` to require the exact stack buffer declarations for `MasterHiHashBytes` and `ShuffleMaskHiBytes`.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents buffer-contract drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Master Hash Edge Fixture Expansion

What was wrong: `ReplayHasher.py self-test` pinned only one full `MasterStateHash` vector. That verified the mixed signed-sector case but did not lock zero metadata, all-max unsigned fields, or signed lane extremes.

What was done: Replaced the single master vector with four frozen fixtures: mixed signed sector, zero metadata, max signed edges, and opposite signed lanes.

Cinematic Cheats used: None. Offline binary ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents master-hash fixture blind spots; no gameplay path changed.

Verification: `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Internal API Boundary Guard

What was wrong: The V10 helper is intentionally isolated until Unity import/load verification exists. The static guard did not prevent a future edit from promoting the V10 header/helper into public API during the active batch.

What was done: Extended `Tools/Security/ValidateSaveMasterHashCSharp.py` to require `SaveMasterHashV10Result`, `SaveFileHeaderV10`, and `SaveMasterHashV10` remain `internal`, and to reject public declarations for the header/helper.

Cinematic Cheats used: None. Offline API boundary validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents premature save ABI surface expansion; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Active Writer Isolation Guard

What was wrong: Keeping V10 helper types internal did not by itself prevent the active save writer from accidentally switching to V10 before Unity import/load verification.

What was done: Extended `Tools/Security/ValidateSaveMasterHashCSharp.py` to require `SaveBinaryStorage.CurrentVersion = 0x0009`, `CurrentHeaderSize = 56`, and no `SaveMasterHashV10`/`SaveFileHeaderV10` references in `SaveBinaryStorage.cs`.

Cinematic Cheats used: None. Offline integration-boundary validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents premature active save ABI mutation; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=2`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reusable XXH3 Reference Verifier

What was wrong: The external comparison against Python `xxhash.xxh3_64_intdigest` existed only as a historical one-off command. That is weak evidence after context loss.

What was done: Added `Tools/Security/VerifyReplayHasherReference.py`. It is separate from `ReplayHasher.py`, imports `xxhash` only from an explicit temporary path, compares 338 XXH3 vectors, and runs 128 shuffle inverse cases.

Cinematic Cheats used: None. Offline reference validation only.

Exact Microseconds saved: 0 runtime microseconds. This improves reproducibility of save-hash verification; no gameplay path changed.

Verification: first run without `xxhash` correctly returned a missing optional dependency error. After temporary install to `.codex_tmp\xxhash_check`, `python -B .\Tools\Security\VerifyReplayHasherReference.py --xxhash-path .\.codex_tmp\xxhash_check --fuzz-count 128` returned `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2`. AST syntax parsing returned `PY_AST_OK files=3`. Temporary package directory was removed with workspace path guard (`XXHASH_TMP_REMOVED`).

## 2026-05-15 - Reference Verifier Command Documentation

What was wrong: The new external-reference verifier was discoverable in logs, but not in the stable save header design document where future agents will look first.

What was done: Added the optional `xxhash` temporary install command and `VerifyReplayHasherReference.py` invocation to `Docs/Design/Save_Binary_Header.md`.

Cinematic Cheats used: None. Documentation hardening only.

Exact Microseconds saved: 0 runtime microseconds.

Verification: Stable doc now lists the verifier command. Latest tool verification remains `SELFTEST_OK`, `PY_AST_OK files=3`, and `SAVE_MASTER_HASH_CSHARP_GUARD=PASS ... activeWriterSentinels=2`.

## 2026-05-15 - Result Constructor Lane-Order Guard

What was wrong: `BinaryLayoutManifest` verifies `SaveMasterHashV10Result` offsets, but not that the constructor assigns plain and stored lanes in canonical order.

What was done: Extended `Tools/Security/ValidateSaveMasterHashCSharp.py` to validate the constructor assignments and the raw `Compute` overload's result construction call.

Cinematic Cheats used: None. Offline ABI semantic validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents lane-order corruption; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=3`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Blit-Safe Attribute Static Guard

What was wrong: Unity cold-boot validation would catch missing `[BinaryBlittableSafe]`, but the offline parity guard did not. Unity import is unavailable, so attribute drift needed a static guard.

What was done: Extended `Tools/Security/ValidateSaveMasterHashCSharp.py` to require `[BinaryBlittableSafe]` paired with the packed `StructLayout` declarations for `SaveMasterHashV10Result` and `SaveFileHeaderV10`.

Cinematic Cheats used: None. Offline ABI validation only.

Exact Microseconds saved: 0 runtime microseconds. This prevents blit-safety attribute drift; no gameplay path changed.

Verification: `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`. `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. AST syntax parsing returned `PY_AST_OK files=3`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Master Preimage Byte Fixture

What was wrong: Final master hash vectors were frozen, but the raw 80-byte preimage was not. That makes byte-order drift harder to diagnose.

What was done: Added the exact mixed-case master preimage hex and length check to `ReplayHasher.py self-test`.

Cinematic Cheats used: None. Offline byte-order validation only.

Exact Microseconds saved: 0 runtime microseconds. This improves diagnostic precision; no gameplay path changed.

Verification: `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`. AST syntax parsing returned `PY_AST_OK files=3`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Path Pinning

What was wrong: `VerifyReplayHasherReference.py` claimed the external `xxhash` oracle came from an explicit temp path, but the argument was optional. A globally installed `xxhash` could satisfy the proof and contaminate the result with developer machine state.

What was done: Made `--xxhash-path` mandatory, verified the path exists, and added a containment check that rejects an imported `xxhash` module whose `__file__` is outside the resolved temp directory. Documented the hard requirement in `Docs/Design/Save_Binary_Header.md`.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves reproducibility and contamination resistance; no gameplay path changed.

Verification: owned verification suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Malformed Module Guard

What was wrong: The new path containment check assumed an imported `xxhash` module has `__file__`. That is true for the official wheel, but a contaminated module object without `__file__` would fail through an uncontrolled exception path.

What was done: Added an explicit missing-`__file__` guard in `verify_module_path()` so unverifiable modules fail cleanly with a controlled error.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves failure determinism; no gameplay path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. Direct malformed-module probe returned `XXHASH_MODULE_FILE_GUARD=PASS`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Cached Module Eviction

What was wrong: Embedded use of `VerifyReplayHasherReference.py` could inherit an existing `sys.modules["xxhash"]` from the host Python process. The path containment check would reject a wrong path, but it still did not force loading the explicitly requested isolated module.

What was done: Added `sys.modules.pop("xxhash", None)` after inserting `--xxhash-path` and before importing `xxhash`. Updated the save header design doc to state the cached-module eviction behavior.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves isolation for embedded verifier calls; no gameplay path changed.

Verification: direct embedded-process probe preloaded a polluted `sys.modules["xxhash"]`, then verified the tool imported from the requested temp path and returned `XXHASH_SYSMODULES_EVICTION_GUARD=PASS`. Full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXHASH_MODULE_FILE_GUARD=PASS`, `XXHASH_SYSMODULES_EVICTION_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Embedded Cleanup

What was wrong: Embedded calls to `VerifyReplayHasherReference.py` could leave `sys.path` and `sys.modules["xxhash"]` mutated after `main()` returned. The CLI process exits, but a Python harness would keep that contamination.

What was done: Wrapped the isolated import/verification window in `finally`, removing the inserted `--xxhash-path` entry and restoring the previous `xxhash` module object or absence state.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves repeatability of tooling; no gameplay path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXHASH_MODULE_FILE_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_SUCCESS_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_FAILURE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Evidence Consistency Correction

What was wrong: `Status_SAVE_HASH_CRYPTOGRAPHER.md` still claimed the rationale ordering proof covered Decisions `1-37`, while the rationale file now contains Decisions `1-41`. The latest-suite evidence list also carried an older standalone sys.modules guard label after the cleanup probe had superseded it.

What was done: Updated the status file to record Decisions `1-41`, added Loop 40, and aligned latest-suite labels with the current cleanup success/failure probes.

Cinematic Cheats used: None. Evidence hygiene only.

Exact Microseconds saved: 0 runtime microseconds. No runtime or active save writer changed.

Verification: post-correction checks returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_TMP_ABSENT`, and `git diff --check` passed with line-ending warnings only. Status/rationale readback confirms Loop 40 and Decisions `1-42`.

## 2026-05-15 - Reference Verifier Temp Helper Cleanup

What was wrong: Embedded verifier cleanup restored `xxhash` and `sys.path`, but a temp-path `xxhash` package could import helper modules that remained in `sys.modules` after `main()` returned.

What was done: Added cleanup for newly loaded modules whose `__file__` resolves under `--xxhash-path`, while preserving modules that existed before verifier execution. Updated the design doc and status file.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This prevents verifier state contamination; no runtime save path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXHASH_MODULE_FILE_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_SUCCESS_GUARD=PASS`, `XXHASH_TEMP_HELPER_MODULE_CLEANUP_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_FAILURE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier API Shape Guard

What was wrong: A module named `xxhash` under the explicit temp path could pass path containment but lack `xxh3_64_intdigest`, producing an uncontrolled traceback instead of a deterministic verifier error.

What was done: Added `verify_module_api()` to require a callable `xxh3_64_intdigest` before vector verification starts. Updated the design doc and status file.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves failure clarity; no runtime save path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_PATH_REQUIRED_GUARD=PASS`, `XXHASH_MODULE_FILE_GUARD=PASS`, `XXHASH_API_SHAPE_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_SUCCESS_GUARD=PASS`, `XXHASH_TEMP_HELPER_MODULE_CLEANUP_GUARD=PASS`, `XXHASH_EMBEDDED_CLEANUP_FAILURE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Digest Shape Guard

What was wrong: A callable `xxh3_64_intdigest` could still return a non-integer or an integer outside the unsigned 64-bit digest range, causing noisy failures or invalid comparison data.

What was done: Added explicit reference digest type and range checks inside `verify_xxh3()` before comparing against `ReplayHasher.py`.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves verifier failure precision; no runtime save path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_DIGEST_TYPE_GUARD=PASS`, `XXHASH_DIGEST_RANGE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Replay Digest Shape Guard

What was wrong: The external reference digest was type/range checked, but the local `ReplayHasher.py` digest was not. A future oracle regression could produce a non-integer or out-of-range value and fail through later formatting.

What was done: Added shared `require_u64_digest()` validation and applied it to both reference and replay digests before comparison.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves verifier failure precision; no runtime save path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `REPLAY_DIGEST_TYPE_GUARD=PASS`, `REPLAY_DIGEST_RANGE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Controlled Mismatch Failure

What was wrong: Real XXH3 or shuffle mismatches still surfaced as uncaught `AssertionError` tracebacks in CLI mode. That is noisy for CI and for future agents reading command output.

What was done: Caught vector `AssertionError` failures in `main()`, printed the exact mismatch message, returned exit code `1`, and preserved import-state cleanup.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves verifier failure clarity; no runtime save path changed.

Verification: full owned suite returned `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_MISMATCH_CONTROLLED_FAILURE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Shuffle Mask Preimage Byte Fixtures

What was wrong: Shuffle mask output vectors were frozen, but the raw low/high mask preimage bytes were not. That makes lane-order drift harder to diagnose.

What was done: Added exact low/high shuffle-mask preimage hex and length checks to `ReplayHasher.py self-test`.

Cinematic Cheats used: None. Offline byte-order validation only.

Exact Microseconds saved: 0 runtime microseconds. This improves diagnostic precision; no gameplay path changed.

Verification: `python -B .\Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`. `python -B .\Tools\Security\ValidateSaveMasterHashCSharp.py` returned `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`. AST syntax parsing returned `PY_AST_OK files=3`. `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Shuffle Pair Guard

What was wrong: `VerifyReplayHasherReference.py` validated XXH3 digest type/range, but the shuffle path still trusted `shuffle_hash128` and `unshuffle_hash128` to return exactly two unsigned 64-bit lanes.

What was done: Added lane-pair validation helpers and applied them before indexing stored lanes or comparing recovered lanes.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves verifier failure precision; no runtime save path changed.

Verification: `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `SHUFFLE_PAIR_TYPE_GUARD=PASS`, `SHUFFLE_PAIR_RANGE_GUARD=PASS`, `UNSHUFFLE_PAIR_TYPE_GUARD=PASS`, `UNSHUFFLE_PAIR_RANGE_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, `RATIONALE_ORDER_OK decisions=48`, `XXHASH_TMP_REMOVED`, and `git diff --check` passed with line-ending warnings only.

## 2026-05-15 - Reference Verifier Exact Int Guard

What was wrong: Python accepts `bool` as an `int` subtype, so the verifier still allowed boolean digest/lane values through the type gate.

What was done: Replaced `isinstance(value, int)` with exact `type(value) is int` checks for digest and 128-bit lane validation.

Cinematic Cheats used: None. Offline verification tooling only.

Exact Microseconds saved: 0 runtime microseconds. This improves verifier ABI strictness; no runtime save path changed.

Verification: `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `XXHASH_DIGEST_BOOL_GUARD=PASS`, `REPLAY_DIGEST_BOOL_GUARD=PASS`, `SHUFFLE_PAIR_BOOL_GUARD=PASS`, `UNSHUFFLE_PAIR_BOOL_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, and `XXHASH_TMP_REMOVED`.

## 2026-05-15 - Final Owned Verification Sweep

What was wrong: The verifier and logs had been hardened incrementally; final evidence needed one consolidated pass after all edits.

What was done: Re-ran syntax, replay self-test, C# static parity guard, shuffle pair guards, exact-int bool guards, isolated external `xxhash` fuzz, temp cleanup, rationale ordering, and scoped diff hygiene.

Cinematic Cheats used: None. Verification-only pass.

Exact Microseconds saved: 0 runtime microseconds. No runtime save path changed.

Verification: `PY_AST_OK files=3`, `SELFTEST_OK`, `SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=21 headerForwarding=12 preimageWrites=15 preimageOps=26 preimageEnd=80 shuffleOps=12 shuffleEnds=36/44 rotGuards=2 endianWriters=3 hash64Helpers=2 stackallocBuffers=2 internalTypes=3 activeWriterSentinels=2 resultCtor=4 blitAttrs=2`, `SHUFFLE_PAIR_TYPE_GUARD=PASS`, `SHUFFLE_PAIR_RANGE_GUARD=PASS`, `UNSHUFFLE_PAIR_TYPE_GUARD=PASS`, `UNSHUFFLE_PAIR_RANGE_GUARD=PASS`, `XXHASH_DIGEST_BOOL_GUARD=PASS`, `REPLAY_DIGEST_BOOL_GUARD=PASS`, `SHUFFLE_PAIR_BOOL_GUARD=PASS`, `UNSHUFFLE_PAIR_BOOL_GUARD=PASS`, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`, `XXHASH_TMP_REMOVED`, `RATIONALE_ORDER_OK decisions=49`, and `git diff --check` passed with line-ending warnings only.
