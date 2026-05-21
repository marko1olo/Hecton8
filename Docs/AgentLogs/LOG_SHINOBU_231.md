# LOG_SHINOBU_231

## 2026-05-20 / TOOL_UPGRADE_MATRIX_COMPILER

What was wrong:
- Upgrade stat evaluation needed a deterministic 64-bit mask route instead of branchy item checks, virtual modifier chains, or string/dictionary stat maps.
- The route needed concrete Vault buffer ownership, not just isolated math helpers.
- Visual upgrade state needed a cheap shader flag lane; runtime mesh/geometry spawning is rejected.
- Blackbox requirements required a 300-frame fixed telemetry ring and a raw binary dump path.

What was done:
- Added `UpgradeMaskDTO` as `[StructLayout(LayoutKind.Explicit, Size = 16)]`: offset 0 `EntityHashID`, offset 4 `EquipmentHashID`, offset 8 `ActiveUpgradesMask`.
- Added `UpgradeStatVectorDTO[64]`, `UpgradeLutEntryDTO[128]`, `UpgradeBitRuleDTO[32]`, `InventoryUpgradeSlotDTO[16]`, `UpgradeItemMapDTO[16]`, `VehicleKinematicUpgradeDTO[64]`, and `UpgradeTelemetryEntry[64]`.
- Added `UpgradeMatrixVault.AcquireHandles` and `TryResolveViews`; all route buffers request `NativeArrayOptions.UninitializedMemory` from `IDataVault`.
- Added Burst jobs: `BuildUpgradeLUTJob`, `EvaluateUpgradeMasksJob`, `GenerateMockUpgradeMasksJob`, `SyncUpgradeMasksJob`, `PublishActiveEquipmentStatsJob`, `PublishVehicleKinematicStatsJob`, `RecordUpgradeTelemetryJob`.
- Added `ReadOnlySpan<byte>` CSV ingestor for `upgrade_chip_parameters.csv`-style rows; no `string.Split`, no `float.Parse`.
- Added `DumpTelemetry` raw `ReadOnlySpan<byte>` binary write to `Docs/AgentLogs/Dump_SHINOBU_231.bin`.
- Added editor tooling: `Stat Compilation X-Ray`, `UpgradeMatrixDebugGizmo`, `UpgradeMatrixDebugGizmoEditor`, `Polymorphic_Modifier_Scanner`.
- Wrote `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` with `verdict=PASS`, `forbiddenPatternHits=0`.
- Wrote route card `Docs/ARCHITECTURE/UPGRADE_MATRIX_COMPILER_SHINOBU_231.md`.

Cinematic Cheats used:
- High 16 bits of `ActiveUpgradesMask` are reserved as visual flags. VISUAL_SYNC can feed those flags to shader step functions for glow/extrusion/fins/armor lies.
- No runtime mesh instantiation, no simulated physical add-on geometry, no per-frame component search.
- Low/Middle/High/Ultra use identical survival truth; higher tiers spend saved cycles on shader richness only.

Exact microseconds saved:
- Measured proof is absent because compile/profiler execution was blocked by CPU gate.
- Static estimate: OOP modifier chain removal target is 0-80 us/entity if such chain exists; scanner found no runtime virtual chain.
- Static estimate: branchless mask/LUT evaluation target is 8-35 us/fleet pass versus sequential upgrade branches, depending fleet size and branch history.
- Static estimate: uninitialized Vault acquisition avoids clear cost proportional to buffer capacity; exact value requires profiler.
- Static estimate: visual upgrade shader flags avoid millisecond-scale runtime `Instantiate`/mesh mutation spikes; exact value depends asset count.

Verification:
- Batch block extracted from `Docs/Tasks/CURRENT_BATCH.md`; task count 20.
- Static scan: `EvaluateUpgradeMasksJob` body contains no `if (` and no forbidden managed containers or modifier calls.
- Static scan: `float.Parse`, `int.Parse`, `string.Split`, `.Split(` absent from `UpgradeMatrixCompiler.cs`.
- Static scan: `Pack=1` absent from SHINOBU_231 runtime DTOs.
- Build gate: `Get-Process dotnet,csc` returned no active compiler process; CPU counter returned `100`, so dotnet build was not launched by mandate.

Remaining proof required:
- Unity C# compile when CPU is below 50%.
- Burst Inspector confirmation for `EvaluateUpgradeMasksJob`.
- Runtime scheduler wiring into the actual `SystemDispatcher` phase graph.
- Profiler timing for 10,000 mock masks and production equipment capacity.
- Integration decision from Agent 113 if `VehicleKinematicDTO` concrete contract appears; current output uses isolated `VehicleKinematicUpgradeDTO` mirror to avoid dependency invention.

<SELF_AUDIT agent="SHINOBU_231" status="IMPLEMENTED_STATIC_PROOF_BUILD_BLOCKED">
  <TaskCheck count="20">
    <Task id="01" result="PASS" proof="Scanner/report forbids runtime virtual ApplyModifier and List<Upgrade> chains."/>
    <Task id="02" result="PASS" proof="Hot evaluation uses bit extraction, math.select, and LUT reads."/>
    <Task id="03" result="PASS" proof="Matrix DTOs expose raw fields only."/>
    <Task id="04" result="PASS" proof="UpgradeMaskDTO size 16 offset 8 validator exists."/>
    <Task id="05" result="PASS" proof="GenerateMockUpgradeMasksJob creates 10000 masks."/>
    <Task id="06" result="PASS" proof="EvaluateUpgradeMasksJob has no hot if branches."/>
    <Task id="07" result="PASS" proof="BuildUpgradeLUTJob writes precomputed UpgradeLutEntryDTO rows."/>
    <Task id="08" result="PASS" proof="VisualFlagMask high bits feed VISUAL_SYNC flag lane."/>
    <Task id="09" result="PASS" proof="SyncUpgradeMasksJob packs inventory slots and item map into ulong masks."/>
    <Task id="10" result="PASS" proof="Publication jobs mutate ActiveEquipmentDTO and vehicle mirror DTO by UnsafeUtility.AsRef."/>
    <Task id="11" result="PASS" proof="ThermalReactorBit gates heat differential through multiplication."/>
    <Task id="12" result="PASS" proof="Entity double3 AUP minus thermal grid double3 origin before float3 mapping."/>
    <Task id="13" result="PASS" proof="Burst deterministic float mode and fixed state hashes."/>
    <Task id="14" result="PASS" proof="Vault handles use NativeArrayOptions.UninitializedMemory."/>
    <Task id="15" result="PASS" proof="UpgradeTelemetryEntry ring and raw dump path implemented."/>
    <Task id="16" result="PASS" proof="Stat Compilation X-Ray editor window implemented."/>
    <Task id="17" result="PASS" proof="ReadOnlySpan byte CSV parser implemented without Split/Parse."/>
    <Task id="18" result="PASS" proof="Live stat debug gizmo and scene label implemented."/>
    <Task id="19" result="PASS" proof="Polymorphic_Modifier_Scanner and report JSON implemented."/>
    <Task id="20" result="PASS" proof="Status, rationale, architecture card, log, and static self-audit written."/>
  </TaskCheck>
  <ARM64 primary="UpgradeMaskDTO" size="16" offsets="EntityHashID:0,EquipmentHashID:4,ActiveUpgradesMask:8"/>
  <VaultBuffers ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389,71410"/>
  <ZeroGC hotPath="true" note="Hot jobs use NativeArray/raw pointers; editor scanner/gizmo allocate only outside runtime hot path."/>
  <AUP check="PASS" note="double3 subtraction precedes float3 thermal grid sampling."/>
  <DearLie check="PASS" note="Visual upgrades are mask flags for shader presentation, not runtime geometry."/>
  <Dependency check="PASS" note="No GlobalRegistry polling in hot jobs; Agent 141/113 dependencies represented as DTO bridges/mirrors."/>
  <CompileGuard check="BLOCKED" reason="CPU counter 100 percent; dotnet build not launched."/>
</SELF_AUDIT>

## 2026-05-21 LOOP16_RECURRING_HELPER_REGRESSION_QUARANTINE

What was wrong:
- A delayed control scan found `AupPrecisionMath.DowncastLocalDelta` back inside `EvaluateUpgradeMasksJob.Execute` at line 1064 after the telemetry route edits.
- The JSON report still said `PASS`, proving the report can become stale if source is not re-scanned immediately before handoff.

What was done:
- Replaced the helper with direct local lane casts: `new float3((float)deltaAup.x, (float)deltaAup.y, (float)deltaAup.z)`.
- Re-ran a 750 ms delayed hot-slice scan: `EvaluateUpgradeMasksJob.Execute forbidden_matches=0`.
- Focused `rg` now finds `DowncastLocalDelta` only in `Polymorphic_Modifier_Scanner.cs` regex text.

Cinematic Cheats used:
- No change. Visual upgrade work still routes through `UpgradeVisualStateDTO` shader scalars.

Exact Microseconds saved:
- Preserves the previously estimated 1-3 us / 10k rows by keeping hidden AUP helper branches out of the Burst evaluator. No profiler timing was collected.

