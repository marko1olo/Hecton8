# Rationale_SHINOBU_77

Date: 2026-05-18
Agent: SHINOBU_77
Status: PENDING VERIFICATION / POLISH PASS ACTIVE / BUILD BLOCKED BY GUARD

## Decision 00: Domain Lock

Problem: SHINOBU_77 must repair Babel dictionary alignment without crossing into UI renderer ownership or AUP/spatial systems.
Solution: Constrain edits to Babel/localization/data ingestion and diagnostics; expose raw UTF-8 spans and hashes only.
Rejected Alternatives: UI string rendering, TMP mutation, and spatial trigger edits are outside Echelon 8 item 69 for this task and would create cross-domain coupling.
Scalability potential: Low uses throttled lookup cadence; Middle keeps full lookup with bounded per-frame budget; High/Ultra spend saved CPU on richer decrypted lore presentation downstream without bloating runtime DTOs.
Hardware Impact: Estimated low-end i3/MX350 gain is avoidance of ARM64 trap class and removal of managed string/dictionary churn; measured proof absent.

## Decision 01: Mandate Selection

Problem: The 1295-byte anomaly is both binary I/O and runtime memory layout risk.
Solution: Apply ARM64 struct layout, zero-GC localization, native memory/job discipline, telemetry blackbox, registry/signal boundary, and UI data streaming mandates.
Rejected Alternatives: `Dictionary<uint,string>`, packed runtime structs, managed locale tables, and renderer-owned text parsing were rejected because they violate cache locality and hot-path zero-GC law.
Scalability potential: Continuous `GlobalQualityWeight` throttles non-critical lookup population instead of a binary UI off switch.
Hardware Impact: Expected gain is bounded lookup work and aligned linear memory access; microsecond estimate pending source implementation and compile.

## Decision 02: Lore Decryption API

Problem: The runtime had low-level XOR jobs but no public path binding lookup, player-progress mask, and caller-owned decrypted output.
Solution: Added `TryBuildProgressDecryptionMask`, `LocRegistry.TrySetLoreDecryptionMask`, `LocRegistry.TryScheduleLoreDecryption`, and `BabelDictionaryStore.TryScheduleLoreDecryption`. Missing required lore bits generate a deterministic 16-byte mask; all collected bits clear the mask to zero, revealing clean UTF-8.
Rejected Alternatives: Decrypting into managed `string`, returning heap-owned `byte[]`, or letting the PDA/UI layer poke private Babel buffers. These would violate zero-GC and ownership boundaries.
Scalability potential: Low/Middle can decrypt only visible/requested fragments into caller buffers; High/Ultra can prefetch more fragments using the same Burst byte XOR without changing DTO layout.
Hardware Impact: Estimated low-end i3/MX350 gain is no main-thread string allocation and no cache-hostile dictionary traversal. XOR cost is O(n) over requested bytes; for a 256-byte lore line the work is expected below 10 us on weak CPU, pending profiler proof.

## Decision 03: Verification Boundary

Problem: Full compile is forbidden while CPU is under load and `dotnet`/`csc` already run.
Solution: Performed static source scans, direct binary header/length probe, and `VerifyBinaryHygiene.py` report generation. Deferred dotnet compile until guard is clear.
Rejected Alternatives: Launching another build into 100% CPU or reporting compile success without evidence.
Scalability potential: Verification remains cheap and does not create additional build contention while 20+ agents are active.
Hardware Impact: Prevents workstation contention. No runtime hardware gain; this is integration hygiene.

## Decision 04: Ultra-Think Polish Corrections

Problem: The previous pass left pointer safety invariants implicit and allowed the public pointer-backed lore decrypt scheduler to accept an uncreated mask even though Unity job safety can reject unconstructed NativeArray fields at schedule time.
Solution: Require a created progress mask for `TryScheduleLoreDecryption`; clean text is represented by the existing 16-byte zero mask. Add three-part safety comments to raw pointer job fields and annotate the two `Complete()` calls as explicit structural sync points.
Rejected Alternatives: Scheduling with a default mask and relying on `NativeArray.IsCreated` inside `Execute` was rejected because the scheduler can validate containers before Burst executes. Removing the `Complete()` calls outright was rejected because CSV/staged-locale mutation must not race active UTF-8 reader jobs.
Scalability potential: Low/Middle request fewer visible text slices and decrypt only visible fragments; High/Ultra can prefetch more lore fragments with the same zero-GC job path. No binary low/high switch was added.
Hardware Impact: Estimated low-end i3/MX350 impact is fewer schedule-time safety failures and no extra source copy before XOR decrypt. Runtime microsecond delta is unchanged for valid calls; measured proof absent.

## Decision 05: Compile Wall Caveat

Problem: The polish mandate demands no sibling Runtime assembly references, but Babel currently sits inside the monolithic `Hecton8.Core` asmdef, and that asmdef already references multiple sibling runtime assemblies.
Solution: Record the compile-wall violation as pre-existing architecture debt and avoid mutating `Hecton8.Core.asmdef` from a Babel-local task.
Rejected Alternatives: Removing 16 sibling references from `Hecton8.Core.asmdef` was rejected because it would be a cross-domain integration operation with high compile break risk. Creating a new asmdef around the existing root-level `LocRegistry.cs` was rejected because it would change ownership and public assembly boundaries mid-batch.
Scalability potential: No runtime scalability effect. The correct future fix is an integrator-owned assembly extraction where Babel runtime depends on Contracts/Memory only.
Hardware Impact: No frame-time gain now; preserving the compile wall requires a dedicated integration pass, not a blind local edit.

## Decision 06: Pointer Job Lifetime Fence

Problem: `BabelDictionaryStore.TryScheduleLoreDecryption` schedules a job reading from an MMF/Vault raw pointer. Without a store-owned reader fence, `CloseFile()` could release the pointer during reload/shutdown before the job completes.
Solution: Add `_activeLoreReadHandle` and combine scheduled lore decrypt handles into it. `CloseFile()` completes the fence before releasing MMF/Vault pointers; completed fences are cleared non-blockingly on later schedules.
Rejected Alternatives: Trusting callers to never reload while jobs run was rejected because it is an undocumented temporal contract. Copying source bytes into an intermediate output before scheduling was rejected because it doubles bandwidth and defeats MMF.
Scalability potential: Low/Middle schedule fewer decrypt jobs through lookup throttling; High/Ultra may schedule more jobs, but release safety remains a single combined fence at structural reload/shutdown.
Hardware Impact: No hot-frame cost beyond one `JobHandle.CombineDependencies` per scheduled decrypt. Avoids use-after-free class on Quest/ARM64. Measured proof absent.
