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

## Decision 8

Problem: The first `MasterStateHash` contract existed only as prose and did not bind all current header fields. That left room for SHINOBU to implement a different byte preimage while still passing the shuffle-only tests.

Solution: Added an executable `master` command to `ReplayHasher.py` and corrected the preimage to include `MagicValue`, `Version`, `CompatMask`, `Flags`, `TimestampUnixMs`, `Checksum`, counts, offsets, `HashPayload64`, `WorldSeed`, and `AUP.SectorHash`. `HashHeader64` and `MasterStateHashLo/Hi` remain excluded to avoid circular hashing.

Rejected Alternatives: Keeping the master hash as documentation-only was rejected because it does not prove cross-platform byte order. Including `HashHeader64` was rejected because the existing header hash already depends on the header span and would create an ordering/circularity problem.

Scalability potential: Low/Middle/High/Ultra use the same 16-byte stored master hash. Higher tiers can log the unshuffled plain lanes in development-only telemetry without changing the save ABI.

Hardware Impact: 0 us gameplay impact. Save/load cold-path cost remains two XXH3 lanes plus the existing shuffle.

## Decision 9

Problem: Self-review caught a defect in my own edit: the executable `master` command returned the deterministic master vector, but the self-test still compared against stale placeholder lanes and failed.

Solution: Patched the frozen self-test vector to the CLI-emitted lanes: `plain_lo=0x82C250ACAADCFCEE`, `plain_hi=0x750FEB3BE2F001A7`, `stored_lo=0x32C38E7EA8C9246D`, `stored_hi=0x8CB2B6D20A988126`.

Rejected Alternatives: Weakening or removing the master self-test was rejected. A failing self-test is useful evidence; the correct fix is to freeze the real deterministic vector and rerun the suite.

Scalability potential: Same as Decision 8; this is verification hardening only.

Hardware Impact: 0 us frame impact. Offline self-test only.

## Decision 10

Problem: The Python oracle and design doc were not enough for implementation handoff; SHINOBU still needed a concrete C# surface for the master hash and bit-shuffle math.

Solution: Added `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs` as an internal isolated helper. It uses stackalloc buffers, manual little-endian byte writes, Unity.Mathematics `xxHash3.Hash64`, and explicit 128-bit lane rotation. Added `SaveMasterHashV10Result` to `BinaryLayoutManifest` so its 32-byte ABI is checked at cold boot.

Rejected Alternatives: Directly bumping `SaveBinaryStorage.CurrentVersion` to `0x000A` was rejected because that would change active save ABI without Unity import/play/load verification. Managed `byte[]`, `BitConverter`, and string preimage builders were rejected because they are allocation-prone and weaker for Burst parity.

Scalability potential: Low/Middle/High/Ultra share the same result ABI. Ultra/debug builds can log `PlainLo/PlainHi`; shipping saves store only `StoredLo/StoredHi`.

Hardware Impact: 0 us gameplay frame impact. Cold save/load path only; stack buffer footprint is 91 bytes max.

## Decision 11

Problem: The C# helper computed the V10 hash but did not yet own a concrete 72-byte header layout. That left the most important requirement, byte offsets 56 and 64, protected only by documentation.

Solution: Added `SaveFileHeaderV10` with `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 72)]`, added helper overloads for compute/fill/validate, and extended `BinaryLayoutManifest` to assert every V10 header field offset.

Rejected Alternatives: Mutating `SaveBinaryStorage.SaveFileHeader` was rejected because it would alter the active v9 ABI. Leaving layout proof in docs only was rejected because offset drift must fail cold-boot validation once Unity can import.

Scalability potential: Low/Middle/High/Ultra share the same 72-byte header ABI. Future high-tier debug can validate plain lanes without changing stored bytes.

Hardware Impact: 0 us gameplay frame impact. Cold save/load header math only; no active writer integration in this pass.

## Decision 12

Problem: Tooling evidence was incomplete. `MSBuild` was absent from PATH, but a Visual Studio Community MSBuild executable exists in a standard install directory.

Solution: Ran MSBuild directly against `Hecton8.slnx`. The build did not reach source compilation because the solution references missing `.csproj` files while this checkout only has `.csproj.lscache` artifacts.

Rejected Alternatives: Reporting "MSBuild missing" without checking standard install paths was rejected after review. Creating or renaming project files from `.lscache` was rejected because that would mutate generated Unity project artifacts outside this task's domain.

