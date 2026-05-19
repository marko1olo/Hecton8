# Rationale_SHINOBU_72

State: PENDING VERIFICATION

## Decision 00: Work Boundary

Problem: Save compression touches shared persistence code while other agents may be active.
Solution: Limit edits to SaveSystem/Core Memory/Editor facade files and required SHINOBU_72 logs.
Rejected Alternatives: Broad SaveManager rewrite; too much regression surface and unnecessary dependency on inventory/construction agents.
Scalability potential: Low devices avoid cosmetic I/O; middle devices process critical plus sparse cosmetic leaves; high and ultra devices keep richer payloads without blocking the main thread.
Hardware Impact: Target gain on i3/MX350/MicroSD is hitch reduction by moving raw save writes into sparse deltas and throttled WAL writes.

## Decision 01: No Duplicate WAL System

Problem: Prompt requests RLE + XXHash3 WAL saves while existing `H8BinaryWorldPager` and `SaveStateMerkleTree` already implement WAL, MMF, Merkle hashing, telemetry, and editor inspection.
Solution: Patch the existing Merkle sub-block job and binary manifest in place. Keep pager RLE path intact. Use existing `GlobalDataVault` buffers and WAL validation.
Rejected Alternatives: New `RleWalCompressor` service, second WAL file, or parallel save manager; all would create split recovery semantics and higher integration risk with 20+ agents.
Scalability potential: Low devices get RLE wins on empty/repeated payloads; middle devices fall back to LZ4 for mixed payloads; high devices keep richer cosmetic payloads through continuous thresholds; ultra devices spend saved I/O budget on denser state persistence instead of stalling.
Hardware Impact: On i3/MX350 with MicroSD, expected gain is reduced worker write bytes for repetitive inventories/voxels. CPU cost is one linear worker-side scan per sub-block; no main-thread dependency added.

## Decision 02: Literal ARM64 SectorEntryDTO

Problem: Prompt required exact `SectorEntryDTO` field names/types, but the Merkle code had an internal `SectorEntry` with signed `long` fields.
Solution: Rename only the Merkle directory DTO to `SectorEntryDTO`, change the two 8-byte fields to `ulong`, and update `BinaryLayoutManifest` plus emergency header size.
Rejected Alternatives: Leave "meaning-equivalent" signed fields; violates literal prompt and can hide directory sign bugs. Changing `SaveBinaryStorage.SectorEntry`; rejected because that is a separate V10 storage DTO with its own compatibility shim.
Scalability potential: Low/middle devices scan aligned 32B entries without traps; high/ultra devices can scale sector counts without layout ambiguity.
Hardware Impact: Directory scans stay 8-byte aligned for ARM64/Quest-class CPUs. Estimated gain is small per entry, but it removes a hard crash class.

## Decision 03: RLE First, LZ4 Second

Problem: Autosaves on slow MicroSD freeze when raw or weakly compressed repetitive state reaches the WAL. The existing sub-block job was LZ4/raw only.
Solution: Add `Lz4BlockFlagRle` and a Burst-safe RLE pass inside `Lz4SubBlockCompressionJob`; use RLE only when smaller than raw, otherwise use existing LZ4/raw fallback and CRC validation.
Rejected Alternatives: Replace LZ4 with pure RLE; bad for mixed entropy payloads. Add a second NativeList compressor; duplicate memory and recovery semantics.
Scalability potential: Low devices and weak storage benefit from smaller empty-slot/debris payloads; middle devices still get LZ4 for mixed data; high/ultra devices can retain cosmetic sectors because write pressure is lower.
Hardware Impact: i3/MX350/MicroSD expected benefit is fewer synchronous flush bytes on worker WAL commits. Random data pays a bounded linear scan and does not expand the WAL.

## Decision 04: Continuous Cosmetic Drop Instead Of Quality Switch

Problem: Save pressure must drop cosmetics under low `GlobalQualityWeight` without a binary quality mode.
Solution: Keep `ResolveRuntimeConfigForQuality()` as the authority: continuous weight/stress scales cosmetic drop threshold and WAL byte budget; `CosmeticDeltaPayloadPruneJob` drops only cosmetic records above threshold.
Rejected Alternatives: "Low-tier disables cosmetics" branch; creates hysteresis-free pop and violates systemic mandate. Saving all cosmetics; causes MicroSD spikes.
Scalability potential: Low keeps critical inventory/base/player AUP; middle saves critical plus sparse cosmetics; high saves most cosmetics; ultra keeps visual overkill state where budget allows.
Hardware Impact: On low-end silicon, byte pressure is reduced before compression and disk flush. Expected savings vary with cosmetic density; no fake profiler number claimed.

