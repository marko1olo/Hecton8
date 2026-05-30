# Rationale_AUDIT_DOD_COMPUTE

Status: COMPLETE_STATIC_AUDIT
Date: 2026-05-30

Decision 0
Problem: The chat supplied a 10-point architecture questionnaire but no `<AGENT_PROMPT id="AUDIT_DOD_COMPUTE">` block exists in `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Use `AUDIT_DOD_COMPUTE` as an ad-hoc audit ID, set task count to 10, and treat the chat questionnaire as primary directive.
Rejected Alternatives: Fabricating a neighboring batch agent ID; relying on truncated memory instead of disk source.
Scalability potential: Audit covers low, middle, high, and ultra paths by checking continuous quality gates, not binary switches.
Hardware Impact: 0 us/frame. This is a static audit, not a runtime patch.

Decision 1
Problem: DataVault has 64B arena alignment, but that does not automatically stop false sharing inside arbitrary payload arrays.
Solution: Classify false-sharing status as partial. Evidence: `VaultBlockAlignment = 64`, explicit 64B telemetry/meta DTOs, and 128B SignalBus cursor padding; missing proof: universal payload SoA or per-element 64B padding.
Rejected Alternatives: Claiming arena block alignment protects adjacent element writes; padding every DTO blindly.
Scalability potential: Low tier needs owner-owned SoA and coarse batches; middle/high need cache-line-aware write partitioning; ultra can afford larger telemetry and overdraw lanes only after truth buffers stay isolated.
Hardware Impact: Potential savings on i3/MX350 are workload dependent; no microsecond number can be claimed without profiler.

Decision 2
Problem: Dense entity storage cannot be inferred from BufferID handles.
Solution: Classify DataVault as contiguous per-buffer native memory with bounded arena relocation, not a global ECS dense table. Per-entity swap-and-pop remains owner responsibility.
Rejected Alternatives: Describing the vault as sparse ECS or dense ECS globally.
Scalability potential: Low tier resolves handles once and scans linear owner arrays; middle/high add owner-level SoA compaction; ultra may keep visual-only mirrors larger while truth remains compact.
Hardware Impact: 0 us/frame proven. Reduced cache misses are plausible only inside owner buffers with measured linear scans.

Decision 3
Problem: CPU to GPU path has LockBufferForWrite and SetData fallback, so "pure zero-copy" is inaccurate.
Solution: Report current path as zero-GC/minimal-copy where LockBufferForWrite plus MemCpy is used, with SetData fallback and managed-array helper still present.
Rejected Alternatives: Labeling LockBufferForWrite upload as direct Vault-to-GPU zero-copy; banning all SetData without checking UAV/copy-destination constraints.
Scalability potential: Low tier should upload dirty pages only; middle/high use staging plus CopyBuffer; ultra spends saved bandwidth on optional visuals.
Hardware Impact: Pending profiler. Audit itself saves 0 us/frame.

Decision 4
Problem: MPSC producer order can vary under parallel writers.
Solution: Report determinism as lane-policy based. Mutation-critical lanes sort deterministic snapshots; noncritical/VFX lanes may coalesce, drop, or preserve reservation order.
Rejected Alternatives: Assuming CAS tail reservation equals deterministic gameplay order across cores.
Scalability potential: Low tier drops/coalesces presentation lanes; middle/high preserve larger frame limits; ultra expands visual signal budgets via `GlobalQualityWeight01`.
Hardware Impact: No measured value. Determinism value is correctness, not frame-time.

Decision 5
Problem: NativeQueue still exists in legacy/cold event lanes and tooling despite first-party SignalBus MPSC rings.
Solution: Classify lifecycle as partially controlled by central sentinels and explicit disposals, but not proven 100 percent by analyzer for every transient queue.
Rejected Alternatives: Claiming "no NativeQueue remains"; trusting Unity leak detector alone.
Scalability potential: Low tier uses bounded persistent lanes; middle/high can prewarm larger queues; ultra must not add unmanaged allocations in hot paths.
Hardware Impact: 0 us/frame proven.

Decision 6
Problem: DataMonolith and pager paths are binary/native, but some fallback file paths and async/cold managed constructs remain.
Solution: Report static-data boot path and world pager as binary native-oriented, while marking 0B GC chunk-boundary runtime proof pending.
Rejected Alternatives: Claiming all runtime persistence is parser-free because `static_data.h8bin` exists.
Scalability potential: Low tier uses fixed page pools and small read windows; middle/high increase residency; ultra prefetches more pages without changing truth layout.
Hardware Impact: Pending player profiler and disk trace.

Decision 7
Problem: First-party code avoids sync `GetData`, but async GPU readbacks are present for telemetry/queries.
Solution: Report render path as mostly one-way for first-party hot rendering, with delayed async readbacks not equivalent to synchronous CPU stalls.
Rejected Alternatives: Treating any readback as fatal; ignoring `AsyncGPUReadback.WaitAllRequests` teardown/config cases.
Scalability potential: Low tier disables optional readbacks; middle/high samples sparsely; ultra can retain telemetry readbacks with delayed consumption.
Hardware Impact: 0 us/frame proven by audit.

Decision 8
Problem: Dirty-page upload APIs exist, but no single global PCIe byte-budget owner was proven.
Solution: Report local quantization present and global transaction cap unproven.
Rejected Alternatives: Claiming every uploader respects one shared budget; bulk-uploading all buffers on every spike.
Scalability potential: Low tier uploads first dirty pages only; middle/high raise byte budgets; ultra increases optional buffer cadence through continuous quality.
Hardware Impact: Pending GPU/CPU frame capture.

Decision 9
Problem: Analyzer coverage is split across standalone CLIs, editor tests, and one real Roslyn analyzer.
Solution: Report compile-time guards as partial. Layout guards and audit tools exist; CI-wide Burst DTO enforcement is not proven from source scan.
Rejected Alternatives: Calling every audit CLI a compile-time gate.
Scalability potential: All tiers need the same ABI guarantees; quality may scale capacity/cadence, never DTO layout.
Hardware Impact: 0 us/frame.

Decision 10
Problem: Phase isolation must be source-proven, not inferred from Unity ordering.
Solution: Use `SystemDispatcher` master phases and PostSimulation job completion window as evidence; mark runtime scheduling proof pending.
Rejected Alternatives: Treating `DefaultExecutionOrder` or documentation as sufficient proof.
Scalability potential: Low tier sheds VisualSync; middle/high keep larger visual sync; ultra buys more visuals only after PostSimulation fences resolve.
Hardware Impact: 0 us/frame proven by audit.

Decision 11
Problem: The 10-point DoD/Compute questionnaire contains several multi-week engine rewrites mixed with one-day guardrail work. Treating all items as equal would waste agent time and risk destabilizing runtime ownership routes.
Solution: Prioritize one-day work that enforces existing mandates: static architecture gate aggregation, NativeQueue/SignalBus allowlist enforcement, central GPU upload budget telemetry/claim helper, deterministic SignalBus lane policy audit, and runtime proof harnesses for GC/upload/readback. Keep DataVault ECS rewrites, universal 64B padding, full render-path migration, and global streaming proof out of the 24-hour batch.
Rejected Alternatives: Padding every runtime DTO; rewriting GlobalDataVault into ECS dense tables; banning all AsyncGPUReadback; claiming pure zero-copy where LockBufferForWrite plus MemCpy is the real route; making broad code migrations without Frame Debugger/profiler evidence.
Scalability potential: Low/MX350 path gets bounded uploads, bounded signals, and static rejection of new hot-path violations. Middle/high/ultra paths can raise capacities through continuous `GlobalQualityWeight` without changing gameplay truth ownership, DTO layout, save identity, or authority route.
Hardware Impact: Static gates save 0 us/frame directly but prevent regressions. GPU byte-budget enforcement can reduce PCIe/main-thread spikes on i3/MX350, but exact microseconds remain pending until player/profiler capture.