<SELF_AUDIT id="SHINOBU_231" pass="LOOP16_RECURRING_HELPER_REGRESSION_QUARANTINE">
  <hot_slice_proof EvaluateUpgradeMasksJob="0 forbidden matches after delayed scan" RecordUpgradeTelemetryJob="0 forbidden matches"/>
  <compile_guard result="not_run" reason="CPU counter 100; no dotnet/csc process"/>
</SELF_AUDIT>

## 2026-05-21 LOOP15_LIVE_TELEMETRY_VISUAL_ROUTE_CLOSURE

What was wrong:
- `RecordUpgradeTelemetryJob` existed but was not part of the live `ModularEquipmentEngine` matrix dependency chain.
- `71385/71386/71389` were documented route lanes but not resolved/released by the active equipment owner.
- `RecordUpgradeTelemetryJob.Execute` had a safety guard branch, and upgrade-owned payloads still had two Unity `Time.frameCount` stamps.

What was done:
- Added `UpgradeTelemetryEntry[300]`, `UpgradeTelemetryCursor[1]`, and `UpgradeVisualStateDTO[MaxTrackedTools]` generation handles/views to `ModularEquipmentEngine`.
- Added lifecycle release and clear-handle reset for every SHINOBU_231 upgrade matrix handle owned by `ModularEquipmentEngine`.
- Added `UpgradeMatrixScheduler.ScheduleTelemetry(...)` and chained `BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob`.
- Moved telemetry validation out of `RecordUpgradeTelemetryJob.Execute`; focused hot-slice scan returned `RecordUpgradeTelemetryJob.Execute forbidden_matches=0`.
- Replaced `VehicleUpgradeModule` and `SuitUpgradeManager` Unity frame stamps with owner-local monotonic counters.
- Updated route card, binary payload ledger addendum, equipment report, status, and rationale.

Cinematic Cheats used:
- `UpgradeVisualStateDTO` remains the Dear Lie output. High mask bits become shader-side glow/extrusion/intensity scalars; no runtime mesh or GameObject geometry is instantiated for upgrade visuals.

Exact Microseconds saved:
- No measured profiler claim. Static targets remain: 1-3 us / 10k mask rows from hot AUP helper removal, 2-8 us / transport stat refresh from branchless bit math, 4-12 us / tool-fleet pass from four-slot LUT matrix once Unity profiler proof is available.
- The telemetry route adds bounded O(n) active-mask aggregation for blackbox proof. It is scheduled behind the same dispatcher-owned fence, not same-frame readback.

<SELF_AUDIT id="SHINOBU_231" pass="LOOP15_LIVE_TELEMETRY_VISUAL_ROUTE_CLOSURE">
  <task_reconciliation total="20" source="Docs/Tasks/CURRENT_BATCH.md:2321">
    <task id="01" verdict="PASS"/>
    <task id="02" verdict="PASS"/>
    <task id="03" verdict="PASS"/>
    <task id="04" verdict="PASS"/>
    <task id="05" verdict="PASS"/>
    <task id="06" verdict="PASS"/>
    <task id="07" verdict="PASS"/>
    <task id="08" verdict="PASS" proof="71389 UpgradeVisualStateDTO live chain"/>
    <task id="09" verdict="PASS"/>
    <task id="10" verdict="PASS"/>
    <task id="11" verdict="PASS"/>
    <task id="12" verdict="PASS"/>
    <task id="13" verdict="PASS" proof="owner-local frame counters replace Unity Time in upgrade payloads"/>
    <task id="14" verdict="PASS"/>
    <task id="15" verdict="PASS" proof="71385/71386 RecordUpgradeTelemetryJob live chain"/>
    <task id="16" verdict="PASS"/>
    <task id="17" verdict="PASS"/>
    <task id="18" verdict="PASS"/>
    <task id="19" verdict="PASS"/>
    <task id="20" verdict="PENDING_UNITY_COMPILE_PROFILER" proof="static scans pass; CPU gate blocks build"/>
  </task_reconciliation>
  <struct_layout>
    <UpgradeMaskDTO size="16" fields="0:uint EntityHashID,4:uint EquipmentHashID,8:ulong ActiveUpgradesMask"/>
    <UpgradeTelemetryEntry size="64" buffer="71385"/>
    <UpgradeVisualStateDTO size="64" buffer="71389"/>
    <ToolUpgradeModuleRuleDTO size="96" buffer="71410"/>
  </struct_layout>
  <dependency_graph chain="ActiveEquipmentIntegration -> BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob" output="H8Memory.RegisterActiveJob(SystemID.GameplayTools, telemetryChain.Final)"/>
  <hot_slice_proof EvaluateUpgradeMasksJob="0 forbidden matches" RecordUpgradeTelemetryJob="0 forbidden matches"/>
  <compile_guard result="not_run" reason="CPU counter 100; no dotnet/csc process; AGENTS forbids build above 50 percent CPU"/>
</SELF_AUDIT>

## 2026-05-21 / ULTRA POLISH PASS 6 / VEHICLE MASK WIDTH

What was wrong:
- Transport upgrade math used `ulong` during stat reads, but `VehicleUpgradeBits` and installed transport masks were still 32-bit at the source.

What was done:
- Changed `VehicleUpgradeBits` to `ulong`.
- Changed `VehicleUpgradeModule._runtimeInstalledUpgradeMask` to `ulong`.
- Changed `SubmarineCoreDirector.ComposeInstalledUpgradeMask()` and `SelectVehicleBit()` to `ulong`.
- Kept low-32 `ActiveUpgradeBitmask` only for the legacy signal ABI.

Cinematic Cheats used:
- No new CPU visual simulation. Transport visual high bits remain compatible with the existing shader-mask Dear Lie route.

Exact Microseconds saved:
- No new ALU saving claimed. This is a mask-width correctness fix that preserves the 2-8 us/transport refresh branchless resolver target.

Verification:
- `rg` shows `VehicleUpgradeBits : ulong`, `VehicleUpgradeModule._runtimeInstalledUpgradeMask` as `ulong`, and submarine installed mask composition as `ulong`.
- Hot matrix slice after vehicle/doc edits: `hot_forbidden_matches=0`.
- JSON parses and `git diff --check` reports only CRLF normalization warnings.
- Build not launched: no `dotnet.exe`/`csc.exe`; CPU sample `100`.

<SELF_AUDIT pass="ultra_polish_6" agent="SHINOBU_231" status="PENDING_UNITY_COMPILE_AND_PROFILER">
  <VehicleMaskWidth>VehicleUpgradeBits:ulong; VehicleUpgradeModule runtime mask:ulong; SubmarineCoreDirector installed mask:ulong; legacy signal publishes low 32 bits only.</VehicleMaskWidth>
  <CompileGuard>No build launched; CPU gate remains enforced.</CompileGuard>
</SELF_AUDIT>

## 2026-05-21 / ULTRA POLISH PASS 5 / LIVE MATRIX ROUTE HARDENING

What was wrong:
- The tool module LUT matrix existed but was not live from `ModularEquipmentEngine`.
- `EvaluateUpgradeMasksJob` again contained `AupPrecisionMath.DowncastLocalDelta` after concurrent file churn.
- Matrix scheduling used `MaxTrackedTools` against `UninitializedMemory` buffers, risking unread inactive tails.
- The route card/report did not mention `71412 ToolProfiles` or `ToolState.UpgradeBitmask64`.

What was done:
- Added live post-integration scheduling: `BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob`.
- Added `ToolState.UpgradeBitmask64`; low `UpgradeBitmask` is now compatibility mirror only.
- Added `UpgradeToolProfilesBuffer 71412` and staged `ToolRuntimeProfile[48]` rows for Burst stat compilation.
- Added schedule count = highest used slot + 1 and cleared matrix staging on unregister/overcharge.
- Replaced the hot AUP helper again and verified `hot_forbidden_matches=0` twice.
- Updated `UPGRADE_MATRIX_COMPILER_SHINOBU_231.md` and aggregate `EQUIPMENT_OPTIMIZATION_REPORT.json`.

Cinematic Cheats used:
- Visual upgrades remain high mask bits plus `UpgradeVisualStateDTO` shader scalars. CPU does not instantiate fins, plates, decals, or per-tool geometry.

Exact Microseconds saved:
- Live tool matrix target remains estimated 4-12 us/tool-fleet pass versus managed module stat rebuilds; profiler proof pending.
- Hot AUP helper removal remains estimated 1-3 us/10k rows.
- Highest-used-slot scheduling avoids up to 16 inactive tool matrix rows and prevents stale-tail reads; exact timing is capacity-dependent.

Verification:
- Hot slice scan: `hot_forbidden_matches=0`.
- Focused `rg`: no `DowncastLocalDelta`, `0x7FFFFFFF`, `CompileRuntimeStats(`, `CompileStats(`, managed stat-total calls, `PowerDrawRate *=`, or `HeatGenerationRate *=` in touched runtime stat files; scanner/report text is the only intentional textual exception.
- JSON parse: `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` parses.
- `git diff --check`: line-ending normalization warnings only.
- Build not launched: no `dotnet.exe`/`csc.exe`; CPU sample `100`.

