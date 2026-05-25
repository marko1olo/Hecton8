# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/OFFLINE_GEOLOGY_MESH_BAKER.md
Rule: historical snapshot only; not active doctrine.

# Offline Geology Mesh Baker

Date: 2026-05-20

Status: STATIC SOURCE POLISHED / PENDING VERIFICATION

Owner: SHINOBU_208 / Echelon 2 World Generation

## Contract

Static geology mesh generation belongs in Editor-only tools under `Assets/_Project/Scripts/Editor/GeologyForge`.

Runtime gameplay must consume immutable baked mesh assets from `Assets/_Project/BakedGeometry/Geology`.

## Runtime Boundary

- Baked rocks are static environmental art. Collision/proxy/SDF ownership is outside this render-bake lane.

- Baked mesh vertex layout is 32 bytes: position Float32x3 at byte 0, normal Float32x3 at byte 12, vertex color UNorm8x4 at byte 24, UV0 UNorm16x2 at byte 28.

- Vertex `Color.r` stores baked ambient occlusion.

- Generated LOD0/1/2 assets are immutable and are not gameplay state.

- The BRG handoff artifact is `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom`: 64B header plus 128B records containing LOD mesh GUIDs, AUP seed, bounds, triangle counts, variation, flags, and 32B vertex stride proof; `BoundsExtents` owns bytes 60..71 and aligned GUID lanes start at byte 72.

- Manifest, black-box dump, bake report, layout audit, and scanner report writes use `.tmp` replacement and preserve the prior artifact as `.bak` when one exists.

- Layout self-audit treats every top-level geology mesh absent from the manifest GUID set as unmanifested, including the empty or missing-manifest case; missing manifest proof cannot hide orphan mesh output behind `unmanifestedMeshCount=0`.

- Editor raw working rows are fixed at 64 bytes to avoid parallel worker false sharing while keeping runtime meshes compact.

- Editor extraction uses a fixed packed-nibble tetra edge LUT (`GeologyTetraExtractionLut`) shared by the count and extraction jobs; complement cases reverse triangle winding and `ValidateComplementWinding()` runs through the layout validator. No managed LUT array or runtime topology generation route is introduced.

- UI and menu batch baking are driven through `BakeProfilesAsync` and `EditorApplication.update`; one variation is baked per editor tick, asset editing is opened only inside `SaveMeshesAndManifest` around LOD asset creation, pre-existing LOD assets are backed up under `_H8Backups` before overwrite, newly created partial LOD assets are deleted on save/GUID/manifest-record failure, the batch writes `.h8geom` only when records exist and writes reports when metrics exist, and `AssemblyReloadEvents.beforeAssemblyReload` routes through `CancelAsyncBake` before domain transition.

- Async bake progress uses static cancelable-progress title/message text and a numeric progress scalar; exact profile facts belong in bake reports/manifests, not per-update formatted UI strings.

- Async bake variation totals and execution both use the same 1..500 variation clamp; aggregate progress totals saturate instead of wrapping on malformed authoring input.

- Async runner state is assigned atomically after profile snapshot and result-list allocation complete; setup faults before assignment leave no active runner latch, and setup/update exception paths clear through `TryFinishAsyncBake(true)` so cleanup/report/progress failures do not replace the original exception.

- The UI Toolkit variation field displays and resolves through the same shared `MaximumVariations=500` ceiling as the generator, so designer-facing values match executed batch counts.

- Async result storage is sized from sanitized total bakes up to `MaximumAsyncResultPreallocation=5000`; it no longer derives from `profiles.Count * 4`, so the assignment-scale forge path avoids expected list backing-array growth while malformed oversized totals stay bounded.

- Existing geology CSV files are fail-closed authoring truth: missing file still creates the deterministic fallback profile, but an existing file with zero data rows throws `CsvErrorNoProfiles=1009`. The last `sector_z` field consumes its own row terminator; `TryReadProfile` does not call `SkipLine` after it, so consecutive rows are not skipped.

