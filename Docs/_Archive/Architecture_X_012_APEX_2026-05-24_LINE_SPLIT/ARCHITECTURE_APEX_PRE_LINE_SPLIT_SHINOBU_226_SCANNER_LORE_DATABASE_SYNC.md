# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_226_SCANNER_LORE_DATABASE_SYNC.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_226 Scanner Lore Database Sync

Authority: scanner hot path uses 32-bit FNV-1a target hashes, Vault-owned DTO buffers, and unmanaged unlock bitmasks. Authored strings remain cold editor/authoring input only.

Runtime route:

- `ScannerDataMiningRouter` resolves `IDataVault` at boot and stores only `VaultGenerationHandle<T>` descriptors.

- Scanner ray origin/forward are sourced from cached `PlayerRuntimePoseSnapshot`; active acquisition fails closed without that pose snapshot or a finite non-zero forward vector.

- Lore row publication reads AUP from the spatial owner via `WorldSpatialHashGrid.TryGetAbsolutePosition`; missing or non-finite spatial AUP fails closed by clearing the row hash instead of synthesizing a runtime-origin fallback.

- Scan target acquisition runs as Burst deterministic jobs over `ScannerSpatialEntityDTO`, metadata, SDF occlusion zones, and spatial hash buckets.

- Completion writes `ScanProgressDTO` and `ScannerEncyclopediaStateDTO` bitmasks through `UpdateScanProgressJob` and `EvaluateScanCompletionJob`.

- PDA/UI continues to receive hash-only signals; no direct concrete PDA runtime dependency was added.

- ScannerTool discovery pulses publish only uint FNV-1a hashes from cold-baked numeric identities; no `item.*` or `module.*` strings are constructed or folded during scan processing.

- Loop 12 correction: scanner pulse no longer reads `item.PersistentId`, `data.PersistentId`, `scannable.EntryCategory`, or lazy `EntityHash`. Pickup reads `ItemData.PersistentHashId`; module reads cold-cached `ModuleMarker.ScannerEntryHash`; scannables read `CachedEntityHash` and `CachedCategoryKind`.

- Loop 13 correction: `ScannerDataMiningRouter` hot ticks read a cached non-owning `ScannerVaultViews` snapshot through `TryReadVaultViews`; Vault handle fan-out is confined to `TryRefreshVaultViewsCold` during owner setup.

- Loop 15 correction: `ScannerTool`, `PDAEncyclopediaStreamer`, and `ScannableTarget` now cache non-owning Vault views after cold/owner refresh; scanner black-box, PDA buffer, and lore entity read surfaces no longer resolve handles from hot readers.

- Loop 15 signal route: `ToolAcousticSignal`, `ScanCompleteSignal`, and `ResourceDepletionDeltaSignal` publish through direct `SignalBus<T>.Push`. `AcousticPingSignal`, `ScannerToolActiveSignal`, `AnomalySignal`, and `CrashTelemetrySignal` remain explicit `GlobalSignals` bridge lanes until latest/dequeue consumers are migrated.

- Loop 16 correction: `PdaH8lrLoreStore` no longer re-resolves the Vault mirror handle per readable span. The cached mirror pointer is protected by `IDataVault.TryGetBufferGeneration` against the captured `VaultGenerationHandle<byte>.Generation`; mismatch fails closed.

- Loop 17 correction: `ScannerDataMiningRouter.OnDisable` no longer force-completes the amortized spatial query. Disable enters a nonblocking drain state, unregisters Fast/Slow lanes, keeps LateFrame only until `TryFinalizeScheduledQuery` observes natural completion, then unlocks query buffers and releases descriptors without processing stale scan results.

- Loop 18 correction: `PDAEncyclopediaStreamer` invalidates `_vaultReady` when cached Vault view generation checks fail; Tick/LateFrameTick re-enter `TryColdBootstrap` and fail closed if fresh non-owning views cannot be reacquired.

- Loop 18 correction: `ScannerTool.IsColliderOwnedByTarget` no longer reads `hitCollider.transform`, `target.transform`, or `Transform.IsChildOf`. `ScannableTarget` caches a runtime GameObject id in Awake/OnEnable and occlusion validation compares collider/attached-Rigidbody object ids.

- Loop 18 correction: scanner quality tier telemetry initializes to `Unknown` and no longer polls `GlobalRegistry.ScalabilityTier`; scanner presentation cost remains controlled by continuous `GlobalQualityWeight` curves.

- `ScannerTool` scientific black box and `PDAEncyclopediaStreamer` native state retain pointer-free `VaultGenerationHandle<T>` descriptors only. PDA still resolves method-local views; router hot ticks use the cached non-owning view snapshot described above.

- Editor-only live debugging reads the same Vault rows in `OnDrawGizmos` and draws AUP-local blue/yellow/green wire spheres without runtime debug GameObjects or text labels.

- Runtime scanner/PDA frame IDs route through `TimeSliceScheduler.CurrentFrameId`; scanner presentation/cooldown timestamps route through `SystemDispatcher.CurrentUnscaledTimeSeconds`. No scanner-domain direct Unity time/frame/random read remains in `ScannerDataMiningRouter`, `ScannerTool`, `ScannableTarget`, or `PDAEncyclopediaStreamer`.

Vault buffers:

- Existing scanner buffers: `70640..70652`.

- Added scanner tool black box: `70639 ShinobuScannerToolBlackBox`.

- Added scanner buffers: `70657 ShinobuScannerScanProgress`, `70658 ShinobuScannerLoreIndex`, `70659 ShinobuScannerEncyclopediaState`.

