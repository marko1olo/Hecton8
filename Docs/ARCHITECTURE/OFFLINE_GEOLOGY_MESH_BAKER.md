# Offline Geology Mesh Baker

Date: 2026-05-20
Status: STATIC SOURCE POLISHED / PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- Docs/README.md
- Docs/DOC_GOVERNANCE.md
- Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: SHINOBU_208 / Echelon 2 World Generation

## Contract

Static geology mesh generation belongs in Editor-only tools under `Assets/_Project/Scripts/Editor/GeologyForge`.
Runtime gameplay must consume immutable baked mesh assets from `Assets/_Project/BakedGeometry/Geology`.

## Runtime Boundary

- Baked rocks are static environmental art. Collision/proxy/SDF ownership is outside this render-bake lane.
- Baked mesh vertex layout is 32 bytes: position Float32x3 at byte 0, normal Float32x3 at byte 12, vertex color UNorm8x4 at byte 24, UV0 UNorm16x2 at byte 28.
- Vertex `Color.r` stores baked ambient occlusion.
- Generated LOD0/1/2 assets are immutable and are not gameplay state.
- The BRG handoff artifact is `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom`: 64B header plus 128B records containing LOD mesh GUIDs, AUP seed, bounds, triangle counts, variation, flags, and 32B vertex stride proof.
- Manifest, black-box dump, bake report, layout audit, and scanner report writes use `.tmp` replacement and preserve the prior artifact as `.bak` when one exists.
- Editor raw working rows are fixed at 64 bytes to avoid parallel worker false sharing while keeping runtime meshes compact.
- Editor extraction uses a fixed packed-nibble tetra edge LUT (`GeologyTetraExtractionLut`) shared by the count and extraction jobs; complement cases reverse triangle winding and `ValidateComplementWinding()` runs through the layout validator. No managed LUT array or runtime topology generation route is introduced.
- UI and menu batch baking are driven through `BakeProfilesAsync` and `EditorApplication.update`; one variation is baked per editor tick, asset editing is scoped to that variation's save tranche, the batch writes one manifest/report at finish or cancel, and `AssemblyReloadEvents.beforeAssemblyReload` routes through `CancelAsyncBake` before domain transition.
- Async bake progress uses static cancelable-progress title/message text and a numeric progress scalar; exact profile facts belong in bake reports/manifests, not per-update formatted UI strings.
- Async bake variation totals and execution both use the same 1..500 variation clamp; aggregate progress totals saturate instead of wrapping on malformed authoring input.
- The UI Toolkit variation field displays and resolves through the same shared `MaximumVariations=500` ceiling as the generator, so designer-facing values match executed batch counts.
- Async result storage is sized from sanitized total bakes up to `MaximumAsyncResultPreallocation=5000`; it no longer derives from `profiles.Count * 4`, so the assignment-scale forge path avoids expected list backing-array growth while malformed oversized totals stay bounded.
- The UI Toolkit window reuses one editor-owned bake-request list for selected/all bake commands. `BakeProfilesAsync` copies the profiles immediately, so the facade does not retain an active runner alias.
- CSV profile reloads and the menu bake route use caller-owned lists: `_profiles` for the active window and `_menuProfiles` for the static menu. `LoadProfiles(List<GeologyBakeProfile>)` clears/fills the caller-owned list and preserves fallback-profile behavior, avoiding a throwaway list before the async runner snapshot.
- SceneView preview fills its fixed 2048-point buffer by counting all near-surface SDF candidates and deterministically striding through them. This keeps the Dear Lie point cloud representative without escalating to full mesh extraction, AO, or mesh upload.
- Runtime mesh generation scans are editor proof tooling. Non-batch scans time-slice both directory discovery and file scanning through `EditorApplication.update` with a 4 ms budget and a cancelable progress bar; batch-mode scans remain synchronous for deterministic CI/report execution.
- Async finish always clears runner state in `finally`; manifest/report IO faults and UI progress callback faults cannot leave `_asyncProfiles` latched.
- Zero-output canceled async bakes do not rewrite the previous `.h8geom` manifest or bake report; partial artifacts are emitted only after at least one metrics row or manifest record exists.
- The Geology Forge window rejects duplicate/empty async bake requests through `TryStartBake`; the SceneView SDF preview subscribes only when preview data is built and unsubscribes through `GeologyForgePreview.Shutdown` when the window closes.
- SceneView preview is a bounded cold editor probe over 24^3 SDF samples. It invokes the Burst preview kernel through `Run(count)` rather than `Schedule().Complete()`, avoiding a fake asynchronous fence while keeping preview scratch local to the button action.
- Mesh and manifest bounds are computed from finite raw positions only; poisoned rows are skipped and an all-poisoned mesh falls back to a 1m local bound instead of serializing NaN bounds.
- Editor normal smoothing builds transient quantized weld buckets, accumulates angle-weighted neighboring face normals, aligns them with SDF gradients, and writes tangents before packing the 32B runtime stream.
- Final Burst payload kernels sanitize their own vector inputs: triplanar UVs, AO nearest sampling, LOD snap, and UV packing all fail closed to finite fallback data.
- Editor bake black-box rows are fixed at 64 bytes and held in a 300-entry ring. Fault dump path is `Docs/AgentLogs/Dump_SHINOBU_208.bin`; this is editor diagnostics, not runtime state.
- Non-asset bake probes destroy transient LOD mesh objects after metrics are recorded; only saved assets retain mesh ownership.
- Mesh upload/validation failure destroys the newly created transient `Mesh` before leaving `CreateUnityMesh`; successful return explicitly transfers ownership to the asset/save path.
- Generated prefabs are no longer emitted by this lane. Runtime consumers must use static mesh assets plus the binary manifest, not generated GameObjects or `LODGroup` wrappers.
- Generated meshes intentionally do not add `MeshCollider`.
- Netcode rollback, Merkle hashing, and `StateRingBuffer` must not hash baked mesh vertex or index buffers every frame.
- `GlobalQualityWeight` is a continuous `smoothstep` curve over bake math: SDF noise frequency, noise amplitude, fractional octave contribution, Voronoi/ridged contribution, AO ray budget, AO step count, AO range, UV scale, LOD budgets, and collapse size.
- Runtime LOD transition distances may also be shifted by continuous `GlobalQualityWeight`; the generator does not author binary quality forks.
- Compile-wall boundary: GeologyForge is isolated by `Hecton8.World.OfflineGeology.Editor.asmdef`, Editor-only, unsafe-enabled, and references only `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`.
- Designer CSV profiles include persisted `iso_level` density threshold tuning. The parser validates the exact supported header schema before row parsing, and keeps old no-`iso_level` layouts valid by detecting the header token before consuming the column.

