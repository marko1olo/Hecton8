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

## 2026-05-19 - GlobalQualityWeight Finite Fail-Closed Fence

What was wrong:
- SHINOBU quality ingress did not have a single finite-input owner.
- A non-finite quality value could be saturated inconsistently or default a resolver path toward ultra presentation instead of minimum survival.

What was done:
- Added `ScavengingLootOracleMath.SanitizeQualityWeight(float)` as the owner-local quality fence.
- Routed mock requests, ResourceNode queued requests, resolver VFX scalar, telemetry quality, and compute-debris particle count through the same sanitizer.
- Verified with `rg` that the old raw `math.saturate(GlobalQualityWeight)` / default-to-ultra request quality patterns no longer match in SHINOBU oracle/resource-node files.

Cinematic cheats used:
- The Dear Lie route remains direct item truth plus visual/compute debris. Bad quality input now collapses only presentation cost to minimum survival; it does not change loot truth.

Exact microseconds saved:
- 0 us claimed. This is NaN/fault containment with one scalar finite clamp at existing quality ingress points.

<SELF_AUDIT_DELTA>
  <QUALITY_FAULT_MODE status="PASS_STATIC">Non-finite GlobalQualityWeight fails closed to 0.0 before visual scalar, telemetry, queued requests, mock requests, and impact-debris math.</QUALITY_FAULT_MODE>
  <SCALABILITY status="PASS_STATIC">Valid quality remains continuous 0..1; no binary hardware tier switch was introduced.</SCALABILITY>
  <ZERO_GC status="PASS_STATIC">No managed allocation, no native allocation, no new Vault buffer, and no new SignalBus lane.</ZERO_GC>
</SELF_AUDIT_DELTA>

Build gate recheck:
- Active `dotnet` process detected.
- CPU sampled at 100%, above the project build threshold.
- `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` remains absent while referenced by `Hecton8.Core.csproj`.
- `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs` remain absent while referenced by `Assembly-CSharp.csproj`.
- No build launched.

## 2026-05-19 - Bottom Ledger Correction After Compile Recheck

What was wrong:
- The newest forced-result, source-kind, self-audit parity, and compile-gate evidence had been inserted above older log sections. That violates the local reporting rule: Top=Old, Bottom=New.

What was done:
- Appended this bottom ledger entry without deleting old history.
- Reconfirmed current SHINOBU_125 state from `Status_SHINOBU_125.md` and `Rationale_SHINOBU_125.md`.
- The latest implementation facts remain: resource-node depletion passes `ForcedItemHashID = 0`, biome scalar is owned by modifier rows only, self-audit consumes active modifiers/tool masks, CSV CDF accumulation saturates, and source-kind 13 is owned by `Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs`.
- The latest compile proof remains blocked by external dependency walls after one guarded `dotnet build Hecton8.Core.csproj --no-restore --nologo`: 82 external errors / 1 duplicate-source warning; no visible error references SHINOBU_125 touched source files.

Cinematic Cheats used:
- No new simulation was introduced. Runtime truth still routes directly into inventory through `ItemAcquiredSignal`; eye-candy remains a fake `VisualScavengeSignal` budgeted by `GlobalQualityWeight`.

Exact Microseconds saved:
- Log correction: 0 us runtime.
- Forced-result bypass: restores deterministic weighted table truth at no added CPU cost.
- No-physics loot path remains the real saving: avoids pooled ore GameObject/PhysX wake work, estimated 50-500 us per depleted node and larger spikes on weak CPUs.

<SELF_AUDIT_DELTA>
  <LOG_ORDER status="PASS">Newest evidence is now appended at the bottom of `LOG_SHINOBU_125.md`.</LOG_ORDER>
  <FORCED_RESULT_BYPASS status="PASS">Real node depletion enters CDF selection; prefab hash remains seed/context, not forced output.</FORCED_RESULT_BYPASS>
  <CONTRACT_SOURCE_KIND status="PASS">Scavenging source-kind 13 has one owner in Core contracts.</CONTRACT_SOURCE_KIND>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">Latest guarded build is blocked by non-SHINOBU missing contracts; no foreign-domain edits were made.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Forced Result And Contract Isolation Polish

What was wrong:
- Runtime `ResourceNode` requests passed the prefab item as `ForcedItemHashID`, bypassing the CDF/RNG table for real rocks.
- Editor tuning applied biome scalar twice: once into CDF rows and again through biome modifier rows.
- Self-audit sampled raw CDF rows, not the modified runtime weights.
- Source-kind 13 was declared inside the runtime oracle file instead of a narrow contracts surface.

What was done:
- `ResourceNode.TrySpawnLoot()` now sends `ForcedItemHashID = 0u`; prefab item hash remains ore/type seed context.
- `TryApplyEditorTuning()` now writes tool/rare into CDF rows and biome only into `ScavengingBiomeModifierDTO` rows.
- `ScavengingLootOracleSelfAuditJob` now consumes active biome modifiers, active row counts, and all tool masks before 10k simulated rolls.
- Added `Assets/_Project/Scripts/Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs` plus `.meta`; removed the source-kind declaration from `ScavengingLootOracle.cs`.
- CSV parser now rejects overflowing unsigned tokens and saturates CDF addition.

