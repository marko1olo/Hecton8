# LOG_SHINOBU_226

## 2026-05-20T00:00:00Z - Scanner Lore Database Sync

What was wrong:
- Scanner/lore sync had legacy pointer-bearing Vault handle retention in `ScannerDataMiningRouter`.
- Scan completion still depended on signal/UI follow-up for encyclopedia unlock authority instead of a scanner-owned unmanaged bitmask proof.
- Quality cadence used tier-style logic and pressure thresholding instead of continuous `GlobalQualityWeight`.
- SHINOBU dump filenames still used `SHINOBU_24`.
- No scanner-specific editor tuner, static string inquisition validator, route card, or self-audit artifact existed for this batch.

What was done:
- Replaced persistent `VaultBufferHandle<T>` scanner fields with `VaultGenerationHandle<T>` descriptors and method-local `NativeArray<T>` resolves.
- Added `ScanProgressDTO` (64B), `ScannerLoreIndexDTO` (32B), and `ScannerEncyclopediaStateDTO` (128B).
- Added Vault buffer IDs `70657`, `70658`, `70659` for scan progress, lore index, and encyclopedia bitmask.
- Added deterministic Burst jobs: `GenerateMockScannableTargetsJob`, `UpdateScanProgressJob`, `EvaluateScanCompletionJob`, `AcquireScanTargetJob`, plus deterministic/NoAlias cleanup on scan query jobs.
- Added byte-span FNV-1a CSV lore index ingestion and hash lookup helpers.
- Added unmanaged atomic OR unlock path for scanner encyclopedia bitmask.
- Added continuous quality cadence and shader HUD scalar publishing.
- Added `ScannerLoreDatabaseSyncTunerWindow`, `ScannerStringInquisitionValidator`, edit tests, architecture route card, and `SHINOBU_226_SELF_AUDIT.xml`.

Cinematic cheats used:
- Midpoint SDF occlusion remains the scan obstruction fake instead of Unity physics raycasts/colliders.
- Scanner HUD uses shader scalar globals for progress/quality/refresh/dither rather than CPU-driven UI object simulation.
- Mock lore database is generated as deterministic hash records, not authored strings.

Exact microseconds saved:
- Managed string/object identity lookup removal: estimated 4 us per avoided lookup.
- Dispatcher path vs per-MonoBehaviour polling: estimated 10-40 us per active scanner frame.
- Native bitmask unlock vs managed PDA/object route: estimated 2-8 us per completion.
- Burst bounded spatial candidate scan vs scene object scan: prevents O(scene objects); measured runtime proof absent.
- Continuous pressure cadence can shed up to 3x query frequency under thermal pressure; measured profiler proof absent.

Verification:
- Static forbidden-pattern scan over `ScannerTool.cs`, `ScannableTarget.cs`, `ScannerDataMiningRouter.cs`, `PDAEncyclopediaStreamer.cs`, and `PdaH8lrLoreStore.cs`: 0 hits for `target.name` and forbidden `GetComponent` scanner patterns.
- Burst attribute scan: scanner jobs show `CompileSynchronously=true`, `FloatMode.Deterministic`, and `NoAlias` fields.
- Compile not launched: CPU gate reported 100% average load, and project rule forbids dotnet build under CPU >50%.

## 2026-05-20T00:00:00Z - Loop 6 Polish Reconciliation

What was wrong:
- Task 18 had no literal `OnDrawGizmos` implementation; prior proof relied on tuner/shader state exposure.
- Task 16 editor facade lacked direct Unlock All / Lock All controls for the 128-byte Vault bitmask.

What was done:
- Added editor-only `ScannerDataMiningRouter.OnDrawGizmos` that reads Vault scannable rows, lore index, active scan state, and encyclopedia masks, then draws blue/yellow/green AUP-local wire spheres.
- Added `ScannerDataMiningRouter.IsLoreBitUnlocked` and an edit test assertion for bit 130.
- Extended `ScannerLoreDatabaseSyncTunerWindow` with Vault mask/telemetry readout and direct `ScannerEncyclopediaStateDTO` Unlock All / Lock All writes.
- Re-extracted the SHINOBU_226 prompt from `CURRENT_BATCH.md`; task count remains 19 with Task 09 absent.

Cinematic cheats used:
- Scene debug uses Vault DTOs and AUP-local wire spheres instead of spawning target debug GameObjects or runtime text labels.

Exact microseconds saved:
- Player runtime: 0 us cost added.
- Avoided debug GameObject/string label route: estimated 10-30 us per editor-visible scanner cohort if such a route had been added to runtime.