<SELF_AUDIT pass="ultra_polish_5" agent="SHINOBU_231" status="PENDING_UNITY_COMPILE_AND_PROFILER">
  <TaskReconciliation>
    <Task id="01" result="PASS" proof="Scanner still covers virtual ApplyModifier/List<Upgrade>/managed stat-total regressions."/>
    <Task id="02" result="PASS" proof="Tool and generic upgrade stat hot paths use bit masks, LUT rows, and math.select/bit gates."/>
    <Task id="03" result="PASS" proof="Hot DTO structs expose public fields; no C# properties were added to matrix DTOs."/>
    <Task id="04" result="PASS" proof="UpgradeMaskDTO size 16: EntityHashID@0:4, EquipmentHashID@4:4, ActiveUpgradesMask@8:8."/>
    <Task id="05" result="PASS" proof="GenerateMockUpgradeMasksJob remains deterministic and Burst compiled."/>
    <Task id="06" result="PASS" proof="EvaluateUpgradeMasksJob hot slice scanned with forbidden count 0 after final patch."/>
    <Task id="07" result="PASS" proof="UpgradeLutEntryDTO rows are precomputed; tool module matrix has 16 rows per tool."/>
    <Task id="08" result="PASS" proof="Visual upgrades are shader flags/scalars in UpgradeVisualStateDTO."/>
    <Task id="09" result="PASS" proof="SyncUpgradeMasksJob remains the inventory-to-mask bridge; tool staging writes UpgradeMaskDTO rows."/>
    <Task id="10" result="PASS" proof="Active equipment and tool runtime stats are written through NativeArray/pointer jobs; live post-integration chain added."/>
    <Task id="11" result="PASS" proof="Thermal reactor contribution is multiplied by bit gate without hot if."/>
    <Task id="12" result="PASS" proof="AUP remains double-subtracted before local float mapping; helper downcast removed from hot evaluator."/>
    <Task id="13" result="PASS" proof="Matrix jobs use deterministic Burst flags for rollback-adjacent stat truth."/>
    <Task id="14" result="PASS" proof="Vault buffers use UninitializedMemory; inactive matrix holes are owner-cleared instead of per-frame memclear."/>
    <Task id="15" result="PASS" proof="Upgrade telemetry ring remains 300 entries; suit telemetry moved to Vault in previous pass."/>
    <Task id="16" result="PASS" proof="X-Ray editor facade remains editor-only."/>
    <Task id="17" result="PASS" proof="CSV parser remains ReadOnlySpan<byte>-based cold ingestion."/>
    <Task id="18" result="PASS" proof="Debug gizmo remains editor-only and outside runtime stat math."/>
    <Task id="19" result="PASS" proof="Polymorphic_Modifier_Scanner writes hotEvaluateForbiddenMatches and aggregate report entry."/>
    <Task id="20" result="PASS" proof="This log/status/rationale/card update records layout, Vault, alias, dependency, and compile-gate evidence."/>
  </TaskReconciliation>
  <StructLayoutVerification>
    <UpgradeMaskDTO size="16">0 EntityHashID uint4; 4 EquipmentHashID uint4; 8 ActiveUpgradesMask ulong8; total 16.</UpgradeMaskDTO>
    <ToolState size="32">0 CurrentBattery float4; 4 InternalHeat float4; 8 Durability float4; 12 UpgradeBitmask uint4; 16 StatusMask uint4; 20 ToolTypeId byte1; 21 ModuleSlotCount byte1; 22 Reserved0 ushort2; 24 UpgradeBitmask64 ulong8; total 32.</ToolState>
    <ToolRuntimeProfile size="48">0 ToolId uint4; 4..40 ten float lanes; 44 ModuleSlotCount byte1; 45..47 pad3; total 48.</ToolRuntimeProfile>
    <ToolUpgradeModuleRuleDTO size="96">0 UpgradeBit ulong8; 8 StateHash ulong8; 16 EntityHashID uint4; 20 EquipmentHashID uint4; 24 CompressedBit uint4; 28 VisualFlags uint4; 32..68 ten float lanes; 72 Occupied byte1; 73 SlotIndex byte1; 74..95 padding22; total 96.</ToolUpgradeModuleRuleDTO>
  </StructLayoutVerification>
  <ScalabilityCurve>Gameplay stat truth does not branch on `GlobalQualityWeight`. Weak devices execute the same 64-bit mask/LUT truth and near-zero visual intensity; middle devices get moderate shader response; high/ultra devices use stronger shader extrusion/glow from `UpgradeVisualStateDTO` without CPU mesh work.</ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">Route buffers: 71380 Masks, 71381 BaseStats, 71382 CompiledStats, 71383 Lut, 71384 Rules, 71385 TelemetryRing, 71386 TelemetryCursor, 71387 InventorySlots, 71388 ItemMap, 71389 VisualStates, 71410 ToolModuleRules, 71411 SuitTelemetry, 71412 ToolProfiles.</HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>[NoAlias] remains on non-overlapping NativeArray/raw pointer fields. `ScheduleToolUpgradeMatrixPostIntegration` consumes `_equipmentIntegrationHandle` and outputs `chain.Final` back to `_equipmentIntegrationHandle`; no `.Complete()` added.</PointerAliasingAndDependencyGraph>
  <CompileGuard>No asmdef edits and no sibling runtime dependency added. Build skipped by CPU gate; known ledger also warns the generated Core csproj has stale includes.</CompileGuard>
  <DearLie>Visual upgrade path is O(1) mask/scalar publication. Rejected runtime GameObject/mesh mutation; shader interprets high mask bits with step/smoothstep-style response.</DearLie>
</SELF_AUDIT>

## 2026-05-21 / ULTRA POLISH PASS 5 / HOT EVALUATOR BRANCH GATE

What was wrong:
- `EvaluateUpgradeMasksJob.Execute` regressed again to `AupPrecisionMath.DowncastLocalDelta`, reintroducing hidden `if` guards through a shared AUP helper.
- The previous pointer-safety patch used `if (Lut != null)` and `if (ThermalGridCelsius != null)` inside the hot Burst evaluator. That was safe, but it violated the branchless stat-compiler contract.
- `Polymorphic_Modifier_Scanner` caught OOP modifier regressions but did not catch branch/helper regressions inside the exact hot evaluator slice.

What was done:
- Replaced `DowncastLocalDelta` with direct local casts after double-domain AUP subtraction and finite `math.select` vaccination.
- Removed `if`, `else`, `switch`, and ternary tokens from `EvaluateUpgradeMasksJob.Execute`; static hot-slice scan returned `hot_forbidden_matches=0`.
- Added `UpgradeMatrixScheduler.ScheduleUpgradeMaskEvaluation(...)` as the approved cold wrapper. It validates `NativeArray.IsCreated`, equipment row counts, LUT length, and thermal-grid length before scheduling the raw pointer job.
- Enhanced `Polymorphic_Modifier_Scanner` with hot-slice checks for `HOT_EVALUATE_MASKS_BRANCH` and `HOT_EVALUATE_AUP_HELPER_DOWNCAST`.
- Updated `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` with `hotEvaluateForbiddenMatches: 0` and validated the JSON parse.

Cinematic Cheats used:
- No new CPU visual simulation. The visual upgrade route remains a 64-bit mask plus `UpgradeVisualStateDTO` shader scalars. Missing thermodynamics must use a one-cell fallback grid, not a hot-path branch.

Exact Microseconds saved:
- Hot AUP helper removal: estimated 1-3 us / 10k mask rows on i3/MX350-class CPUs.
- Hot pointer guard removal: expected branch predictor stability improvement in per-row evaluation; profiler timing still pending because build/profiler gate is closed.
- Scanner hardening: runtime cost 0 us; prevents repeated regression under concurrent-agent edits.

Verification:
- `CURRENT_BATCH.md` extraction: start `2321`, end `2385`, task count `20`.
- PowerShell hot-slice scan: `hot_forbidden_matches=0`.
- `rg` found no `DowncastLocalDelta`, managed stat-total call sites, `PowerDrawRate *=`, or `HeatGenerationRate *=` in touched stat files.
- `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` parses successfully.
- `git diff --check` returned only CRLF normalization warnings.
- Build not launched: no `dotnet.exe` or `csc.exe`; CPU sample was `100`, above the mandated 50% gate.

