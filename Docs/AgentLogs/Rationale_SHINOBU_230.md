# SHINOBU_230 Rationale

Status: IMPLEMENTED / POLISH PASS APPLIED / SUB-AGENT CONCURRENCY FINDINGS APPLIED / SCOPED BUILD BLOCKED BY EXTERNAL DELETED FILE

## Decision 01 - Charger Ownership Boundary

Problem: `BatteryCharger` and `BatteryChargerModule` exposed charger-named managed cadence paths. `BatteryChargerModule` still registered into `GlobalRegistry.RegisterSlowTickable`, and `BatteryCharger` retained a dead object-charging loop after an early return.

Solution: Removed charger slow-tick registration from `BatteryChargerModule`, changed charger power rating to zero, reduced `BatteryCharger.SlowTick()` to a cold compatibility no-op, and made `BatteryCharger` register only integer `ChargerLinkDTO` handles.

Rejected Alternatives: Keeping managed `SlowTick` as "rare enough" was rejected; it keeps a per-object execution surface and invalidates the static scanner proof. Inventing a tool-battery SOA link for `BatteryChargerModule` was rejected because no authoritative tool inventory slot contract exists in this domain.

Scalability potential: Low tier has no charger MonoBehaviour charge execution. Middle/high/ultra tiers spend cycles in one Burst batch and GPU-side indicators, not per-prefab scripts.

Hardware Impact: Estimated low-end i3/MX350 saving is 20-80 us per 100 active chargers by removing managed dispatch, grid dirty churn, and component-side charge loops.

## Decision 02 - Inventory As Source Of Battery Truth

Problem: Charger state previously lived in `BatterySlot` managed objects and `ItemData` references. That is heap-scattered and cannot be snapshotted or charged by a SIMD-friendly kernel.

Solution: Physical UI remains a cold facade, but hot charge truth is `InventorySlotDTO.ItemHashID`, `Quantity`, `ConditionFlags` as `math.asuint(float charge01)`, and `ReservedLock`. Insert/remove writes one SOA slot state; job reads only the native array.

Rejected Alternatives: `Battery[]`, `List<Battery>`, `Dictionary<Slot,Battery>`, and `GetComponent` lookup were rejected because they create cache misses, managed iteration, and dependency on scene object lifetime.

Scalability potential: Low uses the same slot schema with fewer simulation ticks. Middle/high/ultra use identical data layout with larger link counts and richer visual buffers.

Hardware Impact: Estimated saving is 10-30 us per 1,000 batteries versus managed slot traversal on weak desktop/mobile CPUs.

## Decision 03 - ARM64 DTO Layout

Problem: Linkage data must bridge inventory and power graph without CS1612 copies or ARM64 misalignment.

Solution: `ChargerLinkDTO` is `[StructLayout(LayoutKind.Explicit, Size = 32)]`: offset 0 `InventorySlotIndex`, 4 `PowerGraphNodeIndex`, 8 `ChargeRate`, 12 `EfficiencyScalar`, 16 `Flags`, offsets 20-31 explicit byte padding. Layout audit uses `UnsafeUtility.SizeOf`, `AlignOf`, and `GetFieldOffset`.

Rejected Alternatives: Auto-layout structs and C# properties were rejected because they obscure offsets and can force defensive copies during Burst iteration.

Scalability potential: Low-to-ultra all use one 32-byte DTO. Higher tiers increase presentation richness, not layout complexity.

Hardware Impact: 32-byte contiguous links keep two links per 64-byte cache line; expected gain is 5-15 us per 5,000 links compared with scattered object references.

## Decision 04 - Emergency 5,000 Link Mock Grid

Problem: The charger kernel cannot wait on live base/inventory authoring to provide dense test data.

Solution: `GenerateMockChargerNetworkJob` fills 5,000 `ChargerLinkDTO`, SOA inventory slots, power nodes, node hashes, AUPs, and visual states. Runtime fallback schedules the job as `IJobParallelFor` with Burst and exposes the generated records only after the dispatcher fence reports completion.

Rejected Alternatives: Hand-authored scene prefabs and editor-only mock MonoBehaviours were rejected because they test object plumbing, not math throughput.

Scalability potential: Low can execute the same mock with throttled cadence. Middle/high/ultra validate full link count and visual overkill buffers.

Hardware Impact: Mock generation is cold. Runtime proof target remains under 10 us for 1,000 links and suspicious above 100 us for 5,000 links until profiler evidence exists.

## Decision 05 - Atomic Energy Transaction

Problem: Inventory slots and power graph nodes can be touched by separate systems. A charge transfer must not clone or destroy energy under contention.

Solution: Job acquires `InventorySlotDTO.ReservedLock` using `Interlocked.CompareExchange`, CAS-writes `ConditionFlags`, CAS-deducts `PowerNodeDTO.Potential`, and rolls inventory charge back with `Interlocked.Exchange` under the same slot lock if the CSR CAS loses. Atomic conflict counters and fault flags are written through Interlocked operations.

Rejected Alternatives: Plain writes, `lock`, `Monitor`, `NativeParallelHashMap`, and bounded power-refund loops were rejected. Managed locks are illegal in Burst/hot path; hash maps are extra indirection for fixed slot-to-node links; power-refund retries are not an absolute conservation proof.

Scalability potential: Low tiers reduce cadence, not correctness. Ultra tiers can add richer telemetry; transaction math remains identical.

Hardware Impact: Interlocked cost is paid only on contested writes. Expected steady-state cost is 10-40 us for 5,000 links on desktop CPUs; exact Unity profiler data still blocked by CPU guard.

## Decision 06 - Continuous Cadence And Efficiency Curve

Problem: Charging every frame is unnecessary, but changing cadence must not change economy.

Solution: Scheduler maps `HomeostasisBrain.GlobalQualityWeight` through `smoothstep` to 5-60 Hz and passes accumulated `dt` into the Burst job. Transfer uses `pow(max(0.0001, 1 - charge01), EfficiencyCurveExponent)` for internal-resistance cheat.

Rejected Alternatives: Binary low/high switches and fixed 60 Hz simulation were rejected because they waste low-end CPU and violate continuous quality weight rules.

Scalability potential: Low 5 Hz, Middle ~24-36 Hz, High ~45-55 Hz, Ultra 60 Hz plus richer visual/audio output. Energy over time is conserved by integrated `dt`.

Hardware Impact: Low-end can shed up to ~91% of charger scheduling frequency while preserving time-to-full.

## Decision 07 - GPU LED Dear Lie

Problem: Per-charger material or `MaterialPropertyBlock` writes move visual status through CPU renderer state.

Solution: Removed `MaterialPropertyBlock` allocation and renderer writes from `BatteryCharger`. Burst writes `ChargerVisualStateDTO` status values; `VISUAL_SYNC` double-buffers a global `GraphicsBuffer` and exposes `_H8BatteryChargerStatusBuffer`.

Rejected Alternatives: Changing emissive material colors from `BatteryCharger` was rejected because it creates CPU render work proportional to charger count.

Scalability potential: Low gets coarse LED status at no per-renderer CPU cost. Ultra can spend the same buffer on richer shader animation.

Hardware Impact: Estimated saving is 15-60 us per 100 visible chargers versus individual renderer property writes.

## Decision 08 - AUP Audio And Gizmo Precision

Problem: Charger hums and debug lines must not drift at far map coordinates.

Solution: Runtime stores charger and node `double3` AUPs. Audio emits unmanaged `AcousticPingSignal` with `AbsoluteUniversePosition`. Gizmo converts AUP to runtime-local `Vector3` only at draw time.

Rejected Alternatives: Attaching `AudioSource` components or storing world `float3` positions was rejected due component count and 100 km precision loss.

Scalability potential: Low emits fewer signals through existing SignalBus limits. Ultra can render more x-ray/gizmo detail without changing the simulation contract.

Hardware Impact: Removes AudioSource overhead; estimated saving is 10-40 us during active charger clusters plus lower scene object memory.

## Decision 09 - Black Box, X-Ray, CSV, Scanner

Problem: Without forensic telemetry and static proof, charger optimization cannot be audited.

Solution: Added 300-entry `ChargerTelemetryEntry` ring, binary dump to `Docs/AgentLogs/Dump_SHINOBU_230.bin`, UI Toolkit X-Ray window, span-based CSV parser, live gizmo, and `Charger_OOP_Scanner`. The shared report file preserves previous agent data in `reports[]`.

Rejected Alternatives: Console-only logging, `string.Split`, `float.Parse`, and overwriting shared JSON were rejected because they allocate, lose history, or damage concurrent agent output.

Scalability potential: Low keeps minimal telemetry and no per-frame UI. Ultra can inspect histograms and visual links while simulation remains flat native data.

Hardware Impact: Hot path stays zero-GC. Editor-only tools allocate in editor/cold paths only. Dump cost occurs only on >0.5 ms fault or NaN.

## Verification Constraint

Problem: Compile verification is required, but current machine CPU load stayed above 50% across repeated checks (55-100% observed) and no `dotnet`/`csc` process was active. User rule forbids build when CPU load is above 50%.

Solution: Did not launch `dotnet build`, `msbuild`, or `csc`. Ran static grep scanner, `git diff --check` for tracked edits, targeted forbidden-pattern scan, and re-read changed source.

Rejected Alternatives: Building under 100% CPU load was rejected because it directly violates the batch rule and would contaminate concurrent agents.

Scalability potential: Compile gate can be rerun unchanged once CPU drops below 50% and no compiler process exists.

Hardware Impact: No extra compiler load was added to an already saturated workstation.

## Decision 10 - Managed Facade State Eviction

Problem: The charger MonoBehaviour still retained `_slotChargedFlags` and `_registeredLinkIndices` managed arrays. Even though they were cold allocations, they preserved object-owned charging state and required each prefab to remember native link handles.

Solution: Removed both arrays and the resize helper. `BatteryCharger` now writes SOA inventory slot state and registers links without retaining link indices. Unregistration is a native range operation keyed by `inventorySlotStartIndex`, `slotCount`, and `powerGraphNodeIndex`.

Rejected Alternatives: Keeping a cold `int[]` handle cache was rejected because it makes the charger object a secondary owner of logistics truth. Using a managed dictionary from slot to link was rejected as worse heap fragmentation and an illegal hot-path temptation.

Scalability potential: Low tier pays zero per-charger heap allocations for logistics handles. Middle/high/ultra can spawn dense charger walls without multiplying object-owned state; the Vault remains the owner.

Hardware Impact: Low-end i3/MX350 avoids small managed array allocations per charger and removes stale handle churn during enable/disable. Runtime hot-path saving is structural rather than per-frame: the charge job still consumes only Vault arrays.

## Decision 11 - False Sharing Counter Lanes

Problem: The first counter implementation used one 64-byte `ChargerAtomicCountersDTO` lane. It was correctly padded, but every worker thread still wrote the same cache line via Interlocked increments, causing avoidable MESI invalidation under the 5,000-link stress path.

Solution: Vault allocation for `AtomicCounters` is now 128 lanes, each lane exactly 64 bytes. `ExecuteBatteryChargingJob` receives `[NativeSetThreadIndex]` and writes plain increments into its lane; `PostSimulationTick` aggregates lanes after `DispatcherJobFence.TryFinalizeCompleted`.

Rejected Alternatives: Retaining shared Interlocked telemetry counters was rejected because telemetry does not need per-increment atomic visibility inside a completed job. A `NativeParallelHashMap` reducer was rejected because fixed worker lanes are contiguous, deterministic, and cheaper.

Scalability potential: Low tier schedules fewer cadence ticks and touches fewer lanes. Middle/high/ultra can run high link counts without telemetry cache-line fights; extra CPU budget can feed richer LED shader states.

Hardware Impact: Expected low-end gain is contention-dependent: negligible for one worker, material under multiple workers. The change removes repeated shared-cache-line invalidation on Active/Full/Unpowered/Failure telemetry writes.

## Decision 12 - Read Accessor Purity

Problem: `TryReadCharge01`, `TryGetTelemetryReadOnly`, and `TryGetGizmoLink` reached buffers through the same resolver used by mutation paths. That resolver could cold-bind `_vault` from `GlobalRegistry`, which violates the doctrine that read-looking methods do not mutate global or owner state.

