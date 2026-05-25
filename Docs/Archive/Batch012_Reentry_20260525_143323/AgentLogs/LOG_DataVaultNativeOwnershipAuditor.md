# LOG_DataVaultNativeOwnershipAuditor

2026-05-25 DataVault/native ownership read-only audit.

What was wrong:
- HazardZoneManager dirty migration removed local Persistent NativeArray/NativeList ownership, but scheduled Burst jobs over GlobalDataVault-resolved NativeArray views without using GlobalDataVault.TryLockBuffer/TryUnlockBuffer.
- GlobalDataVault TryAcquireWriteLock is not an external job pointer pin and does not set BlockFlagLocked/Reserved1 or ActiveBurstLockMask.
- HazardZoneManager public read model still resolves mutable vault views through HazardVaultArray indexers/properties in GetHazardIntensity/TrySampleHazardAvoidance paths.
- GlobalDataVault TryAcquireWriteLock does not enforce caller SystemID equals handle/meta owner outside collections checks.
- ReleaseBuffer rejects stale generations; HazardVaultArray.ReleaseBuffer clears descriptors without checking release success.

What was done:
- Static source audit only. No production code edits.
- Reviewed AGENTS.md, domain boundary, and mandates: GlobalRegistry DI, execution phases, signal lane segregation, native memory/jobs, zero GC, crash telemetry.
- Ran git status, git log since 2026-05-22, and targeted rg gates for TryGetLatestCreated, TryReadHandle, TryAcquireWriteLock, ReleaseBuffer, Allocator.Persistent.
- Verified findings against line-numbered source in HazardZoneManager.cs and GlobalDataVault.cs.

Cinematic cheats used:
- None. Audit only.

Exact microseconds saved:
- 0 us direct runtime change.
- Estimated potential if fixed: hazard point-sample read path can avoid repeated vault handle metadata resolution in O(N) loops; unmeasured microsecond-scale on i3/MX350.
- Estimated potential if fixed: DataVault relocation against live job pointers avoids catastrophic hitch/crash path; no legitimate average-frame number without profiler proof.

Build/verification:
- No build run. Prompt was read-only audit; no compile proof claimed.
