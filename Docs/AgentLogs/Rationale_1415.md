# Rationale_1415

Status: STATIC RECHECKED / BUILD BLOCKED BY HOST LOAD
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
