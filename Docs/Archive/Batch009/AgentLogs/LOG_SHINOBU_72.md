# LOG_SHINOBU_72

## 2026-05-19 Session Start

What was wrong: Assignment targets autosave stalls from bloated state serialization on slow MicroSD and requires RLE/XXHash3 WAL architecture.
What was done: Prompt extracted from CURRENT_BATCH.md, domain classified as ECHELON 1 save data archivist, status/rationale/log files initialized.
Cinematic Cheats used: State dehydration instead of per-frame dynamic truth; cosmetic-save deprioritization planned under low GlobalQualityWeight.
Exact Microseconds saved: Pending measurement. Initial estimate target is <1000 us hash/delta pass for bounded mock vault leaves.

## 2026-05-19 Static Completion Pass

What was wrong: Merkle/WAL save path already existed, but the integration surface needed literal SHINOBU_72 compliance: exact `SectorEntryDTO` manifest coverage, RLE-recognized WAL sub-blocks, proof that JSON/text serialization is outside the save loop, and persistent audit files.
What was done: Updated `BinaryLayoutManifest` to assert `SectorEntryDTO` 32B ARM64 layout; verified existing XXHash3-128 unmanaged hashing, sparse delta extraction, MMF WAL append, AUP local offset quantization, Dear Lie dehydration, tombstone pruning, mod sidecar CRC isolation, 300-frame telemetry dump, CSV override parser, and State Delta X-Ray facade. Existing RLE-first sub-block path is present with `Lz4BlockFlagRle`; no duplicate compressor was added.
Cinematic Cheats used: Stable rest / `NeedsWake` state persistence instead of frame-perfect physics truth; `EcosystemSectorDTO` dehydration instead of per-fish state; continuous `GlobalQualityWeight` cosmetic deprioritization instead of binary quality switches.
Exact Microseconds saved: Static estimate only, no Unity profiler proof. Expected savings by mechanism: JSON/text removal 400-1200 us per delta batch; unchanged-leaf hash abort 500-1500 us; sparse delta extraction 600-2000 us on small edits; RLE repetitive batch I/O-equivalent 150-800 us; uninitialized native buffers 200-900 us. Compile/profiler verification blocked by CPU gate: first CPU=100 with no compiler process, retry CPU=100 with `csc.exe` PID 15540 active.
Verification: Prompt re-read after task markers 3/6/9/12/15/18, bytes=9282, sha256=da9e1712f50b61c4c870489409701af91ff80c3deb565cf668e0a690eefa8a6e. Static scans found no `JsonUtility`, `System.Text.Json`, or `BinaryFormatter` in SaveSystem WAL/Merkle path. `git diff --check` reported no whitespace errors beyond line-ending warnings.

## 2026-05-19 Ultra Polish Re-Audit

What was wrong: The previous pass underreported two technical risks: RLE storage flag used a low bit that overlaps leaf flag semantics, and WAL headers used wall-clock ticks in state-bearing record CRCs. Compile-wall risk also exists because SaveSystem files live under the broad `Hecton8.Core.asmdef`.
What was done: Moved `Lz4BlockFlagRle` to bit 8, retained bit-3 legacy sub-block validation, removed UTC ticks from Merkle WAL header default and pager WAL sequence, and rewrote `SelfAudit_SHINOBU_72.xml` as a forensic audit rather than a five-line checklist.
Cinematic Cheats used: Save stable rest / `NeedsWake` instead of dynamic motion truth; save dehydrated sector DTOs instead of per-entity cosmetic state; use continuous `GlobalQualityWeight` to drop cosmetic WAL bytes under pressure.
Exact Microseconds saved: Still static only. RLE flag repair saves no CPU directly; it prevents false rollback/drop behavior. Removing wall-clock from WAL avoids nondeterministic CRC divergence, not a frame-time optimization. No `dotnet build` was launched per user instruction.
Verification: Static scan after patch found no `JsonUtility`, `System.Text.Json`, `BinaryFormatter`, `Pack=1`, `{ get; set; }`, or managed `byte[]` in the SHINOBU_72 SaveSystem files. Runtime/Unity profiler proof remains pending.

## 2026-05-19 Ultra Polish Dependency Pass

