# Rationale_SAVE_HASH_CRYPTOGRAPHER

## Decision 1

Problem: The requested `CURRENT_BATCH_OSHINO.md` was absent in `C:\Hecton8`, but the required XML prompt existed in `Docs/Tasks/CURRENT_BATCH.md`.

Solution: Used PowerShell regex extraction of only `<AGENT_PROMPT id="SAVE_HASH_CRYPTOGRAPHER">...</AGENT_PROMPT>` from the active batch file.

Rejected Alternatives: Stopping for a missing filename would waste the batch; reading adjacent prompts would violate strict parsing.

Scalability potential: No runtime impact. Low/Middle/High/Ultra all use the same extracted directive.

Hardware Impact: 0 us frame impact on i3/MX350.

## Decision 2

Problem: Save integrity needs a replayable hash oracle that matches Burst C# and Python byte-for-byte.

Solution: Implemented scalar XXH3-64 in `Tools/Security/ReplayHasher.py` with fixed unsigned masks, little-endian loads, official default secret, and no external dependency.

Rejected Alternatives: Python `xxhash` package wrapper was rejected as the primary path because clean build agents may not have it. Managed C# reflection or runtime plugin calls were rejected because this is an offline oracle.

Scalability potential: Low uses the same deterministic oracle; High/Ultra can add more diagnostic lanes around the same hash without changing ABI.

Hardware Impact: 0 us frame impact. Offline oracle cost is irrelevant to MX350 frame budget.

## Decision 3

Problem: A 128-bit save hash needs tamper friction while staying simple enough for Burst and ARM.

Solution: Domain-separated 128-bit mask derived from `WorldSeed` and `AUP.SectorHash`, then XOR plus 128-bit rotate.

Rejected Alternatives: Per-byte pseudo-random permutation was rejected because it increases porting risk and does not create real cryptographic security. Calling it encryption was rejected; XXH3 is not a MAC.

Scalability potential: Low stores the same 16 bytes; Ultra can retain extra unshuffled debug hash in development dumps only.

Hardware Impact: Estimated sub-0.1 us for two 64-bit XORs and one 128-bit rotate in Burst on i3/MX350; no profiler artifact captured.

## Decision 4

Problem: Existing docs say the v8 header is 52 bytes, but current source uses `CurrentHeaderSize = 56`.

Solution: `Docs/Design/Save_Binary_Header.md` records current byte offsets and reserves v10 `MasterStateHashLo/Hi` at offsets 56 and 64.

Rejected Alternatives: Inserting `MasterStateHash` before `HashHeader64` was rejected because it would shift existing fields and break current readers.

Scalability potential: Stable header prefix lets old readers fail fast and new readers append stronger diagnostics.

Hardware Impact: 0 us frame impact. Save/load cold-path only.

## Decision 5

Problem: `SaveData.cs` contains mixed managed DTOs and blit-safe DTOs; raw blitting managed layouts is an ARM/IL2CPP crash risk.

Solution: Audited the DTO surface and flagged bool/string/array/list/dictionary/hashset DTOs for PHI_VOD mirror conversion while preserving public field order.

Rejected Alternatives: Mutating existing DTO public fields was rejected under interface immutability and migration safety. Blind `[StructLayout]` over managed fields was rejected because strings and arrays are references.

Scalability potential: Low uses fixed compact blit mirrors; Ultra can store additional debug hashes in telemetry without altering managed DTOs.

Hardware Impact: Prevents unaligned/native crash class. No measured runtime gain claimed.

## Decision 6

Problem: Internal Python self-tests can pass while still diverging from the canonical XXH3 implementation on seeded or long-input paths.

Solution: Installed the Python `xxhash` package into `.codex_tmp/xxhash_check` and compared `ReplayHasher.xxh3_64` against `xxhash.xxh3_64_intdigest` across 136 vectors covering seed and length boundaries.

Rejected Alternatives: Trusting only the empty-string vector was rejected because it does not exercise the 1-3, 4-8, 9-16, 17-128, 129-240, and long-input branches.

Scalability potential: Low/Middle/High/Ultra all keep the same deterministic hash oracle; higher tiers may add diagnostic hash lanes without changing the file ABI.

Hardware Impact: 0 us frame impact. Verification is an offline tooling pass.

## Decision 7

Problem: The mandated `<POLISH_MANDATE>` tag is absent from `Docs/Tasks/CURRENT_BATCH.md`, and the first self-test pass depended on an external reference comparison for strong branch coverage.

Solution: Treated the missing tag as a batch defect, then performed a local anti-bloat pass on owned artifacts. Embedded fixed expected XXH3 values for zero-seed branch boundaries, seeded branch boundaries, the shuffle mask, and the shuffled 128-bit output into `ReplayHasher.py` self-test.

Rejected Alternatives: Leaving self-test as empty-vector plus range checks was rejected because it would not catch accidental drift in the 17-128, 129-240, or long custom-secret paths. Adding the `xxhash` package as a repo dependency was rejected because the oracle must run on clean agents.

Scalability potential: Low/Middle/High/Ultra use the same deterministic vector set. Higher diagnostic tiers can add more vectors without touching save ABI.

Hardware Impact: 0 us frame impact. The extra self-test vectors are offline-only.