Cinematic Cheats used:
- Physical loot remains a Dear Lie: inventory truth arrives by `ItemAcquiredSignal`, visual belief arrives by `VisualScavengeSignal`.
- No Rigidbody, MeshCollider, or pooled ore prefab was reintroduced.

Exact Microseconds saved:
- Forced-result patch is correctness, not CPU saving; it restores the intended <5 us Burst CDF roll.
- Contract isolation has 0 runtime cost and prevents future asmdef compile-wall coupling.
- CSV overflow guard is cold/editor path only.

Verification:
- `rg` confirms `ItemAcquiredSignalSourceKinds` exists in the Core contracts signal file and is consumed by both `PlayerInventory` and the oracle.
- Scoped forbidden hot-path scan over `ScavengingLootOracle.cs` and `ResourceNode.cs` remains clean for `UnityEngine.Random`, `System.Random`, dictionaries/lists, LINQ, `string.Format`, `Pack=1`, private persistent native arrays, and `DontDestroyOnLoad`.
- `git diff --check` on touched SHINOBU source/docs returns exit 0 with LF/CRLF warnings only.
- Build recheck result is recorded in the next section.

<SELF_AUDIT_DELTA>
  <FORCED_RESULT_BYPASS status="PASS">Real resource-node depletion now enters deterministic CDF selection instead of constant forced prefab output.</FORCED_RESULT_BYPASS>
  <BIOME_SINGLE_OWNER status="PASS">Biome scalar is owned by biome modifier rows; CDF rows own base/tool/rare only.</BIOME_SINGLE_OWNER>
  <SELF_AUDIT_PARITY status="PASS">Editor audit uses active runtime modifiers and tool masks.</SELF_AUDIT_PARITY>
  <CONTRACT_SOURCE_KIND status="PASS">Scavenging source-kind 13 lives in Core contracts signal file, not the runtime oracle file.</CONTRACT_SOURCE_KIND>
  <COMPILE_PROOF status="PENDING">No build launched under CPU gate.</COMPILE_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Compile Gate Recheck After Polish

What was wrong:
- The forced-result/source-kind/self-audit changes touched compile surface and required one guarded compile check.

What was done:
- Waited for CPU/compiler gate: CPU 34.2%, no active `dotnet/csc`.
- Ran `dotnet build Hecton8.Core.csproj --no-restore --nologo`.
- Build failed with 82 external errors and 1 duplicate-source warning.

Cinematic Cheats used:
- None. This was verification only.

Exact Microseconds saved:
- No runtime claim. The useful result is ownership isolation: SHINOBU_125 does not edit foreign domains to hide their missing contracts.

Visible external compile walls:
- `PlayerSwimPresentationController.cs`: missing `Hecton8.Animation.KineticCharacter` / `KineticCharacterAnimatorRuntime`.
- `TerminalOsRuntime.cs`: missing `TerminalVirtualButtonDTO`.
- `HectonVisorUberPostFeature.cs` and `DeferredDecalPass.cs`: missing Uber Noir reconstruction and decal DTO/contracts.
- `ModularEquipmentEngine.cs` and `GlobalRegistryContracts.cs`: missing equipment DTO/contracts.
- `PredatorCognitionDomain.cs`: missing Mesofauna DTO/constants.
- `SomaticTunerWindow.cs`: missing VR comfort DTO/telemetry.
- `EcosystemDirector.cs`: missing MacroEcosystem DTO/contracts.

SHINOBU_125 visibility:
- No visible compiler error references `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`.
- No visible compiler error references `Assets/_Project/Scripts/ResourceNode.cs`.
- No visible compiler error references `Assets/_Project/Scripts/PlayerInventory.cs`.
- No visible compiler error references `Assets/_Project/Scripts/Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs`.

<SELF_AUDIT_DELTA>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">Current Core build blocked by external KineticCharacter/TerminalOS/Visor/Equipment/Fauna/Somatic/Ecosystem contract walls.</COMPILE_GATE>
  <SHINOBU_VISIBLE_ERROR_SET status="PASS_STATIC">No SHINOBU_125 touched source appears in visible compiler errors.</SHINOBU_VISIBLE_ERROR_SET>
</SELF_AUDIT_DELTA>

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
    <TASK id="20" status="BLOCKED_BY_DEPENDENCY">Self-audit job exists and static scans pass; latest guarded C# compile is blocked by external non-SHINOBU contract errors. Runtime Unity/Profiler proof remains pending.</TASK>
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
  <COMPILE_GUARD status="BLOCKED_BY_DEPENDENCY">
    No sibling runtime assembly reference was added. Hecton8.Core.csproj compile surface now includes ScavengingLootOracle.cs, ItemAcquiredSignalSourceKinds.cs, and VisualScavengeSignal.cs. The latest guarded build ran before the visual-signal file split, reached SHINOBU code, corrected the AUP namespace issue, and stopped at external KineticCharacter/TerminalOS/Visor/Equipment/Fauna/Somatic/Ecosystem missing contracts. The visual-signal split is static-scan verified only until that external wall changes.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Physical ore chunk instantiate/pool/rigidbody pickup was replaced by direct inventory signal plus visual-only scavenge signal. Before: O(N GameObjects + PhysX wake/despawn) per depleted node. After: O(requests + flat CDF scan) CPU truth with VFX-only fake particles/icons.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - True EOF Ledger Correction

