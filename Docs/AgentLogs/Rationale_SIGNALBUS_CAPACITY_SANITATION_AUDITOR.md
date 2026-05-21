# Rationale_SIGNALBUS_CAPACITY_SANITATION_AUDITOR

Problem: Bounded files configure typed SignalBus lanes with low-tier frame capacity lower than max/default. This can create route cliffs when a lane carries presentation truth or construction fallback data rather than optional cosmetic spam.
Solution: Static audit only. Read AGENTS, domain map, relevant mandates, bounded files, signal contracts, producers, consumers, and SignalBus frame limit math. Judged whether raising `lowTierFrameSignals` to max/default preserves truth by removing deterministic low-tier drops.
Rejected Alternatives: No source edit because mission says do not edit. No build because mission says no build. No broad `LowTier` audit because mission explicitly forbids unrelated LowTier naming.
Scalability potential: Low keeps signal routes truthful at bounded capacities; Middle keeps normal preview/audio response stable; High and Ultra may add visual overkill consumers in `VISUAL_SYNC` without changing authority or DTO layout.
Hardware Impact: MX350/i3 expected impact is negligible memory growth for the inspected lanes because `SignalBus<T>` already allocates snapshot buffer at `_maxFrameSignals`; `lowTierFrameSignals` changes runtime frame copy/drop limit, not snapshot allocation. CPU delta is tiny bounded extra copies only when more than the current survival limit arrives.

Decision: `WaterlineBreachSignal` low-tier limit 2 is suspicious but lower severity. The producer publishes only on camera waterline state change. Consumer in dynamic music reads all breach signals for impulses. Raising low-tier to max/default 8 preserves truth and prevents missed presentation impulses during bursty transitions; no gameplay authority changes.
Rejected Alternatives: Keep 2 and rely on drop-oldest. That makes surface breach audio response hardware-dependent.
Scalability potential: Low still bounded at 8. Middle/High/Ultra can spend higher-quality response in audio synthesis, not simulation.
Hardware Impact: Worst extra 6 x 64-byte copies in a flush, about 384 bytes of memory traffic; estimated below 1 us on i3/MX350, runtime proof absent.

Decision: `BaseStructuralWarningSignal` low-tier limit 4 is currently not proven as a route cliff in the scanned source because no `GetFrameSnapshot` consumer for that signal was found. Raising to max 32 would preserve any future warning truth, but current evidence does not prove a present loss.
Rejected Alternatives: Flag as critical without consumer. That would be fake severity.
Scalability potential: Low through Ultra can keep warning fan-out bounded; warning payload is fixed 64 bytes.
Hardware Impact: If consumed later, max extra 28 x 64-byte copies in flush, about 1792 bytes traffic; estimated below 3 us, runtime proof absent.

Decision: `ConstructionPreviewSignal` low-tier limit 1 is a real route cliff. The lane is produced by `PlayerBuilder`, consumed by `HectonBlueprintPreviewBatch`, and also used by `FoundationPylonGpuBatch.TryPopulatePreviewFallback`. Multiple active preview signals can be collapsed to one at low quality, changing preview batch contents and pylon fallback input.
Solution: Recommended sanitation is `lowTierFrameSignals: 8`, matching `maxFrameSignals`, in every `ConstructionPreviewSignal` configure site, including duplicate `PlayerBuilder` configure if source edits are later approved.
Rejected Alternatives: Keep 1 as quality scaling. That is binary capacity loss for a route carrying preview truth and fallback construction geometry.
Scalability potential: Low keeps all bounded preview signals; Middle/High/Ultra may vary visual detail via `GlobalQualityWeight` fields already in the payload.
Hardware Impact: Worst extra 7 x 128-byte copies in a flush, about 896 bytes traffic; estimated below 2 us on i3/MX350, runtime proof absent.
