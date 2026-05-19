# SHINOBU_125 Scavenging Loot Oracle Route Card

Date: 2026-05-19
Owner: SHINOBU_125
Owner domain: ECHELON 4 / Scavenging & Harvesting + S.O.A. Inventory handoff
Status: STATIC ROUTE CARD / GREEN REVIEW ARTIFACT REQUIRED / UNITY COMPILE PENDING
Source anchor: `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`.
Unity asset metadata: `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs.meta`.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Route ID: SCAVENGING_LOOT_ORACLE_DIRECT_INVENTORY

Problem: depleted resource nodes were routed through pooled loot prefabs, rigidbody impulse, delayed despawn, and pickup interaction before inventory truth changed.

Why owner-local data is insufficient: depletion, inventory acquisition, save tombstone, HUD notice, and VFX fake are separate owners. One local `ResourceNode` field cannot own all facts.

Why direct caller/owner interface is insufficient: direct inventory mutation would bypass inventory owner rules and save rollback visibility.

Instrument:
- SignalBus<T> first-party broadcast: `ItemAcquiredSignal`, `VisualScavengeSignal`, `ResourceDepletionDeltaSignal`, `HUDNotificationSignal`
- GlobalDataVault / IDataVault: flat loot tables, request/yield staging, biome modifiers, telemetry, CSV scratch
- Black-box/telemetry route: `ScavengingTelemetryEntry[300]`
- Item acquisition source-kind: `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` = 13 for scavenging oracle; source-kind 9 remains manual pickup.

Producer phase: `ResourceNode` queues request, `ScavengingLootOracleRuntime` resolves/publishes at Core late-frame. The publish completion is an explicit `[BLOCKING_SYNC_POINT]` signal flush fence because `SignalBus<T>.ParallelWriter` has no producer-handle registration route.
Consumer phase: inventory drains `ItemAcquiredSignal`; VFX/UI consume `VisualScavengeSignal`; save archivist consumes depletion deltas; HUD consumes notification.
Cadence: dirty only, resource depletion or incremental oracle request.
Expected max events/reads per frame: 64 queued requests, 512 visual signals high-tier, 64 visual signals low-tier.
GlobalQualityWeight behavior: loot math is invariant; VFX scalar is `math.lerp(0.1f, 1.0f, GlobalQualityWeight)`.
Editor tuning behavior: `Procedural Loot Tuner` writes continuous biome/tool/rare sliders into active Vault CDF rows and biome modifier rows; no player hot-path managed tuning table is introduced.

Payload/data shape: unmanaged explicit DTOs and signals.
Managed fields present: no.
UnityEngine.Object fields present: no.
Layout proof: `LootTableEntryDTO` is explicit 16 bytes; validation asserts offsets 0/4/8/12.
Capacity: entries 256, requests/yields 64, biome modifiers 128, telemetry 300, audit 32, CSV scratch 64KB.
Active-count law: `_activeLootEntryCount` and `_activeBiomeModifierCount` are the only valid read extents for Vault buffers allocated as `UninitializedMemory`; full-capacity scans are rejected.
Overflow/failure mode: queue refusal returns false and leaves node intact; inventory-full request emits HUD notification and leaves node intact.

Telemetry fields: root AUP, resource hash, selected item hash, ore hash, frame, total weight, roll, flags, estimated us, table hash, quality.
Black-box fields: same as telemetry; dump path `Docs/AgentLogs/Dump_LOOT_ORACLE.bin`.
Profiler marker: pending.
GC proof required: Unity Profiler/GC allocation check pending.

Shutdown/disposal rule: Vault owns buffers; SignalBus owns native queues; scene-local host unregisters late-frame tick on disable.
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

Why this does not increase global monolith risk: no `GlobalRegistry` live-state polling is added; GlobalDataVault is used only for flat unmanaged domain buffers; facts leave via typed lanes.

H-Phi impact expected: lowers direct ResourceNode -> ObjectPool/PhysX/inventory entanglement. Root assembly remains existing reality; no new sibling asmdef dependency was added.

Runtime proof required before acceptance: Unity import/compile, Play Mode depletion, Profiler GC 0 B on depletion, SignalBus snapshot verification, 10k audit counts recorded.
Current compile note: CLI build now includes `ScavengingLootOracle.cs`; SHINOBU AUP namespace error was fixed, but retry is gated by CPU/compiler policy and external Visor/Somatic/Equipment contract failures.
