# SHINOBU_230 Rationale

Status: IMPLEMENTED / POLISH PASS APPLIED / COMPILE GATE BLOCKED BY CPU > 50%

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

Solution: `GenerateMockChargerNetworkJob` fills 5,000 `ChargerLinkDTO`, SOA inventory slots, power nodes, node hashes, AUPs, and visual states. Runtime fallback schedules the job as `IJobParallelFor` with Burst and completes it during cold vault initialization.

Rejected Alternatives: Hand-authored scene prefabs and editor-only mock MonoBehaviours were rejected because they test object plumbing, not math throughput.

Scalability potential: Low can execute the same mock with throttled cadence. Middle/high/ultra validate full link count and visual overkill buffers.

Hardware Impact: Mock generation is cold. Runtime proof target remains under 10 us for 1,000 links and suspicious above 100 us for 5,000 links until profiler evidence exists.

## Decision 05 - Atomic Energy Transaction

Problem: Inventory slots and power graph nodes can be touched by separate systems. A charge transfer must not clone or destroy energy under contention.

Solution: Job acquires `InventorySlotDTO.ReservedLock` using `Interlocked.CompareExchange`, CAS-deducts `PowerNodeDTO.Potential`, CAS-writes `ConditionFlags`, and rolls power back with a deterministic CAS retry guard if slot write fails. Atomic conflict counters and fault flags are written through Interlocked operations.

Rejected Alternatives: Plain writes, `lock`, `Monitor`, or `NativeParallelHashMap` were rejected. Managed locks are illegal in Burst/hot path; hash maps are extra indirection for fixed slot-to-node links.

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