What was wrong: The deterministic WAL patch still had one avoidable compile-risk: `math.max(0L, timestampTicks)`. The compile-wall diagnosis also needed concrete dependency facts, not a vague "broad Core" note.
What was done: Replaced the `long` clamp with `timestampTicks < 0L ? 0L : timestampTicks`. Re-read the SHINOBU_72 prompt from `CURRENT_BATCH.md` with an attribute-tolerant CLI regex: bytes=9282, lines=56, tasks=20, sha256=da9e1712f50b61c4c870489409701af91ff80c3deb565cf668e0a690eefa8a6e. Audited asmdef extraction and recorded the blocker: pager files are namespace `Hecton8.Core.Persistence.Paging` but physically compiled inside `Hecton8.Core`; moving them into the existing paging asmdef currently creates a Core/Paging cycle because the pager uses `HectonPersistentPathPolicy` and `GlobalRegistry` from Core while Core already references Paging.
Cinematic Cheats used: No new runtime cheat added in this pass. Existing cheat remains state dehydration: stable rest / local AUP / `NeedsWake` rather than frame-perfect dynamic history.
Exact Microseconds saved: 0 us claimed for the clamp. Compile-risk removed. Compile-wall runtime impact is 0 us; developer iteration impact remains until the Integrator/Core owner extracts path policy or registry access into a narrow contract.
Verification: `SelfAudit_SHINOBU_72.xml` parses as XML. `git diff --check` reports no whitespace errors, only line-ending warnings. Static scans found no forbidden serializers, `Pack=1`, hot DTO properties, managed `byte[]`, `Time.deltaTime`, or `UnityEngine.Random` in the SHINOBU_72 save files. `dotnet build` was not launched per user instruction.

## 2026-05-19 Endianness Hardening Pass

What was wrong: Merkle WAL record headers and LZ4/RLE sub-block headers were still written as raw host struct memory. That is fine on current little-endian PC/ARM targets, but it is not a real file-format contract and violates the binary serialization endianness mandate.
What was done: Added explicit little-endian write/read helpers for `SaveMerkleWalAppendHeader` and `Lz4SubBlockHeader`. `TryAppendCompressedWalMmf()` now builds a stackalloc 64-byte canonical header, computes CRC over those exact bytes, writes those bytes through MMF/FileStream, and never writes `&header` directly. `TryValidateWalAndRollback()` and `TryValidateStoredSubBlocks()` read stackalloc header bytes once per loop scope and hydrate fields explicitly. `Lz4SubBlockCompressionJob` now writes sub-block headers into the compressed byte arena via explicit little-endian byte stores instead of `UnsafeUtility.MemCpy(&header)`.
Cinematic Cheats used: No new presentation cheat. This pass protects the existing save-cheat architecture: dehydrated local AUP + `NeedsWake`, not frame-perfect dynamic state.
Exact Microseconds saved: 0 us claimed. Added worker-side byte stores: 64 bytes per WAL record and 32 bytes per sub-block. Main-thread impact remains 0 us by design; correctness gain is deterministic WAL bytes independent of host struct endianness.
Verification: Static grep confirms no remaining raw WAL header writes through `&header` in `SaveStateMerkleTree.cs`. Build/profiler not run per user instruction.

## 2026-05-19 Rollback Burst Determinism Pass

What was wrong: SHINOBU_72 SaveSystem Burst jobs still used `FloatMode.Fast`. Persistence is rollback-sensitive, and `DearLieDehydrationJob` writes quantized local AUP state that becomes save truth.
What was done: Changed all Burst jobs under `Assets/_Project/Scripts/SaveSystem` from `FloatMode.Fast` to `FloatMode.Deterministic`, keeping `CompileSynchronously = true` and `FloatPrecision.Standard`.
Cinematic Cheats used: Existing Dear Lie remains unchanged: stable/dehydrated state over frame-perfect simulation history.
Exact Microseconds saved: 0 us. Potential worker-side ALU cost accepted for deterministic state bytes across x86/ARM64. Main-thread save path remains job-scheduled.
Verification: Static grep reports no `FloatMode.Fast` in `Assets/_Project/Scripts/SaveSystem`. Build/profiler not run per user instruction.

## 2026-05-19 Delta Record Endianness Pass

