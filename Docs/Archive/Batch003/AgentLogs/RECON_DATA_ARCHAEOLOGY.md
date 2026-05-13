# RECON_DATA_ARCHAEOLOGY

Status: PENDING VERIFICATION
Agent: DATA_ARCHAEOLOGY

## ScannerTool.cs Recon

Command: `Select-String -LiteralPath 'Assets\_Project\Scripts\ScannerTool.cs' -Pattern 'Instantiate','string'`

Findings:
- `Instantiate` matches: 0.
- `string` matches: 256.
- Existing string sites are legacy/cold scanner presentation paths: constants, operational summary caches, prefixed scanner string caches, old scientific text formatting, and binary/UI save helpers outside the new data archaeology hold scan path.

Hot path check:
- `ToolTick` calls `UpdateScientificScanning`.
- The slice from `private void UpdateScientificScanning` to `private bool TryResolveScientificSpatialContact` was scanned for `Instantiate`, `string`, `new string`, `string.Create`, `.ToString(`, and `Format(`.
- Matches in that slice: 0.

New DATA_ARCHAEOLOGY path:
- `QueueScientificRaycast` uses `RaycastCommand` and integer request ids.
- `ConsumeScientificRaycastHit` reads `ScannableTarget.EntityHash`.
- `UpdateScientificScanning` holds only floats/hashes and forwards to `DataArchaeologyRuntime`.
- `PDADataArchaeologyDecryptLabel` and `DataArchaeologyRuntime` returned 0 hits for `new char[`, `new string`, `.text =`, `SetText(`, `string.Create`, and `ToString(`.

Conclusion: ScannerTool still contains pre-existing string-heavy reporting code, but the new scanner update/hold path for DATA_ARCHAEOLOGY does not generate strings or instantiate UI prefabs.
