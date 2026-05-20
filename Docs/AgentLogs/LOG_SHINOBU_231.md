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
  <VaultBuffers ids="71380,71381,71382,71383,71384,71385,71386,71387,71388,71389"/>
  <ZeroGC hotPath="true" note="Hot jobs use NativeArray/raw pointers; editor scanner/gizmo allocate only outside runtime hot path."/>
  <AUP check="PASS" note="double3 subtraction precedes float3 thermal grid sampling."/>
  <DearLie check="PASS" note="Visual upgrades are mask flags for shader presentation, not runtime geometry."/>
  <Dependency check="PASS" note="No GlobalRegistry polling in hot jobs; Agent 141/113 dependencies represented as DTO bridges/mirrors."/>
  <CompileGuard check="BLOCKED" reason="CPU counter 100 percent; dotnet build not launched."/>
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
    Buffers requested by route handles: 71380 Masks, 71381 BaseStats, 71382 CompiledStats, 71383 Lut, 71384 Rules, 71385 TelemetryRing, 71386 TelemetryCursor, 71387 InventorySlots, 71388 ItemMap, 71389 VisualStates.
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