Solution: Renamed the mutating vault path to `BindVaultFromRegistry` and confined it to bootstrap, mutation entrypoints, and dispatcher phases. The generic `Resolve<T>` now uses only the cached `_vault`; public read accessors fail closed if the runtime has not already bound the Vault.

Rejected Alternatives: Keeping the resolver because it only mutated a cache was rejected. Read accessors are consumed by editor windows, gizmos, and UI facades; hidden owner-state writes would make diagnostics order-dependent.

Scalability potential: Low-to-ultra behavior is unchanged. The gain is authority hygiene: readers consume immutable snapshots and generation handles rather than causing bootstrap side effects.

Hardware Impact: Runtime microsecond gain is negligible; the value is eliminating hidden global mutation and making read paths deterministic under heavy editor diagnostics.

## Decision 13 - Deferred Emergency Mock Hydration

Problem: The emergency mock job scheduled 5,000 links and immediately forced `TryComplete` during initialization. That is a same-frame schedule/readback loop and violates the dispatcher completion-window doctrine unless restricted to teardown.

Solution: Mock hydration is now scheduled and exposed only after `DispatcherJobFence.TryFinalizeCompleted` reports completion. While the mock is pending, `EnsureVaultState` returns false so no consumer sees half-hydrated mock links. Forced completion remains only in `Shutdown`, where teardown must drain owned jobs before releasing buffers.

Rejected Alternatives: Directly executing `job.Execute(i)` was rejected because the assignment requires a Burst job. Keeping forced completion was rejected because it masks stalls and breaks phase ownership.

Scalability potential: Weak devices can finish mock hydration over the dispatcher window without blocking a frame. High-tier devices finish quickly and then expose the same 5,000-link stress data.

Hardware Impact: Removes a potential cold bootstrap stall proportional to 5,000 link writes. Exact us requires Unity profiler; static proof shows no runtime forced completion path remains.

## Decision 14 - Vault Scratch CSV Reload And Explicit Gizmo AUP

Problem: Editor CSV monitor used `File.ReadAllBytes`, creating a managed `byte[]`, and the gizmo used the no-offset `ToRuntimePosition` overload, hiding a global floating-origin read per endpoint.

Solution: CSV monitor now streams into the Vault-owned `CsvScratch` NativeArray through an unsafe `Span<byte>` and parses that span directly. The gizmo samples committed offset once and calls the explicit AUP conversion overload for both charger and node endpoints.

Rejected Alternatives: Keeping managed file reads because the path is editor-only was rejected; Task 17 requested a span parser and the doctrine rejects managed staging where a native scratch buffer exists. Keeping no-offset AUP conversion was rejected because diagnostics should show the same explicit route being audited.

Scalability potential: Low-tier editor and CI paths avoid managed byte arrays on profile reload. Ultra/debug paths can draw more links without multiplying hidden origin reads.

Hardware Impact: CSV reload allocation drops from one managed byte array sized to the file into zero managed bytes for file payload staging. Gizmo offset read drops from two hidden origin lookups per drawn link to one explicit offset sample per draw pass.

## Decision 15 - Deterministic Registration Window

Problem: The link buffers are intentionally requested with `NativeArrayOptions.UninitializedMemory`. After the read-accessor and deferred mock passes, a real charger could register before the emergency mock job had initialized the full 5,000-link window. Scanning the whole Vault capacity in `TryRegisterChargerLink` would then read uninitialized `Flags` values and make registration order nondeterministic.

Solution: Registration now scans only `0.._activeCount`, which is the initialized window already written by prior real registrations or by the completed mock hydration. If no reusable inactive/mock row exists, the new real link is written at `initializedCount`; no byte beyond the initialized window is read. The same registration path also advances `_powerNodeCount` to cover the registered CSR node index, preventing a live pre-mock link from being clipped to a one-node simulation window.

Rejected Alternatives: Clearing the buffer with `UnsafeUtility.MemClear` was rejected because Task 14 explicitly forbids blanket clearing. Completing the mock job synchronously was rejected because it violates the dispatcher completion-window doctrine. Maintaining a managed free list was rejected because it would reintroduce object-side ownership.

Scalability potential: Low tier can construct chargers before mock stress hydration without nondeterministic skips. Middle/high/ultra retain hole reuse inside the initialized window and keep the 5,000-link flat Vault layout.

Hardware Impact: Removes undefined cold registration behavior without adding hot-path work. Registration remains cold O(initialized rows), while the simulation job still consumes only deterministic contiguous Vault records and the required CSR node prefix.

## Decision 16 - Mock Power Node Single Writer Fence

Problem: `GenerateMockChargerNetworkJob` used `nodeIndex = index % powerNodeCount` and wrote `PowerNodes[nodeIndex]` from every link index. With the normal 5,000-node fallback this is one writer per node, but a shorter Vault node buffer would make multiple parallel job lanes write the same node row.

Solution: The scheduler refuses to schedule emergency mock hydration when the resolved power-node window is zero. Its pending active count is now clamped to the common link-side window across `Links`, `LinkAup`, `ExpectedPowerNodeHashes`, `VisualStates`, and inventory slots. The mock job also treats `powerNodeCount <= 0` as a fail-closed no-op and writes `PowerNodes[nodeIndex]` plus `PowerNodeAup[nodeIndex]` only when `index < powerNodeCount`. Link rows can still reference modulo node indices, but each power node row is initialized by exactly one job lane.

Rejected Alternatives: Reducing link count to the power-node count was rejected because it weakens the 5,000-link stress test when links and nodes intentionally have different capacities. Serializing node initialization into a separate job was rejected because the single-writer guard removes the race without adding another dispatcher fence.

Scalability potential: Low-tier and CI fallback mocks remain robust under reduced buffer capacities. Middle/high/ultra keep the full 5,000-link stress path while retaining deterministic node initialization.

Hardware Impact: Removes a possible mock-hydration data race with no steady-state simulation cost. Branch cost is cold fallback only and paid once per synthetic link during mock generation.

## Decision 17 - Raw Pointer Buffer Lock Coverage

Problem: `ExecuteBatteryChargingJob` receives raw pointers for `Links`, `LinkAup`, `ExpectedPowerNodeHashes`, `VisualStates`, inventory slots, power nodes, and counters. The lock chain covered writable buffers but did not lock the two read-only pointer buffers, so Vault relocation/growth could still invalidate raw pointers. The same chain could leave a partial lock mask until a later phase if a later lock failed.

Solution: `TryLockJobBuffers` now locks every buffer whose raw pointer enters the job, including `LinkAup` and `ExpectedPowerNodeHashes`. Every lock failure immediately calls `UnlockJobBuffers` before returning false, so no partial lock survives a failed schedule admission.

Rejected Alternatives: Assuming read-only buffers do not need locks was rejected because relocation invalidates raw pointers regardless of write intent. Leaving cleanup to `PostSimulationTick` was rejected because schedule admission can fail before `_simulationScheduled` is true.

Scalability potential: Low-to-ultra behavior is unchanged. The improvement is relocation safety under Vault pressure and deterministic lock ownership during concurrent agent/system operation.

Hardware Impact: Adds two cold scheduler lock calls per admitted charge job. Hot Burst math is unchanged; the cost buys raw-pointer validity and removes a potential deadlock/stale-lock failure mode.

## Decision 18 - MonoBehaviour Charging Shadow State Removal

Problem: `BatteryCharger` still carried `_isCharging`, `RefreshChargingDemand`, `HasChargeWork`, `SetChargingState`, and `MarkPowerGridDirty`. They no longer transferred energy, but they preserved a second local charging-state route and dirtied the legacy grid based on managed slot facade state.

Solution: Removed the local charging state and demand refresh path. `OnPowerStatusChanged` now only caches the cold power flag and refreshes disabled legacy indicators. Insert/remove still write SOA inventory state; energy flow remains exclusively in `ExecuteBatteryChargingJob` over inventory and CSR power node buffers.

Rejected Alternatives: Keeping `_isCharging` as harmless UI state was rejected because shader LED state already comes from `ChargerVisualStateDTO`, and `PowerRating` is zero. Dirtying a legacy grid from a managed slot facade would keep the old object-charge architecture alive.

Scalability potential: Low tier avoids object-side demand churn. Middle/high/ultra preserve the same SOA/CSR route and spend saved presentation work in the global LED StructuredBuffer shader path.

Hardware Impact: Removes cold managed branch work on insert/remove/power callback and eliminates a stale grid-dirty signal. Hot charge job cost is unchanged, but ownership ambiguity drops.

## Decision 19 - Visual Pointer Window Clamp

Problem: `ExecuteBatteryChargingJob` receives a raw `ChargerVisualStateDTO*` and writes `VisualStates[index]` without a separate visual length field. The normal Vault allocator gives link-side buffers the same capacity, but the schedule proof must survive reduced or externally repaired buffer windows.

Solution: Registration capacity and simulation `linkCount` now clamp against the common link-side window: `Links`, `LinkAup`, `ExpectedPowerNodeHashes`, and `VisualStates`. A row can only be admitted or scheduled when every raw pointer array used at `index` contains that row.

Rejected Alternatives: Adding a separate `VisualStateCount` to the Burst job was rejected because the hot kernel already treats `LinkCount` as the authoritative row window; a wider job contract would add another branch to every link instead of fixing the scheduler invariant.

Scalability potential: Low-tier degraded Vault capacities fail closed by reducing active rows. Middle/high/ultra retain the same flat 5,000-link path with no extra hot-branch cost.

Hardware Impact: Hot Burst math is unchanged. The change removes a possible out-of-bounds visual pointer write under buffer-window mismatch with zero steady-state CPU cost.

## Decision 20 - Tool Dock Shadow-State Removal

Problem: `BatteryChargerModule` was still carrying `_isCharging`, `SetChargingState`, and `MarkGridDirty`. The state never performed real charging after `PowerRating => 0`, but it contradicted the scanner report and kept a legacy grid-dirty route alive in a charger-owned file.

Solution: Removed `_isCharging`, the charging prompt branch, `SetChargingState`, `MarkGridDirty`, and all grid-dirty calls from docking/restore/power callbacks. The module is now a cold physical tool-dock facade; battery energy transfer remains in `BatteryChargerLogisticsRuntime`.

Rejected Alternatives: Reclassifying the module as unrelated was rejected because it is a charger-named owned file included in the SHINOBU_230 scanner scope. Keeping grid dirty as harmless was rejected because `PowerRating` is zero and there is no authored power demand to publish.

Scalability potential: Low tier avoids unnecessary grid invalidations from tool docking. Middle/high/ultra keep the same SOA/CSR battery transaction route and can spend saved CPU in GPU-driven charger visuals.

Hardware Impact: Removes cold docking/restore grid-dirty churn and eliminates a false report condition. Hot Burst charge cost is unchanged.

## Decision 21 - Scanner Shadow-State Coverage

Problem: `Charger_OOP_Scanner` counted Update/coroutine/list/slow-tick patterns, but did not count the exact class of miss found by the sub-agent: local charger `_isCharging` state and legacy grid-dirty methods.

Solution: Expanded the scanner to count `_isCharging`, `SetChargingState`, `RefreshChargingDemand`, `HasChargeWork`, `MarkPowerGridDirty`, `MarkGridDirty`, and `Grid.MarkDirty` within charger-named files. These counters now contribute to `forbiddenPatternHits` and the generated verdict.

Rejected Alternatives: Keeping scanner scope narrow was rejected because the scanner report includes `BatteryChargerModule.cs` and must fail on charger-owned shadow-state, not only on explicit `Update()` loops.

Scalability potential: Low-to-ultra runtime is unaffected. The gain is CI/editor regression detection for ownership drift before it reaches playmode.

Hardware Impact: Editor-only scan cost increases by a few token-count passes per charger file. Runtime cost is 0 us.

## Decision 22 - Inventory Slot Contract Extraction

Problem: `BatteryChargerLogisticsRuntime` and `BatteryChargerLogisticsContracts` compile under the root `Hecton8.Core` asmdef, while `InventorySlotDTO` was defined inside sibling `Hecton8.Inventory.Routing.Runtime`. Referencing that sibling from Core would create a circular asmdef dependency because Inventory Routing already references Core.

