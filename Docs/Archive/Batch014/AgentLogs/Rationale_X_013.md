# X_013 Rationale

## Decision 001

Problem: The assignment requires byte-perfect memory layout facts while forbidding C# source mutation.
Solution: Use static source extraction plus compiler-backed layout probes only if existing project references allow it without modifying source. Primary evidence remains file path and line number.
Rejected Alternatives: Unity Editor mutation scripts were rejected because they create source/assets outside the read-only mission. Documentation-only inference was rejected because it cannot prove field offsets.
Scalability potential: Low/Middle/High/Ultra are unaffected by this audit; the output protects all tiers from stale pointer and alignment errors in future memory work.
Hardware Impact: Static audit saves zero runtime microseconds directly. It prevents future low-end i3/MX350 stalls/crashes caused by misaligned DTOs or relocation races.

## Decision 002

Problem: Many mandates apply broadly; this task only audits memory core and synchronization.
Solution: Load the six mandates directly governing runtime struct layout, native memory, DataVault ownership, zero-GC, postmortem telemetry, and global registry/data authority.
Rejected Alternatives: Reading unrelated AI/render/physics mandates was rejected as prompt contamination and wasted context.
Scalability potential: Narrow mandate set keeps report evidence focused on memory sovereignty and ARM64 safety across device tiers.
Hardware Impact: No runtime gain; reduces architecture ambiguity before future allocator/defragmenter edits.

## Decision 003

Problem: The mission is read-only but AGENTS requires progress and final logs.
Solution: Write only task/status/rationale/report/log documentation under `Docs`; do not edit any `.cs` source.
Rejected Alternatives: Skipping disk logs would violate batch protocol. Editing source to add probes was rejected.
Scalability potential: Documentation creates a stable handoff for future agents without changing gameplay truth or runtime paths.
Hardware Impact: No runtime cost.

## Decision 004

Problem: A broad documentation search returned archived batch logs, which AGENTS forbids as fresh context.
Solution: Discard all archive output and use only live source, current prompt, current mandates, and active domain file as evidence.
Rejected Alternatives: Mining archive logs for prior GlobalDataVault claims was rejected because it could import stale or unrelated agent state.
Scalability potential: Current source-only evidence prevents obsolete memory assumptions from entering future Low/Middle/High/Ultra implementations.
Hardware Impact: No runtime cost.

## Decision 005

Problem: `TryAcquireWriteLock` locks a block by `Reserved0`/`Reserved1`, while `TryLockBuffer` also sets `_activeLocks`; the two routes are not equivalent.
Solution: Record lock semantics separately: writer locks use `ActiveWriterSystemID + Reserved0 + Reserved1`; external job locks use `Reserved0 + Reserved1 + _activeLocks`.
Rejected Alternatives: Treating `ActiveBurstLockMask` as the universal lock truth was rejected because `TryAcquireWriteLock` never calls `SetActiveLockBit`.
Scalability potential: Low devices avoid stale-pointer crashes; high and ultra devices can only increase visual/memory pressure if lock truth is exact.
Hardware Impact: No immediate us/frame gain; prevents future hard stalls/crashes from unsafe relocation under job pressure.

## Decision 006

Problem: Live compaction checks active locks, but arena growth relocation uses `H8Memory.ReallocateRaw` and can move the whole arena from allocation paths.
Solution: Report arena growth as a separate stale-pointer risk vector, not as part of the bounded defrag slice.
Rejected Alternatives: Limiting the audit to `FrostTickDefrag` was rejected because stale pointers do not care whether relocation came from defrag or growth.
Scalability potential: Low tier is most exposed because memory pressure can force growth/relocation; high/ultra tiers may hit it under visual-overkill buffer demand.
Hardware Impact: No direct runtime gain; identifies a route that can invalidate Burst/job pointers on i3/MX350 under memory pressure.