Scalability potential: No runtime impact. This only tightens verification accuracy.

Hardware Impact: 0 us frame impact.

## Decision 13

Problem: Unity/C# compile remains unavailable in this shell, so C# helper parity with the Python oracle could drift silently between prose, `ReplayHasher.py`, and `SaveMasterHashV10.cs`.

Solution: Added `Tools/Security/ValidateSaveMasterHashCSharp.py`. It statically extracts C# domain byte writers, constants, forbidden managed patterns, and `BinaryLayoutManifest` sentinels, then compares them against the Python oracle constants and frozen master vector.

Rejected Alternatives: Relying on documentation-only parity was rejected because byte-domain drift is easy to miss. Generating C# from Python was rejected because it would change the implementation workflow and introduce a larger generated-code surface.

Scalability potential: Low/Middle/High/Ultra all keep the same save ABI; the guard prevents platform-specific drift before Unity import.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 14

Problem: The first executable guard run failed for tooling reasons, not hash reasons. `extract_method_body` matched the first call-site name before the declaration, and `extract_byte_assignments` assumed every writer started at `target[0]` even though shuffle lane writers intentionally append from `target[15]`.

Solution: Changed method extraction to require a C# static method declaration and added explicit `expected_start` validation for byte assignment windows. The guard now verifies both contiguous full domains and suffix-only domain writers without relaxing byte equality.

Rejected Alternatives: Suppressing parser failures or slicing empty tails was rejected because that would turn the guard into a false positive. Rewriting the C# domain writers just to satisfy a weaker parser was rejected because the C# implementation was already explicit and cheaper to review.

Scalability potential: Low/Middle/High/Ultra save ABI is unchanged. This only hardens offline parity checks.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 15

Problem: The static C# guard still under-verified the header ABI. It checked the master-lane offsets but did not prove the full `SaveFileHeaderV10` field order or all manifest sentinels.

Solution: Expanded `ValidateSaveMasterHashCSharp.py` to validate all 21 manifest sentinel lines and the exact `SaveFileHeaderV10` public field order.

Rejected Alternatives: Trusting the manifest because it was manually written was rejected. A binary ABI guard must fail if any field is reordered, not only if the final two fields move.

Scalability potential: No runtime impact. This reduces future ABI drift across all quality tiers.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 16

Problem: Unity compile remains blocked, but plain text/static checks do not prove the C# file parses.

Solution: Located Visual Studio Roslyn `csc.exe` and ran it directly against `SaveMasterHashV10.cs`. The compiler reached expected missing-reference diagnostics for Unity/project assemblies and did not emit syntax diagnostics.

Rejected Alternatives: Generating fake Unity/project stubs for a green standalone compile was rejected as weaker evidence than a real Unity import and as extra temporary code surface. Reporting "no compiler available" was rejected after finding Roslyn.

Scalability potential: No runtime impact. This only tightens verification accuracy.

Hardware Impact: 0 us frame impact.

## Decision 17

Problem: The executable C# parity guard proved domains, constants, layout sentinels, and field order, but it did not prove that the `SaveFileHeaderV10` overload forwards only the intended non-circular fields or that validation compares stored shuffled lanes.

Solution: Added static checks for the exact 12-field header forwarding order, exclusion of `HashHeader64` and `MasterStateHash*` from the master preimage, stored-lane assignment/comparison, Unity.Mathematics `uint2` hash lane assembly, and the Python master preimage length.

Rejected Alternatives: Relying on `BinaryLayoutManifest` alone was rejected because layout correctness does not prove hash preimage correctness. Adding a broad C# parser dependency was rejected because this guard must run on clean machines with standard Python only.

Scalability potential: No runtime impact. All tiers retain the same V10 save ABI while the offline guard catches future circular-hash drift.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 18

Problem: The guard proved the header overload forwarding order, but a future edit could still reorder byte writes inside `BuildMasterPreimage` while preserving the public overload signature.

Solution: Added a parser for the C# byte-writer sequence and locked it to the canonical 15-step order: domain, header prefix fields, `HashPayload64`, `WorldSeed`, and `SectorHash`.

Rejected Alternatives: Trusting method parameter order was rejected because byte writers are the actual ABI. Replacing the C# writer with reflection or struct dumps was rejected because this path must stay explicit, little-endian, and stack-only.