Solution: Moved the shared `InventorySlotDTO` ABI into `Hecton8.Core.Contracts` at `Assets/_Project/Scripts/Core/Contracts/InventorySlotDTO.cs`, preserving namespace `Hecton8.Inventory`, explicit 32-byte layout, and offsets 0/4/8/16/20. Removed the duplicate definition from `InventoryRoutingNetwork.cs`. Charger logistics consumes only the contract DTO and no longer calls `InventoryRoutingNetwork`.

Rejected Alternatives: Adding `Hecton8.Inventory.Routing.Runtime` to `Hecton8.Core.asmdef` was rejected as a compile-wall/circular-dependency breach. Duplicating `InventorySlotDTO` in Power was rejected because it would split rollback/save identity and allow ABI drift.

Scalability potential: Low-to-ultra behavior is unchanged; the gain is assembly isolation. Power can operate on the SOA slot ABI while Inventory Routing keeps ownership of higher-level query/transaction implementation.

Hardware Impact: Runtime cost is 0 us. The impact is production velocity and correctness: the charge kernel can compile without a sibling runtime reference and without changing the slot memory layout.

## Decision 23 - Cold Slot Atomic Facade And Raw Dump

Problem: The cold `TryWriteInventorySlotState` facade wrote `InventorySlotDTO` fields directly and reset `ReservedLock`, which could race with a scheduled charge kernel. The telemetry dump also serialized each field through `BinaryWriter`, while the assignment requested a raw `ReadOnlySpan<byte>` dump.

Solution: `TryWriteInventorySlotState` now locks `BufferID.ShinobuInventorySlots`, acquires `ReservedLock` via `Interlocked.CompareExchange`, writes item hash, quantity, and charge bits, then releases both slot and Vault locks. `WriteDump` writes a fixed little-endian header and then streams the raw telemetry ring as `ReadOnlySpan<byte>` over the native array.

Rejected Alternatives: Keeping direct cold writes was rejected because insert/remove can coincide with dispatcher phases. Keeping `BinaryWriter` was rejected because it is a managed per-field serializer and not the requested raw black-box payload.

Scalability potential: Low devices fail closed when the slot buffer is locked instead of stalling. Middle/high/ultra keep the same hot kernel; crash dumps stay compact and ABI-faithful.

Hardware Impact: Cold insert/remove adds a buffer lock plus one uncontended CAS. Hot charge job is unchanged. Fault dump path avoids per-entry/per-field writer dispatch and writes contiguous telemetry bytes.

## Decision 24 - Scoped Build Gate External Block

Problem: CPU and compiler-process gates opened, so a scoped compile was required. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` stopped before SHINOBU_230 files at `CS2001` because `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs` is still included by the generated project but is deleted in the worktree.

Solution: Classified the failure as an external compile-wall/stale-project block. Verified the file is absent, `git status --short` reports it as deleted, and `Hecton8.Core.csproj` still references it. Did not restore, recreate, or remove the reference because that file is outside the battery charger logistics domain and the project file is Unity-generated.

Rejected Alternatives: Reverting the deleted gameplay file was rejected because it is unrelated work by another agent or the user. Editing `Hecton8.Core.csproj` was rejected because it is generated and would hide the real project-state problem.

Scalability potential: Low-to-ultra runtime behavior is unchanged. This decision preserves concurrent-agent boundaries and keeps compile evidence honest instead of forcing a local-only build artifact.

Hardware Impact: No runtime effect. Build load was limited to one scoped project attempt after the CPU gate opened; no full rebuild was launched.

## Decision 25 - ABI Placement Recheck

Problem: Post-build static verification found the new `InventorySlotDTO` file under `Assets/_Project/Scripts/Inventory/InventorySlotDTO.cs`, while the rationale and compile-wall fix require the ABI to live under `Core/Contracts` so `Hecton8.Core` consumes only `Hecton8.Core.Contracts`.

Solution: Moved the DTO and its GUID-preserving `.meta` to `Assets/_Project/Scripts/Core/Contracts/InventorySlotDTO.cs`. Re-ran the definition scan and confirmed exactly one `public struct InventorySlotDTO` under `Assets/_Project/Scripts`, at the contract path.

Rejected Alternatives: Leaving the file in `Inventory/` was rejected because path ownership would contradict the compile-wall contract even if namespace resolution worked. Duplicating the DTO was rejected because it would split rollback/save ABI identity.

Scalability potential: Low-to-ultra runtime behavior is unchanged. The value is assembly ownership: the SOA slot layout is now a shared ABI, while Inventory Routing remains an implementation consumer.

Hardware Impact: Runtime cost is 0 us. Compile-wall risk is reduced by keeping the shared DTO in the contracts layer.

## Decision 26 - Post-Resume ABI Filesystem Drift Recheck

Problem: On the 2026-05-21 resume pass, disk verification again found `InventorySlotDTO` staged under `Assets/_Project/Scripts/Inventory/InventorySlotDTO.cs`, while the active compile-wall contract requires the ABI under `Assets/_Project/Scripts/Core/Contracts/InventorySlotDTO.cs`.

Solution: Re-applied the move with the same GUID-preserving `.meta`, then verified the only `public struct InventorySlotDTO` under `Assets/_Project/Scripts` is at the Core.Contracts path and that the Inventory-root staging path is absent.

Rejected Alternatives: Trusting the previous log was rejected because disk state is authority. Keeping the DTO under Inventory was rejected because it would make `Hecton8.Core` consume a sibling implementation folder instead of a contracts-layer ABI.

Scalability potential: Low-to-ultra runtime behavior remains unchanged; the value is compile-wall isolation and stable ABI ownership.

Hardware Impact: Runtime cost is 0 us. This prevents assembly-boundary drift, not frame-time work.

## Decision 27 - Binary Payload Ledger Row

Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` documented SHINOBU_141's inventory routing payload lane, but did not record SHINOBU_230's cross-domain use of the shared slot ABI or the static proof caveat for the battery charger transaction.

Solution: Added a SHINOBU_230 static boundary row documenting `InventorySlotDTO` in Core.Contracts, `BufferID.ShinobuInventorySlots` as the charge-bit route, charger DTO sizes, the shader-buffer LED presentation route, and the unrelated `HectonScannerProjectionState.cs` compile blocker.

Rejected Alternatives: Leaving the ledger unchanged was rejected because the current architecture depends on a shared ABI moved out of Inventory Routing implementation scope. Claiming runtime proof was rejected because Unity import/Burst/Play Mode/profiler evidence is still absent.

Scalability potential: Low-to-ultra behavior is unchanged; the ledger update keeps future agents from reintroducing sibling runtime coupling or a duplicate DTO.

Hardware Impact: Runtime cost is 0 us. The value is documentation authority and compile-wall preservation.

## Decision 28 - Sibling World Import Prune

Problem: `BatteryChargerLogisticsRuntime.cs` still imported `Hecton8.World`, even though the charger runtime's AUP helper usage resolves through `Hecton8.Core.HectonFloatingOrigin`. The import was not a runtime call, but it weakened the compile-wall proof.

Solution: Removed the unused `using Hecton8.World` from the owned Power runtime file. The runtime still uses `Hecton8.Core`, `Hecton8.Core.Contracts.Signals`, `Hecton8.Core.Memory`, and the shared `Hecton8.Inventory` contract DTO only.

Rejected Alternatives: Keeping the import because it is harmless to IL was rejected. Static architecture scans treat domain imports as coupling evidence, and the assignment explicitly requires aggressive `using` verification.

Scalability potential: Low-to-ultra runtime behavior is unchanged. The value is assembly-boundary clarity.

Hardware Impact: Runtime cost is 0 us.

## Decision 30 - Tracked Core.Contracts DTO Embedding

Problem: The loose untracked `InventorySlotDTO.cs` asset drifted between Core.Contracts and Inventory-root placement across checks, creating unstable evidence. A shared ABI cannot depend on an untracked asset that disappears or reappears during concurrent workspace activity.

Solution: Embedded the `Hecton8.Inventory.InventorySlotDTO` namespace block into the already tracked `Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs`. Deleted the loose Inventory-root DTO asset. Current scan resolves one DTO definition from tracked Core.Contracts source.

Rejected Alternatives: Keeping a new loose `.cs` asset was rejected after repeated filesystem drift. Moving the DTO back into Inventory Routing was rejected because it recreates sibling runtime coupling for Power/Core consumers.

Scalability potential: Runtime behavior is unchanged; the ABI location is now stable for all hardware tiers and future agents.

Hardware Impact: Runtime cost is 0 us.

## Decision 31 - Generated Project Staleness Boundary

Problem: Static source now carries the DTO in tracked Core.Contracts source, but current generated `.csproj` files still do not list `CoreContractsAssemblyMarker.cs`, `BatteryChargerLogisticsRuntime.cs`, or `BatteryChargerLogisticsContracts.cs`. They also still list deleted `HectonScannerProjectionState.cs`, which blocks scoped `dotnet build` before charger code.

Solution: Recorded this as a project-generation/import boundary, not a source-code architecture fix. Did not edit generated `.csproj` files. Unity project regeneration is required after the external deleted gameplay file is resolved.

Rejected Alternatives: Editing `.csproj` files was rejected because they are generated and would produce false local proof. Rebuilding again was rejected because the same external missing source blocks before SHINOBU_230 code.

Scalability potential: Runtime behavior is unchanged.

Hardware Impact: Runtime cost is 0 us.

## Decision 29 - Sub-Agent Audit Reconciliation

Problem: The static audit sub-agent reported the DTO under `Assets/_Project/Scripts/Inventory/InventorySlotDTO.cs`, but that read was taken before the final separated add/delete move. It also reported two still-valid generated project issues: `Hecton8.Core.csproj` references deleted `HectonScannerProjectionState.cs`, and generated project files do not include `BatteryChargerLogistics*.cs` or `InventorySlotDTO.cs`.

Solution: Treated disk verification after the move as authority. Current scan shows the only `InventorySlotDTO` definition is under Core.Contracts. Kept the generated-project omissions as active compile-proof caveats requiring Unity project regeneration after the external deleted gameplay file is resolved.

Rejected Alternatives: Blindly trusting sub-agent output over newer disk scans was rejected. Editing generated `.csproj` was rejected because it would hide stale Unity generation and create local-only proof.

Scalability potential: Runtime behavior is unchanged; the value is evidence ordering and compile-proof hygiene.

Hardware Impact: Runtime cost is 0 us.

## Decision 32 - Player Runtime Reflection Prune

Problem: `InventorySlotRuntimeLayoutValid()` used `typeof(InventorySlotDTO).GetField(...)` in the non-editor player path. The call is cold, but AGENTS forbids runtime reflection outside editor-only code; keeping it would preserve a metadata lookup in boot validation and weaken the zero-GC proof.

Solution: Split the check by compilation boundary. Player builds now validate only `UnsafeUtility.SizeOf<InventorySlotDTO>() == 32`; the field-offset audit remains under `#if UNITY_EDITOR`, where `UnsafeUtility.GetFieldOffset` plus reflection is acceptable for layout diagnostics.

Rejected Alternatives: Leaving the reflection in place was rejected because "cold" is not the same as "editor-only." Deleting the offset audit entirely was rejected because Task 04 needs a static/editor offset proof for `InventorySlotDTO` and charger DTOs.

Scalability potential: Low-tier player boot loses an unnecessary metadata path. Middle/high/ultra keep the same runtime layout and editor diagnostics without changing gameplay truth or quality behavior.

Hardware Impact: Hot path cost was already 0 us. Cold player boot saves one reflection walk per layout check and removes a class of metadata allocation risk.

## Decision 33 - Vault Lock Coverage For Link Mutation And Mock Hydration

Problem: Public charger registration and unregister paths wrote `Links`, `LinkAup`, `ExpectedPowerNodeHashes`, and `VisualStates` without a Vault lock. The emergency mock job also wrote Vault-owned arrays after scheduling without holding locks for the full dispatcher window.

Solution: Added cold link-mutation lock helpers and a separate mock lock mask. Registration/unregister now fail closed during simulation or mock hydration and lock every link-side buffer before writing. Registration also locks power nodes while resolving the expected CSR hash. Mock hydration locks all writer buffers before scheduling and unlocks only after dispatcher finalize or teardown forced completion.

Rejected Alternatives: Keeping public direct `NativeArray` writes was rejected because relocation can invalidate rows and jobs can see half-mutated records. A managed queue was rejected because the existing cold facade can fail closed and retry without adding heap state.

