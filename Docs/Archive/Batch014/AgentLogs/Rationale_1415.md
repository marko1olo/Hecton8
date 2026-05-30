# Rationale_1415

Status: STATIC RECHECKED / RECHECK11 HEADER-EVENT DTO FIX COMPLETE / CORE TELEMETRY ROWS RESOLVED / BUILD DEFERRED BY RESOURCE THROTTLE / OUT-OF-CORE OVERSIZE SPLIT BACKLOG IDENTIFIED
Evidence class: STATIC_SOURCE until Unity/compiler proof exists.

## Decision 001 - Scope Boundary

Problem: Agent 1415 prompt grants DTO autonomy across telemetry/black-box entries, but domain roster assigns crash telemetry to Echelon 1 and forbids cross-domain edits without justification.
Solution: Treat only structs under `Assets/_Project/Scripts` ending in `TelemetryEntry` or `BlackBoxEntry`, plus directly proven ring element aliases, as in-scope. Any other DTO remains untouched unless scan proves it is a telemetry ring element.
Rejected Alternatives: Broad edits to all structs named `*Entry` would mutate non-telemetry data and risk binary/save layout breakage.
Scalability potential: Low tier gains predictable 64-byte cache lines; middle/high/ultra can add richer telemetry only inside stable 64-byte lanes or separate presentation-only diagnostics.
Hardware Impact: Static expectation only; reducing mixed-size ring strides avoids extra cache-line fetches on i3/MX350. Measured gain absent.

## Decision 002 - Oversize Telemetry Entries

Problem: Static plan found 36 nonconforming telemetry/black-box structs whose existing diagnostic fields exceed 64 bytes when preserved.
Solution: Mark oversize entries as blockers for field-preserving 64-byte conversion and do not delete fields or narrow data types. Convert only entries whose existing payload fits inside 64 bytes.
Rejected Alternatives: Dropping AUP vectors, fault hashes, or diagnostic counters would make crash dumps less useful and violate the prompt's no-data-loss rule.
Scalability potential: Low/middle tiers get cache-line-normalized records where possible; high/ultra oversize diagnostics need a later split into 64-byte header + secondary detail record instead of a forced lossy shrink.
Hardware Impact: Convertible records can avoid 32/40/48-byte ring stride drift. Oversize records remain cache-heavy until split by owner-domain follow-up. Measured gain absent.

## Decision 003 - Build Throttle

Problem: The prompt requires compile confirmation but forbids build launch while CPU exceeds 50% or compiler/dotnet processes run.
Solution: CPU sample returned 100% and an active `dotnet` process was present; compile is blocked, static AST validation continues.
Rejected Alternatives: Launching `dotnet build` under contention would violate coordinator protocol and risk host stalls.
Scalability potential: No runtime effect.
Hardware Impact: Host preservation only; no game-frame claim.

## Decision 004 - Header Fields Were Not Blindly Added

Problem: Task text asks for `SystemID`, `BufferID`, and generation-style standardization, but many existing telemetry rings already route ownership through surrounding vault handles, constants, or owner systems instead of per-entry header fields.
Solution: Preserve payload fields and owner routing; do not inject new identity fields into records unless the owning system already uses that semantic lane. Data sovereignty proof is recorded in `Docs/Reports/TELEMETRY_DATA_SOVEREIGNTY_AUDIT_1415.json`.
Rejected Alternatives: Adding fake identity fields to reach a visual standard would silently displace diagnostic payload, break dump readers, and create false authority routes.
Scalability potential: Low/middle/high/ultra all keep invariant 64-byte DTO stride without changing truth ownership. Future richer diagnostics must use an owner-approved side lane, not hidden field theft.
Hardware Impact: Preserves cache-line normalization without extra writer cost. Measured runtime gain absent.

## Decision 005 - Reflection Editor Validator