- PDA encyclopedia masks/state/metadata/telemetry/mock UTF8/H8LR mirror buffers use existing PDA `BufferID` constants through generation handles.

Layout:

- `ScanProgressDTO`: 64 bytes. `TargetHashID@0`, `CurrentProgress01@4`, `ScanRate@8`, `Flags@12`, `ScannerAUP@16`, `LastFrame@40`, `CompletedHash@44`, padding `48..63`.

- `ScannerLoreIndexDTO`: 32 bytes.

- `ScannerEncyclopediaStateDTO`: 128 bytes, 16 contiguous `ulong` mask words.

Scalability:

- Query cadence is driven by continuous `GlobalQualityWeight` and pressure curves, not tier switches.

- HUD cost collapses to scalar shader globals: progress, quality, refresh Hz, dither complexity.

Verification surface:

- `ScannerDataMiningRouterEditTests` covers layout offsets, FNV CSV ingestion, mock lore index generation, unmanaged unlock bit writes, and continuous cadence behavior.

- `ScannerStringInquisitionValidator` scans the scanner/PDA slice for forbidden hot-path string identity and `GetComponent` lookup patterns.

- Validator coverage also includes direct Unity time reads, direct Transform pose reads in scanner acquisition, managed formatting/parser/LINQ/list/array patterns, removed prefixed-string helper names, and string discovery overload calls. It writes `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json`; the shared `CONSTRUCTION_OPTIMIZATION_REPORT.json` is preserved unless absent or already SHINOBU_226-owned.

- `ScannerLoreDatabaseSyncTunerWindow` exposes Vault mask/telemetry readout and direct Unlock All / Lock All writes to `ScannerEncyclopediaStateDTO`.

- Runtime source scan for `ScannerDataMiningRouter.cs` returns 0 hits for Unity frame/time/random reads, direct Transform pose reads, raw job completion, legacy Vault handles, hot managed collection/parser patterns, and `Pack=1`.

- Scanner/PDA bridge scan returns 0 hits for `Time.frameCount` after routing frame stamps through dispatcher frame state.

- Scanner/PDA ownership scan returns 0 hits for `VaultBufferHandle`, persistent private `NativeArray` fields, and retained active UTF8 pointer fields in the scanner/PDA sync slice.

- PDA same-frame job scan returns 0 hits for `IJob`, `BurstCompile`, `.Execute()`, `Unity.Jobs`, `Unity.Burst`, `_mockLookupResultHandle`, and `MockLookupResultBufferId`.

- Loop 11 scan returns 0 hits for `string.Format`, `string.Create`, `.Split`, LINQ/list/array conversion patterns, `foreach`, removed prefixed-string builders, old module/pickup summary helpers, and `RaiseEntryDiscovered("`. Remaining discovery calls are uint overloads only.

- Loop 12 scan returns 0 hits for `ComputeLowerAsciiPrefixedFnvHash`, `AppendLowerAsciiFnv`, `FoldAsciiLower`, `ItemEntryPrefix`, `ModuleEntryPrefix`, `item.PersistentId`, `data.PersistentId`, `scannable.EntryCategory`, and `RaiseEntryDiscovered("` in the scanner/PDA sync slice.

- Loop 13 scan returns 0 hits for `TryResolveVaultViews`; hot router routes use `TryReadVaultViews`.

- Loop 14 removed scheduled completion jobs for one completed scan slot; only the amortized spatial query `.Schedule(` remains in the router.

- Loop 15 validator filters cold Vault refreshes, documented `GlobalSignals` bridge lanes, teardown completion, and valid amortized spatial scheduling after a line-level hit.

- Loop 17 runtime sweep returns 0 hits in scanner/PDA runtime files for direct Unity time/random, string formatting/discovery, legacy GetComponent identity, `VaultBufferHandle`, `.Complete(`, `forceComplete:true`, and `Pack=1`. Hot DTO/property sweep returns 0 `{ get; set; }` / get-only property hits in scanner/PDA target files; `ScientificScanSnapshot` is raw readonly fields.

- Loop 18 runtime sweep returns 0 hits in scanner/PDA runtime files for direct Unity time/random, string formatting/discovery, legacy GetComponent identity, `VaultBufferHandle`, `.Complete(`, `forceComplete:true`, `Pack=1`, Transform hierarchy ownership, direct `GlobalRegistry.ScalabilityTier`, binary minimum-quality helper, and discrete tier cadence overload patterns.
- `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json` currently records `blocked_findings = 0` after Loop 18 and includes the expanded Transform/scalability residual pattern list. The former H8LR mirror residual is guarded by a generation check instead of per-read `TryResolveHandle`, router teardown force-completion no longer appears in runtime source, and scientific occlusion ownership no longer traverses Transform hierarchy.

- Handoff: `WorldSpatialHashGrid.TryScheduleFarUnload` and `BuildAcousticDensityMap` still poll `GlobalRegistry.Player` from recurring world maintenance. That debt is outside SHINOBU_226 ownership; scanner use of the spatial grid is limited to the pure AUP accessor.

- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted only after CPU gate opened; it is blocked by unrelated dependency-wall errors. Generated csproj coverage does not include the router/editor/PDA files, so Unity import remains the required proof path for those files. Loop 16 and Loop 17 compiles were not launched because no dotnet/csc process output was visible but CPU sampled 100. Loop 18 compile was not launched because `Get-Process dotnet,csc` returned `NO_DOTNET_CSC`, but CPU sampled 82 then 100.
