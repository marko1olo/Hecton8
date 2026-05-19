# Rationale_SHINOBU_101

Status: PENDING VERIFICATION

## Initial Boundary Decision

Problem: Addressables streaming handle ownership is Echelon 1 memory infrastructure; unmanaged DTOs and release cadence can create global authority routes.

Solution: Limit edits to Addressables/memory infrastructure, native DTO contracts, editor-only tuner surface, and concise docs/logs. Any cross-domain dependency must be through existing registry/interface/vault/signal seams discovered in source.

Rejected Alternatives: Direct references to narrative, VRAM, or homeostasis concrete classes before source proof. That creates compile walls with 20+ concurrent agents.

Scalability potential: Low uses shortest TTL and conservative capacity pressure; Middle keeps normal TTL; High extends residency for backtracking; Ultra uses saved CPU to keep more visual assets resident and reduce visible swaps.

Hardware Impact: Target is avoiding managed dictionary rehash/GC spikes on i3/MX350. Static estimate before source proof: 50-500 us avoided on biome-boundary lookup storms; runtime proof absent.

## Mandate Selection

Problem: Task spans Addressables, native memory, Burst jobs, AUP, telemetry, editor tuning.

Solution: Apply selected mandates: STRM asset lifecycle, OPT native jobs, DATA ARM64 layout, OPT zero-GC, MATH AUP, DBG telemetry, ARCH phases, TOOL CSV/editor bridge.

Rejected Alternatives: Reading only AGENTS.md. The registry contains task-specific laws for runtime DTO layout, Burst, and designer data bridges that AGENTS.md only summarizes.

Scalability potential: Mandates force continuous quality weight, not binary tiers; release windows and TTL curves must scale from weak devices to Ultra.

Hardware Impact: Mandate compliance prevents hidden GC and unaligned native layout penalties on ARM64/Steam Deck/Quest-class devices. Exact gain pending profiler evidence.

## Runtime Storage Rewrite

Problem: Addressables residency used managed hash containers for hot lookup/release bookkeeping. Managed dictionary rehash and queue growth are unacceptable during biome-boundary asset churn.

Solution: Replace the hot `Dictionary`/`Queue`/`List` layer in `AssetLifecycleGovernor` with fixed managed arrays for non-blittable `AsyncOperationHandle` storage plus a Vault-owned `AddressableHeapHandleMap` open-address table. `AssetHandleMapEntryDTO` is explicit 64-byte layout and mutation goes through `GetEntryAsRef` over Vault memory.

Rejected Alternatives: `NativeHashMap` auto-growth and managed `Dictionary<uint, AsyncOperationHandle>` were rejected. The former can still reallocate; the latter keeps GC/rehash risk and pointer-chasing in the streaming hot path.

Scalability potential: Low keeps bounded slots and aggressive eviction; Middle retains stable TTL; High/Ultra can raise TTL without changing table shape, preserving no-resize behavior.

Hardware Impact: Static estimate only: avoiding managed dictionary probes/rehash during streaming storms targets roughly 50-500 us jitter reduction on i3/MX350-class CPUs. Profiler proof absent.

## Core Buffer ID Boundary

Problem: CSV tuning needed a Vault-owned scratch buffer, and SHINOBU_101 had to prove it was not inventing a private parser buffer or hijacking another domain's ID.

Solution: Verify the existing Core Memory `BufferID.AddressableHeapCsvScratch = 70329` authority and route CSV parsing through that Vault buffer. No SHINOBU_101 edit to `H8Memory.cs` is required for this ID; other dirty `H8Memory.cs` differences in the worktree are neighboring-lane changes and were not reverted.

Rejected Alternatives: A private `byte[]` or local `NativeArray<byte>` parser buffer was rejected because it violates H-PHI/DataVault ownership. Reusing an unrelated buffer ID was rejected because one fact needs one owner.

Scalability potential: Low devices parse designer TTL overrides without managed text allocations; High/Ultra keep the same hot-reload route with larger residency windows.

