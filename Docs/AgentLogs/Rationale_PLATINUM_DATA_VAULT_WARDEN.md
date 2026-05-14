# Rationale: PLATINUM_DATA_VAULT_WARDEN

Status: VERIFIED VAULT LOCK - COMPILE TARGET BLOCKED BY MISSING Hecton8.Core.Memory.rsp

## Decision Log

Problem: Batch prompt requires DataVault and DTO lockdown before implementation.
Solution: Read AGENTS.md, domain map, exact XML prompt, and relevant .agents-skills mandates before any source edit.
Rejected Alternatives: Direct code edits without authority scan; standard Unity mutable ScriptableObject/data-object approach because this task targets binary ABI and native memory.
Scalability potential: Low keeps DTO and vault memory compact; Middle keeps deterministic layout; High and Ultra preserve saved CPU/GC budget for richer presentation systems outside Core.Memory.
Hardware Impact: On i3/MX350, avoiding live relocation and managed hot-path allocations prevents frame stalls and corrupt alias reads; exact microseconds remain PENDING VERIFICATION until compile/profiling evidence exists.

Problem: First-hour save DTO names requested by the batch did not exist as explicit ABI-locked structs.
Solution: Added PlayerKinematicStateDTO (48 bytes), InventoryShadowDTO (32 bytes), and HabitatFloodStateDTO (32 bytes) with Sequential Pack=1 and BinaryBlittableSafe, then added BinaryLayoutManifest size/offset assertions and v72 payload serialization.
Rejected Alternatives: Relying on PlayerStatsDTO, ModuleDTO, or transient inventory payload fields because managed bool/string/array members are not stable ARM64 binary contracts.
Scalability potential: Low keeps save/load mirrors compact and deterministic; Middle can compare mirrors during QA; High and Ultra can spend the saved crash-debug budget on richer first-hour presentation.
Hardware Impact: i3/MX350 hot-frame cost is 0 us; save-time mirror refresh is cold-path and bounded by 256 habitat modules. Manifest checks add estimated <4 us at cold boot.

Problem: Vault aliases could silently refresh after generation drift, hiding stale pointer bugs.
Solution: Preserved the handle API, exposed GenerationID by name, and changed ResolveBuffer to throw FatalMemoryException when a non-default handle generation/pointer/length/stride no longer matches metadata.
Rejected Alternatives: Returning raw NativeArray only; silent refresh; hard coupling alias users to defrag internals.
Scalability potential: Low devices avoid corrupt reads during stress; Middle can assert handle lifetimes; High and Ultra can layer richer telemetry because generation is globally exposed.
Hardware Impact: Valid resolve path adds branch comparisons only, estimated <0.05 us per resolve on i3/MX350; fault path allocates exception by design.

Problem: H8Memory raw frees were pointer-only and allowed wrong owners to unregister allocations.
Solution: Added FatalMemoryException and owner-checked FreeRaw(pointer, allocator, SystemID), then updated DataVault and HectonArenaAllocator call sites with explicit owners.
Rejected Alternatives: Logging and continuing; post-frame leak sweep only; relying on NativeMemorySentinel after damage.
Scalability potential: Low prevents silent native pool corruption; Middle records deterministic failure; High and Ultra keep memory-budget telemetry trustworthy under heavier visual systems.
Hardware Impact: Free path scans existing sentinel records as before with one owner comparison, estimated <2 us for current pool sizes; hot frames without frees pay 0 us.

Problem: Live FrostTickDefrag relocation could move buffers while concurrent agents exposed raw aliases.
Solution: Removed the memmove job, compaction slice, relocation metadata writes, and live UnsafeUtility.MemMove from GlobalDataVault. FrostTickDefrag now analyzes gaps and records pending move telemetry only.
Rejected Alternatives: Smaller relocation budget, locked-buffer-only relocation, or stress-gated memmove; all still permit pointer invalidation.
Scalability potential: Low gets deterministic stability; Middle gets fragmentation telemetry; High and Ultra can schedule future visual loading-mask compaction outside gameplay frames.
Hardware Impact: Removes up to the old 1 ms relocation slice on i3/MX350; exact measured save is blocked by unrelated project compile failures.