What was wrong:
- A previous bottom-ledger patch matched an older `</SELF_AUDIT>` marker and inserted above newer historical sections.
- The corrected verification showed that a broad forbidden scan including all of `PlayerInventory.cs` reports pre-existing inventory-owned private NativeArrays. Those are not SHINOBU_125 allocations and were not edited by this pass.

What was done:
- Appended this note after the final audit marker, making the newest evidence physically last in `LOG_SHINOBU_125.md`.
- Re-ran the forbidden scan on SHINOBU-owned source files only: `ScavengingLootOracle.cs`, `ResourceNode.cs`, and `Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs`. It returned no matches.
- Confirmed the source-kind route: `ItemAcquiredSignalSourceKinds.ScavengingLootOracle = 13`, consumed by both the oracle constants and inventory drain.

Cinematic Cheats used:
- No physical drop simulation was reintroduced. Inventory truth remains direct signal delivery; visual feedback remains a fake particle/icon signal with continuous `GlobalQualityWeight` scaling.

Exact Microseconds saved:
- EOF correction: 0 us runtime.
- Corrected scan scope: 0 us runtime, prevents false ownership claims over the inventory domain.
- Preserved loot cheat: avoids ore prefab spawn/PhysX wake/debris lifecycle, estimated 50-500 us per depleted node on typical scenes and higher tail spikes on weak CPUs.

<SELF_AUDIT_DELTA>
  <LOG_ORDER status="PASS">This entry is appended after the final audit marker and is now the newest ledger record.</LOG_ORDER>
  <FORBIDDEN_SCAN_SCOPE status="PASS">SHINOBU-owned source files returned no forbidden hot-path API/layout/allocation matches.</FORBIDDEN_SCAN_SCOPE>
  <INVENTORY_NATIVE_ARRAYS status="OUT_OF_SCOPE_PREEXISTING">Broad scan hits in `PlayerInventory.cs` are existing inventory-owned buffers, not SHINOBU_125 allocations.</INVENTORY_NATIVE_ARRAYS>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No additional build launched; latest guarded build remains blocked by external missing contracts outside SHINOBU_125.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Visual Signal Contract Isolation Pass

What was wrong:
- `VisualScavengeSignal` lived physically inside `ScavengingLootOracle.cs` while declaring the `Hecton8.Core.Contracts.Signals` namespace. That is a compile-wall trap: future VFX/UI consumers would be forced toward the scavenging runtime file for a pure signal payload.
- Status and route-card wording still implied a CPU-gated compile wait in places, while the latest real compile attempt is externally blocked by non-SHINOBU missing contracts.

What was done:
- Moved `VisualScavengeSignal` to `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs`.
- Preserved the explicit 80-byte layout: AUP at offset 0, resource hash 48, item/ore/quantity/frame 56/60/64/68, quality scalar 72, byte flags 76/77, ushort padding 78.
- Added `VisualScavengeSignal.cs.meta` and included the contract file in `Hecton8.Core.csproj`.
- Updated status, rationale, and route card to state static implementation plus runtime proof pending and external compile wall.

Cinematic Cheats used:
- Same Dear Lie path: no ore rigidbody, no physics pickup truth. Loot truth goes to `ItemAcquiredSignal`; player-facing feedback is the contract-owned `VisualScavengeSignal` with continuous `GlobalQualityWeight` emission scaling.

Exact Microseconds saved:
- Signal file split: 0 us runtime.
- Preserved no-physics loot path: estimated 50-500 us saved per depleted node versus pooled ore chunks and PhysX wake/despawn work.

<SELF_AUDIT_DELTA>
  <VISUAL_SIGNAL_CONTRACT status="PASS">`VisualScavengeSignal` is now physically owned by Core contracts, not the scavenging runtime source file.</VISUAL_SIGNAL_CONTRACT>
  <STRUCT_LAYOUT status="PASS">`VisualScavengeSignal` remains explicit 80 bytes; no Pack=1, no managed refs.</STRUCT_LAYOUT>
  <COMPILE_WALL status="PASS_STATIC">No sibling runtime dependency was added; current build wall remains external to SHINOBU_125.</COMPILE_WALL>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Prompt Extraction Regex Correction

What was wrong:
- The exact-tag extractor falsely reported `SHINOBU_125` missing because the active XML tag has additional attributes.

What was done:
- Re-extracted the prompt with an attribute-aware regex and updated `Status_SHINOBU_125.md` to name the exact active tag.