Hardware Impact: Cold/editor parse path only; no steady-frame claim.

## TTL, VRAM Panic, and AUP Eviction

Problem: TTL decay must run outside the main thread, while panic eviction must avoid unloading assets that were reacquired between evaluation and release.

Solution: `AssetTtlEvaluationJob` is `IJobParallelFor` with Burst synchronous compile flags and `[NoAlias]` fields. It mirrors refcount/TTL into the Vault map, subtracts player AUP from asset AUP before casting to `float3`, and applies the required continuous TTL curve. VRAM panic now selects the furthest 10% of unreferenced, unpinned assets via atomic zero-ref verification before queuing release.

Rejected Alternatives: Marking every unreferenced handle under VRAM panic was rejected because it over-evicts and creates avoidable visible churn. Absolute `Transform.position` scoring was rejected because it violates AUP precision rules.

Scalability potential: Low/thermal quality collapses TTL to 10%; Middle keeps default residency; High/Ultra stretch TTL to 300% and spend saved reload stalls on richer visual residency.

Hardware Impact: Static estimate only: Burst TTL pass moves O(n) decay off the main thread; panic selection is O(n*10%) but runs only under OOM-risk pressure where stutter is acceptable. Measured frame cost pending.

## Human Control and CSV Bridge

Problem: The editor facade used IMGUI/OnGUI and CSV ingest used `File.ReadAllText`, creating avoidable managed strings and failing the native scratch requirement.

Solution: Replaced the tuner with UI Toolkit, fixed graph element arrays, direct telemetry reads, sliders for TTL/VRAM thresholds, and leak banner display. CSV loading now uses `FileStream.Read(Span<byte>)` into Vault buffer `AddressableHeapCsvScratch`, then parses `ReadOnlySpan<byte>` with manual ASCII FNV-1a/float/uint parsing.

Rejected Alternatives: `string.Split`, `Regex`, `File.ReadAllText`, and IMGUI row generation were rejected. They allocate and obscure the runtime/editor boundary.

Scalability potential: Low devices consume compact binary/Vault records only; editor cost is isolated. High/Ultra users get live tuning without recompilation or runtime parser allocations.

Hardware Impact: Static estimate only: CSV parser is cold/editor path; runtime benefit is zero hot-path managed allocation from profile reload plumbing. Measured GC proof absent.

## Polish Pass: Release Gate and Unsafe Mock Removal

Problem: Static self-review found two architectural defects after the first report. `MockChunkLoadSpamJob` wrote the same tracker slots from different parallel indices through an unsafe pointer, and raw `AsyncOperationHandle` helper overloads called `Addressables.Release` immediately despite being named as a blind-frame gate.

Solution: Delete the unused mock spam job and its signal DTO; it was not referenced outside `AssetRecord.cs` and did not satisfy the SPSC or partitioned-write proof required for unsafe pointer jobs. Add a fixed 64-slot detached Addressables release bridge for non-registered handles. The only direct `Addressables.Release` source line now sits inside `TryExecuteOrDeferBlindFrameRelease`, which releases only during `IsBlindReleaseFrame()` or VRAM panic; otherwise it stores the handle for later gated drain.

Rejected Alternatives: Keeping `NativeDisableUnsafePtrRestriction` with a comment was rejected because the job had an actual slot aliasing race, not just a safety-system false positive. Directly releasing failed-registration handles was rejected because it violated Task 08 under visible frames.

Scalability potential: Low/thermal devices avoid visible-frame release stalls; Middle keeps ordinary release deferral; High/Ultra retain larger cache windows while still honoring the same hard release gate.

Hardware Impact: Static estimate only. The detached bridge is a fixed cold array, so it adds bounded memory and avoids unmanaged dictionary growth or visible-frame release spikes. Measured proof absent.

## Polish Pass: Compile-Wall AUP Boundary

