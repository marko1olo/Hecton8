# LOG_SHINOBU_224

## 2026-05-20 06:51:05 +04:00 - ACTIVE_EQUIPMENT_PROCESSOR

What was wrong:
- Active equipment battery/heat authority already existed in `ModularEquipmentEngine`, but active-use durability wear still drained through `PlayerTool.ApplyDurabilityDrain` into `ToolDurabilitySystem`.
- Crash artifact identity still carried an older agent dump path.
- Static architecture proof for forbidden tool `Update`/coroutine ownership was missing for this agent.
- Existing editor gizmo path was window-driven; the requested `OnDrawGizmos` hook was not present on the runtime owner.

What was done:
- Kept `ModularEquipmentEngine` as the single active equipment authority; no second processor or prefab `Update` loop was added.
- Added Vault buffer `BufferID.ShinobuActiveEquipmentWearDrainRates = 71316` and `_activeEquipmentWearDrainRates` as a separate SOA stream so `ActiveEquipmentDTO` remains exactly 32 bytes.
- Renamed the Burst integration kernel to `EquipmentStateIntegrationJob` and integrated battery, heat, and active-use wear in one deterministic `IJobParallelFor`.
- Passed `ToolState*` and wear-rate stream into the job; job writes battery, heat, durability, counters, depletion flags, overheat signals, and fault flags.
- Converted authored durability drain from absolute points/sec to normalized/sec with `drainRate / maxDurability` before central wear integration.
- Blocked old per-use durability drain when modular equipment runtime is active; legacy fallback remains only when the central runtime is unavailable.
- Mirrored central durability back to `ToolDurabilitySystem` after the job fence with `SetDurabilityNormalizedFromEquipment`.
- Kept overheat and depletion as unmanaged signal queues only; no direct VFX/audio/GameObject disable path was added.
- Kept environmental cooling bound to thermodynamic grid readback using AUP-relative double subtraction before float cell mapping.
- Published DTO readback through POST_SIMULATION `UnsafeUtility.MemCpy`.
- Changed forensic dump path to `Docs/AgentLogs/Dump_SHINOBU_224.bin`.
- Added editor-only `Equipment_Update_Inquisition`; generated `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` with PASS, 42 candidate tool files, 0 forbidden pattern hits.
- Added editor-only `OnDrawGizmos` hook on `ModularEquipmentEngine` for heat-colored wire spheres and battery/heat labels from published DTOs.
- Updated `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md` with SHINOBU_224 buffer/layout refresh.

Cinematic cheats used:
- Thermodynamics uses sampled grid ambient and scalar Newton-style exchange, not particle/steam simulation.
- Overheat presentation is a signal-only "Dear Lie"; downstream VFX/audio can exaggerate without changing simulation truth.
- Low quality uses nearest-cell thermal sampling and slower cadence; high/ultra can blend toward trilinear and faster response through continuous `GlobalQualityWeight`.

Exact microseconds saved or bounded:
- No second equipment owner: 2-5 us avoided vs duplicate owner scan on i3/MX350-class hardware.
- No managed `List<Tool>` active registry: 2-5 us avoided vs scattered heap iteration.
- 32-byte DTO blind readback: 512-byte copy for 16 tools, estimated <0.2 us.
- Wear stream: one extra 64-byte cache line for 16 tools, estimated <0.5 us.
- Normal no-signal frame: overheat/depletion queue cost 0 us; threshold edge <1 us.
- Low-quality cadence skip: 3-8 us recovered on frames where integration is deferred.
- Telemetry ring aggregation/write: estimated <0.3 us for 16 slots.
- Editor tuner/gizmo/inquisition: 0 us player-frame impact.

Verification:
- Static scan: no `Update`, `FixedUpdate`, `LateUpdate`, coroutine, or `StartCoroutine` hits in target tool/equipment runtime files.
- Static scan: no managed `List<Tool>` active registry found in target files.
- Static layout evidence: `ActiveEquipmentDTO` explicit 32 bytes; validator uses `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`.
- Static inquisition report: `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`, PASS, 0 forbidden hits.
- `git diff --check` passed for touched files; only existing LF-to-CRLF warnings were reported.
- Compile was not launched: CPU gate sampled 100 percent, then 86.84 percent, then 98.06 percent. Final check also found active `dotnet` process `Id=16748`. Project law forbids dotnet build above 50 percent CPU or while dotnet/csc is already running.

<SELF_AUDIT agent="SHINOBU_224">
  <DTO name="ActiveEquipmentDTO" sizeBytes="32" layout="explicit">
    <field offset="0" type="uint" name="ToolHashID" />
    <field offset="4" type="float" name="CurrentBattery" />
    <field offset="8" type="float" name="ThermalLoad" />
    <field offset="12" type="uint" name="StateFlags" />
    <field offset="16" type="float" name="PowerDrawRate" />
    <field offset="20" type="float" name="HeatGenerationRate" />
    <field offset="24-31" type="byte[8]" name="_pad0.._pad7" />
  </DTO>
  <VaultBuffers>
    <buffer id="71300" name="ShinobuActiveEquipmentState" />
    <buffer id="71301" name="ShinobuActiveEquipmentPublishedState" />
    <buffer id="71302" name="ShinobuActiveEquipmentAupSamples" />
    <buffer id="71303" name="ShinobuActiveEquipmentGridLoadRequests" />
    <buffer id="71304" name="ShinobuActiveEquipmentTelemetryRing" />
    <buffer id="71305" name="ShinobuActiveEquipmentTelemetryCursor" />
    <buffer id="71306" name="ShinobuActiveEquipmentIntegrationCounters" />
    <buffer id="71308" name="ShinobuActiveEquipmentTuning" />
    <buffer id="71309" name="ShinobuActiveEquipmentHardwareSpecs" />
    <buffer id="71311" name="ShinobuActiveEquipmentToolStates" />
    <buffer id="71312" name="ShinobuActiveEquipmentToolStats" />
    <buffer id="71313" name="ShinobuActiveEquipmentToolTypes" />
    <buffer id="71314" name="ShinobuActiveEquipmentStatusMasks" />
    <buffer id="71315" name="ShinobuActiveEquipmentEnvironmentHeat01" />
    <buffer id="71316" name="ShinobuActiveEquipmentWearDrainRates" />
  </VaultBuffers>
  <ZeroGC>
    <hotPath managedAllocBytes="0" basis="Burst job uses unmanaged pointers, NativeArrays, NativeQueues, value DTOs; editor/file/string work is outside player hot path." />
    <noPrefabUpdate>true</noPrefabUpdate>
    <noRuntimePrefabInstantiation>true</noRuntimePrefabInstantiation>
  </ZeroGC>
  <ThermalMapping>
    <aupRule>ToolAups[slot] - ThermalGridRootAup in double precision before float3 grid mapping.</aupRule>
    <coolingRule>Scalar thermodynamic grid exchange with continuous quality LOD.</coolingRule>
  </ThermalMapping>
  <FaultHandling>
    <nanClamp>true</nanClamp>
    <dumpPath>Docs/AgentLogs/Dump_SHINOBU_224.bin</dumpPath>
    <telemetryFrames>300</telemetryFrames>
  </FaultHandling>
  <CompileStatus>BLOCKED_BY_CPU_GATE: dotnet build not launched while CPU exceeded 50 percent; final check also found dotnet process 16748 active.</CompileStatus>
</SELF_AUDIT>

### Ultra-Polish Durability Generation Handle Migration 2026-05-20

