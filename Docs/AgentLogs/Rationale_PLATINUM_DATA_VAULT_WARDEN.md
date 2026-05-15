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

Problem: User-requested continued recheck found a new source snapshot where live DataVault compaction, stale-handle refresh, editor-only type validation, caller-trusted reallocation size, and raw macro payload version increments had drifted back into the locked scope.
Solution: Removed the live relocation slice and recorder again, kept `FrostTickDefrag` as analyze/validate/record telemetry only, restored stale cached handle PHI/VOD dump plus `FatalMemoryException`, moved `ValidateType` out of editor-only compilation, made `H8Memory.ReallocateRaw` use tracked old byte counts before allocation/copy, and switched macro payload overwrites to `NextGeneration(existing.Version)`.
Rejected Alternatives: Accepting stress-gated relocation; trusting caller-provided `oldBytes`; relying on Unity collection checks for type mismatch; using unchecked `existing.Version + 1u` that can wrap to zero.
Scalability potential: Low keeps DataVault deterministic and cheap under weak hardware; Middle keeps ABI/type failures diagnosable; High and Ultra can spend the saved relocation budget on visible systems instead of heap movement. Macro cache versions remain monotonic across long sessions.
Hardware Impact: Removes the reintroduced 512 KB live move path, Thread fences, and Stopwatch maintenance checks. `ReallocateRaw` adds one existing O(active allocations) ownership scan only on reallocation; valid handle resolves remain branch-only; macro version hardening is one overflow-safe increment on cache overwrite.

Problem: `H8Memory.Allocate<T>` and the old-pointer branch of `ReallocateRaw` could still accept `SystemID.Unknown`, creating or mutating tracked native records with no accountable owner while `AllocateRaw` already rejected unknown owners.
Solution: Added the same `FatalMemoryException.ThrowUnknownAllocationOwner()` gate to `Allocate<T>` and `ReallocateRaw`, then scanned project call sites for direct `SystemID.Unknown` use against `H8Memory.Allocate` and `H8Memory.AllocateRaw`.
Rejected Alternatives: Relying on later `Release<T>` cleanup or leak reaping; both happen after accountability is already lost.
Scalability potential: Low keeps native-array ownership deterministic; Middle/High/Ultra keep pool telemetry and leak attribution stable as larger systems allocate more SOA buffers.
Hardware Impact: One branch on cold/native-array allocation only; 0 us steady-frame impact for already allocated buffers.

Problem: The save writer appended the v72 first-hour DTO tail while writing `data.version` unchanged. Repair/manual rewrite paths can pass a loaded older `SaveData`, causing reload to skip the v72 tail by version gate and then fail payload byte-length validation.
Solution: Normalize `data.version` to `SaveData.CurrentVersion` inside `SaveBinaryPayloadCodec.TryWrite` before `WriteSaveData` emits the header and DTO tail.
Rejected Alternatives: Writing `SaveData.CurrentVersion` only in the header without mutating `data.version`; `SaveBinaryStorage` also writes prefix metadata from `data.version` after codec serialization. Skipping the DTO tail for older in-memory objects would preserve inconsistent repaired artifacts.
Scalability potential: Low keeps save repair deterministic; Middle/High/Ultra avoid backup promotion loops caused by self-created payload length mismatches.
Hardware Impact: One cold save-path integer compare/assign; 0 us frame impact and 0 B GC.

Problem: NativeArray disposal through `H8Memory.Release<T>` was still a weak ownership lane: legacy overloads could unregister without an explicit owner, created arrays/raw pointers became silent no-ops if the H8 tracker had already been shut down, and one power boot disposal chain ignored the final scheduled release handle.
Solution: Added owner-tagged immediate and job-deferred `Release<T>` overloads, marked legacy release/free overloads `Obsolete(error: true)`, converted external call sites to explicit owners, made created arrays/raw pointers throw `FatalMemoryException` when owner or tracker proof is missing, and completed the final power boot release dependency.
Rejected Alternatives: Keeping legacy release overloads for convenience; relying on NativeMemorySentinel after disposal; dropping created arrays when `_initialized` is false; scheduling final disposal without retaining the returned handle. Those hide ownership defects and can turn leaks into non-reproducible shutdown behavior.
Scalability potential: Low devices get deterministic native ownership and fewer silent leaks; Middle keeps pool accounting stable across scene churn; High and Ultra can run larger SOA buffers and visual caches without corrupting the memory budget model.
Hardware Impact: 0 us steady-frame cost. Disposal/free paths add one owner branch and the existing O(active allocations) owner lookup; failures now stop immediately instead of losing native memory silently. Completing the power boot disposal chain is cold teardown-only.