## Decision 05: AUP Local Offset Persistence

Problem: Full AUP `double3` is oversized and unstable for binary diffs across origin shifts.
Solution: Use `SaveAupLocalOffset32` quantization through `QuantizeAupForSave()`: sector key plus finite local `float3`, with stable rest / wake flags in Dear Lie dehydration.
Rejected Alternatives: Save Unity `Transform.position`; presentation-only and origin-relative. Save raw `double3`; larger and less diff-friendly.
Scalability potential: Low devices store compact critical positions; middle/high/ultra can afford more entities because each position payload is smaller.
Hardware Impact: Per-entity spatial payload shrinks by roughly 12-24 bytes depending source representation. Hash pages change less under rebases, reducing delta churn.

## Decision 06: Compile Gate Obeyed

Problem: AGENTS forbids `dotnet build` when CPU load is above 50% or `dotnet/csc` is active.
Solution: Ran CPU/process gate before build. First result: CPU=100, `dotnet/csc`=none. Retry result: CPU=100, `csc.exe` active (PID 15540). Build is deferred until CPU is <=50% and no compiler process is active.
Rejected Alternatives: Build anyway; violates batch law and risks interfering with other agents.
Scalability potential: Parallel agent throughput preserved; no extra contention on compile server.
Hardware Impact: No build heat added while host is saturated.

## Decision 07: RLE Flag Namespace Repair

Problem: `Lz4BlockFlagRle` occupied bit 3, the same semantic slot as `LeafFlagModPayload`. The fields live in different headers today, but the overlap is a bad binary contract and can produce false mod/RLE reasoning during WAL recovery work.
Solution: Move new RLE storage flag to bit 8 and accept bit 3 only as a legacy RLE sub-block read flag. Do not change leaf flags or mod sector prefix.
Rejected Alternatives: Leave the collision because current writer/reader structs are separate; rejected because future recovery code can merge header flags. Rename/rebuild the whole LZ4 header; rejected as churn.
Scalability potential: Low devices keep RLE storage savings; middle/high/ultra keep LZ4/raw fallback. The flag move does not alter payload size.
Hardware Impact: No frame-time gain claimed. This prevents false recovery branches that could cost a full WAL rollback.

## Decision 08: Remove Wall-Clock From WAL Truth

Problem: Merkle WAL header and pager WAL sequence used `DateTime.UtcNow.Ticks`, making otherwise identical state produce different WAL bytes.
Solution: Merkle `BuildWalHeader` now defaults `TimestampTicks` to zero unless caller supplies a value; pager WAL sequence now uses the deterministic frame. Telemetry `TicksUtc` remains because it is for forensic metadata only.
Rejected Alternatives: Keep UTC in WAL record CRC for convenience; rejected for bit-perfect persistence. Delete telemetry timestamps; rejected because post-mortem ordering needs wall-clock evidence.
Scalability potential: All tiers get identical state bytes independent of host clock. Ultra forensic paths still keep UTC in telemetry.
Hardware Impact: CPU impact is negligible; correctness gain is deterministic WAL payloads for replay comparisons.

## Decision 09: Compile Wall Boundary

Problem: `Hecton8.Core.asmdef` currently references many sibling runtime assemblies, so the save files under `Assets/_Project/Scripts/SaveSystem` inherit a broad compile wall.
Solution: Record as a pre-existing architectural risk. SHINOBU_72 added no asmdef reference and did not split assemblies during this pass because moving save files requires coordinated namespace/assembly migration.
Rejected Alternatives: Editing `Hecton8.Core.asmdef` in isolation; rejected because it can break unrelated domains and violate the active multi-agent batch. Hiding the issue in the report; rejected.
Scalability potential: No runtime scaling impact. Iteration-speed risk remains until a dedicated persistence assembly is extracted.
Hardware Impact: No runtime hardware impact. Developer hardware impact remains compile-time only.

## Decision 10: Avoid Unity.Mathematics Long Overload Risk

