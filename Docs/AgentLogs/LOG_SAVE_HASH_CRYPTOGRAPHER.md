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