Problem: DataVault still carried false relocation signals after live compaction was locked out: a dead `_lastRelocationRecords` NativeArray was allocated every vault init, descriptors were flagged `Relocatable`, comments described handles as relocatable, and `GetBuffer` mapped `SystemID.Unknown` to `CoreDataVault`.
Solution: Removed the dead relocation-record allocation/disposal path, kept `TryGetLastRelocationRecord` as an empty compatibility surface, stopped setting the `Relocatable` descriptor flag, renamed comments to generation-checked handles, and made unknown `GetBuffer` requesters fail fast.
Rejected Alternatives: Leaving unused relocation storage for future work; keeping misleading descriptor flags; silently assigning unknown callers to CoreDataVault. All three weaken H-Phi data sovereignty by hiding true ownership.
Scalability potential: Low saves persistent native memory and avoids false relocation expectations; Middle gets cleaner telemetry; High and Ultra can reserve memory budget for real visual systems instead of dead bookkeeping.
Hardware Impact: Removes one 64 * 32 byte persistent relocation-record allocation, approximately 2048 bytes plus allocator overhead, at vault init. Runtime hot path impact is one cold requester-owner branch on `GetBuffer`; no dotnet rebuild was run per user order.

Problem: The H8 allocation flag enum and internal unregister helpers still exposed dead relocation/ownerless concepts after the active paths were removed.
Solution: Removed `H8AllocationFlags.Relocatable` and deleted the unused private `UnregisterPointer(void*)` shim so all H8 unregister calls require owner proof.
Rejected Alternatives: Keeping reserved symbols for hypothetical future relocation. In this codebase, dead symbols repeatedly became reconnection points for live compaction drift.
Scalability potential: Low/Middle devices avoid accidental reintroduction of hidden memory movement; High/Ultra keep the saved frame budget available for visible systems instead of heap churn.
Hardware Impact: 0 us runtime change. Static API surface is smaller; no allocation, branch, or frame cost added.

Problem: `ProceduralFaunaStateDTO` and `HibernatedFaunaStateDTO` stored managed `bool` fields inside structured save DTOs. The codec already wrote them as one-byte wire fields, but the in-memory DTOs were not safe native mirror candidates.
Solution: Replaced bool storage with fixed byte flags behind compatibility properties, preserved the existing codec wire format, added `[BinaryBlittableSafe]`, and asserted the sizes/flag offsets in `BinaryLayoutManifest`.
Rejected Alternatives: Changing the save wire format or renaming public DTO accessors; both would widen migration risk. Leaving managed bool fields would keep the ARM/native-blit hazard documented in `Save_Binary_Header.md`.
Scalability potential: Low gets deterministic ABI-safe fauna state loads; Middle can use DTO mirrors without managed bool ambiguity; High/Ultra can persist larger fauna state sets without changing binary layout again.
Hardware Impact: 0 us frame impact. Save/load still writes and reads the same bytes; property flag packing is cold persistence code only.

Problem: Several `[BinaryBlittableSafe]` `SaveData.cs` DTOs lacked explicit `BinaryLayoutManifest` size coverage, leaving the marker weaker than the enforcement.
Solution: Added manifest size/offset assertions for every currently marked blit-safe SaveData DTO, including external scavenger sites, geology seam/cave records, module blit records, PDA advisory, environmental strain, and module graph edge records.
Rejected Alternatives: Trusting `[StructLayout]` declarations without a central boot assertion; that lets accidental field drift survive until runtime persistence fails.
Scalability potential: Low catches binary drift before save/load corrupts data; Middle/High/Ultra can add more native persistence consumers with a single manifest gate.
Hardware Impact: Cold boot/static validation only. The added assertions are O(number of DTO fields checked) and 0 us steady-frame.

