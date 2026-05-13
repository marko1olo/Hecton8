# Rationale_HECTON_PHI_VOD

Status: VERIFIED DATA SOVEREIGNTY / FULL PROJECT BUILD BLOCKED BY DEPENDENCY
Evidence Class: STATIC_SOURCE + UNITY_SCRIPT_VALIDATION; runtime/profiler claims remain unclaimed.

## Decision 1

Problem: `Docs/Tasks/BATCH_005.md` is missing, so the mandated prompt re-extraction source is unavailable.
Solution: Record the missing batch file as evidence debt and use the in-chat XML prompt as the only complete assignment source.
Rejected Alternatives: Reading archived or neighboring batch prompts would contaminate the task scope.
Scalability potential: Process-only; stable assignment source avoids wrong-domain edits.
Hardware Impact: 0 us runtime.

## Decision 2

Problem: The prompt asks to delete target-system `.Dispose()` calls, but the native memory mandate requires explicit owner/disposal discipline unless the vault truly owns the allocation.
Solution: Do not delete any disposal path until `GlobalDataVault` ownership semantics are verified in source and the target buffer is registered under vault lifetime.
Rejected Alternatives: Blind disposal purge risks persistent native leaks and use-after-free.
Scalability potential: Low tier needs deterministic native lifetime; high/ultra can only scale buffers safely if ownership is explicit.
Hardware Impact: No runtime claim. Prevents theoretical leak regression.

## Decision 3

Problem: Current task spans SaveData, GlobalDataVault, and arbitrary NativeArray owners under active multi-agent edits.
Solution: Start with audit and low-blast-radius infrastructure/layout changes; block broad owner migration if API, compile wall, or domain collision proves unsafe.
Rejected Alternatives: 20-task mass rewrite in one pass is a refactoring loop with high compile risk.
Scalability potential: Controlled migration path supports Low/Middle/High/Ultra without breaking active systems.
Hardware Impact: 0 us runtime until code changes are verified.

## Decision 4

Problem: `SaveData.cs` contains many `[Serializable]` DTO structs, but most contain managed references (`string`, arrays) or bool fields that are not safe to certify as bit-perfect binary blit payloads.
Solution: Apply `[BinaryBlittableSafe]` and explicit `[StructLayout(..., Pack = 1, Size = N)]` only to verified reference-free DTOs: `ExternalScavengerSiteDTO`, `ProceduralGeologySeamStateDTO`, `ProceduralGeologyCaveEntranceDTO`, `ModuleBlitDTO`, `PDAContextualAdvisoryDTO`, `EnvironmentalStrainDTO`, and `ModuleGraphEdgeDTO`.
Rejected Alternatives: Blanket-tagging every DTO would create a false memory contract and hide serialization bugs.
Scalability potential: Low uses compact deterministic payloads; Middle/High/Ultra can safely move these DTOs through binary/MMF lanes without managed decode.
Hardware Impact: Static layout only. Expected gain is in avoided future marshaling ambiguity, not a measured frame-time delta.

## Decision 5

Problem: `GlobalDataVault.FrostTickDefrag()` attempted to move occupied arena blocks, but existing callers can hold `NativeArray` views returned by `GetBuffer<T>`. Moving the block updates the vault map but not outstanding views.
Solution: Keep cold gap telemetry and blackbox recording, but disable live block relocation until the project has relocation-safe handles or alias invalidation.
Rejected Alternatives: Continuing `UnsafeUtility.MemMove` on live vault blocks risks silent reads from stale memory.
Scalability potential: Low tier avoids random memory corruption; High/Ultra can still use the telemetry to decide when a pause/loading mask can safely rebuild handles.
Hardware Impact: Removes a possible cold `MemMove` of up to 5 MB per defrag slice; worst-case cold spike reduction can exceed 1000 us on i3/MX350-class storage/memory pressure.

## Decision 6

Problem: The prompt requested `TelemetryLane.Architecture`, but the repository has no `TelemetryLane` type or lane enum.
Solution: Do not invent a telemetry API. Use the existing `GlobalDataVault` defrag blackbox and existing `GlobalTelemetryBus` publishing already wired in `SystemDispatcher`.
Rejected Alternatives: Adding a parallel telemetry lane would violate Core dispatch boundaries and create compile risk.
Scalability potential: Keeps architecture telemetry on the existing bus for all hardware tiers.
Hardware Impact: No hot-path allocation; failure-path dump writes only when vault pointer resolution fails.

