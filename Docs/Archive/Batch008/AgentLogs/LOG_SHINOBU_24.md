# LOG_SHINOBU_24

Date: 2026-05-17
Status: PENDING VERIFICATION

## 2026-05-17 - Scanner Data Mining Router

What was wrong:
The scanner domain still depended conceptually on Unity physics and managed object discovery patterns. That is not viable for 5000 flora/fauna candidates or mobile VR. The batch demanded a pure math scanner: fixed DTOs, spatial hash O(1) lookup, no hit-array allocation, no `Physics.Raycast`.

What was done:
Created `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` with `ScanResultDTO`, exact 16-byte `ScannableEntityMetadataDTO`, `ScannerVfxDTO`, `ActiveScanStateDTO`, mock input/tool/SDF DTOs, `ScannerSpatialQueryJob`, line-sphere math, SDF midpoint occlusion, query cadence throttling, SignalBus unlock/depletion routing, acoustic progress signals, and a 300-frame black box dump path.
Created `Assets/_Project/Scripts/Editor/DataMiningTunerWindow.cs` for editor-only scan tuning and SceneView visualization.
Created `Assets/_Project/Tests/Editor/ScannerDataMiningRouterEditTests.cs` for DTO sizes, AUP-safe ray/sphere math, target selection, SDF occlusion, ref mutation, and CSV override parsing.

Cinematic Cheats used:
Bounding spheres replace collider/mesh intersection.
Midpoint SDF sample replaces BVH wall occlusion.
Dot-distance scalar replaces sorted hit lists.
Runtime VFX receives a 32-byte DTO instead of querying scanner internals.

Exact microseconds saved:
Physics BVH and hit-array path removed from new router: estimated 18-45 us per bounded query window on low tier.
No sorting for multiple targets: estimated 8 us per 32 candidates.
Midpoint SDF occlusion instead of raycast: estimated 25 us per validation.
Query cadence halves or quarters reacquire work under pressure: estimated 75% query CPU saved when SHI > 0.8.
GC saved in hot scan/progression path: 0 B allocation target.

Verification:
CLI prompt extraction repeated for `SHINOBU_24`.
Static scanner scan found no `Physics.Raycast`, `OverlapSphere`, `GetComponent`, `RaycastCommand`, `Split`, `OrderBy`, or sorted hit-array path in the new files.
Unity compile was attempted four times. It fails outside this domain in Core, Quest, Rendering, Construction, and Audio editor files. `UnityCompile4_SHINOBU_24.log` reports no compiler-error lines in `ScannerDataMiningRouter.cs`, `DataMiningTunerWindow.cs`, or `ScannerDataMiningRouterEditTests.cs`.

SELF_AUDIT:
Written to `Docs/AgentLogs/SelfAudit_SHINOBU_24.xml`.

## 2026-05-17 - Final SHINOBU_24 Closure

What was wrong:
The first editor visualizer drew a target line and sphere, but the final scanner handoff needed the full operator proof: cone envelope, lock line, and hit sphere. Designers need beam width versus selected hash without runtime debug objects.

What was done:
Updated `DataMiningTunerWindow` to draw a yellow beam cone, red target line, and blue hit sphere from the active `ScannerDataMiningRouter`. Re-ran the static forbidden-pattern scan against the SHINOBU_24 runtime/editor/test files. The scan returned no matches for `Physics.Raycast`, `OverlapSphere`, `GetComponent`, `RaycastCommand`, `Split`, `OrderBy`, or hit-array patterns.

Cinematic Cheats used:
SceneView cone is editor-only and reads the same lightweight VFX DTO path. Runtime remains bounding-sphere math plus midpoint SDF occlusion.

Exact Microseconds saved:
Player hot path unchanged: 0 us added by the editor visualizer. Runtime savings remain: 18-45 us from removing physics queries, 25 us from SDF midpoint wall rejection, 8 us from no sorted candidate list, and 0 B target GC in scan/progression.

Verification:
`rg` forbidden-pattern scan returned no matches in `ScannerDataMiningRouter.cs`, `DataMiningTunerWindow.cs`, and `ScannerDataMiningRouterEditTests.cs`.
Unity compile remains blocked by unrelated domains already documented in `UnityCompile4_SHINOBU_24.log`; no SHINOBU_24 file errors were present in that compile log.