Scalability potential: No runtime impact. The same V10 ABI is preserved across Low/Middle/High/Ultra, with stronger offline drift detection.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 19

Problem: A mandatory re-extraction check found that `Docs/Tasks/CURRENT_BATCH.md` no longer contains `SAVE_HASH_CRYPTOGRAPHER`; the file has been replaced by a different batch while this task is already in progress.

Solution: Preserved the exact removed XML directive inside `Docs/Tasks/Status_SAVE_HASH_CRYPTOGRAPHER.md` and recorded the latest `PROMPT_NOT_FOUND` result as batch churn.

Rejected Alternatives: Reverting `CURRENT_BATCH.md` was rejected because it is outside my owned changes and appears to belong to another active batch. Continuing without a local prompt snapshot was rejected because context compression would erase the assignment trail.

Scalability potential: No runtime impact. This protects task continuity only.

Hardware Impact: 0 us frame impact.

## Decision 20

Problem: The C# parity guard proved the `BuildMasterPreimage` field write order, but not the cursor advance schedule. A wrong `cursor += N` would still change the hash preimage while using the same field names.

Solution: Added static extraction of the C# cursor/write operation stream, locked the expected 26 operations, and verified the final written byte end is exactly `80`.

Rejected Alternatives: Trusting field order alone was rejected because binary ABI correctness depends on offsets, not names. Adding runtime reflection was rejected because the guard must remain offline and dependency-free.

Scalability potential: No runtime impact. This only hardens cross-platform save ABI evidence.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 21

Problem: The bit-shuffle mask path still depended on manual review for the exact C# byte order of `WorldSeed`, `SectorHash`, and `maskLo`.

Solution: Added static extraction of the `DeriveShuffleMask` byte operation stream, locked 12 operations, and verified the low/high mask preimages end at bytes `36` and `44`.

Rejected Alternatives: Trusting the domain string check was rejected because matching domains do not prove lane payload order. Executing C# in a fake standalone harness was rejected because Unity.Mathematics references are unavailable in this shell.

Scalability potential: No runtime impact. This strengthens the same Low/Middle/High/Ultra save ABI and prevents Python/Burst shuffle drift.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 22

Problem: The Python oracle self-test validated one shuffle/inverse vector but did not explicitly pin 128-bit rotate edge shifts around `0`, `64`, and `127`.

Solution: Added deterministic rotate vectors for shifts `0/1/63/64/65/127` plus inverse `rotr128(rotl128(x, shift), shift)` checks.

Rejected Alternatives: Trusting the shuffle vector alone was rejected because the derived rotation might not hit edge cases. Adding random-only rotate fuzz was rejected because fixed vectors give stable CI evidence.

Scalability potential: No runtime impact. This hardens offline replay math for all save tiers.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 23

Problem: Python now pins rotate edge vectors, but the C# parity guard still did not lock the `Rotl128` and `Rotr128` branch formulas.

Solution: Added static formula checks for C# rotate branches: `shift == 0`, `shift == 64`, `<64`, and `>64` lane-swap formulas for both left and right rotation.

Rejected Alternatives: Trusting the Python rotate vectors alone was rejected because the C# implementation could drift independently. Compiling a standalone Unity.Mathematics harness remains blocked by missing Unity/project references.

Scalability potential: No runtime impact. This hardens the Burst-side bit-shuffle contract for all save tiers.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 24

Problem: Full Unity/C# verification is still requested by policy, but the local workspace cannot reach source compilation: `dotnet` and Unity are unavailable, and MSBuild fails before compilation because `.slnx` references missing Unity-generated `.csproj` files.

Solution: Re-ran external tooling probes with longer timeouts and recorded the exact failure boundary. Kept verification status as `PENDING UNITY VERIFICATION` instead of claiming a green compile.

Rejected Alternatives: Generating fake `.csproj` files was rejected because that would mutate Unity-generated project artifacts and create false confidence. Reporting the static guard as equivalent to Unity import was rejected because Unity assembly references, unsafe settings, and IL2CPP behavior remain unverified.

Scalability potential: No runtime impact. This is verification boundary reporting only.

Hardware Impact: 0 us frame impact.

## Decision 25

Problem: The C# parity guard proved preimage order, cursor offsets, shuffle byte operations, and rotate formulas, but it still trusted the primitive little-endian writer helpers by manual review.