## First-20-Minutes Route Impact

This removes static geology topology generation from the route budget and buys readable cave/seabed silhouette in the Copper Wire route without adding runtime Marching Cubes stalls.

## Verification

Current evidence is static source only. Unity import, bake execution, mesh inspector validation, Frame Debugger, GCMonitor, and player-route proof are pending.

Static black-box source is present: SDF, extraction, attribute, AO, and serialization stages write `GeologyBakeTelemetryEntry` rows. Non-finite stage timing and exceptions dump the ring to `Docs/AgentLogs/Dump_SHINOBU_208.bin`. No dump file is expected until a fault path is exercised.

Static normal-weld source is present: `BuildNormalBucketJob` writes transient `NativeParallelMultiHashMap<ulong,int>` buckets and `CalculateSmoothNormalsJob` consumes the buckets with `[NoAlias]` fields and raw `GeologyRawVertex*` mutation. Unity import/Burst Inspector proof remains pending.

Static manifest source is present: `GeologyMeshManifestHeader` validates at 64 bytes and `GeologyMeshManifestRecord` validates at 128 bytes. Unity import and runtime BRG consumption proof remain pending.
Raw geology binary payloads are explicitly little-endian. The writer fails fast on a non-little-endian host instead of emitting native-endian `.h8geom` or dump bytes.

Static quality-weight source is present: `GenerateMockFractalNoiseJob` takes `GlobalQualityWeight` directly, and both full bake and SceneView preview pass the same profile scalar. Unity bake timing proof remains pending.

Static async-facade source is present: `GeologyForgeWindow` and `BakeCsvProfilesMenu` start `BakeProfilesAsync`, which advances through profile variations on `EditorApplication.update`, reports progress through the UI Toolkit progress bar, and exposes `Cancel Bake`. The old public synchronous batch path has been removed from owned source. Unity Editor responsiveness/cancel proof remains pending.

Static async progress source is present: `TickAsyncBake` uses static progress-bar text instead of formatting profile/variation strings inside the editor update hook. Unity Profiler allocation proof remains pending.

Static variation-count guard source is present: `SanitizeVariationCount()` feeds both `SanitizeProfile()` and `CountTotalBakes()`, and aggregate async totals saturate before integer wrap. Unity malformed-CSV execution proof remains pending.

Static variation-facade source is present: `GeologyForgeConstants.MaximumVariations` is shared by the generator and UI Toolkit field resolution. Unity Editor field proof remains pending.