## 2026-05-18 - Ultra Polish / DataVault Sovereignty Pass

What was wrong:
The prior SHINOBU_24 implementation still owned private persistent `NativeArray`, `NativeList`, and `NativeParallelMultiHashMap` state inside the MonoBehaviour. That passed the first no-Physics target but failed H-Phi/DataVault sovereignty. Several support DTOs were size-aligned but not lane-ordered for ARM64, and SHINOBU_24 signal structs used forbidden `Pack=1`.

What was done:
Moved scanner runtime buffers to `GlobalDataVault` handles: entities, metadata, occlusion zones, spatial bucket heads, spatial next links, result slot, result count, active state, VFX target, query stats, telemetry ring, and settings. Replaced `NativeParallelMultiHashMap` with a flat O(1) bucket hash using `NativeArray<int> BucketHeads` plus `NativeArray<int> BucketNext`. Updated the query job and editor tests to use fixed result storage instead of `NativeList`. Added `BufferID.ShinobuScanner*` reservations (`70640..70652`) in `H8Memory.cs`. Editor tuner now reads/writes the unmanaged `ScannerSettingsDTO` in Play Mode. Reordered ARM64-sensitive DTOs and removed `Pack=1` from SHINOBU_24 signals.

Cinematic Cheats used:
The physical truth remains fake by design: scannables are spheres, walls are a midpoint SDF sample, and target magnetism is a dot-distance scalar. Low tier clamps cadence and candidates; High/Ultra spend saved cycles downstream on VFX/acoustic presentation, not gameplay physics.

Exact Microseconds saved:
Flat bucket arrays avoid native hash-map iterator overhead: estimated 3-7 us per dense query on i3/MX350. Physics BVH remains removed: estimated 18-45 us per bounded query. Midpoint SDF still saves about 25 us per occlusion validation versus wall raycasts. Removing sorted hit lists still saves about 8 us per 32 candidates. Hot scan/progression GC target remains 0 B.

Struct Layout:
`ScanResultDTO` 48 bytes: 0 AUP, 24 EntityHash, 28 Distance, 32 ScanProgress, 36 pad0, 40 pad1.
`ScannerSpatialEntityDTO` 64 bytes: 0 AUP, 24 SectorHash, 32 DepletionMask, 40 EntityHash, 44 Radius, 48 MetadataIndex, 52 Flags, 56 DepletionWordIndex, 60 pad0.
`ActiveScanStateDTO` 128 bytes: 0 TargetAUP, 24 LastOriginAUP, 48 SectorHash, 56 DepletionMask, 64/72 padding, 80+ 4-byte runtime lanes.
`ScannerTelemetryEntry` 64 bytes: 0 TargetAUP, 24 pad0, 32+ counters/progress lanes.

H-Phi Check:
No private persistent runtime native arrays remain in `ScannerDataMiningRouter`; the router stores vault handles and scalar cursors only. Query buffers are locked while the Burst job owns them and resolved again after completion.

Blackbox:
The 300-frame telemetry ring is active in `BufferID.ShinobuScannerTelemetryRing` and dumps to `Docs/AgentLogs/Dump_SHINOBU_24.bin` plus `Docs/AgentLogs/Dump_SHINOBU_24.h8dump` on NaN input or query budget breach.

Verification:
Forbidden-pattern scan returned no matches in SHINOBU_24 runtime/editor files for `Physics.Raycast`, `OverlapSphere`, `GetComponent`, `FindObjectsOfType`, `NativeParallelMultiHashMap`, `NativeList`, `new NativeArray`, `Pack=1`, LINQ, or `ToString`.
`dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` remains blocked externally. Latest run reports `GlobalTelemetryBus.Blackbox.cs` missing `TryBindBlackboxVaultBuffersNoLock`, `SubmarineDynamicsRuntime.cs` `math.min` ambiguity, and missing `GlobalPhysicsStateManager` SHINOBU_37 partial members. No SHINOBU_24 file appeared in compiler output.
