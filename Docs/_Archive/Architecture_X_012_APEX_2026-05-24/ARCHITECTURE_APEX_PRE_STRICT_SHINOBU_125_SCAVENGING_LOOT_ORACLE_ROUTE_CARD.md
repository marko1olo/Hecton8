# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_125 Scavenging Loot Oracle Route Card

Date: 2026-05-19

Owner: SHINOBU_125

Owner domain: ECHELON 4 / Scavenging & Harvesting + S.O.A. Inventory handoff

Status: STATIC ROUTE CARD / YELLOW STATIC_SOURCE_ONLY / GREEN REVIEW ARTIFACT REQUIRED / UNITY COMPILE BLOCKED BY EXTERNAL DEPENDENCY / RUNTIME PROOF PENDING

Source anchor: `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`.

Unity asset metadata: `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs.meta`.

Contract source-kind anchor: `Assets/_Project/Scripts/Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs`.

Visual signal contract anchor: `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs`.

## R48 Exact Route Field Normalization

Route ID: SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD

Owner: SHINOBU_125

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

Route ID: SCAVENGING_LOOT_ORACLE_DIRECT_INVENTORY

Problem: depleted resource nodes were routed through pooled loot prefabs, rigidbody impulse, delayed despawn, and pickup interaction before inventory truth changed.

Why owner-local data is insufficient: depletion, inventory acquisition, save tombstone, HUD notice, and VFX fake are separate owners. One local `ResourceNode` field cannot own all facts.

Why direct caller/owner interface is insufficient: direct inventory mutation would bypass inventory owner rules and save rollback visibility.

Instrument:

- SignalBus<T> first-party broadcast: `ItemAcquiredSignal`, contract-owned `VisualScavengeSignal`, `ResourceDepletionDeltaSignal`, `HUDNotificationSignal`

- GlobalDataVault / IDataVault: flat loot tables, request/yield staging, biome modifiers, telemetry, CSV scratch

- Black-box/telemetry route: `ScavengingTelemetryEntry[300]`

- Item acquisition source-kind: `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` = 13 for scavenging oracle, owned by Core contracts signal surface. The same narrow source-kind surface preserves existing `HarvestableOutcrop` = 14; source-kind 9 remains manual pickup.