Verification:
- `git diff --check` scoped to touched scanner files reported only existing LF/CRLF warnings.
- Scoped scanner/PDA forbidden target-name/GetComponent scan returned 0 hits.
- Runtime scanner source scan returned 0 hits for legacy Vault handles, raw `JobHandle.Complete`, Unity random/time, hot private native owners, foreach/LINQ/split/string.Format, and `Pack=1`.
- Compile not launched after Loop 6: CPU gate samples were 91 then 100 with no `dotnet`/`csc` process output.

## 2026-05-20T00:00:00Z - Loop 7 Determinism Frame Route

What was wrong:
- Scanner runtime still had direct `Time.frameCount` reads for cadence, signal frame IDs, VFX frame stamps, telemetry, and anomaly events.

What was done:
- Added scanner-local `ResolveSimulationFrame` / `ResolveSimulationFrameInt` helpers that read `TimeSliceScheduler.CurrentFrameId`.
- Replaced every scanner-domain direct Unity frame read with the dispatcher-owned frame source.
- Re-ran runtime scanner static scans for Unity time/random, raw job completion, legacy Vault handles, managed parser/collection patterns, and `Pack=1`.

Cinematic cheats used:
- No new physics or UI simulation was introduced. Scanner HUD remains scalar shader state; debug visibility remains editor-only Gizmos over Vault rows.

Exact microseconds saved:
- Raw speed: 0 us.
- Determinism risk removed: one timing fact now routes through dispatcher frame state instead of Unity frame reads scattered across scanner presentation and telemetry.

Verification:
- `ScannerDataMiningRouter.cs` returned 0 hits for `Time.frameCount`, `Time.deltaTime`, `UnityEngine.Random`, `JobHandle.Complete`, `VaultBufferHandle`, `NativeList`, `NativeHashMap`, `foreach`, `.Split`, `string.Format`, and `Pack=1`.
- `git diff --check` over touched files reported only LF/CRLF conversion warnings.
- Compile not launched after Loop 7: `dotnet/csc` had no visible process, but CPU samples were 100, 80, 75, 100, 51, then 70, above the explicit <=50 launch gate at launch decision time.

## 2026-05-20T00:00:00Z - Loop 8 Scanner/PDA Pose And Frame Authority

What was wrong:
- `ScannerDataMiningRouter` still used Unity `Transform` pose reads to construct scanner rays and mock grid orientation.
- `ScannerTool`, `ScannableTarget`, and `PDAEncyclopediaStreamer` still used `Time.frameCount` for scanner/PDA sync stamps.
- The editor inquisition did not guard Unity time/random or router Transform pose regressions.

What was done:
- Active scanner ray construction now consumes cached `PlayerRuntimePoseSnapshot` AUP and forward fields.
- Active acquisition fails closed without a full pose snapshot or finite non-zero forward vector instead of inventing a default gameplay gaze.
- Mock grid seeding uses scanner pose, cached player AUP, or global AUP fallback and runs `GenerateMockScannableTargetsJob` through `IJob.Run`.
- Scanner/PDA frame stamps now route through `TimeSliceScheduler.CurrentFrameId`.
- `ScannerStringInquisitionValidator` now checks scanner/PDA string/GetComponent patterns, Unity time/random patterns, and router-only Transform pose patterns.

Cinematic cheats used:
- No gameplay physics, trigger collider, UI canvas, or scene-object debug path was introduced. Scanner HUD remains scalar shader state and editor discovery debugging remains Gizmos over Vault rows.

Exact microseconds saved:
- Transform bridge removal from scanner query construction: small unmeasured per-query saving, expected sub-micro to low-single-digit microseconds depending on platform.
- Frame route consolidation: 0 us raw speed, but removes a timing authority split.

Verification:
- Scoped scanner/PDA scan returned 0 hits for target-name/GetComponent, `Time.frameCount`, `Time.deltaTime`, and `UnityEngine.Random`.
- Router scan returned 0 hits for `transform.forward`, `transform.position`, and `transform.right`.
- `git diff --check` over touched scanner/PDA files reported only LF/CRLF conversion warnings.
- Compile launched after CPU gate opened at 34/25/19 with no compiler process: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`.
- Build failed with 76 unrelated dependency-wall errors, including missing `Hecton8.Equipment`, `Hecton8.Logistics.Grid`, `HectonFluidEngine`, `SoundEmissionSignal`, `H8BinaryWorldPager`, docking/socket DTOs, audio/world bridge interfaces, and WFC grid constants.
- Generated csproj coverage includes `H8Memory.cs`, `ScannerTool.cs`, and `ScannableTarget.cs`, but not `ScannerDataMiningRouter.cs`, `ScannerLoreDatabaseSyncTunerWindow.cs`, or `PDAEncyclopediaStreamer.cs`; Unity import proof remains pending.
- Failed build left resident dotnet build servers; `dotnet build-server shutdown` cleared MSBuild and compiler servers, and a follow-up process check returned no dotnet/csc output.