Cinematic Cheats used:
- None. This was task-authority hygiene.

Exact Microseconds saved:
- 0 us runtime. Prevents protocol drift and wrong-agent contamination under batch-file edits.

<SELF_AUDIT_DELTA>
  <PROMPT_EXTRACTION status="PASS">Current batch block is present at `Docs/Tasks/CURRENT_BATCH.md` and extracted by ID-aware attribute matching.</PROMPT_EXTRACTION>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Masked CDF Prefix Recompute Removal

What was wrong:
- Base CDF selection used a total scan plus a binary search that recomputed the eligible prefix from the table start at every midpoint. That preserved deterministic output but made sparse tool-mask tables O(n log n) in memory reads.

What was done:
- `LootResolutionJob` now proves whether the active table can use raw CDF binary search. Fully eligible monotonic tables still use binary search. Sparse tool-mask tables select with a second deterministic linear pass and no scratch allocation.

Cinematic cheats used:
- None; this is item-truth math. The existing physical-drop fake remains unchanged and still emits only inventory/visual signals.

Exact microseconds saved:
- Estimated 2-8 us per 256-row sparse-table harvest on i3/MX350-class silicon. Four-row emergency tables save effectively 0 us but keep the same deterministic output.

<SELF_AUDIT_DELTA>
  <CDF_SELECTION status="PASS">No repeated prefix recomputation remains in the base no-biome path; raw CDF binary search is used only when proved legal.</CDF_SELECTION>
  <ZERO_GC status="PASS">No scratch CDF array, managed collection, LINQ, string formatting, or Unity random API was introduced.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched; latest CPU gate read 100% and the previous compile wall remains external to SHINOBU_125.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Incremental Yield Physical Drop Purge

What was wrong:
- `ResourceNode` depletion loot used the oracle, but incremental mining yield still registered persistent dropped items and pushed an ad hoc source-kind 1 acquisition signal. That kept a physical dropped-item route alive in the resource-node mining loop.

What was done:
- Added suppress-depletion request/result flags to the oracle.
- Routed incremental yield as a forced oracle request with `emitDepletionDelta: false`.
- `PublishLootYieldsJob` now emits inventory and visual signals for those yields but skips `ResourceDepletionDeltaSignal`, preventing premature node tombstones.

Cinematic cheats used:
- The dropped item is no longer registered as world persistence. Player feedback is the existing visual-only scavenge signal.

Exact microseconds saved:
- Estimated 20-200 us per incremental emitted unit by removing persistent dropped-item registration, hydration checks, spawn impulse bookkeeping, and future world-item hydration pressure.

<SELF_AUDIT_DELTA>
  <INCREMENTAL_YIELD_ROUTE status="PASS_STATIC">`ResourceNode` incremental yield now enters the oracle signal path and no longer calls `TryRegisterDroppedItem` in the scoped resource-node source.</INCREMENTAL_YIELD_ROUTE>
  <DEPLETION_TRUTH status="PASS_STATIC">Suppress-depletion flags prevent incremental yield from emitting `ResourceDepletionDeltaSignal`; actual depletion still emits the save delta.</DEPLETION_TRUTH>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched; latest CPU gate read 100% and the previous compile wall remains external to SHINOBU_125.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Source Kind Contract Surface Correction

What was wrong:
- The proof text described `ItemAcquiredSignalSourceKinds` as if it only carried the scavenging source-kind. Static scan shows `HarvestableOutcrop` already consumes `ItemAcquiredSignalSourceKinds.HarvestableOutcrop`.

What was done:
- Kept `ScavengingLootOracle = 13` and `HarvestableOutcrop = 14` in the narrow Core contracts signal file.
- Updated route/status/rationale wording so the contract is documented as the shared owner for source-kind facts, not a scavenging-runtime artifact.

Cinematic cheats used:
- None. This is compile-wall hygiene.

Exact microseconds saved:
- 0 us runtime. Prevents a future missing-constant compile break without adding a runtime dependency.

<SELF_AUDIT_DELTA>
  <SOURCE_KIND_SURFACE status="PASS_STATIC">`ItemAcquiredSignalSourceKinds.cs` owns `ScavengingLootOracle = 13` and `HarvestableOutcrop = 14` in Core contracts.</SOURCE_KIND_SURFACE>
  <COMPILE_WALL status="PASS_STATIC">Runtime producers consume a narrow contract file instead of defining duplicate source-kind values.</COMPILE_WALL>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Repair Tool Titanium Side-Effect Fence

What was wrong:
- `PlayerInventory.DrainRepairToolTitaniumSignals()` interpreted every titanium `ItemAcquiredSignal` as a repair-tool durability trigger. The scavenging oracle now publishes mined titanium through `ItemAcquiredSignalSourceKinds.ScavengingLootOracle`, so the same signal could add inventory and also repair equipment without an explicit repair-cost route.