Solution: Added exact static body checks for `WriteU16`, `WriteU32`, and `WriteU64`, locking byte indexes and shift counts for little-endian serialization.

Rejected Alternatives: Trusting call-site order was rejected because a single primitive writer drift would corrupt every preimage while preserving field names and cursor offsets. Replacing the helpers with `BitConverter` or native struct copies was rejected because it reintroduces platform-endian and allocation risk.

Scalability potential: Low/Middle/High/Ultra retain the same save ABI. The offline guard prevents cross-platform drift before Unity import or IL2CPP testing.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 26

Problem: Negative `WorldSeed` or `AUP.SectorHash` values rely on two's-complement lane packing. The implementation masked them correctly by inspection, but the Python oracle did not freeze signed edge-case bytes in self-test.

Solution: Added fixed self-test vectors for `0`, `1`, `-1`, `-987654321`, `long.MinValue`, and `long.MaxValue`, plus a 128-bit little-endian lane hex roundtrip check.

Rejected Alternatives: Trusting Python `struct.pack` and C# `unchecked((ulong)...)` by inspection was rejected because signed-lane drift would only appear on negative sectors or seeds. Adding a managed C# executable harness was rejected because Unity/project references remain unavailable in this shell.

Scalability potential: No runtime impact. All quality tiers retain the same V10 save ABI while the offline oracle catches signed packing drift.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 27

Problem: The V10 helper uses its own `Hash64` wrapper over `Unity.Mathematics.xxHash3.Hash64`. Existing save storage also has a full-lane `Hash64` helper plus a separate low-lane `Hash32` helper. Future accidental reuse of `.x` would downgrade the V10 master hash.

Solution: Extended the static guard to validate both `SaveMasterHashV10.Hash64` and existing `SaveBinaryStorage.Hash64` assemble `((ulong)hash.y << 32) | hash.x`.

Rejected Alternatives: Trusting method names was rejected because `Hash32` intentionally uses `.x`, and a reviewer could confuse the two conventions. Refactoring the helpers into a shared public API was rejected during this batch because changing the save API surface would require Unity import/play/load verification.

Scalability potential: No runtime impact. All quality tiers retain the same full 64-bit hash convention.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 28

Problem: The C# parity guard only counted `stackalloc byte[` occurrences. That would not catch an undersized stack buffer or an extra accidental stack buffer that changes the helper's memory contract.

Solution: Replaced the loose count with exact declaration checks for `byte* preimage = stackalloc byte[MasterHiHashBytes];` and `byte* buffer = stackalloc byte[ShuffleMaskHiBytes];`.

Rejected Alternatives: Keeping a count-only guard was rejected because it proves allocation style but not buffer capacity. Heap arrays were already forbidden and remain rejected because this path must stay allocation-free.

Scalability potential: No runtime impact. All tiers retain the same stack-only cold save/load helper.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 29

Problem: `ReplayHasher.py self-test` pinned only one full `MasterStateHash` vector. That covered the normal mixed case but not zero metadata, all-max unsigned fields, or signed lane extremes.

Solution: Replaced the single master vector with four frozen fixtures: mixed signed sector, zero metadata, max signed edges, and opposite signed lanes.

Rejected Alternatives: Random-only master fuzz was rejected because deterministic CI evidence must expose the exact failing fixture. Depending on external `xxhash` for every self-test was rejected because the oracle must run on clean machines.

Scalability potential: No runtime impact. All quality tiers retain the same V10 master hash ABI while offline regression coverage improves.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 30

Problem: The V10 helper is intentionally isolated until Unity import/load verification exists. A future edit could accidentally expose `SaveFileHeaderV10` or `SaveMasterHashV10` as public API during the batch.

Solution: Extended the static guard to require the V10 result, header, and helper declarations remain `internal`, and to reject public declarations for the header/helper.

Rejected Alternatives: Making the helper public now was rejected because active save ABI promotion needs Unity import, load, migration, and player save verification. Trusting code review alone was rejected because API drift is easy in parallel-agent work.

Scalability potential: No runtime impact. All tiers keep the same isolated implementation surface until integration is verified.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 31

Problem: The helper being internal is not enough. `SaveBinaryStorage` could still accidentally start using V10 or bump the active header version without Unity import/load verification.

Solution: Extended the static guard to require `SaveBinaryStorage.CurrentVersion = 0x0009`, `CurrentHeaderSize = 56`, and no `SaveMasterHashV10`/`SaveFileHeaderV10` references in the active writer.