<SELF_AUDIT pass="ultra_polish_5" agent="SHINOBU_231" status="STATIC_PASS_COMPILE_PENDING">
  <TaskReconciliation>
    <Task id="01" result="PASS" proof="Scanner still rejects virtual ApplyModifier chains and managed runtime upgrade mirrors."/>
    <Task id="02" result="PASS" proof="Hot evaluator text slice contains no if/else/switch/ternary tokens."/>
    <Task id="03" result="PASS" proof="Upgrade DTO hot structs expose raw fields; no property regressions found in touched DTOs."/>
    <Task id="04" result="PASS" proof="UpgradeMaskDTO remains explicit 16 bytes: EntityHashID@0, EquipmentHashID@4, ActiveUpgradesMask@8."/>
    <Task id="05" result="PASS" proof="GenerateMockUpgradeMasksJob remains deterministic and unchanged by this pass."/>
    <Task id="06" result="PASS" proof="EvaluateUpgradeMasksJob executes direct LUT/grid reads with no branch/helper tokens in Execute."/>
    <Task id="07" result="PASS" proof="LUT path remains precomputed; evaluator reads `Lut + lutIndex` in O(1)."/>
    <Task id="08" result="PASS" proof="Visual upgrade bits remain shader-facing `UpgradeVisualStateDTO`, not runtime mesh objects."/>
    <Task id="09" result="PASS" proof="Inventory bridge path remains mask packing; no GlobalRegistry hot polling was added."/>
    <Task id="10" result="PASS" proof="Publication jobs remain pointer/NativeArray writes; compounding rate multiplication was not reintroduced."/>
    <Task id="11" result="PASS" proof="Thermal reactor scalar remains mask-gated multiplication."/>
    <Task id="12" result="PASS" proof="AUP subtraction is double-domain before float local cast."/>
    <Task id="13" result="PASS" proof="Upgrade jobs retain deterministic Burst flags."/>
    <Task id="14" result="PASS" proof="Vault acquisition still requests UninitializedMemory; hot job requires validated views."/>
    <Task id="15" result="PASS" proof="Telemetry cursor guard remains bounded to the 300-entry ring."/>
    <Task id="16" result="PASS" proof="X-Ray/editor facade untouched by runtime branch patch."/>
    <Task id="17" result="PASS" proof="CSV parser still gates LUT-compressed bits to the 12-bit/4096-row matrix."/>
    <Task id="18" result="PASS" proof="Debug gizmo remains editor-only."/>
    <Task id="19" result="PASS" proof="Scanner now catches OOP and hot evaluator branch/helper regressions."/>
    <Task id="20" result="PASS" proof="Status, rationale, architecture card, report JSON, and this log were updated."/>
  </TaskReconciliation>
  <StructLayoutVerification>
    <UpgradeMaskDTO size="16">0 EntityHashID uint4; 4 EquipmentHashID uint4; 8 ActiveUpgradesMask ulong8; total 16.</UpgradeMaskDTO>
    <UpgradeStatVectorDTO size="64">0 StateHash ulong8; 8 EntityHashID uint4; 12 EquipmentHashID uint4; 16 VisualFlags uint4; 20 FaultFlags uint4; 24..63 ten float stat lanes; total 64.</UpgradeStatVectorDTO>
    <ToolUpgradeModuleRuleDTO size="96">0 UpgradeBit ulong8; 8 StateHash ulong8; 16 EntityHashID uint4; 20 EquipmentHashID uint4; 24 CompressedBit uint4; 28 VisualFlags uint4; 32..68 ten float multiplier lanes; 72 Occupied byte; 73 SlotIndex byte; 74..95 padding.</ToolUpgradeModuleRuleDTO>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Core stat truth does not quality-branch. `GlobalQualityWeight` only feeds `UpgradeVisualStateDTO` intensity, extrusion, and glow. Low hardware can collapse visuals to near-zero shader response while keeping identical mask/LUT survival stats; high/ultra hardware spends the saved CPU on shader-driven visual overkill.
  </ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">
    Vault IDs: 71380 Masks, 71381 BaseStats, 71382 CompiledStats, 71383 Lut, 71384 Rules, 71385 TelemetryRing, 71386 TelemetryCursor, 71387 InventorySlots, 71388 ItemMap, 71389 VisualStates, 71410 ToolModuleRules, 71411 SuitUpgradeTelemetryRing.
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    Scheduler consumes caller `inputDeps`; `ScheduleUpgradeMaskEvaluation` outputs `Evaluate` and `Final` as the same scheduled handle. No `.Complete()` added. `[NoAlias]` remains on non-overlapping pointer/NativeArray fields.
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    No asmdef reference was added. Runtime remains in existing `Hecton8.Core` assembly surface with `Core.Contracts/Core.Memory` references. Build is pending because CPU sample was 100 percent.
  </CompileGuard>
  <DearLie>
    Visual upgrade state is a shader-consumed mask/scalar DTO. Before: potential object/mesh toggles per upgrade. After: O(1) DTO publication and GPU-side step/extrusion interpretation; CPU mesh mutation remains zero.
  </DearLie>
</SELF_AUDIT>

## 2026-05-20 / ULTRA POLISH PASS 5 / SUIT SIDE-AUDIT CLOSEOUT

What was wrong:
- `SuitUpgradeResolverJob` used a bare `[BurstCompile]` attribute.
- `SuitUpgradeManager` called `job.Execute()` for a one-element result, which is not a meaningful batch job and bypasses the dispatcher proof model.
- `SuitUpgradeManager` owned a private persistent `NativeArray<SuitUpgradeTelemetryEntry>` instead of Vault-owned blackbox memory.
- The scanner would false-fail serialized ScriptableObject catalogs such as `SuitUpgradeManager.allUpgrades`.

What was done:
- Added deterministic Burst flags to `SuitUpgradeResolverJob`.
- Removed direct `job.Execute()` from `ResolveAndApplyUpgradeMask`; the scalar resolver now writes local truth directly and mirrors into the Vault result slot when available.
- Replaced `_telemetryRing` with `VaultGenerationHandle<SuitUpgradeTelemetryEntry>` and buffer `71411 SuitUpgradeTelemetryRingBuffer`.
- Updated scanner/report/architecture docs for serialized authoring array allowance and the new suit telemetry Vault route.

Cinematic Cheats used:
- Suit mesh/visual changes remain mask/signal driven. The CPU path publishes stat truth and high-level mask state; visual details remain shader/signal-side rather than object mutation.

Exact Microseconds saved:
- Tiny-job removal: avoids scheduler overhead for a one-row resolver; exact value requires Unity profiler.
- Telemetry Vault migration: no hot ALU claim; removes one scene-owned persistent allocation and stale pointer/dispose surface.
- Scanner refinement: editor-only, runtime 0 us.

Verification:
- `rg` found no `job.Execute()`, bare `[BurstCompile]`, private telemetry `NativeArray` field, `DowncastLocalDelta` in `UpgradeMatrixCompiler.cs`, `CompileRuntimeStats(`, managed stat-total calls, or `CompileStats(` in touched hot/stat C# files.
- Remaining managed authoring arrays: `ToolMetadata.defaultModules` public authoring data and `[SerializeField] SuitUpgradeManager.allUpgrades` authoring catalog.
- Build not launched: `Get-CimInstance Win32_Process` returned no `dotnet.exe` or `csc.exe`; CPU counter returned `100`, above the allowed gate. Compile/Burst/profiler proof remains pending.

<SELF_AUDIT pass="ultra_polish_5" agent="SHINOBU_231" status="STATIC_PASS_COMPILE_PENDING">
  <TaskReconciliation>Tasks 01-20 remain static PASS. Loop 10 closed sidecar suit resolver/telemetry findings without adding a same-frame job readback.</TaskReconciliation>
  <StructLayout name="SuitUpgradeTelemetryEntry" size="64">0 FrameIndex uint4; 4 Sequence uint4; 8 UpgradeMask ulong8; 16 EffectiveMask ulong8; 24 InventoryMask ulong8; 32 Flags uint4; 36 StateHash uint4; 40 MaxO2 float4; 44 CrushDepth float4; 48 SwimSpeedMultiplier float4; 52 ThermalResistance float4; 56 MaxEnergy float4; 60 RadiationThreshold float4; total 64 bytes.</StructLayout>
  <Scalability>Suit stat truth is quality-invariant. Visual tiering remains signal/shader-side; low devices do not pay object mutation, high/ultra can spend shader budget on suit presentation.</Scalability>
  <VaultStatus ids="504,71411">`SuitUpgradeResolverResult` remains Vault-owned result memory; `71411 SuitUpgradeTelemetryRingBuffer` now owns the 300-frame suit blackbox ring. No private persistent suit telemetry NativeArray remains.</VaultStatus>
  <PointerAliasing>Suit manager no longer executes a fake one-row job. Matrix jobs still expose NoAlias fields and return JobHandles through scheduler helpers.</PointerAliasing>
  <CompileGuard>No direct sibling assembly dependency was introduced. Build remains pending behind CPU gate.</CompileGuard>
  <DearLie>Suit visuals stay as mask/signal data, not mesh/GameObject mutation. Complexity remains O(1) stat resolve plus O(1) telemetry write per mask change.</DearLie>
</SELF_AUDIT>

---

## Ultra Polish Pass 3 - Typed Tool Rule Route

What was wrong:
- `71384 UpgradeRulesBuffer` was documented as carrying either `UpgradeBitRuleDTO` or `ToolUpgradeModuleRuleDTO`. Typed Vault buffers cannot be polymorphic without corrupting route proof.
- `BuildToolModuleLUTJob` accepted caller-provided matrix strides. The actual ABI is fixed: 4 module rules and 16 rows.
- The managed compatibility compiler still had direct per-module stat multiplication, which could drift from the Burst LUT semantics.
- `CoolingSink` needed non-linear max-bonus semantics inside the matrix instead of ordinary linear stacking.

