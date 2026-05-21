# Base Module Catalog - SHINOBU_216

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

## Authority
- Runtime authority is `GlobalDataVault`, not prefab hierarchies.
- Authoring uses `BaseModuleTemplate` only in editor/cold conversion.
- Runtime DTO buffers:
  - `BaseModuleCatalogState` = 70330
  - `BaseModuleCatalogDefinitions` = 70331
  - `BaseModuleCatalogSockets` = 70332
  - `BaseModuleCatalogCosts` = 70333
  - `BaseModuleCatalogHashToIndex` = 70334
  - `BaseModuleCatalogTelemetryRing` = 70335
  - `BaseModuleCatalogHydrationBytes` = 70336
  - `BaseModuleCatalogHydrationStatus` = 70337
  - `BaseModuleCatalogCsvScratch` = 70338
  - `BaseModuleCatalogScannerReport` = 70339
- Runtime catalog provider stores no private persistent `NativeArray`, `NativeList`, or `NativeHashMap`; callers resolve Vault buffers during boot/scheduling.

## Layout
- `ModuleDefinitionDTO`: explicit 64 bytes.
- `SocketDefinitionDTO`: explicit 32 bytes.
- `ModuleCostDTO`: explicit 64 bytes.
- DTOs expose fields only. No properties.
- `.h8bin` header is explicit 64 bytes, versioned, checksumed by folded `xxHash3.Hash64`, and must carry `BinaryLittleEndianFlag`; reversed magic is rejected as `InvalidEndian` instead of silently hydrating corrupt DTOs.

## Runtime Rules
- Module lookup is a binary search over sorted `NativeArray<ModuleDefinitionDTO>`.
- Socket ranges are contiguous: `SocketStartIndex` + `SocketCount`.
- Runtime adjacency systems resolve one Vault reference before the module loop and read sockets through `TryGetModuleSocketRangeFromVault(PrefabHashID)`; authoring-template socket conversion is editor-only fallback.
- Socket AUP = root `double3` + rotated/local `float3` offset promoted to double.
- Runtime adjacency keys quantize that socket AUP directly. Runtime `Vector3` socket positions are presentation data for pipe/edge rendering, not connection truth.
- Compatibility is `(lhs.AllowedConnectionsMask & rhs.AllowedConnectionsMask) != 0u`.
- Catalog buffers are immutable static data and must not be hashed into rollback state.
- `GenerateMockModuleCatalogJob`, `QueryModuleDefinitionJob`, and `HydrateModuleCatalogJob` use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Job `NativeArray` fields are marked `[NoAlias]` where the buffers are physically distinct Vault lanes; scheduling consumes a caller dependency and returns the output `JobHandle`.
- Binary bytes may be loaded by `TryStartCatalogByteLoad` on a background `FileStream` directly into Vault-owned `NativeArray<byte>` through a native span; hydration remains a separate Burst job fenced behind boot/dispatcher dependency.
- GlobalQualityWeight does not alter catalog truth, DTO layout, socket positions, masks, or save/network identity. It may only scale editor/presentation overlays owned outside this runtime lane.

## Editor Tools
- `HECTON-8/Construction/Base Module Catalog` opens the UI Toolkit bake window.
- `BAKE CATALOG BINARY` writes `Assets/_Project/Data/Construction/BaseModuleCatalog.h8bin`.
- Scene View socket gizmo reads Vault DTO ranges first, then falls back to editor authoring data when the Vault catalog is absent.
- `Hierarchy_Data_Scanner` writes `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`.

## Verification Boundary
- Static source proof exists for Transform socket traversal removal from construction graph/proxy paths.
- Compile/import/profiler/player proof remains pending behind the project build guard; local CPU was measured above 50%, so `dotnet build` was not launched.