- Empty-surface bakes still write metrics/report evidence, but `.h8geom` and `AssetDatabase.SaveAssets()` are gated on positive manifest records so an empty bake cannot overwrite a prior valid BRG manifest.

- The UI Toolkit window reuses one editor-owned bake-request list for selected/all bake commands. `BakeProfilesAsync` copies the profiles immediately, so the facade does not retain an active runner alias.

- CSV profile reloads and the menu bake route use caller-owned lists: `_profiles` for the active window and `_menuProfiles` for the static menu. `LoadProfiles(List<GeologyBakeProfile>)` clears/fills the caller-owned list and preserves fallback-profile behavior, avoiding a throwaway list before the async runner snapshot.

- SceneView preview fills its fixed 2048-point buffer by counting all near-surface SDF candidates and deterministically striding through them. This keeps the Dear Lie point cloud representative without escalating to full mesh extraction, AO, or mesh upload.

- SceneView preview subscribes its draw callback only after successful point-buffer population, so failed preview SDF generation cannot leave a dead SceneView hook registered.

- Runtime mesh generation scans are editor proof tooling over `Assets/_Project/Scripts` excluding `Editor` folders. Non-batch scans time-slice both directory discovery and file scanning through `EditorApplication.update` with a 4 ms budget and a cancelable progress bar; batch-mode scans remain synchronous for deterministic CI/report execution.

- The non-batch runtime mesh scanner reuses editor-owned source-file, directory-stack, and finding buffers across launches. Scan lifetime is tracked by `_asyncScanActive`, not nullable list allocation state.

- Async finish always clears runner state in `finally`; manifest/report IO faults and UI progress callback faults cannot leave `_asyncProfiles` latched.

- Zero-output canceled async bakes do not rewrite the previous `.h8geom` manifest or bake report; partial artifacts are emitted only after at least one metrics row or manifest record exists.

- The Geology Forge window rejects duplicate/empty async bake requests through `TryStartBake`; the SceneView SDF preview subscribes only when preview data is built and unsubscribes through `GeologyForgePreview.Shutdown` when the window closes.

- SceneView preview is a bounded cold editor probe over 24^3 SDF samples. It invokes the Burst preview kernel through `Run(count)` rather than `Schedule().Complete()`, avoiding a fake asynchronous fence while keeping preview scratch local to the button action.

- Mesh and manifest bounds are computed from finite raw positions only; poisoned rows are skipped and an all-poisoned mesh falls back to a 1m local bound instead of serializing NaN bounds.

- Editor normal smoothing builds transient quantized weld buckets, accumulates angle-weighted neighboring face normals, aligns them with SDF gradients, and writes tangents before packing the 32B runtime stream.

- Final Burst payload kernels sanitize their own vector inputs: triplanar UVs, AO nearest sampling, LOD snap, and UV packing all fail closed to finite fallback data.

- Profile setup finite-vaccinates non-CSV editor inputs before jobs are scheduled: radius, height, frequency, amplitude, ridged/voronoi weights, `IsoLevel`, `GlobalQualityWeight`, and `SectorAup` are routed through finite fallbacks before SDF, AUP hash, AO, and LOD math. AUP zero lanes are canonicalized before deterministic seed hashing so `0` and `-0` do not diverge.

- Editor bake black-box rows are fixed at 64 bytes and held in a 300-entry ring. Fault dump path is `Docs/AgentLogs/Dump_SHINOBU_208.bin`; this is editor diagnostics, not runtime state.

- Non-asset bake probes destroy transient LOD mesh objects after metrics are recorded; only saved assets retain mesh ownership. LOD construction and asset-save failure paths now also destroy already-built unsaved transient meshes, delete newly created partial asset files, preserve references that had already transferred to pre-existing `AssetDatabase` assets, and log cleanup failures without replacing the original save exception.