Problem: `AssetLifecycleGovernor` used `Hecton8.World` only to call floating-origin helpers for fallback AUP. That created a direct source-level sibling-domain smell in Optimization runtime code.

Solution: Remove the direct `using Hecton8.World` from the governor. Player fallback AUP is reconstructed from `PlayerRuntimePoseSnapshot.Aup` using the contract-owned `HectonPhysicsContract.AupSectorSizeMetersDouble`. Exact chunk-center AUP stamping remains in `WorldChunkResidencyManager`, which is the owner that already knows chunk AUP.

Rejected Alternatives: Keeping the direct world namespace import was rejected because the required fallback can be computed from the Core player snapshot contract. Moving chunk-center ownership into Optimization was rejected because it would invert ownership; the world domain owns chunk coordinates.

Scalability potential: No visual tier change. This preserves the same TTL/eviction math while reducing compile-wall coupling.

Hardware Impact: Static architecture impact only. No frame-time saving claimed; compile graph risk is reduced.

## Polish Pass: TTL False-Sharing Boundary

Problem: `AssetTtlEvaluationJob` wrote release bits into `NativeArray<byte> trackerFlags` from parallel worker indices. Even with one index per worker, byte lanes colocate many handles on one 64-byte cache line, so adjacent workers can invalidate each other while flipping TTL bits.

Solution: Remove the byte flag array from the Burst TTL job. Before scheduling, mirror byte flags into the already 64-byte `AssetTrackerDTO.Flags` lane while preserving future high `uint` bits. The job reads/writes `AssetTrackerDTO.Flags`; completion mirrors the low byte back into the existing main-thread/editor flag mirror. A `_ttlEvaluationFlagsMirrored` guard prevents stale DTO flags from overwriting main-thread mutations when `TryPrepareTrackerMutation()` completes a job early. Remove byte flag writes from the parallel sanitizer clear job as well; the cold byte mirror is cleared sequentially after the 64-byte DTO/map clear.

Rejected Alternatives: Padding the existing byte buffer was rejected because it would require a new Vault DTO and a broad API migration. Documenting the write as "small" was rejected because the mandate is explicit about false sharing. Leaving the byte writes in Burst was too fragile for Quest-class cores.

Scalability potential: Low devices avoid cache-line ping-pong during TTL collapse. Middle/High/Ultra keep the same TTL math and can stretch residency without adding managed state.

Hardware Impact: Static estimate only. Expected gain is removal of worker cache invalidations in the 1 Hz TTL pass; exact microseconds require Burst profiler/Unity runtime proof, currently blocked by external Visor/Somatic compile ownership.

## Polish Pass: Editor Facade Text Churn

Problem: The UI Toolkit tuner had fixed graph elements, but metric labels and tracker rows formatted strings every 250 ms refresh even when values were unchanged. This is editor-only, yet it violated the intent of the zero-churn facade requirement.

Solution: Add fixed numeric caches for metric labels, leak banner identity, row hashes/refcounts/slots/TTL/flags, and visible-row count. Labels now assign text only on value change; graph bars still use preallocated `VisualElement` arrays.

Rejected Alternatives: Replacing UI Toolkit labels with custom text rendering was rejected as unnecessary editor scope. Leaving repeated string concatenation was rejected because change-gating is cheap and keeps the facade predictable.

Scalability potential: Low-end editor machines avoid avoidable UI refresh churn; High/Ultra machines get the same live telemetry without rebuilding runtime assemblies.

Hardware Impact: Editor-only static estimate. Runtime frame cost unchanged; editor GC churn is reduced to value-change events rather than every scheduled refresh.

## Compile Attempt 1 and Import Correction

Problem: The first allowed build attempt exposed `AssetRecord.cs` missing `using Unity.Mathematics;` after the DTO/job moved `double3`, `float3`, and `math` into that file. The same build also failed in unrelated `Visor/HectonVisorUberPostFeature.cs` references to missing reconstruction DTOs.