What was wrong: `StateDeltaRecordDTO` was aligned and blittable, but its header bytes inside the WAL delta arena were still emitted with raw host-memory struct copies. That leaves the delta payload weaker than the WAL record and sub-block headers.
What was done: Added explicit little-endian read/write helpers for `StateDeltaRecordDTO`. `MerkleChangedLeafExtractionJob` now writes canonical 64B delta headers into `DeltaBytes`; `CosmeticDeltaPayloadPruneJob` reads canonical headers and rewrites the adjusted `DeltaPayloadOffset` canonically. `BinaryLayoutManifest` now asserts every field offset for `StateDeltaRecordDTO`, `Lz4SubBlockHeader`, and `SaveMerkleWalAppendHeader`.
Cinematic Cheats used: No new simulation cheat. This protects the existing Dear Lie payload: local AUP plus stable/wake flags instead of frame-perfect dynamic history.
Exact Microseconds saved: 0 us claimed. Added fixed worker-side 64B stores/reads per changed leaf header; main-thread impact remains 0 us. The gain is deterministic WAL delta bytes independent of host endianness.
Verification: Static grep confirms no remaining `MemCpy(deltaPtr + byteCursor, &record)`, `ReadArrayElement<StateDeltaRecordDTO>`, or raw `WriteArrayElement` delta header writes in `SaveStateMerkleTree.cs`. Build/profiler not run per user instruction.

## 2026-05-19 Emergency Header Endianness Pass

What was wrong: `GenerateEmergencyMockHeader()` still wrote `SaveMerkleEmergencyHeader64` from host struct memory. This is a cold fallback path, but it is still a binary header used when legacy V8 archaeology is absent.
What was done: Added `WriteEmergencyHeaderLittleEndian()`. The emergency header now writes canonical 64B bytes, computes CRC over those bytes with checksum zero, rewrites checksum, and copies canonical bytes to the destination buffer. `BinaryLayoutManifest` now asserts every emergency header offset.
Cinematic Cheats used: None added. This pass preserves the fallback binary evidence contract rather than adding a second legacy-header model.
Exact Microseconds saved: 0 us; cold path only. Benefit is deterministic fallback bytes for recovery/bootstrap across architectures.
Verification: Static grep confirms no remaining `ComputeCrc32((byte*)&header)` or `MemCpy(destination, &header)` in `SaveStateMerkleTree.cs`. Build/profiler not run per user instruction.

## 2026-05-19 WAL Replay Decode Pass

What was wrong: The Merkle WAL path could validate record and sub-block CRCs, but it did not expose a zero-GC helper that decodes stored raw/RLE/LZ4 sub-blocks back into a delta arena. That left Task 15 stronger on corruption detection than on replay recovery.
What was done: Added `TryReplayWalToDeltaArena()`. It reads canonical WAL headers, verifies record CRC, rejects overlapping destination/scratch buffers, decodes raw/RLE/LZ4 sub-blocks into caller-owned `NativeArray<byte>` destination storage, uses caller-owned compressed scratch, and updates native counters. Corrupt mod WAL records are skipped; corrupt core records fail. No `byte[]` staging and no duplicate WAL format were added.
Cinematic Cheats used: No new presentation cheat. This protects the existing save cheat by rehydrating compact local-AUP/dehydrated delta bytes instead of replaying full frame histories.
Exact Microseconds saved: 0 us claimed; cold recovery path. Expected MicroSD behavior is sequential read/decode into native buffers instead of managed expansion or random I/O.
Verification: Static grep confirms the replay helper has no `byte[]`, no forbidden serializers, and no `JobHandle.Complete()`. Build/profiler not run per user instruction.

## 2026-05-19 Narrow Compile Attempt

What was wrong: Static verification could not prove the new replay helper compiles. CPU/compiler gate passed: `CPU=11`, no active `dotnet/csc` listed.
What was done: Ran one narrow compile: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`.
Cinematic Cheats used: None. This was verification only.
Exact Microseconds saved: 0 us. The build took ~68 seconds.
Verification: Build failed outside SHINOBU_72 files. Errors: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs(1363,37)` missing `math.reversebytes`; `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs(1977,58)` unassigned `sanitizedWeight`; `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs(476,81)` and `(589,21)` missing `IndustrialLoreBitMask`; `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs(402,17)` and `HectonScooterVolumetricShaftsFeature.cs(935,17)` missing `HectonDrsRenderFeatureGate`. No SHINOBU_72 file appeared in compiler errors. Per 3-strikes protocol, this is marked blocked by external dependency rather than patched across domains.
