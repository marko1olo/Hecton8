# Rationale_VAULT_MEMORY_RELOCATOR

STATUS: PENDING VERIFICATION

## Decision 0: Assignment Boundary
Problem: GlobalDataVault must relocate memory, but `Hecton8.Core.Memory` cannot depend on `Hecton8.Core` without creating an asmdef cycle.
Solution: Keep relocation state and handles inside `Hecton8.Core.Memory`; expose fixed-size relocation records through `IDataVault`; let `SystemDispatcher` publish the existing `MemoryAddressShiftSignal` lane from the Core assembly.
Rejected Alternatives: Direct `GlobalSignals.Publish` from GlobalDataVault was rejected because Memory is a lower-level assembly already referenced by Core. A new concrete event bus inside Memory was rejected because it would duplicate existing typed signal lanes.
Scalability potential: Low = no compaction while stressed; Middle = one pre-simulation slice per cadence; High = larger low-stress moves; Ultra = saved stability budget can support heavier visual memory residency.
Hardware Impact: i3/MX350 gain is reduced long-session fragmentation and fewer native allocation failures; direct frame savings are workload-dependent and unmeasured.

## Decision 1: Live Compaction Trigger
Problem: A telemetry-only defrag reports gaps but leaves arena holes intact during long sessions.
Solution: Gate actual memmove compaction behind `GapRatio > 0.15f` and `SystemStress < 0.5f`, then run inside the dispatcher pre-simulation fence.
Rejected Alternatives: Full defrag every FrostTick was rejected because moving native blocks while the frame is hot can exceed the 0.1 ms suspicion threshold. Runtime GC compaction is irrelevant to native arena fragmentation and was rejected.
Scalability potential: Low = skip compaction under pressure; Middle = bounded slices; High = more frequent low-stress maintenance; Ultra = more stable high-detail asset residency during long play sessions.
Hardware Impact: i3/MX350 avoids compaction during throttled frames; expected cost is bounded to a 1 ms watchdog, but runtime proof is pending.