Problem: Procedural-world save arrays are capacity-backed, so the codec could serialize full backing capacity and the generic reader could allocate a corrupt over-limit array before domain max validation.
Solution: Wrote suppressed placement, fauna, geology seam, geology cave, and hibernated fauna arrays as logical bounded slices; clamped their mirrored count fields to array length and domain maxima; added bounded struct-array reads for generic procedural arrays; rejected custom fauna counts before allocation; and made `ProceduralWorldStateDTO.EnsureCapacity` copy existing entries when expanding shorter loaded arrays.
Rejected Alternatives: Keeping full-capacity writes for compatibility. The wire shape remains count + array payload, but the payload now reflects logical state instead of unused capacity. Trusting post-load migration was rejected because allocation happens before migration can clamp, and because no-copy expansion would discard compact payload entries.
Scalability potential: Low saves disk and decompression bandwidth on weak hardware; Middle keeps save corruption fail-fast; High and Ultra can increase procedural state detail without paying for empty capacity slots.
Hardware Impact: Cold save path adds bounded count clamps and removes up to approximately 240 KiB raw procedural-world payload when all capacity arrays are mostly empty: 8192 long suppressed keys, 4096 fauna records, 512 hibernated fauna records, 512 geology seam records, and 512 cave entrance records. Frame impact is 0 us.

Problem: Capacity repair was still allowed to rewrite compact loaded arrays before clamping logical count mirrors. A corrupt or stale count could survive as a full-capacity count after `EnsureCapacity`, and old no-copy repair paths could discard the only valid payload entries.
Solution: Centralized exact-capacity, copy-preserving array normalization in `SaveData.EnsureExactArrayCapacity`; made migration compute pre-expansion bounds for inventory, world, construction, exploration, PDA, lore, meta, resource scarcity, ecosystem, procedural world, encrypted audio fragments, and archaeology state before repair; added missing construction graph/flood count clamps and changed root lore count clamps to report mutation.
Rejected Alternatives: Post-expansion clamping to backing array length and resetting paired root arrays to count zero. Post-expansion clamps accept default entries as real state, and zero-resetting paired arrays destroys salvageable cold-load data.
Scalability potential: Low keeps save repair deterministic and avoids reload loops on weak hardware; Middle keeps compact payloads valid during migration; High and Ultra can increase save-state density without paying for unused max-capacity records or accepting silent default-entry pollution.
Hardware Impact: 0 us frame impact. Cold migration adds scalar bound checks and `Array.Copy` only when an array is normalized; avoiding full-capacity false counts prevents downstream restore loops over thousands of default entries, worst case approximately 4096 fauna/world records plus 8192 pickup/suppression records.

Problem: The fixed-capacity save codec needed a final allocation-bomb audit after capacity repair. Most logical-slice writers/readers were already locked, but the legacy `InventoryCellDTO` custom writer still delegated to the unbounded generic custom-array writer.
Solution: Verified fixed-capacity writer/readers for world, construction, scan, PDA, lore, resource scarcity, ecosystem, root bitmasks, module sorter buffers, and cultivation arrays; then routed `WriteInventoryCellArray` through `WriteCustomArraySlice` with `InventoryDTO.MaxCells`.
Rejected Alternatives: Leaving the legacy writer untouched because current inventory writing no longer calls it. Legacy persistence code remains a reconnection point during migrations, so it must carry the same max-cell invariant as the reader.
Scalability potential: Low devices avoid malformed legacy payload expansion; Middle keeps save repair deterministic; High and Ultra can keep larger DTO backings without allowing wire payloads to grow past logical caps.
Hardware Impact: 0 us frame impact. Cold legacy save path now caps the custom item-cell loop at 128 records and prevents oversized string-bearing inventory-cell payloads from being emitted.