- Mesh upload/validation failure destroys the newly created transient `Mesh` before leaving `CreateUnityMesh`; successful return explicitly transfers ownership to the asset/save path.

- Generated prefabs are no longer emitted by this lane. Runtime consumers must use static mesh assets plus the binary manifest, not generated GameObjects or `LODGroup` wrappers.

- Generated meshes intentionally do not add `MeshCollider`.

- Netcode rollback, Merkle hashing, and `StateRingBuffer` must not hash baked mesh vertex or index buffers every frame.

- `GlobalQualityWeight` is a continuous `smoothstep` curve over bake math: SDF noise frequency, noise amplitude, fractional octave contribution, Voronoi/ridged contribution, AO ray budget, AO step count, AO range, UV scale, LOD budgets, and collapse size.

- Runtime LOD transition distances may also be shifted by continuous `GlobalQualityWeight`; the generator does not author binary quality forks.

- Compile-wall boundary: GeologyForge is isolated by `Hecton8.World.OfflineGeology.Editor.asmdef`, Editor-only, unsafe-enabled, and references only `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`.

- Designer CSV profiles include persisted `iso_level` density threshold tuning. The parser skips an optional UTF-8 BOM, validates the exact supported header schema before row parsing, reports row-1/column diagnostics for header mismatches, and keeps old no-`iso_level` layouts valid by detecting the header token before consuming the column.

- CSV profile ingestion fails closed on malformed authoring data: existing empty, oversized above `MaximumCsvBytes=4194304`, short-read, or length-changing CSV files throw `CsvErrorFileSize=1008`, row column counts are validated, strict numeric byte readers reject invalid terminators, empty cells, and signed/unsigned integer overflow, and positive-only physical fields throw with stable numeric error codes plus row/column/field context instead of falling back to defaults. Missing CSV files still use the default mock profile for editor/CI bootstrap. Sector AUP cells use the double parser, not the float parser.

## First-20-Minutes Route Impact

This removes static geology topology generation from the route budget and buys readable cave/seabed silhouette in the Copper Wire route without adding runtime Marching Cubes stalls.

## Verification

Current evidence is static source only. Unity import, bake execution, mesh inspector validation, Frame Debugger, GCMonitor, and player-route proof are pending.

Static black-box source is present: SDF, extraction, attribute, AO, and serialization stages write `GeologyBakeTelemetryEntry` rows. Non-finite stage timing and exceptions dump the ring to `Docs/AgentLogs/Dump_SHINOBU_208.bin`. No dump file is expected until a fault path is exercised.

Static normal-weld source is present: `BuildNormalBucketJob` writes transient `NativeParallelMultiHashMap<ulong,int>` buckets and `CalculateSmoothNormalsJob` consumes the buckets with `[NoAlias]` fields and raw `GeologyRawVertex*` mutation. Unity import/Burst Inspector proof remains pending.

Static manifest source is present: `GeologyMeshManifestHeader` validates at 64 bytes and `GeologyMeshManifestRecord` validates at 128 bytes. `BoundsExtents` occupies bytes 60..71, so no explicit padding field exists there; the first 8-byte GUID lane starts aligned at byte 72. The layout self-audit opens `.h8geom` with `FileShare.Read`, rejects post-parse length drift, rejects non-finite `SectorAup`, `BoundsCenter`, or `BoundsExtents` lanes, requires manifest GUIDs to resolve under `Assets/_Project/BakedGeometry/Geology`, rejects duplicate GUIDs, and fails when top-level output meshes are unmanifested. Orphan mesh accounting is unconditional against the manifest GUID set, so an empty/missing manifest reports every top-level mesh as unmanifested. Unity import and runtime BRG consumption proof remain pending.

Raw geology binary payloads are explicitly little-endian. The writer fails fast on a non-little-endian host instead of emitting native-endian `.h8geom` or dump bytes.

Static vertex-layout application source is present: `GeologyVertexLayoutValidator.ApplyVertexBufferParams()` keeps the descriptor array private and applies it directly during mesh upload. The old per-upload layout-copy accessor is absent. Unity import/profiler proof remains pending.

