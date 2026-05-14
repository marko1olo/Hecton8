# LOG_VAULT_MEMORY_RELOCATOR

## 2026-05-14 - Metabolic Compaction Pass
What was wrong:
GlobalDataVault exposed fragmentation telemetry but the working copy had lost the live relocation pass. Handles existed but stale generation resolution had regressed toward fatal behavior in one path. A 10-hour session would still accumulate arena holes unless the vault physically moved occupied blocks into prior free gaps.

What was done:
Implemented/restored `VaultBufferHandle<T>` resolution semantics so generation/pointer/length/stride mismatches refresh the cached pointer. Restored direct `UnsafeUtility.MemMove` relocation in the zero-GC defrag slice. Re-enabled low-stress compaction in `FrostTickDefrag(elapsedSeconds, systemStress01)` behind `GapRatio > 0.15f` and `SystemStress < 0.5f`. Added pre-simulation relocation fence usage, 64-byte alignment audit, locked-block skip, fixed relocation records, watchdog breach counting, total moved byte telemetry, vault generation telemetry, and memory barriers around move publication.

Cinematic Cheats used:
No physical simulation was introduced. The system uses deterministic memory shape surgery, not a simulated allocator model. The main cheat is time-sliced movement only during low-stress pre-simulation windows, preserving frame feel instead of pursuing perfect immediate compaction.

Exact Microseconds saved:
Measured runtime microseconds are unavailable because Unity MCP has no active editor session. Static estimates recorded in status: stress gate ~1 us, metadata table shift ~2 us per moved block, signal publish ~3 us per relocation, stale handle resolve cold path ~6 us, watchdog branch <1 us, lock mutation ~2 us. The hard cap is 1.0 ms per compaction slice. Actual savings depend on long-session fragmentation and allocation failure avoidance.

Verification:
Memory-only Roslyn compile passed for `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` and `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` against Unity 6000.4 references. `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -v:minimal -p:UseSharedCompilation=false` remains blocked by unrelated missing domain dependencies: Environment.Fluids, Core.Scheduling, Audio.Virtualization/Propagation/Echolocation, Physics.CCD, Persistence, Inventory algorithms/corrosion, world terrain/outpost/GPR contracts, and related interfaces. Unity MCP console validation failed because no Unity session is available.

Final anti-bloat pass:
Checked for duplicate compaction helpers, stale `FatalMemoryException.ThrowStaleVaultHandle()` use, live memmove presence, relocation signal bridge, and TODO/HACK/FIXME markers. Removed the remaining fatal stale-handle path and re-ran the memory-only compile successfully.

Integrator note:
Legacy `GetBuffer<T>()` consumers can still cache raw `NativeArray` views across frames. Correct migration target is `VaultBufferHandle<T>.Resolve()` or invalidation via `MemoryAddressShiftSignal`. Blocks owned by active jobs must be locked with `TryLockBuffer` and unlocked after job completion.

## 2026-05-14 - Hardening Pass 2
What was wrong:
The working copy regressed during concurrent integration: `GlobalDataVault.cs` was overwritten back toward telemetry-only defrag and fatal stale-handle checks. That would make a valid relocated handle crash instead of healing, and it would leave arena holes unclaimed.

What was done:
Re-applied live compaction in `FrostTickDefrag(elapsedSeconds, systemStress01)`, restored generation healing in `ResolveBuffer`, restored stress-block/moved/locked-skip flags, kept relocation records bounded, and added a 512 KB soft move budget inside the 1.0 ms watchdog. Full project build was rerun after parallel integrations settled and exits 0.

Cinematic Cheats used:
No allocator simulation. The cheat remains cold-window surgery: do small deterministic moves in pre-simulation only when system stress is below 0.5, then broadcast exact buffer shifts instead of forcing every system to rebuild caches.

Exact Microseconds saved:
Still not runtime-measured. Static budget: stress gate ~1 us, stale handle heal ~6 us on cold mismatch, metadata publication ~2 us per moved block, signal bridge ~3 us per moved record, watchdog check <1 us, lock mutation ~2 us. Hard slice ceiling remains 1.0 ms with a 512 KB per-slice soft byte cap for low-end silicon.

Verification:
`rg` readback confirmed no stale-handle fatal call remains in `GlobalDataVault.cs` after the final patch. Targeted Roslyn compile for `H8Memory.cs` + `GlobalDataVault.cs` passed. `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -v:minimal -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1` exits 0 and emits `Temp\bin\Debug\Hecton8.Core.dll`.

## 2026-05-14 - Hardening Pass 3
What was wrong:
The compaction slice had no explicit pre-move stop when relocation records reached capacity. With many tiny buffers, it could move more blocks than the 64-record signal bridge could report. Locked buffers were protected from compaction but not resize, and H8Memory editor teardown hooks only unsubscribed callbacks.

What was done:
Added relocation-record capacity gates before memmove, widened the alignment audit to source offset, destination offset, and moved byte span, rejected resize of locked blocks, and restored editor assembly-reload/quitting/playmode-exit shutdown registration for H8Memory.

Cinematic Cheats used:
The allocator keeps exact per-buffer signals instead of broad cache flushes. When the fixed signal budget is exhausted, compaction stops and continues in a later cold window.

Exact Microseconds saved:
Record-budget and lock checks are branch-level, estimated under 1 us. The meaningful win is avoided unreported pointer relocation and avoided native-memory accumulation across editor play sessions.

Verification:
Targeted Roslyn compile for `H8Memory.cs` + `GlobalDataVault.cs` passed. Full `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -v:minimal -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1` exits 0. Current build has 11 unrelated warnings: duplicate source-file includes, obsolete `Object.GetInstanceID()` usage, and unrelated audio/spatial fields.

## 2026-05-14 - Hardening Pass 4
What was wrong:
Editor teardown can call `H8Memory.Shutdown()` before owner-level cleanup. Late `Release` or `FreeRaw` calls after sentinel shutdown could otherwise dispose/free wrappers whose backing memory was already released by the global shutdown pass.

What was done:
Guarded `H8Memory.Release<T>` and owner-tagged `FreeRaw` when `_initialized` is false. NativeArray wrappers are nulled without calling `Dispose`; raw pointers are ignored when the sentinel is offline. This prevents post-shutdown double-free while keeping normal tracked cleanup strict when the sentinel is active.

Cinematic Cheats used:
No runtime simulation. This is teardown hardening: strict owner checks during play, conservative no-op cleanup after global shutdown.

Exact Microseconds saved:
No frame-time saving claimed. The added checks are branch-level and estimated under 1 us; the gain is avoiding native heap corruption during repeated editor sessions.

Verification:
Targeted Roslyn compile for `H8Memory.cs` + `GlobalDataVault.cs` passed. Initial no-restore build failed because `Temp\obj\Hecton8.Core\project.assets.json` had been removed; reran `dotnet build .\Hecton8.Core.csproj --nologo -v:minimal -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1`, which restored assets and exited 0. Final strict `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -v:minimal -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1` exits 0 with 0 warnings and 0 errors.
