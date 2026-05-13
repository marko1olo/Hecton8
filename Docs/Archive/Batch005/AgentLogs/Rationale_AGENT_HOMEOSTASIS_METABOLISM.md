# Rationale_AGENT_HOMEOSTASIS_METABOLISM

## Decision 0 - Prompt Authority
Problem: Local `Docs/Tasks/CURRENT_BATCH.md` does not contain `AGENT_HOMEOSTASIS_METABOLISM`, while the user supplied two duplicate XML tags for the same ID.
Solution: Use the user-supplied second tag as active directive because it is later in the prompt and contains 20 numbered tasks including the explicit rationale requirement.
Rejected Alternatives: Running `MEMORY_DEFRAGMENTATION_OVERSEER` from the local batch was rejected because it is a different ID. Merging both user tags was rejected because duplicate task numbering would corrupt state tracking.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process hygiene only.
Hardware Impact: 0 us/frame.

## Decision 2 - Compile-Visible Addressables Purge
Problem: The prompt requests Unity 6 `Addressables.Residency`, but the installed Addressables 2.7.6 package cache exposes no `Addressables.Residency` symbols.
Solution: Use immediate `Addressables.Release(handle)` plus dependency cache clear and AssetLifecycle pending-release drain on `SectorDehydratedSignal`.
Rejected Alternatives: Adding a direct call to a nonexistent Residency API was rejected because it would break compilation. Lazy unload-only was rejected because MX350/Quest memory pressure needs deterministic release pressure.
Scalability potential: Low releases chunk handles and cache entries aggressively. Middle keeps normal radius but purges dehydrated sectors. High/Ultra can keep wider residency through existing radius/tier settings without changing the purge contract.
Hardware Impact: On i3/MX350, expected VRAM recovery is asset-dependent; main-thread CPU cost is bounded to signal-drain plus handle release, estimated 5-40 us only on dehydration frames.

## Decision 3 - Vault Relocation Contract
Problem: Moving a vault block invalidates cached raw pointers unless every consumer hears about the address shift and no reader enters during the copy.
Solution: Keep compaction inside `GlobalDataVault`, audit gaps with a Burst `IJob`, move one adjacent block with `UnsafeUtility.MemMove`, wrap the copy in a compaction fence, and publish `MemoryAddressShiftSignal` after metadata/H8 descriptors are updated.
Rejected Alternatives: Full multi-block defrag was rejected because it risks a main-thread stall. `MemCpy` was rejected because adjacent defrag can overlap source/destination. Moving externally-viewed blocks was rejected because existing aliases cannot be rewritten safely without a generation contract.
Scalability potential: Low/Middle get one-block-per-FrostTick repair. High/Ultra with 32 GB RAM bypasses CPU defrag and spends cycles on visuals instead.
Hardware Impact: MX350/i3 avoids fragmented-vault OOM while limiting each slice to <=5 MB, estimated below 1 ms watchdog and usually 20-300 us depending block size.

## Decision 4 - VRAM Throttle Signal
Problem: VRAM pressure needed a concrete Gfx-used sampler and a broadcast when runtime texture mip residency changes.
Solution: Add the `Gfx.UsedMemory` profiler counter candidate to `VRAMMonitor`, max it into total VRAM, and publish `ResolutionChangedSignal` when `VRAMPressureMonitor` changes `QualitySettings.globalTextureMipmapLimit`.
Rejected Alternatives: Using `QualitySettings.masterTextureLimit` was rejected because Unity 6 routes texture mip residency through `globalTextureMipmapLimit`. Per-frame sampling was rejected because SlowTick already exists and the prompt calls for slow sampling.
Scalability potential: Low/MX350 clamps textures once redline pressure is present. Middle restores under hysteresis. High/Ultra can tolerate full mip residency longer through larger VRAM headroom.
Hardware Impact: Sampling remains SlowTick; mip drop trades texture detail for hundreds of MB of VRAM on constrained GPUs.

