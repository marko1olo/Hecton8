# Rationale_CORE_CHUNK_STREAMING

STATUS: PENDING VERIFICATION

## Decision 0 - Domain Boundary
Problem: The batch requires chunk residency, Addressables, pools, and AUP shift handling without mutating GameBootstrapper while 24 other agents may alter adjacent systems.
Solution: Add a self-contained `Hecton8.World` residency manager registered through existing `SystemDispatcher` tick lanes and consuming existing `GlobalSignals` / `GlobalRegistry` surfaces.
Rejected Alternatives: Mutating `GameBootstrapper` was explicitly forbidden. Adding new GlobalRegistry service slots would mutate public registry contracts during a batch. Direct references into MapMagic/Crest were rejected under third-party integrity.
Scalability potential: Low uses short load radius, one request per frame, low upload budget. Middle keeps default 500m/600m. High and Ultra can widen residency and spend saved stalls on stronger fade and richer resident visuals.
Hardware Impact: i3/MX350 estimate is lower streaming hitch risk by avoiding `Addressables.InstantiateAsync` burst dispatch and hard banning first-party `Resources.UnloadUnusedAssets()` calls. Measured proof absent.

## Decision 1 - Awaitable Instantiation
Problem: Activating a loaded chunk can run `Awake`/`OnEnable` bursts if all objects spawn in one frame.
Solution: Use `UnityEngine.Awaitable` with `AwaitableDebtMonitor.NextFrameAsync()` and a hard cap of 5 spawned objects per frame.
Rejected Alternatives: Coroutine activation allocates iterator state. `Task.Run` cannot touch Unity objects and violates the async mandate for Unity-facing orchestration.
Scalability potential: Low keeps the same cap. High/Ultra can raise authoring caps later only after profiler proof.
Hardware Impact: Expected i3/MX350 gain is frame-spike flattening; estimated 300-900 us avoided during dense chunk activation. PENDING VERIFICATION.

## Decision 2 - AUP Hashing And Radius Job
Problem: Chunk residency needs stable IDs and radius decisions that survive floating-origin shifts.
Solution: Hash `AbsoluteUniversePosition.ResolveChunkId()` results into a 64-bit id and run `RadiusBasedStreamingJob` over AUP center arrays with `math.distancesq`.
Rejected Alternatives: Transform-space `Vector3` chunk IDs break on origin shifts. Managed dictionaries in the scan path violate zero-GC/cached-layout rules. `sqrt` distance was rejected under RSQRT/distance-squared doctrine.
Scalability potential: Low scans the same flat arrays with smaller radii. Middle/High/Ultra can increase chunk count and radius without changing the job contract.
Hardware Impact: i3/MX350 expected saving is about 420 us for 512 chunks versus managed main-thread scans. PENDING PROFILER.

## Decision 3 - Load Queue And Asset Lifecycle
Problem: `Addressables.InstantiateAsync` fan-out can create main-thread stalls and orphaned handles during chunk churn.
Solution: Native load request queue dispatches one `Addressables.LoadAssetAsync<GameObject>` per frame; the returned handle is retained until explicit unload. Prefab dependencies are warmed through `ObjectPoolManager`.
Rejected Alternatives: `Addressables.InstantiateAsync` was rejected because it combines load and activation. Fire-and-forget handles were rejected because they cannot be released deterministically.
Scalability potential: Low keeps one dispatch/frame. High/Ultra may raise dispatch only after frame-time proof; visuals improve through higher residency, not by breaking the queue.
Hardware Impact: Expected i3/MX350 gain is reduced IO/upload burst spikes, estimated 600-1200 us per congested load frame. PENDING VERIFICATION.

## Decision 4 - Runtime Memory Gate
Problem: The prompt required `RuntimeWatchdog.GetAvailableMemory()` but no such public query existed.
Solution: Add a conservative static query to `RuntimeWatchdog` using cached safe-bound bytes minus `Profiler.GetTotalReservedMemoryLong()`. Chunk loading halts below 500MB and publishes `MemoryBreach`.
Rejected Alternatives: A private streaming-only memory estimator was rejected because it creates a second authority. `GC.Collect` and `Resources.UnloadUnusedAssets` were rejected by mandate.
Scalability potential: Low hits the halt path earlier and keeps visuals via proxy/fade. High/Ultra keeps broader residency when memory headroom exists.
Hardware Impact: Prevents low-end memory cliff behavior; exact gain requires memory profiler. PENDING VERIFICATION.