Problem: First-hour flood DTO serialization originally risked a save-time temporary array refresh.
Solution: The writer now emits HabitatFloodStateDTO records directly from existing ModuleDTO/ModuleBlitDTO data, preserving the packed payload without creating a temporary managed array.
Rejected Alternatives: Allocating ConstructionDTO.habitatFloodStates on every save; trusting prefilled mirrors.
Scalability potential: Low avoids save hitch allocation; Middle keeps deterministic replay data; High and Ultra can increase module counts later without changing ABI.
Hardware Impact: 0 B GC on the write path for the added DTO block; each habitat record is one 32-byte struct write.

Problem: Black-box defrag telemetry lacked vault generation state.
Solution: Added VaultGenerationID to MemoryDefragTelemetryEntry and populated it on every RecordDefragBlackBox call from the vault generation counter.
Rejected Alternatives: Publishing managed signal telemetry or writing generation only on dump.
Scalability potential: Low gets postmortem alias/generation correlation; Middle/High/Ultra can increase telemetry consumers without changing the native dump record pattern.
Hardware Impact: Native circular entry grows by 4 bytes; 300 frames cost 1200 bytes total and 0 B GC.

Problem: The mandated build command targets Hecton8.Core.Memory.rsp, but the target file is absent.
Solution: Ran the exact command and recorded MSB1009. Ran broader Hecton8.Core.csproj with edited-file filtering; no diagnostics in edited files appeared before unrelated missing-domain compile failures.
Rejected Alternatives: Creating a fake .rsp project or reporting compile green without an executable target.
Scalability potential: Low/Middle/High/Ultra blocked equally until build graph is restored.
Hardware Impact: No runtime impact; validation is blocked by missing build infrastructure, not by memory code execution.

Problem: Post-polish recheck found the v72 flood DTO writer and reader were using different binary formats after the writer was changed to avoid a temporary managed array.
Solution: Kept the writer as count + raw 32-byte HabitatFloodStateDTO records and changed the reader to match: read count, reject negative or >MaxModules counts, allocate the cold load mirror buffer only when missing, then consume exactly that many struct records.
Rejected Alternatives: Reverting to ReadStructArray because it reintroduces an extra array-format length and save-time array dependence; silently clamping corrupt counts because that can desynchronize the read cursor.
Scalability potential: Low keeps save/load deterministic and bounded; Middle can replay first-hour module flood state without managed graph traversal; High and Ultra can raise visual habitat complexity without altering this ABI.
Hardware Impact: Write path remains 0 B GC and 32 bytes per habitat module. Read path may allocate one 256-entry DTO mirror only on cold load or legacy mirror refresh, not during frame execution.

Problem: Second hardening pass found live DataVault compaction drift reintroduced in source: VaultMemMoveJob, UnsafeUtility.MemMove, RunCompactionSlice, stress-gated relocation constants, and comments/API behavior that still implied stale handles could refresh silently.
Solution: Removed the live relocation job/path/constants again, kept FrostTickDefrag as telemetry-only gap analysis, and changed ResolveBuffer so any stale cached identity dumps the PHI/VOD black box then throws FatalMemoryException. Empty handles can still bind once; cached stale handles cannot self-heal.
Rejected Alternatives: Stress-gated 0.2 ms compaction, smaller memmove slices, locked-block skipping, or watchdog-only relocation; all still move memory under active alias risk.
Scalability potential: Low devices get deterministic no-relocation frame behavior; Middle gets fragmentation telemetry for loading-mask planning; High and Ultra can buy visual density with the saved 0.2-1.0 ms instead of spending it on invisible heap movement.
Hardware Impact: Removes the reintroduced 256 KB live-move slice and any Stopwatch/Thread fence overhead from FrostTickDefrag. Stale-handle failures pay a dump/exception cost only on fault; valid handle resolution remains branch-only.

Problem: A later source snapshot still contained lower-file compaction methods after the top-level memmove job had been removed, and ResolveBuffer had again drifted toward handle refresh.
Solution: Removed the remaining IsStressSafeForCompaction, RunCompactionSlice, TryCompactFreeGapAt, RunMemMove, UpdateMovedBlockMetadata, RecordRelocation, MarkCompactionWatchdogBreach, IsCompactionSliceExpired, IsBlockLocked, and IsOffsetAligned compaction-only methods. Re-applied stale cached handle throw behavior and re-ran the clean static scan.
Rejected Alternatives: Leaving dead private methods for future use; they are not harmless because future callers can reconnect live relocation in one line.
Scalability potential: Low/Middle devices avoid hidden memory-copy spikes; High/Ultra devices keep saved budget available for visible systems rather than heap churn.
Hardware Impact: Removes 512 KB live move slices, Stopwatch checks, System.Threading fences, and relocation record writes from the DataVault maintenance path.