Problem: Root legacy save collections still had unbounded binary read paths for compatibility lists, dictionaries, and hash sets. A malformed payload could force large managed allocations before migration could clamp or rebuild packed state.
Solution: Added explicit root collection caps in `SaveData` from producer evidence: 32 tool records, 108 legacy biome IDs, 1024 audio-log IDs, 1024 legacy quest IDs, 32 suit upgrade IDs, 16 corporate orders, 32 mission IDs, and 64 custom mod entries. `SaveBinaryPayloadCodec` now writes those collections through capped overloads and rejects over-limit counts before allocating during read. Corporate pending order IDs and timers use a paired-count clamp on write.
Rejected Alternatives: Trusting migration to trim after allocation; keeping generic unbounded list/dictionary/hashset readers for root compatibility fields; sorting/truncating dictionaries with temporary arrays. Read-time rejection is the only deterministic anti-bomb gate, and producer caps make write-side truncation a cold bug containment path rather than a gameplay feature.
Scalability potential: Low devices reject corrupt save payloads before heap pressure; Middle keeps legacy compatibility lists bounded while packed bitmasks and DTO arrays carry primary state; High and Ultra can keep richer save DTO capacity without letting compatibility maps scale with mod or corruption noise.
Hardware Impact: 0 us frame impact. Cold load now caps worst-case root compatibility allocation to 32/108/1024/1024/32/16/32/64 records instead of attacker-controlled counts; cold save adds scalar clamps and one paired-list min chain only.

Problem: The binary codec was bounded, but non-binary restore paths could still hand migration oversized legacy lists/maps from JSON, editor repair, or manual DTO construction.
Solution: Added cold migration trimming for tool durability maps, custom mod data, legacy biome/audio-log discovery collections, quest lists, suit upgrade lists, corporate order lists/timers, and mission lists. Dictionary/hash-set trimming uses repeated single-entry removal to avoid temporary key arrays; corporate pending order IDs/timers clamp to a shared paired count.
Rejected Alternatives: Assuming every restore path uses the binary codec; adding hot runtime guards in producer systems; sorting legacy dictionaries before trimming. Migration is the correct cold gate, and deterministic order is not guaranteed for these existing legacy dictionaries anyway.
Scalability potential: Low devices avoid oversized post-load compatibility containers even from non-binary saves; Middle keeps migration repair bounded and explicit; High and Ultra can keep rich primary DTO state without compatibility baggage growing beyond producer limits.
Hardware Impact: 0 us frame impact. Cold migration adds O(extra entries) list trims and O(extra entries * remaining dictionary count) dictionary/hash-set removal only when corrupt or oversized data is present.

Problem: Individual binary strings were only bounded by remaining payload bytes. A single corrupt length could still allocate a large managed string, and unused unbounded array helper methods remained as reconnection points beside the bounded helpers.
Solution: Capped every `SaveBinaryPayloadCodec` UTF-16 string at one protected 16 KiB block (`8192` chars) before writer copy or reader allocation, removed unused unbounded `WriteStringArray`, `ReadStringArray`, `WriteStructArray`, and `ReadStructArray` helper surfaces, and documented the string cap in `Save_Binary_Header.md`.
Rejected Alternatives: Per-field string limits in all 74 call sites; leaving dead unbounded helpers for convenience; permitting root `CustomModData` to carry large mod payloads. Mod persistent data already has protected indexed sectors, so root compatibility strings should remain bounded metadata.
Scalability potential: Low devices reject string bombs before heap pressure; Middle keeps root compatibility payloads predictable; High and Ultra can reserve large mod payloads for the protected sector path instead of expanding the root DTO surface.
Hardware Impact: 0 us frame impact. Cold save/read adds one integer compare per string. Worst single-string managed allocation is now bounded to 16 KiB of UTF-16 payload instead of being constrained only by full save payload length.

Problem: Private compatibility helper overloads still routed list/dictionary/hash-set/custom-array reads and writes through `int.MaxValue`, leaving dead surfaces that future root metadata could reconnect to unbounded allocation paths.
Solution: Deleted the no-cap wrapper overloads for string lists, float lists, string-float/string-bool/string-string dictionaries, int hash sets, and custom arrays. Removed optional default max parameters from legacy array conversion helpers so each caller must pass the domain cap explicitly.
Rejected Alternatives: Leaving private wrappers unused; adding comments that callers should prefer capped overloads. This project has repeatedly reconnected dead private code, so the safer contract is no unbounded overload.
Scalability potential: Low devices keep corrupt root metadata bounded before allocation; Middle keeps migration and binary compatibility lanes aligned; High and Ultra can grow fixed DTO capacity without widening compatibility helper debt.
Hardware Impact: 0 us frame impact. Cold save/load code loses wrapper calls and keeps the same capped loops; the gain is compile-time/API pressure against future unbounded reads.