What was done:
- Added a source-kind exclusion in the repair-tool titanium drain.
- Scavenging source-kind 13 remains consumed by `DrainScavengingLootOracleSignals()` only for inventory insertion.
- No new signal lane, allocation, or public payload field was added.

Cinematic cheats used:
- No physical loot or repair simulation was introduced. The existing Dear Lie inventory signal remains item truth; repair state no longer piggybacks on that visual/inventory route.

Exact microseconds saved:
- 0 us claimed as performance. This is correctness isolation: one byte branch prevents hidden durability writes from scavenging broadcasts.

<SELF_AUDIT_DELTA>
  <SOURCE_KIND_SIDE_EFFECT status="PASS_STATIC">Scavenging source-kind 13 is excluded from the repair-tool titanium drain.</SOURCE_KIND_SIDE_EFFECT>
  <ZERO_GC status="PASS_STATIC">The change is one branch inside an existing indexed `ReadOnlySpan` scan; no managed allocation, LINQ, string formatting, or container allocation was added.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched; CPU gate remains closed at 100% and the previous compile wall remains external to SHINOBU_125.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Guarded Build Attempt Hit Deleted Foreign Includes

What was wrong:
- After the source-kind fence patch, compile proof was still pending. The CPU/compiler gate opened at CPU 19.8% with no active `dotnet/csc`, so the no-restore Core project build was allowed.
- The build did not reach SHINOBU source. `Hecton8.Core.csproj` references two files absent from disk: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore --nologo`.
- Verified both missing files return `Test-Path = False`.
- Verified `git status` reports both missing paths as deleted in the working tree.
- Did not recreate World/Construction files and did not edit the broad project file from the scavenging lane.

Cinematic cheats used:
- None. This was compile-gate evidence.

Exact microseconds saved:
- 0 us runtime. The build consumed about 4.3 seconds; the result is dependency evidence, not runtime proof.

<SELF_AUDIT_DELTA>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">Fresh guarded build failed at `CS2001` on deleted foreign source includes before SHINOBU semantic compilation.</COMPILE_GATE>
  <FOREIGN_FILES status="OUT_OF_SCOPE">`ChemicalInfluenceGrid.cs` is World/Ecosystem ownership; `LogisticsPipeEvents.cs` is Construction/Logistics ownership and was deleted by another lane.</FOREIGN_FILES>
  <SHINOBU_PROOF status="STATIC_ONLY">Scoped static scans remain the current evidence for SHINOBU-owned code after this blocker.</SHINOBU_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 - AUP Millimeter Hash Quantization

What was wrong:
- `BuildDeterministicSeed()` and `BuildResourceNodeHash()` folded raw local AUP float bits. That violates the AUP commit rule because persistence/telemetry hashes need millimeter-quantized local coordinates, not presentation-float bit identity.

What was done:
- Added `QuantizeLocalMillimetersForHash(float)`.
- RNG seed mixing and resource depletion hash mixing now use finite, clamped, millimeter-rounded AUP locals.
- Static scan confirms the oracle no longer uses raw `math.asuint(Local*)` in those hash paths.

Cinematic cheats used:
- None in gameplay truth. The Dear Lie remains the visual-only item-flight signal; this patch hardens deterministic authority beneath it.

Exact microseconds saved:
- 0 us claimed. This is desync prevention. Cost is three scalar clamp/round conversions per resolved request, still inside the <5 us oracle target.

<SELF_AUDIT_DELTA>
  <AUP_HASH status="PASS_STATIC">Local AUP hash inputs are quantized to millimeters before deterministic RNG and depletion hash mixing.</AUP_HASH>
  <ZERO_GC status="PASS_STATIC">No managed allocation, managed collection, string path, native allocation, or new signal lane was introduced.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched because the last guarded build is blocked before SHINOBU semantic compilation by deleted foreign source includes.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Loot Quantity Overflow Fence

What was wrong:
- `ResourceNode` converted authored `lootCount * unitQuantity` directly through `uint` multiplication. Extreme template values could wrap before inventory capacity preflight and before the oracle request was staged.

What was done:
- Added `MultiplyLootQuantitySaturated(...)`.
- Cached payload and fresh payload resolution paths now multiply in `ulong` and clamp to `uint.MaxValue`.
- Static scan confirms the old unchecked `lootCount` multiply pattern is absent.

Cinematic cheats used:
- None. This is item-truth payload hygiene. The visual route still uses the existing fake scavenge signal.

Exact microseconds saved:
- 0 us claimed. This prevents silent quantity corruption; cost is one scalar saturated multiply in the cold/resource-node payload bridge.

<SELF_AUDIT_DELTA>
  <QUANTITY_OVERFLOW status="PASS_STATIC">Loot prefab quantity is saturated before inventory-capacity preflight and oracle enqueue.</QUANTITY_OVERFLOW>
  <ZERO_GC status="PASS_STATIC">No allocation, no new collection, no SignalBus lane, and no Vault buffer were introduced.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched because the known `CS2001` foreign-source wall is unchanged.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Visual Signal AUP Contract Detachment

What was wrong:
- `VisualScavengeSignal.cs` lived in Core contracts but imported `Hecton8.World.AbsoluteUniversePosition`. `Hecton8.Core.Contracts.asmdef` does not reference World/Core runtime and must not grow that reverse dependency.

What was done:
- Added explicit 48-byte `VisualScavengeAup48` to the visual signal contract file.
- Changed `VisualScavengeSignal.PositionAup` to use the contract-local transfer DTO.
- Converted runtime `AbsoluteUniversePosition` to `VisualScavengeAup48` inside `PublishLootYieldsJob` before enqueue.

Cinematic cheats used:
- Same Dear Lie: item truth remains `ItemAcquiredSignal`; player belief remains visual-only `VisualScavengeSignal` scaled by `GlobalQualityWeight`. No physics ore route was reintroduced.

Exact microseconds saved:
- 0 us claimed. This is compile-wall hardening. Runtime cost is six scalar field copies in the existing Burst publish job; no allocation, no new SignalBus lane, and no Vault buffer.

<SELF_AUDIT_DELTA>
  <CONTRACT_ISOLATION status="PASS_STATIC">`VisualScavengeSignal.cs` no longer imports `Hecton8.World`; Core contracts own their signal payload shape locally.</CONTRACT_ISOLATION>
  <STRUCT_LAYOUT status="PASS_STATIC">`VisualScavengeAup48` is explicit 48 bytes; `VisualScavengeSignal` remains explicit 80 bytes.</STRUCT_LAYOUT>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched because the known `CS2001` foreign-source wall is unchanged.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Oracle-Owned Simulation Frame Counter

What was wrong:
- `ScavengingLootOracleRuntime` stamped request metadata and resolved signal frames with `Time.frameCount`. RNG truth was not using it, but signal/telemetry metadata still depended on Unity presentation frame state.

What was done:
- Added `_simulationFrameCounter`.
- Queue metadata now uses `PeekNextSimulationFrame()`.
- `LateFrameTick()` advances the oracle frame exactly once per drain and passes it into `LootResolutionJob.Frame`.
- Scoped scan now finds no `Time.frameCount`, `Time.deltaTime`, or `Time.fixedDeltaTime` in the SHINOBU oracle/resource-node route.

Cinematic cheats used:
- None. This is rollback hygiene under the existing Dear Lie route.

Exact microseconds saved:
- 0 us claimed. Runtime cost is one `uint` increment per oracle drain. The gain is deterministic signal metadata and removal of Unity frame coupling.

<SELF_AUDIT_DELTA>
  <SIMULATION_FRAME status="PASS_STATIC">Oracle signal and telemetry frames are generated by `_simulationFrameCounter`, not Unity `Time.frameCount`.</SIMULATION_FRAME>
  <ROLLBACK_BOUNDARY status="PASS_STATIC">RNG seed remains AUP/session/table based; the simulation frame is metadata only and does not perturb deterministic loot truth.</ROLLBACK_BOUNDARY>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build rerun launched because the known `CS2001` foreign-source wall is unchanged.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

Build gate recheck:
- `ChemicalInfluenceGrid.cs` and `LogisticsPipeEvents.cs` still return `Test-Path = False`; `git status` still reports both as deleted.
- No active `dotnet`/`csc` process was found, but CPU is 82.6%, above the project build threshold. No build launched.

## 2026-05-19 - Prompt / Assembly Route Revalidation

What was wrong:
- A quick `<Task id=` parser falsely counted zero tasks because the batch file uses `Task NN:` text inside an attributed `<AGENT_PROMPT ...>` tag.
- The report risked implying a dedicated `Hecton8.Scavenging.Runtime.asmdef` exists.

What was done:
- Re-extracted the SHINOBU block with an attribute-aware regex and counted exactly 20 tasks.
- Scanned asmdefs and confirmed there is no dedicated scavenging runtime asmdef.
- Confirmed SHINOBU runtime remains in root `Hecton8.Core`; new `VisualScavengeSignal` and `ItemAcquiredSignalSourceKinds` files are in `Hecton8.Core.Contracts`.

Cinematic cheats used:
- None. This is authority and compile-wall evidence.

Exact microseconds saved:
- 0 us runtime. Prevents false architecture claims and keeps the next integrator from chasing a nonexistent asmdef.

## 2026-05-19 - Latest Static Verification Gate

What was wrong:
- A new build request would violate the AGENTS CPU/compiler gate and would still hit the same foreign `CS2001` wall.

What was done:
- Reran `git diff --check` on SHINOBU files; only LF/CRLF warnings remain.
- Reran forbidden API scan over oracle/resource-node/contract files; no matches for Unity random, System.Random, managed collections/LINQ, `string.Format`, `Pack=1`, sequential layout, private persistent native containers, Unity frame time, old prefix-search helpers, or raw AUP local-bit hashing.
- Rechecked compiler gate: no active `dotnet/csc`, CPU 97.5%, and both `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still absent.

