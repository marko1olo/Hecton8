# PROJECT_AUDIT Private Native Collection Triage

Date: 2026-05-21
Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, compile, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or device proof was executed.

## Source

- Tool: `Tools/PolishMandateStaticAudit.py`
- JSON artifact: `Docs/Reports/PROJECT_AUDIT_polish_private_native_risk_buckets.json`
- Markdown artifact: `Docs/Reports/PROJECT_AUDIT_polish_private_native_risk_buckets.md`
- Companion mutable API triage: `Docs/Reports/PROJECT_AUDIT_native_api_exposure_triage.md`
- Command: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_private_native_risk_buckets.json --report-path Docs\Reports\PROJECT_AUDIT_polish_private_native_risk_buckets.md`

## Raw Count Preservation

The raw warning class remains intact:

- `privateNativeCollectionField`: 1316 matches / 229 files

Additive non-overlapping classifier sum:

- `privateNativeCollectionVaultAlias`: 28
- `privateNativeCollectionStaticQueueLane`: 209
- `privateNativeCollectionBlackBoxTelemetry`: 83
- `privateNativeCollectionOwnerLocalScratch`: 79
- `privateNativeCollectionUnclassified`: 917
- Sum: 1316

Additional declaration/risk dimensions also preserve raw total:

- Declaration: 1185 field declarations, 131 method-return signatures, 0 ambiguous.
- Build surface: 1254 player-runtime, 48 editor-only, 14 QA/dev-proof.
- Primary risk: 776 owner-local runtime native state, 218 static signal/event bridge, 117 static global native state, 131 native-collection-returning methods, 29 Vault alias/resolver, 45 editor/proof state, 0 unclassified.

This is not a debt reduction. It separates audit noise and justified surfaces from the remaining owner-local and static-global native state.

## Interpretation

`SpatialAudioManager` is not the primary private-native ownership problem in this pass: 28 of its findings are explicitly marked as Vault aliases. Those aliases still need generation/fence proof, but the field declarations are not direct allocator ownership by themselves.

The serious current risk is the 776 owner-local runtime native fields across 97 files, plus 117 static global native fields and 218 static signal/event bridge fields. Top owner-local runtime files:

| File | Owner-local runtime fields | Static risk |
|---|---:|---|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 50 | Dear Lie Vault lanes exist, but health/destroyed/regrowth maps remain private owner-local truth. |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 | Inventory truth, save/defrag/mass/radiation state, and blackbox-adjacent state remain mostly private persistent arrays. |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 | Persistent graph truth, solver buffers, published node state maps; likely cross-domain power authority without enough Vault surface. |
| `Assets/_Project/Scripts/HectonFluidEngine.cs` | 40 | Fluid/buoyancy/advection state is mostly private persistent SoA. Some can stay local, but exported raw NativeArray accessors need route proof. |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 34 | Room/base gas truth sits in private SOA arrays; must be reconciled against physiology/atmosphere authority boundaries. |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 33 | Parallel pressure/gas room state exists beside `GasDynamicsSolver`; ownership overlap requires review before migration. |
| `Assets/_Project/Scripts/TetherInstance.cs` | 25 | Tether physics native state is private runtime truth and likely rollback/telemetry relevant. |
| `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 24 | Streaming state is mixed Vault acquisition plus private maps/queues/lists; local scratch and cross-domain residency facts need separation. |

## Safe Next Actions

1. Do not mass-migrate these fields to `GlobalDataVault`. The native memory mandate allows owner-local scratch when lifetime, disposal, and job fences are self-contained.
2. For each top file, split fields into: gameplay truth, cross-domain snapshot, save/rollback state, blackbox telemetry, GPU upload, method-returning native views, and owner-local scratch.
3. Only gameplay truth, cross-domain snapshots, save/rollback state, and blackbox telemetry should move to Vault descriptors. Owner-local solver scratch can stay local if disposal and fences are proven.
4. `GlobalSignals.cs` remains a separate static queue-lane migration problem: 209 static queue lane declarations are first-party hot broadcast pressure, not private allocator proof alone.

## Current Worst Architectural Smell

The codebase has many systems that partially adopted Vault patterns while keeping old private persistent buffers in place. That is worse than either clean owner-local design or clean Vault ownership, because it creates ambiguous truth: a field can look local, backed by Vault, or mirrored into another route depending on the phase.

The next real engineering step is not a broad refactor. It is route-by-route ownership reduction: one file, one fact family, one owner, one proof artifact.

Companion API exposure scan now shows `nativeCollectionPublicMutableApiExposure=268` across 97 files. `HabitatGraphManager` graph SoA accessors are currently read-only and no longer appear in the direct mutable return top list, but many read-looking methods still return mutable native views. Private ownership cleanup should not be considered sufficient until public/internal native view exports are migrated to read-only adapters or explicit writer-lock routes.