Global authority proof boundary: this route remains `YELLOW` until `SignalBus<T>` and `GlobalDataVault` evidence names owner, producer/consumer phase, capacity/overflow behavior, failure/telemetry behavior, and a proof artifact tuple. Static source visibility is not runtime proof.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY`. Proof required before GREEN: compile/import/runtime/profiler artifact tuple plus SignalBus/DataVault stress evidence with path, command/tool, timestamp, environment, and output.

Producer phase: `ResourceNode` queues request, `ScavengingLootOracleRuntime` resolves/publishes at Core late-frame. The publish completion is an explicit `[BLOCKING_SYNC_POINT]` signal flush fence because `SignalBus<T>.ParallelWriter` has no producer-handle registration route.

Incremental mining yield: forced oracle requests use `RequestFlagSuppressDepletionDelta`, so the item and visual fake are emitted without tombstoning the node before actual depletion.

Impact debris fake: mining hits publish one `DebrisSpawnSignal.FlagComputeShard` packet; the old pooled runtime shard prefab/Rigidbody path has been removed from `ResourceNode`.

Consumer phase: inventory drains `ItemAcquiredSignal`; VFX/UI consume `VisualScavengeSignal`; save archivist consumes depletion deltas; HUD consumes notification.

Inventory side-effect fence: `PlayerInventory.DrainRepairToolTitaniumSignals()` explicitly excludes `ItemAcquiredSignalSourceKinds.ScavengingLootOracle`, so mined titanium grants do not double as repair-tool durability restoration.

Cadence: dirty only, resource depletion or incremental oracle request.

Frame authority: `ScavengingLootOracleRuntime` owns an internal monotonic simulation frame counter for signal/telemetry stamps; Unity `Time.frameCount` is not used by the oracle.

Yield sample authority: `ResourceNode.ResolveYieldSampleDeltaSeconds()` returns a deterministic fixed sample interval for incremental yield mass; Unity `Time.time` is not used by the SHINOBU resource-node/oracle route until a formal dispatcher `SimulationTickDelta` contract is assigned.

Expected max events/reads per frame: 64 queued requests, 512 maximum-quality visual signals, 64 minimum-quality visual signals.
GlobalQualityWeight behavior: loot math is invariant; valid quality remains a continuous 0..1 scalar, loot VFX scalar is `math.lerp(0.1f, 1.0f, sanitizedQuality)`, and mining impact debris request count follows a smooth polynomial from small compute bursts to dense high-fidelity chip sprays. Non-finite quality input fails closed to 0.0 through `ScavengingLootOracleMath.SanitizeQualityWeight()` before mock requests, queued requests, runtime resolution, telemetry, and impact-debris math.
Editor tuning behavior: `Procedural Loot Tuner` writes continuous tool/rare sliders into active Vault CDF rows; biome slider writes only biome modifier rows; no player hot-path managed tuning table is introduced.

Payload/data shape: unmanaged explicit DTOs and signals.

Managed fields present: no.

UnityEngine.Object fields present: no.

Hot-path value construction proof: gameplay-facing request, resolve, telemetry, publish-signal, visual-AUP, late-frame job descriptor, Vault view, and impact-debris structs use `default` stack locals with direct field writes so explicit-layout padding is zeroed; remaining `new ScavengingHarvestRequestDTO` / `new InventoryCapacityDTO` matches are isolated to `MockHarvestRequestJob`, not player gameplay.

Layout proof: `LootTableEntryDTO` is explicit 16 bytes; validation asserts offsets 0/4/8/12.

Visual signal contract proof: `VisualScavengeSignal` is explicit 80 bytes and carries contract-local `VisualScavengeAup48` at offset 0; `Core.Contracts` does not import `Hecton8.World`.

Capacity: entries 256, requests/yields 64, biome modifiers 128, telemetry 300, audit 32, CSV scratch 64KB.

Active-count law: `_activeLootEntryCount` and `_activeBiomeModifierCount` are the only valid read extents for Vault buffers allocated as `UninitializedMemory`; full-capacity scans are rejected.

CDF selection law: raw-CDF binary search is legal only when every active row passes the tool mask and the CDF is monotonic. Tool-masked sparse tables use deterministic two-pass integer selection, avoiding a hidden O(n log n) prefix recomputation loop.

AUP hash law: local AUP components are sanitized and quantized to millimeters before RNG seed and depletion hash mixing; raw float bit hashing is rejected.

Overflow/failure: prefab payload quantity is multiplied in `ulong`, then clamped through `ScavengingLootOracleRuntime.ClampItemSignalQuantity()` before capacity preflight so `ItemAcquiredSignal`, `VisualScavengeSignal`, and telemetry share the same item quantity. Queue refusal returns false and leaves node intact; inventory-full request emits HUD notification and leaves node intact.

Telemetry fields: root AUP, resource hash, selected item hash, ore hash, frame, total weight, roll, flags, estimated us, table hash, quality.

Black-box fields: same as telemetry; planned/generated-on-fault dump path `Docs/AgentLogs/Dump_LOOT_ORACLE.bin`. No existing artifact is implied unless a timestamped runtime trigger and output are linked.

Profiler marker: pending.

GC proof required: Unity Profiler/GC allocation check pending.

Shutdown/disposal: Vault owns buffers; SignalBus owns native queues; scene-local host unregisters late-frame tick on disable.

ResourceNode depletion guard: owner-local `int` with `Interlocked`; no per-node persistent `NativeArray` remains on the oracle route.

Scene unload behavior: host is cold-created after scene load; no `DontDestroyOnLoad` route is used; subsystem registration resets static owner.

Stale-handle behavior: all handles resolve through `VaultBufferHandle<T>.Resolve` before use.

Rejected alternatives:

- owner-local field: rejected, multiple owners need facts

- cached owner interface: rejected for inventory mutation

- existing SignalBus lane only: rejected, no visual fake payload existed

- existing Vault buffer: rejected, no loot oracle payload storage existed

- cold HectonEventBus hook: rejected, first-party hot gameplay

- no global route needed: rejected, inventory/save/HUD/VFX fan-out is required

Why this does not increase global monolith risk: no `GlobalRegistry` live-state polling is added; GlobalDataVault is used only for flat unmanaged domain buffers; facts leave via typed lanes. This is an architectural risk boundary, not runtime acceptance evidence.

H-Phi impact expected: lowers direct ResourceNode -> ObjectPool/PhysX/inventory entanglement. Root assembly remains existing reality; no new sibling asmdef dependency was added. The shared source-kind fact and visual fake payload now live in narrow contracts files rather than the scavenging runtime file, and the visual fake payload no longer imports World runtime AUP.

Proof required before GREEN: Unity import/compile, Play Mode depletion, Profiler GC 0 B on depletion, SignalBus snapshot verification, 10k audit counts recorded.

Current compile note: current CLI project surface includes `ScavengingLootOracle.cs`, `ItemAcquiredSignalSourceKinds.cs`, and `VisualScavengeSignal.cs`; SHINOBU AUP namespace error was fixed, the visual signal no longer imports `Hecton8.World`, oracle frame stamps no longer use Unity `Time.frameCount`, ResourceNode incremental yield sampling no longer reads Unity wall-clock time, gameplay-facing value-type initializer blocks were replaced by `default` locals plus direct field writes, and quality ingress now fails non-finite values closed to 0.0. R37-era generated-project shielding covers the stale generated `Hecton8.Core.csproj` include for absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`; `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` is present on disk, while `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs` remain absent in `Assembly-CSharp.csproj`. Follow-up Core compile now fails later on external missing contract/source bridge types outside this route card. The visual-signal split, source-kind contract surface, masked CDF path, incremental-yield no-depletion route, impact-debris compute fake, repair-tool side-effect fence, AUP millimeter hash quantization, saturated quantity bridge, item-signal quantity clamp, visual-signal AUP contract detachment, simulation-frame counter purge, yield-sample wall-clock purge, gameplay value-initializer purge, and GlobalQualityWeight finite fail-closed fence are `STATIC_SOURCE` scan orientation only; no current compile, Unity, profiler, or runtime proof is claimed without an artifact tuple.
