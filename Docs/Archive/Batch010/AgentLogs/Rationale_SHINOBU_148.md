# Rationale_SHINOBU_148

Agent: SHINOBU_148
Domain: EQUIPMENT_THERMAL_AND_BATTERY_GRID
Status: STATIC IMPLEMENTATION HARDENED / COMPILE GATE BLOCKED BY 100PCT CPU

## Decision 00 - Mandate Selection And Ownership Boundary
Problem: Equipment heat and battery math currently may be scattered across individual tool MonoBehaviours, creating per-object update cost and cross-domain coupling risk.
Solution: Use the task-relevant mandates for tool heat/power ownership, ARM64 layout, zero-GC hot paths, native job lifecycle, dispatcher phases, signal lanes, AUP determinism, and power-grid graph boundary before any code generation.
Rejected Alternatives: Reading only AGENTS.md was rejected because SHINOBU_148 explicitly requires registry mandates. Inventing DataVault or Thermodynamic Grid APIs was rejected because 20+ agents are active and cross-domain concrete dependencies are forbidden.
Scalability potential: Low uses reduced cadence and flat 32-byte DTOs; Middle keeps normal cadence; High adds denser telemetry; Ultra spends saved simulation cost on VFX/audio consumers, not heavier gameplay truth.
Hardware Impact: Expected gain on i3/MX350 comes from replacing per-tool MonoBehaviour scalar loops and managed collections with a contiguous Burst O(N) pass. Static estimate pending source scan.

## Decision 01 - Initial Data Contract Target
Problem: Tool state requires deterministic layout for ARM64, Burst, rollback snapshots, and blind MemCpy publication.
Solution: Target `ActiveEquipmentDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]` with raw public fields at mandated offsets and named padding bytes at 24-31.
Rejected Alternatives: Auto-layout structs were rejected because offset drift would break ARM64/cache and netcode snapshot assumptions. Properties were rejected because CS1612 and stack-copy overhead are explicit task failures.
Scalability potential: Same 32-byte truth struct across Low/Middle/High/Ultra; richer visuals consume a read snapshot rather than bloating truth.
Hardware Impact: Four DTOs fit in two 64-byte cache lines; sequential pass reduces L1 miss risk on i3/MX350 versus pointer-chasing MonoBehaviours.

## Decision 02 - Vault-Only Truth Buffers
Problem: Private persistent NativeArrays and NativeHashMap slot tables violate Data Sovereignty and create fragmentation/ownership ambiguity.
Solution: Request all SHINOBU_148 truth buffers from GlobalDataVault using BufferID lane `71300..71315`, plus existing `ToolRuntimeHeat01` and `ToolRuntimeBatteryCharge`; remove the private NativeHashMap and deferred battery-drain arrays. Slot lookup is a bounded 16-slot linear scan over owner mirrors and DTO hashes, not a heap-backed collection.
Rejected Alternatives: Keeping fallback Persistent NativeArrays was rejected after the second mandate because it creates a second allocator owner. NativeHashMap was rejected because MaxTrackedTools is 16 and O(16) scan is cheaper than a private persistent hash table.
Scalability potential: Low/Middle/High/Ultra all use the same Vault memory; higher tiers spend extra ALU only on thermal-grid sampling quality, not on larger ownership structures.
Hardware Impact: Removes two private NativeArray buffers and one NativeHashMap from the equipment runtime. On i3/MX350 this is mainly memory-fragmentation and cache predictability, not raw ALU.

## Decision 03 - Deterministic Burst Thermo-Electric Kernel
Problem: Battery drain, heat generation, water cooling, and overheat/depleted events were previously spread across tool components and visual facades.
Solution: Implement `EquipmentThermalBatteryJob` as deterministic Burst `IJobParallelFor` over raw pointers with `[NoAlias]`; each index mutates one `ActiveEquipmentDTO`, writes one `EquipmentGridLoadRequest`, and writes one 64-byte `EquipmentIntegrationCounters` slot.
Rejected Alternatives: `IJob` internal loop was rejected because it serializes the pass and does not prove thread-local counter ownership. Interface-based tool handlers were rejected because they block Burst devirtualization.
Scalability potential: Low uses 5Hz cadence with accurate accumulated dt and nearest thermal grid. Middle raises cadence and blends sampling. High/Ultra uses near-frame cadence and trilinear ambient reads.
Hardware Impact: At 16 tools, expected runtime is sub-10 us on desktop and materially lower than per-component dispatch on i3/MX350; false-sharing is avoided by one cache-line counter per worker.

