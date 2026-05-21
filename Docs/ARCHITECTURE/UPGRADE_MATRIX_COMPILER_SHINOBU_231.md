# Upgrade Matrix Compiler SHINOBU_231

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: SHINOBU_231 / TOOL_UPGRADE_MATRIX_COMPILER.

Domain: branchless equipment upgrade stat compilation for tools, suits, and vehicle-adjacent stat publication. The route is a stateless Vault kernel; it does not poll `GlobalRegistry` in hot paths and does not own private persistent `NativeArray` fields.

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
- `SIMULATION`: `ModularEquipmentEngine.ScheduleToolUpgradeMatrixPostIntegration(...)` chains `BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob` after active equipment integration and returns the final `JobHandle` to the existing equipment dispatcher lane. The schedule count is the highest used tool slot plus one; inactive holes are cleared on unregister/overcharge so `UninitializedMemory` tails are not read.
- `SIMULATION`: `EvaluateUpgradeMasksJob` applies LUT rows and branchless environmental gates.
- `SIMULATION`: `UpgradeMatrixScheduler.ScheduleUpgradeMaskEvaluation(...)` is the only approved route for the raw pointer evaluator. It validates `NativeArray.IsCreated`, per-equipment row counts, non-empty LUT rows, and non-empty thermal grid data before scheduling; the Burst `Execute` body contains no `if`, `else`, `switch`, ternary operator, or `DowncastLocalDelta` helper call.
- `SIMULATION`: publication jobs write compiled scalars into active equipment and vehicle kinematic mirror DTOs via `UnsafeUtility.AsRef`.
- `POST_SIMULATION`: `RecordUpgradeTelemetryJob` writes the 300-frame blackbox ring into `71385/71386`; after the equipment fence finalizes, `ModularEquipmentEngine.PatchUpgradeTelemetryMicroseconds(...)` patches the latest row with the measured fence microseconds and triggers `Docs/AgentLogs/Dump_SHINOBU_231.bin` on threshold breach.
- `VISUAL_SYNC`: `PublishUpgradeVisualStateJob` writes `71389 UpgradeVisualStateDTO` rows with high mask bits and continuous `GlobalQualityWeight` scalars for shader-driven geometry lies, not runtime mesh instantiation.

Rules:
- `UpgradeMaskDTO` must remain exactly 16 bytes with `ActiveUpgradesMask` at offset 8.
- Runtime arrays are requested through `UpgradeMatrixVault.AcquireHandles(..., NativeArrayOptions.UninitializedMemory)`.
- Tool module authoring data crosses the boundary as `ToolUpgradeModuleRuleDTO[96]`; Burst jobs never read `ScriptableObject` module arrays.
- `ModularEquipmentEngine` owner mirrors packed `ToolUpgradeModuleRuleDTO[]` rows only; public `ToolMetadata.defaultModules` remains authoring data and is not retained in the runtime mirror. `ToolState.UpgradeBitmask64` is the authoritative 64-bit runtime mask; `ToolState.UpgradeBitmask` remains a low-32 compatibility mirror only.
- Vehicle upgrade bits use `VehicleUpgradeBits : ulong`; `VehicleUpgradeModule` and `SubmarineCoreDirector` compose installed transport masks as `ulong`. The legacy `VehicleUpgradesChangedSignal.UpgradeMask` remains low-32 only because that signal contract predates this matrix route.
- Upgrade-owned payload frame values use owner-local monotonic counters, not Unity `Time.frameCount`.
- `SuitUpgradeManager` does not own a persistent telemetry `NativeArray`; it creates/refreshes Vault generation handles only during cold service setup and writes telemetry through phase-local resolved views.
- Core stat truth is identical across hardware tiers. `GlobalQualityWeight` only affects `UpgradeVisualStateDTO` intensity/extrusion/glow, so weak hardware collapses to cheap shader flags while high/ultra can spend the saved CPU on visual overkill.
- The hot evaluator requires a valid LUT pointer and a valid thermal grid pointer. If thermodynamics is unavailable, boot must provide a one-cell fallback grid with `AmbientFallbackCelsius`; pointer absence is a cold-route failure, not a hot-path branch.