Solution: Add the required `Unity.Mathematics` import to `AssetRecord.cs`. Remove the stale unused `Hecton8.SaveSystem` import from `AssetLifecycleGovernor` during compile-wall scan.

Rejected Alternatives: Treating the build as purely external was rejected because two errors were in the SHINOBU file and had a direct import fix. Editing the Visor reconstruction feature was rejected as outside the Addressables heap defragmenter domain.

Scalability potential: No runtime tier change. This is compile hygiene only.

Hardware Impact: No frame-time claim. Build attempt 2 ran after CPU dropped again and reported no SHINOBU/Optimization errors; the remaining compile wall is external Visor/Somatic DTO ownership.

## Compile Attempt 2 Boundary

Problem: After the SHINOBU import fix, `Hecton8.Core.csproj` still fails compilation, but the errors no longer reference Optimization files.

Solution: Treat this as a dependency block rather than editing another domain. Remaining errors are missing reconstruction DTOs in `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` and missing comfort DTOs in `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs`.

Rejected Alternatives: Touching Visor or Somatic editor DTO ownership was rejected. It is outside Addressables heap defragmentation and would violate the domain boundary without a direct cross-domain interface need.

Scalability potential: No runtime tier change.

Hardware Impact: No SHINOBU frame-time impact. Verification is static plus partial compile evidence until the external DTO owners restore their contracts.

## Polish Pass: TTL Map-Entry Iteration

Problem: The TTL Burst job was structurally still doing tracker-slot iteration and then probing the open-address map to mirror refcount/TTL per tracker. That reintroduced an avoidable O(tracker slots * map probe length) pattern inside the kernel and contradicted the requirement that the Vault hash table be the bounded lookup surface.

Solution: Reorient `AssetTtlEvaluationJob.Execute` to iterate `AddressableHeapHandleMap` entries directly. Each occupied 64-byte map entry carries the pool slot, so the job updates `TimeToLiveSeconds[slot]`, `AssetTrackerDTO.Flags`, and the map entry itself without an inner probe. The job now validates `tracker.AssetHash == entry.AssetHash` before touching the slot, preventing stale occupied map entries from mutating the wrong tracker.

Rejected Alternatives: Keeping the mirror probe was rejected because it burns probe work inside the hot kernel. Iterating only the dense tracker array was rejected because map pressure and tombstone state live in the map, not in tracker order.

Scalability potential: Low devices reduce TTL kernel ALU/memory traffic under asset churn; Middle/High/Ultra keep the same residency curve while spending less CPU on bookkeeping.

Hardware Impact: Static estimate only. Worst-case kernel work drops from tracker count times probe walk to one pass over the fixed map. Measured microseconds still blocked by external compile errors.

## Polish Pass: Release Queue Idempotence

Problem: The fixed pending-release ring and detached raw Addressables handle bridge were bounded, but they accepted duplicate entries. Under repeated callbacks or repeated blocked drains, duplicate entries could consume ring capacity and risk a duplicate release attempt.

Solution: Add a fixed-ring `Contains(uint)` scan before pending-release enqueue and compare existing detached handles before storing a raw handle. No managed dictionary/list is introduced; scans are bounded by the fixed capacity and occur only on release staging.

Rejected Alternatives: A managed `HashSet` was rejected because it reintroduces heap allocation and resize risk. Ignoring duplicates was rejected because idempotent release ownership must be explicit.

Scalability potential: Low devices avoid release queue churn during visible-frame blocks; High/Ultra keep the same larger residency behavior without duplicate release risk.

Hardware Impact: Cold-path bounded O(n) scan trades tiny staging cost for preventing queue overflow and duplicate `Addressables.Release` pressure. No measured microseconds claimed.

## Polish Pass: Pending Release Ownership

Problem: Several paths set `AssetRecord.PendingRelease = true` before proving the fixed release ring accepted the key. If the ring was full, or if a queue-drained release hit a scheduled TTL job and could not clear its native slot, the record could remain pending without a queue owner.

