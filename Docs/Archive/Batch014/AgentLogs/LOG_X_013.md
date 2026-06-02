# LOG_X_013

## 2026-05-25 MEMORY_AND_LOCK_STRUCTURE_SCOUT Phase 0

What was wrong:
- Future agents had no current byte-layout ledger for `GlobalDataVault.cs` and `H8Memory.cs`.
- Lock ownership was ambiguous without source-line evidence.
- Defrag and arena relocation safety could not be judged from docs alone.

What was done:
- Extracted `X_013` prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Loaded relevant mandates: ARM64 struct layout, native memory/jobs, H8 arena allocator, zero-GC, postmortem telemetry, global registry/authority.
- Read live source files only: `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` and `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`.
- Wrote `Docs/Reports/MEMORY_STRUCTURE_SCOUT_REPORT_X_013.json`.
- Wrote `Docs/Reports/MEMORY_STRUCTURE_SCOUT_REPORT_X_013.md`.

Cinematic Cheats used:
- None. This was memory-core static audit, not simulation/rendering work.

Exact microseconds saved:
- 0 us/frame direct runtime saving. No runtime path changed.
- Preventive value: identified stale-pointer risk routes before future allocator/defrag code generation.

Risk findings:
- RED_STATIC: `TryAcquireWriteLock` lacks pre-mutation `_compactionFence` check.
- RED_STATIC: `TryLockBuffer/TryUnlockBuffer` ignore `lockOwner` and lack `_compactionFence` checks.
- RED_STATIC: `TryGrowArena` can relocate entire arena through `H8Memory.ReallocateRaw` without active lock mask checks.
- YELLOW_STATIC: `ActiveBurstLockMask` is 32-bit BufferID low-bit collision domain.
- YELLOW_STATIC: writer locks from `TryAcquireWriteLock` are not represented in `ActiveBurstLockMask`.

Verification:
- C# source mutation by X_013: none. Post-audit `git status` shows `GlobalDataVault.cs` and `H8Memory.cs` dirty in the shared worktree; X_013 did not write them.
- Build: not run; read-only audit.
- Pack scan: no `StructLayout(... Pack=...)` in target files.
- Partial scan: no production partial definitions for `GlobalDataVault`, `H8Memory`, or `IDataVault`.

## 2026-05-25 APEX Override Addendum

What was wrong:
- Prior report did not spell out `VaultArenaBlock` byte offsets and the non-relationship between `TryAcquireWriteLock` and `ActiveBurstLockMask` with enough force.
- The defrag section needed to distinguish live compaction `MemMove` from whole-arena growth relocation in `H8Memory.ReallocateRaw`.

What was done:
- Wrote `Docs/Reports/MEMORY_STRUCTURE_SCOUT_REPORT_X_013_APEX_ADDENDUM.md`.
- Re-read exact live ranges in `GlobalDataVault.cs` for `VaultArenaBlock`, `TryAcquireWriteLock`, `_activeLocks`, `FrostTickDefrag`, `TryRunLiveCompactionSlice`, `TryMoveOccupiedBlockLeft`, and arena growth.
- Re-read exact live ranges in `H8Memory.cs` for `ReallocateRaw`.

Cinematic Cheats used:
- None. Memory audit only.

Exact microseconds saved:
- 0 us/frame direct saving. No runtime code changed.

Risk findings refined:
- `VaultArenaBlock` is explicitly 32 bytes with fields at offsets 0, 8, 16, 20, 24, 28, 29, and 30; 32-byte total size is 8-byte aligned.
- `TryAcquireWriteLock` does not touch `_activeLocks`; it uses `Interlocked.CompareExchange` on `ActiveWriterSystemID`, then mutates `Reserved0` and `Reserved1`.
- Live compaction rechecks active locks and block flags before `UnsafeUtility.MemMove`, but lock acquisition methods do not check `_compactionFence`.
- Arena growth relocation remains the sharper stale-pointer risk because `TryGrowArenaForBytes` does not check active Burst locks before `H8Memory.ReallocateRaw` moves and frees the old arena.

## 2026-05-25 APEX Override Revalidation

What was wrong:
- The first revalidation regex expected an exact `<AGENT_PROMPT id="X_013">` shape and missed the active prompt because the tag also contains `role` and `chat_name` attributes.

What was done:
- Re-extracted the active prompt with an attribute-aware regex: `<AGENT_PROMPT\s+id="X_013"[^>]*>...`.
- Re-read live source line ranges for `VaultArenaBlock`, `TryAcquireWriteLock`, `TryLockBuffer`, `TryUnlockBuffer`, `_activeLocks`, `FrostTickDefrag`, `TryRunLiveCompactionSlice`, `TryMoveOccupiedBlockLeft`, arena growth, and `H8Memory.ReallocateRaw`.
- Confirmed the APEX addendum still matches current source line evidence.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- 0 us/frame. Read-only revalidation.
