# Base Module Catalog - SHINOBU_216

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: Construction base module catalog
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

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

- `TryStartCatalogByteLoad` may load binary bytes on a background `FileStream` into Vault-owned `NativeArray<byte>` through native span.
- Hydration remains a separate Burst job behind boot/dispatcher dependency.

- GlobalQualityWeight does not alter catalog truth, DTO layout, socket positions, masks, or save/network identity. It may only scale editor/presentation overlays owned outside this runtime lane.

## Editor Tools

- `HECTON-8/Construction/Base Module Catalog` opens the UI Toolkit bake window.

- `BAKE CATALOG BINARY` writes `Assets/_Project/Data/Construction/BaseModuleCatalog.h8bin`.

- Scene View socket gizmo reads Vault DTO ranges first, then falls back to editor authoring data when the Vault catalog is absent.

- `Hierarchy_Data_Scanner` writes `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`.

## Verification Boundary

- Static source proof exists for Transform socket traversal removal from construction graph/proxy paths.

- Compile/import/profiler/player proof remains pending behind the project build guard; local CPU was measured above 50%, so `dotnet build` was not launched.