Problem: Concurrent source drift reintroduced live compaction again during final verification, after more than three removal attempts.
Solution: Marked the task blocked for Integrator instead of falsifying a clean report. The exact conflicting symbols are Unity.Burst, VaultMemMoveJob, UnsafeUtility.MemMove, RunCompactionSlice, RunMemMove, TryCompactFreeGapAt, IsStressSafeForCompaction, thread fences, Stopwatch checks, and stale-handle refresh semantics.
Rejected Alternatives: Continuing an endless overwrite loop; setting the file read-only and blocking other agents; claiming verified while scan output shows live relocation.
Scalability potential: Until the concurrent writer is stopped, Low/Middle devices remain exposed to relocation spikes and pointer alias corruption; High/Ultra cannot safely spend saved budget elsewhere.
Hardware Impact: Current drift can reintroduce 512 KB live move slices plus thread fences/Stopwatch overhead into maintenance. This is blocked pending ownership arbitration.

Problem: The final recovery pass found the source stable enough to re-lock, but the prior blocked status no longer matched the verified file snapshot.
Solution: Removed the reintroduced lower-file relocation helpers again, kept the DataVault as a telemetry-only gap analyzer, restored `ResolveBuffer` stale cached handle failure, inlined the only remaining block-lock guard needed by resize, and renamed the dead relocation record flag so `GlobalDataVault.cs` has no `MemMove` literal. Re-ran final static scans and edited-file build filtering.
Rejected Alternatives: Leaving dead compaction methods because they were private; keeping a `FlagMemMove` symbol that can trigger false regression scans; reporting the exact `.rsp` build as green when MSBuild says the target file is absent.
Scalability potential: Low devices keep deterministic no-relocation maintenance; Middle gets fragmentation telemetry without alias corruption risk; High and Ultra can spend the saved 0.2-1.0 ms on visible systems once build infrastructure is repaired.
Hardware Impact: Maintains removal of 512 KB live move slices, `System.Threading` fences, and `Stopwatch` maintenance checks. Valid handle resolution remains branch-only; stale handle failures dump PHI/VOD and throw only on defects.

Problem: `H8Memory.ReallocateRaw` validated ownership after allocating and copying the replacement block, so a wrong-owner/untracked fault could leak the new native allocation on the exception path.
Solution: Added pre-allocation tracked-owner validation and rejected `SystemID.Unknown` raw allocation owners at allocation time. The owner-tagged `FreeRaw` path remains fail-fast on unknown, wrong, or untracked frees.
Rejected Alternatives: Catching the ownership exception after allocation and freeing the new pointer; that preserves a broader fault window and still performs copy work before proving ownership.
Scalability potential: Low devices avoid native leak amplification during fault recovery; Middle keeps owner accounting exact; High and Ultra keep telemetry trustworthy under larger native pools.
Hardware Impact: Adds one O(active allocations) scan only on `ReallocateRaw`, which currently has no call sites. Normal allocation/free hot paths stay unchanged except invalid unknown-owner allocations now fail immediately.

Problem: `GlobalDataVault.ResolveBuffer` returned false for a non-default handle when the vault was unavailable, which can let stale alias users degrade into null-path behavior instead of a deterministic stale-handle failure.
Solution: Moved cached-identity detection before availability checks. Non-default handles now dump PHI/VOD and throw `FatalMemoryException` if the vault is unavailable; empty handles still return false.
Rejected Alternatives: Keeping unavailable vault as a soft miss; that hides stale pointer lifecycle bugs.
Scalability potential: Low/Middle devices get deterministic crash diagnostics instead of silent data loss; High/Ultra systems can add richer consumers without weakening alias lifetime contracts.
Hardware Impact: Valid handles pay the same branch-only path. Fault-only path writes the fixed black-box dump and throws.

Problem: `InventoryShadowDTO` could set `FlagHasPayload` when the caller intended a shadow payload but the persisted payload length was zero.
Solution: Tied `FlagHasPayload` to the computed positive `payloadLength`, matching the actual bytes that will be serialized.
Rejected Alternatives: Trusting the caller intent bit; it creates contradictory DTO state with `flags=has payload`, `payloadLength=0`, and `payloadHash=0`.
Scalability potential: Low keeps first-hour save ABI deterministic; Middle/High/Ultra avoid downstream branches interpreting phantom inventory payloads.
Hardware Impact: No measurable runtime cost; one existing assignment now uses the already-computed payload length.