Problem: `BuildWalHeader()` used `math.max(0L, timestampTicks)` after the deterministic WAL repair. Unity.Mathematics overload availability for `long` is version-sensitive and unnecessary here.
Solution: Replace it with `timestampTicks < 0L ? 0L : timestampTicks`. Same deterministic clamp, no dependency on a math overload.
Rejected Alternatives: Keep the overload and rely on compile to prove it later; rejected because this is a cheap static risk to remove. Cast to int; rejected because WAL timestamps are 64-bit.
Scalability potential: No runtime tier behavior change. All tiers keep deterministic WAL bytes.
Hardware Impact: No measurable frame-time claim. It removes a possible compile failure without adding work.

## Decision 11: Persistence Assembly Extraction Blocker

Problem: `H8BinaryWorldPager` and `H8WalInspector` already declare namespace `Hecton8.Core.Persistence.Paging`, but physically live in `Assets/_Project/Scripts/SaveSystem`, so Unity compiles them inside broad `Hecton8.Core.asmdef`.
Solution: Do not move files or edit asmdefs in this SHINOBU_72 pass. A narrow extraction into existing `Hecton8.Core.Persistence.Paging.asmdef` would require moving path policy / registry access or adding references that currently form a cycle: Core references Paging, while Pager uses `HectonPersistentPathPolicy` and `GlobalRegistry` from Core plus DataVault from Core.Memory.
Rejected Alternatives: Move pager files into `Core/Persistence/Paging` and add `Hecton8.Core` as a reference; rejected because Core already references Paging. Duplicate path policy inside Paging; rejected under DO NOT WRITE DUPLICATES. Move `HectonPersistentPathPolicy` to Contracts; rejected as a public core contract migration outside this domain.
Scalability potential: No runtime save behavior change. Compile-wall cleanup remains a dedicated Integrator/Core task, not a WAL compression task.
Hardware Impact: Runtime hardware impact is zero. Developer hardware impact remains broad recompiles until the assembly graph is corrected.

## Decision 12: Canonical Little-Endian WAL Bytes

Problem: Merkle WAL append and validation used raw struct memory for `SaveMerkleWalAppendHeader` and `Lz4SubBlockHeader`. That is correct on current little-endian targets but violates the binary serialization law because file bytes implicitly follow host endianness.
Solution: Serialize WAL append headers and sub-block headers through explicit little-endian byte writers. Validation now reads stackallocated header bytes and hydrates fields through explicit little-endian readers; record CRC is computed over the exact canonical header bytes with `RecordCrc32` zeroed at offset 56.
Rejected Alternatives: Rely on current x86/ARM64 little-endian hardware; rejected because WAL is an archive format, not a host-memory dump. Use `BinaryPrimitives` in the Burst sub-block job; rejected because the job path must stay pointer/simple-intrinsic friendly.
Scalability potential: Low/middle/high/ultra tiers keep identical WAL bytes. No quality-mode branch was introduced.
Hardware Impact: Runtime cost is 64 bytes of explicit header writes per WAL record and 32 bytes per sub-block, worker-side only. Expected frame impact is 0 us on the main thread; correctness impact is deterministic cross-platform file bytes.

## Decision 13: Deterministic Burst For Rollback-Sensitive Save Jobs

Problem: SaveSystem Burst jobs still used `FloatMode.Fast`. Most jobs are integer/hash jobs, but the domain is rollback-sensitive and `DearLieDehydrationJob` quantizes AUP/local float state that becomes persisted truth.
Solution: Change SHINOBU_72 SaveSystem Burst jobs to `FloatMode.Deterministic` while keeping `CompileSynchronously = true` and `FloatPrecision.Standard`.
Rejected Alternatives: Change only the float-heavy job; rejected because a future edit can add float math to an integer-named save job and silently reintroduce fast-math drift. Leave Fast for speed; rejected for co-op rollback correctness.
Scalability potential: All tiers now serialize the same state bytes for the same input. Quality scaling still happens through `GlobalQualityWeight`, not Burst fast-math.
Hardware Impact: Possible worker-side ALU cost on float-heavy save jobs; main-thread cost remains scheduled. Correctness is prioritized over microscopic worker throughput in persistence.

## Decision 14: Canonical Little-Endian Delta Record Headers