Problem: Several materialized telemetry structs are private nested types; compile-time `UnsafeUtility.SizeOf<MyTelemetryEntry>()` assertions from a central Editor script cannot legally reference them without widening access modifiers.
Solution: Generate `Assets/_Project/Scripts/Editor/TelemetryLayoutValidator1415.cs`; it searches loaded assemblies for each simple type name, then uses reflected `UnsafeUtility.SizeOf<T>()` plus `UnsafeUtility.GetFieldOffset(FieldInfo)` for every field, including padding.
Rejected Alternatives: Widening private structs to public would expand API surface across domains; skipping private structs would leave the highest-risk layouts unguarded.
Scalability potential: Editor-only guard has zero runtime cost on weak devices and gives high-tier development machines immediate fatal feedback on bad DTO edits.
Hardware Impact: Runtime impact is zero; Editor domain reload cost is accepted as validation cost.

## Decision 006 - Dump Size Repair

Problem: Post-layout audit found dump and validator constants that still advertised old 32/48-byte rows after the structs were expanded to 64 bytes. DRS also serialized `UpscalerComputeTimeMsBits` at the old offset.
Solution: Patch DRS, Scalability, Biolum, FluidPipe, and Salinity corrosion dump/validator paths. Salinity corrosion was converted from `BinaryWriter` per-row writes to stackalloc row serialization to match the 64-byte stride.
Rejected Alternatives: Updating only `StructLayout(Size=64)` would create files whose header lies about row width or whose bytes are written in old field order.
Scalability potential: All tiers now read deterministic row size; higher tiers can add dump consumers without per-domain stride branching.
Hardware Impact: Predictable 64-byte rows avoid mixed-stride copy and reader ambiguity on i3/MX350-class machines. Measured frame gain absent because dump paths are fault/cold paths.

## Decision 007 - Static Proof Instead Of Source Mutation Corruption Test

Problem: Task 16 requests temporary source corruption to prove validators fail, but the worktree is shared with many active agents and compile/editor execution is blocked by host load.
Solution: Perform an in-memory corruption test: offset `ScalabilityTelemetryEntry.RawFrameMs` from 8 to 9 inside the scanner model and prove the detector catches unaligned, overlap, and hole failures.
Rejected Alternatives: Temporarily editing source in a dirty concurrent tree risks leaking a broken offset into another agent's work or creating false diffs.
Scalability potential: No runtime effect; validation method is deterministic across devices.
Hardware Impact: Host-safe verification only.

## Decision 008 - Continuous Quality And Cinematic Scope

Problem: Layout work must not introduce binary quality switches or physical simulation complexity while normalizing telemetry records.
Solution: Keep DTO size/layout invariant and quality-independent. Existing `HomeostasisBrain.GlobalQualityWeight` remains payload/consumer policy data; the layout pass did not add `isLowEnd` switches or simulation code.
Rejected Alternatives: Scaling DTO size by hardware tier would break dump determinism and DataVault contracts.
Scalability potential: Weak, middle, high, and ultra devices share identical forensic layout; visual systems can scale presentation separately through continuous quality weight.
Hardware Impact: Stable row stride supports deterministic dumps on low-end silicon and richer offline analysis on high-end machines.

## Decision 009 - Report Hash Reconciliation

Problem: A follow-up prompt extraction used a compatible hash path and produced the current persisted prompt hash `b922c32c7f14ef7844c5f6bfa20bcdeb1e370a8c02d21641c43739f7c96780b0`; the final JSON still carried an older prompt-block hash.
Solution: Update the final report prompt section and regenerate `Docs/Reports/TELEMETRY_LAYOUT_OPTIMIZATION_REPORT_1415.json.sha256`.
Rejected Alternatives: Leaving a stale prompt hash in the proof artifact would make the report unverifiable even though runtime source was unchanged.
Scalability potential: No runtime effect.
Hardware Impact: No runtime effect.

## Decision 010 - Validator Newline Literal Repair

Problem: Post-final recheck found `TelemetryLayoutValidator1415.cs` line 67 had a generated newline char literal split across physical C# source lines inside `Spec.Split(new[] { ... })`. That is a source-level compile risk even though the DTO byte maps were intact.
Solution: Replace the malformed split with `Spec.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)`, regenerate `TELEMETRY_EDITOR_VALIDATOR_AUDIT_1415.json`, and update final report/source hashes.
Rejected Alternatives: Leaving the validator as-is would make Task 12 unverifiable under compiler proof; deleting the validator would remove the only central guard for private nested telemetry DTOs.
Scalability potential: Editor-only fix; weak, middle, high, and ultra runtime DTO stride remains unchanged.
Hardware Impact: Runtime impact is zero. Validation remains pending compiler/editor execution because CPU was 100 and active `dotnet` PID 55080 blocked build.