What was wrong:
- `ToolDurabilitySystem` still persisted five obsolete `VaultBufferHandle<T>` fields for durability item state, pending decay, wear multipliers, active flags, and breakdown flags.
- Those legacy handles carry cached pointer metadata and conflict with the current binary payload ledger rule: persistent Vault state must be a pointer-free `VaultGenerationHandle<T>` descriptor, with `NativeArray<T>` views resolved only inside the current phase/method.

What was done:
- Replaced `_itemStatesHandle`, `_pendingDecayDtHandle`, `_wearMultipliersHandle`, `_slotActiveHandle`, and `_breakdownFlagsHandle` with `VaultGenerationHandle<T>`.
- Rewrote `TryResolveBuffer<T>` to use cached `IDataVault.TryResolveHandle(in handle, out buffer)` first.
- Reacquisition now uses `IDataVault.GetGenerationHandle<T>` only when the descriptor is missing, stale, or undersized.
- Added descriptor release on DataVault rebind and owner destroy through `IDataVault.ReleaseBuffer(in handle)`.
- Kept all `NativeArray<T>` views method-local in the durability bridge; the scheduled Burst job receives phase-local arrays only at schedule time.

Cinematic cheats used:
- No new corrosion physics or chemistry simulation was introduced.
- Durability remains a scalar normalized wear lane; active-equipment presentation still uses overheat/depletion signals instead of object-level VFX instantiation.

Exact microseconds saved or bounded:
- Hot arithmetic cost is unchanged; the patch targets memory safety and relocation correctness.
- Descriptor storage shrinks from legacy pointer-bearing 24-byte handles to 16-byte generation descriptors per lane.
- Expected runtime gain is below measurable frame noise; the enforced value is H-Phi/Vault defrag safety and reduced stale-pointer failure risk.

