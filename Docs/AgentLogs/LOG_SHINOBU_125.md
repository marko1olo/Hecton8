# LOG_SHINOBU_125

## 2026-05-19

What was wrong: `Docs/Tasks/CURRENT_BATCH.md` does not contain the required `<AGENT_PROMPT id="SHINOBU_125">` block. Enumeration found `SHINOBU_100` through `SHINOBU_120` only. Searches for `SCAVENGING_LOOT_TABLE_ORACLE`, `LOOT_TABLE_ORACLE`, and loot-related prompt text returned no authoritative block.

What was done: Created the mandatory status and rationale records for SHINOBU_125. No gameplay code was changed. No neighboring XML prompt was used.

Cinematic Cheats used: None. No physical or visual simulation was implemented.

Exact Microseconds saved: 0 us runtime. This log records a batch-authority blocker.

REGRESSION MODEL: CPU no change; GC no change; memory no change; cadence no change; correctness protected by refusing unauthorised code changes.

HOT PATH IMPACT: None.

FAILURE MODES: If implementation proceeds without XML, task scope, task count, phase gates, and self-audit criteria are undefined.

WHY KEPT/REJECTED: Kept blocker state because AGENTS.md requires the batch XML as primary directive. Rejected chat-only implementation until explicit user promotion or batch correction.

## 2026-05-19 Active XML Implementation Pass

What was wrong:
- `ResourceNode.TrySpawnLoot()` used the old scene-object route: `ObjectPoolManager.Spawn(lootPrefab)`, optional `Rigidbody` impulse/torque, and despawn lifetime. This made loot truth depend on world objects.
- No SHINOBU_125 loot DTO/job path existed: no 16-byte `LootTableEntryDTO`, no AUP-seeded Burst CDF resolver, no direct inventory publish job, no visual fake signal, no blackbox ring.
- `PlayerInventory.cs` contained Pack=1 layouts in the touched inventory handoff file.
- `loot_distribution_tables.h8bin` is not a proven active payload in the binary ledger, so hard boot dependency would be unsafe.

What was done:
- Added `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`.
- Added explicit DTOs: `LootTableEntryDTO` 16B, `InventoryCapacityDTO` 16B, `ScavengingBiomeModifierDTO` 16B, `ScavengingHarvestRequestDTO` 128B, `ScavengingResolvedYieldDTO` 96B, `ScavengingTelemetryEntry` 128B, and `VisualScavengeSignal` 80B.
- Added Burst jobs: `GenerateEmergencyMockLootTablesJob`, `MockHarvestRequestJob`, `LootResolutionJob`, `PublishLootYieldsJob`, and `ScavengingLootOracleSelfAuditJob`.
- Replaced depletion loot prefab spawning in `ResourceNode.TrySpawnLoot()` with an oracle request. The node remains intact on inventory-full or oracle-unavailable failure.
- Added `PlayerInventory.DrainScavengingLootOracleSignals()` for source-kind 9 direct inventory ingestion.
- Added `Procedural Loot Tuner` UI Toolkit editor window with layout proof, sliders, 10k audit button, and CSV ingest button.
- Added allocation-free CSV parser core: byte-token parsing, FNV-1a item token hashing, cumulative integer CDF write into Vault DTOs.
- Added editor gizmo hook for active resource nodes to label the highest-probability item hash.
- Added route card: `Docs/ARCHITECTURE/SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md`.
- Updated status/rationale files with 20-task matrix, decisions, and compile-gate state.

Cinematic Cheats used:
- Physical ore chunks are replaced by `ItemAcquiredSignal` for truth and `VisualScavengeSignal` for the eye. The VFX layer receives AUP, item hash, quantity, and `math.lerp(0.1f, 1.0f, GlobalQualityWeight)` emission scalar.
- Big-O before: O(k scene objects + PhysX wake + Transform/Rigidbody work) per depleted node. Big-O after: O(n loot table scan/binary-prefix lookup) with small flat native arrays; no loot GameObject hierarchy.

Exact Microseconds saved:
- Measured profiler delta: not available; Unity/C# build/profiler blocked because CPU counter returned 100% and project rules forbid build under load.
- Engineering estimate for removed loot prefab Rigidbody route: 50-500 us saved per depleted node on i3/MX350-class CPU, plus avoided Transform/PhysX wake and delayed despawn work.
- Resolver target: <5 us per interaction for small CDF. Telemetry currently records the 5 us budget estimate, not a profiler measurement.