## Decision 011 - JSON Artifact Encoding Repair

Problem: Regenerated JSON artifacts were written by Windows PowerShell with UTF-8 BOM. `python -m json.tool` rejected both the final report and validator audit with `Unexpected UTF-8 BOM`.
Solution: Rewrite `TELEMETRY_EDITOR_VALIDATOR_AUDIT_1415.json` and `TELEMETRY_LAYOUT_OPTIMIZATION_REPORT_1415.json` with `System.Text.UTF8Encoding(false)`, then regenerate the final sidecar hash.
Rejected Alternatives: Accepting BOM-tolerant readers would weaken evidence portability; leaving the sidecar at the pre-encoding hash would make the report unverifiable.
Scalability potential: No runtime effect.
Hardware Impact: No runtime effect.

## Decision 012 - UberNoir Complex-Body Skip Was Invalid

Problem: APEX recheck proved `UberNoirShaderTelemetryEntry` was not a harmless skipped DTO. It is a real `BufferID.ShaderFeatureTelemetryRing` element in `Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs` and still used a 48-byte stride while final reports claimed the domain was complete.
Solution: Convert the entry to a 64-byte explicit layout without changing payload semantics: `TelemetryEntrySizeBytes=64`, data fields occupy bytes 0-47, `_pad0.._pad15` occupy bytes 48-63, and the dump serializer clears the full row before writing payload bytes. Add the validator spec row and record the incremental proof in `Docs/Reports/TELEMETRY_APEX_FINAL_VERIFICATION_1415.json`.
Rejected Alternatives: Keeping `complex_body` as a permanent skip would leave a live DataVault telemetry ring nonconforming; narrowing/removing payload fields would corrupt shader diagnostics; adding fake `SystemID`/`BufferID` fields inside the DTO would duplicate owner-route truth already carried by the vault handle.
Scalability potential: Weak devices get deterministic 64-byte forensic rows without mixed-stride cache waste; middle/high/ultra retain the same ABI while visual shader fidelity continues to scale through continuous `HomeostasisBrain.GlobalQualityWeight`.
Hardware Impact: Static cache-line hygiene only; no measured frame gain. Cold dump row now writes 64 deterministic bytes. Compiler proof remains blocked because CPU was 100 with active `csc` PID 8756 and `dotnet` PID 55080.

## Decision 013 - Dump Stride Constants Were Still Lying

Problem: A second APEX residue scan found four materialized 64-byte telemetry DTOs whose dump/header paths still used old row contracts: `ActiveSonarGeoTelemetryEntrySizeBytes=32`, `SwayTelemetryEntrySizeBytes=32`, `InternalFloodWaterlineRuntime.TelemetryEntrySizeBytes=40`, and `HectonVisorFluidDistortionFeature.BlackBoxEntrySizeBytes=48`. Waterline and VisorRefraction also serialized fields in old offset order.
Solution: Change all four row constants to 64, clear each span row before payload writes, serialize fields at their actual `[FieldOffset]` positions, and convert Waterline/VisorRefraction hot initializers from `new Struct { ... }` to `default` plus assignments. Record proof in `Docs/Reports/TELEMETRY_DUMP_STRIDE_RECHECK_1415.json`.
Rejected Alternatives: Updating only the constants would make headers say 64 while writers still emitted 32/40/48 useful bytes or stale field order; using `MemoryMarshal` over private nested structs would be harder to audit under current host-load compile blockade.
Scalability potential: Weak devices avoid mixed row widths in crash dumps; middle/high/ultra get stable forensic parsers independent of visual fidelity. DTO layout remains invariant while presentation systems continue to use continuous quality scalars.
Hardware Impact: Cold dump-path determinism only; no frame-time gain claimed. Compiler proof remains blocked because the dump-stride gate sampled CPU 88 with active `csc` PID 46904 and `dotnet` PID 29008.

## Decision 014 - Residual Blackbox Coverage Was Incomplete

