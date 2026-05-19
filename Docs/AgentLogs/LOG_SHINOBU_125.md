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

## 2026-05-19 Ultra-Think Polish Pass

What was wrong:
- `ItemAcquiredSignal.SourceKind = 9` collided with existing manual pickup source-kind 9. That could make `PlayerInventory.DrainScavengingLootOracleSignals()` add a manually picked item a second time.
- Runtime resource-node requests used local request order as `RollIndex`. Same AUP/type could roll differently when clients queued nodes in different local order.
- The oracle host used `DontDestroyOnLoad`, expanding lifetime beyond the scene route without explicit authorization.
- `ResourceNode` could still scan a loot prefab child hierarchy on depletion if the cache was cold.

What was done:
- Reserved source-kind 13 for scavenging oracle item acquisition and visual fake signals. Inventory now filters source-kind 13 without importing `Hecton8.Scavenging`.
- Runtime resource-node requests now set `RollIndex = 0u`; AUP, session/world seed, table version, and resource hash remain the deterministic seed inputs. Mock/audit jobs still vary roll index intentionally.
- Removed `DontDestroyOnLoad` from the oracle host and added scene-load cold bootstrap plus retry registration through `GlobalRegistry.TryRegisterLateFrameTickable`.
- Added cold payload metadata warm-up in `ResourceNode.Awake()` and `ApplyRuntimeTemplate()`. Depletion path calls `TryResolveLootOraclePayload(... allowHierarchyScan: false)`.

Cinematic Cheats used:
- Physical loot remains a data truth plus `VisualScavengeSignal`; no rigidbody ore is reintroduced.
- Prefab authoring metadata is read cold so the runtime illusion can trigger from flat hashes and AUP, not prefab traversal.

Exact Microseconds saved:
- Source-kind fix: correctness fix, 0 us intended CPU delta.
- Stable roll index: correctness fix, 0 us intended CPU delta.
- Cold payload warm-up: removes O(prefab child count) metadata search from valid depletion paths. Estimated 5-100 us avoided on nested prefabs; profiler proof pending.
- Scene-local host: lifetime safety, no frame-time claim.

Verification:
- `rg` source-kind scan: source-kind 13 appears only in `ScavengingLootOracle.cs` and `PlayerInventory.cs`; manual pickup remains 9, loot magnet 8, voxel carve 12, fabricator 4.
- `rg` forbidden hot-path scan on touched files: no `UnityEngine.Random`, `Random.Range`, `System.Random`, `Dictionary<`, `List<`, LINQ `.Where/.Select`, `foreach`, or `string.Format`.
- `rg` layout scan: no `Pack=1` or `LayoutKind.Sequential` in touched scavenging/inventory/resource-node files.
- `git diff --check` on touched files: only LF/CRLF warnings.
- `dotnet build` not launched: CPU counter returned 100%; project rule forbids build under >50% CPU.

<SELF_AUDIT_DELTA>
  <SOURCE_KIND status="PASS">Scavenging oracle source-kind moved from 9 to 13. Manual pickup source-kind 9 is no longer consumed by the scavenging drain.</SOURCE_KIND>
  <DETERMINISTIC_SEED status="PASS">Runtime node `RollIndex` is stable at 0; local queue order no longer affects same-node loot.</DETERMINISTIC_SEED>
  <HOST_LIFETIME status="PASS">No `DontDestroyOnLoad` call remains in the oracle host.</HOST_LIFETIME>
  <HOT_PATH_PREFAB_SCAN status="PASS">Hierarchy scans are behind `allowHierarchyScan: true` during cold warm-up only; depletion passes false.</HOT_PATH_PREFAB_SCAN>
  <COMPILE_PROOF status="PENDING">Unity/C# compile still blocked by CPU 100% gate.</COMPILE_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Ultra-Think Polish Pass 2

What was wrong:
- Source-kind `13` still existed as a duplicated memory fact: the oracle owned the producer value and inventory mirrored it locally.
- The job `.Complete()` sites were not labeled in source, so a reviewer had to infer which calls were cold/editor sync and which one was the deliberate signal-flush fence.

What was done:
- Added `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` in the signal contract namespace and routed both `ScavengingLootOracleConstants` and `PlayerInventory` through it.
- Kept inventory free of a `Hecton8.Scavenging` using/reference.
- Annotated fallback/audit completions as `COLD SYNC JOB`.
- Annotated late-frame publish completion as `[BLOCKING_SYNC_POINT]` with the exact reason: `SignalBus<T>.ParallelWriter` has no producer-handle registration route.

Cinematic Cheats used:
- No physical loot route was reintroduced. The architecture remains direct item truth plus `VisualScavengeSignal` eye-candy.