Cinematic cheats used:
- No new cheat; existing Dear Lie remains direct inventory signal plus visual fake.

Exact microseconds saved:
- 0 us runtime. Avoids wasting another compile attempt that cannot reach SHINOBU code.

## 2026-05-19 - Item Signal Quantity Contract Clamp

What was wrong:
- The oracle and visual fake carried `uint Quantity`, while `ItemAcquiredSignal.Quantity` is `ushort`.
- The old narrowing happened only in `PublishLootYieldsJob`, after `ResourceNode` capacity preflight and after telemetry payload construction.

What was done:
- Added `ItemSignalMaxQuantity = ushort.MaxValue` and `ClampItemSignalQuantity(...)` as the single clamp route.
- `ResourceNode.TrySpawnLoot()` and incremental yield now capacity-check the exact clamped quantity that inventory can receive.
- The oracle writes request/result clamping flags, telemetry records them, `ItemAcquiredSignal.Flags` marks narrowed payloads, and `VisualScavengeSignal.Flags` mirrors result flags.

Cinematic cheats used:
- No new physical simulation. The Dear Lie route remains direct inventory truth plus visual fake; quantity truth is now identical on both lanes.

Exact microseconds saved:
- 0 us claimed. This is correctness hardening with scalar min/max only.

<SELF_AUDIT_DELTA>
  <QUANTITY_CONTRACT status="PASS_STATIC">Capacity preflight, oracle yield, inventory signal, visual fake, and telemetry now share the same clamped item quantity.</QUANTITY_CONTRACT>
  <ZERO_GC status="PASS_STATIC">No managed allocation, no collection, no new Vault buffer, and no new SignalBus lane.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build launched because the known foreign `CS2001` source-file wall is unchanged and the latest CPU gate was closed.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Mining Impact Debris Dear Lie