Solution: Tie `PendingRelease` to successful `EnqueuePendingRelease(key)`. If enqueue fails, the record stays non-pending and telemetry dumps the fault. If native slot clear is blocked after dequeue, the key is immediately re-enqueued and only then kept pending.

Rejected Alternatives: Leaving the pending bit as an intent flag was rejected because one fact must have one owner; a pending release must be owned by either native flags or the release ring, not a silent boolean. Growing the queue was rejected because fixed capacity is a hard memory contract.

Scalability potential: Low devices under visible-frame release blocking avoid silent pending leaks; High/Ultra larger residency windows remain bounded and deterministic.

Hardware Impact: No measured frame claim. This prevents leak accumulation and queue owner loss under pressure.

## Compile Attempt 3 Boundary

Problem: After TTL map-entry iteration and release ownership hardening, a fresh compile probe was needed.

Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` only after CPU gate opened and no `dotnet/csc` process was present. The build still fails exclusively in external Visor reconstruction DTOs/IDs and Somatic comfort DTOs; no SHINOBU/Optimization compile errors are reported.

Rejected Alternatives: Editing external Visor/Somatic files was rejected again as outside domain ownership.

Scalability potential: No runtime tier change.

Hardware Impact: Compile-boundary proof only. Runtime profiler remains pending.

## Polish Pass: Tombstone Defragmentation

Problem: `AddressableHeapHandleMap` pressure counted occupied and tombstone entries, but the handler only forced eviction. A tombstone-heavy table could remain above the 85% pressure threshold and keep triggering eviction without recovering probe length.

Solution: Add `CompactAddressableHandleMap`, a no-allocation in-place rebuild. When used+tombstone pressure is high but live occupancy is below the threshold, the map is cleared and rebuilt from active tracker slots, existing handle-pool bundle prefix hashes, and current native refcounts. Shared-bundle flags are reset during rebuild and re-applied only when multiple active entries share the prefix.

Rejected Alternatives: Resizing was rejected because the assignment forbids resize under pressure. Allocating a temporary NativeList/Dictionary was rejected because persistent memory must remain Vault-owned and fixed. Ignoring tombstones was rejected because it lets linear probing rot over long sessions.

Scalability potential: Low devices avoid accumulating probe debt after repeated biome-boundary load/unload churn. High/Ultra keep longer residency without turning tombstones into permanent lookup cost.

Hardware Impact: Static estimate only. Compaction is cold pressure-path O(map + active slots), replacing repeated future probe penalties. Runtime profiler proof remains blocked by external compile failures.

## Polish Pass: Bundle Shared Mirror Consistency

Problem: The compaction rebuild re-applied `BundleShared` to map entries, but the current slot's byte mirror and 64-byte `AssetTrackerDTO.Flags` could lag until a later mirror pass. Existing shared-prefix entries also needed immediate DTO sync when `MarkBundlePrefixShared` mutated their byte flags.

Solution: Make bundle-sharing propagation update all three owners at once: `AssetHandleMapEntryDTO.Flags`, `AddressableHeapTrackerFlags`, and the low byte of `AssetTrackerDTO.Flags`. Registration, recompute, and tombstone compaction now pass the tracker DTO buffer into `MarkBundlePrefixShared`. Compaction also preserves the current TTL value when rebuilding each map entry.

Rejected Alternatives: Relying on the next TTL schedule to mirror byte flags was rejected because compaction is a state-repair path and must leave the map internally consistent before returning. Adding a temporary dictionary/list of bundle groups was rejected because the rebuild must stay fixed-capacity and Vault-owned.

Scalability potential: Low devices avoid repeated reload churn from missed shared-bundle TTL inflation after compaction. Middle/High/Ultra keep longer residency windows while map pressure recovery remains no-allocation.

Hardware Impact: Static estimate only. This prevents state skew that could shorten shared bundle TTL after tombstone cleanup; no measured microseconds claimed. Compile attempt 4 reports no SHINOBU/Optimization errors and remains blocked by external Visor/Somatic DTO ownership.

## Polish Pass: Blackbox Dump Identity

Problem: The Addressables heap telemetry dump still used a stale prior-agent filename, which mislabels crash evidence for this agent and breaks owner-local forensic routing.

Solution: Retarget the agent-specific heap dump to `Dump_SHINOBU_101_Addressables.bin` while keeping the shared `Dump_MEMORY_SURGEON.bin` mirror for memory-infrastructure triage.

Rejected Alternatives: Leaving the stale filename was rejected because blackbox artifacts must identify one owner. Removing the shared memory-surgeon dump was rejected because the prompt explicitly references the memory-surgeon forensic path.

Scalability potential: No runtime tier change. This is diagnostic authority hygiene.

Hardware Impact: Fault-path file name only; no steady-frame cost.

## Compile Attempt 4 Boundary

Problem: The compaction signature, bundle-shared mirror propagation, and dump identity changes required a fresh compile probe once the CPU/dotnet gate opened.

Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` only after CPU dropped below 50 and no `dotnet/csc` process was present. Result: no SHINOBU/Optimization errors. Remaining compile failures are unchanged external owners: `HectonVisorUberPostFeature.cs` missing reconstruction DTOs/IDs and `SomaticTunerWindow.cs` missing comfort DTOs. The project file also still warns that `SaveStateMerkleTree.cs` is specified twice.