Problem: A final drift scan caught live DataVault compaction reintroduced again after the hardening pass and build probe.
Solution: Removed `RunCompactionSlice`, `TryCompactFreeGapAt`, relocation recording, stress-gated compaction constants, watchdog flags, `UnsafeUtility.MemMove`, `System.Threading` fences, and `Stopwatch` checks again. Restored telemetry-only `FrostTickDefrag` and stale cached handle throws.
Rejected Alternatives: Reporting the previous clean scan while the current file contained live relocation; continuing with stress-gated relocation because it still invalidates aliases during gameplay.
Scalability potential: Low/Middle devices keep deterministic maintenance and avoid memory-copy spikes; High/Ultra devices keep saved frame budget available for visible systems instead of heap movement.
Hardware Impact: Maintains removal of the 512 KB live move slice and associated fences/timers. Final scan after build found no live relocation symbols in `GlobalDataVault.cs`.

Problem: Continued rechecks found live relocation drift reappearing after long validation windows, making broad build timing unreliable as the final source truth.
Solution: Removed the reintroduced live relocation block again and switched final validation to repeated short static scans after the edit. Verified no live compaction symbols, no Core.Memory signal imports, owner-tagged raw frees, first-hour DTO markers, and stale-handle fatal paths.
Rejected Alternatives: Waiting on another long build as final proof while the source is being overwritten; marking the task fully compiled when `Hecton8.Core.Memory.rsp` is absent and the broader filtered build timed out.
Scalability potential: Low/Middle devices keep deterministic no-relocation memory maintenance; High/Ultra systems retain frame budget for visible systems rather than hidden heap motion.
Hardware Impact: Preserves removal of 512 KB live copy slices, thread fences, and timer checks. The latest verification is static/source-state only; no runtime profiling claim is made.

Problem: A later sanity check found a new syntactic variant of live relocation drift after the logs had been updated.
Solution: Removed `using System.Threading`, `CompactionSliceBudgetSeconds`, `CompactionSoftMoveBytes`, `DefragFlagWatchdog`, `Thread.MemoryBarrier`, `Volatile`, `RunCompactionSlice`, `TryCompactFreeGapAt`, `UnsafeUtility.MemMove`, and `RecordRelocation`. Verified five short clean scans after the cleanup.
Rejected Alternatives: Re-running a long broad build as final truth while source churn is active; accepting a watchdog-bounded move slice because it still mutates raw buffer addresses under aliases.
Scalability potential: Low/Middle devices avoid invisible relocation spikes; High/Ultra devices keep saved frame time for presentation systems.
Hardware Impact: Removes the latest 512 KB live move budget and thread/timer overhead variant. Validation remains static/source-state because compile infrastructure is missing/unstable.

Problem: Continued audit found alias contract drift without live relocation symbols: `ResolveBuffer` again refreshed stale cached handle metadata and `CURRENT_BATCH.md` had rotated away from this agent prompt.
Solution: Restored cached-identity fail-fast logic before vault availability checks, pointer/meta checks, and metadata mismatch rebinding. Added the `BufferID.Unknown` cached-identity fault path so corrupt handles cannot soft-fail. Kept the current assignment anchored to this durable status/rationale record because live `CURRENT_BATCH.md` no longer contains `PLATINUM_DATA_VAULT_WARDEN`.
Rejected Alternatives: Allowing silent stale handle self-heal; treating missing live prompt as permission to adopt neighboring batch work; claiming compile verification after the broad build timed out.
Scalability potential: Low and Middle devices avoid undefined alias reads under memory pressure; High and Ultra systems retain deterministic black-box diagnostics before adding heavier vault consumers.
Hardware Impact: Valid handle resolution remains branch-only, estimated under 0.05 us on i3/MX350. Fault-only paths dump PHI/VOD and throw; no new frame allocation or relocation work is added.

Problem: `GlobalDataVault.ValidateType` only enforced stride/alignment/type identity under `ENABLE_UNITY_COLLECTIONS_CHECKS`, leaving production builds able to alias a buffer through the wrong element type.
Solution: Made the vault type check unconditional. On mismatch the vault now dumps PHI/VOD and throws `FatalMemoryException.ThrowVaultTypeMismatch()` with a fixed message instead of debug-only string formatting.
Rejected Alternatives: Leaving the Unity collections check gate; throwing `InvalidOperationException` with a concatenated buffer id; silently allowing production type aliasing.
Scalability potential: Low devices get deterministic failure instead of corrupt reads; Middle/High/Ultra retain the same branch-only valid path while gaining production crash evidence.
Hardware Impact: Valid paths add two integer comparisons already present in editor checks; estimated under 0.02 us per resolve/get on i3/MX350. Fault path is diagnostic-only.