Exact Microseconds saved:
- Source-kind ownership: 0 us runtime, correctness and drift prevention only.
- Completion annotations: 0 us runtime, architecture review guard only.

Verification:
- Scoped source-kind scan now shows the numeric source-kind owner only in `ItemAcquiredSignalSourceKinds.ScavengingLootOracle`; inventory consumes the contract symbol.
- Scoped `using Hecton8.Scavenging` scan for `PlayerInventory.cs` remains empty.
- Forbidden hot-path scan over touched source returned no `UnityEngine.Random`, `Random.Range`, `System.Random`, `Dictionary<`, `List<`, LINQ, `string.Format`, or `foreach`.
- Layout/lifetime scan over touched source returned no `Pack=1`, `LayoutKind.Sequential`, `DontDestroyOnLoad`, `_requestSequence`, or request-order roll index.
- `git diff --check` on touched SHINOBU_125 files returned exit 0 with LF/CRLF warnings only.
- Build was not launched during this log update; latest CPU sample was 89.4% and `dotnet` PID 16624 was active.

<SELF_AUDIT_DELTA>
  <SOURCE_KIND_OWNER status="PASS">One fact: `ItemAcquiredSignalSourceKinds.ScavengingLootOracle`; one route: `ItemAcquiredSignal.SourceKind`; consumers compare against the contract symbol.</SOURCE_KIND_OWNER>
  <COMPILE_WALL status="PASS">No new sibling runtime dependency was added. Inventory references only `Hecton8.Core.Contracts.Signals`, already present.</COMPILE_WALL>
  <JOB_FENCE status="PASS">The publish completion is explicitly marked as the late-frame signal flush fence, not an arbitrary mid-frame readback.</JOB_FENCE>
  <COMPILE_PROOF status="PENDING">Unity/C# compile remains pending until CPU is below the project threshold and no dotnet/csc process is active.</COMPILE_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Ultra-Think Polish Pass 3

What was wrong:
- `ResourceNode` still owned a private `NativeArray<int>(1, Allocator.Persistent)` depletion lock in the loot producer path.
- `ScavengingLootOracleSelfAuditJob` used full loot-buffer capacity as the audit entry count even though the buffer is allocated with `UninitializedMemory`.
- `LootResolutionJob` scanned full biome modifier capacity even when no modifier rows had been written.

What was done:
- Replaced the native depletion lock with owner-local `int _depletionLockState` and `Interlocked` operations.
- Added `_activeLootEntryCount` and `_activeBiomeModifierCount` to the oracle host.
- CSV ingest sets active loot row count; emergency fallback sets active loot rows to four.
- Self-audit and biome modifier jobs now receive explicit counts and never read unwritten Vault tails.

Cinematic Cheats used:
- The physical loot fake remains unchanged: truth goes straight to inventory, visual noise is a separate `VisualScavengeSignal`.
- No new simulation was added to justify the cleanup.

Exact Microseconds saved:
- ResourceNode lock: removes one persistent native allocation and sentinel registration per node; hot path remains one atomic op.
- Biome no-modifier path: avoids up to 128 modifier DTO reads per loot resolution, estimated 1-4 us saved on weak CPUs.
- Audit path: avoids up to 252 stale loot DTO reads per 10k audit run; editor/cold path only.

Verification:
- `rg` over `ResourceNode.cs` now shows no `NativeArray`, `NativeArrayUnsafeUtility`, `NativeMemorySentinel`, `Allocator.Persistent`, or `Unity.Collections`.
- Oracle active-count scan shows `BiomeModifierCount`, `EntryCount`, `_activeLootEntryCount`, and `_activeBiomeModifierCount` wired into jobs.
- Forbidden hot-path API scan remains clean for `UnityEngine.Random`, `Random.Range`, `System.Random`, managed dictionaries/lists, LINQ, `foreach`, and `string.Format`.
- `git diff --check` on touched SHINOBU_125 files returned exit 0 with LF/CRLF warnings only.
- Build was not launched during this polish pass; compile-gate result is recorded below.

<SELF_AUDIT_DELTA>
  <RESOURCE_NODE_NATIVE_LOCK status="PASS">Per-node persistent native depletion lock removed; local scalar interlocked guard remains.</RESOURCE_NODE_NATIVE_LOCK>
  <UNINITIALIZED_VAULT_READS status="PASS">Loot audit and biome scans are count-gated; full-capacity reads over unwritten Vault memory are no longer allowed.</UNINITIALIZED_VAULT_READS>
  <COMPILE_PROOF status="PENDING">Compile-gate result is recorded below.</COMPILE_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Compile Gate Attempt

