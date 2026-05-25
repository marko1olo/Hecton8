# Rationale_DataVaultNativeOwnershipAuditor

Problem: Audit needed DataVault/native ownership risks without modifying runtime code.
Solution: Static evidence pass against active dirty files and 2026-05-22..2026-05-25 commits. Focused on GlobalDataVault ownership contracts, HazardZoneManager dirty migration, TryGetLatestCreated fallback, release/generation paths, and persistent native allocation evidence.
Rejected Alternatives: Running a full build or broad refactor loop. The prompt required read-only verdict, and compile output would not prove native ownership correctness.
Scalability potential: Low uses stable snapshots and no relocation hazards; Middle keeps owner-local snapshots; High/Ultra can spend saved time on visual systems only after DataVault job fences and read-only surfaces are proven.
Hardware Impact: 0 us direct saved by audit. If fixed, expected i3/MX350 gain is mostly hitch avoidance: no DataVault relocation against live job pointers, no repeated per-element handle resolution, and fewer stale-handle leak paths.

Problem: HazardZoneManager moved from local Persistent NativeArray/NativeList ownership into GlobalDataVault buffers, then scheduled jobs on transient vault views.
Solution: Verdict requires DataVault job pointer pinning via TryLockBuffer/TryUnlockBuffer or an equivalent dispatcher-owned reader fence, not write-lock-as-pin.
Rejected Alternatives: Treating TryAcquireWriteLock as a job fence. GlobalDataVault defrag checks BlockFlagLocked/Reserved1 and active burst lock mask, while TryAcquireWriteLock only sets ActiveWriterSystemID.
Scalability potential: Low avoids relocation crashes; Middle/High/Ultra can safely grow hazard volume counts without pointer invalidation.
Hardware Impact: Prevents sporadic stalls/crashes; exact frame gain unmeasured, potential worst-frame recovery >100 us if relocation fault/dump is avoided.

Problem: Public read accessors can resolve mutable vault views repeatedly.
Solution: Read models need cached read-only snapshots per owner phase; Get/TryGet/Read paths must not resolve ownership or return mutable NativeArray.
Rejected Alternatives: Accepting TryResolveHandle inside point-sample loops. It preserves behavior but violates read-accessor purity and burns metadata lookups per sample.
Scalability potential: Low gets cheap stable arrays; High/Ultra can increase query density without turning DataVault into a hot dictionary.
Hardware Impact: Potential i3/MX350 saving depends on hazard query count; repeated handle resolution inside O(N) hazard loops is avoidable microsecond-scale CPU cost.

Problem: GlobalDataVault write-lock API accepts a caller SystemID but does not prove it matches the handle/meta owner in release builds.
Solution: Require owner match or explicit CoreDiagnostics/editor exception path before returning mutable NativeArray.
Rejected Alternatives: Relying on ENABLE_UNITY_COLLECTIONS_CHECKS owner validation. Player builds can compile that away.
Scalability potential: Prevents cross-domain write corruption from scaling with more systems.
Hardware Impact: No direct speed gain; avoids corruption recovery/dump cost.

Problem: Stale handles cannot release if generation changed, and HazardVaultArray clears its descriptor without checking ReleaseBuffer success.
Solution: Release path needs stale descriptor recovery by BufferID/SystemID or owner sweep proof before clearing local handles.
Rejected Alternatives: Fire-and-forget ReleaseBuffer. It hides leaks when ReleaseBuffer returns false on generation mismatch.
Scalability potential: Low memory stays flat after scene unload; High/Ultra can tolerate more vault buffers without orphan accumulation.
Hardware Impact: Prevents persistent arena growth; exact memory saved depends on stale buffer size.