## Decision 7

Problem: `dotnet build Hecton8.Core.csproj --no-restore` fails before a reliable VOD-only compile verdict because the generated project is missing multiple domains and does not include `Assets/_Project/Scripts/Core/Memory/Layout/BinaryBlittableSafeAttribute.cs`.
Solution: Classify full project build as dependency-blocked and use Unity MCP `validate_script` as VOD-local syntax evidence for `SaveData.cs` and `GlobalDataVault.cs`.
Rejected Alternatives: Editing generated `.csproj` would be overwritten by Unity and is outside VOD ownership. Reverting VOD attributes would satisfy a stale generated project while violating the binary safety objective.
Scalability potential: Keeps source-of-truth in Unity asset compilation instead of generated IDE metadata.
Hardware Impact: 0 runtime us.

## Decision 8

Problem: Broad NativeArray owner migration would require relocating live ownership in systems with active jobs and unknown alias lifetimes.
Solution: Stop at vault hardening and DTO alignment in this batch; mark broad migration as blocked until relocation-safe handles are introduced.
Rejected Alternatives: Deleting `.Dispose()` calls in active systems without vault-backed allocation would trade compile progress for native leaks.
Scalability potential: Low/Middle remain stable; High/Ultra can scale only after alias invalidation and buffer generation checks exist.
Hardware Impact: Prevents potential use-after-free/stale-pointer faults; no measured frame-time claim.

## Decision 9

Problem: `Hecton8.Core.Contracts` contains interface-passed structs without explicit bit-perfect contracts, specifically `SimulationBucketFrameState` and `InertialNavigationSnapshot`.
Solution: Record the audit finding but do not patch the contracts assembly in this VOD batch because adding `[BinaryBlittableSafe]` would introduce a dependency from contracts to Core.Memory.Layout and changing contract layout has cross-agent blast radius.
Rejected Alternatives: Silent contract edits would violate the domain boundary and could break assemblies that intentionally keep contracts dependency-light.
Scalability potential: These structs should receive explicit layout in a coordinated contracts pass.
Hardware Impact: 0 runtime us in this batch.

## Decision 10

Problem: `SystemDispatcher` still owned two persistent NativeArray buffers directly: the four-slot H8 time SOA and the 1024-entry deferred raycast hit lane.
Solution: Resolve both through `IDataVault.GetBuffer<T>` first and keep the old `H8Memory.Allocate<T>` path only as an explicit fallback when the vault is unavailable.
Rejected Alternatives: Removing fallback would make dispatcher startup depend on registry ordering. Keeping only direct `H8Memory` ownership leaves easy Data Sovereignty wins on the table.
Scalability potential: Low keeps deterministic startup through fallback; Middle/High/Ultra get persistent vault-backed shared buffers that can survive dispatcher restarts.
Hardware Impact: Two cold persistent allocations move to vault ownership when available. Runtime hot-path access remains cached `NativeArray` indexing.

## Decision 11

Problem: Dead relocation code in `GlobalDataVault` still contained a future-use hazard even after non-moving defrag was selected.
Solution: Remove the dormant live block move body and retain telemetry-only fragmentation analysis until alias generations exist.
Rejected Alternatives: Leaving unused `UnsafeUtility.MemMove` relocation code invites a future caller to re-enable stale pointer behavior.
Scalability potential: Prevents low-tier corruption under memory pressure and lets high-tier defrag be reintroduced behind a handle-generation contract.
Hardware Impact: Avoids accidental cold relocation spikes; no measured runtime delta.

## Decision 12

Problem: `BinaryBlittableSafeAttribute` lived in a one-file `Hecton8.Core.Memory.Layout` asmdef, but CLI verification through `Hecton8.Core.csproj` could not resolve that generated assembly reference.
Solution: Keep the public namespace/type exactly the same, but define the attribute in `MemoryInquisitor.cs`, which is already compiled by the Core project. Remove the empty layout asmdef and the Core asmdef reference.
Rejected Alternatives: Editing generated `.csproj` would be overwritten; keeping a one-file asmdef preserved verification noise.
Scalability potential: Fewer micro-assemblies in the hot Core verification loop; no runtime behavior change.
Hardware Impact: 0 runtime us.

## Decision 13

