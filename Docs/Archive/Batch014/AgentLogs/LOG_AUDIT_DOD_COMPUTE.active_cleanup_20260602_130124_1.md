# LOG_AUDIT_DOD_COMPUTE

## 2026-05-30 Static DoD Compute Architecture Audit

What was wrong:
- The prompt requested a hard architecture answer for 10 DoD/Compute topics but did not provide a matching batch XML prompt for `AUDIT_DOD_COMPUTE`.
- Several architecture claims would be false if answered from doctrine alone: pure zero-copy, full false-sharing exclusion, universal deterministic SignalBus ordering, global PCIe byte budget, and 100 percent transient NativeQueue lifecycle proof.

What was done:
- Read project authority files and mandates: `AGENTS.md`, `Docs/Actual Domains of Project.txt`, `DATA_Runtime_Struct_Layout_ARM64.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `ARCH_Signal_Lane_Segregation.txt`, `REND_GPU_Sovereignty.txt`, `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`, `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `ARCH_Execution_Phases.txt`, `OPT_HectonArenaAllocator_2_0.txt`, and `STRM_World_Streaming_Residency_Chunk_Management.txt`.
- Verified no `<AGENT_PROMPT id="AUDIT_DOD_COMPUTE">` exists in `Docs/Tasks/CURRENT_BATCH.md`.
- Audited core source evidence:
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`
  - `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`
  - `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs`
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs`
  - `Assets/_Project/Scripts/Core/NativeMemorySentinel.cs`
  - `Assets/_Project/Scripts/SaveBinaryStorage.cs`
  - `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`
  - `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`
  - `Tools/VaultNativeAliasRoslynAudit/Program.cs`
  - `Tools/SignalBusContractAuditCli/Program.cs`
  - `Tools/RoslynAnalyzers/Hecton8.PlatformNeutralityAnalyzer/PlatformNeutralityAnalyzer.cs`
- Updated status and rationale artifacts for the ad-hoc audit ID.

Cinematic Cheats used:
- None. This was architecture audit work, not a simulation or rendering implementation.
- Relevant existing code uses visual scalability lanes, dirty-page uploads, signal coalescing, and VisualSync shedding as performance currency. No new cheat was added.

Exact Microseconds saved:
- Audit work: 0 us/frame.
- No runtime code was changed, so claiming runtime savings would be fake.
- Potential savings from dirty-page uploads, LockBufferForWrite routes, SignalBus coalescing, and phase fencing require Unity player profiler proof before a number is legal.

Findings:
1. False sharing in GlobalDataVault: PARTIAL.
   Evidence: `VaultBlockAlignment = 64`, explicit 64B DataVault telemetry/meta DTOs, and 128B SignalBus cursor state with head/tail on separate cache lines. Gap: the vault does not universally force every payload element to 64B or SoA. Owner buffers and job partitioning must prove hot write isolation.

2. Dense vs sparse arrays: PARTIAL.
   Evidence: DataVault resolves handles into contiguous `NativeArray<T>` views over arena memory and has bounded PreSimulation relocation. Gap: this is contiguous per-buffer storage, not a global swap-and-pop ECS entity table. Dense entity locality is owner responsibility.

3. Hidden marshaling and zero-copy: PARTIAL.
   Evidence: hot upload helper prefers `GraphicsBuffer.LockBufferForWrite` plus guarded `UnsafeUtility.MemCpy`; many first-party paths use `GraphicsBuffer`. Gap: `SetData(NativeArray<T>)` and managed-array helper fallbacks exist. This is zero-GC/minimal-copy in good paths, not pure Vault-to-GPU zero-copy.

4. SignalBus determinism and race conditions: PARTIAL.
   Evidence: first-party MPSC ring uses CAS tail reservation and per-slot published tickets; frame snapshots can sort deterministic mutation lanes and coalesce damage/impact/acoustic signals. Gap: not every lane is sorted. Non-authoritative/VFX lanes can use reservation order, coalescing, or shedding.