Rejected Alternatives: Integrating V10 into `SaveBinaryStorage` now was rejected because the local environment cannot run Unity import, migration, load, backup recovery, or IL2CPP checks. Trusting the status note was rejected because parallel edits can bypass prose.

Scalability potential: No runtime impact. All tiers keep the current verified v9 writer while V10 remains staged for a future integration batch.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 32

Problem: The strongest external `xxhash` reference comparison existed as a one-off historical command, not as a reusable project tool. That weakens future verification after context loss.

Solution: Added `Tools/Security/VerifyReplayHasherReference.py`, a separate optional verifier that imports `xxhash` only from an explicit temporary path and compares 338 XXH3 cases plus 128 shuffle inverse cases.

Rejected Alternatives: Adding `xxhash` as a normal dependency was rejected because `ReplayHasher.py` must remain clean-machine and dependency-free. Leaving the old log-only comparison was rejected because future agents need a rerunnable command.

Scalability potential: No runtime impact. All tiers retain the same save ABI; offline regression proof is easier to reproduce.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 33

Problem: The reusable reference verifier existed after Decision 32, but the stable design doc did not show the command to install `xxhash` into a temporary path and run the verifier.

Solution: Added the optional install and `VerifyReplayHasherReference.py` invocation to `Docs/Design/Save_Binary_Header.md` beside the existing replay-hasher commands.

Rejected Alternatives: Leaving usage only in `LOG_SAVE_HASH_CRYPTOGRAPHER.md` was rejected because logs are append-only evidence, not the stable command surface. Adding `xxhash` as a project dependency remains rejected.

Scalability potential: No runtime impact. Future verification can be reproduced without changing save ABI or runtime dependencies.

Hardware Impact: 0 us frame impact. Documentation only.

## Decision 34

Problem: Layout sentinels verified `SaveMasterHashV10Result` field offsets, but not constructor assignment order. A swapped constructor assignment would preserve binary layout while corrupting plain/stored lane semantics.

Solution: Extended the static guard to validate `SaveMasterHashV10Result` constructor assignments and the raw `Compute` overload's `new SaveMasterHashV10Result(plainLo, plainHi, storedLo, storedHi)` call.

Rejected Alternatives: Trusting constructor review was rejected because lane swaps are visually subtle and catastrophic for save validation. Removing the result constructor was rejected because it is simple, explicit, and compile-time friendly.

Scalability potential: No runtime impact. All tiers retain the same V10 result ABI with stronger offline drift detection.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 35

Problem: Unity's `BinaryLayoutManifest` would catch a missing `[BinaryBlittableSafe]` attribute at runtime, but the offline parity guard did not. That left attribute drift invisible while Unity import is unavailable.

Solution: Extended the static guard to require `[BinaryBlittableSafe]` directly before the packed `StructLayout` declarations for `SaveMasterHashV10Result` and `SaveFileHeaderV10`.

Rejected Alternatives: Relying only on Unity cold-boot validation was rejected because Unity is unavailable in this shell. Duplicating runtime layout logic was rejected; the guard only verifies the required attributes and layout declarations.

Scalability potential: No runtime impact. All tiers retain the same blit-safe V10 header/result contract.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 36

Problem: Master hash vectors prove the final output, but they do not make byte-order failures easy to diagnose. The raw 80-byte master preimage was not frozen inside `ReplayHasher.py self-test`.

Solution: Added the exact mixed-case master preimage hex and length check to the Python self-test before the shuffle and master vector checks.

Rejected Alternatives: Relying only on final hash mismatch was rejected because it forces future agents to reverse-engineer which field byte order drifted. Adding C# execution was still blocked by missing Unity/project references.

Scalability potential: No runtime impact. All tiers retain the same master hash ABI with clearer offline diagnostics.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 37

Problem: Shuffle-mask output was frozen, but the low/high mask preimage bytes were not. A byte-order failure would only show as a hash mismatch without identifying which mask input lane drifted.

Solution: Added exact low and high shuffle-mask preimage hex checks to `ReplayHasher.py self-test`, including byte lengths `36` and `44`.

Rejected Alternatives: Relying only on final shuffle-mask lanes was rejected for the same diagnostic reason as the master preimage. Adding C# execution remains blocked by missing Unity/project references.