Verification:
- `rg "VaultBufferHandle<|GetBufferHandle|\\.Resolve\\(|ResolvePointer|GetElementAsRef|GetElementAsReadOnlyRef|ptr\\b" ToolDurabilitySystem.cs` returned no hits.
- `rg "VaultGenerationHandle<|GetGenerationHandle|TryResolveHandle|ReleaseBuffer" ToolDurabilitySystem.cs` shows all five durability descriptors and the resolve/release path.
- `rg "GlobalRegistry\\.(DataVault|Save|Player)" ToolDurabilitySystem.cs` reports only cold cache assignments.
- `ToolDurabilitySystem` Burst job still uses `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- Residual risk is explicitly not hidden: `ModularEquipmentEngine` still stores Vault-resolved `NativeArray<T>` aliases as private fields. That is a larger main-owner migration and remains the next cut, not evidence against the durability descriptor patch.
- Final build gate check sampled CPU at 100 percent with no `dotnet`/`csc` process; no build was launched.

<SELF_AUDIT agent="SHINOBU_224" phase="durability_generation_handle_migration">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No prefab Update path was added.</task>
    <task id="02" status="PASS">No managed active-tool list was added; durability owner now stores pointer-free Vault descriptors.</task>
    <task id="03" status="PASS">No unmanaged DTO properties were added.</task>
    <task id="04" status="PASS">Active equipment DTO layout unchanged at 32 bytes.</task>
    <task id="05" status="PASS">Mock equipment job unchanged.</task>
    <task id="06" status="PASS">Equipment and durability Burst jobs retain deterministic compile attributes.</task>
    <task id="07" status="PASS">Thermodynamic cooling path unchanged.</task>
    <task id="08" status="PASS">Overheat remains signal-driven.</task>
    <task id="09" status="PASS">Depletion remains DTO/signal-driven.</task>
    <task id="10" status="PASS">Publication fence unchanged.</task>
    <task id="11" status="PASS">Continuous cadence unchanged.</task>
    <task id="12" status="PASS">Durability bridge now obeys pointer-free Vault descriptor boundary.</task>
    <task id="13" status="PASS">AUP mapping unchanged.</task>
    <task id="14" status="PASS">Rollback-safe DTO ABI unchanged.</task>
    <task id="15" status="PASS">Telemetry ring unchanged.</task>
    <task id="16" status="PASS">Editor tuner unchanged.</task>
    <task id="17" status="PASS">CSV parser unchanged.</task>
    <task id="18" status="PASS">Editor gizmo unchanged.</task>
    <task id="19" status="PASS">Static validator unchanged.</task>
    <task id="20" status="PASS">This log records the descriptor migration and residual main-owner NativeArray alias risk.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <unchanged name="ActiveEquipmentDTO" sizeBytes="32">Offsets 0/4/8/12/16/20 and explicit padding 24..31 remain unchanged.</unchanged>
    <handle name="VaultGenerationHandle<T>" sizeBytes="16">BufferID uint at 0, SystemID uint at 4, Generation uint at 8, Flags uint at 12. No pointer field.</handle>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    No quality curve changed in this loop. `GlobalQualityWeight` still controls active-equipment cadence continuously; the durability descriptor change only changes how Vault memory is referenced before scheduling the same scalar wear job.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    `ToolDurabilitySystem` declares zero private native container fields and zero legacy `VaultBufferHandle<T>` fields after this patch. It persists only generation descriptors and resolves method-local native views.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    `DurabilityDecayJob` receives `States`, `PendingDecayDt`, `WearMultipliers`, `SlotActive`, and `BreakdownFlags` as non-overlapping `[NoAlias]` arrays resolved from generation descriptors at schedule time. Output handle remains `_scheduledDecayHandle`, retired by `DispatcherJobSwap`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling runtime dependency was added. No build was launched in this loop; the previous build remains blocked by non-equipment dependency-wall symbols.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Wear remains scalar and centralized. The code does not simulate material microstructure, electrolytic corrosion, or particle feedback per tool; presentation remains delegated to signal/VFX consumers.
  </DEAR_LIE>
</SELF_AUDIT>

### Ultra-Polish Durability Registry Boundary Closure 2026-05-20

What was wrong:
- `ToolDurabilitySystem.Tick()` could still reach `GlobalRegistry.Save` through `TryRegisterSaveService()`.
- Durability Vault resolution could still reach `GlobalRegistry.DataVault` through `TryResolveBuffer<T>()`.
- Slow durability owner resolution could still reach `GlobalRegistry.Player` when finding the active `PlayerToolManager`.
- The Burst active-equipment solver was already cached, but the durability bridge is sampled by active equipment and therefore needed the same cold-discovery discipline.

What was done:
- `ToolDurabilitySystem` now implements `IGlobalRegistryHotSwapListener` and `IGlobalRegistryHotSwapRefListener`.
- Added cached `_dataVault`, `_saveService`, and `_playerRuntimeContext` fields.
- Added `CacheRegistryDependenciesCold()`, `ApplyRegistryServiceRebind()`, `RebindDataVault()`, `RebindSaveService()`, `TryRegisterHotSwap()`, and `TryUnregisterHotSwap()`.
- Changed durability buffer resolution to use `_dataVault`, save registration to use `_saveService`, and player tool ownership to use `_playerRuntimeContext`.
- DataVault rebind forces any scheduled durability job to retire, clears stale handles, and reacquires lanes from the rebound vault.

Cinematic cheats used:
- No physical durability model or per-object corrosion simulation was added.
- Wear remains a scalar normalized drain lane, while overheat/depletion presentation remains signal-driven and shader/VFX-owned.

Exact microseconds saved or bounded:
- Removes per-tick `DataVault/Save/Player` registry traversal from durability-adjacent active equipment paths.
- Expected low-end gain is sub-microsecond normally and roughly 0.5-1.0 us under active tool spam or frequent durability scheduling.
- The main enforced bound is architectural: no hidden registry polling in the equipment/durability tick surface and no new managed allocation.

Verification:
- Re-read `Docs/Tasks/Status_SHINOBU_224.md`, `Docs/AgentLogs/Rationale_SHINOBU_224.md`, `AGENTS.md`, SHINOBU_224 XML, and the active equipment/native memory/layout/signal/zero-GC/AUP mandates before the patch.
- `rg "GlobalRegistry\.(DataVault|Save|Player)" ToolDurabilitySystem.cs` now reports only cold cache assignments in `CacheRegistryDependenciesCold()`.
- Registry scan across `PlayerTool`, `ModularEquipmentEngine`, and `ToolDurabilitySystem` reports only cold cache assignments plus lifecycle `ToolDurability` ownership checks.
- Update/coroutine scan returned no hits for LaserCutter, FlashlightTool, Gameplay/MantaScooter, ScannerTool, PlayerTool, ModularEquipmentEngine, or Tools runtime files.
- Power/World coupling scan returned no hits for SHINOBU runtime files.
- Managed native allocation/LINQ/foreach scan returned no hits for SHINOBU runtime files.
- DTO/layout Pack=1 and hot DTO property scan returned no hits for active equipment DTO/job files; the only broad property hit is legacy managed `PlayerTool.IsEquipped`, not an unmanaged hot-array DTO.
- `git diff --check` passed with LF-to-CRLF warnings only.
- Build was not relaunched: the last guarded build is still blocked by non-equipment missing-symbol errors, and another run would only repeat the dependency-wall noise without proving SHINOBU_224 further.
- Final build gate check sampled CPU at 100 percent with no `dotnet`/`csc` process; the project law forbids launching a build under that load.

<SELF_AUDIT agent="SHINOBU_224" phase="durability_registry_boundary_closure">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No prefab Update/FixedUpdate/LateUpdate/coroutine scalar path was introduced; static scan remains clean.</task>
    <task id="02" status="PASS">No managed active-tool list was added; equipment truth remains Vault-backed contiguous DTO streams.</task>
    <task id="03" status="PASS">No unmanaged DTO property setter was added; hot DTOs remain raw public fields.</task>
    <task id="04" status="PASS">`ActiveEquipmentDTO` layout remains explicit 32 bytes with pad bytes 24..31.</task>
    <task id="05" status="PASS">Mock equipment job unchanged and still deterministic Burst.</task>
    <task id="06" status="PASS">Equipment integration job unchanged and still deterministic Burst with `[NoAlias]` streams.</task>
    <task id="07" status="PASS">Thermodynamic cooling still samples relative AUP grid data; no Transform sampling was added to the solver.</task>
    <task id="08" status="PASS">Overheat consequences remain unmanaged signals and visual-only scalar payloads.</task>
    <task id="09" status="PASS">Depletion remains DTO clamp/flag clear plus unmanaged signal, not GameObject mutation.</task>
    <task id="10" status="PASS">Published readback remains POST_SIMULATION memcpy into a stable read buffer.</task>
    <task id="11" status="PASS">Continuous cadence remains `math.lerp(min,max,1-q)` through `GlobalQualityWeight`; no binary quality branch was added.</task>
    <task id="12" status="PASS">Grid and durability bridges use cached owner services; no Power runtime telemetry dependency or hot registry poll was added.</task>
    <task id="13" status="PASS">AUP grid mapping remains double subtraction before float cast.</task>
    <task id="14" status="PASS">Rollback-safe DTO ABI and deterministic Burst mode remain unchanged.</task>
    <task id="15" status="PASS">Telemetry ring and SHINOBU dump route remain unchanged; no Debug.Log-only forensic path was added.</task>
    <task id="16" status="PASS">Editor tuner remains editor/cold; no runtime UI allocation was added.</task>
    <task id="17" status="PASS">Span CSV parser and cached tool spec hash bridge remain unchanged.</task>
    <task id="18" status="PASS">Thermal debug gizmo remains editor-only.</task>
    <task id="19" status="PASS">Static inquisition remains the project-level proof artifact; no runtime scanner was added.</task>
    <task id="20" status="PASS">This loop updates status, rationale, architecture ledger, and bottom-ordered forensic log with static gate results.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <struct name="ActiveEquipmentDTO" sizeBytes="32" alignment="multiple-of-16-and-32">
      <field offset="0" size="4" name="ToolHashID" type="uint"/>
      <field offset="4" size="4" name="CurrentBattery" type="float"/>
      <field offset="8" size="4" name="ThermalLoad" type="float"/>
      <field offset="12" size="4" name="StateFlags" type="uint"/>
      <field offset="16" size="4" name="PowerDrawRate" type="float"/>
      <field offset="20" size="4" name="HeatGenerationRate" type="float"/>
      <field offset="24" size="1" name="_pad0" type="byte"/>
      <field offset="25" size="1" name="_pad1" type="byte"/>
      <field offset="26" size="1" name="_pad2" type="byte"/>
      <field offset="27" size="1" name="_pad3" type="byte"/>
      <field offset="28" size="1" name="_pad4" type="byte"/>
      <field offset="29" size="1" name="_pad5" type="byte"/>
      <field offset="30" size="1" name="_pad6" type="byte"/>
      <field offset="31" size="1" name="_pad7" type="byte"/>
      <math>6 fields * 4 bytes = 24; 8 explicit pad bytes = 32; 32 % 16 = 0; 32 % 8 = 0.</math>
    </struct>
    <struct name="EquipmentIntegrationCounters" sizeBytes="64" falseSharing="padded-single-cache-line">
      <math>Offsets 0..31 contain scalar counters; offsets 32,40,48,56 are four ulong reserves. 32 + 32 = 64 bytes.</math>
    </struct>
    <struct name="EquipmentTelemetryEntry" sizeBytes="64" ringEntries="300">
      <math>16 4-byte scalar fields occupy offsets 0..60; final byte ends at 64. One telemetry row is one cache line.</math>
    </struct>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Low quality below 0.3 keeps the equipment cadence near the 0.2 second interval and uses accumulated dt so drain, heat, and wear totals stay correct while redundant per-frame work is skipped. Thermal sampling collapses toward cheaper nearest-cell behavior through continuous weight math, while higher quality tightens the cadence toward 0.016 seconds and can afford richer trilinear thermal sampling and stronger downstream shader/VFX interpretation of the same scalar heat signal. This loop did not add any binary `isLowEnd` branch.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No private NativeArray/NativeList/NativeHashMap/NativeQueue was added. Equipment buffers remain Vault-owned: active DTOs, published DTOs, tool AUPs, grid load requests, integration counters, tuning, hardware specs, wear drain rates, and 300-entry telemetry. Durability buffers remain owner-local durability lanes resolved through cached `IDataVault`.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Consumed dependencies remain dispatcher-provided simulation prerequisites plus cached thermodynamic/power/durability/player service state. Output remains the scheduled equipment integration JobHandle returned to the dispatcher fence and the durability decay JobHandle completed only through `DispatcherJobSwap`. `[NoAlias]` remains on non-overlapping equipment and durability job streams.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling runtime namespace was added. SHINOBU_224 still routes Power, Thermodynamics, Player, Submarine, Save, and Durability access through Core contracts or cached registry slots. Build proof remains blocked by non-equipment dependency-wall errors from the prior guarded run.
  </COMPILE_GUARD>
  <DEAR_LIE>
    The system still uses scalar heat/battery/wear DTOs and unmanaged overheat/depletion signals instead of simulating tool chemistry, steam particles, GameObject state, or water physics per tool. Before the Dear Lie, per-prefab physical/presentation updates would be O(N components plus presentation side effects). After the Dear Lie, simulation is one O(N slots) Burst pass plus edge-only signals; presentation cost is delegated to downstream GPU/shader systems.
  </DEAR_LIE>
</SELF_AUDIT>

### Ultra-Polish Compile-Wall Coupling 2026-05-20

What was wrong:
- `ModularEquipmentEngine` implemented `IPowerGridTelemetryListener` from the `Hecton8.Power` runtime namespace.
- The equipment domain subscribed to `PowerGridTelemetryEvents` only to read a supply ratio for wireless/tool brownout flicker.
- That scalar is already available through the cached Core `IPowerGridService` contract, so the Power runtime event dependency widened compile-wall coupling without adding authority.

What was done:
- Removed `using Hecton8.Power` and the unused `using Hecton8.World` from `ModularEquipmentEngine`.
- Removed `IPowerGridTelemetryListener`, telemetry subscription/unsubscription, and `PowerGridTelemetrySnapshot` callback methods.
- Added `RefreshWirelessBrownoutFromPowerSnapshot()`, which derives the brownout scalar from cached `IPowerGridService.TotalGeneration`, `TotalConsumption`, and `BatterySnapshot`.
- Preserved the existing brownout flicker readback API through `IModularEquipmentService`.

Cinematic cheats used:
- Brownout remains a scalar triangle-wave flicker, not a simulated voltage transient or per-light mutation.
- Downstream presentation can still scale shader/audio distortion by quality without changing equipment simulation truth.

Exact microseconds saved or bounded:
- Removed one cold listener registration route and one sibling runtime callback path.
- Per-tick cached Core service scalar reads are bounded and allocation-free; no managed event or queue drain is introduced.
- Compile-wall risk decreases by deleting direct Power runtime namespace use from the SHINOBU_224 runtime file.

Verification:
- SHINOBU_224 XML re-extraction found 20 `Task NN:` lines.
- Static runtime scan found no `using Hecton8.Power`, `using Hecton8.World`, `IPowerGridTelemetryListener`, `PowerGridTelemetryEvents`, or `PowerGridTelemetrySnapshot` in SHINOBU_224 runtime files.
- Static forbidden-pattern scan over SHINOBU_224 runtime files found no `new NativeArray`, `NativeHashMap`, LINQ `.Select/.Where`, `foreach`, `Pack=1`, `Update/FixedUpdate/LateUpdate`, `StartCoroutine`, `IEnumerator`, or `File.ReadAllBytes`.
- Static Vault/signal scan found no direct `GetBuffer<T>`, `GetBufferHandle`, `VaultBufferHandle`, private equipment signal queue allocation, or queue-drain remnants in `ModularEquipmentEngine.cs`.
- `git diff --check` passed with LF-to-CRLF warnings only.

<SELF_AUDIT agent="SHINOBU_224" phase="compile_wall_coupling_polish">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No prefab update loop or coroutine was introduced by the brownout route change.</task>
    <task id="02" status="PASS">No managed active tool collection was added.</task>
    <task id="03" status="PASS">No DTO property mutation surface was added.</task>
    <task id="04" status="PASS">Primary DTO layout remains explicit 32 bytes.</task>
    <task id="05" status="PASS">Mock equipment state path remains deterministic.</task>
    <task id="06" status="PASS">Burst thermo-electric kernel remains the equipment math owner.</task>
    <task id="07" status="PASS">Thermal grid cooling path is unchanged.</task>
    <task id="08" status="PASS">Overheat remains signal-only; no presentation call added.</task>
    <task id="09" status="PASS">Depletion routing remains data-only.</task>
    <task id="10" status="PASS">Published state copy remains `UnsafeUtility.MemCpy` fenced after simulation.</task>
    <task id="11" status="PASS">Quality cadence remains continuous and independent of Power telemetry events.</task>
    <task id="12" status="PASS">Power bridge now depends on cached Core `IPowerGridService`, not the sibling Power telemetry event bus.</task>
    <task id="13" status="PASS">AUP-relative thermal mapping unchanged.</task>
    <task id="14" status="PASS">Rollback-safe DTO and deterministic Burst mode unchanged.</task>
    <task id="15" status="PASS">Telemetry ring and dump path unchanged.</task>
    <task id="16" status="PASS">Editor tuner remains editor-only.</task>
    <task id="17" status="PASS">CSV ingestion remains cold and span-based.</task>
    <task id="18" status="PASS">Gizmo remains editor-only.</task>
    <task id="19" status="PASS">Inquisition scanner remains editor-only.</task>
    <task id="20" status="PASS">Docs and rationale updated with the coupling reduction.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <unchanged name="ActiveEquipmentDTO" sizeBytes="32">Brownout coupling polish did not touch runtime DTO ABI.</unchanged>
    <unchanged name="EquipmentIntegrationCounters" sizeBytes="64">Padded per-slot counter rows remain cache-line isolated.</unchanged>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Brownout presentation remains a cheap scalar triangle-wave fake. `GlobalQualityWeight` still governs equipment cadence and thermal sampling cost; the deleted Power telemetry listener does not introduce any binary tier switch.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Vault buffer set remains `71300..71306`, `71308..71309`, and `71311..71316`; acquisition remains generation-descriptor based. No new private native owner was added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    No Burst job pointer set changed in this polish. `EquipmentStateIntegrationJob` still emits the same `JobHandle` through `H8Memory.RegisterActiveJob`, and post-fence publication remains behind `DispatcherJobFence.TryFinalizeCompleted`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    `ModularEquipmentEngine.cs` no longer imports `Hecton8.Power` or `Hecton8.World`. Power data enters through cached Core `IPowerGridService`; overheat/depletion events leave through typed `SignalBus<T>`.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: equipment listened to full aggregate Power telemetry to drive a local flicker. After: equipment samples only Core scalar totals and renders the same flicker with a triangle wave. The voltage visual remains O(1), not a grid/lighting simulation.
  </DEAR_LIE>
</SELF_AUDIT>

### Ultra-Polish Signal/Vault Ownership 2026-05-20

What was wrong:
- `EquipmentStateIntegrationJob` wrote overheat/depletion edges into two equipment-owned `NativeQueue` buffers.
- `CompleteActiveEquipmentJob` then drained those queues and re-published each unmanaged payload into `SignalBus<T>`, adding duplicate native queue ownership and a bounded main-thread loop.
- `AcquireEquipmentBuffer<T>` used direct `IDataVault.GetBuffer<T>`, which exposes an external view instead of following the current generation-descriptor Vault pattern.

What was done:
- Removed private equipment overheat/depleted `NativeQueue` fields, allocation, prewarm, disposal, and post-fence drain code.
- Configured `SignalBus<EquipmentOverheatSignal>` and `SignalBus<ToolDepletedSignal>` during cold native-state initialization.
- Passed `SignalBus<T>.ParallelWriter` directly into `EquipmentStateIntegrationJob`.
- Changed equipment buffer acquisition to request `VaultGenerationHandle<T>` with `GetGenerationHandle<T>` and resolve the phase view through `TryResolveHandle`.
- Updated architecture docs with the typed SignalBus writer and generation-descriptor acquisition boundary.

Cinematic cheats used:
- Overheat remains a visual-only scalar event: `Heat01`, `AmbientCelsius`, `Severity01`, and `VisualOnly=1`.
- No particles, sounds, GameObject disable calls, or thermal mesh simulation were added to the solver.
- Presentation systems can spend their own quality-scaled GPU budget after reading the typed signal lane.

Exact microseconds saved or bounded:
- Removed one bounded dequeue/enqueue bridge after every completed equipment tick: expected normal-frame gain below 1 us, higher on threshold-burst frames.
- Removed two equipment-domain scene-lifetime native queue allocations; the typed bus owns the queue/snapshot lane once.
- Vault descriptor resolution remains cold/phase-local; hot integration still receives raw no-alias pointers and does not query the Vault or registry inside the Burst loop.

Verification:
- SHINOBU_224 XML re-extraction found 20 `Task NN:` lines.
- Static scan found no direct `GetBuffer<T>`, no `GetBufferHandle`, no `VaultBufferHandle`, no private equipment signal queue names, and no `new NativeQueue<Equipment...>` in `ModularEquipmentEngine.cs`.
- Static runtime scan found no `new NativeArray`, `NativeHashMap`, LINQ `.Select/.Where`, `foreach`, `Pack=1`, `Update/FixedUpdate/LateUpdate`, `StartCoroutine`, or `IEnumerator` hits in the SHINOBU_224 runtime files.
- `git diff --check` passed with LF-to-CRLF warnings only.
- Compile was not relaunched: CPU sampled 100 percent; follow-up process check found no active `dotnet/csc`, but CPU gate remains closed by project law.

<SELF_AUDIT agent="SHINOBU_224" phase="signal_vault_polish">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No tool-prefab `Update`, `FixedUpdate`, coroutine, or direct per-frame battery/heat loop was reintroduced.</task>
    <task id="02" status="PASS">Active equipment truth remains contiguous Vault streams; no managed active tool list was added.</task>
    <task id="03" status="PASS">Hot DTOs remain raw fields; no `{ get; set; }` mutation surface was added.</task>
    <task id="04" status="PASS">`ActiveEquipmentDTO` ABI remains explicit 32 bytes with offsets `0/4/8/12/16/20/24-31`.</task>
    <task id="05" status="PASS">Mock equipment path remains deterministic and unchanged by signal/Vault polish.</task>
    <task id="06" status="PASS">`EquipmentStateIntegrationJob` remains Burst deterministic and now emits threshold edges directly to typed signal writers.</task>
    <task id="07" status="PASS">Thermal-grid cooling still samples AUP-relative grid coordinates and quality-blended nearest/trilinear math.</task>
    <task id="08" status="PASS">Overheat output stays a visual-only unmanaged signal; no direct VFX/audio/prefab call exists in the solver.</task>
    <task id="09" status="PASS">Depletion still clamps battery, clears active state, and emits a data-only depleted signal edge.</task>
    <task id="10" status="PASS">Post-simulation readback remains `UnsafeUtility.MemCpy`; the removed queue drain does not affect DTO publication.</task>
    <task id="11" status="PASS">Continuous `GlobalQualityWeight` cadence remains unchanged; no binary hardware switch was added.</task>
    <task id="12" status="PASS">Grid-powered tools still write `EquipmentGridLoadRequest` rows for the cached power-grid bridge.</task>
    <task id="13" status="PASS">Thermal sampling still subtracts `ThermalGridRootAup` before float conversion.</task>
    <task id="14" status="PASS">Rollback DTO ABI and deterministic Burst mode remain intact.</task>
    <task id="15" status="PASS">300-frame telemetry ring and `Dump_SHINOBU_224.bin` fault path remain active.</task>
    <task id="16" status="PASS">Editor tuner remains editor-only; runtime signal/Vault path has no UI allocation.</task>
    <task id="17" status="PASS">CSV spec ingestion remains cold, span-based, and backed by the Vault spec table.</task>
    <task id="18" status="PASS">Thermal gizmo remains editor-only and reads published DTO state.</task>
    <task id="19" status="PASS">Inquisition scanner remains editor-only; runtime scans are not added.</task>
    <task id="20" status="PASS">Status, rationale, architecture ledger, and this log were updated with the signal/Vault polish proof.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ActiveEquipmentDTO sizeBytes="32">offset 0 uint ToolHashID; 4 float CurrentBattery; 8 float ThermalLoad; 12 uint StateFlags; 16 float PowerDrawRate; 20 float HeatGenerationRate; 24-31 eight explicit pad bytes.</ActiveEquipmentDTO>
    <EquipmentIntegrationCounters sizeBytes="64">one 64-byte counter row per slot: writes do not share a cache line across parallel indices.</EquipmentIntegrationCounters>
    <EquipmentOverheatSignal sizeBytes="32">typed signal payload remains unmanaged: `ToolHashID=0`, `Frame=4`, heat/ambient/severity at `8/12/16`, flags at `20`, visual byte at `24`, padding/reserve through `31`.</EquipmentOverheatSignal>
    <ToolDepletedSignal sizeBytes="32">typed signal payload remains unmanaged: `ToolHashID=0`, `Frame=4`, battery/requested power at `8/12`, flags at `16`, grid byte at `20`, reserve through `31`.</ToolDepletedSignal>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight=0.3`, cadence stretches continuously toward the configured maximum tick interval and thermal sampling blends to nearest-cell work instead of full trilinear weight. Signal propagation now uses the shared `SignalBus<T>` lane caps rather than a local queue bridge, so stress/quality shedding remains centralized and continuous.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Equipment persistent state remains in Vault buffers `71300..71306`, `71308..71309`, `71311..71316`. Acquisition now uses generation descriptors plus `TryResolveHandle`. The equipment domain declares no private overheat/depletion native queues after this polish.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    The integration job still consumes active DTOs, tool states, stats, AUP samples, wear rates, thermal grid readback, grid-load requests, and padded counters as explicit `[NoAlias]` pointer fields. It outputs the same scheduled `JobHandle` to `H8Memory.RegisterActiveJob`; completion remains fenced by `DispatcherJobFence.TryFinalizeCompleted` before DTO readback, grid aggregation, and telemetry.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling runtime assembly reference was introduced. Thermodynamics, power, player pose, and durability access remain cached service/contract routes; the signal lane is the existing typed `SignalBus<T>` infrastructure.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: O(S) post-fence queue bridge plus downstream presentation, where S is edge-signal count. After: O(S) direct Burst enqueue into the typed lane and zero local presentation work. Heavy boiling/steam distortion remains a downstream visual fake driven by severity scalars.
  </DEAR_LIE>