5. NativeQueue lifecycle: PARTIAL.
   Evidence: SignalBus hot path uses H8Memory-owned MPSC rings; `NativeMemorySentinel.RegisterNativeQueue` exists; many queues are persistent/cold/prewarmed and manually disposed. Gap: legacy `NativeQueue` lanes remain, and no single proven analyzer enforces all transient `Dispose` paths.

6. Zero-allocation I/O: PARTIAL.
   Evidence: DataMonolith loads binary `static_data.h8bin` into Vault-backed native arena; `H8BinaryWorldPager` uses fixed page sizes, worker thread, native arrays, WAL, and 300-frame telemetry; `AsyncWriteManager` writes unmanaged pointers through native OS routes. Gap: FileStream/MMF/fallback paths and cold managed objects remain. 0B GC during chunk boundary streaming is pending runtime proof.

7. GPU-driven one-way pipeline: MOSTLY FIRST-PARTY HOT PATH, NOT ABSOLUTE.
   Evidence: first-party source scan found indirect draw, compute buffer binding, CopyBuffer, and no first-party synchronous `ComputeBuffer.GetData`/`GraphicsBuffer.GetData` in hot runtime scan. Gap: async GPU readbacks exist for telemetry/queries, and teardown/config can wait. Vendor/editor paths are separate contamination risks.

8. CPU to GPU quantization and delta updates: PARTIAL.
   Evidence: `GraphicsBufferUploadUtility` has dirty pages, max bytes per frame, deferred pages, and range uploads; SignalBus frame limits scale through `GlobalQualityWeight01`. Gap: no proven single global PCIe transaction owner across all uploaders.

9. Compile-time guards: PARTIAL.
   Evidence: `UnsafeUtility.SizeOf` and offset guards exist, `SignalBusContractAuditCli` scans Pack=1, managed events, hot allocations, Sync IO, and cache-line critical stride debt; `VaultNativeAliasRoslynAudit` scans persistent native aliases; PlatformNeutrality Roslyn analyzer exists. Gap: CI-wide enforcement for every Burst DTO/no managed ref/no bad packing was not proven.

10. Tick pipeline isolation: STRONG SOURCE EVIDENCE, RUNTIME PROOF PENDING.
   Evidence: `DispatcherPhase` is explicit; `RunMasterSimulationPhase` combines job handles; `RunMasterPostSimulationPhase` opens a swap window and completes pending simulation jobs; `RunMasterVisualSyncPhase` runs after PostSimulation and handles shader/global flushes. Gap: no Unity player run or profiler trace was executed in this audit.

Verification:
- Static source audit complete.
- No compile launched. No runtime code changed, and unnecessary build is forbidden by the project instructions.
- Runtime profiler and GC allocation proof are pending.

## 2026-05-30 One-Day Implementation Recommendation

What was wrong:
- The 10-point DoD/Compute questionnaire mixes real mandate gaps with broad engine rewrites. Not all of it is appropriate for a 24-hour parallel agent batch.
- The immediate risk is architectural regression from parallel work: new hot `NativeQueue` routes, SignalBus lanes without capacity/overflow contracts, direct hot `SetData/GetData`, hidden `.Complete()`, unmanaged allocations without sentinel registration, and unbudgeted CPU->GPU uploads.

What should be done first:
- Build one static architecture gate runner that aggregates existing audits and emits a single report artifact.
- Add a `NativeQueue`/`GlobalSignals` allowlist gate: every retained direct queue needs owner, drain phase, frame budget, overflow policy, and telemetry counter; new gameplay queues fail.
- Add a GPU upload byte-budget ledger around central upload helpers. Start with dirty-page upload paths only; do not rewrite every renderer in one batch.
- Extend SignalBus lane audits so gameplay-truth lanes require deterministic ordering, coalescing, or overflow policy documentation.
- Add runtime proof harnesses for GC bytes, upload bytes, deferred bytes, sync readback absence, and phase fences. Output is proof only after player/profiler data exists.

Cinematic Cheats used:
- None. This recommendation is architecture enforcement, not physical simulation or rendering polish.

Exact Microseconds saved:
- 0 us/frame proven by this recommendation.
- Static gates prevent regression. GPU byte-budget enforcement can reduce PCIe/main-thread spikes on i3/MX350, but exact values remain pending until player/profiler capture.