Rejected Alternatives: Editing Visor reconstruction, Somatic comfort, or SaveSystem project hygiene from the Addressables heap lane was rejected because it violates owner-local routing and would expand the compile wall.

Scalability potential: No runtime tier change. This is verification boundary evidence.

Hardware Impact: No frame-time claim. SHINOBU runtime verification remains static/source-level until external compile owners restore their DTOs.

## Polish Pass: Release Ownership Preflight

Problem: `ExecuteReleaseFlow` cleared native slot ownership and removed the managed record after calling `TryExecuteOrDeferBlindFrameRelease(record.AddressableHandle)`, but it ignored a `false` return. If the visible frame was not blind and the fixed detached-release bridge was full, the last valid `AsyncOperationHandle` reference could be dropped while the actual `Addressables.Release` never executed.

Solution: Add `CanAcceptAddressableRelease(AsyncOperationHandle)` as a bounded preflight. The release flow now proves the handle can either execute immediately in a blind/panic frame or fit in the detached fixed bridge before clearing the native map slot. If the bridge cannot accept it, the record is preserved and re-owned by the fixed pending-release queue. The release counter now increments only when the handle is released in the current blind/panic frame, not when merely staged.

Rejected Alternatives: Growing the detached bridge was rejected because the capacity contract must stay fixed. Dropping the record and relying on telemetry was rejected because that converts a full queue into a silent Addressables leak.

Scalability potential: Low devices that spend longer outside blind frames keep release ownership without leaking handles. High/Ultra larger residency windows keep the same bounded bridge and do not add managed collections.

Hardware Impact: Cold release-path bounded scan over 64 detached slots before native ownership is cleared. No measured frame cost; it prevents lost-handle leaks under release pressure.

## Polish Pass: Hard-Reaper Addressables Handle Retry

Problem: The hard-reaper `Addressables.CleanBundleCache()` handle used the same release helper but also ignored a failed defer path. If the completed cache-clean handle could not fit in the detached bridge, the code could clear the field and lose the handle. The first preflight patch preserved the handle, but the active hard-reaper window had no retry pump.

Solution: Gate completed cache-clean handle release with `CanAcceptAddressableRelease()`. When it cannot be accepted, keep `_hardReaperBundleCacheCleanComplete=false` and leave the handle live. While `_hardReaperAsyncWindowActive` is true, `EvaluateHardMemoryReaper` now retries `PurgeAddressableCachesAsync()` and `TryCompleteHardReaperAsyncWindow()` every slow tick. Releasing an already completed clean handle now returns immediately instead of starting a second cache-clean operation.