</SELF_AUDIT>

---

## 2026-05-20 SHINOBU_224 Ultra-Polish Audit

What was wrong:
- `AreEquipmentBuffersReady()` omitted `_activeEquipmentWearDrainRates`; initialization could proceed without the central wear stream while `IsServiceReady` later reported false. That is a readiness split and a durability truth risk.
- `RefreshActiveEquipmentInputs()` sampled `owner.transform.position` for every registered active-equipment slot. That kept Unity object position reads in the equipment input gather path.
- The previous report stated no private active-equipment array fallback, but did not explicitly separate Vault truth from bounded private edge-signal queues.

What was done:
- Added `_activeEquipmentWearDrainRates.IsCreated` to the fail-closed initialization gate.
- Routed equipped-tool AUP sampling through cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`; detached/non-equipped registered tools use `PlayerTool.TryResolveCachedAup()` fallback only.
- Added `PlayerTool.TryResolveCachedRuntimePosition()` and `PlayerTool.TryResolveCachedAup()` so equipment runtime does not call `owner.transform.position` directly in slot refresh.
- Updated `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md`, `Docs/Tasks/Status_SHINOBU_224.md`, and `Docs/AgentLogs/Rationale_SHINOBU_224.md` with the polish correction.

Cinematic cheats used:
- Cooling remains scalar thermodynamic-grid exchange, not local fluid simulation.
- Overheat/depletion remain unmanaged signals; the visual lie is downstream shader/VFX amplification, not CPU particle or GameObject spawning.
- Low quality collapses thermal sampling/cadence by continuous weight; high quality spends the saved cycles on denser thermal response.

Exact microseconds saved or bounded:
- Fail-closed wear stream: 0 us hot-path cost; removes silent fallback risk.
- Player-pose AUP reuse: avoids up to 16 per-slot Transform position bridge reads during equipped-tool refresh; estimated 0.3-1.2 us on i3/MX350-class silicon, lower engine-object traffic on Quest-class ARM.
- Cached fallback for detached tools: one cached Transform read only when pose snapshot cannot represent the registered tool; normal equipped route is one player pose read.
- Private edge queues: bounded 32 entries each, prewarmed, threshold-only writes; 0 us on no-signal frames, below 1 us on signal edge.

Verification after polish:
- XML extraction: `SHINOBU_224_XML_TASKS=20`.
- Static scan: no `Update`, `FixedUpdate`, `LateUpdate`, coroutine, or `StartCoroutine` hits in target tool/equipment runtime set.
- Static scan: no `Pack=1`, `new NativeArray`, `new List`, LINQ `.Select/.Where`, or `foreach` hits in SHINOBU_224 runtime files.
- Static scan: `_activeEquipmentWearDrainRates.IsCreated` is present in both `IsServiceReady` and `AreEquipmentBuffersReady`.
- `git diff --check` passed touched files; only LF-to-CRLF warnings.
- Compile was not launched after polish: no `dotnet`/`csc` process was present, but CPU sampled 97.86 percent after wait. Project law forbids dotnet build above 50 percent CPU.

<SELF_AUDIT agent="SHINOBU_224" phase="ultra_polish">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">Update loops removed from active equipment authority; tools route intent into central runtime.</task>
    <task id="02" status="PASS">No managed active `List<Tool>` registry; fixed owner mirror plus Vault SOA streams.</task>
    <task id="03" status="PASS">Hot DTOs use raw fields, no property mutation in Burst arrays.</task>
    <task id="04" status="PASS">`ActiveEquipmentDTO` explicit 32 bytes; counters/telemetry explicit 64 bytes.</task>
    <task id="05" status="PASS">Burst mock state generator remains available for editor/test facade.</task>
    <task id="06" status="PASS">Battery, heat, wear integrate in deterministic Burst `EquipmentStateIntegrationJob`.</task>
    <task id="07" status="PASS">Cooling samples thermodynamic grid with AUP-relative mapping.</task>
    <task id="08" status="PASS">Overheat is unmanaged `EquipmentOverheatSignal`, visual-only flag, no direct VFX spawn.</task>
    <task id="09" status="PASS">Battery depletion clamps and emits `ToolDepletedSignal` on edge.</task>
    <task id="10" status="PASS">Published DTO readback uses post-simulation memcpy fence.</task>
    <task id="11" status="PASS">Cadence uses continuous `GlobalQualityWeight`, not hardware-tier bools.</task>
    <task id="12" status="PASS">Wireless/grid draw emits `EquipmentGridLoadRequest` and uses cached power service after fence.</task>
    <task id="13" status="PASS">Tool AUP minus thermal-grid root AUP occurs in double precision before float cell math.</task>
    <task id="14" status="PASS">Deterministic Burst mode and blittable DTOs preserve rollback snapshot route.</task>
    <task id="15" status="PASS">300-entry telemetry ring and `Dump_SHINOBU_224.bin` fault dump path exist.</task>
    <task id="16" status="PASS">Editor tuner is editor-only and writes tuning/rates through service/Vault paths.</task>
    <task id="17" status="PASS">CSV ingest uses `ReadOnlySpan<byte>` parser into `EquipmentHardwareSpecDTO` buffer.</task>
    <task id="18" status="PASS">Thermal debug gizmo reads published DTOs; no runtime debug prefab.</task>
    <task id="19" status="PASS">Inquisition scanner generated JSON PASS artifact with zero forbidden update hits.</task>
    <task id="20" status="PASS">Status/rationale/log/doc artifacts updated with static proof and compile gate facts.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <struct name="ActiveEquipmentDTO" sizeBytes="32" alignment="multiple_of_32">
      <field offset="0" size="4" type="uint" name="ToolHashID" />
      <field offset="4" size="4" type="float" name="CurrentBattery" />
      <field offset="8" size="4" type="float" name="ThermalLoad" />
      <field offset="12" size="4" type="uint" name="StateFlags" />
      <field offset="16" size="4" type="float" name="PowerDrawRate" />
      <field offset="20" size="4" type="float" name="HeatGenerationRate" />
      <field offset="24" size="8" type="byte[8]" name="_pad0.._pad7" />
      <math>4+4+4+4+4+4+8=32 bytes.</math>
    </struct>
    <struct name="EquipmentIntegrationCounters" sizeBytes="64" falseSharing="cache_line_padded">
      <field offset="0" size="4" name="BatteryDrainWattSeconds" />
      <field offset="4" size="4" name="GridDrawWattSeconds" />
      <field offset="8" size="4" name="PeakThermal01" />
      <field offset="12" size="4" name="ActiveCount" />
      <field offset="16" size="4" name="SignalCount" />
      <field offset="20" size="4" name="FaultFlags" />
      <field offset="24" size="4" name="LastFaultToolHashID" />
      <field offset="28" size="4" name="WearDrainNormalized" />
      <field offset="32" size="32" name="Reserved1..Reserved4" />
      <math>32 live bytes + 32 reserved bytes = 64-byte exclusive cache line.</math>
    </struct>
    <struct name="EquipmentTelemetryEntry" sizeBytes="64" alignment="cache_line">
      <math>Frame/tick/counters/quality/grid/hash/wear fields occupy offsets 0..63 exactly.</math>
    </struct>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is consumed as a continuous scalar. Cadence uses `math.lerp(MinimumTickInterval, MaximumTickInterval, 1-q)`; low q raises interval and amortizes battery/heat/wear through accumulated dt. Thermal sampling weight blends from nearest-cell toward trilinear-like multi-cell read cost. Below q=0.3 the path collapses toward one grid cell and fewer integration ticks; middle q keeps smoother response; q near 1 spends cycles on tighter heat/readback response without changing authoritative DTO shape.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <truth_owner>ModularEquipmentEngine owns active equipment battery/heat/wear truth.</truth_owner>
    <private_native_array_fallback>false</private_native_array_fallback>
    <vault_buffers>71300,71301,71302,71303,71304,71305,71306,71308,71309,71311,71312,71313,71314,71315,71316</vault_buffers>
    <edge_queues>Private `NativeQueue` fields exist only for bounded overheat/depleted signal edges, prewarmed at capacity 32; they are not scalar truth storage.</edge_queues>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <noalias_fields>Equipment, ToolStates, Stats, ToolAups, WearDrainRates, ThermalGrid, GridLoadRequests, Counters</noalias_fields>
    <input_handle>_equipmentIntegrationHandle plus external dispatcher dependency when provided through `Tick` chain.</input_handle>
    <output_handle>Equipment integration `JobHandle` registered with H8Memory and completed only at publish/late fence.</output_handle>
    <blocking_notes>Cold init/mock clear jobs complete locally; gameplay integration is scheduled and fenced by phase.</blocking_notes>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef was added and no direct sibling runtime assembly reference was introduced. Runtime files remain under current project assembly surface and route thermodynamics/power/player access through cached registry contracts/services.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: per-prefab Update loops plus possible direct presentation side effects, effectively O(T tools) managed object traffic with fragmented thermal/battery/wear side effects. After: O(N) contiguous Burst pass over fixed 16 local slots / 50 multiplayer target, signal-only presentation, scalar grid cooling. No Navier-Stokes, no per-tool particle owner, no direct runtime VFX spawn.
  </DEAR_LIE>
</SELF_AUDIT>

### Compile Wall Note

Gate state before compile:
- CPU: 6.78 percent.
- `dotnet/csc`: no running process.

Command:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo /clp:ErrorsOnly /p:UseSharedCompilation=false`