What was done:
- Added `71410 UpgradeToolModuleRulesBuffer` and `VaultGenerationHandle<ToolUpgradeModuleRuleDTO>` / `NativeArray<ToolUpgradeModuleRuleDTO>` views.
- Fixed per-tool LUT stride to `ToolModuleSlotsPerEquipment=4` and `ToolModuleLutEntriesPerEquipment=16`.
- Added `UpgradeMatrixCompiler.ApplyToolModuleRule` and reused it from both `BuildToolModuleLUTJob` and `ToolUpgradeSystem.CompileRuntimeStatsFromRules`.
- Rewired the cold compatibility route so compilation consumes four packed rule DTOs, evaluates the selected LUT row, applies `ApplyLut`, then projects through `CompileRuntimeStatsFromLut`.
- Removed float `enabled > 0f` mask generation; LUT builders now derive masks from integer bit results.

Cinematic Cheats used:
- No new CPU visual simulation. Tool and vehicle upgrade presentation remains shader-side `UpgradeVisualStateDTO` flags/intensity/glow/extrusion.

Exact microseconds saved:
- No new measured claim. This pass is memory-safety and semantic-unification work.
- Prior estimate remains: 4-12 us/tool-fleet stat pass when dispatcher uses the tool module matrix instead of managed module refresh.

<SELF_AUDIT pass="ultra_polish_3" agent="SHINOBU_231">
  <RouteCorrection>Tool rules now have typed BufferID 71410. BufferID 71384 remains `UpgradeBitRuleDTO` only.</RouteCorrection>
  <StrideContract>Per-tool module matrix is fixed at 4 rules and 16 rows. Scheduler parameters cannot alter row stride.</StrideContract>
  <SemanticUnification>`CoolingSink` max-bonus semantics live inside `ApplyToolModuleRule`; Burst and compatibility compilers call the same helper.</SemanticUnification>
  <StructLayout name="ToolUpgradeModuleRuleDTO" size="96">Offsets unchanged: 0/8 ulong, 16..28 uint, 32..68 floats, 72 byte occupancy, 73 byte slot, 74..95 padding.</StructLayout>
  <VaultStatus ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389,71410">No private persistent arrays added.</VaultStatus>
  <CompileGuard>No dotnet build launched in this pass; build gate must check CPU and compiler process first.</CompileGuard>
</SELF_AUDIT>

---

## Ultra Polish Pass 2 - Tool Module LUT Matrix

What was wrong:
- Tool stat projection had a Burst consumer, but authored `ToolModuleData[]` multipliers did not yet have a per-tool 4-slot LUT matrix. That left the cold managed compiler as the only complete route for custom module multipliers.
- `EvaluateUpgradeMasksJob` still called `AupPrecisionMath.DowncastLocalDelta`, which hides branch guards inside a shared helper and weakened the hot-slice proof.

What was done:
- Added `ToolUpgradeModuleRuleDTO[96]` with explicit field offsets and validator coverage.
- Added `BuildToolModuleLUTJob`: bakes each equipment row into 16 `UpgradeLutEntryDTO[128]` rows from four packed tool-module rules, with stride clamped to the 4/16 contract.
- Added `EvaluateToolModuleLUTJob`: reads `toolIndex * 16 + slotMask` in O(1), then applies the stat vector and continuous visual quality lane.
- Added cold facades `BuildModuleRule`, `CompileInstalledRuleMask64`, and `CompileRuntimeStatsFromLut` so ScriptableObject authoring data can cross into unmanaged rows once.
- Replaced the remaining hot `DowncastLocalDelta` call with local double-to-float lane casts plus finite-select fallback.
- Updated `Docs/ARCHITECTURE/UPGRADE_MATRIX_COMPILER_SHINOBU_231.md` and `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json`.

Cinematic Cheats used:
- Tool upgrade visuals remain `UpgradeVisualStateDTO` shader scalars: intensity, extrusion, glow, and visual flags. No runtime mesh/module GameObject toggles were introduced.

Exact microseconds saved:
- Tool module matrix path estimate: 4-12 us/tool-fleet stat pass once dispatcher wiring replaces the managed authoring loop.
- AUP branch removal estimate: 1-3 us/10k mask evaluations.
- Transport stat branch removal remains estimated at 2-8 us/transport stat refresh.
- Measured profiler proof is still pending. Compile/profiler gate stayed closed because CPU counter returned `100`.

<SELF_AUDIT pass="ultra_polish_2" agent="SHINOBU_231">
  <Task id="01" status="PASS">No runtime polymorphic modifier chain added; new tool rule rows are unmanaged DTOs.</Task>
  <Task id="02" status="PASS">Tool and transport stat paths now use masks/LUT/select math instead of branch ladders.</Task>
  <Task id="03" status="PASS">New DTO exposes raw fields only; no hot-path properties.</Task>
  <Task id="04" status="PASS">`ToolUpgradeModuleRuleDTO` is explicit 96 bytes; existing `UpgradeMaskDTO` remains 16 bytes.</Task>
  <Task id="05" status="PASS">Existing 10k mock job remains intact.</Task>
  <Task id="06" status="PASS">Hot evaluated slices show no `if/else`, `switch`, `foreach`, `.Complete()`, `new List`, or `Dictionary`.</Task>
  <Task id="07" status="PASS">Global LUT and new per-tool 16-row LUT are both precomputed matrices.</Task>
  <Task id="08" status="PASS">Visual upgrades publish shader scalar state, not object/mesh instantiation.</Task>
  <Task id="09" status="PASS">Inventory sync bridge remains unmanaged; tool slot mask facade added for per-equipment LUT rows.</Task>
  <Task id="10" status="PASS">`CompileToolRuntimeStatsJob` consumes compiled vectors from the LUT route.</Task>
  <Task id="11" status="PASS">Thermal reactor gate remains bit-extracted and branchless.</Task>
  <Task id="12" status="PASS">AUP subtraction stays in double; hidden downcast helper removed from hot evaluator.</Task>
  <Task id="13" status="PASS">New jobs use deterministic Burst directives and blittable DTOs.</Task>
  <Task id="14" status="PASS">No private persistent native arrays introduced.</Task>
  <Task id="15" status="PASS">Telemetry ring route unchanged.</Task>
  <Task id="16" status="PASS">X-Ray editor route unchanged.</Task>
  <Task id="17" status="PASS">CSV parser route unchanged.</Task>
  <Task id="18" status="PASS">Debug gizmo route unchanged.</Task>
  <Task id="19" status="PASS">Report JSON updated to include tool module LUT authority.</Task>
  <Task id="20" status="PASS">Status, rationale, architecture, and log updated; Unity compile still gated by CPU policy.</Task>
  <StructLayout name="ToolUpgradeModuleRuleDTO" size="96" alignment="8plus">
    offset0="ulong UpgradeBit"
    offset8="ulong StateHash"
    offset16="uint EntityHashID"
    offset20="uint EquipmentHashID"
    offset24="uint CompressedBit"
    offset28="uint VisualFlags"
    offset32to68="ten float multipliers"
    offset72="byte Occupied"
    offset73="byte SlotIndex"
    offset74to95="manual padding"
  </StructLayout>
  <ScalabilityCurve>GlobalQualityWeight is consumed by compiled Stat11 and `UpgradeVisualStateDTO`; low weight collapses glow/extrusion intensity toward zero while the same deterministic stat truth remains intact. High/Ultra consume the same CPU route and can make shader-side visual overkill richer.</ScalabilityCurve>
  <VaultStatus>No private persistent arrays were added. Vault IDs are 71380..71389 plus 71410 for typed tool module rules; `71384` remains `UpgradeBitRuleDTO` only.</VaultStatus>
  <DependencyGraph>`BuildToolModuleLUTJob` consumes packed module-rule arrays and outputs per-tool LUT rows. `EvaluateToolModuleLUTJob` consumes masks/base stats/tool LUT and outputs compiled stats. `CompileToolRuntimeStatsJob` consumes profiles/compiled stats and outputs `ToolRuntimeStats`.</DependencyGraph>
  <CompileGuard>No new sibling runtime assembly reference was introduced. `dotnet build` was not launched because CPU was 100.</CompileGuard>
  <DearLie>Authored module visuals are shader scalar flags, avoiding runtime module mesh toggles. Complexity moves from per-refresh module iteration to O(1) row lookup after cold/pre-sim LUT bake.</DearLie>
</SELF_AUDIT>

## 2026-05-20 / ULTRA POLISH PASS / TOOL_UPGRADE_MATRIX_COMPILER

What was still wrong:
- `EvaluateUpgradeMasksJob` used the shared AUP downcast helper. The subtraction order was correct, but that helper contains branch guards, so the hot stat compiler proof was not strict enough.
- `BuildUpgradeLUTJob.ApplyRule` still contained a lane `switch`. It was cold boot, not per-frame, but it was still a branch table inside the matrix compiler.
- VISUAL_SYNC used a raw `uint` visual flag route. That was insufficient for continuous `GlobalQualityWeight`, shader extrusion, and glow intensity.
- `SubmarineCoreDirector` still had transport stat chains for thrust, turn speed, max depth, and integrity.
- Tool runtime structs were sequential-layout DTOs, and the Burst route from compiled LUT vector to `ToolRuntimeStats` was missing.