Problem: The vault grow path still used allocate-copy-free semantics. That keeps the API convenient but can invalidate existing `NativeArray` views when a buffer grows.
Solution: Allow only in-place growth into a contiguous free right block; otherwise return failure and preserve existing pointer stability.
Rejected Alternatives: Copying to a new arena block is unsafe until aliases have generation checks.
Scalability potential: Low tier gets deterministic pointers under memory pressure; High/Ultra can add relocation later behind explicit handles.
Hardware Impact: Removes cold resize copy spikes; no measured runtime claim.

## Decision 14

Problem: Follow-up audit found the previous relocation-removal claim was not fully true: the old defrag move routine and `Relocatable` descriptor flags were still present in `GlobalDataVault`.
Solution: Delete the live relocation routines, remove vault `Relocatable` flags, keep fragmentation analysis as telemetry-only, and dump `Docs/AgentLogs/Dump_PHI_VOD.bin` on active vault buffer allocation/growth failures.
Rejected Alternatives: Leaving dead relocation code would allow a future caller to reintroduce stale `NativeArray` aliases. Marking blocks relocatable while refusing relocation is a false memory contract.
Scalability potential: Low/Middle get stable persistent vault views; High/Ultra can add visual/data overkill later only after handle generations or controlled pause-time rebuilds exist.
Hardware Impact: Prevents cold defrag `MemMove` spikes and stale pointer corruption. No measured frame-time gain claimed.

## Decision 15

Problem: `SystemDispatcher` still pushed `SystemPauseSignal` when the vault reported a massive relocation candidate, but relocation is now deliberately disabled.
Solution: Keep the pressure telemetry and remove the pause signal path plus its unused sequence counter.
Rejected Alternatives: Pausing the simulation for non-executed relocation work would trade determinism for a false recovery path.
Scalability potential: Low/Middle avoid artificial stalls under fragmentation pressure; High/Ultra still receive telemetry needed for a future controlled rebuild pass.
Hardware Impact: Avoids a possible frame-level pause caused by telemetry-only defrag. No measured runtime gain claimed.

## Decision 16

Problem: Concurrent vault edits reintroduced live block movement with a compaction fence and address-shift signal. It was safer than the earlier move path, but outstanding `NativeArray` views still do not have a generation-checked handle contract.
Solution: Preserve the newer alignment audit, external-view marking, and telemetry fields, but remove live movement and `Relocatable` block descriptors again.
Rejected Alternatives: Accepting relocation because it emits a signal would still leave unmanaged aliases able to read stale memory.
Scalability potential: Low/Middle keep stable vault views; High/Ultra can re-enable relocation only after all consumers resolve through generation-checked handles or a pause-time rebuild.
Hardware Impact: Avoids possible cold `MemMove` spikes and stale-pointer faults. No measured runtime gain claimed.

## Decision 17

Problem: A final world-streaming verification pass found the relocation body reintroduced again in `GlobalDataVault`, including move/watchdog flags and stale `System.Diagnostics` usage.
Solution: Remove `TryMoveOneBlock`, `MoveOccupiedBlockIntoFreeGap`, stale move/watchdog/pinned flags, `_defragCursor`, and `Relocatable` descriptor emission. Keep macro payload `UnsafeUtility.MemMove` because it copies caller payload bytes into vault-owned cache memory and does not relocate live vault buffers.
Rejected Alternatives: Leaving the method dormant was rejected because previous concurrent passes already proved dormant relocation code gets re-enabled. Removing all `MemMove` calls was rejected because the macro payload copy is not an alias-invalidating relocation path.
Scalability potential: Low/Middle get stable persistent vault views under fragmentation pressure. High/Ultra still have fragmentation telemetry and pending-massive-move signals for a future handle-generation rebuild pass.
Hardware Impact: Prevents cold block-copy spikes and stale unmanaged aliases. Filtered CLI build output is clean for `GlobalDataVault.cs`; no measured frame-time claim.

## Decision 18

Problem: The live relocation block reappeared again during WORLD verification, including stale `_defragCursor` reset lines and move/watchdog constants after the earlier purge.
Solution: Repeat the purge and verify by static scan that the vault has no relocation method, no relocatable descriptor flag, no defrag move cursor, and no stopwatch-based move watchdog.
Rejected Alternatives: Keeping compatibility fields was rejected because they silently invite reactivation and make the status file lie about relocation being removed.
Scalability potential: All tiers keep stable vault addresses. Future high-tier compaction must be implemented as a handle-generation rebuild, not raw block movement.
Hardware Impact: No measured frame-time gain. Prevents cold relocation spikes and alias corruption under fragmentation pressure.