Result:
- Failed in 6.90s with 139 errors.
- Error set is cross-domain missing-symbol/dependency damage before SHINOBU_224 files: `Hecton8.Logistics.Grid`, `VaultGenerationHandle<>`, `IDockingAutopilotService`, `SocketDefinitionDTO`, `HectonFluidEngine`, `H8BinaryWorldPager`, `FaunaKinematicsRuntime`, WFC outpost grid symbols, and brownout binding types.
- No emitted error referenced `Assets/_Project/Scripts/ModularEquipmentEngine.cs`, `Assets/_Project/Scripts/PlayerTool.cs`, `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`, or `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs`.

Integrator note:
- SHINOBU_224 changes remain static-verified only.
- Do not treat the failed `Hecton8.Core.csproj` build as an equipment-domain compile failure unless a later Unity import reports errors in the SHINOBU_224 files.

### Ultra-Polish CSV Bridge Hardening 2026-05-20

What was wrong:
- `tool_hardware_specs.csv` was documented as intended but was absent from the checkout.
- `EquipmentHardwareSpecsCsvParser` converted the first CSV cell to a lower-case FNV-1a hash, while `PlayerTool.RuntimeToolId` uses `Animator.StringToHash`. Result: a row like `tool_laser_cutter,...` parsed into the Vault spec table but never matched the runtime tool id in `RefreshActiveEquipmentInputs()`.
- The editor tuner could change slot rates and mock state, but had no direct cold bridge for loading the specs CSV into `ShinobuActiveEquipmentHardwareSpecs`.