## Decision 04 - Thermal Grid Cooling With Continuous Math LOD
Problem: Cooling must depend on Agent 117 thermal grid without binary quality switches or absolute-coordinate float jitter.
Solution: Subtract thermal-grid root double3 AUP from tool double3 AUP, cast only the local delta to float3, and map to cells. `GlobalQualityWeight` controls `tickInterval` and ambient sampling: below ~0.25 it collapses to nearest-cell; above that it polynomially blends toward 8-tap trilinear.
Rejected Alternatives: Direct `AbyssalThermalManager.SampleThermalFlow` calls from managed tool code were rejected because they are component-bound and cadence-gated outside Burst. Casting absolute AUPs to float was rejected for 100km jitter.
Scalability potential: Low = 1 tap / 5Hz. Middle = partial trilinear blend / medium cadence. High = full trilinear / near-frame. Ultra spends saved CPU on VFX consumers through signals, not extra gameplay truth.
Hardware Impact: Low-tier avoids seven thermal reads per tool and reduces scheduler pressure; high-tier buys smoother cooling near cell boundaries.

## Decision 05 - Dear Lie Signal Routing
Problem: Tool scripts should not instantiate steam, audio, UI, or disable GameObjects from the thermal truth loop.
Solution: Emit unmanaged `EquipmentOverheatSignal` and `ToolDepletedSignal` from the Burst pass into NativeQueues, then route through SignalBus after the simulation fence. VFX/audio consume severity scalars and produce the illusion.
Rejected Alternatives: Direct particle/audio spawning from LaserCutter/Flashlight was rejected because it allocates, couples domains, and cannot run in Burst.
Scalability potential: All tiers emit the same compact scalar truth. High/Ultra consumers can render richer distortion or audio without changing simulation state.
Hardware Impact: Removes managed event allocation and GameObject activation pressure from the hot path.

## Decision 06 - Human Tuning Without CSharp Recompile
Problem: Designers need to adjust heat, water cooling, and power draw without touching source constants.
Solution: Add Vault-backed `EquipmentTuningDTO`, Vault-backed `EquipmentHardwareSpecDTO`, a cold `ReadOnlySpan<byte>` CSV parser, and an editor-only Tool Thermo-Electric Tuner with graph/gizmo/mock controls.
Rejected Alternatives: ScriptableObject-only tuning was rejected for this runtime truth because it reintroduces managed object reads. `string.Split` CSV ingestion was rejected for allocation.
Scalability potential: Low devices can tune slower cadence and stronger cooling cheaply; middle/high/ultra can tune smoother thermal visuals and higher power/heat ceilings while preserving the same DTO route.
Hardware Impact: Gameplay path remains zero-GC; editor strings/labels are editor-only.

## Decision 07 - Compile Gate Discipline
Problem: Verification requires a compile, but project policy forbids launching dotnet while existing dotnet/csc are active or CPU exceeds 50 percent.
Solution: Ran process and CPU gate checks before build. Earlier checks observed active `dotnet`/`csc.exe`; the latest check shows no compiler process output but CPU remains 100 percent, so compile is still deferred. Static checks were run instead: `git diff --check` and targeted `rg` scans for `Pack=1`, private NativeArray/NativeHashMap allocation, LINQ/foreach, `Time.deltaTime`, and local heat/battery drain mutation in edited hot path.
Rejected Alternatives: Forcing `dotnet build` during 100 percent CPU pressure was rejected because it violates the hardware protection mandate and would add noise to unrelated compile-wall failures.
Scalability potential: No runtime impact; protects iteration hardware and keeps the compile-wall signal clean.
Hardware Impact: Avoids compounding 100 percent CPU load on the workstation.

## Decision 08 - Remove Last Main-Thread Thermal Mutation
Problem: The legacy overcharge branch in `ModularEquipmentEngine.Tick()` still grew `ToolState.InternalHeat` before the Burst solver, creating a second authoritative thermal path.
Solution: Delete the pre-job overcharge heat growth. Overcharge now modifies only the per-slot `HeatGenerationRate` prepared for `EquipmentThermalBatteryJob`; explosion handling remains after the job publishes the DTO, so the fault response still reacts to solver truth.
Rejected Alternatives: Keeping the Tick-side heat growth was rejected because it violates one fact -> one owner -> one route. Moving explosion side effects into Burst was rejected because jobs must emit data, not call Unity object behavior.
Scalability potential: Low/Middle/High/Ultra now scale through one cadence-controlled solver path; the same overcharge curve feeds all tiers.
Hardware Impact: Removes a per-frame scalar thermal mutation from the main thread and prevents duplicate heat integration on i3/MX350-class hardware.