## Decision 5 - Unload Ban Scope
Problem: First-party project code had two `Resources.UnloadUnusedAssets()` editor calls; packages and third-party code still contain calls.
Solution: Remove first-party calls under `Assets/_Project`; leave packages/third-party untouched under third-party asset integrity.
Rejected Alternatives: Editing Unity ShaderGraph, MicroSplat, or RealtimeCSG was rejected because they are not first-party ownership. Leaving first-party editor calls was rejected because batch wording demanded deletion.
Scalability potential: Low hardware avoids accidental adoption of an unload nuke in runtime/editor tools. High hardware does not need stop-the-world cleanup either.
Hardware Impact: Avoids possible 40ms+ stall if the pattern enters runtime paths. PENDING RUNTIME PROOF.

## Decision 6 - Compile Wall Classification
Problem: Three build attempts failed before project-wide verification could complete.
Solution: Mark Task 15 compile check as `[BLOCKED BY DEPENDENCY]` after three attempts. Initial external blockers during the three-strike window were `SurvivalPhysiologyScalarResult`, `TetherVerletTelemetryEntry`, and `MantaScooter` missing `IPlayerTransportSource.GetTransportDragCoefficientMultiplier()`. The final Polish build is now blocked only by `SurvivalPhysiologyScalarResult`.
Rejected Alternatives: Fixing survival, tether, or transport owners was rejected as cross-domain churn. Reverting this slice was rejected because compiler output reports no errors in `WorldChunkResidencyManager.cs` once the file is included in `Hecton8.Core.csproj`.
Scalability potential: No runtime scalability effect until dependency owners restore compile.
Hardware Impact: 0 us claimed. Verification remains PENDING.