Scalability potential: No runtime impact. All tiers retain the same bit-shuffle ABI with clearer offline diagnostics.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 38

Problem: `Tools/Security/VerifyReplayHasherReference.py` documented an explicit temporary `xxhash` path, but the CLI still allowed fallback to a globally installed module. That makes the proof depend on developer machine state and weakens reproducibility.

Solution: Made `--xxhash-path` mandatory, inserted only that resolved directory into `sys.path`, and added a containment check that rejects an imported `xxhash` module whose `__file__` is outside the requested temp directory.

Rejected Alternatives: Relying on global package discovery was rejected because it hides contamination from the host Python environment. Vendoring `xxhash` into the repo was rejected because `ReplayHasher.py` must remain dependency-free and the external package is only an optional oracle.

Scalability potential: No runtime impact. All tiers retain the same save ABI; the offline reference proof is now machine-state independent.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 39

Problem: The path-containment check assumed any imported `xxhash` module exposes `__file__`. The official wheel does, but a contaminated module object without `__file__` would produce an uncontrolled exception path.

Solution: Added an explicit `getattr(..., "__file__", None)` guard and return a controlled verification failure when the module path cannot be proven.

Rejected Alternatives: Ignoring the edge case was rejected because this tool exists to detect environment contamination. Swallowing all exceptions was rejected because it would hide real path-resolution bugs.

Scalability potential: No runtime impact. All tiers retain the same save ABI; external-reference failure modes are now deterministic.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 40

Problem: `importlib.import_module("xxhash")` reuses `sys.modules["xxhash"]` if the verifier is called from an already-running Python process. That leaves a cached host module path able to bypass the explicit path insertion until the containment check catches it, and it makes embedded use less deterministic.

Solution: Evict `sys.modules["xxhash"]` immediately after inserting the requested `--xxhash-path` and before importing the reference package.

Rejected Alternatives: Depending on command-line process freshness was rejected because the verifier is a project tool and may be called from another Python harness. Keeping the cached module and relying only on containment rejection was rejected because the correct behavior is to load the requested isolated module.

Scalability potential: No runtime impact. All tiers retain the same save ABI; embedded verifier calls are now isolated from prior Python process state.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 41

Problem: After loading the isolated `xxhash` module, embedded verifier calls left `sys.path` and `sys.modules["xxhash"]` mutated in the host Python process. That is harmless for a one-shot CLI process but unacceptable for a reusable verification tool.

Solution: Saved the previous `xxhash` module state, inserted the requested path only for the import/verification window, and restored both `sys.path` and `sys.modules["xxhash"]` in a `finally` block on success and failure paths.

Rejected Alternatives: Leaving process-state cleanup to callers was rejected because future verification harnesses should not need to know this tool's internal import mechanics. Removing the isolated import entirely was rejected because the external reference proof must stay independent of globally installed packages.

Scalability potential: No runtime impact. All tiers retain the same save ABI; repeated embedded verifier calls no longer contaminate later tooling.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 42

Problem: `Status_SAVE_HASH_CRYPTOGRAPHER.md` still stated that the rationale trail was ordered through Decisions `1-37`, while the actual rationale file now contains Decisions `1-41`. The latest-suite line also included an older standalone sys.modules guard label that has been subsumed by the embedded cleanup success/failure probes.

Solution: Corrected the status evidence text to Decisions `1-41` and aligned the latest-suite labels with the current verifier cleanup checks.

Rejected Alternatives: Leaving stale evidence text was rejected because the status file is long-term memory for this task. Rewriting old append-only logs was rejected; the correction belongs in current status and a new log entry.

Scalability potential: No runtime impact. Evidence hygiene only.

Hardware Impact: 0 us frame impact. Documentation correction only.

## Decision 43

Problem: Restoring only `sys.modules["xxhash"]` does not remove helper modules that an isolated temp-path `xxhash` package might import. A one-shot CLI exits, but an embedded Python harness could keep those helper modules loaded after verification.

Solution: Added cleanup for any newly loaded module whose `__file__` resolves under `--xxhash-path`, while preserving modules that existed before verifier execution.

Rejected Alternatives: Removing every new module was rejected because that could delete unrelated modules loaded concurrently by a harness. Leaving helper modules in place was rejected because this verifier exists specifically to avoid environment contamination.

Scalability potential: No runtime impact. All tiers retain the same save ABI; embedded verifier calls now clean up temp package helper modules.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 44