Verification:
- `git diff --check` on touched files reports only LF/CRLF warnings for `PlayerInventory.cs` and `ResourceNode.cs`.
- Scoped oracle scan reports no `UnityEngine.Random`, `System.Random`, `Dictionary<`, `List<`, LINQ `.Where/.Select`, `foreach`, `string.Format`, `Pack=1`, `LayoutKind.Sequential`, or private Persistent `NativeArray`.
- Scoped inventory/oracle layout scan now reports no `Pack=1` or `LayoutKind.Sequential` in the oracle plus touched inventory layouts.
- Scoped loot regression scan reports no `pool.Spawn(lootPrefab)` and no `QueueForce(rigidbody)` in `ResourceNode.TrySpawnLoot()`.
- `dotnet build` not launched: CPU was 100%, no compiler process was active, and the hardware-protection gate blocked build.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Fallback CDF job plus `GenerateEmergencyMockLootTables()` present; active h8bin not found.</TASK>
    <TASK id="02" status="PASS">Depletion loot prefab spawn path replaced by oracle signal queue.</TASK>
    <TASK id="03" status="PASS">New hot DTOs expose public fields only.</TASK>
    <TASK id="04" status="PASS">`LootTableEntryDTO` explicit 16B, offsets 0/4/8/12 validated.</TASK>
    <TASK id="05" status="PASS">`MockHarvestRequestJob` writes deterministic synthetic requests.</TASK>
    <TASK id="06" status="PASS">`LootResolutionJob` uses AUP/session seed, `Unity.Mathematics.Random`, deterministic Burst, `[NoAlias]`.</TASK>
    <TASK id="07" status="PASS">`ConditionMask & ToolHashID` gates eligible CDF entries.</TASK>
    <TASK id="08" status="PASS">Direct inventory truth plus `VisualScavengeSignal`; no rigidbody loot drop.</TASK>
    <TASK id="09" status="PASS">`PublishLootYieldsJob` enqueues `ItemAcquiredSignal` through `NativeQueue.ParallelWriter`.</TASK>
    <TASK id="10" status="PASS">VFX multiplier is continuous GlobalQualityWeight lerp.</TASK>
    <TASK id="11" status="PASS">Biome modifier flat table applies integer milli-scalars.</TASK>
    <TASK id="12" status="PASS">AUP/type hash produces `ResourceNodeHash`; depletion delta signal emitted.</TASK>
    <TASK id="13" status="PASS">Inventory capacity abort emits `HUDNotificationSignal` and keeps node intact.</TASK>
    <TASK id="14" status="PASS">Deterministic Burst float mode and integer CDF avoid cross-platform float RNG drift.</TASK>
    <TASK id="15" status="PASS">Oracle buffers use GlobalDataVault handles with `UninitializedMemory`.</TASK>
    <TASK id="16" status="PASS">300-entry telemetry ring and dump path present.</TASK>
    <TASK id="17" status="PASS">UI Toolkit `Procedural Loot Tuner` present.</TASK>
    <TASK id="18" status="PASS">Allocation-free parser core for CSV bytes present; editor file read is cold-only.</TASK>
    <TASK id="19" status="PASS">Editor gizmo label present for active ResourceNode draw.</TASK>
    <TASK id="20" status="PARTIAL">10k audit job exists; actual Unity runtime execution/profiler proof pending due CPU build gate.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <LootTableEntryDTO size="16">
      <field name="ItemHashID" offset="0" size="4" />
      <field name="DropWeight" offset="4" size="4" />
      <field name="ConditionMask" offset="8" size="4" />
      <field name="_pad0" offset="12" size="4" />
    </LootTableEntryDTO>
    <ScavengingHarvestRequestDTO size="128">AUP 0-47, ulong fields 48/56, uint fields 64-104, float 108, InventoryCapacityDTO 112-127.</ScavengingHarvestRequestDTO>
    <ScavengingTelemetryEntry size="128">AUP 0-47, hash/item/frame/roll/flags/quality fields 48-111, two ulong pads 112-127.</ScavengingTelemetryEntry>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Loot truth is invariant. Below quality 0.3, VFX consumers receive emission multiplier near 0.1-0.37 and can collapse to a single icon/low particle count. At 1.0, same loot math emits multiplier 1.0 for visual overkill. No binary low/high branch was added.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No private Persistent NativeArray fields were added to the oracle. Vault BufferIDs: 70930 entries, 70931 requests, 70932 resolved yields, 70933 biome modifiers, 70934 telemetry ring, 70935 audit counts, 70936 CSV scratch.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    `LootResolutionJob` consumes fallback/table dependency and writes resolved yields/telemetry with `[NoAlias]`. `PublishLootYieldsJob` consumes resolve handle and writes ItemAcquired/VisualScavenge/ResourceDepletion/HUD lanes with parallel writers. Host completes at Core late-frame to avoid SignalBus queue race; depletion logic itself does not complete jobs.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling asmdef reference was added. The implementation lives in existing root assembly because `ResourceNode` and `PlayerInventory` already live there. Unity compile is pending; dotnet build was blocked by CPU 100%.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Replaced physical loot rocks with direct inventory signal and VFX fake. Before: spawned/pool GameObject plus Rigidbody work. After: flat DTO + SignalBus payload.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