Problem: A broader residual grep after the dump-stride repair still found `OrbitalDropReentryVfxController.TelemetryEntrySizeBytes=48` for a validator-covered 64-byte `ReentryVfxTelemetryEntry`, plus `BurstTokenBucketJobAdmissionService.JobAdmissionBlackboxEntry` as a real 300-slot `BufferID.JobAdmissionBlackBox` ring still sized 32 and omitted from `TelemetryLayoutValidator1415`.
Solution: ReentryVfx now uses a 64-byte dump row, stackalloc/FileStream row serialization at the actual `[FieldOffset]` order, and `default` plus assignments in the write-lock hot path. JobAdmission now uses `BlackboxEntrySizeBytes=64`, explicit `_pad0.._pad31`, validator row coverage, `TryAcquireWriteLock` at line 751, and `ReleaseWriteLock` in `finally` at line 779.
Rejected Alternatives: Updating only the header constants would leave short rows; leaving JobAdmission outside the validator would preserve a real Echelon 1 blackbox ABI gap; rewriting unrelated 32/40/48-byte signal contracts would cross the crash-telemetry boundary and risk public contract churn.
Scalability potential: Weak devices get one fixed crash-dump row width; middle/high/ultra get stable forensic parsers while admission budgets and visual fidelity continue to scale through continuous `GlobalQualityWeight`, not DTO-size branches.
Hardware Impact: Cold dump determinism and safer DataVault writer ownership only. Hot JobAdmission blackbox write adds an explicit vault writer fence; no measured frame-time data exists because compile/Unity proof is blocked by CPU 100.

## Decision 015 - Recheck3 Separates Fixable Rows From Split Blockers

Problem: A fresh APEX scan after the earlier residual repairs still needed proof that no literal under-64 telemetry/blackbox structs remained, and it had to avoid turning oversize diagnostics into fake 64-byte rows by deleting useful postmortem data.
Solution: Regenerate `Docs/Reports/TELEMETRY_APEX_RESIDUAL_RECHECK3_1415.json`. Current source scan reports `under64TelemetryOrBlackBoxCount=0`. `MacroDatabaseTelemetryEntry` is confirmed at 64 bytes and added to `TelemetryLayoutValidator1415` line 22. `ChunkResidencyTelemetryEntry` is recorded as a split blocker because bytes 0-65 are useful payload and byte 64..65 is `ActiveImpostorCount`.
Rejected Alternatives: Deleting `ActiveImpostorCount`, truncating `MemoryDefragTelemetryEntry`, or calling a 66-byte payload "64-byte compliant" would produce false forensic evidence.
Scalability potential: Weak devices keep deterministic 64-byte rows where payload fits; middle/high/ultra systems need owner-approved header/detail splits for richer telemetry instead of bloating the primary ring or changing DTO size by quality tier.
Hardware Impact: Static proof only. No frame-time gain measured. Compiler/Unity validation remains pending because build launch is still gated by host CPU/process rules.

## Decision 016 - Chunk Residency Packed Without Data Loss

Problem: Recheck3 classified `ChunkResidencyTelemetryEntry` as a 72-byte split blocker because `ActiveImpostorCount` occupied bytes 64..65 after the 64-byte line. A deeper flag audit proved the current telemetry flags use only bits 0..12, leaving high bits in the 32-bit flag lane unused.
Solution: Replace the public `Flags` field with private `_packedFlags` at offset 48. Preserve the public `Flags` and `ActiveImpostorCount` API as properties: low 16 bits hold `Flags`, high 16 bits hold `ActiveImpostorCount`. The final byte map is 0..63 with zero holes and zero overlaps. The writer now acquires `ResidencyTelemetryVaultBufferId=(BufferID)70568` through `TryAcquireWriteLock` and releases in `finally`; the dump writer emits the same 64-byte offset order.
Rejected Alternatives: Deleting `ActiveImpostorCount` would erase postmortem signal. Keeping 72 bytes would preserve a cache-line violation. Owner split was rejected for this specific row after proving a safe spare-bit lane existed.
Scalability potential: Low tier keeps a single 64-byte row and a cheap visual-fake residency path. Middle/high/ultra keep identical ABI while chunk radius, load cadence, and impostor work continue scaling through `HomeostasisBrain.GlobalQualityWeight` instead of DTO-size branches.
Hardware Impact: Per-ring storage drops from 21,600 bytes to 19,200 bytes for 300 samples, saving 2,400 bytes per residency ring/dump. No frame-time microbenchmark exists. Compiler proof is still blocked because the recheck4 gate sampled CPU 100 with active `dotnet` PID 5428.

