# Upgrade Matrix Compiler SHINOBU_231

Owner: SHINOBU_231 / TOOL_UPGRADE_MATRIX_COMPILER.

- Domain: branchless equipment upgrade stat compilation.
- Scope: tools, suits, vehicle-adjacent stat publication.
- Route: stateless Vault kernel.
- Hot path: no `GlobalRegistry` polling.
- Native ownership: no private persistent `NativeArray` fields.

Vault buffers:

- `71380` `UpgradeMasksBuffer`: `UpgradeMaskDTO[16]`, active `ulong` masks.

- `71381` `UpgradeBaseStatsBuffer`: `UpgradeStatVectorDTO[64]`, baseline stat lanes.

- `71382` `UpgradeCompiledStatsBuffer`: `UpgradeStatVectorDTO[64]`, compiled stat lanes.

- `71383` `UpgradeLutBuffer`: `UpgradeLutEntryDTO[128]`, precomputed multiplier/additive rows.

- `71384` `UpgradeRulesBuffer`: `UpgradeBitRuleDTO[32]`, cold boot CSV/rule source.

- `71385` `UpgradeTelemetryRingBuffer`: `UpgradeTelemetryEntry[64] x 300`.

- `71386` `UpgradeTelemetryCursorBuffer`: `int[1]`.

- `71387` `UpgradeInventorySlotsBuffer`: `InventoryUpgradeSlotDTO[16]`.

- `71388` `UpgradeItemMapBuffer`: `UpgradeItemMapDTO[16]`.

- `71389` `UpgradeVisualFlagsBuffer`: `UpgradeVisualStateDTO[64]`, VISUAL_SYNC shader flag, quality, glow, and extrusion lane.

- `71410` `UpgradeToolModuleRulesBuffer`: `ToolUpgradeModuleRuleDTO[96]`, packed four-slot tool module rule rows. `71390..71409` are occupied by ProceduralCoral; `71480..71489` are occupied by Auxiliary, so `71410` is the nearest scanned free local owner ID.

- `71411` `SuitUpgradeTelemetryRingBuffer`: `SuitUpgradeTelemetryEntry[64] x 300`, suit blackbox mirror owned by `GlobalDataVault` through a pointer-free `VaultGenerationHandle`.

- `71412` `UpgradeToolProfilesBuffer`: `ToolRuntimeProfile[48]`, packed tool base profiles consumed by `CompileToolRuntimeStatsJob`.

Phase route:

- `PRE_SIMULATION`: `SyncUpgradeMasksJob` packs inventory item hashes into `UpgradeMaskDTO.ActiveUpgradesMask`.

- `PRE_SIMULATION`: `BuildToolModuleLUTJob` bakes each 4-slot tool rule set into 16 contiguous `UpgradeLutEntryDTO` rows; `EvaluateToolModuleLUTJob` reads `toolIndex * 16 + slotMask` in O(1).

- `SIMULATION` post-integration chain:
  - API: `ModularEquipmentEngine.ScheduleToolUpgradeMatrixPostIntegration(...)`.
  - Chain: `BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob`.
  - Returns final `JobHandle` to the existing equipment dispatcher lane.
  - Schedule count: highest used tool slot plus one.
  - Inactive holes clear on unregister/overcharge; `UninitializedMemory` tails are not read.

- `SIMULATION`: `EvaluateUpgradeMasksJob` applies LUT rows and branchless environmental gates.

- `SIMULATION`: `UpgradeMatrixScheduler.ScheduleUpgradeMaskEvaluation(...)` is the only approved raw-pointer evaluator route.
- It validates `NativeArray.IsCreated`, per-equipment row counts, non-empty LUT rows, and non-empty thermal grid data before scheduling.
- Burst `Execute` body contains no `if`, `else`, `switch`, ternary, or `DowncastLocalDelta`.

- `SIMULATION`: publication jobs write compiled scalars into active equipment and vehicle kinematic mirror DTOs via `UnsafeUtility.AsRef`.

- `POST_SIMULATION`: `RecordUpgradeTelemetryJob` writes the 300-frame blackbox ring into `71385/71386`.
- After equipment fence finalizes, `ModularEquipmentEngine.PatchUpgradeTelemetryMicroseconds(...)` patches latest row.
- Patch value: measured fence microseconds.
- Threshold breach triggers `Docs/AgentLogs/Dump_SHINOBU_231.bin`.

- `VISUAL_SYNC`: `PublishUpgradeVisualStateJob` writes `71389 UpgradeVisualStateDTO` rows with high mask bits and continuous `GlobalQualityWeight` scalars for shader-driven geometry lies, not runtime mesh instantiation.

Rules:

- `UpgradeMaskDTO` must remain exactly 16 bytes with `ActiveUpgradesMask` at offset 8.

- Runtime arrays are requested through `UpgradeMatrixVault.AcquireHandles(..., NativeArrayOptions.UninitializedMemory)`.

- Tool module authoring data crosses the boundary as `ToolUpgradeModuleRuleDTO[96]`; Burst jobs never read `ScriptableObject` module arrays.

- `ModularEquipmentEngine` mirrors packed `ToolUpgradeModuleRuleDTO[]` rows only.
- `ToolMetadata.defaultModules` remains authoring data.
- Runtime mirror does not retain default module arrays.
- `ToolState.UpgradeBitmask64` is the authoritative 64-bit runtime mask.
- `ToolState.UpgradeBitmask` remains a low-32 compatibility mirror.

- Vehicle upgrade bits use `VehicleUpgradeBits : ulong`; `VehicleUpgradeModule` and `SubmarineCoreDirector` compose installed transport masks as `ulong`. The legacy `VehicleUpgradesChangedSignal.UpgradeMask` remains low-32 only because that signal contract predates this matrix route.

- Upgrade-owned payload frame values use owner-local monotonic counters, not Unity `Time.frameCount`.

- `SuitUpgradeManager` does not own a persistent telemetry `NativeArray`; it creates/refreshes Vault generation handles only during cold service setup and writes telemetry through phase-local resolved views.

- Core stat truth is identical across hardware tiers. `GlobalQualityWeight` affects only `UpgradeVisualStateDTO` intensity/extrusion/glow; weak hardware uses cheap shader flags, high/ultra spends saved CPU visually.

- Hot evaluator requires valid LUT and thermal-grid pointers.
- If thermodynamics is unavailable, boot provides a one-cell fallback grid with `AmbientFallbackCelsius`.
- Pointer absence is a cold-route failure, not a hot-path branch.