Static quality-weight source is present: `GenerateMockFractalNoiseJob` takes `GlobalQualityWeight` directly, and both full bake and SceneView preview pass the same profile scalar. Unity bake timing proof remains pending.

Static async-facade source is present: `GeologyForgeWindow` and `BakeCsvProfilesMenu` start `BakeProfilesAsync`, which advances through profile variations on `EditorApplication.update`, reports progress through the UI Toolkit progress bar, and exposes `Cancel Bake`. The old public synchronous batch path has been removed from owned source. Unity Editor responsiveness/cancel proof remains pending.

Static async progress source is present: `TickAsyncBake` uses static progress-bar text instead of formatting profile/variation strings inside the editor update hook. Unity Profiler allocation proof remains pending.

Static variation-count guard source is present: `SanitizeVariationCount()` feeds both `SanitizeProfile()` and `CountTotalBakes()`, and aggregate async totals saturate before integer wrap. Unity malformed-CSV execution proof remains pending.

Static variation-facade source is present: `GeologyForgeConstants.MaximumVariations` is shared by the generator and UI Toolkit field resolution. Unity Editor field proof remains pending.

Static CSV variation-ceiling source is present: CSV `variations` parsing clamps through `GeologyForgeConstants.MaximumVariations`, matching UI field resolution, async total math, and generator execution. Unity malformed-CSV execution proof remains pending.

Static async-result preallocation source is present: `BakeProfilesAsync` computes `_asyncTotalBakes` before result-list allocation, `ResolveAsyncResultCapacity()` caps preallocation at `MaximumAsyncResultPreallocation=5000`, and the old `profiles.Count * 4` capacity path is absent. Unity Profiler allocation proof remains pending.

Static editor request/preview source is present: `GeologyForgeWindow` reuses `_bakeRequestProfiles` for bake buttons, and `GeologyForgePreview.Build()` performs deterministic two-pass candidate sampling into the bounded preview buffer. Unity Editor allocation/timing proof remains pending.

Static caller-owned CSV profile source is present: `GeologyProfileCsv.LoadProfiles(List<GeologyBakeProfile>)` is the only CSV ingestion API, fills caller-owned storage, the window reload path loads directly into `_profiles`, and the menu bake path reuses `_menuProfiles` before async snapshotting. Unity Profiler allocation proof remains pending.

Static CSV fail-closed source is present: missing profile files use one deterministic fallback, while existing header-only or blank data files throw `CsvErrorNoProfiles=1009`; `TryReadProfile` no longer skips a second line after `sector_z`.

Static asset-edit scope source is present: `AssetDatabase.StartAssetEditing()` is not opened in `TickAsyncBake`; it is opened only inside `SaveMeshesAndManifest` around the three `SaveMeshAsset` calls and closed in `finally` before manifest GUID reads. Full per-job staged scheduling proof remains pending.

Static async-finish hardening source is present: `BakeProfilesAsync` assigns runner state only after local snapshot/allocation succeeds, `FinishAsyncBake` stops the update runner, attempts artifact writes/progress notification, and clears all static async state in `finally`. Unity exception-path proof remains pending.

Static exception-path finish isolation source is present: setup/update catch blocks call `TryFinishAsyncBake(true)`, so a cleanup/report/progress callback failure is logged without replacing the original bake/setup exception. Normal finish and explicit cancel still call `FinishAsyncBake` directly. Unity exception-path proof remains pending.

Static empty-manifest guard source is present: public single-bake and async finish paths only write `.h8geom`/`SaveAssets` when manifest records exist; bake reports still write when metrics exist. Manifest self-audit rejects no-output audits, zero triangle counts, non-positive extents, and GUIDs that cannot resolve back to mesh assets.