Problem: DataVault black-box dumps used an old relocation-specific filename and `FrostTickDefrag` accepted NaN/Infinity inputs until later telemetry validation. Save migration also had a duplicate exact-capacity helper that could drift from `SaveData`, and the codec still had one unused custom-array wrapper that serialized by backing length instead of a named domain cap.
Solution: Renamed the defrag black-box path to `Docs/AgentLogs/Dump_PLATINUM_DATA_VAULT_WARDEN.bin`, kept PHI/VOD stale-handle dumps in an agent-scoped sidecar, added immediate non-finite input fault capture before gap analysis, routed migration capacity repair through `SaveData.EnsureExactArrayCapacity`, and removed the dead `WriteCustomArray` wrapper.
Rejected Alternatives: Keeping the old `Dump_VAULT_MEMORY_RELOCATOR.bin` path; treating non-finite defrag inputs as harmless because the current elapsed/stress values are not used for relocation; keeping duplicate helper code for locality; leaving a private wrapper because no current call site used it.
Scalability potential: Low devices get deterministic first-fault postmortem files without live compaction cost; Middle keeps repair behavior centralized; High and Ultra can expand DTO capacity or diagnostics without adding unbounded helper debt.
Hardware Impact: Valid defrag maintenance adds four scalar non-finite checks on a cold maintenance path, 0 us frame impact. Fault path writes the fixed 300-entry native telemetry ring once. Removing the duplicate helper and dead wrapper has no runtime cost.

Problem: `H8Memory.RegisterPointer` could silently return after native memory was already acquired if tracking or descriptor registration failed. Public allocation callers did not verify that registration succeeded, and block descriptor registration returned `-1` instead of growing when the descriptor list filled.
Solution: Made pointer registration return success/failure, made `Allocate<T>`, `AllocateRaw`, and `ReallocateRaw` free the new allocation and throw `FatalMemoryException` if registration fails, and made raw reallocation register the replacement before freeing the old tracked block. Descriptor registration now grows to `MaxTrackingCapacity` before returning `-1`.
Rejected Alternatives: Trusting pre-allocation capacity checks alone; returning default/null after a post-allocation tracking failure; freeing the old raw block before proving the replacement is tracked. Those paths either leak ownership evidence or risk losing the only valid block on tracker failure.
Scalability potential: Low devices avoid silent native leaks when tracking pressure rises; Middle keeps H8 owner byte telemetry and descriptor maps reliable; High and Ultra can run larger native pools without letting tracking failure corrupt memory sovereignty.
Hardware Impact: 0 us steady-frame impact. Valid allocation paths add one boolean check after registration. Failure path frees native memory immediately and throws; descriptor growth is cold and bounded by `MaxTrackingCapacity`.

Problem: Read-only alias creation accepted a `SystemID reader` but did not enforce that it was known, leaving an ownerless read-alias lane beside the owner-gated allocation/free paths.
Solution: Added `FatalMemoryException.ThrowUnknownAliasReader` and reject `SystemID.Unknown` in `GlobalDataVault.CreateAlias`, `H8Memory.CreateAlias(NativeArray<T>, SystemID)`, and the raw pointer alias helper.
Rejected Alternatives: Treating read aliases as harmless because they are read-only; read aliases still expose persistent vault memory and must be attributable during PHI/VOD triage.
Scalability potential: Low keeps alias access deterministic and attributable; Middle keeps shared-buffer debugging honest; High and Ultra can add more read-heavy consumers without widening anonymous memory access.
Hardware Impact: One branch on alias creation only, estimated <0.05 us on i3/MX350; 0 us steady-frame cost for already-held aliases.

