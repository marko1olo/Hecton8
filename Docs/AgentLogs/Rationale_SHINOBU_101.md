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
