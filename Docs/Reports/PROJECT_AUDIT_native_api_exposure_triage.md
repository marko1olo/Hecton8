# PROJECT_AUDIT Native API Exposure Triage

Date: 2026-05-21
Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, compile, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or device proof was executed.

## Source

- Tool: `Tools/PolishMandateStaticAudit.py`
- JSON artifact: `Docs/Reports/PROJECT_AUDIT_polish_native_api_exposure.json`
- Markdown artifact: `Docs/Reports/PROJECT_AUDIT_polish_native_api_exposure.md`
- Command: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.json --report-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.md`

## Raw Count Preservation

The public/internal/protected mutable native API warning class is:

- `nativeCollectionPublicMutableApiExposure`: 268 matches / 97 files

Additive exposure-kind buckets:

- `nativeApiExposureMutableReturn`: 79
- `nativeApiExposureOutRefMutable`: 189
- `nativeApiExposureAmbiguousMutable`: 0
- Sum: 268

Additive build-surface buckets:

- `nativeApiExposureBuildPlayerRuntime`: 254
- `nativeApiExposureBuildEditorOnly`: 5
- `nativeApiExposureBuildQaDevProof`: 9
- Sum: 268

Additive primary-risk buckets:

- `nativeApiRiskCoreVaultOrAllocatorSurface`: 21
- `nativeApiRiskEditorOrProofSurface`: 14
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 61
- `nativeApiRiskRuntimeOutRefMutableView`: 114
- `nativeApiRiskRuntimeReturnMutableView`: 58
- `nativeApiRiskRuntimeAmbiguousMutableView`: 0
- Sum: 268

This is not a debt reduction. It separates allocator/Vault APIs, editor/proof surfaces, and runtime mutable view exports so fixes can happen without breaking neighboring agents.

## Interpretation

The raw count is real enough to matter: most findings are player-runtime surfaces, not editor-only tooling. The two dangerous shapes are:

- Direct mutable native returns/properties, such as internal `NativeArray<T>` graph arrays and runtime BRG/GPU handoff buffers.
- `out/ref NativeArray<T>` APIs that hand mutable views to callers, even when method names say `ForEditor`, `Debug`, or `Snapshot`.

Core allocator and Vault APIs are counted intentionally. They are not automatically wrong, but they are ownership choke points and should be reviewed under a different standard than domain runtime APIs.

Top runtime mutable return/property files:

| File | Count | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 27 | Exposes active vegetation matrix/metadata/type buffers for direct GPU/consumer handoff. |
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 | Opens legacy/native queue writer surfaces. |
| `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 4 | Exposes mutable simulation pools/views. |
| `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs` | 3 | Exposes mutable fauna simulation buffers. |

Top runtime `out/ref NativeArray<T>` files:

| File | Count | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 21 | Native read-buffer acquisition and cache sampling paths still surface mutable native views. |
| `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs` | 8 | Economy/ledger vault or owner-state views are exposed as mutable arrays. |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 6 | Navigation grid native state exits through mutable views. |
| `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs` | 5 | Buoyancy state is exposed to callers as mutable native buffers. |
| `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` | 4 | Tide/seismic buffers are exposed through mutable out views. |

## Safe Next Actions

1. Do not mass-change signatures to `NativeArray<T>.ReadOnly`; many call sites pass these buffers into jobs, and compile fallout would be broad.
2. For each domain file, add a read-only accessor first while keeping the legacy mutable wrapper until all consumers move.
3. Mark true writer APIs explicitly: allocator/Vault write locks can return mutable views, but read/snapshot/debug APIs should return `NativeArray<T>.ReadOnly` or a domain snapshot DTO.
4. Runtime methods named `ForEditor`, `Debug`, `Snapshot`, or `Readback` need an actual compile/runtime boundary. A name is not an authority boundary.
5. BRG/GPU upload handoffs can keep native buffers only if the owner route documents lifetime, generation id, read fence, and mutation window.

## Current Worst Architectural Smell

The codebase has many methods that look like read accessors but return mutable native views. That violates the global read-accessor doctrine: a read route must not hand out a write-capable surface unless the name and ownership contract prove that the caller is the writer.

`HabitatGraphManager` graph SoA accessors are currently `NativeArray<T>.ReadOnly`, so they are no longer part of the direct mutable return/property top list. The diagnostic/editor-named bucket now separates 61 runtime-compiled mutable views with names or payloads such as `ForEditor`, `Debug`, `Readback`, `Snapshot`, or `Telemetry`; these are still player-runtime signatures unless they have an actual compile/runtime guard. The next real engineering step is per-domain read-only migration: start with one `HectonMapMagicVegetationBridge` buffer family, add read-only adapters, migrate consumers, then retire the mutable view only after compile/integration proof.