Rejected Alternatives: Dropping the clean handle on bridge overflow was rejected for the same reason as asset handles: one release fact needs one owner. Starting another `CleanBundleCache` after releasing a completed handle was rejected because it can create an unbounded hard-reaper loop.

Scalability potential: Low devices that have fewer blind frames avoid losing cleanup handles under pressure; higher tiers retain the same hard-reaper cadence without extra allocations.

Hardware Impact: Cold hard-reaper path only. Adds one bounded 64-slot preflight scan and retry pump during the active cleanup window; no steady-frame cost claimed.

## Polish Pass: No-Owner Registration Failure Release

Problem: A failed `RegisterAddressableHandleSlot` can happen after `Addressables.LoadAssetAsync` has already returned a valid local handle. That handle is not yet owned by the Vault map or managed record. Calling the normal defer helper and then dropping the local variable can leak the handle if the visible-frame detached bridge is full.

Solution: Add `TryExecuteOrForceAddressableReleaseFault`. It first uses the normal blind-frame/deferred helper. If the handle has no durable owner and the detached bridge refuses it, the path raises a short external VRAM panic window and releases through the same helper under panic semantics. This is restricted to no-owner registration-failure cleanup; normal tracked records still preserve ownership and requeue instead of forcing visible release.

Rejected Alternatives: Adding a second overflow bridge was rejected because it creates another owner for the same release fact. Dropping the handle after telemetry was rejected because it leaks Unity Addressables state. Forcing all tracked release failures was rejected because tracked records can preserve ownership safely.

Scalability potential: Low-memory devices get a deterministic leak-prevention escape hatch when fixed release staging is saturated. High/Ultra systems keep normal blind-frame release behavior.

Hardware Impact: Fault path only. The cost is one failed bounded bridge scan plus a forced release under panic semantics; no normal-frame cost claimed.

## Compile Attempt 5 Boundary

Problem: Release-ownership hardening changed runtime helper signatures and failure routes, requiring a compiler probe after the CPU/dotnet gate opened.

Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal` after CPU dropped below 50 and no `dotnet/csc` process was present. Result: no SHINOBU/Optimization errors. Remaining failures are external: Visor reconstruction DTOs/IDs, Somatic comfort DTOs, and Construction `HeadlessDroneTask` references. Duplicate `SaveStateMerkleTree.cs` remains a project-file warning outside this lane.

Rejected Alternatives: Editing Visor, Somatic, Construction, or SaveSystem project ownership from this Addressables heap task was rejected. That would turn a local memory-infrastructure lane into a cross-domain compile-wall patch.

Scalability potential: No runtime tier change. This is verification boundary evidence.

Hardware Impact: No frame-time claim. SHINOBU runtime profiler proof remains blocked by external compile owners.

## Post-Polish Forensic Audit R6

Problem: The lower `LOG_SHINOBU_101.md` audit needed to reflect the latest ownership repairs, not the older intermediate state before tracked-handle preflight, hard-reaper retry, and no-owner registration failure handling.

Solution: Append a new R6 `<SELF_AUDIT>` block to the agent log. It explicitly reconciles all 20 tasks, maps 64-byte DTO layouts, states Vault buffer IDs 70323-70329, records the `[NoAlias]` job graph, documents the single direct `Addressables.Release` route, and marks build verification as blocked only by external Visor/Somatic/Construction owners.

Rejected Alternatives: Leaving the earlier audit as the latest evidence was rejected because it underreports the release ownership invariants. Rebuilding after documentation-only changes was rejected because attempt 5 already covers the current code and the CPU/build gate should not be wasted on log edits.

Scalability potential: No code-tier change. The audit preserves the evidence trail for low, middle, high, and ultra behavior already implemented by continuous TTL scaling and bounded Vault ownership.

Hardware Impact: Documentation-only. No new microsecond claim.
