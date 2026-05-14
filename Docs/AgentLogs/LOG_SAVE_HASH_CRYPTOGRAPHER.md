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