What was wrong:
- `ResourceNode.SpawnImpactDebris()` still spawned pooled runtime mineral shards, configured mesh/material/collider/Rigidbody state, queued force/torque, and ran a per-shard lifetime updater.
- This was a CPU physics simulation for a visual chip effect.

What was done:
- Replaced the shard loop with one `DebrisSpawnSignal` using `DebrisSpawnSignal.FlagComputeShard`.
- Particle request count now follows a continuous `GlobalQualityWeight` polynomial and tool-power scalar.
- Removed the runtime debris prefab builder, runtime debris physics materials, `RuntimeDebrisShard`, cardinal-rotation RNG helpers, and per-shard force/torque loop from `ResourceNode`.

Cinematic cheats used:
- Dear Lie: GPU/compute debris conveys the impact, while no rock shard exists as a physics object.

Exact microseconds saved:
- Estimated 50-300 us per impact burst on low-end CPUs by replacing O(k) ObjectPool/GameObject/Rigidbody work with one signal publish.

<SELF_AUDIT_DELTA>
  <DEAR_LIE status="PASS_STATIC">Mining impact debris is now one compute debris signal instead of pooled Rigidbody shards.</DEAR_LIE>
  <SCALABILITY status="PASS_STATIC">Requested debris quantity scales continuously from low to ultra through `GlobalQualityWeight`.</SCALABILITY>
  <ZERO_GC status="PASS_STATIC">No managed allocation, no new SignalBus lane, no new Vault buffer.</ZERO_GC>
</SELF_AUDIT_DELTA>

Build gate recheck:
- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` is now present and modified in the working tree.
- `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is still absent while referenced by `Hecton8.Core.csproj`.
- No active `dotnet`/`csc` process was found, but CPU is 100%, above the project build threshold. No build launched.

## 2026-05-19 - ResourceNode Yield Sample Wall-Clock Purge

What was wrong:
- `ResourceNode.ResolveYieldSampleDeltaSeconds()` used `Time.time` and `_lastYieldSampleTimeSeconds`, making incremental yield mass depend on presentation wall-clock cadence.

What was done:
- Removed `_lastYieldSampleTimeSeconds`.
- `ResolveYieldSampleDeltaSeconds()` now returns deterministic `DefaultFirstYieldSampleSeconds`.
- Scoped scan over `ResourceNode.cs` and `ScavengingLootOracle.cs` now finds no `Time.time`, `Time.frameCount`, `Time.deltaTime`, `Time.fixedDeltaTime`, `_lastYieldSampleTimeSeconds`, or `MaximumYieldSampleSeconds`.

Cinematic cheats used:
- No new physical cheat. This preserves the existing Dear Lie route by keeping item truth deterministic while visual scale stays in `VisualScavengeSignal` and compute debris.

Exact microseconds saved:
- Below 1 us per mining damage call: removes one Unity time read, one float state write, and one elapsed-time clamp. The material gain is rollback hygiene, not frame-time.

<SELF_AUDIT_DELTA>
  <TIME_AUTHORITY status="PASS_STATIC">Incremental yield mass no longer reads Unity wall-clock time in the SHINOBU resource-node/oracle route.</TIME_AUTHORITY>
  <ZERO_GC status="PASS_STATIC">No allocation, no new Vault buffer, no new SignalBus lane.</ZERO_GC>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build launched because CPU was last measured above the project threshold and `LogisticsPipeEvents.cs` remains absent from the referenced project surface.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Gameplay Value-Initializer Audit