## Decision 017 - Live Telemetry ABI Was Still 32 Bytes

Problem: A residual audit found `CrashTelemetryBuffer.LiveTelemetryRecord` was still `[StructLayout(... Size = 32)]` with `LiveTelemetryVersion=1`, and `Tools/TelemetryDashboard/server.py` only decoded `struct.Struct("<IIIIIfff")`. That left the live crash dashboard file outside the 64-byte evidence lane even after the telemetry/blackbox DTO pass.
Solution: Convert the private nested `LiveTelemetryRecord` to an explicit 64-byte v2 layout: `Magic` 0, `Version` 4, `RecordSizeBytes` 8, `FrameIndex` 12, `ActiveChunkCount` 16, `GcAllocBytes` 20, `CpuFrameTimeMs` 24, `DeltaTime` 28, `ReservedMemoryMb` 32, `LatencyMs` 36, `GpuFrameTimeMs` 40, `SystemMask` 44, `ErrorFlags` 48, `VelocityPacked` 52, `AupShiftSequence` 56, `LastOriginShiftFrame` 60. The dashboard parser now supports both v2 64-byte records and legacy v1 32-byte records with an explicit warning. The central validator now has a `LiveTelemetryRecord` row at line 24.
Rejected Alternatives: Keeping the file as a permanent 32-byte exception would contradict the live crash telemetry ABI goal. Breaking v1 dashboard parsing would make existing `runtime_telemetry.bin` artifacts unreadable. Moving this file into `GlobalDataVault` was rejected because it is an existing CrashTelemetryBuffer-owned optional dashboard file, not cross-domain native ownership.
Scalability potential: The every-frame crash ring remains invariant. Optional `runtime_telemetry.bin` write cadence now consumes `HomeostasisBrain.GlobalQualityWeight`: low tier writes every 120 frames, middle quality around 75 frames, high/ultra every 30 frames. No `isLowEnd`/binary switch was added.
Hardware Impact: Low-end machines reduce optional live-file IO frequency by 2x versus the old fixed 60-frame cadence. High-end machines can buy fresher dashboard telemetry without changing the 64-byte ABI. No frame-time benchmark exists; static zero-GC scan reports `TryWriteLiveTelemetry` and `ResolveLiveTelemetryWriteIntervalFrames` both have `newReferenceLike=0`, `string.Format=0`, `.ToString=0`, `LINQ=0`, `foreach=0`.

## Decision 018 - Crash Ring Setter Needed A Writer Fence

Problem: APEX recheck6 found `CrashTelemetryBuffer.VaultArray<T>.set` mutating GlobalDataVault-backed buffers through `TryResolveHandle` without a writer lock. That meant 36 `_ringBuffer[writeIndex] = entry` crash telemetry writes had no `ActiveWriterSystemID` proof and no `finally` release path. The same pass found a stale `byte[32]` comment on the live telemetry scratch array after the v2 64-byte migration.
Solution: Change the `VaultArray<T>` index setter to fail closed unless `_vault` and the generation handle are valid, acquire `TryAcquireWriteLock(in _handle, SystemID.CoreDiagnostics, out buffer)`, write only after bounds and `IsCreated` checks, and release with `ReleaseWriteLock(in _handle, SystemID.CoreDiagnostics)` inside `finally`. Correct the live scratch comment to `byte[64]`. Recheck6 proof artifact is `Docs/Reports/TELEMETRY_APEX_RESIDUAL_RECHECK6_1415.json` with SHA-256 `5a0563e0846475921ee709d66afad30f5fe399b89ec2b3b65b20b8fd05b9b4f4`.
Rejected Alternatives: Keeping direct `TryResolveHandle` mutation was a Data Sovereignty violation. Bulk-locking the whole per-frame crash telemetry phase was rejected because the existing write sites are event-scattered and a broad lock would create a larger contention window. Editing `ModularEquipmentEngine.cs` after the targeted compile failure was rejected because it is equipment domain ownership, not 1415 crash telemetry layout.
Scalability potential: Low tier keeps deterministic 64-byte crash rows and bounded writer ownership. Middle/high/ultra retain the same ABI and can increase optional live telemetry cadence through continuous `GlobalQualityWeight`; no binary tier branch or physical simulation was introduced.
Hardware Impact: The setter adds a DataVault writer fence around each crash ring row write. No frame-time microbenchmark exists. The correctness gain is explicit ownership and fail-closed behavior during compaction/alias contention. Final compile proof is blocked by external `Assets/_Project/Scripts/ModularEquipmentEngine.cs:1290-1317` CS8168/CS8350; forensic dump is `Docs/AgentLogs/Dump_1415_BuildBlockedByDependency_20260528T133135Z.txt`.

