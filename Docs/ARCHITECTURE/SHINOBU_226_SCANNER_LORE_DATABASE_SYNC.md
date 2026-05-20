# SHINOBU_226 Scanner Lore Database Sync

Authority: scanner hot path uses 32-bit FNV-1a target hashes, Vault-owned DTO buffers, and unmanaged unlock bitmasks. Authored strings remain cold editor/authoring input only.

Runtime route:
- `ScannerDataMiningRouter` resolves `IDataVault` at boot and stores only `VaultGenerationHandle<T>` descriptors.
- Scan target acquisition runs as Burst deterministic jobs over `ScannerSpatialEntityDTO`, metadata, SDF occlusion zones, and spatial hash buckets.
- Completion writes `ScanProgressDTO` and `ScannerEncyclopediaStateDTO` bitmasks through `UpdateScanProgressJob` and `EvaluateScanCompletionJob`.
- PDA/UI continues to receive hash-only signals; no direct concrete PDA runtime dependency was added.

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