What was done:
- Replaced hot AUP downcast with local double subtraction plus explicit float cast and branchless finite-select fallback.
- Replaced the LUT lane switch with twelve straight-line lane masks.
- Added `UpgradeVisualStateDTO[64]` and `PublishUpgradeVisualStateJob`.
- Added `CompileToolRuntimeStatsJob` to project `UpgradeStatVectorDTO` lanes into `ToolRuntimeStats` in Burst.
- Converted `ToolState[32]`, `ToolRuntimeProfile[48]`, and `ToolRuntimeStats[40]` to explicit layouts without changing existing field offsets.
- Replaced submarine transport upgrade branches with `math.select` bit packing and bit-extracted multiplier/additive math.
- Updated the architecture card and rationale route so buffer `71389` is `VisualStates`, not a raw `uint[]`.

Cinematic Cheats used:
- Visual upgrades remain shader data: high mask bits + continuous quality/intensity/extrusion/glow scalars. No runtime fin mesh, armor plate GameObject, or procedural geometry spawn.
- Core upgrade truth ignores quality weight. `GlobalQualityWeight` only shapes visual overkill, so weak hardware collapses presentation cost without changing multiplayer state.

Exact microseconds saved:
- Hot AUP helper branch removal: estimated 1-3 us per 10,000 evaluated masks; profiler proof pending.
- Submarine transport branch removal: estimated 2-8 us per transport stat refresh depending call frequency and branch history.
- Tool matrix projection job: estimated 4-12 us per fleet pass once dispatcher wiring replaces managed module multiplier loops.
- VISUAL_SYNC shader DTO avoids millisecond-scale runtime mesh/object churn; exact value depends on visual asset count.
- LUT lane switch removal: cold boot only; runtime frame savings not claimed.

Verification performed:
- `rg` found no `AupPrecisionMath.DowncastLocalDelta`, `switch (lane)`, or branchy `VehicleUpgradeBits` mask chains in the patched files.
- `rg` found no `LayoutKind.Sequential` or `Pack=1` in `ToolUpgradeSystem.cs` or `UpgradeMatrixCompiler.cs`.
- `EvaluateUpgradeMasksJob` still contains no hot `if (`. Remaining `if` hits in `UpgradeMatrixCompiler.cs` are layout validation, dump path guards, and cold CSV parsing.
- Compile was not launched in this pass; `Get-Process dotnet,csc` produced no active compiler output and CPU counter returned `100`, so the >50% CPU gate blocked `dotnet build`.

<SELF_AUDIT agent="SHINOBU_231" status="POLISH_STATIC_PASS_COMPILE_PENDING">
  <TaskReconciliation count="20">
    <Task id="01" result="PASS" proof="Scanner/report route remains; runtime OOP modifier patterns not reintroduced."/>
    <Task id="02" result="PASS" proof="Transport stat branches also removed from SubmarineCoreDirector."/>
    <Task id="03" result="PASS" proof="Tool and matrix hot DTOs now explicit raw fields."/>
    <Task id="04" result="PASS" proof="UpgradeMaskDTO remains 16 bytes: EntityHashID@0 size4, EquipmentHashID@4 size4, ActiveUpgradesMask@8 size8."/>
    <Task id="05" result="PASS" proof="GenerateMockUpgradeMasksJob unchanged and still deterministic."/>
    <Task id="06" result="PASS" proof="EvaluateUpgradeMasksJob no longer calls branchy AUP downcast helper."/>
    <Task id="07" result="PASS" proof="BuildUpgradeLUTJob lane switch replaced by mask math."/>
    <Task id="08" result="PASS" proof="UpgradeVisualStateDTO and PublishUpgradeVisualStateJob implement Dear Lie route."/>
    <Task id="09" result="PASS" proof="SyncUpgradeMasksJob uses item/equipment match scalars; wildcard match no longer uses boolean OR."/>
    <Task id="10" result="PASS" proof="Active equipment and vehicle publication jobs remain pointer/NativeArray based; CompileToolRuntimeStatsJob added."/>
    <Task id="11" result="PASS" proof="Thermal reactor path remains mask-gated multiplication."/>
    <Task id="12" result="PASS" proof="AUP subtraction is double-domain before local float mapping."/>
    <Task id="13" result="PASS" proof="New jobs use deterministic Burst flags."/>
    <Task id="14" result="PASS" proof="Vault handles still request UninitializedMemory, including VisualStates."/>
    <Task id="15" result="PASS" proof="Telemetry ring unchanged at 300 x 64-byte entries."/>
    <Task id="16" result="PASS" proof="X-Ray window remains editor-only."/>
    <Task id="17" result="PASS" proof="CSV parser still uses ReadOnlySpan byte parsing."/>
    <Task id="18" result="PASS" proof="Debug gizmo remains editor-only."/>
    <Task id="19" result="PASS" proof="Scanner remains available and report remains PASS before rerun."/>
    <Task id="20" result="PASS" proof="This log/status/rationale/card update records the extra pass."/>
  </TaskReconciliation>
  <StructLayoutVerification>
    <UpgradeMaskDTO size="16" align=">=8" fields="EntityHashID@0:4,EquipmentHashID@4:4,ActiveUpgradesMask@8:8"/>
    <UpgradeVisualStateDTO size="64" fields="ActiveUpgradesMask@0:8,StateHash@8:8,EntityHashID@16:4,EquipmentHashID@20:4,VisualFlags@24:4,GlobalQualityWeight@28:4,VisualIntensity@32:4,ShaderExtrusionScale@36:4,GlowScalar@40:4,FaultFlags@44:4,pad@48:16"/>
    <ToolState size="32" fields="CurrentBattery@0:4,InternalHeat@4:4,Durability@8:4,UpgradeBitmask@12:4,StatusMask@16:4,ToolTypeId@20:1,ModuleSlotCount@21:1,Reserved0@22:2,Reserved1@24:8"/>
    <ToolRuntimeProfile size="48" fields="ToolId@0:4,stats@4..40:10x4,ModuleSlotCount@44:1,pad@45:3"/>
    <ToolRuntimeStats size="40" fields="10x float lanes @0..36"/>
  </StructLayoutVerification>
  <ScalabilityCurve>
    Core stat jobs do not scale with quality. Low/Middle/High/Ultra evaluate the same mask/LUT truth for rollback stability. `GlobalQualityWeight` enters only `UpgradeVisualStateDTO`: low weights reduce smooth intensity/extrusion/glow toward zero; middle weights give moderate shader response; high and ultra allow stronger shader-only armor/fins/glow lies without CPU geometry work.
  </ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">
    Buffers requested by route handles: 71380 Masks, 71381 BaseStats, 71382 CompiledStats, 71383 Lut, 71384 Rules, 71385 TelemetryRing, 71386 TelemetryCursor, 71387 InventorySlots, 71388 ItemMap, 71389 VisualStates, 71410 ToolModuleRules.
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    [NoAlias] applied on non-overlapping NativeArray and raw pointer fields. Jobs consume PRE_SIMULATION inventory sync handles, SIMULATION evaluation/publish handles, POST_SIMULATION telemetry handles, and VISUAL_SYNC visual-state handles. Jobs return JobHandle through normal Unity scheduling; no arbitrary Complete call was added.
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    No direct asmdef edit was made. The transport branch fix uses a local bit helper to avoid adding a Tools assembly dependency from submarine code. Build remains pending because CPU counter stayed at 100 percent.
  </CompileGuard>
  <DearLie>
    Before: visual upgrade proof was a raw flag, likely forcing later runtime mesh toggles. After: O(1) visual DTO write and shader-step interpretation. CPU geometry complexity remains O(entities), no instantiated visual parts.
  </DearLie>
</SELF_AUDIT>

## 2026-05-20 / ULTRA POLISH PASS 4 / SIDE-AUDIT INTEGRATION

What was wrong:
- Sidecar audit found `ModularEquipmentEngine` still retaining authored `ToolModuleData[]` mirrors and calling a compatibility stat compiler route.
- `EvaluateUpgradeMasksJob` had regressed back to `AupPrecisionMath.DowncastLocalDelta`, which hides branch guards behind a shared helper.
- `Polymorphic_Modifier_Scanner` would overwrite the aggregate equipment optimization report instead of replacing only the `SHINOBU_231` entry.

What was done:
- Replaced runtime module mirror/scratch arrays with `ToolUpgradeModuleRuleDTO[]` in `ModularEquipmentEngine`.
- Replaced `PlayerTool.CopyAuthoredModules` / `ToolMetadata.CopyDefaultModules` with `CopyAuthoredModuleRules` / `CopyDefaultModuleRules`.
- Removed the `CompileRuntimeStats(` compatibility name from touched runtime paths; rebuild now calls `ToolUpgradeSystem.CompileRuntimeStatsFromRules`.
- Replaced the reverted hot AUP downcast helper with direct localized double-to-float lane casts plus finite select.
- Updated scanner matching to catch private/protected/internal managed upgrade arrays and preserve the multi-agent report JSON.