## Decision 09 - Burst Cold Initialization For Equipment Vault Lane
Problem: Task 15 requires avoiding zero-fill cost through `UninitializedMemory` plus controlled initialization; main-thread `UnsafeUtility.MemClear` alone did not prove the requested Burst initialization path for SHINOBU_148 buffers.
Solution: Add `ClearActiveEquipmentNativeStateJob`, a deterministic Burst `IJobParallelFor` over raw `[NoAlias]` pointers for active DTOs, published DTOs, AUP samples, grid requests, telemetry ring/cursor, padded counters, and hardware specs.
Rejected Alternatives: Keeping only `ClearNativeArray` was rejected because it meets memory correctness but not the explicit Burst-init requirement. A generic reflection clear was rejected because it would allocate and break Burst.
Scalability potential: Cold boot cost scales linearly with buffer length and remains identical across Low/Middle/High/Ultra; runtime quality scaling stays in the integration job.
Hardware Impact: Keeps boot initialization contiguous and worker-friendly; avoids making i3/MX350 pay managed or OS zero-fill costs for SHINOBU_148 state.

## Decision 10 - Cached Registry Dependencies For Tool Hot Paths
Problem: `PlayerTool`, `LaserCutter`, and `FlashlightTool` still had direct `GlobalRegistry` reads in use, brownout, recoil, or thermal side-effect paths. Even if most reads are cheap static accessors, this leaves service routing ambiguous and violates the one-route proof expected by the Compile Wall mandate.
Solution: `PlayerTool` now implements registry hot-swap listeners and caches `IModularEquipmentService`, `IPowerGridService`, `ISubmarineRuntimeContext`, and `IPlayerRuntimeContext` during spawn/cold cache. Brownout feedback, wireless power availability, recoil, overcharge damage, `LaserCutter` open-water boil, and `FlashlightTool` runtime resolve use protected cached accessors instead of hot `GlobalRegistry` polling. `ModularEquipmentEngine` already mirrors the same pattern for DataVault, thermodynamics, power grid, player, submarine, and scalability tier.
Rejected Alternatives: Keeping per-use `GlobalRegistry.ModularEquipment`, `GlobalRegistry.Submarine`, or `GlobalRegistry.Player` lookups was rejected because it hides dependencies inside tool behavior. Passing concrete sibling runtime references into tools was rejected because it would widen coupling; existing registry service contracts and hot-swap events are the correct route.
Scalability potential: Low/Middle/High/Ultra all keep the same cached service route; high tiers can spend saved main-thread attention on richer downstream VFX/audio while the solver remains a single authority.
Hardware Impact: Estimated 1-4 us saved in aggregate during busy hand-tool frames on i3/MX350-class hardware by removing repeated service lookup branches from tool-use paths; larger gain is architectural proof that thermal/battery state still has one owner and one route.

## Decision 11 - Remove Legacy Flashlight Suit-Energy Battery Fallback
Problem: `PlayerFlashlight` no longer drained battery locally, but its `EnergyPercent` read path could still fall back to `HectonSurvivalSystem.EnergyPercent` when no `FlashlightTool` adapter was bound. That made suit energy a second battery truth source for the flashlight facade and could keep a lamp alive with no central equipment battery owner.
Solution: Remove the serialized survival battery fallback and dead `PlayerTool` survival binding. `PlayerFlashlight.EnergyPercent` now reports only the bound `IBatteryTool` charge, which is supplied by `FlashlightTool` and the central `ModularEquipmentEngine` readback. If the central adapter is missing, the lamp turns off instead of silently using suit energy. `PlayerFlashlight` also caches `IPlayerRuntimeContext` through registry hot-swap for camera resolve instead of polling `GlobalRegistry.Player` from a Tick-reachable path.
Rejected Alternatives: Keeping suit energy as a compatibility fallback was rejected because it violates one fact -> one owner -> one route. Moving flashlight presentation into the Burst solver was rejected because visuals/input remain presentation concerns; only battery and heat truth belongs in SHINOBU_148 central math.
Scalability potential: Low/Middle/High/Ultra use the same charge authority. Low-tier sheds solver cadence and thermal taps; presentation continues to consume scalar readback without owning charge.
Hardware Impact: Removes one cold component resolve branch and prevents a long-lived no-drain flashlight state. Runtime microsecond gain is small, estimated under 1 us normally, but it eliminates an authority leak that would break deterministic battery accounting.