## Decision 019 - Crash Export Aliases Needed Lock Fences

Problem: Recheck7 found a hidden private `VaultArray<T>` to `NativeArray<T>` implicit conversion. `SnapshotRecentEntries` and `BuildExportScratch` could therefore bypass the DataVault writer route and write export snapshot/scratch buffers through unsafe pointers without an explicit `TryAcquireWriteLock`/`finally ReleaseWriteLock` proof.
Solution: Remove the implicit conversion. `SnapshotRecentEntries` now pins `BufferID.CrashTelemetryRing` with `TryLockBuffer`, reads through `TryReadHandle`, writes `BufferID.CrashTelemetryExportSnapshot` only after `TryAcquireWriteLock`, and releases both locks in `finally`. `BuildExportScratch` now pins `BufferID.CrashTelemetryExportSnapshot`, reads through `TryReadHandle`, writes `BufferID.CrashTelemetryExportScratch` only after `TryAcquireWriteLock`, and releases both locks in `finally`. Recheck7 proof artifact is `Docs/Reports/TELEMETRY_APEX_RESIDUAL_RECHECK7_1415.json` with SHA-256 `2f8a320475b3bb3704a68fec56cc3053d058255d69ef914b076d6ba38ecacbbe`.
Rejected Alternatives: Trusting a private conversion was rejected because it makes future lock audits lie. A single broad crash-export global lock was rejected because the existing DataVault handle-level locks already express owner and buffer identity. Editing the external equipment compile blocker was rejected because `ModularEquipmentEngine.cs` is outside 1415 ownership.
Scalability potential: Weak devices get fail-closed crash export ownership without adding simulation or binary tier logic. Middle/high/ultra keep the same 64-byte ABI and can still increase optional live telemetry cadence through continuous `HomeostasisBrain.GlobalQualityWeight`; export correctness does not depend on device tier.
Hardware Impact: Crash export is fault/cold path. No measured frame-time gain exists. Correctness gain is explicit DataVault ownership during unsafe copy/build phases; Recheck7 did not launch `dotnet build` because CPU was 87% and active `dotnet` PID 56280 was already building.

## Decision 020 - AUP Hot Writer Cleanup Without Fake ABI Shrink

Problem: Recheck8 found Echelon 1 `AupOriginShiftTelemetryEntry` remains a 128-byte telemetry ring row. A full field-preserving 64-byte fix requires a header/detail split and a second DataVault buffer/dump contract. The safe immediate defect was narrower: two AUP hot telemetry writers still used `new AupOriginShiftTelemetryEntry { ... }`, which weakens strict Zero-GC text evidence even though it is a value-type initializer rather than a managed reference allocation.
Solution: In `RecordRebaseCompletion` and `RecordFrameTelemetry`, replace telemetry object initializers with `default` plus direct field assignments. In `RecordRebaseCompletion`, replace the local `new float3(...)` with `default` plus `x/y/z` assignments. Preserve all telemetry payload fields and keep `TelemetryRingBuffer=(BufferID)73033` unchanged.
Rejected Alternatives: Deleting `double3 TotalUniverseOffset`, `double3 ShiftDelta`, or camera diagnostics to force 64 bytes was rejected as forensic data loss. Adding a new detail buffer without an owner route card and compiler proof was rejected as a cross-contract migration risk. Claiming the AUP ring is fixed was rejected because the row is still 128 bytes.
Scalability potential: Low/middle/high/ultra devices keep the existing AUP data contract. Existing batch sizing continues to consume `HomeostasisBrain.GlobalQualityWeight`; no binary `isLowEnd` branch or physical simulation was added.
Hardware Impact: No measured frame-time gain. Static hot-path text evidence improves: modified AUP telemetry writer ranges now report forbidden `new`, `string.Format`, `.ToString`, LINQ, and `foreach` counts of zero. The 128-byte AUP ring remains a real cache-line residual until split by owner-approved header/detail design.