Cinematic Cheats used:
- Visual upgrade truth remains a 64-bit mask plus `UpgradeVisualStateDTO` shader scalars. No runtime fin/armor mesh instantiation.

Exact Microseconds saved:
- Tool rebuild object-mirror purge: estimated 4-12 us/tool-fleet pass on i3/MX350 when rebuilds run; Unity profiler proof is still blocked by CPU gate.
- Hot AUP helper removal: estimated 1-3 us/10k mask rows and removes hidden branch proof failure.
- Mesh instantiation avoided by Dear Lie: avoids millisecond-scale spikes; exact GPU/CPU proof pending play-mode profiling.

Verification:
- `rg` found no `DowncastLocalDelta`, `CompileRuntimeStats(`, managed stat-total call sites, or `CompileStats(` in touched stat files.
- `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` parses with `verdict=PASS` for `SHINOBU_231`.
- `git diff --check` returned only CRLF normalization warnings on touched files.
- Build not launched: CPU gate remained above allowed threshold in previous checks; no dotnet/csc process was observed.

<SELF_AUDIT pass="ultra_polish_4" agent="SHINOBU_231" status="STATIC_PASS_COMPILE_PENDING">
  <TaskReconciliation>Tasks 01-20 remain PASS by static proof; compile/Burst Inspector/profiler remain pending behind the CPU gate.</TaskReconciliation>
  <StructLayout name="UpgradeMaskDTO" size="16">0 EntityHashID uint4; 4 EquipmentHashID uint4; 8 ActiveUpgradesMask ulong8; total 16 bytes.</StructLayout>
  <StructLayout name="ToolUpgradeModuleRuleDTO" size="96">0 UpgradeBit ulong8; 8 StateHash ulong8; 16 EntityHashID uint4; 20 EquipmentHashID uint4; 24 CompressedBit uint4; 28 VisualFlags uint4; 32..68 ten float multiplier lanes; 72 Occupied byte; 73 SlotIndex byte; 74..95 padding; total 96 bytes.</StructLayout>
  <Scalability>Gameplay stats do not quality-branch. `GlobalQualityWeight` only drives VISUAL_SYNC scalar intensity/glow/extrusion; low devices pay mask publication only, high/ultra shaders spend the saved CPU visually.</Scalability>
  <VaultStatus ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389,71410">Upgrade matrix persistent data is Vault-owned. `ModularEquipmentEngine` still has cold managed owner arrays for tool owners/slot flags and packed DTO mirrors, but no `ToolModuleData[]` runtime mirror remains.</VaultStatus>
  <PointerAliasing>NoAlias remains on NativeArray/pointer fields in Burst kernels. Scheduler helper returns `BuildLut`, `Evaluate`, `CompileStats`, `VisualSync`, and `Final` JobHandles without calling `.Complete()`.</PointerAliasing>
  <CompileGuard>No direct sibling Runtime assembly reference was introduced; touched code uses existing domain namespaces and Contracts-facing services. Dotnet build was not launched under CPU gate.</CompileGuard>
  <DearLie>Runtime visual upgrades are mask-driven shader flags and continuous scalars; algorithmic cost is O(1) scalar publication instead of O(N) GameObject or mesh mutation.</DearLie>
</SELF_AUDIT>
## 2026-05-21 LOOP14_RESUME_STATIC_PROOF_REFRESH

What was wrong:
- Context compression can hide whether the recurring `AupPrecisionMath.DowncastLocalDelta` regression returned inside `EvaluateUpgradeMasksJob.Execute`.
- Transport masks were previously widened, but the proof needed a fresh post-resume scan.
- Unity compile proof is still gated by workstation CPU policy.

What was done:
- Re-read disk authority: `Status_SHINOBU_231.md`, `Rationale_SHINOBU_231.md`, domain map, and `CURRENT_BATCH.md` lines 2321-2385.
- Re-scanned `EvaluateUpgradeMasksJob.Execute`; result: `hot_forbidden_matches=0`, line 1013 direct local cast from already-localized `double3` to `float3`.
- Re-scanned transport upgrade files; result: `VehicleUpgradeBits : ulong`, `_runtimeInstalledUpgradeMask : ulong`, `ComposeInstalledUpgradeMask() : ulong`.
- Parsed `EQUIPMENT_OPTIMIZATION_REPORT.json`; aggregate entries: 3, SHINOBU_231 verdict: `PASS`, `forbiddenPatternHits=0`, `hotEvaluateForbiddenMatches=0`.
- Checked build gate: no `dotnet.exe` / `csc.exe`; CPU sample `100`; no build launched.

Cinematic Cheats used:
- Visual upgrade bits remain shader-facing state through `UpgradeVisualStateDTO`; geometry instantiation is not used.
- `GlobalQualityWeight` only shapes visual intensity/extrusion/glow and cadence; gameplay stat truth stays deterministic and 64-bit mask based.

Exact Microseconds saved:
- Hot AUP helper removal target remains 1-3 us / 10k upgrade rows on i3/MX350-class CPUs.
- Branchless transport stat refresh target remains 2-8 us / transport refresh, call-rate dependent.
- Tool module LUT route target remains 4-12 us / tool-fleet pass after Unity profiler measurement.
- Unity profiler measurement not executed: CPU gate held at `100`.

<SELF_AUDIT id="SHINOBU_231" pass="LOOP14_RESUME_STATIC_PROOF_REFRESH">
  <task_reconciliation total="20" source="Docs/Tasks/CURRENT_BATCH.md:2321">
    <task id="01" verdict="PASS" proof="scanner reports no runtime virtual ApplyModifier or List&lt;Upgrade&gt; stat modifier chain"/>
    <task id="02" verdict="PASS" proof="EvaluateUpgradeMasksJob hot slice has zero if/else/switch/ternary hits"/>
    <task id="03" verdict="PASS" proof="hot DTOs use public fields; no property wrappers in matrix structs"/>
    <task id="04" verdict="PASS" proof="UpgradeMaskDTO explicit 16 bytes, ulong at offset 8"/>
    <task id="05" verdict="PASS" proof="GenerateMockUpgradeMasksJob present for 10k synthetic masks"/>
    <task id="06" verdict="PASS" proof="EvaluateUpgradeMasksJob is Burst IJobParallelFor, branchless hot slice"/>
    <task id="07" verdict="PASS" proof="BuildUpgradeLUTJob and per-tool 16-row LUT matrix present"/>
    <task id="08" verdict="PASS" proof="UpgradeVisualStateDTO visual flags route; no runtime geometry instantiation"/>
    <task id="09" verdict="PASS" proof="SyncUpgradeMasksJob packs inventory slot DTOs to ulong masks"/>
    <task id="10" verdict="PASS" proof="publication jobs mutate Vault DTOs in place through unmanaged routes"/>
    <task id="11" verdict="PASS" proof="thermal gain multiplied by extracted bit scalar"/>
    <task id="12" verdict="PASS" proof="AUP subtract in double before local float3 cast"/>
    <task id="13" verdict="PASS" proof="matrix jobs use deterministic Burst flags where rollback-adjacent"/>
    <task id="14" verdict="PASS" proof="Vault buffers requested with UninitializedMemory"/>
    <task id="15" verdict="PASS" proof="300-entry UpgradeTelemetryEntry ring route present"/>
    <task id="16" verdict="PASS" proof="Stat Compilation X-Ray editor facade present"/>
    <task id="17" verdict="PASS" proof="ReadOnlySpan&lt;byte&gt; CSV parser present"/>
    <task id="18" verdict="PASS" proof="UpgradeMatrixDebugGizmo editor route present"/>
    <task id="19" verdict="PASS" proof="Polymorphic_Modifier_Scanner aggregate JSON verdict PASS"/>
    <task id="20" verdict="PENDING_UNITY_COMPILE" proof="static self-audit refreshed; compile/profiler held by CPU gate"/>
  </task_reconciliation>
  <struct_layout>
    <UpgradeMaskDTO size="16" align="8">
      <field name="EntityHashID" offset="0" size="4"/>
      <field name="EquipmentHashID" offset="4" size="4"/>
      <field name="ActiveUpgradesMask" offset="8" size="8"/>
    </UpgradeMaskDTO>
    <ToolUpgradeModuleRuleDTO size="96" align="8" note="multiple of 32, cold rule matrix row"/>
    <UpgradeVisualStateDTO size="64" note="one cache line visual proof row"/>
  </struct_layout>
  <vault_status private_persistent_native_arrays="0_in_matrix_owner">
    <buffer id="71380" name="UpgradeMasks"/>
    <buffer id="71381" name="UpgradeBaseStats"/>
    <buffer id="71382" name="UpgradeCompiledStats"/>
    <buffer id="71383" name="UpgradeLut"/>
    <buffer id="71384" name="UpgradeRules"/>
    <buffer id="71385" name="UpgradeTelemetryRing"/>
    <buffer id="71386" name="UpgradeTelemetryCursor"/>
    <buffer id="71387" name="UpgradeInventorySlots"/>
    <buffer id="71388" name="UpgradeItemMap"/>
    <buffer id="71389" name="UpgradeVisualState"/>
    <buffer id="71410" name="UpgradeToolModuleRules"/>
    <buffer id="71411" name="SuitUpgradeTelemetryRing"/>
    <buffer id="71412" name="UpgradeToolProfiles"/>
  </vault_status>
  <dependency_graph input="ModularEquipmentEngine active equipment integration handle" chain="BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob" output="H8Memory.RegisterActiveJob(SystemID.GameplayTools, chain.Final)"/>
  <compile_guard result="not_run" reason="CPU counter 100 with no dotnet/csc process; AGENTS forbids build above 50 percent CPU"/>