What was wrong:
- The earlier compile gate was blocked by CPU/compiler-process rules. A later sample opened the gate: CPU 41.9%, no `dotnet`/`csc`.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore --nologo`.
- The build failed in unrelated domains: `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` missing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, and `UberNoirReconstructionVaultIds`; `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs` missing `VrComfortProfileDTO` and `ComfortTelemetryEntry`.
- The reported error set emitted no errors from `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`, `Assets/_Project/Scripts/ResourceNode.cs`, or `Assets/_Project/Scripts/PlayerInventory.cs`.

Cinematic Cheats used:
- None. This was compile verification only.

Exact Microseconds saved:
- No runtime claim. Stopping after the external wall avoids repeated build loops.

<SELF_AUDIT_DELTA>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">Targeted Core build is blocked by Visor/Somatic missing-contract errors owned outside SHINOBU_125.</COMPILE_GATE>
  <LOCAL_ERROR_SET status="PASS_STATIC">No SHINOBU_125 touched source file appears in the emitted compiler errors.</LOCAL_ERROR_SET>
</SELF_AUDIT_DELTA>
## 2026-05-19 - Compile Surface And Editor Tuning Pass

What was wrong: `ScavengingLootOracle.cs` had no Unity `.meta` and was absent from the current `Hecton8.Core.csproj`, so the CLI build did not actually prove the new oracle file. The editor facade also had sliders that did not mutate Vault truth.

What was done: Added `ScavengingLootOracle.cs.meta`; included the oracle in the current CLI compile surface; added `TryApplyEditorTuning()` and an `Apply Vault Tuning` UI button that writes preview CDF rows and biome modifier DTOs into existing Vault buffers.

Cinematic Cheats used: Physical ore remains replaced by direct `ItemAcquiredSignal` plus `VisualScavengeSignal`; slider changes affect deterministic math and leave VFX richness to the fake particle/icon consumer.

Exact microseconds saved: Runtime unchanged from the previous pass. The editor tuning path is cold/manual; hot-path modifier scanning remains count-gated, avoiding up to 128-row uninitialized scans when no modifiers are active.

Verification: Static forbidden API scan clean except editor-only `NativeArray<byte>(Allocator.Temp)` CSV bridge. `git diff --check` passed with existing LF/CRLF warnings. Targeted build now reaches SHINOBU file; local AUP namespace error was fixed by importing `Hecton8.World`. The editor gizmo now cold-generates the preview table and uses `_activeLootEntryCount`, avoiding uninitialized Vault reads. Latest retry gate sample has CPU 35.9% but active external `dotnet` PIDs 25032/29032/35364/38596/40748/55468/57416, so another build is forbidden; external Visor/Somatic/Equipment compile walls remain outside this domain.

<SELF_AUDIT agent_id="SHINOBU_125" domain="SCAVENGING_LOOT_TABLE_ORACLE" date="2026-05-19">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archive/StreamingAssets fallback path implemented; emergency flat CDF generated in unmanaged Vault storage.</TASK>
    <TASK id="02" status="PASS">ResourceNode depletion no longer instantiates physical loot; truth is queued into oracle payloads.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use explicit public fields; no getter/setter DTO properties in SHINOBU path.</TASK>
    <TASK id="04" status="PASS">LootTableEntryDTO is explicit 16 bytes; runtime/editor validation checks offsets 0/4/8/12.</TASK>
    <TASK id="05" status="PASS">MockHarvestRequestJob injects deterministic AUP/tool test requests.</TASK>
    <TASK id="06" status="PASS">LootResolutionJob uses AUP/session/resource seed and Unity.Mathematics.Random with deterministic Burst mode.</TASK>
    <TASK id="07" status="PASS">ConditionMask/tool gating uses bitwise checks over flat entries.</TASK>
    <TASK id="08" status="PASS">Dear Lie route sends ItemAcquiredSignal plus VisualScavengeSignal, no ore rigidbody truth.</TASK>
    <TASK id="09" status="PASS">PublishLootYieldsJob writes item signals through NativeQueue.ParallelWriter; inventory owns consumption.</TASK>
    <TASK id="10" status="PASS">VFX emission multiplier is math.lerp(0.1, 1.0, GlobalQualityWeight).</TASK>
    <TASK id="11" status="PASS">Biome modifier Vault rows apply milli-scalars and are active-count gated.</TASK>
    <TASK id="12" status="PASS">ResourceNodeHash derives from AUP/type hash and publishes depletion delta signal.</TASK>
    <TASK id="13" status="PASS">Inventory-full path aborts RNG and emits HUD notification; node remains intact.</TASK>
    <TASK id="14" status="PASS">Rollback fence uses deterministic Burst float mode, integer CDF, and stable resource-node RollIndex = 0.</TASK>
    <TASK id="15" status="PASS">Vault buffers use UninitializedMemory; reads are gated by active counts.</TASK>
    <TASK id="16" status="PASS">300-entry telemetry ring and binary dump path exist for scavenging blackbox.</TASK>
    <TASK id="17" status="PASS">Editor tuner sliders now write unmanaged loot and biome modifier rows through Apply Vault Tuning.</TASK>
    <TASK id="18" status="PASS">CSV byte parser hashes tokens and writes CDF entries without managed string splitting in parser core.</TASK>
    <TASK id="19" status="PASS">Editor gizmo labels active-table probability and no longer reads unwritten Vault rows.</TASK>
    <TASK id="20" status="PENDING_COMPILE">Self-audit job exists and static scans pass; full C# compile is blocked by external contract errors and current CPU gate.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="LootTableEntryDTO" size="16" alignment="16">
      <FIELD name="ItemHashID" offset="0" size="4"/>
      <FIELD name="DropWeight" offset="4" size="4"/>
      <FIELD name="ConditionMask" offset="8" size="4"/>
      <FIELD name="_pad0" offset="12" size="4"/>
      <MATH>4+4+4+4=16 bytes; exact 16-byte stride.</MATH>
    </DTO>
    <DTO name="ScavengingResolvedYieldDTO" size="96" alignment="16">
      <FIELD name="NodeAup" offset="0" size="48"/>
      <FIELD name="ResourceNodeHash" offset="48" size="8"/>
      <FIELD name="ItemHashID/OreHash/Quantity/Frame" offset="56" size="16"/>
      <FIELD name="VfxEmissionMultiplier/Roll/TotalWeight" offset="72" size="12"/>
      <FIELD name="DepletionWordIndex/SourceKind/Flags" offset="84" size="4"/>
      <FIELD name="TableHash/RequestId" offset="88" size="8"/>
      <MATH>48+8+16+12+4+8=96 bytes; exact 16-byte multiple.</MATH>
    </DTO>
    <DTO name="ScavengingTelemetryEntry" size="128" alignment="64">
      <FIELD name="NodeAup" offset="0" size="48"/>
      <FIELD name="ResourceNodeHash" offset="48" size="8"/>
      <FIELD name="core scalar block" offset="56" size="48"/>
      <FIELD name="DepletionMask" offset="104" size="8"/>
      <FIELD name="_pad0/_pad1" offset="112" size="16"/>
      <MATH>128 bytes; two 64-byte cache lines, no Pack=1.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Loot truth does not degrade with hardware. Visual cost does: VisualScavengeSignal.VfxEmissionMultiplier uses math.lerp(0.1, 1.0, GlobalQualityWeight). Editor tuning uses continuous clamped floats and a smooth rare polynomial; no low/high binary switch. At weight below 0.3, VFX consumers receive a 0.1-0.37 emission scalar while inventory truth remains identical.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_array_allocations="0">
    <BUFFER id="70930" name="LootEntries" type="LootTableEntryDTO"/>
    <BUFFER id="70931" name="HarvestRequests" type="ScavengingHarvestRequestDTO"/>
    <BUFFER id="70932" name="ResolvedYields" type="ScavengingResolvedYieldDTO"/>
    <BUFFER id="70933" name="BiomeModifiers" type="ScavengingBiomeModifierDTO"/>
    <BUFFER id="70934" name="TelemetryRing" type="ScavengingTelemetryEntry"/>
    <BUFFER id="70935" name="DistributionAudit" type="uint"/>
    <BUFFER id="70936" name="CsvScratch" type="byte"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS status="PASS">Burst jobs mark disjoint NativeArray fields with [NoAlias].</NO_ALIAS>
    <JOB name="GenerateEmergencyMockLootTablesJob" consumes="dependency" outputs="fallback table handle"/>
    <JOB name="LootResolutionJob" consumes="fallback/table dependency" outputs="resolveHandle"/>
    <JOB name="PublishLootYieldsJob" consumes="resolveHandle" outputs="publishHandle completed at late-frame signal fence"/>
    <JOB name="ScavengingLootOracleSelfAuditJob" consumes="fallback/table dependency" outputs="editor audit buffer"/>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD status="PENDING">
    No sibling runtime assembly reference was added. Hecton8.Core.csproj compile surface now includes ScavengingLootOracle.cs. Latest build reached SHINOBU code, AUP namespace was corrected, and retry is blocked by CPU/compiler gate plus external Visor/Somatic/Equipment contract failures.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Physical ore chunk instantiate/pool/rigidbody pickup was replaced by direct inventory signal plus visual-only scavenge signal. Before: O(N GameObjects + PhysX wake/despawn) per depleted node. After: O(requests + flat CDF scan) CPU truth with VFX-only fake particles/icons.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