## Decision 021 - AUP 128-Byte Ring Split Into Paired 64-Byte Rows

Problem: Recheck9 proved the previous blocker could be fixed locally without deleting telemetry. `AupOriginShiftTelemetryEntry` carried 128 bytes of useful postmortem data: `ShiftDelta` plus frame/count fields and detail data (`TotalUniverseOffset`, camera local position, rebase cost, hashes, hot entity count). A single 64-byte row cannot preserve that payload.
Solution: Split the contract into `AupOriginShiftTelemetryEntry` at bytes `0..63` and `AupOriginShiftTelemetryDetailEntry` at bytes `0..63`. Keep the primary ring at `BufferID 73033`; add `TelemetryDetailRingBuffer=(BufferID)73056` after a pre-use source/report/log scan returned no matches. Open/resolve/release the detail handle beside the header handle. Write both rings under `TryAcquireWriteView` in `TickPreSimulation` and `RecordRebaseCompletion`, then release both locks inside `finally`. Raise the dump contract to version 3 and record `DetailStrideBytes=64` plus `CombinedRecordBytes=128`.
Rejected Alternatives: Deleting `TotalUniverseOffset`, camera diagnostics, rebase timing, or hash fields was rejected as false forensic compliance. Keeping a 128-byte primary row was rejected because the task is explicit 64-byte telemetry rows. Writing a second unmanaged array without a DataVault `BufferID` was rejected as Data Sovereignty bypass. Binary `isLowEnd` routing was not relevant and was not added.
Scalability potential: Low devices keep the same cheap AUP math and now write two predictable 64-byte rows only when telemetry is recorded; middle/high/ultra devices retain full forensic detail without changing gameplay truth or authority. Existing workload scaling still uses continuous `HomeostasisBrain.GlobalQualityWeight` through `ResolveQualityScaledBatchSize`; no low/ultra dichotomy was introduced.
Hardware Impact: The primary AUP telemetry ring row drops from 128 bytes to 64 bytes; the detail ring adds a paired 64-byte row only for detail payload, preserving cache-line alignment and dump parser determinism. No profiler microsecond gain is claimed. Build proof was not launched because the gate first found active `dotnet` PID 3560, then the post-artifact gate sampled CPU 100% with active `csc` PID 21732 and active `dotnet` PIDs 24600/25932.

## Decision 022 - Core Residual Rows And Defrag Header/Detail Split

Problem: Recheck10 found remaining core-domain telemetry fragments that were not covered by the earlier strict `*TelemetryEntry`/`*BlackBoxEntry` scanner: `MemoryDefragTelemetryEntry` was 128 bytes, `SignalLaneTelemetry`, `TelemetryAnomalySignal`, `CrashTelemetrySignal`, `CoreFluidCompartmentTelemetryDTO`, `BlackboxEditorFrame`, `TelemetryLoggingMaskDTO`, and `BlackboxSourceSlot` were compact 16/32-byte telemetry carriers. The same scan also found `TelemetryHeaderDTO` and `TelemetryEventDTO`, but those are internal blackbox header/event fragments, not standalone ring rows.
Solution: Split memory defrag into paired 64-byte primary/detail rows without deleting fields. Pad the compact core telemetry carriers to explicit 64-byte layouts and add validator rows. Recheck10 kept `TelemetryHeaderDTO` and `TelemetryEventDTO` compact as embedded prefix/marker fragments; Recheck11 later superseded that exception after proving `TelemetryEventDTO` is a real `BufferID.ShinobuCrashTelemetryEvents` row. Recheck10 proof artifact is `Docs/Reports/TELEMETRY_APEX_RESIDUAL_RECHECK10_1415.json` with SHA-256 `5b0c975384f91c09ce4034bb211bce720ff164b426f20b34fad17c8c6b831a2f` after line-reference metadata correction.
Rejected Alternatives: Deleting memory defrag counters/hash/context fields was rejected as forensic data loss. At Recheck10 time, widening `TelemetryHeaderDTO` and `TelemetryEventDTO` was rejected as an embedded-fragment migration; Recheck11 proved that rejection was too broad and superseded it. Adding fake `SystemID`/`BufferID` fields was rejected because owner routes already exist outside these DTO payloads.
Scalability potential: Low devices get 64-byte cache-line rows for core crash/telemetry evidence. Middle/high/ultra keep the same ABI and can spend saved ambiguity budget on richer diagnostics through paired detail rows, not DTO-size branches. No binary `isLowEnd` switch or physical simulation was introduced; Recheck10 hot scans report `isLowEnd=0`.
Hardware Impact: Static layout gain only. The defrag primary row is now one 64-byte line plus one 64-byte detail line when detail is recorded; compact core telemetry carriers are stable 64-byte rows. No profiler microsecond gain is claimed. Targeted core build proof passed after CPU 24% and no active compiler processes: exit code 0 in 75,273 ms, CS0636=0, 1415 diagnostic hits=0. Full solution build was not repeated.