## OMEGA POLISH CHANGES
Problem: Polish mandate required anti-bloat review after core task completion: fake math where possible, reciprocal multiplies over division, bitmask flags, zero-GC scans, and final build proof.
Solution: Extracted `<POLISH_MANDATE id="OMEGA_POLISH">` by CLI after all 15 core tasks were checked or blocked. Replaced `_fadeTimer / ChunkFadeSeconds` with `_fadeTimer * ChunkFadeSecondsRcp`. Confirmed `RadiusBasedStreamingJob` already uses bitmask state checks and `math.distancesq`, not `math.sqrt` or `math.normalize`. Confirmed touched runtime files have no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `Task.Run`, `Addressables.InstantiateAsync`, `Resources.UnloadUnusedAssets`, managed `List`/`Dictionary` construction, LINQ, or Unity `Update`/`FixedUpdate`/`LateUpdate` methods. Normalized non-ASCII dash comments in `WorldChunkResidencyManager.cs` to ASCII.
Rejected Alternatives: A 1D LUT for radius was rejected because squared AUP distance is cheaper and exact enough for residency. A triangle wave for fade was rejected because the fade is already a one-multiply shader uniform ramp and must be monotonic for chunk promotion. Marking `VERIFIED MASTER GRADE` was rejected because the project still does not compile.
Cinematic Cheats Used: Squared-distance residency instead of physical streaming truth; hysteresis deadzone instead of boundary-perfect loads; `_ChunkFadeMask` shader cross-fade instead of simulating structural transition; one request/frame queue instead of honest burst loading; pooled activation capped at five objects/frame instead of immediate complete chunk realization.
Scalability potential: Low uses shorter residency, one dispatch/frame, low async upload budget, and same fade fake. Middle uses default 500m/600m. High/Ultra can increase resident radius and visual overdraw while preserving the same queue and release contract.
Hardware Impact: Polish change is small: estimated 0.02 us per fade update from removing one division. Main retained i3/MX350 savings remain 420 us per 512 chunk radius scan, 600-1200 us load-spike avoidance, 300-900 us activation spike flattening, and 40ms+ stall avoidance from first-party unload-ban removal. All runtime estimates remain pending profiler evidence.
Final Git Diff: Streaming slice adds `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` and `.meta`, updates `RuntimeWatchdog.GetAvailableMemory()`, removes first-party `Resources.UnloadUnusedAssets()` from `Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs` and `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`, and adds `Docs/AgentLogs/RECON_CORE_CHUNK_STREAMING.md`, this rationale, and `Docs/Tasks/Status_CORE_CHUNK_STREAMING.md`. Tracked diff stat: `HectonSkyAtlasGenerator.cs` -1, `RuntimeWatchdog.cs` +56/-49 including unrelated pre-existing edits plus the memory query, `HectonArtOptimizationTools.cs` -1. New untracked slice files are listed by `git status --short`.
Build Result: Final Polish build command `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with one external error: `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult could not be found`. No warning count reported. STATUS remains PENDING VERIFICATION.

## Decision 7 - Honest R&D Continuation / Additive Scene Race
Problem: Additive sub-scenes were being marked loaded immediately after `allowSceneActivation=true`. Unity activation is asynchronous; an eviction during that window could clear addressable handles while the scene later finished activation, leaving residency state wrong and unload timing ambiguous.
Solution: Track `_additiveSceneUnloadWhenLoaded`, mark additive scenes loaded only after `AsyncOperation.isDone`, and route eviction during activation through a deferred unload path. `UnloadAdditiveScene(int index)` centralizes state clearing so loaded, activation-requested, deferred-unload, and operation fields cannot drift apart.
Rejected Alternatives: Leaving the old optimistic loaded flag was rejected because it hides a real race. Blocking the main thread for scene activation was rejected because it violates the frame-time dictatorship. Cancelling Unity scene activation directly was rejected because the API path is not deterministic once activation has been requested.
Scalability potential: Low tier avoids leaked structural scenes when the player crosses boundaries quickly. Middle keeps current one-scene activation gate. High and Ultra can spend residency budget on larger structural chunks without accepting a broken unload state machine.
Hardware Impact: 0 us steady-state CPU claimed. The gain is risk removal: low-end devices avoid memory pressure from additive scenes that activated after eviction and were no longer tracked correctly.

## Decision 8 - Compile Medic Boundary After R&D
Problem: The stale final log claimed a single survival include blocker, but the current build now opens a broader external wall after earlier include gaps are no longer the first failure.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. Current result: 73 errors and 3 warnings. The repeated missing authorities are `HectonPersistentPathPolicy`, `HectonNativeBridge`, `HectonNativeLibrary`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `SteamDeckInputPal`, `HapticWaveformLibrary`, and `HardwareTierDetector`. No compiler error names `WorldChunkResidencyManager.cs`.
Rejected Alternatives: Patching Save/Core/Audio/Input/Optimization systems from the streaming slice was rejected as cross-domain churn. Regenerating the ignored `*.csproj` wholesale was rejected because `Hecton8.Core.csproj` is git-ignored and shared by concurrent agents.
Scalability potential: No runtime scalability effect until dependency owners restore compile. The streaming code remains designed for Low/Middle/High/Ultra residency tiers, but profiler proof is blocked by project compile health.
Hardware Impact: 0 us claimed. Verification remains PENDING, with compile debt external to this slice based on current error output.

## Decision 9 - NativeCollection Race Gate
Problem: `RadiusBasedStreamingJob` reads `_chunkStates`, but the previous Tick order could still mutate or scan the same `NativeParallelHashMap` from load dispatch, Addressables completion, explicit load/evict requests, `IsResident`, or black-box telemetry while the job was scheduled but not complete. That is undefined NativeCollection access, not a harmless timing issue.
Solution: Treat `_chunkStates` as owned by the job while `_residencyJobScheduled` is true. Tick-side mutation now waits for the job fence. Explicit load requests can queue without reading `_chunkStates`; explicit evictions go into `_deferredEvictChunkIds` and drain after the fence. `WriteTelemetrySample` uses cached resident/loading/evicting counts and cached state hash during active job frames. `SetChunkState` refuses writes during active job ownership and marks force-reevaluation instead. `OnDisable` completes the job as a teardown fence before releasing chunk handles.
Rejected Alternatives: A mid-Tick `Complete()` was rejected because it serializes worker execution and violates the native jobs mandate except at teardown. Dropping explicit requests was rejected because it makes external streaming commands unreliable. Duplicating the entire chunk-state map into a second NativeHashMap was rejected as extra memory and synchronization surface for no measured gain.
Scalability potential: Low tier avoids safety-system exceptions and rare release crashes under slow i3 frames where residency jobs overlap more often. Middle keeps the same behavior with fewer stale cycles. High and Ultra can increase chunk counts/radii later without multiplying a known NativeContainer race.
Hardware Impact: 0 us direct CPU saving claimed. During active job frames, telemetry avoids a live 512-entry hash scan and uses cached values; the real gain is correctness and crash-risk removal on low-end silicon.

## Decision 10 - Build State After Race Gate
Problem: Verification must reflect current objective data, not the earlier smaller compile wall.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:quiet -clp:ErrorsOnly` and wrote `Docs/AgentLogs/Build_CORE_CHUNK_STREAMING_errors3.txt`. Result: 76 errors, 3 warnings. No error references `WorldChunkResidencyManager.cs`. Current repeated blockers are external: `HectonPersistentPathPolicy`, `HardwareTierDetector`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `SteamDeckInputPal`, `HectonNativeBridge`, `HectonNativeLibrary`, and voxel modified event types.
Rejected Alternatives: Fixing Save/Core/Input/Audio/Voxel symbols inside this streaming turn was rejected as cross-domain churn. Marking verified was rejected because full project compile still fails.
Scalability potential: None until compile is restored. The streaming race gate is isolated and ready for runtime profiling after external compile health returns.
Hardware Impact: 0 us claimed. STATUS remains PENDING VERIFICATION.