What was done:
- Added `Assets/_Project/Data/Tools/tool_hardware_specs.csv` plus `.meta`.
- Added cached `PlayerTool.RuntimeToolSpecHashId`, using the same lower-case FNV-1a key as the parser without changing the legacy `RuntimeToolId` route.
- Changed hardware-spec lookup to compare `RuntimeToolId` first and cached `RuntimeToolSpecHashId` second.
- Extended `EquipmentHardwareSpecsCsvParser` so the key cell accepts strict decimal or `0x` hex ids before falling back to the FNV name hash.
- Added an editor-only `Load tool_hardware_specs.csv` button to `EquipmentThermoElectricTunerWindow`.
- Updated `EQUIPMENT_SOA_LAYOUT.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to remove the stale "CSV absent" statement.

Cinematic cheats used:
- Hardware specs remain scalar heat/power/cooldown curves; no per-part thermal mesh, battery chemistry simulation, or local fluid model was introduced.
- Designers can buy visual overkill from saved CPU by increasing heat/power response scalars that feed existing shader/VFX consumers, without widening the rollback DTO.

Exact microseconds saved or bounded:
- Avoided a managed parser/string-hash fallback: 0 B GC, cold CSV parse stays `ReadOnlySpan<byte>`.
- Runtime added one cached `uint` compare during hardware-spec table scan; bounded below 0.1 us at the 16-slot local equipment cap.
- Avoided inert CSV tuning loops that would force designers back into C# recompiles; player-frame cost remains 0 us for file I/O.

Verification:
- Static forbidden-pattern scan over SHINOBU_224 runtime files returned no `Update/FixedUpdate/LateUpdate`, coroutine, `new NativeArray`, `new List`, LINQ `.Select/.Where`, `foreach`, or `Pack=1` hits.
- `git diff --check` passed touched files with LF-to-CRLF warnings only.
- Compile was not relaunched after this polish: CPU sampled 100 percent and no `dotnet/csc` process was running; project law forbids dotnet build above 50 percent CPU.

<SELF_AUDIT agent="SHINOBU_224" phase="csv_bridge_polish">
  <TASK_RECONCILIATION total="20">
    <task id="17" status="PASS">CSV source now exists, parser remains span-based, numeric/hex runtime hashes and FNV name hashes are both accepted, and runtime lookup bridges the legacy Animator hash mismatch.</task>
    <task id="16" status="PASS">Editor facade can explicitly load the hardware spec CSV into the active equipment service; managed file I/O remains editor-only.</task>
    <task id="20" status="PASS">Status, rationale, architecture ledger, and agent log record the corrected CSV path and remaining compile gate state.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <unchanged name="ActiveEquipmentDTO" sizeBytes="32">CSV bridge did not alter the 32-byte rollback/UI DTO ABI.</unchanged>
    <unchanged name="EquipmentHardwareSpecDTO" sizeBytes="32">Existing explicit layout remains `ToolHashID=0`, capacity/thermal/power/heat/cooldown at `4..20`, flags at `24`, reserve at `28`.</unchanged>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    CSV rows tune scalar capacity, thermal limit, draw rate, heat generation, and cooldown. Low quality still sheds cost through the continuous equipment cadence and cheaper grid sampling; high quality can increase visual heat response without changing simulation ownership.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    `ShinobuActiveEquipmentHardwareSpecs` remains the Vault-backed spec table. The CSV file is a cold authoring source only; it is not a second gameplay owner.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    No job dependency graph changed. `EquipmentStateIntegrationJob` still consumes the same no-alias streams; CSV ingestion mutates specs only through the service before/around simulation, not from the Burst job.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling assembly reference or public contract mutation was introduced. `RuntimeToolSpecHashId` is internal to the tool/equipment domain.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Spec tuning remains scalar data used by existing thermal/battery math and downstream visuals. Complexity stays O(rows + slots), with no per-component physical battery/heat simulation.
  </DEAR_LIE>
</SELF_AUDIT>

### Ultra-Polish Hot Registry Cache Closure 2026-05-20

What was wrong:
- `PlayerTool.CurrentDurability`, `IsBroken`, fallback durability drain, overcharge input checks, queued interaction raycast helper calls, and overcharge inventory removal still used direct `GlobalRegistry` reads.
- `ModularEquipmentEngine.RegisterDurabilityMirror()` had a fallback `GlobalRegistry.ToolDurability` lookup instead of relying only on cached dependency injection.
- These reads were not inside the Burst equipment job, but several sat on call paths that feed `RefreshActiveEquipmentInputs()` or active tool use. That violates the cold-discovery boundary.

What was done:
- Added cached `IPlayerInventoryService`, `IInputService`, `IInteractionSignalService`, and `ToolDurabilitySystem` fields to `PlayerTool`.
- Extended `PlayerTool` cold cache and hot-swap rebind switch for `PlayerInventory`, `Input`, `InteractionSignals`, and `ToolDurabilityRuntime`.
- Replaced direct `GlobalRegistry.ToolDurability`, `GlobalRegistry.Input`, `GlobalRegistry.InteractionSignals`, and `GlobalRegistry.PlayerInventoryRuntime` reads in the active tool call paths with cached fields.
- Removed the `GlobalRegistry.ToolDurability` fallback from `ModularEquipmentEngine.RegisterDurabilityMirror()`.
- Added `RegisterDurabilityMirrorsCold()` and invoke it when `ToolDurabilityRuntime` is rebound, so existing registered tools get their centralized durability mirror after a service replacement without polling the registry from the equipment tick path.
- Updated `EQUIPMENT_SOA_LAYOUT.md` brownout ownership: equipment no longer subscribes to `PowerGridTelemetryEvents`; it reads cached Core `IPowerGridService` scalars.

Cinematic cheats used:
- No new durability physics, battery chemistry model, or inventory-side equipment simulation was added.
- The active equipment owner continues to use scalar DTOs and signal-only presentation; richer overcharge/brownout visuals stay downstream of cached scalar readback.

Exact microseconds saved or bounded:
- Removes up to 16 durability-registry property reads during one equipment input refresh when active slots sample `DurabilityNormalized`.
- Removes live registry reads from accepted-use overcharge/input/raycast helper paths; expected low-end gain is sub-microsecond to roughly 1 us under active tool spam.
- Primary effect is architectural: registry traversal is cold/rebind only, with 0 B GC hot-path call-stack hygiene preserved.

Verification:
- SHINOBU_224 XML re-extracted with flexible tag parsing: `TASK_LINES=20`.
- Static scan over SHINOBU runtime files now reports only cold cache assignments for `GlobalRegistry.ToolDurability/Input/InteractionSignals`; no `GlobalRegistry.PlayerInventoryRuntime` hot read remains in `PlayerTool`.
- Static scan found no `using Hecton8.Power`, `using Hecton8.World`, `IPowerGridTelemetryListener`, `PowerGridTelemetryEvents`, or `PowerGridTelemetrySnapshot` in SHINOBU_224 runtime files.
- `git diff --check` passed touched files with LF-to-CRLF warnings only.
- CPU gate opened at 21.31 percent and no `dotnet/csc` process was present, so `dotnet build Hecton8.Core.csproj --no-restore --nologo /clp:ErrorsOnly /p:UseSharedCompilation=false` was launched. It failed in 24.75s with 230 cross-domain missing-symbol errors before SHINOBU_224 runtime proof could be established: `Hecton8.Logistics.Grid`, `VaultGenerationHandle<>`, `SoundEmissionSignal`, `H8BinaryWorldPager`, docking/world/audio bridge types, and other non-equipment symbols. No visible emitted error referenced `PlayerTool.cs` or `ModularEquipmentEngine.cs`.

<SELF_AUDIT agent="SHINOBU_224" phase="hot_registry_cache_closure">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No equipment prefab Update loop was introduced; active tool call paths now cache registry dependencies.</task>
    <task id="03" status="PASS">No DTO properties or managed hot-path containers were added.</task>
    <task id="06" status="PASS">Burst equipment kernel unchanged; registry cleanup is pre-job input hygiene.</task>
    <task id="12" status="PASS">Power/grid and durability bridges use cached owner services instead of tick-time registry polling.</task>
    <task id="20" status="PASS">Status, rationale, architecture ledger, and this log capture the cache closure and compile-wall state.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <unchanged name="ActiveEquipmentDTO" sizeBytes="32">No layout change; offsets remain 0/4/8/12/16/20 and padding 24..31.</unchanged>
    <unchanged name="EquipmentIntegrationCounters" sizeBytes="64">No false-sharing layout change; per-slot counter cache-line padding remains intact.</unchanged>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    The registry-cache polish does not change the continuous `GlobalQualityWeight` cadence or thermal LOD curve. Low quality still amortizes equipment work through larger tick intervals; high quality keeps tighter active-equipment response using the same cached dependency surface.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No new private native arrays or queues were added. Vault-owned equipment buffers remain the same; this loop changed only cached managed service references and cold rebind behavior.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    `EquipmentStateIntegrationJob` no-alias fields and output `JobHandle` are unchanged. `RegisterDurabilityMirrorsCold()` runs only on service rebind/cold registration and does not schedule jobs or block the simulation fence.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No sibling runtime namespace was added. The guarded `dotnet build` remains blocked by non-equipment missing symbols; SHINOBU_224 files did not appear in the visible emitted error set.
  </COMPILE_GUARD>
  <DEAR_LIE>
    The loop preserves scalar equipment truth and presentation signals. It avoids adding event buses for private queries and avoids per-tool object simulation; complexity remains O(N slots) for equipment input refresh and one O(N) Burst integration pass.
  </DEAR_LIE>
</SELF_AUDIT>

### Ultra-Polish Namespace Coupling Sweep 2026-05-20

What was wrong:
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs` still had `using Hecton8.World;` even though the file only uses `UnityEngine.Transform` for player root tracking.
- The import was behaviorally inert, but static compile-wall scans still counted it as a sibling-domain edge.