Static artifact-failure hardening source is present: `CreateUnityMesh` destroys transient meshes on failed upload/validation, LOD construction/save failures destroy unsaved transient meshes and delete newly created partial asset files, strict GUID hydration throws on malformed asset GUIDs, and `FinishAsyncBake` skips manifest/report writes for zero-output cancels. Unity exception/cancel proof remains pending.

Static atomic-write source is present: `.h8geom`, dump, bake report, layout audit, and scanner report writers use temp files and replacement with backup preservation. Unity IO-fault proof remains pending.

Static scanner time-slice source is present: non-batch `RuntimeMeshGenerationScanner.ScanAndWriteReport()` starts `StartAsyncScan`, advances directory discovery through `ExpandNextAsyncDirectory()`, advances file scans on `TickAsyncScan`, and cleans up through `CancelAsyncScan` or completion. Unity Editor progress/cancel proof remains pending.

Static editor-lifetime source is present: `TryStartBake` handles rejected async starts, and `GeologyForgePreview.Shutdown` removes `SceneView.duringSceneGui` on window disable. Unity Editor callback proof remains pending.

Static preview fence source is present: `GeologyForgePreview.Build()` imports `Unity.Jobs` and runs the bounded preview kernel through `Run(count)` instead of `Schedule(count, 64).Complete()`. Unity Editor preview timing proof remains pending.

Static preview hook lifetime source is present: `GeologyForgePreview.Build()` registers `SceneView.duringSceneGui` only after successful point generation. Unity exception-path proof remains pending.

Static scanner reusable-buffer source is present: `RuntimeMeshGenerationScanner` owns static readonly async queues/findings, clears them through `ClearAsyncScanState()`, and writes reports before clearing on finish. Unity Profiler allocation proof remains pending.

Static bounds-vaccination source is present: `CalculateBounds` initializes min/max from the first finite `GeologyRawVertex.Position`, skips non-finite rows, and falls back to a finite 1m bound if no valid position exists. Unity mesh inspector and manifest audit proof remain pending.

Static Burst finite-guard source is present: final UV/AO/LOD/pack kernels sanitize non-finite normals, positions, sample positions, snapped positions, and UVs before serializing mesh data. Unity/Burst compile proof remains pending.

Static profile finite-guard source is present: `SanitizeProfile` replaces non-finite profile scalar and AUP lanes before bake jobs are scheduled and canonicalizes zero AUP lanes before seed hashing. Unity malformed-profile execution proof remains pending.

Static extraction-LUT source is present: `SdfCellVertexCountJob` and `SdfToMeshExtractionJob` derive the same tetra case index and edge sequence through `GeologyTetraExtractionLut`. Burst Inspector/vectorization proof remains pending.

Static CSV iso-level source is present: `GeologyProfileCsv` reads `iso_level` when the header contains it and falls back to `0` for older layouts. Unity Editor facade proof remains pending.

Static CSV schema guard source is present: `GeologyProfileCsv` validates header tokens before row parsing and throws exact row-1/column diagnostics on reordered/missing columns instead of silently corrupting profile fields. Unity Editor error-path proof remains pending.

Static CSV row/cell guard source is present: `GeologyProfileCsv` validates file size/stability, per-row column count, and strict numeric cells before profile hydration, including signed/unsigned integer overflow rejection, sector-lane double parsing, and numeric error-code reporting. Unity malformed-row execution proof remains pending.

On-demand layout audit source is present: `HECTON-8/Geology Forge/Run Layout Self Audit` validates generated mesh streams and `geology_mesh_manifest.h8geom`, then writes `Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json`. Current report is a placeholder until a Unity Editor bake/audit is executed.

`Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` currently uses scanner schema v2, project-wide runtime script scope, and reports `findingCount=137`, `actionableFindingCount=131`, `proceduralMaterialCloneFindingCount=66`, and `runtimeMeshAllocationsEradicated=false`. These remaining runtime topology/material-clone sites are outside the GeologyForge render-bake lane and require owner-specific migration before project-wide eradication can be claimed.