Scalability potential: Low tier pays a few cold Vault lock calls when chargers are constructed or removed. Middle/high/ultra keep the hot charging kernel flat and preserve dense native buffers without relocation hazards.

Hardware Impact: Hot path remains unchanged. Cold path cost is 4-5 lock calls for link mutation and 8 lock calls for mock schedule; this buys raw-pointer safety and prevents data races.

## Decision 34 - Conservation Transaction Order And Active Hum AUP

Problem: The previous transaction debited CSR power potential first, then attempted an inventory `ConditionFlags` CAS, then used a bounded best-effort refund if that CAS failed. That is not a hard conservation proof. A short-lived report draft also overstated the replacement as unconditional `Interlocked.Exchange` primary write. Hum routing also emitted from `linkAups[0]` regardless of which charger drew energy.

Solution: Kept the slot `ReservedLock` as the inventory ownership fence. The locked inventory `ConditionFlags` CAS now lands before the CSR power-node CAS. If the power CAS loses, inventory charge is rolled back with `Interlocked.Exchange` while the same slot lock is still held. There is no power refund loop and no post-debit inventory CAS path. Added `LastActiveLink` at offset 32 in `ChargerAtomicCountersDTO`; lanes record the link that actually drew energy, aggregation preserves it, and hum signal AUP reads that link.

Rejected Alternatives: Keeping the bounded `AddPotential` rollback loop was rejected because bounded retries can still destroy energy under contention. Treating `Exchange` as the primary inventory write was rejected because it would silently overwrite a changed charge word; the CAS is the proof, the `Exchange` is only the locked rollback after a failed CSR CAS. Computing an active centroid was rejected for now because it needs extra summed AUP state; last active link is deterministic and stays within the 64-byte counter line.

Scalability potential: Low tier keeps identical gameplay truth with fewer cadence ticks. Middle/high/ultra can add richer hum or shader presentation later without changing the SOA/CSR transaction route.

Hardware Impact: Successful transfer keeps one inventory CAS and one CSR CAS, but removes the bounded power-refund helper and its retry path. `LastActiveLink` reuses padding inside the existing 64-byte counter DTO, so false-sharing protection remains intact.

## Decision 35 - Scanner Proof-Chain Sanitization

Problem: `Charger_OOP_Scanner` classified files and counted forbidden tokens in raw text. Its own detector string literals could classify the scanner file as charger code and pollute counts. The manual shared report also needed a scope marker because compile proof remains blocked by external generated-project state.

Solution: Added `StripNonCode` to replace comments, normal/verbatim/interpolated strings, and char literals before classification/counting. Scanner output now records string/comment stripping, self-classification false-positive fix, and `verdictScope=scanner-only`. The shared report mirrors the current schema and keeps the external compile blocker explicit.

Rejected Alternatives: Switching to Roslyn was rejected inside this pass because the project is already under generated-project staleness and compile evidence is blocked. Raw token scanning without stripping was rejected because it creates false evidence.

Scalability potential: Runtime behavior is unchanged. The gain is editor/CI proof quality across all hardware tiers.

Hardware Impact: Runtime cost is 0 us. Editor scanner pays one linear text pass per `.cs` file.

## Decision 36 - Custom Scanner Invocation Counting

Problem: The custom scanner pass originally excluded identifiers whose previous non-space character was `.`, which meant a forbidden member-call form such as `GlobalRegistry.RegisterSlowTickable(...)` could avoid the `CountInvocation` detector even though it is the exact slow-tick registration shape the scanner must catch.

Solution: `CountInvocation` now counts any identifier followed by `(` after non-code stripping. Method declarations remain covered by `CountMethodDeclaration`, and member-call false negatives are removed. The JSON report records `scannerUsesCustomSyntaxPass=true`, `scannerUsesAstParser=false`, and `scannerCountsMemberInvocations=true`.

Rejected Alternatives: Keeping the dot exclusion was rejected because it optimized for avoiding rare duplicate counts while missing real legacy calls. Adding a Roslyn compile-time dependency was rejected for this pass because generated project state is stale and static source proof can be improved without assembly churn.

Scalability potential: Runtime behavior is unchanged. The scanner becomes a stricter editor/CI guard across low-to-ultra hardware targets by preventing managed charger cadence routes from returning unnoticed.

Hardware Impact: Runtime cost is 0 us. Editor scan remains O(total source bytes) with a few extra identifier checks.

## Decision 37 - Runtime Lock And Reentry Audit

Problem: Descartes found four remaining runtime integrity gaps: `ResolveExpectedPowerNodeHash` mutated `_powerNodeCount`, `ScheduleSimulation` could re-enter while a job/mock was already scheduled, tuning/profile CSV writes touched Vault buffers without locks, and scanner report writing appended duplicate SHINOBU_230 entries.

Solution: Split the hash helper into mutating `ExtendPowerNodeWindowForLink` and pure `ReadExpectedPowerNodeHash`. Added an early schedule guard and made `TryLockJobBuffers`/`TryLockMockBuffers` fail closed when their masks are already set. Wrapped `Tuning`, `Profiles`, and `CsvScratch` writes in `TryLockBuffer`/`TryUnlockBuffer` with `finally`. Changed scanner report writing to remove the previous SHINOBU_230 JSON object before inserting the current one.

Rejected Alternatives: Relying on dispatcher ordering was rejected because public phase methods must be fail-closed. Treating editor tuning/CSV as harmless was rejected because Vault relocation rules apply to cold writes too. Append-only scanner reports were rejected because duplicate PASS/FAIL evidence is worse than no evidence.

Scalability potential: Low-to-ultra runtime math is unchanged. The gain is concurrency hygiene: weak devices avoid undefined stalls from accidental unlock/reentry, and high-tier stress tests keep proof artifacts single-source.

Hardware Impact: Hot Burst loop cost is 0 us. Cold/editor writes pay one or two Vault lock calls. Reentry guard prevents accidental raw-pointer release with no per-link cost.

## Decision 38 - Lock Before Alias And External Inventory Owner Boundary

Problem: A follow-up audit found two separate hazards. First, `ScheduleSimulation` and the cold slot facade could obtain `NativeArray` aliases before the Vault lock, leaving a relocation window before raw pointer extraction. Second, Inventory Routing shares `BufferID.ShinobuInventorySlots` and contains maintenance/container jobs that write whole `InventorySlotDTO` records without slot `ReservedLock`, so the charger transaction cannot honestly claim global conservation if those jobs overlap charger-owned slot ranges.

Solution: `ScheduleSimulation` now locks all job buffers before resolving aliases and unlocks on every pre-schedule failure path. The job lock mask includes `Tuning`, and quality/cadence sampling uses a short `Tuning` lock. `TryWriteInventorySlotState` now locks `ShinobuInventorySlots` before resolving the buffer; `TryReadCharge01` fails closed if `ReservedLock` is non-zero. The scanner/report now records the external Inventory Routing whole-slot writer risk instead of burying it under a PASS.

Rejected Alternatives: Editing `InventoryRoutingNetwork.cs` from the Power lane was rejected for this pass because it is a sibling owner domain and the correct fix requires an owner route decision: either maintenance jobs acquire slot locks, phase-fence through the same Vault lock, or reserve charger slot ranges so container maintenance cannot target them. Ignoring the finding was rejected because it would turn the conservation proof into a lie. Allocating a private charger inventory buffer was rejected because Task 02/07 require the SOA inventory route, not shadow battery state.

Scalability potential: Low-to-ultra hot charge math is unchanged. Weak devices gain deterministic fail-closed scheduling instead of undefined pointer races; high-tier stress tests now have explicit evidence when the external inventory owner violates the slot lock discipline.

Hardware Impact: Hot Burst loop cost remains 0 us change. Scheduler pays one extra `Tuning` lock. Cold slot writes pay the same slot lock but now in the correct order. The external owner boundary is a correctness proof, not a microsecond saving.

## Decision 39 - Unassigned Live Slot Range Fail-Closed

Problem: `BatteryCharger` serialized `inventorySlotStartIndex` with the CLR default `0`. The Inventory Routing audit already proved that `BufferID.ShinobuInventorySlots` is shared and maintenance writers can operate from low slot ranges. Letting an unconfigured prefab write slot zero would convert a missing authoring assignment into a real SOA mutation, corrupting the inventory owner boundary.

Solution: Treat `inventorySlotStartIndex == 0` as unassigned in the charger facade. Link registration, cold slot writes, unregister, and Vault charge reads now fail closed until a non-zero SOA range is explicitly authored. The runtime transaction kernel still accepts any index it is given by trusted owners; the guard belongs to the Unity authoring facade where default serialized values enter the system.

Rejected Alternatives: Auto-allocating slot ranges in Power was rejected because slot identity is save/rollback authoring data and belongs to Inventory/Base construction ownership, not the charger transaction kernel. Allowing slot zero as valid was rejected because it makes an unconfigured prefab indistinguishable from an intentional low-range reservation. Moving chargers to a private buffer was rejected because Tasks 02 and 07 require the shared SOA inventory route.

Scalability potential: Low-tier devices avoid undefined slot contention from default prefabs. Middle/high/ultra keep the same flat SOA/CSR transaction after authoring provides a valid range; visual overkill remains GPU-buffer driven and unaffected.

Hardware Impact: Hot Burst loop cost is unchanged at 0 us. Cold facade calls add one scalar branch before registration/write/read. The gain is preventing corrupt writes, not reducing frame time.

## Decision 40 - Facade SOA Commit Before Link Registration

Problem: `RegisterLogisticsLinks` created an active `ChargerLinkDTO` before writing the corresponding `InventorySlotDTO` state. If the slot write failed because the SOA buffer was locked or the slot `ReservedLock` was contested, the link could become visible while pointing at stale inventory data.

Solution: `WriteInventorySlotState` now returns the Vault write result. `RegisterLogisticsLinks` writes or clears the SOA slot first and skips link registration on failure. `InsertBattery` and `RemoveBattery` also require the SOA write/clear to succeed before mutating the local cold facade slot, so interaction code cannot silently diverge from the Vault route.

Rejected Alternatives: Registering the link first was rejected because it can expose stale truth to the Burst job. Local-only insert/remove fallback was rejected because the assignment explicitly requires physical battery presence to live in SOA inventory, not object state. Retrying writes in a managed loop was rejected because contested ownership must fail closed rather than spin in an interaction path.

Scalability potential: Low-tier devices avoid stale link churn under contention. Middle/high/ultra keep the same dense link buffer after successful authoring commits; no quality tier changes gameplay truth ownership.

Hardware Impact: Hot Burst loop cost is unchanged at 0 us. Cold registration/user interaction pays one boolean branch. The gain is correctness and rollback consistency.

## Decision 41 - Player And Tool Bridge Rollback

Problem: `InsertBatteryFromInventory` wrote the charger state first and then called `PlayerInventory.RemoveItemAt`, a void method with no success signal. That can duplicate a battery if the player inventory removal is refused or targets stale state. Tool swap paths also ignored `InsertBattery` return values after removing a battery from its previous owner.

Solution: Player inventory insertion now removes one item from the exact grid cell with `RemoveOneItem`, verifies the removed hash, then attempts the charger SOA commit. If the charger commit fails, it returns the item through `TryAddItem`. Removal from charger to player inventory preflights `CanAcceptItemQuantity` before clearing the charger SOA slot and uses the same candidate hash for the final add. Tool-to-charger swap checks for a free charger slot and authored SOA range before removing the tool battery, then returns the battery to the tool if the charger insert fails. Charger-to-tool swap reinserts the battery into the charger if the tool insert fails.

Rejected Alternatives: Continuing to use `RemoveItemAt` was rejected because void mutation cannot prove conservation. Writing both owners then hoping a later cleanup reconciles was rejected because it creates a clone window. Adding a new cross-domain inventory reservation API was rejected because that belongs to the Inventory owner and would violate this domain's scope.

Scalability potential: Low-tier devices avoid duplicate/vanished battery recovery work from failed interactions. Middle/high/ultra keep the same SOA/CSR hot transaction; bridge work remains cold user interaction.

Hardware Impact: Hot Burst loop cost remains 0 us. Cold interaction pays one return-value branch, one capacity preflight, and a rare rollback `TryAddItem`.

## Decision 42 - Scanner Structural Metadata Honesty