## Decision 023 - Header/Event DTO Compact Exception Was Invalid

Problem: Recheck11 rejected my Recheck10 compact-fragment exception. `TelemetryEventDTO` is used as a real DataVault-backed SHINOBU telemetry event row through `BufferID.ShinobuCrashTelemetryEvents`, so leaving it at 16 bytes was not honest 64-byte ring hardening. `TelemetryHeaderDTO` also crosses unmanaged dump/header copy paths and needed an explicit 64-byte contract while preserving the first 16-byte prefix semantics.
Solution: Widen `TelemetryHeaderDTO` and `TelemetryEventDTO` in `Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs` to `[StructLayout(LayoutKind.Explicit, Size = 64)]`. Keep the existing first 16 bytes unchanged: header `Timestamp 0..7`, `FrameNumber 8..11`, `FatalHash 12..15`; event `EventHash 0..3`, `ScalarValue 4..7`, `EntityId 8..11`, `_pad0 12..15`. Fill remaining bytes with named padding and add validator rows at `TelemetryLayoutValidator1415.cs:29-30`. Recheck11 proof artifact is `Docs/Reports/TELEMETRY_APEX_RESIDUAL_RECHECK11_1415.json` with SHA-256 `0e18105addccc2c9054f621f03df38262e2448eacbf6efaeb24d09395b4ad718`.
Rejected Alternatives: Keeping the compact exception was rejected because it left a real telemetry event row outside the 64-byte ABI. Raising `BlackboxHeaderPrefixBytes` from 16 to 64 was rejected because dump metadata is intentionally written after the 16-byte prefix and changing that offset would be a broader file-contract migration. Adding a per-write `TryAcquireWriteLock` retrofit to SHINOBU event writes in the same patch was rejected because the existing blackbox route uses lifetime vault pin/unpin; changing it requires a separate owner route card and compiler proof.
Scalability potential: Weak, middle, high, and ultra devices now share one 64-byte SHINOBU header/event ABI. The change does not add physical simulation, visual work, or binary tier switches. Fidelity and cadence remain owned by consumer policy and continuous `HomeostasisBrain.GlobalQualityWeight`, not DTO size.
Hardware Impact: `TelemetryEventDTO` storage grows from 16 bytes to 64 bytes; a 4096-event ring increases from 65,536 bytes to 262,144 bytes, plus 196,608 bytes. The cost is accepted for cache-line-stable forensic rows. No profiler microsecond gain is claimed. Recheck11 compile proof is pending because gates sampled CPU 99% with active `dotnet` PID 65020, then CPU 99% with active `csc` PID 28228 and `dotnet` PID 46892. No build was launched after this source change. Resource-gate dump: `Docs/AgentLogs/Dump_1415_Recheck11_CompileBlocked_20260528T_resource_gate.txt`, SHA-256 `80d2f31b70db1d6450aa778d1f5ac3324be27d2d8109efc2559ce67d899fb4d3`.