## Decision 5 - Adrenaline Purge Gate
Problem: Critical homeostasis pressure must stop speculative memory growth without hard-coupling to a brain implementation that is not present yet.
Solution: Consume both existing `MemoryPressureSignal` severity and the new `SystemHealthIndexSignal` SHI-critical equivalent, then suspend predictive streaming for a short window and trim half of inactive object-pool entries.
Rejected Alternatives: Direct dependency on `AGENT_HOMEOSTASIS_BRAIN` code was rejected because that code does not exist in source. Clearing all pools was rejected because 50% release matches the prompt and preserves warm pools.
Scalability potential: Low blocks speculation and releases inactive objects immediately. Middle uses the same gate with normal loading radius. High/Ultra mostly avoid entering the gate but still have the path for pathological spikes.
Hardware Impact: On low-end CPUs the trim occurs only on critical signals; estimated cost is proportional to destroyed inactive objects and not paid per frame.

## Decision 6 - MacroDB Breadcrumb Limits
Problem: The prompt asks for eviction of old distant MacroDB breadcrumbs/tombstones, but `MacroDatabasePayloadHandle` has no last-access day or tombstone-day field to enforce a precise 30-day age rule.
Solution: Wire the compile-visible `IMacroDatabaseService.EvictDistant` path into residency SlowTick with a persistent native scratch buffer. The service writes dirty payloads before eviction and removes distant cached sectors through the DataVault cache owner.
Rejected Alternatives: Adding age fields to `MacroDatabasePayloadHandle` was rejected because it would change the cross-domain file/cache contract without a migration owner. Editing `PersistentWorldRegistry` tombstone policy was rejected because it is outside the metabolism domain and already owns item tombstone decay separately.
Scalability potential: Low/Middle shed distant cached sectors during normal residency slow ticks. High/Ultra can retain wider MacroDB windows through the existing tier radius.
Hardware Impact: On MX350/i3 this frees native cache payloads in bounded batches of 128 sector hashes; estimated 10-80 us on slow ticks with cached far sectors.

## Decision 1 - Relevant Mandate Set
Problem: The task crosses native memory, DataVault, Addressables, streaming residency, telemetry, and registry boundaries.
Solution: Read eight mandate files: native memory/jobs, H8 arena allocator, Addressables lifecycle, world streaming residency, zero-GC, performance/VRAM, telemetry/postmortem, and GlobalRegistry DI.
Rejected Alternatives: Reading the entire registry was rejected because it creates context noise and increases risk of cross-domain contamination. Reading only Addressables docs was rejected because the defrag work is pointer-critical.
Scalability potential: Low/Middle/High/Ultra decisions will be recorded per implementation decision.
Hardware Impact: 0 us/frame.

## Decision 7 - OMEGA POLISH CHANGES / Concurrent Vault Wall
Problem: During OMEGA re-verification, `GlobalDataVault.cs` repeatedly reverted from a patched relocation implementation back to an audit-only variant while verification commands were running. This removed the stable `TryMoveOneBlock`/`MemoryAddressShiftSignal` publish path after three repair attempts.
Solution: Preserve the stable, low-risk parts that survived current-file verification: Burst-qualified `VaultGapAuditJob`, `math.rcp` fragmentation ratio, native audit result storage, and agent-specific blackbox dump path. Mark relocation execution and pointer-publish as blocked by concurrent dependency instead of reporting fake completion.
Rejected Alternatives: Fighting the concurrent writer indefinitely was rejected as a refactoring loop. Leaving dangling relocation calls was rejected because it risks compile break. Adding direct physics/consumer dependencies was rejected because the architecture requires SignalBus/GlobalRegistry decoupling.
Scalability potential: Low/Middle still get audit telemetry and VRAM/residency purge behavior. High/Ultra defrag bypass remains conceptually required, but stable FrostTick integration is blocked until the GlobalDataVault owner stops overwriting the method body. Low - audit only; Middle - audit plus purge; High/Ultra - no stable relocation until owner handoff.
Hardware Impact: Current stable gain on i3/MX350 is VRAM pressure relief plus fragmentation visibility. The intended memmove gain remains blocked; expected cost if restored is one <=5 MB move per FrostTick, typically 20-300 us with 0.1 ms watchdog flag.