Problem: The scanner implementation already used comment/string stripping plus class/method/invocation parsing helpers, but the JSON proof still said `scannerUsesStructuralSyntaxPass=false`. That under-reports the real proof route while still correctly refusing to claim Roslyn AST.

Solution: Updated the scanner and shared report metadata to `scannerUsesStructuralSyntaxPass=true`, `scannerUsesCustomSyntaxPass=true`, `scannerUsesAstParser=false`, and added a parser route string that names the custom declaration/invocation parser. This preserves honesty: stronger than raw grep, not a Roslyn AST dependency.

Rejected Alternatives: Adding Roslyn in this pass was rejected because Unity package/asmdef project generation is already stale and compile proof is blocked by an unrelated deleted Gameplay source. Claiming `scannerUsesAstParser=true` was rejected because that would be false evidence.

Scalability potential: Runtime behavior is unchanged. CI/editor proof becomes clearer across hardware tiers without adding player runtime code.

Hardware Impact: Runtime cost is 0 us. Editor scanner remains O(source bytes).

## Decision 43 - Facade AUP World Import Prune

Problem: `BatteryCharger` used `Hecton8.World.AbsoluteUniversePosition` to turn a runtime `Transform.position` into AUP before link registration. That pulled a World namespace into the charger facade and made the compile-wall proof weaker. The logistics runtime hum path also referenced the `AbsoluteUniversePosition.FromAbsolutePosition` factory shape while the domain report claimed zero sibling world import hits.

Solution: The facade now resolves AUP through `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(...)`, which is already exposed by Core and returns the `double3` used by `ChargerLinkAup`. The hum signal path writes `AcousticPingSignal.PositionAup` fields directly from the same absolute `double3` using `HectonPhysicsContract.AupSectorSizeMetersDouble`, and returns without publishing if the source AUP or derived locals are non-finite. Scanner/report fields now prove the route: zero facade/runtime `using Hecton8.World`, Core floating-origin facade AUP, and manual contract-field hum AUP.

Rejected Alternatives: Keeping `using Hecton8.World` was rejected because the facade does not need the World DTO to register a native charger link. Adding a new public Core helper was rejected because generated project files are stale and compile proof is externally blocked. Publishing default AUP on invalid data was rejected because it would create a false acoustic source at the origin.

Scalability potential: Low-tier devices keep the cheapest cold authoring conversion and no extra hot charge work. Middle/high/ultra keep the same GPU-buffer visual overkill and active hum signal route, now without a facade World import.

Hardware Impact: Hot Burst charge kernel cost remains 0 us changed. Cold registration removes one World DTO construction path; post-phase hum emission pays one finite branch and several scalar field writes only when energy was drawn.

## Decision 44 - Read Accessor And Hum AUP Proof Repair

Problem: A fresh source scan showed the prior AUP report was stale on current disk: `BatteryCharger.cs` still had the World AUP/global-origin route. The same pass found `IInteractable.GetInteractText()` calling `BindToolManagerForInteraction()`, which could mutate cached player/tool state from a read-looking accessor. Hum AUP conversion also depended on downstream SignalBus sanitization for out-of-world coordinates.

Solution: Replaced the current-disk facade AUP helper with a direct finite-guarded `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(...)` call. Changed `GetInteractText()` to read `_cachedToolManager` only; real binding remains in `Interact(Transform interactor)`, where mutation is explicit. Added `AcousticHumMaxAupExtentMeters = 100000.0d` and fail-closed extent checks inside `TryWriteAbsoluteAupFields` before writing the acoustic AUP payload. Scanner/report now emit `interactTextUsesCachedToolOnly` and `humAupRejectsOutOfExtent`.

Rejected Alternatives: Trusting the previous report was rejected because disk source contradicted it. Keeping lazy binding in `GetInteractText()` was rejected because read accessors must not bind services, search transforms, or mutate cached context. Relying only on SignalBus sanitization was rejected because a bad hum source would be normalized downstream instead of being suppressed at the charger presentation boundary.

Scalability potential: Low devices keep cold interaction text as a pure cached read and avoid extra registry/component searches from UI polling. Middle/high/ultra keep the same GPU-buffer LED overkill and active hum signal route; invalid acoustic data is dropped before it can create false spatial cues.

Hardware Impact: Hot Burst charge kernel cost remains 0 us. Interaction UI read avoids lazy binding work. Hum emission adds three scalar extent comparisons only in post-simulation frames where energy was drawn.

## Decision 45 - Loop 29 Facade AUP Source Reconciliation

Problem: After context compaction, the compacted memory claimed the facade AUP route was already clean, but a current disk grep found `Hecton8.World.AbsoluteUniversePosition`, `GlobalSignals.CurrentRuntimeOriginAup()`, and `AbsoluteUniversePosition.OffsetAbsoluteMeters` back inside `BatteryCharger.ResolveChargerAup`. A final scan after the first Loop 29 repair saw the same old route again, proving an active concurrent write race or stale-file rewrite. Leaving the report as-is would make the compile-wall proof false.

Solution: Replaced the route again with the Core-owned `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position)` helper behind the existing finite `Transform.position` guard, then repeated the same repair after the later rewrite. The helper returns the `double3` consumed by the SHINOBU-owned link AUP buffer, so the facade does not construct a World DTO and does not read global origin through `GlobalSignals`. An 8-second delayed verification kept zero forbidden AUP hits and one Core helper hit with the file timestamp unchanged.

Rejected Alternatives: Trusting Loop 28 documentation was rejected because objective source grep contradicted it. Keeping the direct `Hecton8.World` route was rejected because it reintroduces a sibling-domain dependency shape into the facade. Adding a new helper or editing generated project files was rejected because generated project state is stale and compile proof is already blocked by an unrelated deleted Gameplay source.

Scalability potential: Low tier keeps the cheapest cold authoring conversion and zero extra hot charge-kernel work. Middle/high/ultra keep the same Vault-owned link AUP, GPU StructuredBuffer LED path, and scalar hum presentation; GlobalQualityWeight remains presentation/cadence only and does not alter DTO layout or authority route.

Hardware Impact: Hot Burst charge kernel cost remains 0 us. Cold registration removes a World DTO/global-signal read path and preserves compile-wall isolation; the measurable value is avoiding dependency churn and false proof, not frame-time reduction.

## Decision 46 - Facade Bridge And Mock Inventory Ownership Reconciliation

Problem: Socrates found that default serialized `BatterySlot[]` entries could be null and crash before SOA fail-closed logic, player/tool bridges still removed items before a hard Inventory reservation proof, and the scanner/report claimed more than the source could prove. Kepler also found that the Power runtime fabricated the shared `ShinobuInventorySlots` buffer with `SystemID.GameplayPlayer`, creating false ownership and possible uninitialized shared slot exposure.