## Decision 12 - Seaglide/Manta Battery Drain Route Lockdown
Problem: `MantaScooter` was still subtracting `_currentCharge` during `UsePrimary()` and pushing that drain through inventory condition as a second persistent charge route. `ScannerTool` and `RepairTool` also kept local charge fields as runtime truth mirrors instead of treating them as cold fallback values.
Solution: Add `IModularEquipmentService.SetToolActive(uint,bool,float)` so Manta can publish active intent plus current propulsion draw rate while `EquipmentThermalBatteryJob` remains the only battery subtractor. Remove Manta per-frame `_currentCharge` subtraction and inventory-condition drain. `FlashlightTool`, `RepairTool`, `ScannerTool`, and `MantaScooter` now read runtime charge through `GetRuntimeBatteryNormalized(...)`, and only sync their local charge fields before cold unregister/re-register boundaries.
Rejected Alternatives: Calling `ConsumeBattery()` from Manta was rejected because it would preserve per-frame managed charge mutation. Keeping inventory condition as a hidden battery ledger was rejected because it creates one fact with two owners. Making Manta a concrete `ModularEquipmentEngine` dependency was rejected; the route stays on the contract interface.
Scalability potential: Low uses the same active/draw request but the solver cadence collapses toward 5Hz with accumulated dt; Middle/High/Ultra can use smoother draw-rate changes and downstream propulsion visuals without changing the charge owner.
Hardware Impact: Removes Manta local charge subtraction and inventory lookup attempts from use frames. Static estimate: 4-10 us saved during active propulsion on i3/MX350-class hardware, plus eliminated inventory service polling while swimming.

## Decision 13 - Tool Surface Unity Frame Hook Removal
Problem: `HarpoonLauncherTool` still used `LateUpdate()` for GPU tracer presentation. It did not own battery or heat state, but it was still a Unity frame-method in a tool script and therefore weakened the proof that tool-surface frame hooks are not bypassing the dispatcher.
Solution: Move tracer rendering to `ILateFrameTickable.LateFrameTick()` and register/unregister the harpoon tool through `GlobalRegistry.TryRegisterLateFrameTickable` on the player priority lane. Keep GPU tracer resource allocation cold and presentation-only. Adjust the Manta active bridge so inactive calls clear only the active bit and do not zero compiled `BatteryDrainPerSecond`.
Rejected Alternatives: Leaving `LateUpdate()` as "only rendering" was rejected because the mandate requires frame hooks removed from tool scripts. Folding tracer drawing into the thermal solver was rejected because presentation draw calls are not battery/heat authority and do not belong in Burst simulation.
Scalability potential: Low/Middle/High/Ultra now route tool presentation frame work through dispatcher lanes, giving the scheduler a single place to cadence or suspend presentation if needed while thermal/battery truth remains in the Burst equipment pass.
Hardware Impact: Small direct gain, estimated under 1 us, but it removes a Unity message dispatch from a tool component and prevents hidden per-frame behavior outside dispatcher accounting.

## Decision 14 - Non-Sticky Tool Activity Intent
Problem: Base `PlayerTool.TryConsumeRuntimeEnergy()` set `SetToolActive(toolId, true)` for hold tools, but that external mask was only cleared on unequip. Laser/repair/scanner style tools could remain active in the central solver after input release, creating false battery drain and heat.
Solution: Remove the base `SetToolActive(true)` call. Base hold tools now publish a short `_runtimeActiveIntentSeconds` countdown after a successful use, advanced by `PlayerToolManager` dispatcher `deltaTime` before blocked/lockout early returns; `ModularEquipmentEngine` reads `HasRuntimeActiveIntent`. Continuous/toggle tools keep explicit external intent through `SetToolActive`: Manta for propulsion draw and Flashlight for latched lamp state.
Rejected Alternatives: Clearing the external mask every central tick was rejected because it would break toggled tools and require every continuous tool to republish in a strict order. Keeping `WasRecentlyUsed(Time.time)` as the thermal/battery gate was rejected because SHINOBU_148 simulation truth should not depend on wall-clock `Time.time`.
Scalability potential: Low/Middle/High/Ultra preserve the same intent route; lower tiers integrate less often but do not inherit stale activity from released input.
Hardware Impact: Prevents indefinite false O(N) active work and removes sticky battery/heat drain. Static estimate: 2-8 us saved during post-use idle frames on i3/MX350 plus correctness for rollback snapshots.

## Decision 15 - Brownout Feedback Through Contract
Problem: `PlayerTool` still cast cached `IModularEquipmentService` back to `ModularEquipmentEngine` for wireless/tool brownout flicker. That concrete route was not a second battery owner, but it violated the compile-wall proof by requiring tool base code to know the runtime implementation.
Solution: Add `TryGetWirelessBrownoutFeedback` and `TryGetToolBrownoutFeedback` to `IModularEquipmentService`, make the runtime methods public contract members, and have `PlayerTool` call the interface only.
Rejected Alternatives: Keeping the concrete cast was rejected because it weakens service isolation. Moving flicker computation into each tool was rejected because brownout remains central equipment state derived from the solver/power-grid route.
Scalability potential: Low/Middle/High/Ultra use the same contract scalar; lower tiers can suppress downstream presentation without changing battery/thermal truth.
Hardware Impact: Microsecond gain is under 1 us; the value is compile-wall isolation and removal of a runtime type test from brownout readback.
