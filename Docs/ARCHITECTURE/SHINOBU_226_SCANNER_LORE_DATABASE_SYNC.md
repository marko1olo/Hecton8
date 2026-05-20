# SHINOBU_226 Scanner Lore Database Sync

Authority: scanner hot path uses 32-bit FNV-1a target hashes, Vault-owned DTO buffers, and unmanaged unlock bitmasks. Authored strings remain cold editor/authoring input only.

Runtime route:
- `ScannerDataMiningRouter` resolves `IDataVault` at boot and stores only `VaultGenerationHandle<T>` descriptors.
- Scanner ray origin/forward are sourced from cached `PlayerRuntimePoseSnapshot`; active acquisition fails closed without that pose snapshot or a finite non-zero forward vector.
- Scan target acquisition runs as Burst deterministic jobs over `ScannerSpatialEntityDTO`, metadata, SDF occlusion zones, and spatial hash buckets.
- Completion writes `ScanProgressDTO` and `ScannerEncyclopediaStateDTO` bitmasks through `UpdateScanProgressJob` and `EvaluateScanCompletionJob`.
- PDA/UI continues to receive hash-only signals; no direct concrete PDA runtime dependency was added.
- Editor-only live debugging reads the same Vault rows in `OnDrawGizmos` and draws AUP-local blue/yellow/green wire spheres without runtime debug GameObjects or text labels.
- Runtime scanner/PDA frame IDs route through `TimeSliceScheduler.CurrentFrameId`; no scanner-domain `Time.frameCount` read remains in `ScannerDataMiningRouter`, `ScannerTool`, `ScannableTarget`, or `PDAEncyclopediaStreamer`.

Vault buffers:
- Existing scanner buffers: `70640..70652`.
- Added scanner buffers: `70657 ShinobuScannerScanProgress`, `70658 ShinobuScannerLoreIndex`, `70659 ShinobuScannerEncyclopediaState`.

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
- `ScannerLoreDatabaseSyncTunerWindow` exposes Vault mask/telemetry readout and direct Unlock All / Lock All writes to `ScannerEncyclopediaStateDTO`.
- Runtime source scan for `ScannerDataMiningRouter.cs` returns 0 hits for Unity frame/time/random reads, direct Transform pose reads, raw job completion, legacy Vault handles, hot managed collection/parser patterns, and `Pack=1`.
- Scanner/PDA bridge scan returns 0 hits for `Time.frameCount` after routing frame stamps through dispatcher frame state.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted only after CPU gate opened; it is blocked by unrelated dependency-wall errors. Generated csproj coverage does not include the router/editor/PDA files, so Unity import remains the required proof path for those files.