Solution: Added cold `EnsureSlotObjects()` in `Awake`/`OnValidate` and null guards in slot scans/accessors. Added authored-SOA-range preflight before player inventory removal and checked rollback escalation through `TryReturnItemToInventory`, `batteryTool.InsertBattery`, and `InsertBattery` return values. Removed the shared inventory allocation fallback; emergency mock data now uses `BatteryChargerLogisticsBufferIds.MockInventorySlots` owned by Power, while live charger registration fails closed unless the Inventory-owned `BufferID.ShinobuInventorySlots` already exists and covers the requested slot. Added skipped-cadence telemetry writes, NaN fault producers, and raw-pointer safety comments. Repaired the recurring facade AUP route again to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`.

Rejected Alternatives: Allocating `ShinobuInventorySlots` from Power was rejected because Inventory owns that fact. Continuing best-effort rollback without checked results was rejected because it can hide battery loss. Introducing a new Inventory reservation API was rejected in this lane because it requires Inventory owner route-card work; the scanner now records `playerInventoryBridgeHardReservationProof=false` instead of claiming absolute proof. Editing generated `.csproj` or restoring the deleted external Gameplay file was rejected.

Scalability potential: Low tier keeps the same continuous cadence shedding and no per-object charger loop. Middle/high/ultra keep the GPU StructuredBuffer LED Dear Lie and live hum route. Mock stress still provides a 5,000-link test path without polluting the shared Inventory owner buffer.

Hardware Impact: Hot charge kernel cost is unchanged except finite guards that produce NaN telemetry. Skipped cadence adds one 64-byte telemetry write on frames where the charger job does not run. Cold interaction paths pay scalar preflight/rollback checks. The main gain is preventing ownership corruption, null exceptions, and false proof artifacts rather than shaving frame time.

Residual Risk: `BatteryCharger` still uses concrete `PlayerInventory`, `PlayerToolManager`, and `IBatteryTool` facade APIs because current Core contracts expose those concrete types. A hard two-phase Inventory reservation/commit route is not present in this domain and remains an Inventory/Core contract task.

## Decision 47 - AUP Drift Scanner Gap Closure

Problem: A delayed verification pass caught the same `BatteryCharger.ResolveChargerAup` regression again: fully-qualified `Hecton8.World.AbsoluteUniversePosition`, `GlobalSignals.CurrentRuntimeOriginAup()`, and `AbsoluteUniversePosition.OffsetAbsoluteMeters` had returned after the prior repair. The existing scanner proof was too weak because it counted `using Hecton8.World` only. That allowed a fully-qualified World route to pass `facadeWorldImportHits=0` and keep `facadeUsesCoreFloatingOriginAup=true`.

Solution: Repaired the facade block back to the finite-guarded Core route `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position)`. Tightened `Charger_OOP_Scanner` with explicit counters for fully-qualified `Hecton8.World.` routes, `GlobalSignals.CurrentRuntimeOriginAup()`, and `OffsetAbsoluteMeters`, plus a runtime-side fully-qualified World route counter. `facadeUsesCoreFloatingOriginAup` now requires all World/global-origin counters to be zero. The shared JSON proof now records those counters instead of relying on the import-only field.

Rejected Alternatives: Only patching `BatteryCharger.cs` was rejected because the proof system would miss the next fully-qualified regression. Counting only namespace imports was rejected because current source proved it was insufficient. Running another `dotnet build` was rejected because the generated project is still known-blocked by another domain's deleted `HectonScannerProjectionState.cs` and the user explicitly forbade premature rebuilds.

Scalability potential: Low-tier devices keep the cheapest cold AUP conversion and no new hot charge-kernel cost. Middle/high/ultra keep the same Vault-owned link AUP, GPU StructuredBuffer LED Dear Lie, scalar hum route, and continuous `GlobalQualityWeight` cadence. The scanner improvement prevents false proof across every tier without changing gameplay truth, DTO layout, or authority route.

Hardware Impact: Runtime hot-path cost is 0 us. Editor scanner cost remains linear in source bytes with four additional token counts in two files. Cold registration avoids the World DTO/global-origin signal route and preserves compile-wall isolation. Residual risk is external concurrent rewrite pressure on `BatteryCharger.cs`; the strengthened scanner now catches that class of regression.

## Decision 48 - Active AUP Rewrite Stabilization

Problem: After Loop 31 documentation and report updates, another delayed probe found the forbidden facade AUP route had returned yet again. This invalidated the Loop 31 delayed-verification claim and proved the issue was not only a scanner blind spot; a concurrent or stale-file writer is actively rewriting `BatteryCharger.cs` to the old World/global-origin implementation.

Solution: Patched `ResolveChargerAup()` back to the Core floating-origin helper again and treated the source as contested until it survived a longer watch. Ran six probes across roughly 30 seconds, each checking forbidden World/global-origin tokens and the Core helper. All probes stayed on `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position)`, with the file timestamp and length unchanged. The scanner/report hardening remains in place so a future fully-qualified regression is machine-readable instead of hidden behind `facadeWorldImportHits=0`.

Rejected Alternatives: Marking Loop 31 as clean was rejected because the later probe contradicted it. Setting the source file read-only was rejected because this repository has concurrent agents and file permissions would become a coordination hazard. Reverting or editing unrelated generators/processes was rejected because no owning generator was proven and foreign-domain edits are outside SHINOBU_230 scope.

Scalability potential: No runtime scaling behavior changed. Low/middle/high/ultra all keep the same continuous cadence/presentation route. The benefit is proof stability and compile-wall honesty, not a new gameplay feature.

Hardware Impact: Hot-path cost is 0 us. The 30-second watch is tooling-only. Remaining risk is external: if another agent rewrites the same method after this pass, the strengthened scanner will flag it, but this domain cannot own the other writer without an integrator route decision.

## Decision 49 - Blocked By Active Concurrent AUP Rewrite

Problem: A longer 18-probe watch over roughly 90 seconds invalidated the Loop 32 stability claim. The forbidden World/global-origin route returned at probe 12 with `LastWriteTimeUtc=2026-05-20T23:52:36.6040949Z` and file length `34092`. Repeating the same patch would be an infinite loop and would leave a false report if the external writer runs after the final response.

Solution: Stop fighting the active rewrite under the 3-strike protocol and make the proof artifact honest. `Charger_OOP_Scanner` now computes `routeProofClean` and fails the scanner verdict if facade/runtime World/global-origin counters are non-zero. `EQUIPMENT_OPTIMIZATION_REPORT.json` was updated to match current disk: `facadeUsesCoreFloatingOriginAup=false`, `facadeWorldRouteHits=2`, `facadeGlobalOriginAupHits=1`, `facadeOffsetAbsoluteAupHits=1`, and verdict `FAIL`. Non-AUP SHINOBU changes remain preserved.

Rejected Alternatives: Continuing the patch/watch loop was rejected because the method has already been restored by another writer multiple times. Setting `BatteryCharger.cs` read-only or killing unknown PowerShell processes was rejected because this workspace has concurrent agents and no proven owning process was identified. Reverting whole-file changes was rejected because most SHINOBU-owned fixes are still valid and should not be discarded.

Scalability potential: No gameplay scaling behavior changed. The active blocker concerns compile-wall/AUP route proof only; continuous `GlobalQualityWeight` cadence, GPU StructuredBuffer LED Dear Lie, mock buffer ownership, telemetry, and job math remain as implemented.

Hardware Impact: Runtime cost is unchanged. Editor scanner now does a few extra token checks and prevents a false PASS. Remaining blocker requires integrator coordination to stop the external stale writer or merge the Core AUP route as the single accepted source.

## Decision 50 - AUP Route Policy Reconciliation

Problem: Loop 33 classified the `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetAbsoluteMeters` route as a blocker. A fresh source and tooling audit showed that classification was wrong. `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position)` is the direct bridge rejected by the SHINOBU_205 AUP precision gate, because it hides `CurrentTotalOffsetDouble` behind a runtime-position conversion helper. The current `BatteryCharger.ResolveChargerAup` body uses the current-origin proof route, finite-checks the runtime `position`, finite-checks the origin AUP, and finite-checks the resulting `double3`.

Solution: Keep the current `BatteryCharger` route body. Correct `Charger_OOP_Scanner` so `routeProofClean` accepts the current-origin proof and fails on the actual forbidden forms: `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(` and `AbsoluteUniversePosition.FromRuntimePosition`. Add explicit report fields for `facadeUsesCurrentOriginAupProof`, `facadeRejectsDirectFloatingOriginBridge`, `facadeAupFiniteGuarded`, `facadeDirectFloatingOriginBridgeHits`, and `facadeFromRuntimePositionHits`. Update the shared equipment report to `PASS` with zero direct bridge hits.

Rejected Alternatives: Reapplying the Core floating-origin helper was rejected because it would reopen `runtimeAupBridgeReviewCount` in `Tools/AupPrecisionGate_SHINOBU_205.py`. Keeping the Loop 33 `FAIL` was rejected because it contradicted the project’s AUP precision gate and would force the integrator to chase a false blocker. Editing `GlobalSignals`, `HectonFloatingOrigin`, or World AUP APIs was rejected as a cross-domain/core route change outside this lane.

Scalability potential: Low/middle/high/ultra behavior is unchanged. The route is cold facade registration; the hot Burst charge kernel, continuous `GlobalQualityWeight` cadence, GPU StructuredBuffer LED Dear Lie, mock ownership, telemetry ring, and hum signal payload remain unchanged. Better proof prevents false churn without altering gameplay truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: Hot runtime cost remains 0 us changed. Editor scanner adds two token counters and three booleans. The practical gain is avoiding repeated patch churn and preserving the AUP gate’s current-origin proof discipline.

## Decision 51 - Visual Buffer Prewarm And Cold Facade Allocation Annotation

Problem: The LED Dear Lie path correctly used double-buffered `GraphicsBuffer` upload and dirty hashes, but the buffer pair was still first-created from `VisualSyncTick`. That puts first-use GPU allocation on the presentation phase, exactly where shader/driver stutter is most visible. A separate static scan also showed cold managed `BatterySlot` fallback allocations without canonical `COLD ALLOC` comments, leaving future auditors a false trail even though those objects are facade/editor metadata rather than charge truth.

Solution: `PreSimulationTick` now calls `EnsureGraphicsBuffers()` after Vault/default readiness and tuning application, before `VISUAL_SYNC` can upload LED state. `VisualSyncTick` keeps the same guard as a safety fallback, but normal runtime gets the double buffer prewarmed earlier in the dispatcher frame. `Charger_OOP_Scanner` emits `visualBuffersPrewarmedBeforeVisualSync`, and the shared equipment report records it as true. `BatteryCharger` now annotates the serialized `BatterySlot[2]`, legacy fallback array, and per-slot facade object allocations with canonical `COLD ALLOC` comments.

Rejected Alternatives: Keeping lazy first allocation in `VISUAL_SYNC` was rejected because it risks a visible first-active frame hitch for no gameplay benefit. Deleting `BatterySlot` facade metadata was rejected because it would break prefab/editor migration and does not affect the SOA/Vault charge truth. Moving graphics buffer allocation into a static constructor was rejected because Unity graphics resources need runtime lifecycle control and explicit release.

Scalability potential: Low devices avoid a first-use visual allocation spike and keep coarse GPU LED state. Middle/high/ultra keep the same StructuredBuffer route and can spend saved CPU on richer shader interpretation of the visual state; `GlobalQualityWeight` still scales cadence/presentation only and does not change DTO layout, save identity, or authority route.

Hardware Impact: Hot Burst charge kernel remains 0 us changed. First-active LED presentation loses one avoidable GPU allocation on the visual phase; the buffer cost remains bounded at two structured buffers of `DefaultLinkCapacity * sizeof(ChargerVisualStateDTO)`.

## Decision 52 - CSV Tuning Parser Fail-Closed Polish

Problem: The charger profile CSV parser used an allocation-free span parser, but the numeric parser accepted partial values because it returned after consuming the prefix digits and did not require end-of-field. A malformed field like `0.5junk` could therefore hydrate as `0.5`, and an accidental extra column could be ignored. That violates the human-readable tuning bridge rule: bad authored data must fail closed, not become silent simulation tuning.

Solution: Replaced `ParseFloat` with `TryParseFiniteFloat`. It trims the span, requires at least one digit, rejects non-finite intermediate accumulation, rejects trailing characters by requiring `index == value.Length`, and returns a boolean so `TryParseLine` can reject malformed rows. `TryParseLine` now also rejects extra columns by checking `Trim(line).Length != 0` after the expected four numeric fields. `Charger_OOP_Scanner` and the shared equipment report now expose `csvParserRejectsMalformedRows=true`.

Rejected Alternatives: Keeping permissive partial parsing was rejected because it turns authoring typos into valid tuning. Using `float.Parse`, `string.Split`, or managed dictionaries was rejected because Task 17 mandates `ReadOnlySpan<byte>` and zero-GC cold ingestion. Throwing exceptions on malformed rows was rejected because editor/runtime hot reload should skip bad rows deterministically and preserve the last valid tuning state.

Scalability potential: Low-tier devices keep the same cheap charger cadence and avoid corrupted rate/exponent values that could create spikes. Middle/high/ultra keep designer-authored overkill charge profiles, but only when rows are syntactically valid. `GlobalQualityWeight` cadence remains continuous and independent of CSV validity.

Hardware Impact: Hot Burst charge kernel cost remains 0 us. Cold/editor CSV ingestion adds a few byte comparisons per numeric field and prevents malformed tuning from amplifying grid drain or visual LED cadence.

## Decision 53 - Emergency Mock Fallback Authority Fence

Problem: The emergency mock charger generator satisfied the CI fallback requirement, but it could also hydrate 5,000 mock charger links in a normal player runtime if no live links existed when the dispatcher first touched the domain. Once `_usingMockInventorySlots` became true, `TryRegisterChargerLink` refused live registration, making mock data sticky and capable of blocking streamed or late-built real chargers.

Solution: Added `AllowEmergencyMockNetwork()` so mock hydration is confined to `UNITY_EDITOR || DEVELOPMENT_BUILD`. Non-development runtime still creates Vault buffers and tuning state, but does not fabricate charger truth. `TryRegisterChargerLink` now validates that the live Inventory-owned `ShinobuInventorySlots` buffer exists for the authored slot first; if a mock network is active, it calls `DropMockNetworkForLiveRegistration()` to reset mock active counts and allow the live link to overwrite the window. Scanner/report proof fields now expose `emergencyMockEditorOrDevelopmentOnly=true` and `liveRegistrationDropsMockFallback=true`.

Rejected Alternatives: Keeping always-on mock hydration was rejected because a fallback benchmark must not become runtime authority. Removing the mock generator was rejected because Task 05 requires isolated 5,000-link stress data for CI/dev. Clearing or owning the shared Inventory slot buffer was rejected because Inventory owns live slot truth; this domain only drops its own mock window.

Scalability potential: Low-tier release builds avoid an unnecessary 5,000-link synthetic job when no chargers exist. Editor/development builds still get the full 5,000-link pressure test. Middle/high/ultra behavior with live chargers is unchanged: continuous `GlobalQualityWeight` controls cadence, not authority ownership.

Hardware Impact: Hot Burst charge kernel remains 0 us changed. Release/no-charger boot avoids scheduling one 5,000-link `GenerateMockChargerNetworkJob`; live registration pays a cold scalar reset if it replaces a dev mock fallback.

## Decision 54 - Binary Payload Ledger Registration

Problem: `BatteryChargerLogisticsBufferIds` owns the local `72300..72310` Vault ID range, but `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` did not register that range or provide a SHINOBU_230 payload route card. The ledger explicitly rejects local numeric `(BufferID)N` casts without a registered range, so source-only proof was incomplete.

Solution: Added `72300..72310` to the active BufferID range table and inserted a SHINOBU_230 payload boundary entry. The entry records each buffer ID, the Power owner, primary DTO layout anchors, authority route, endian status, rollback/save boundary, and `Docs/AgentLogs/Dump_SHINOBU_230.bin` fault route. Reconciled the equipment report query against the current `{ reports: [...] }` schema and reran targeted source/report guards.

Rejected Alternatives: Editing `H8Memory.BufferID` enum entries was rejected for this loop because the mandate was to register and document the existing range without broad core churn; no runtime callsite requires enum-name syntax to resolve the Vault handles. Leaving the ledger silent was rejected because it violates the binary payload integration doctrine. Treating the initial root-array JSON query as a real missing report row was rejected after schema inspection proved the row exists under `reports`.

Scalability potential: Low-tier devices get no additional runtime work. Middle/high/ultra behavior is unchanged: continuous cadence and LED StructuredBuffer presentation remain the scaling path. The ledger improvement prevents future agents from colliding with or reallocating the charger range.

Hardware Impact: Runtime cost is 0 us. Documentation/proof cost only. The practical gain is preventing BufferID ownership ambiguity that could corrupt Vault payload routing or create compile-wall churn later.

## Decision 55 - Scanner-Enforced Ledger Proof

Problem: Loop 38 registered `72300..72310` in the binary payload ledger, but the scanner/report contract did not verify that registration. That made the proof dependent on prose in `LOG_SHINOBU_230.md` instead of the Task 19 machine-readable equipment report. A future edit could remove the ledger entry while `Charger_OOP_Scanner` still returned `PASS`.

Solution: Added `binaryPayloadLedgerRangeRegistered` and `binaryPayloadLedgerBoundaryRegistered` to `Charger_OOP_Scanner`. The scanner reads `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, checks the `72300..72310` range, the SHINOBU_230 boundary heading, `72310` mock inventory slot route, and `Dump_SHINOBU_230.bin`, then includes those booleans in the verdict. The shared equipment report was updated to the same schema.

Rejected Alternatives: Leaving the ledger proof in log text was rejected because Task 19 requires static proof output to `EQUIPMENT_OPTIMIZATION_REPORT.json`. Adding a separate Python checker was rejected because this is editor-only scanner-owned proof and no new toolchain dependency is needed. Running Unity or dotnet was rejected because CPU/build gate remains blocked.

Scalability potential: Runtime behavior is unchanged across low/middle/high/ultra tiers. The hardening protects the Vault range from future collision and keeps the charger graph/scalability math tied to a documented payload route.

Hardware Impact: Runtime cost is 0 us. Editor scanner pays one `File.ReadAllText` on the ledger and a handful of ordinal string checks. This is acceptable because the scanner is a cold proof tool, not gameplay.

## Decision 56 - Runtime Assembly Isolation Bridge

Problem: The charger logistics runtime and contracts were still under the root `Hecton8.Core` asmdef. Creating a naive Power runtime asmdef would have forced `Hecton8.Core` to reference the Power runtime because `BatteryCharger.cs` directly called `BatteryChargerLogisticsRuntime`. That is the compile-wall failure mode the mandate forbids.

Solution: Moved the charger logistics runtime/contracts/gizmo into `Assets/_Project/Scripts/Power/BatteryChargerLogistics/` and added `Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef`. Added `Hecton8.Core.BatteryChargerLogisticsBridge`, a cold delegate table owned by Core, so the Gameplay facade depends only on Core. The Power runtime registers its methods into the bridge at boot and clears them during reset/shutdown. Added an editor asmdef for the X-Ray/scanner tools and scanner/report fields that gate on asmdef presence, no sibling runtime refs, no direct facade runtime calls, and runtime bridge registration. The remaining `using Hecton8.Inventory` resolves the Core-contract `InventorySlotDTO` ABI marker; there is no asmdef reference to `Hecton8.Inventory.Routing.Runtime`.

Rejected Alternatives: Adding `Hecton8.Power.BatteryChargerLogistics.Runtime` to `Hecton8.Core.asmdef` was rejected because it creates the direct Core-to-domain compile wall. Moving the entire `Power/` folder under a new asmdef was rejected because unrelated Power files would be swept into this lane. Expanding `GlobalRegistry` with a new service slot was rejected because it touches a massive core registry header for a cold facade bridge. Reflection or scene lookup was rejected because read/register accessors must stay pure and allocation-free.

Scalability potential: Low/middle/high/ultra runtime math is unchanged. The split protects iteration time and keeps saved CPU budget available for the existing continuous `GlobalQualityWeight` cadence and shader-buffer LED Dear Lie. No gameplay truth owner, DTO layout, save identity, or authority route changed.

Hardware Impact: Hot Burst charge kernel cost remains 0 us changed. Cold facade calls now go through a static delegate bridge; that is outside the scheduled charge kernel and only affects interaction/registration/readback paths. The real gain is compile-wall isolation and avoiding a future whole-Core rebuild cascade for Power-only logistics edits.

## Decision 57 - Locked Simulation Tick Authority

Problem: `BatteryChargerLogisticsRuntime.ScheduleSimulation` accumulated `timing.FrameDelta` before deciding whether to schedule the deterministic charge transaction kernel. That made charge authority dependent on render-frame pacing when the dispatcher did not provide a fixed lane value, violating the rollback rule that critical state changes must be driven by a locked simulation tick rather than variable frame time.

Solution: Added `SimulationTickDeltaSeconds = 1f / 60f` and `ResolveSimulationTickDelta(in DispatcherTimingDTO timing)`. The resolver uses finite positive `timing.FixedDelta` when the dispatcher supplies a fixed-step bridge, clamped to `1/240..1/5`, and otherwise returns the locked 60 Hz domain constant. `ScheduleSimulation` now accumulates that resolved tick value; the Burst job still receives a single scalar `DeltaSeconds`, so DTO layout and hot pointer kernel shape do not change. `Charger_OOP_Scanner` now proves `lockedSimulationTickDeltaUsed=true` and `frameDeltaBypassedForChargeAuthority=true`, and gates the shared report verdict on both fields.

Rejected Alternatives: Keeping `FrameDelta` was rejected because it lets render stalls alter authority-side charge transfer and rollback snapshots. Adding a new field to `DispatcherTimingDTO` was rejected as core contract churn outside SHINOBU_230 scope and unnecessary because `FixedDelta` already exists. Moving charger scheduling to a fixed dispatcher interface was rejected for this loop because it would expand the assembly split blast radius; the local resolver gives deterministic authority without changing global dispatcher contracts.

Scalability potential: Low-tier devices now shed charge-kernel frequency through the existing continuous `GlobalQualityWeight` cadence without changing total authority time based on render stalls. Middle/high/ultra tiers still resolve cadence through `math.smoothstep` and `math.lerp(5f, 60f, q)`, reaching per-frame 60 Hz charge transactions at quality 1.0 while preserving the same DTO/save identity and shader-buffer LED Dear Lie.

Hardware Impact: Hot kernel cost is unchanged: one scalar `DeltaSeconds` is written into the job exactly as before. The scheduler replaces a variable frame-time read with a deterministic finite resolver, estimated below 0.01 us per dispatcher tick. The practical gain is deterministic rollback behavior and removal of frame-pacing drift from power-grid energy conservation.

## Decision 58 - GlobalRegistry Service Bridge Hardening

Problem: Loop 40 isolated the Power runtime assembly, but the Core facade bridge still used a managed delegate table. That protected the compile wall but did not match the stricter cross-domain route rule, which allows GlobalRegistry service locator, unmanaged function pointers, or typed SignalBus payloads. Unmanaged function pointers are a poor fit for these facade methods because the target bodies cross managed Unity/runtime boundaries: `IDataVault`, `GlobalRegistry`, `GraphicsBuffer` lifecycle state, and cold authored inventory checks. SignalBus is also wrong for `TryRegister`/`TryRead` because the facade needs synchronous commit/fail-closed answers before removing or rolling back inventory.

Solution: Replaced the delegate table with `IBatteryChargerLogisticsService`, published by `BatteryChargerLogisticsRuntime` through `GlobalRegistry.RegisterBatteryChargerLogisticsRuntime(this)`. `GlobalRegistry` was opened as `partial` and the new service route lives in `GlobalRegistry.BatteryChargerLogistics.cs`, keeping the edit local instead of inserting into the huge registry body. `BatteryChargerLogisticsBridge` now caches the registry-published service via `BindService` and never polls `GlobalRegistry` from facade reads. The runtime implements the interface explicitly and unregisters during reset/shutdown. Scanner/report proof now gates on `runtimeRegistersGlobalRegistryService`, `bridgeDelegateTableEradicated`, `bridgeUsesCachedRegistryService`, and `globalRegistryBatteryServiceRoute`.

Rejected Alternatives: Keeping the managed delegate bridge was rejected because it is not one of the allowed cross-domain routes. Adding an unmanaged `[UnmanagedCallersOnly]`/function-pointer table was rejected because the methods are not pure Burst-compatible math kernels and would cross managed Vault/Unity lifecycle APIs; this would be compile-risk without performance gain. Adding a full dense `GlobalRegistryServiceSlot` enum entry was rejected for this pass because it touches the central registry atlas and multiple service-slot proof maps; the partial route gives cold service identity without broad core churn. SignalBus was rejected because these facade calls require synchronous return values.

Scalability potential: Low tier keeps the same continuous 5..60Hz cadence shedding and no per-frame registry polling. Middle/high/ultra keep the same GPU StructuredBuffer LED Dear Lie and hum signal route. Gameplay truth ownership, DTO layout, save identity, and authority route remain unchanged; only the cold cross-assembly publication path changed.

Hardware Impact: Hot Burst charge kernel cost remains 0 us changed. Cold facade interactions replace delegate invocation with one cached interface dispatch. Boot/shutdown pay one registry publish/unpublish. The measurable gain is architectural: no direct sibling runtime reference and no ad-hoc delegate table left between Core facade and Power runtime.

## Decision 59 - Inventory Reservation Fence For Charger Insert

Problem: The cold `BatteryCharger.InsertBatteryFromInventory` facade removed one item from `PlayerInventory` before the charger bridge accepted the target slot. The old path attempted rollback with `TryAddItem`, but that was not a hard owner-local reservation proof: a failed charger commit could depend on later managed reinsert capacity instead of preserving Inventory ownership until the Power-owned charger link accepted the handoff.

Solution: Replaced the remove-first sequence with the existing Inventory-owner `PlayerInventory.CraftReservation` fence. `BatteryCharger` now reserves one matching battery hash into a preallocated `PlayerInventory.CraftReservation[1]` scratch buffer, calls `InsertBattery` only after reservation success, releases the reservation if the charger commit fails, and commits the inventory reservation only after the charger accepted the item. If the inventory commit then fails, the charger slot is removed and a development-build rollback fault is emitted. `Charger_OOP_Scanner` now proves `playerInventoryBridgeHardReservationProof=true`, `playerInventoryBridgeRemovesBeforeChargerCommit=false`, and the reservation/commit/release sequence in the shared equipment report.

Rejected Alternatives: Keeping the remove-first path was rejected because rollback is weaker than owner-local reservation. Adding a new Inventory API was rejected as cross-domain churn; the existing craft-reservation contract already provides a bounded owner-local lock. Allocating a reservation array during interaction was rejected; the facade owns one cold, annotated scratch slot instead.

Scalability potential: Low-tier runtime cost is unchanged in the Burst charge kernel and remains 0 us hot-path. Middle/high/ultra behavior is unchanged: continuous `GlobalQualityWeight` controls charge cadence, and the LED Dear Lie remains GPU-buffer driven. The change strengthens the cold human interaction seam without changing DTO layout, save identity, or power authority route.

Hardware Impact: Hot kernel cost remains 0 us changed. Cold interaction adds one reservation check and one reservation commit on successful inventory-to-charger transfer. The gain is conservation proof at the managed facade boundary: Inventory remains the source owner until the charger bridge accepts the slot, removing the prior rollback-capacity dependency.

## Decision 60 - Dead Rollback Helper Eradication

Problem: Loop 43 replaced remove-first inventory insertion with an Inventory-owned reservation fence, but `BatteryCharger.cs` still contained the unused `TryReturnItemToInventory` helper. Even without callsites, that dead helper preserved the old rollback-capacity route in source and could mislead future maintenance or scanner authors into restoring the weaker pattern.

Solution: Deleted `TryReturnItemToInventory` from the managed facade. Re-ran a targeted source scan against `BatteryCharger.cs`; there are now zero hits for `TryReturnItemToInventory`, `playerInventory.RemoveOneItem(`, or direct grid `RemoveItemAt(` in the charger facade. The shared equipment report row already proves the replacement route: reserve before charger commit, release on charger failure, and commit only after charger acceptance.

Rejected Alternatives: Leaving the dead helper was rejected because source-level fossils invite regression. Broadening scanner rules to ignore the helper was rejected because the code should be simpler than the exception. Editing `PlayerInventory` was rejected because its existing craft-reservation API already provides the required owner-local fence.

Scalability potential: Low/middle/high/ultra behavior is unchanged. This is a cold facade cleanup; the Burst charge graph, continuous `GlobalQualityWeight` cadence, GPU LED Dear Lie, telemetry ring, and Vault DTO layout are untouched.

Hardware Impact: Runtime cost remains 0 us changed. The practical gain is reduced maintenance risk at the Inventory-to-Power handoff and one fewer managed method for future compile/import churn.

## Decision 61 - Registry-Owned Bridge Reset

Problem: The GlobalRegistry service route removed the managed delegate table, but `BatteryChargerLogisticsBridge.Clear()` was still a public unconditional clear. Normal shutdown called it after matched registry unregister. In a reload/teardown interleaving, an old runtime instance could clear a newer service binding after `CompareExchange` correctly refused to unregister it.

Solution: Deleted the public bridge clear method. Added `GlobalRegistry.ResetBatteryChargerLogisticsRuntimeForDomainReload()` for the `SubsystemRegistration` window, using `Interlocked.Exchange` to clear the registry slot and then the cached bridge. Normal runtime shutdown now calls only `GlobalRegistry.UnregisterBatteryChargerLogisticsRuntime(this)`, which clears the bridge only when the previous slot matches the shutting-down instance. Updated the scanner/report and binary payload ledger to prove the direct clear route is gone.

Rejected Alternatives: Keeping unconditional `BatteryChargerLogisticsBridge.Clear()` was rejected because it can violate owner-local service identity. Making the bridge clear compare against an expected service was rejected because the Power runtime should not directly own Core bridge mutation; the registry is the owner of the route. Leaving this as a rationale-only convention was rejected because scanner/report proof already gates the compile-wall route.

Scalability potential: Low/middle/high/ultra charge math is unchanged. The change affects only cold boot/shutdown service identity; the continuous cadence, Vault handles, DTO layout, shader LED Dear Lie, and telemetry ring remain identical.

Hardware Impact: Hot path cost remains 0 us changed. Boot/domain-reset pays one `Interlocked.Exchange`; shutdown pays the existing `CompareExchange`. The gain is preventing stale runtime teardown from clearing a live registry-published service.

## Decision 62 - Cadence Cap Remainder Preservation

Problem: `ScheduleSimulation` correctly used locked simulation tick delta and continuous `GlobalQualityWeight` cadence, but the safety cap `integrationDt = min(accumulator, 1s)` then zeroed `_authorityAccumulator`. A long hitch over one second would therefore discard elapsed authority time and violate the requirement that cadence shedding must preserve the mathematical time-to-full.

Solution: Keep the one-second per-job cap, but subtract only the executed slice: `_authorityAccumulator = max(0, _authorityAccumulator - integrationDt)`. This bounds worst-case per-job transfer after a hitch while carrying the remaining elapsed authority time into later dispatcher frames. Added scanner/report proof field `cadenceCapPreservesAccumulatorRemainder=true`.

Rejected Alternatives: Removing the cap was rejected because a multi-second hitch could create a single large transfer spike and fault cascade. Keeping zeroing was rejected because it loses gameplay-economy time. Running catch-up jobs in a same-frame loop was rejected because it would violate dispatcher-owned completion windows and risk frame spikes.

Scalability potential: Low-tier devices can run at the 5Hz end of the continuous cadence without losing charge time during stalls. Middle/high/ultra still approach 60Hz through `smoothstep`/`lerp`. The cap affects per-job chunk size only; it does not change truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: Hot scheduling cost adds one scalar subtraction and `math.max` when a job is actually scheduled. Estimated below 0.01 us per scheduled charge pass. The gain is deterministic economic conservation under long frame stalls without same-frame catch-up loops.

## Decision 63 - Coalesced Skipped-Cadence Telemetry

Problem: Low-quality cadence shedding previously called `RecordSkippedCadenceFrame` on every scheduler frame below the execution period and wrote a full `ChargerTelemetryEntry` row each time. At a 5 Hz charge cadence under a 60 Hz dispatcher, that could replace roughly 55 of 60 black-box rows per second with skip-only records, reducing forensic density for the actual charge transactions, atomic failures, NaN flags, and power draw.

Solution: Converted skipped cadence recording to an in-runtime scalar counter. `RecordSkippedCadenceFrame` now increments `_skippedCadenceFrames` and does not write the telemetry ring. The next executed `WriteTelemetryFrame` ORs `TelemetryFlagSkippedCadence`, writes the coalesced count into `ChargerTelemetryEntry.SkippedCadenceFrames` at offset 60, then clears the counter after the row is emitted. `ChargerTelemetryEntry` stays exactly 64 bytes; the offset-60 tail lane was renamed from anonymous `Reserved0` to a meaningful counter and the layout audit now checks that field.

Rejected Alternatives: Keeping one row per skipped frame was rejected because black-box history should prioritize executed charge state and faults. Adding a second skip-only Vault ring was rejected as unnecessary memory and ownership surface. Dropping skipped-cadence telemetry entirely was rejected because cadence shedding still needs forensic proof. Reusing `DeltaSeconds` for the skip count was rejected because it would corrupt integration-time semantics.

Scalability potential: Low-tier devices still reduce charge work continuously through `GlobalQualityWeight` cadence, but the 300-frame ring now preserves real charge rows while carrying the skipped-frame count. Middle/high/ultra paths are unchanged because fewer frames are skipped as cadence approaches 60 Hz. DTO size, BufferID identity, save identity, and authority route remain unchanged.

Hardware Impact: Each skipped scheduler frame now pays one saturated scalar increment instead of one 64-byte NativeArray write plus cursor update. Exact runtime microseconds remain pending Unity profiler proof; static cost reduction is small per frame but materially improves forensic signal quality under thermal throttling.

## Decision 64 - Editor Tuning DTO Coherence

Problem: The editor/runtime tuning bridge wrote `QualityOverride` and flags before recomputing `GlobalQualityWeight` and `CadenceHz`, but the recompute path read the old tuning row. A designer changing the quality override could therefore leave the Vault tuning row internally stale for one scheduler pass: override fields said one thing, cadence fields still reflected the previous row.

Solution: Added one cold helper, `ApplyPendingTuningValues(ref ChargerTuningDTO dto)`, used by direct editor tuning, pre-simulation tuning, and default tuning. The helper writes max charge rate, exponent, override, flags, resolved continuous `GlobalQualityWeight`, battery capacity, and `CadenceHz` in one DTO update. The resolved quality comes from `ResolvePendingQualityWeight()`, which consumes the pending override immediately when it is finite and non-negative, otherwise falls back to `HomeostasisBrain.GlobalQualityWeight`.

Rejected Alternatives: Leaving the one-frame stale row was rejected because Task 16 requires real-time editor control without recompilation. Recomputing cadence in the X-Ray UI only was rejected because the Vault row is the authority consumed by the scheduler. Adding a second editor-only mirror field was rejected because it would create shadow tuning state.

Scalability potential: Low/middle/high/ultra tuning now changes the actual continuous cadence immediately through the same helper. This does not introduce binary hardware switches and does not change DTO layout, save identity, BufferID identity, or charge authority route. First-20 route impact: habitat power feedback becomes tunable in Play Mode without stale cadence proof.

Hardware Impact: Hot Burst kernel cost remains 0 us changed. Cold tuning writes now perform one direct quality resolver and one cadence calculation when the editor or pre-simulation owner updates the single tuning DTO. The practical gain is removing a one-pass mismatch between visible designer controls and scheduler cadence.

## Decision 65 - Generation-Handle Slot Fence Proof

Problem: The runtime had already moved cold inventory slot writes to `VaultGenerationHandle<InventorySlotDTO>` plus `TryAcquireWriteLock`, but `Charger_OOP_Scanner` still proved `coldSlotWriteLocksBeforeResolve` by searching for the removed legacy `TryLockBuffer(BufferID.ShinobuInventorySlots)` and direct `TryGetBuffer(...)` route. That created a false evidence surface: rerunning the scanner would not be proving the current descriptor write fence, while the JSON report still carried a stale true value.

Solution: Added `coldSlotWriteUsesGenerationHandleFence` to the scanner and report. The scanner now requires the exact `TryAcquireInventorySlotsWrite(...)` callsite, the descriptor borrow through `TryBorrowInventorySlotHandle(...)`, the writer fence `vault.TryAcquireWriteLock(in handle, SystemID.Power, out slots)`, and the authoritative slot write after the acquired view. The old `coldSlotWriteLocksBeforeResolve` field is now backed by this descriptor-route proof.

Rejected Alternatives: Keeping the old token proof was rejected because it described a route that no longer exists. Removing the field was rejected because downstream audit history already consumes `coldSlotWriteLocksBeforeResolve`. A loose search for any `TryAcquireWriteLock` was rejected because it could pass on an unrelated Vault lane and would not prove the inventory owner fence.

Scalability potential: Low/middle/high/ultra runtime behavior is unchanged. The patch affects only editor/static evidence. The actual cold facade slot write already uses owner-local inventory descriptor locking; continuous charger cadence, DTO layout, save identity, BufferID identity, and the GPU LED Dear Lie are unchanged.

Hardware Impact: Hot path cost remains 0 us changed. Static proof cost is editor-only O(source bytes). The gain is audit integrity: the scanner now verifies the generation-handle write fence actually used by the runtime instead of preserving stale legacy evidence.

## Decision 66 - Runtime Admission And Scanner Truth

Problem: The report carried a false PASS despite two facts the scanner already knew were outside SHINOBU ownership: concrete facade residual imports and Inventory Routing whole-slot writers on `BufferID.ShinobuInventorySlots`. The runtime also consumed `_authorityAccumulator` before job admission, so a failed lock or buffer resolve could erase charge authority without executing a job. DTO write paths still allowed NaN via `math.max`, and telemetry named a schedule-to-finalize wall-time value `BurstMicroseconds`.

Solution: Changed the SHINOBU_230 report verdict to `PARTIAL_BLOCKED_BY_CROSS_DOMAIN_OWNER` and preserved the two owner-boundary findings. Moved accumulator subtraction after buffer resolution and `linkCount > 0`, while clearing the local accumulator when no active links exist so a future inserted battery cannot receive idle back-credit. Added finite sanitizers for charger AUP, charge rate, efficiency scalar, max charge rate, efficiency exponent, and quality override before DTO writes. Renamed the telemetry field to `FenceElapsedMicroseconds@28`, renamed the threshold to `FaultDumpFenceElapsedThresholdMicroseconds`, updated the layout audit, X-Ray label, scanner gates, and ledger. The fault dump route is now explicitly documented as a blocking fault-only exception for NaN or fence-elapsed breach.

Rejected Alternatives: Keeping a PASS verdict was rejected because machine-readable proof must not hide cross-domain blockers. Subtracting authority before buffer admission was rejected because it silently loses deterministic charge time on contention. Adding a catch-up loop was rejected because it would create same-frame job pressure and violate dispatcher-owned completion windows. Keeping `BurstMicroseconds` was rejected because it mislabels wall time and corrupts profiler interpretation. Sending fault dumps through a new background managed queue was rejected for this loop because the route is crash/fault-only and the synchronous exception is now documented instead of being used in healthy frames.

Scalability potential: Low-tier devices still shed work continuously through `math.smoothstep` and `math.lerp(5f,60f,q)`. Below 0.3, fewer charge batches are admitted, but authority time is consumed only when a real batch exists and DTO identity is unchanged. Middle/high/ultra keep the same 60 Hz upper cadence, GPU LED StructuredBuffer Dear Lie, and acoustic signal route. No binary quality switch was added.

Hardware Impact: Hot Burst charge kernel cost remains 0 us changed. The admitted scheduler path keeps one scalar subtraction; failed lock/resolve paths no longer lose authority. Finite guards are cold registration/tuning path only. The practical gain is NaN prevention, conservation under contention, and honest telemetry naming without extra Vault buffers or cache-line growth.

## Decision 67 - Owned Surface Mandate Sweep

Problem: After the admission and telemetry patch, the next risk was not a known failing line but mandate drift: hidden `.Complete()`, `Pack=1`, private native allocation, LINQ/foreach, frame delta, unmanaged property use, or sibling runtime references could have entered the SHINOBU_230 surface while patches accumulated across loops.

Solution: Ran a scoped owned-file grep over the battery charger logistics runtime/contracts/editor bridge/facade files instead of a whole-repo scan. The owned surface has no `Pack=1`, hidden `.Complete()`, private native collection allocation, `foreach`, LINQ, `UnityEngine.Random`, `Time.deltaTime`, or hot-path auto-property pattern hits. Re-checked Burst jobs: all three jobs use `CompileSynchronously=true`, `FloatMode.Deterministic`, `FloatPrecision.Standard`; distinct arrays/raw pointers carry `[NoAlias]`. Re-checked asmdefs: runtime references Core/Core.Contracts/Core.Memory and Unity packages only, not sibling Gameplay/Construction/Inventory/World/Generators runtime assemblies. Continuous quality remains `math.smoothstep` plus `math.lerp(5f,60f,q)`.

Rejected Alternatives: A broad repo grep was rejected as primary proof because this worktree is shared and many hits belong to other agents/domains. Adding direct references to cross-domain owners was rejected because it would solve scanner findings by breaking the compile wall. Running a rebuild was rejected because CPU was at 100% and the external scanner projection source/meta are still absent.

Scalability potential: Low/middle/high/ultra behavior remains the same as Loop 50: continuous cadence scales work, DTO identity does not change, and the GPU LED Dear Lie is still the visual path. The sweep did not add work; it proved no hidden hot-path regression entered the owned surface.

Hardware Impact: Runtime delta is 0 us. The value is risk reduction: no new GC patterns, no hidden job fences, no alignment hazards, and no new sibling assembly route on the owned files.