What was wrong:
- Gameplay-facing SHINOBU structs used value-type `new` initializer syntax for request, resolve, telemetry, signal, visual AUP, and compute debris payloads. The IL is heap-free, but the proof surface was weak under the "no new keyword during gameplay" mandate.

What was done:
- Replaced those hot route initializer blocks with `default` stack locals plus direct field writes, preserving zeroed explicit-layout padding without `new` syntax.
- Extended the same pattern to late-frame job descriptors and the Vault view return used by the gameplay drain.
- Scoped scan now matches only `MockHarvestRequestJob` `ScavengingHarvestRequestDTO` / `InventoryCapacityDTO` initializers for that DTO family; player gameplay request/publish/debris paths no longer use those initializer blocks.

Cinematic cheats used:
- None. This is Zero-GC proof hardening around the existing Dear Lie signal route.

Exact microseconds saved:
- 0 us runtime claimed. The result is auditability and lower risk of future managed construction entering the gameplay path.

<SELF_AUDIT_DELTA>
  <ZERO_GC status="PASS_STATIC">Gameplay-facing request, resolve, telemetry, item, visual, depletion, HUD, visual-AUP, job descriptor, Vault view, and debris payloads use `default` stack locals and direct field writes.</ZERO_GC>
  <COLD_EXCEPTION status="PASS_STATIC">Remaining matched DTO initializer syntax is confined to `MockHarvestRequestJob`.</COLD_EXCEPTION>
  <COMPILE_GATE status="BLOCKED_BY_DEPENDENCY">No build launched because CPU remains above threshold and `LogisticsPipeEvents.cs` is still absent.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 - Consolidated Forensic Self-Audit Snapshot

<SELF_AUDIT agent="SHINOBU_125" domain="SCAVENGING_LOOT_TABLE_ORACLE">
  <TASK_RECONCILIATION>01 PASS binary graveyard fallback; 02 PASS no loot prefab spawn; 03 PASS hot DTO public fields; 04 PASS explicit ARM64 layout; 05 PASS deterministic mock; 06 PASS deterministic RNG kernel; 07 PASS tool/condition gating; 08 PASS Dear Lie direct inventory plus compute debris; 09 PASS async inventory signal; 10 PASS continuous VFX scalar; 11 PASS biome modifier table; 12 PASS AUP depletion hash; 13 PASS inventory-full rejection; 14 PASS rollback fence; 15 PASS Vault/uninitialized active-count gates; 16 PASS 300-frame telemetry ring; 17 PASS editor tuner; 18 PASS byte-span CSV parser; 19 PASS editor gizmo; 20 PASS self-audit/static verification.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>LootTableEntryDTO explicit 16B: ItemHashID offset0 size4, DropWeight offset4 size4, ConditionMask offset8 size4, _pad0 offset12 size4. ScavengingHarvestRequestDTO explicit 128B: AUP 0-47, SessionID 48-55, ResourceNodeHash 56-63, uint fields 64-107, GlobalQualityWeight 108-111, InventoryCapacityDTO 112-127. TelemetryEntry explicit 128B; no SHINOBU atomic counter struct introduced.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below GlobalQualityWeight 0.3 the loot truth stays invariant while visual work collapses: VisualScavengeSignal VFX multiplier lerps toward 0.1, impact debris request count follows smoothstep quality from about 4-6 compute particles, and no CPU Rigidbody/GameObject debris simulation exists. At high/ultra the same signal can feed dense GPU compute debris and richer VFX without changing item truth.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_arrays="zero">Requested buffer IDs: 70930 LootEntries, 70931 HarvestRequests, 70932 ResolvedYields, 70933 BiomeModifiers, 70934 TelemetryRing, 70935 DistributionAudit, 70936 CsvScratch.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>NoAlias is present on Burst NativeArray fields. Late-frame chain: EnsureLootTableJob(default) -> LootResolutionJob.Schedule(dependency) -> PublishLootYieldsJob.Schedule(resolveHandle) -> explicit SignalBus flush fence Complete(). Cold audit/fallback completions are documented as cold sync paths.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime asmdef reference was added; current repo has no dedicated Hecton8.Scavenging.Runtime.asmdef, so SHINOBU code remains in existing Core while narrow contracts live in Core.Contracts. Compile retry is blocked: CPU 100%, no compiler process, ChemicalInfluenceGrid.cs present, LogisticsPipeEvents.cs absent.</COMPILE_GUARD>
  <DEAR_LIE>Physical ore loot and mining debris are faked. Before: O(k) ObjectPool/GameObject/Rigidbody shards plus pickup world-item path. After: O(1) direct ItemAcquiredSignal / VisualScavengeSignal and O(1) DebrisSpawnSignal.FlagComputeShard.</DEAR_LIE>
</SELF_AUDIT>