Static CSV variation-ceiling source is present: CSV `variations` parsing clamps through `GeologyForgeConstants.MaximumVariations`, matching UI field resolution, async total math, and generator execution. Unity malformed-CSV execution proof remains pending.

Static async-result preallocation source is present: `BakeProfilesAsync` computes `_asyncTotalBakes` before result-list allocation, `ResolveAsyncResultCapacity()` caps preallocation at `MaximumAsyncResultPreallocation=5000`, and the old `profiles.Count * 4` capacity path is absent. Unity Profiler allocation proof remains pending.

Static editor request/preview source is present: `GeologyForgeWindow` reuses `_bakeRequestProfiles` for bake buttons, and `GeologyForgePreview.Build()` performs deterministic two-pass candidate sampling into the bounded preview buffer. Unity Editor allocation/timing proof remains pending.

Static caller-owned CSV profile source is present: `GeologyProfileCsv.LoadProfiles(List<GeologyBakeProfile>)` fills caller-owned storage, the window reload path loads directly into `_profiles`, and the menu bake path reuses `_menuProfiles` before async snapshotting. Unity Profiler allocation proof remains pending.

Static asset-edit scope source is present: `AssetDatabase.StartAssetEditing()` is opened only inside the async tick save tranche and closed before the tick continues. Full per-job staged scheduling proof remains pending.

Static async-finish hardening source is present: `FinishAsyncBake` stops the update runner, attempts artifact writes/progress notification, and clears all static async state in `finally`. Unity exception-path proof remains pending.

Static artifact-failure hardening source is present: `CreateUnityMesh` destroys transient meshes on failed upload/validation, and `FinishAsyncBake` skips manifest/report writes for zero-output cancels. Unity exception/cancel proof remains pending.

Static atomic-write source is present: `.h8geom`, dump, bake report, layout audit, and scanner report writers use temp files and replacement with backup preservation. Unity IO-fault proof remains pending.

Static scanner time-slice source is present: non-batch `RuntimeMeshGenerationScanner.ScanAndWriteReport()` starts `StartAsyncScan`, advances directory discovery through `ExpandNextAsyncDirectory()`, advances file scans on `TickAsyncScan`, and cleans up through `CancelAsyncScan` or completion. Unity Editor progress/cancel proof remains pending.

Static editor-lifetime source is present: `TryStartBake` handles rejected async starts, and `GeologyForgePreview.Shutdown` removes `SceneView.duringSceneGui` on window disable. Unity Editor callback proof remains pending.

Static preview fence source is present: `GeologyForgePreview.Build()` imports `Unity.Jobs` and runs the bounded preview kernel through `Run(count)` instead of `Schedule(count, 64).Complete()`. Unity Editor preview timing proof remains pending.

Static bounds-vaccination source is present: `CalculateBounds` initializes min/max from the first finite `GeologyRawVertex.Position`, skips non-finite rows, and falls back to a finite 1m bound if no valid position exists. Unity mesh inspector and manifest audit proof remain pending.

Static Burst finite-guard source is present: final UV/AO/LOD/pack kernels sanitize non-finite normals, positions, sample positions, snapped positions, and UVs before serializing mesh data. Unity/Burst compile proof remains pending.

Static extraction-LUT source is present: `SdfCellVertexCountJob` and `SdfToMeshExtractionJob` derive the same tetra case index and edge sequence through `GeologyTetraExtractionLut`. Burst Inspector/vectorization proof remains pending.

Static CSV iso-level source is present: `GeologyProfileCsv` reads `iso_level` when the header contains it and falls back to `0` for older layouts. Unity Editor facade proof remains pending.

Static CSV schema guard source is present: `GeologyProfileCsv` validates header tokens before row parsing and throws on reordered/missing columns instead of silently corrupting profile fields. Unity Editor error-path proof remains pending.

On-demand layout audit source is present: `HECTON-8/Geology Forge/Run Layout Self Audit` validates generated mesh streams and `geology_mesh_manifest.h8geom`, then writes `Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json`. Current report is a placeholder until a Unity Editor bake/audit is executed.

`Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` currently uses scanner schema v2 and reports `findingCount=34`, `actionableFindingCount=28`, `simulationPhaseFindingCount=0`, `bootstrapPhaseFindingCount=0`, `proceduralMaterialCloneFindingCount=0`, and `runtimeMeshAllocationsEradicated=false`. These remaining runtime topology sites are outside the GeologyForge render-bake lane and require owner-specific migration before project-wide eradication can be claimed.