Problem: `StateDeltaRecordDTO` headers inside the delta byte arena were copied as raw host-memory structs. The vault DTO layout is aligned, but WAL payload bytes must be an archive contract, not an x86/ARM little-endian accident.
Solution: Keep `StateDeltaRecordDTO` as the 64B field-only NativeArray DTO for Burst/editor inspection, but write/read the arena header through explicit little-endian helpers. `MerkleChangedLeafExtractionJob` writes canonical bytes; `CosmeticDeltaPayloadPruneJob` reads them canonically and rewrites adjusted payload offsets canonically.
Rejected Alternatives: Leave the raw `UnsafeUtility.MemCpy(&record)` because current hardware is little-endian; rejected because WAL deltas are persistent data. Add a managed serializer; rejected because it violates zero-GC and duplicates the binary contract.
Scalability potential: Low through ultra tiers keep one identical delta format. No quality branch was added; cosmetic pruning remains continuous through `GlobalQualityWeight`.
Hardware Impact: Worker-side cost is 64 fixed byte stores per changed leaf header. Main-thread cost remains 0 us; correctness gain is deterministic delta bytes across host architectures.

## Decision 15: Canonical Emergency Header Bytes

Problem: `GenerateEmergencyMockHeader()` still computed CRC and copied `SaveMerkleEmergencyHeader64` from host struct memory. This is a cold fallback, but it is still a binary header used when legacy archaeology is absent.
Solution: Emit the 64B emergency header through explicit little-endian byte writes, compute CRC over those canonical bytes with `Checksum=0`, then rewrite the checksum and copy the canonical bytes to the caller buffer. Manifest coverage now asserts every emergency header field offset.
Rejected Alternatives: Leave it as raw struct because the path is cold; rejected because fallback headers must be less fragile than normal headers. Add a second emergency-header DTO; rejected under the duplicate-contract ban.
Scalability potential: No tier split. All hardware receives the same fallback header bytes.
Hardware Impact: Cold-path only. Runtime frame impact is 0 us; benefit is deterministic recovery/bootstrap evidence.

## Decision 16: WAL Replay Into Caller-Owned Delta Arena

Problem: Validation proved WAL records were intact, but the Merkle WAL side did not expose a zero-GC recovery path that decodes stored raw/RLE/LZ4 sub-blocks back into a delta arena. A save system that only validates compressed bytes cannot honestly claim replay readiness.
Solution: Add `TryReplayWalToDeltaArena()` as a cold recovery helper. It reads canonical WAL headers, verifies record CRC over canonical bytes, rejects overlapping destination/scratch buffers, decodes sub-blocks into a caller-owned `NativeArray<byte>` delta arena using caller-owned compressed scratch, and records replay counters without allocating `byte[]`. Corrupt mod records are skipped; corrupt core records fail.
Rejected Alternatives: Use the editor window as the replay path; rejected because recovery must be callable from boot/runtime code. Allocate managed payload arrays while replaying; rejected because WAL recovery should not create GC pressure on crash startup. Duplicate the pager WAL reader; rejected under the duplicate WAL law.
Scalability potential: Low devices replay only compact critical/cosmetic-pruned deltas; middle/high/ultra replay the same canonical bytes with larger retained cosmetic payloads as allowed by `GlobalQualityWeight`.
Hardware Impact: Cold recovery path. Main-frame cost is 0 us. On MicroSD, replay is sequential reads into existing native buffers; no random seeks or managed array expansion.

## Decision 17: Compile Attempt Blocked By External Core Errors

Problem: After replay changes, static evidence was not enough to exclude syntax errors. CPU/compiler gate passed (`CPU=11`, no active `dotnet/csc` listed), so a narrow Core compile was justified.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`. It failed in unrelated files outside SHINOBU_72 ownership: `ShinobuFloraFaunaSymbiosisSolver.cs` uses unavailable `math.reversebytes`; `HomeostasisBrain.ScalabilityDictator.cs` has unassigned `sanitizedWeight`; `SaveBinaryPayloadCodec.cs` misses `IndustrialLoreBitMask`; two Visor feature files miss `HectonDrsRenderFeatureGate`.
Rejected Alternatives: Patch AI/Core/Visor/lore errors from this save-domain task; rejected as cross-domain interference. Retry solution-wide build; rejected because the narrow Core build already proves the compile wall is blocked.
Scalability potential: No runtime scaling change. This is a dependency hygiene blocker.
Hardware Impact: One narrow build cost ~68 seconds. No additional build attempts until external errors are cleared or user explicitly redirects.