Problem: `TryGetBufferHandle` built a generation handle without marking the underlying block as externally viewed. That left a handle-based external pointer lane outside the same metadata path used by `GetBuffer`/`TryGetBuffer`.
Solution: `TryBuildHandle` now marks the block as externally viewed, reloads pointer and metadata after the mark, revalidates the type contract, and writes the returned handle with the post-mark generation.
Rejected Alternatives: Leaving handle creation as metadata-only because live compaction is currently disabled; external-view metadata is still required for PHI/VOD attribution and future offline relocation planning.
Scalability potential: Low keeps handle telemetry exact; Middle can diagnose handle lifetimes consistently; High and Ultra can increase handle-based consumers without losing external-view accounting.
Hardware Impact: First handle extraction for a block may pay one block lookup and metadata update on a cold path; subsequent handles hit the external-view flag and return branch-only. Steady-frame cost for already-resolved handles remains 0 us.

Problem: DataVault sub-block descriptors could still be missing even after H8 allocation tracking was made all-or-nothing. Arena initialization and block splitting accepted `RegisterBlockDescriptor` returning `-1`, leaving sub-block memory-map evidence incomplete.
Solution: Arena initialization now dumps PHI/VOD, disposes the partially initialized vault, and throws `FatalMemoryException` if the root free-block descriptor cannot be registered. Block splitting now rejects the allocation and dumps PHI/VOD if the free-remainder descriptor cannot be registered before mutating the block list.
Rejected Alternatives: Allowing `H8BlockIndex = -1` as best-effort telemetry; missing descriptors make later PHI/VOD analysis lie about the real arena map.
Scalability potential: Low devices fail closed instead of running with incomplete memory evidence; Middle keeps block-map telemetry exact; High and Ultra can tolerate larger sub-block maps because descriptor storage already grows to `MaxTrackingCapacity`.
Hardware Impact: One cold descriptor-index branch during vault init and one during block split. 0 us frame impact after buffers are allocated.

## OMEGA POLISH CHANGES

Problem: Polish audit required removal of fake precision, managed iteration/string debt, and any code outside the DataVault domain without justification.
Solution: Re-read OMEGA_POLISH after checklist completion. Replaced the temporary-array flood DTO write with direct 32-byte struct writes from existing module data. Re-ran scoped scans: no added managed foreach, string interpolation, string.Format, sqrt, or normalize in touched scope. The only scoped ToString hit is the pre-existing cold `DateTime.Now.ToString("O")` in SaveData.CreateNew.
Rejected Alternatives: Broad project cleanup outside this domain; fake Hecton8.Core.Memory.rsp project; retaining any live defrag memmove for "future optimization".
Scalability potential: Low writes stable compact DTOs without temporary garbage; Middle keeps deterministic binary save mirrors; High and Ultra can spend saved memory safety budget on richer non-Core visuals after build graph repair.
Hardware Impact: Direct habitat DTO write is 32 bytes per module and 0 B GC. Removed live relocation keeps the former 1 ms defrag budget available for visible work.

Final Git Diff Summary:
- Assets/_Project/Scripts/Core/HectonArenaAllocator.cs: owner-tagged H8Memory.FreeRaw release.
- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs: GenerationID handle exposure, handle external-view marking, stale-handle fatal path, VaultGenerationID telemetry, owner-tagged macro/vault frees, macro copy switched to MemCpy, live defrag memmove code deleted, agent-scoped black-box dump paths, and non-finite defrag input dumping.
- Assets/_Project/Scripts/Core/Memory/H8Memory.cs: FatalMemoryException, owner-gated raw/native allocation, alias-reader gate, all-or-nothing allocation tracking, tracked-byte raw reallocation, descriptor capacity growth, and owner-checked FreeRaw.
- Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs: v72 first-hour DTO payload write/read, direct habitat flood struct loop, bounded root compatibility collections, 16 KiB string cap, and removed unbounded helper wrappers.
- Assets/_Project/Scripts/SaveData.cs: first-hour DTO mirrors and packed DTO definitions/metadata.
- Assets/_Project/Scripts/SaveDataMigration.cs: bounded cold restore clamps and canonical `SaveData.EnsureExactArrayCapacity` repair helper use.
- Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs: first-hour DTO size/offset assertions.
- Docs/Tasks/Status_PLATINUM_DATA_VAULT_WARDEN.md and this rationale log updated.