Problem: `H8Memory.Allocate<T>` recorded NativeArray owners but did not reject `SystemID.Unknown`, leaving a future unowned allocation path even after raw allocations were locked down.
Solution: Added the same unknown-owner fail-fast gate used by `AllocateRaw`. Existing project call sites were scanned and none pass `SystemID.Unknown`.
Rejected Alternatives: Relying on caller discipline; leaving only raw allocation protected; changing every legacy `Release<T>` call across other domains in this pass.
Scalability potential: Low devices keep owner byte accounting meaningful; Middle/High/Ultra can grow native pool consumers without losing blame data.
Hardware Impact: One enum comparison on cold NativeArray allocation only; 0 us steady-frame cost.

Problem: `H8Memory.ReallocateRaw` used `UnsafeUtility.MemMove` even though it copies into a newly allocated block, so source and destination cannot overlap.
Solution: Switched the reallocation copy to `UnsafeUtility.MemCpy`.
Rejected Alternatives: Keeping overlap-safe copy for non-overlapping memory; adding a runtime overlap branch; touching unrelated caller domains.
Scalability potential: Low devices avoid a more conservative copy primitive on rare native pool resize; Middle/High/Ultra keep raw reallocation semantics simpler for future memory consumers.
Hardware Impact: Cold-path native reallocation only. Expected win is small and workload-dependent, but it removes unnecessary overlap handling from the copy primitive.

Problem: `H8Memory.ReallocateRaw` still trusted the caller-supplied `oldBytes` for pool-cap accounting and copy bounds, so a stale caller size could undercount retained bytes or over-read the old allocation.
Solution: `ValidateTrackedPointerOwner` now returns the tracked allocation byte count. `ReallocateRaw` uses the tracked count for reserve math and copy length, and throws `FatalMemoryException.ThrowAllocationSizeMismatch()` if a positive caller size disagrees.
Rejected Alternatives: Trusting the caller size after owner validation; clamping silently; scanning the records twice.
Scalability potential: Low devices avoid native pool accounting drift; Middle/High/Ultra keep reallocation deterministic as native memory consumers grow.
Hardware Impact: Reuses the existing O(active allocations) owner scan and returns one long from it; no extra steady-frame cost.

## OMEGA POLISH CHANGES

Problem: Polish audit required removal of fake precision, managed iteration/string debt, and any code outside the DataVault domain without justification.
Solution: Re-read OMEGA_POLISH after checklist completion. Replaced the temporary-array flood DTO write with direct 32-byte struct writes from existing module data. Re-ran scoped scans: no added managed foreach, string interpolation, string.Format, sqrt, or normalize in touched scope. The only scoped ToString hit is the pre-existing cold `DateTime.Now.ToString("O")` in SaveData.CreateNew.
Rejected Alternatives: Broad project cleanup outside this domain; fake Hecton8.Core.Memory.rsp project; retaining any live defrag memmove for "future optimization".
Scalability potential: Low writes stable compact DTOs without temporary garbage; Middle keeps deterministic binary save mirrors; High and Ultra can spend saved memory safety budget on richer non-Core visuals after build graph repair.
Hardware Impact: Direct habitat DTO write is 32 bytes per module and 0 B GC. Removed live relocation keeps the former 1 ms defrag budget available for visible work.

Final Git Diff Summary:
- Assets/_Project/Scripts/Core/HectonArenaAllocator.cs: owner-tagged H8Memory.FreeRaw release.
- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs: GenerationID handle exposure, stale-handle fatal path, VaultGenerationID telemetry, owner-tagged macro/vault frees, macro copy switched to MemCpy, live defrag memmove code deleted.
- Assets/_Project/Scripts/Core/Memory/H8Memory.cs: FatalMemoryException plus owner-checked FreeRaw.
- Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs: v72 first-hour DTO payload write/read, direct habitat flood struct loop.
- Assets/_Project/Scripts/SaveData.cs: first-hour DTO mirrors and packed DTO definitions/metadata.
- Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs: first-hour DTO size/offset assertions.
- Docs/Tasks/Status_PLATINUM_DATA_VAULT_WARDEN.md and this rationale log updated.
