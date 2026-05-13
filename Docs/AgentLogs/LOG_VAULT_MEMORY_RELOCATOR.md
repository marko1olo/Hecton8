# LOG_VAULT_MEMORY_RELOCATOR

## 2026-05-14 - Metabolic Compaction Pass
What was wrong:
GlobalDataVault exposed fragmentation telemetry but the working copy had lost the live relocation pass. Handles existed but stale generation resolution had regressed toward fatal behavior in one path. A 10-hour session would still accumulate arena holes unless the vault physically moved occupied blocks into prior free gaps.

What was done:
Implemented/restored `VaultBufferHandle<T>` resolution semantics so generation/pointer/length/stride mismatches refresh the cached pointer. Restored Burst-compatible `VaultMemMoveJob` using `UnsafeUtility.MemMove`. Re-enabled low-stress compaction in `FrostTickDefrag(elapsedSeconds, systemStress01)` behind `GapRatio > 0.15f` and `SystemStress < 0.5f`. Added pre-simulation relocation fence usage, 64-byte alignment audit, locked-block skip, fixed relocation records, watchdog breach counting, total moved byte telemetry, vault generation telemetry, and memory barriers around move publication.

Cinematic Cheats used:
No physical simulation was introduced. The system uses deterministic memory shape surgery, not a simulated allocator model. The main cheat is time-sliced movement only during low-stress pre-simulation windows, preserving frame feel instead of pursuing perfect immediate compaction.

Exact Microseconds saved:
Measured runtime microseconds are unavailable because Unity MCP has no active editor session. Static estimates recorded in status: stress gate ~1 us, metadata table shift ~2 us per moved block, signal publish ~3 us per relocation, stale handle resolve cold path ~6 us, watchdog branch <1 us, lock mutation ~2 us. The hard cap is 1.0 ms per compaction slice. Actual savings depend on long-session fragmentation and allocation failure avoidance.

Verification:
Memory-only Roslyn compile passed for `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` and `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` against Unity 6000.4 references. `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -v:minimal -p:UseSharedCompilation=false` remains blocked by unrelated missing domain dependencies: Environment.Fluids, Core.Scheduling, Audio.Virtualization/Propagation/Echolocation, Physics.CCD, Persistence, Inventory algorithms/corrosion, world terrain/outpost/GPR contracts, and related interfaces. Unity MCP console validation failed because no Unity session is available.

Final anti-bloat pass:
Checked for duplicate compaction helpers, stale `FatalMemoryException.ThrowStaleVaultHandle()` use, live `VaultMemMoveJob` presence, relocation signal bridge, and TODO/HACK/FIXME markers. Removed the remaining fatal stale-handle path and re-ran the memory-only compile successfully.

Integrator note:
Legacy `GetBuffer<T>()` consumers can still cache raw `NativeArray` views across frames. Correct migration target is `VaultBufferHandle<T>.Resolve()` or invalidation via `MemoryAddressShiftSignal`. Blocks owned by active jobs must be locked with `TryLockBuffer` and unlocked after job completion.