Problem: A temp-path module named `xxhash` could satisfy path containment but still lack the `xxh3_64_intdigest` API used by the verifier. That produced an uncontrolled `AttributeError` traceback instead of a deterministic verifier failure.

Solution: Added `verify_module_api()` to require a callable `xxh3_64_intdigest` immediately after path containment and before any vector checks.

Rejected Alternatives: Letting `verify_xxh3()` fail naturally was rejected because tool failures must identify contamination clearly. Expanding the project dependency surface was rejected because the external package remains optional and isolated.

Scalability potential: No runtime impact. All tiers retain the same save ABI; offline reference failure modes are clearer.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 45

Problem: A callable `xxh3_64_intdigest` can still return a bad value. Non-integer or out-of-range reference digests would either produce noisy formatting failures or compare against invalid data.

Solution: Added explicit digest type and `0..0xffffffffffffffff` range checks before comparing against `ReplayHasher.py`.

Rejected Alternatives: Allowing Python formatting/comparison to fail later was rejected because the verifier should identify reference contamination precisely. Masking the reference value was rejected because that could hide a bad third-party or fake module.

Scalability potential: No runtime impact. All tiers retain the same save ABI; offline reference validation is stricter.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 46

Problem: The external reference digest was type/range checked, but the local `ReplayHasher.py` result was not. If the oracle regressed to a non-integer or out-of-range value, the verifier could fail later through formatting instead of reporting the bad replay digest directly.

Solution: Replaced duplicated reference checks with `require_u64_digest()` and applied it to both the external reference digest and the `ReplayHasher.py` digest.

Rejected Alternatives: Trusting `ReplayHasher.py` because its self-test currently passes was rejected; the verifier is meant to diagnose future regressions. Masking the replay result was rejected because that would hide an oracle bug.

Scalability potential: No runtime impact. All tiers retain the same save ABI; offline comparison diagnostics are stricter.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 47

Problem: Real vector mismatches in `VerifyReplayHasherReference.py` still surfaced as uncaught `AssertionError` tracebacks from CLI execution. The mismatch text was precise, but the command output was noisy and less suitable for CI parsing.

Solution: Wrapped `verify_xxh3()` and `verify_shuffle_inverse()` in `main()` with an `AssertionError` catch that prints the exact assertion message and returns exit code `1`, while preserving import-state cleanup in `finally`.

Rejected Alternatives: Leaving raw tracebacks was rejected because verification tools should report failure facts without stack noise. Returning code `2` was rejected because `2` already represents usage/environment errors in this verifier.

Scalability potential: No runtime impact. All tiers retain the same save ABI; CLI verification failure output is cleaner.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 48

Problem: The verifier validated XXH3 digest shape, but the 128-bit shuffle path still assumed `shuffle_hash128` and `unshuffle_hash128` returned two unsigned 64-bit lanes. A malformed tuple, list, string, or out-of-range lane would fail later through indexing or inverse comparison noise.

Solution: Added `require_u64_lane()` and `require_u64_pair()` to reject malformed shuffle/unshuffle results before indexing or comparing the recovered lanes.

Rejected Alternatives: Letting tuple unpacking or indexing fail naturally was rejected because verifier output must identify the exact ABI contract violation. Masking lanes with `MASK64` was rejected because it would hide an invalid replay implementation.

Scalability potential: No runtime impact. Low/Middle/High/Ultra retain the same V10 save ABI; the offline verifier now rejects malformed 128-bit lane contracts deterministically.

Hardware Impact: 0 us frame impact. Offline validation only.

## Decision 49

Problem: Python `bool` is a subclass of `int`, so `isinstance(value, int)` accepted `True` or `False` as a valid digest or 64-bit lane. That weakens the verifier ABI contract because a fake module could return booleans and pass the type gate.

Solution: Changed digest and lane validation to require `type(value) is int`, rejecting bool while preserving normal unbounded Python integer support plus the explicit unsigned 64-bit range check.

Rejected Alternatives: Keeping `isinstance` was rejected because it silently accepts invalid boolean values. Converting values with `int(value)` was rejected because it would normalize a bad implementation instead of exposing it.

Scalability potential: No runtime impact. Low/Middle/High/Ultra retain the same V10 save ABI; the offline verifier now enforces exact scalar lane types.

Hardware Impact: 0 us frame impact. Offline validation only.