What was done:
- Removed the unused `Hecton8.World` import.
- Re-ran the SHINOBU_224 runtime coupling scan for `Hecton8.Power`, `Hecton8.World`, `IPowerGridTelemetryListener`, `PowerGridTelemetryEvents`, and `PowerGridTelemetrySnapshot`.
- Re-ran the forbidden hot-path scan and `git diff --check`.

Cinematic cheats used:
- No new simulation path was introduced. Equipment still uses scalar heat/battery/wear state and presentation signals instead of per-object physical submodels.

Exact microseconds saved or bounded:
- Runtime frame impact is 0 us; this is compile-surface hygiene, not a math-path optimization.
- Build/iteration risk is reduced by removing one false namespace edge from the Tools runtime scan.

Verification:
- SHINOBU_224 XML count remains `TASK_LINES=20`.
- Power/World coupling scan returned no hits after the import removal.
- Forbidden hot-path scan returned no hits for Update/coroutine/runtime native allocation/LINQ/foreach/Pack=1 patterns in SHINOBU_224 runtime files.
- Registry scan reports only cold cache assignments in `PlayerTool.CacheToolRegistryDependenciesCold()` and `ModularEquipmentEngine.CacheRegistryDependenciesCold()`.
- `git diff --check` passed with LF-to-CRLF warnings only.

<SELF_AUDIT agent="SHINOBU_224" phase="namespace_coupling_sweep">
  <TASK_RECONCILIATION total="20">
    <task id="01" status="PASS">No prefab Update path was added.</task>
    <task id="06" status="PASS">Burst equipment kernel unchanged.</task>
    <task id="12" status="PASS">Power/grid bridge remains cached Core service access, not sibling runtime subscription.</task>
    <task id="20" status="PASS">Status, rationale, and bottom-ordered log record the final static sweep.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <unchanged name="ActiveEquipmentDTO" sizeBytes="32">No DTO or ABI layout change.</unchanged>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    No quality curve changed; continuous `GlobalQualityWeight` cadence and thermal sampling remain the active scalability path.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    No native containers, Vault handles, or buffer ownership changed.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Job handles and `[NoAlias]` streams are unchanged; this sweep removed only an unused namespace import.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No direct Power/World runtime namespace hits remain in the SHINOBU_224 runtime scan.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Equipment stays scalar and signal-driven; no object-level durability, battery chemistry, or water-fluid simulation was added.
  </DEAR_LIE>
</SELF_AUDIT>