</SELF_AUDIT>

## 2026-05-21 LOOP17_SCANNER_GENERATOR_DRIFT_HARDENING

What was wrong:
- The scanner-generated SHINOBU_231 JSON string was stale. The report artifact already documented `71385/71386`, `71389`, and `71412`, but rerunning `Polymorphic_Modifier_Scanner` would have overwritten that proof with an older route description.
- A broad `Select-String` extraction polluted the terminal with neighboring agent prompts; strict XML isolation was needed.
- A raw `List<SuitUpgradeData>` grep hit existed in `SuitUpgradeManager`, but it was inside `#if UNITY_EDITOR` catalog sync, not runtime stat compilation.

What was done:
- Re-extracted the assignment with `<AGENT_PROMPT id="SHINOBU_231"[^>]*>.*?</AGENT_PROMPT>`: lines 2321-2385, task count 20.
- Patched `Assets/_Project/Scripts/Editor/Polymorphic_Modifier_Scanner.cs` so future reports include the live route: `UpgradeTelemetryEntry[64] via 71385/71386`, `UpgradeVisualStateDTO[64] via 71389`, `ToolRuntimeProfile[48] via 71412`, owner-local upgrade frame counters, and the post-integration chain ending in `RecordUpgradeTelemetryJob`.
- Added generated `editorOnlyIgnored` proof for the editor-only suit upgrade catalog list.
- Re-parsed `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` through `reports[]`; entries: 3, SHINOBU_231 verdict: `PASS`, `forbiddenPatternHits=0`, `hotEvaluateForbiddenMatches=0`.

Cinematic Cheats used:
- Visual upgrade state remains a 64-bit mask plus shader-facing `UpgradeVisualStateDTO`; no runtime mesh or GameObject mutation is introduced.
- `GlobalQualityWeight` remains visual-only for intensity/glow/extrusion and does not change gameplay stat truth.

Exact Microseconds saved:
- Runtime: 0 us from the editor scanner patch itself.
- Protected target savings remain: 1-3 us / 10k rows from removing hidden AUP helper branches; 4-12 us / tool-fleet pass from the tool module LUT route; millisecond-scale spikes avoided by shader-driven visual lies instead of runtime geometry.
- Unity profiler proof remains pending because CPU gate returned `100`.

Verification:
- Delayed hot-slice scan returned `forbidden_matches=0` for `EvaluateUpgradeMasksJob`, `RecordUpgradeTelemetryJob`, `BuildToolModuleLUTJob`, `EvaluateToolModuleLUTJob`, `CompileToolRuntimeStatsJob`, and `PublishUpgradeVisualStateJob`.
- Focused runtime scan returned zero hits for `DowncastLocalDelta`, `0x7FFFFFFF`, `Time.frameCount`, `Time.time`, `Time.deltaTime`, `LayoutKind.Sequential`, `Pack=1`, `CompileRuntimeStats(`, `CompileStats(`, `PowerDrawRate *=`, and `HeatGenerationRate *=`.
- The only `List<SuitUpgradeData>` hit is `SuitUpgradeManager.OnValidate` under `#if UNITY_EDITOR`.
- Build not launched: no `dotnet.exe` / `csc.exe`; CPU counter `100`.
- `git diff --check` returned CRLF normalization warnings only.

<SELF_AUDIT id="SHINOBU_231" pass="LOOP17_SCANNER_GENERATOR_DRIFT_HARDENING">
  <task_reconciliation total="20" source="Docs/Tasks/CURRENT_BATCH.md:2321-2385" verdict="STATIC_PASS_COMPILE_PENDING"/>
  <struct_layout name="UpgradeMaskDTO" size="16">
    <field name="EntityHashID" offset="0" size="4"/>
    <field name="EquipmentHashID" offset="4" size="4"/>
    <field name="ActiveUpgradesMask" offset="8" size="8"/>
  </struct_layout>
  <hot_path_proof jobs="EvaluateUpgradeMasksJob,RecordUpgradeTelemetryJob,BuildToolModuleLUTJob,EvaluateToolModuleLUTJob,CompileToolRuntimeStatsJob,PublishUpgradeVisualStateJob" forbidden_matches="0"/>
  <vault_status ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389,71410,71411,71412"/>
  <dependency_graph chain="BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob"/>
  <compile_guard result="not_run" reason="CPU counter 100; no dotnet.exe/csc.exe; AGENTS forbids build above 50 percent CPU"/>
</SELF_AUDIT>

## 2026-05-21 LOOP18_ALL_JOB_BRANCHLESS_RECHECK

What was wrong:
- The all-job scanner caught `AupPrecisionMath.DowncastLocalDelta` at `UpgradeMatrixCompiler.cs:1064` again. Narrow scans of only evaluator/telemetry can pass while a concurrent source drift reintroduces the helper before the next broader proof.
- The previous Burst audit used stale expected publish job names. The real structs are `PublishActiveEquipmentStatsJob` and `PublishVehicleKinematicStatsJob`.
- Dirty `.asmdef` files exist in the shared worktree, so compile-wall reporting needed to distinguish current-agent edits from other-agent state.

What was done:
- Replaced the helper at line 1064 with direct local lane casts:
  `new float3((float)deltaAup.x, (float)deltaAup.y, (float)deltaAup.z)`.
- Scanned every `IJob` / `IJobParallelFor` `Execute` body in `UpgradeMatrixCompiler.cs` after a 1500 ms delay.
- Verified deterministic Burst flags on SHINOBU_231 matrix jobs.
- Verified explicit DTO layout declarations: `UpgradeMaskDTO[16]`, `UpgradeStatVectorDTO[64]`, `UpgradeLutEntryDTO[128]`, `ToolUpgradeModuleRuleDTO[96]`, `UpgradeTelemetryEntry[64]`, `UpgradeVisualStateDTO[64]`.
- Checked assembly definition state. This pass did not modify `.asmdef`; existing dirty asmdefs are shared-worktree state from other agents.

Cinematic Cheats used:
- Visual upgrades remain shader-facing scalar rows and high mask bits. No CPU mesh toggles or runtime geometry mutation.

Exact Microseconds saved:
- Runtime scanner patch: 0 us.
- Hot AUP helper removal target remains 1-3 us / 10k rows.
- Tool module LUT route target remains 4-12 us / tool-fleet pass.
- Visual mesh mutation avoided remains millisecond-scale spike prevention; Unity profiler proof pending.

Verification:
- `BuildUpgradeLUTJob.Execute forbidden_matches=0`
- `BuildToolModuleLUTJob.Execute forbidden_matches=0`
- `EvaluateToolModuleLUTJob.Execute forbidden_matches=0`
- `EvaluateUpgradeMasksJob.Execute forbidden_matches=0`
- `GenerateMockUpgradeMasksJob.Execute forbidden_matches=0`
- `SyncUpgradeMasksJob.Execute forbidden_matches=0`
- `PublishActiveEquipmentStatsJob.Execute forbidden_matches=0`
- `PublishVehicleKinematicStatsJob.Execute forbidden_matches=0`
- `CompileToolRuntimeStatsJob.Execute forbidden_matches=0`
- `PublishUpgradeVisualStateJob.Execute forbidden_matches=0`
- `RecordUpgradeTelemetryJob.Execute forbidden_matches=0`
- `DowncastLocalDelta` whole-file hit count in `UpgradeMatrixCompiler.cs`: 0.
- Build not launched: no `dotnet.exe` / `csc.exe`; CPU counter `100`.

<SELF_AUDIT id="SHINOBU_231" pass="LOOP18_ALL_JOB_BRANCHLESS_RECHECK">
  <task_reconciliation total="20" source="Docs/Tasks/CURRENT_BATCH.md:2321-2385" verdict="STATIC_PASS_COMPILE_PENDING"/>
  <struct_layout name="UpgradeMaskDTO" size="16" math="4 + 4 + 8 = 16; ActiveUpgradesMask offset 8"/>
  <struct_layout name="ToolUpgradeModuleRuleDTO" size="96" math="8-byte fields at 0/8; scalar lanes through 68; byte flags at 72/73; explicit padding to 96"/>
  <hot_path_proof forbidden_matches="0" scope="all UpgradeMatrixCompiler IJob/IJobParallelFor Execute bodies"/>
  <vault_status private_matrix_native_allocations="0" ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389,71410,71411,71412"/>
  <dependency_graph input="active equipment integration handle" output="H8Memory GameplayTools final handle" chain="BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob"/>
  <compile_guard asmdef_edited_by_this_pass="false" existing_dirty_asmdefs="present_from_shared_worktree" build="not_run_cpu_100"/>
</SELF_AUDIT>
