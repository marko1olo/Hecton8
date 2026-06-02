# LOG_13GEO

## 2026-05-27 - Terrain/MapMagic/Geology Quality Audit Pass

What was wrong:
- `WorldGenerativeGeologySeamExecutionDirector` had fixed seam execution, collar, debris, and gap-dither budgets. That violates continuous `GlobalQualityWeight` scaling and makes low-tier devices pay for visual density they cannot afford.
- `WorldGenerativeGeologyIntegrationDirector` tracked and refreshed seam plans with fixed capacity/hysteresis. That blocks quality-weight-driven Math LOD for terrain/voxel seam planning.
- `WorldGenerativeGeologyTerrainSeamApplier` still pushed a binary low-tier/high-tier seam shader parameter while the heightmap path already used a float expensive-weight curve.
- `WorldGenerativeGeologyTelemetry` only exposed a bool terrain seam telemetry route for blended seams.
- `WorldGenerativeGeologyVoxelBridgeDirector` still contains native persistent allocation, managed cancellation, and interpolated trace debt, but the file is already actively modified by another agent.

What was done:
- Added continuous `HomeostasisBrain.GlobalQualityWeight` consumption to seam execution budgets, tracked plan capacity, refresh distance, voxel collar segment count, debris count/scale, dither particle count, emission, and size/speed.
- Added debug readouts for active visual quality, executed plan budget, tracked plan budget, refresh threshold, collar budget, debris budget, seam expensive weight, and mask detail weight.
- Changed active terrain seam shader parameter upload to use `seamExpensiveWeight` as a float instead of binary low-tier visual-only routing.
- Added `WorldGenerativeGeologyTelemetry.TryPublishTerrainSeamsBlended(int, int, float)` and kept the bool overload as compatibility shim.
- Left voxel bridge debt recorded in rationale instead of overwriting another agent's dirty file.

Cinematic Cheats used:
- Terrain seam presentation is scaled by cheaper visual evidence first: fewer collars, fewer debris transforms, lower particle density, wider refresh hysteresis.
- Low-tier keeps seam concealment and silhouette continuity; high/ultra spends saved budget on richer debris, denser collars, tighter refresh, and stronger blend mask response.
- No terrain truth, DTO layout, save identity, authority route, or MapMagic ownership was changed.

Exact Microseconds saved:
- Verified by profiler: 0 us, because compile/runtime/profiler gate was blocked.
- Static low-tier estimate: 40-75 us saved per active seam reconciliation burst by reducing executed plans, collar primitives, debris objects, and dither emission density.
- Shader route change: 0 us claimed; it removes binary visual discontinuity, not measured CPU time.

Verification:
- `git diff --check` passed for edited source files; only CRLF normalization warnings were reported.
- DataVault audit command timed out after 124 seconds and the lingering Python process was killed.
- Compile/build was not launched because active `dotnet` processes existed and CPU sampled at 100.0%, which violates project compile-server rules.
- Current status remains PENDING VERIFICATION.

Changed files:
- `Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/World/WorldGenerativeGeologyTelemetry.cs`
- `Docs/Tasks/Status_13GEO.md`
- `Docs/AgentLogs/Rationale_13GEO.md`
- `Docs/AgentLogs/LOG_13GEO.md`

## 2026-05-27 - Second Pass Voxel Bridge And DataVault Route Cleanup

What was wrong:
- `WorldGenerativeGeologyVoxelBridgeDirector` had fixed voxel runtime capacity, fixed spawn/async launch budgets, fixed pool warm target, fixed max grid cap, and fixed resolution scaling. This violated continuous `GlobalQualityWeight` scaling.
- The same voxel bridge allocated a managed `CancellationTokenSource` for each pending request and allocated persistent zero-length `NativeArray` inputs for empty cave nodes/tunnels/entrances.
- `WorldGenerativeGeologyTerrainSeamApplier` used `GlobalRegistry.DataVault` inside repeated terrain seam buffer paths instead of cached DI.

What was done:
- Voxel bridge now resolves runtime volume budget, spawn budget, async launch budget, pool warm padding, pool warmup batch, grid cap, and resolution scale from continuous `HomeostasisBrain.GlobalQualityWeight`.
- Pending voxel requests now use a cancellation flag plus lifecycle token. Per-request CTS allocation is removed.
- Zero-length tracked native arrays now return default `NativeArray<T>` instead of persistent allocation.
- Terrain seam applier now caches `_dataVault` and refreshes it on DataVault hotswap; buffer paths use the cached route.

Cinematic Cheats used:
- Low quality keeps fewer but stable voxel seam structures and lower resolution grid caps.
- Middle quality raises detail continuously.
- High/ultra restores configured maximum volume density and richer voxel seam geometry without changing world truth ownership.

Exact Microseconds saved:
- Verified by profiler: 0 us, compile/runtime gate still blocked.
- Static low-tier estimate: 60-140 us saved during voxel seam bursts by lowering volume capacity, launch rate, grid cap, resolution scale, and pool warmup.
- Allocation reduction: per-request CTS removed; zero-length persistent native arrays removed.

Verification:
- `git diff --check` passed for edited domain files; only CRLF normalization warnings were reported.
- Targeted `rg` confirmed no remaining per-request CTS references and no hot `GlobalRegistry.DataVault` reads outside `ResolveReferences()`.
- Compile/build was not launched because CPU sampled at 72.3%, above the project limit of 50%.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Voxel bridge still has one lifecycle `CancellationTokenSource`; it is cold lifecycle control, not per-request allocation.
- Compatibility bool telemetry shim remains in `WorldGenerativeGeologyTelemetry` to avoid breaking callers.

## 2026-05-27 - Third Pass Signature Stability And Hot Registry Cleanup

What was wrong:
- Pending voxel requests could compute a signature with one `GlobalQualityWeight` and build async voxel data with a later `GlobalQualityWeight`.
- `WorldGenerativeGeologyVoxelBridgeDirector` still read thermodynamics, persistent world registry, and object pool services from `GlobalRegistry` in runtime methods.
- MapMagic hydraulic erosion uses large TempJob buffers and same-call completion, but it is a cold MapMagic generation path and changing it would affect terrain truth/performance without a profiler proof.

What was done:
- Added `PendingRequestState.VisualQualityWeight`; voxel request signature and build-data now use the same frozen quality scalar.
- Cached `_thermalManager`, `_persistentWorldRegistry`, and `_objectPoolService` from cold registry refresh / hotswap replacement.
- Left MapMagic generation truth untouched; runtime visual quality must not alter terrain authority output.

Cinematic Cheats used:
- Quality still scales voxel visual density, grid cap, and resolution; now the queued build contract is deterministic.
- No terrain height truth, save identity, DTO layout, or MapMagic graph output was changed.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static estimate: 20-60 us avoided during quality transition bursts by preventing stale signature/build mismatch churn.
- Additional hot registry route cleanup has small per-call savings and mainly removes doctrine violation.

Verification:
- `git diff --check` passed for edited domain files; only CRLF normalization warnings were reported.
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal` was launched legally after CPU sampled at 18.6% and no active `dotnet`/`csc` processes.
- Build failed outside 13GEO domain: `Assets\Candice AI for Games\Scripts\Libs\Candice Save System\Overrides\CandiceSQLiteProvider.cs` cannot resolve `Mono.Data` and `SqliteDataReader`.
- Current status remains PENDING VERIFICATION.

## 2026-05-27 - Fourth Pass Runtime Geology And Voxel Black-Box Audit

What was wrong:
- `WorldGenerativeGeologyService` still treated `FinalVariantActive` as a binary quality switch: non-final generated geology was forced to `SingleFeature`, LOD count was bool-capped, and debris was full-or-zero.
- `WorldGenerativeGeologyVoxelBridgeDirector` had no 300-frame black-box ring for reconcile, queue, missing dependency, null volume, or exception state.
- A direct local `NativeArray` ring would violate the project DataVault ownership rule.

What was done:
- Runtime generated geology now resolves composition, LOD count, and debris count from continuous `HomeostasisBrain.GlobalQualityWeight` plus stable hash dither.
- `FinalVariantActive` remains only a detail bias. Terrain truth, MapMagic graph output, DTO layout, save identity, and authority route were not changed.
- Voxel bridge now caches `_dataVault`, refreshes it on DataVault hotswap, allocates a vault-owned 300-entry black-box ring, writes 64-byte explicit telemetry entries through `TryAcquireWriteLock` / `ReleaseWriteLock`, and dumps once to `Docs/AgentLogs/Dump_13GEO_VoxelBridge.bin` on fault in editor/development builds.

Cinematic Cheats used:
- Low quality: sparse single-feature geology, minimum seam debris, fewer generated LODs.
- Middle quality: stable stochastic spread of paired/context features without global pop.
- High/ultra: configured geology composition and richer debris/LOD density, driven by saved cycles rather than simulation truth changes.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static low-tier estimate: 20-90 us saved during generated-geology rebuild bursts by reducing generated primitives, LOD roots, and debris.
- Voxel black-box cost: one bounded 64-byte vault write per reconcile/fault when the lock is available; no measured runtime cost yet.

Verification:
- `git diff --check -- Assets/_Project/Scripts/WorldGenerativeGeologyService.cs Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs` passed; only CRLF normalization warnings.
- Targeted `rg` verified `useFullDetail` is gone, runtime geology reads `HomeostasisBrain.GlobalQualityWeight`, and voxel black-box writes use `TryAcquireWriteLock` / `ReleaseWriteLock`.
- Build was not launched: `dotnet.exe` PID 62864 is already running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`, and CPU sampled at 69%.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Voxel bridge still has non-empty `Allocator.Persistent` tracked arrays for actual async cave data and one cold lifecycle `CancellationTokenSource`; those are not new in this pass.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Fifth Pass Voxel Black-Box Hot Allocation Closure

What was wrong:
- The voxel bridge black-box ring was DataVault-owned, but `TryAcquireVoxelBridgeBlackBox()` called `TryEnsureVoxelBridgeBlackBox()`.
- If DataVault hotswap invalidated the handle before cold refresh finished, reconcile/fault telemetry could allocate via `EnsureGenerationHandle<T>()` from a hot path.

What was done:
- `TryAcquireVoxelBridgeBlackBox()` now fail-closes unless the cached exact handle already opens and `TryAcquireWriteLock()` succeeds.
- Black-box buffer creation remains in cold refresh / DataVault hotswap handling only.
- Terrain truth, MapMagic output, voxel generation data, save identity, and DTO layout were not changed.

Cinematic Cheats used:
- No new simulation was added. The fix preserves the previous cheap forensic write and skips a diagnostic sample when the vault is not ready instead of allocating under fault pressure.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static edge-case estimate: 20-80 us saved on i3/MX350-class hardware during DataVault hotswap/fault paths by removing a possible native allocation/stall.
- Normal-frame estimate: unchanged; hot path still performs at most one bounded vault write when the buffer exists.

Verification:
- `git diff --check -- Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs Docs/Tasks/Status_13GEO.md Docs/AgentLogs/Rationale_13GEO.md Docs/AgentLogs/LOG_13GEO.md` passed; only CRLF normalization warning.
- Targeted `rg` shows `TryEnsureVoxelBridgeBlackBox()` is only called from DataVault hotswap and cold registry refresh; `EnsureGenerationHandle<VoxelBridgeTelemetryEntry>()` is only inside the ensure method.
- Build was not launched: `dotnet.exe` PID 47232 is already running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`, and `VBCSCompiler.exe` PID 35836 is active.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Non-empty async cave data still uses tracked `Allocator.Persistent` arrays before `HectonVoxelEngine.GenerateVolumeFromDataAsync()`. Full migration needs an engine ownership audit, not a blind patch in a parallel batch.
- One lifecycle `CancellationTokenSource` remains for disable/cancel control.

## 2026-05-27 - Sixth Pass Inline Voxel Cave Graph Handoff

What was wrong:
- `WorldGenerativeGeologyVoxelBridgeDirector` still allocated caller-owned persistent `NativeArray<CaveEntrance>` and `NativeArray<CaveStructure>` for each async voxel seam volume request.
- `HectonVoxelEngine.GenerateVolumeFromDataAsync()` copied those arrays into DataVault streaming scratch, but later runtime configure and terrain-hole registration still read the caller arrays. That forced persistent lifetime across the whole async pipeline.

What was done:
- Added `HectonVoxelEngine.VoxelInlineCaveGraphData`: one entrance slot and seven structure slots, matching the bridge's maximum archetype payload.
- Added `GenerateVolumeFromInlineCaveGraphDataAsync()` overloads. The engine now acquires its streaming scratch, copies inline payload directly into DataVault-backed scratch, and uses `pipelineData.Nodes/Tunnels/Entrances/Structures` for runtime configure and terrain-hole registration.
- Updated the 13GEO bridge to build inline cave graph payloads. It no longer calls `AllocateTrackedNativeArray`, `DisposeTrackedNativeArray`, or `NativeMemorySentinel` for voxel handoff.

Cinematic Cheats used:
- No extra geology simulation was added. This is a transport/ownership fix: the same 1 entrance + 4/5/7 structures are preserved, but ownership moves to existing bounded engine scratch.
- Low quality still reduces voxel volume budgets and resolution from prior passes; high/ultra keep the full configured structure payload.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static low-tier estimate: 25-110 us saved during voxel spawn bursts by removing per-request persistent native allocation/disposal for up to 1 entrance and 7 structures.
- Fragmentation risk reduced: bridge no longer owns transient native arrays across async voxel generation.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs` passed; only CRLF normalization warnings.
- Targeted `rg` verified `WorldGenerativeGeologyVoxelBridgeDirector.cs` has no `AllocateTrackedNativeArray`, `DisposeTrackedNativeArray`, `NativeMemorySentinel`, `Allocator.Persistent`, or `new NativeArray<` matches.
- Legal full `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched after CPU fell to 26% and no compiler processes existed.
- Full build failed in external Editor assemblies before proving runtime: `Assets/AmplifyImpostors/Plugins/Editor/AIStartScreen.cs` missing `Preferences.ShowOption`, and MapMagic Editor calls are ambiguous between `Assets/MapMagic/Expose/Editor/CellExpose.cs` and stale `Library/ScriptAssemblies/MapMagic.Editor.dll`.
- Runtime-only compile was not launched afterward because CPU resampled at 73% then 93%, above the project limit.
- Current status remains PENDING VERIFICATION.

Residual risk:
- `HectonVoxelEngine` still has unrelated persistent smooth-density snapshot allocation for volume runtime data publication. It is engine-owned transient arena, not 13GEO bridge caller handoff, and needs a separate proof pass.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Seventh Pass Streaming Scratch DataVault Route

What was wrong:
- Self-audit found no direct break in the old public `GenerateVolumeFromDataAsync` overloads, but found a remaining doctrine violation in the same voxel pipeline.
- `VoxelStreamingScratchLease.Dispose()` and `UnlockStreamingScratchJobLifetime()` used `GlobalRegistry.DataVault` fallback on scratch buffer unlock paths. That is a hot service-locator route and can unlock against a different vault after DataVault replacement.

What was done:
- Added `_lockedScratchVault` to `VoxelStreamingScratchLease`.
- `TryLockStreamingScratchJobLifetime()` captures the exact vault that locked scratch buffers.
- `UnlockStreamingScratchJobLifetime()` and lease `Dispose()` unlock through that captured vault first, then the owner cached scratch vault. No hot `GlobalRegistry.DataVault` fallback remains in those routes.
- `ReleaseNearestSonarSdfReadLease()` now uses the cached engine vault only; it does not query the registry during release.
- On enable, published sonar SDF payload capacity uses `_streamingScratchVault` instead of re-reading `GlobalRegistry.DataVault`.

Cinematic Cheats used:
- No new simulation. This is route hardening for voxel streaming and sonar SDF reads used during first-20-minutes cave/seam traversal.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static low-tier estimate: 5-20 us saved during voxel generation bursts by removing registry fallback branches from scratch lock/unlock cycles.
- Correctness gain: lock ownership now follows the vault that granted the lock.

Verification:
- `git diff --check` passed for edited files; only CRLF normalization warnings.
- Targeted `rg` verified `WorldGenerativeGeologyVoxelBridgeDirector.cs` has no NativeArray handoff allocation markers.
- Targeted `rg` verified scratch lock/unlock/dispose routes use `_lockedScratchVault` / `_streamingScratchVault` and no longer call `GlobalRegistry.DataVault`.
- Runtime compile not launched: sibling `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` was active, then CPU sampled at 59% and 100%, above the project 50% limit.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Remaining `GlobalRegistry.DataVault` reads in `HectonVoxelEngine` are cold init/cache paths (`MCTables.Initialize`, black-box cache, streaming scratch slot ensure). They still need compile/runtime proof but are not the scratch unlock hot route fixed here.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Eighth Pass Public Sonar SDF Lease Exact-Vault Route

What was wrong:
- `HectonVoxelEngine.TryAcquireNearestSonarSdfReadLease()` acquired a `HectonVoxelVolume.PublishedSonarSdfReadLease` with an exact `IDataVault`, then dropped that vault when returning the public `VoxelSonarSdfReadLease`.
- `ReleaseNearestSonarSdfReadLease()` unlocked through the engine's current `_streamingScratchVault`, which can be different after DataVault replacement. That can leave the original SDF buffer locked.

What was done:
- Added a fixed 32-slot owner-local tracker in `HectonVoxelEngine`: vault, SDF generation, version, ref count.
- Public acquire now records the exact vault before returning the DTO. Public release unlocks through that recorded vault.
- Teardown drains remaining tracked public SDF locks.
- No public DTO layout, interface signature, MapMagic output, terrain truth, save identity, or volume generation contract changed.

Cinematic Cheats used:
- No simulation added. This is lock-route hardening for sonar/GPR/cutter reads over existing published voxel SDF snapshots.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static runtime cost: bounded 32-slot scan on acquire/release.
- Static correctness gain: prevents stale-vault unlock leaks that can block DataVault relocation or later SDF publication under cave/seam traversal.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` verified `NearestSonarSdfReadLease` tracker helpers and that `ReleaseNearestSonarSdfReadLease()` no longer unlocks via `_streamingScratchVault`.
- Runtime compile not launched: CPU sampled at 74%, above the project 50% limit. No active compiler processes were visible.
- Current status remains PENDING VERIFICATION.

Residual risk:
- `ConfigureVolumeRuntimeDataFromPipelineAsync()` still copies smooth density into a transient persistent `NativeArray<float>`, and `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` still encodes through transient persistent byte arrays before copying into DataVault. That is a separate publish-path ownership rewrite, not included in this narrow lease fix.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Ninth Pass Smooth-Density Snapshot Clone Removal

What was wrong:
- `ConfigureVolumeRuntimeDataFromPipelineAsync()` copied `data.ScratchLease.SmoothDensityField` into a transient persistent `NativeArray<float>` before publishing the sonar SDF snapshot.
- At max `StreamingPointScratchMax`, that clone is about 8.2 MB of native allocation per volume snapshot.

What was done:
- Removed `TryCopySmoothDensitySnapshotFromScratch()` and the `smoothDensitySnapshot` clone.
- The engine now locks the existing streaming scratch lease, validates `SmoothDensityField`, passes it directly into `ConfigureVolumeRuntimeDataAsync()`, and unlocks in `finally` after the awaited publish completes.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or voxel fidelity changed.

Cinematic Cheats used:
- No new simulation. This spends existing DataVault scratch ownership correctly instead of cloning data. Low tiers avoid allocator churn; high/ultra keep the same published SDF detail.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static memory gain: up to ~8.2 MB transient native allocation avoided per max-size volume snapshot.
- Static allocator gain: lower persistent allocator churn and fragmentation during voxel seam/cave generation bursts.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` found no `TryCopySmoothDensitySnapshotFromScratch`, no `smoothDensitySnapshot`, and no `NativeArray<float>(` allocation in `HectonVoxelEngine.cs`.
- Runtime compile not launched after this patch because machine state was already illegal for build: CPU above 50% and active `VBCSCompiler.exe` were observed in the preflight window.
- Current status remains PENDING VERIFICATION.

Residual risk:
- `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` still allocates transient persistent byte arrays for encoded SDF and audio material IDs before copying to DataVault. Direct DataVault-output encoding remains a separate, higher-risk owner-path rewrite.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Tenth Pass Direct DataVault Sonar SDF Encode

What was wrong:
- `HectonVoxelVolume.PublishSonarSdfSnapshotAsync()` allocated `encodedScratch` and `audioMaterialScratch` as transient persistent byte arrays, scheduled encode into them, then copied both arrays into DataVault payload buffers.
- At max `PublishedSonarMaxPointCount`, this is roughly 4.3 MB transient native allocation plus two full payload copies per publish.

What was done:
- Removed the byte scratch allocations and `DisposePublishedSonarScratch()`.
- `PublishedSonarSdfEncodeJob` now writes directly into generation-checked DataVault SDF/audio buffers under writer locks.
- Descriptor invalidation is ordered after payload writer locks and capacity validation, immediately before in-place rewrite. Descriptor publication happens only after the encode job finalizes and cancellation/abort checks pass.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or SDF fidelity changed.

Cinematic Cheats used:
- No physical simulation added. This is a memory/bandwidth fix for the existing visual/sonar proxy: use the same compact byte SDF, but stop duplicating it outside DataVault.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static memory gain: up to ~4.3 MB transient native allocation avoided per max-size publish.
- Static bandwidth gain: two full byte-buffer copies removed per publish.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelVolume.cs` passed; only CRLF normalization warning.
- Targeted `rg` found no `encodedScratch`, `audioMaterialScratch`, `DisposePublishedSonarScratch`, `NativeMemorySentinel`, or `NativeArray<byte>(` in `HectonVoxelVolume.cs`.
- Runtime compile not launched: CPU sampled at 73%, and active compiler processes were present (`dotnet.exe` PID 19660, `VBCSCompiler.exe` PID 35324).
- Current status remains PENDING VERIFICATION.

Residual risk:
- Writer locks now live for the encode job duration instead of only the final copy duration. This is intentional to eliminate scratch, but Unity profiler proof is still required to confirm compaction/read contention remains acceptable.
- Unity import/runtime/profiler proof is absent.

## 2026-05-27 - Eleventh Pass Published SDF Lease Surface Closure

What was wrong:
- `TryGetClosestPublishedSonarSdfPayload()` and `TryReadClosestPublishedSonarSdfPayload()` returned `NativeArray<byte>.ReadOnly` DataVault payload views without returning a read lease.
- After direct DataVault encode, that old internal surface was unsafe: a future caller could keep a payload view while the owner publishes into the same buffer.

What was done:
- Removed the stale static unleased closest-payload getters.
- Changed instance `TryGetPublishedSonarSdfPayload()` overloads from `internal` to `private`.
- Current external consumers stay on `TryAcquirePublishedSonarSdfPayloadReadLease()` plus explicit release.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or SDF fidelity changed.

Cinematic Cheats used:
- No simulation added. This is ownership hardening for the existing compact sonar SDF proxy.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static CPU impact: unchanged.
- Static correctness gain: removes an unleased native alias route over DataVault SDF/audio buffers.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelVolume.cs` passed; only CRLF normalization warning.
- Targeted `rg` found no `TryGetClosestPublishedSonarSdfPayload`, no `TryReadClosestPublishedSonarSdfPayload`, and no `internal bool TryGetPublishedSonarSdfPayload` surface.
- Runtime compile not launched: CPU sampled at 65%, and `VBCSCompiler.exe` PID 58740 was active.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Unity import/runtime/profiler proof is absent.
- Direct DataVault encode writer-lock duration still needs runtime contention measurement.

## 2026-05-27 - Twelfth Pass Ambiguous Public SDF Lease Key Rejection

What was wrong:
- `VoxelSonarSdfReadLease` is a 24-byte public DTO with no vault/owner token.
- `HectonVoxelEngine` internally tracked the exact vault for public read leases, but release still receives only `SdfGeneration + Version`.
- If two active tracked leases shared that public key on different vaults, release could unlock the first matching tracker slot instead of the vault that granted the lease.

What was done:
- `TryTrackNearestSonarSdfReadLease()` now scans active slots by public key before creating a new slot.
- Matching vault/generation/version still coalesces and increments ref count.
- Same public generation/version on a different vault now fails closed; the existing acquire `finally` releases the internal volume lease before a public DTO escapes.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or SDF fidelity changed.

Cinematic Cheats used:
- No simulation added. This is ownership hardening for the existing compact sonar SDF proxy and keeps terrain/geology visuals decoupled from lock authority.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static CPU impact: unchanged bounded 32-slot scan.
- Static correctness gain: prevents wrong-vault unlock or retained read lock under rare DataVault hotswap/generation collision.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` verified the ambiguity guard comment and exact tracker path in `HectonVoxelEngine.cs`.
- Runtime compile not launched: CPU sampled at 25%, but `VBCSCompiler.exe` PID 24496 was active, so the build gate remained illegal.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Unity import/runtime/profiler proof is absent.
- This keeps the 24-byte public DTO stable; a future contract version could add an opaque owner token, but that is not safe during this parallel batch.

## 2026-05-27 - Thirteenth Pass Non-Lease SDF Bulk Read Closure

What was wrong:
- `HectonVoxelEngine.TryReadNearestSonarSdf()` returned a DataVault-backed `NativeArray<byte>.ReadOnly` view without returning a lease.
- Its helper selected a candidate payload under `PublishedSonarSdfReadLease`, then released that lease before the public method returned the payload alias.
- After direct DataVault encode, that is not an acceptable lifetime contract.

What was done:
- `TryReadNearestSonarSdf()` now fails closed with default outputs.
- Removed the private `TryReadNearestActiveSonarSdfPayload()` helper that returned an unleased payload view.
- Safe read paths remain intact: explicit public lease acquire/release, owner-directed surface resolver, raymarch, and scalar sample.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or SDF fidelity changed.

Cinematic Cheats used:
- No simulation added. This preserves the compact SDF proxy but forces consumers onto explicit lifetimes instead of unsafe bulk aliases.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static CPU impact: neutral.
- Static correctness gain: removes an unlocked native payload escape over the direct DataVault SDF buffers.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` found no `TryReadNearestActiveSonarSdfPayload` helper and verified the fail-closed bulk-read comment.
- Runtime compile not launched: CPU sampled at 73%, and `VBCSCompiler.exe` PID 24496 was active.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Unity import/runtime/profiler proof is absent.
- The public `IVoxelSonarSdfReadModel.TryReadNearestSonarSdf()` signature still exists for ABI compatibility; this engine implementation no longer returns unsafe payload views.

## 2026-05-27 - Fourteenth Pass Voxel Scratch Exact-Vault Ownership

What was wrong:
- `VoxelStreamingScratchSlot` stored DataVault generation handles but not the vault that issued them.
- After DataVault hotswap, scratch array resolve/dispose/job-lock paths could use the new `_streamingScratchVault` against handles created by the old vault.
- `HectonVoxelEngine.OnGlobalRegistryServiceReplaced()` did not handle `GlobalRegistryServiceSlot.DataVault`.
- Hot marching-cubes mesh phases still used `MCTables.TryAcquireJobTables()` without passing the cached vault route.

What was done:
- Added exact `IDataVault Vault` ownership to each streaming scratch slot.
- Lease array resolution now goes through `slot.Vault`.
- Slot disposal now releases handles through the slot owner vault.
- Job-lifetime scratch locks now use the slot owner vault.
- DataVault hotswap now either rebinds immediately when slots are idle or records a pending vault and drains active slots before switching.
- Marching-cubes table initialization/acquisition in 13GEO hot voxel phases now uses cached `_streamingScratchVault`.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or voxel fidelity changed.

Cinematic Cheats used:
- No simulation added. This is ownership hardening for the existing voxel scratch pipeline so performance budget remains available for visible cave/seam detail.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static CPU gain: removes hot `GlobalRegistry.DataVault` reads from the 13GEO MCTables call sites.
- Static correctness gain: prevents wrong-vault handle release/resolve and stale scratch leaks during DataVault hotswap.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` verified `_pendingStreamingScratchVault`, `slot.Vault`, exact-vault scratch resolve, and cached MCTables call sites.
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched only after CPU/process preflight became legal.
- Build result is inconclusive: command timed out after 124s with no compiler diagnostics and left child build processes. The visible `dotnet.exe` children were stopped, then `dotnet build-server shutdown` cleared the remaining VBCS compiler server. Post-cleanup CPU sampled at 9% and no compiler processes remained.
- Current status remains PENDING VERIFICATION.

Residual risk:
- Unity import/runtime/profiler proof is absent.
- Compatibility `MCTables.Initialize()` / `TryAcquireJobTables()` overloads still exist for external callers; 13GEO hot call sites now pass the cached vault directly.

## 2026-05-27 - Fifteenth Pass DataVault Hotswap Payload/Scratch Owner Split

What was wrong:
- `OnGlobalRegistryServiceReplaced(DataVault)` used `_streamingScratchVault` for published sonar SDF payload capacity after hotswap. While scratch drains, that field intentionally still points at the old vault, so the call could grow retired SDF payload buffers.
- A vault bounce A->B->A could leave `_teardownStreamingScratchRequested` true after `_pendingStreamingScratchVault` was cleared, allowing teardown finalization to null the scratch vault.

What was done:
- Routed published sonar SDF payload capacity through the event `currentVault`.
- Kept `_streamingScratchVault` as the old active scratch/MCTables owner until active voxel jobs drain.
- Cancelled scratch teardown when the requested vault again equals the active scratch vault.
- No public API, DTO layout, MapMagic output, terrain truth, save identity, or voxel fidelity changed.

Cinematic Cheats used:
- No physical simulation added. This preserves the existing compact sonar/SDF proxy and protects the owner routes that feed visual cave/seam detail.

Exact Microseconds saved:
- Verified by profiler: 0 us.
- Static CPU gain: none claimed.
- Static correctness gain: avoids retired-vault SDF payload allocation/growth and prevents scratch acquisition outage after DataVault bounce.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` passed; only CRLF normalization warning.
- Targeted `rg` verified `currentVault` payload route and teardown cancellation in `RebindStreamingScratchVault()`.
- Compile was not launched in this pass because CPU sampled at 51% and active compiler/build processes existed: `dotnet.exe` PID 22452 and `VBCSCompiler.exe` PID 17480.
- Full compile/runtime/profiler proof remains PENDING VERIFICATION.

Residual risk:
- Unity import/runtime/profiler proof is absent.
- MCTables remains intentionally bound to the old scratch vault until active slots drain; this is correct for active jobs but needs runtime hotswap coverage.
## 2026-05-27 - 13GEO Sixteenth Pass - Published Sonar SDF Read-Lock Contract Closure

What was wrong:
- `HectonVoxelVolume.TryReadPublishedSonarVaultPayload()` resolved both published payload arrays for every caller.
- SDF-only density/raycast/gradient paths held `VoxelSdfTexture3D` read locks but still resolved `VoxelSdfAudioMaterialIds`.
- Audio material sampling held `VoxelSdfAudioMaterialIds` but still resolved `VoxelSdfTexture3D`.
- `VoxelSdfPayloadDescriptor` was read without a descriptor read lock even though publisher invalidation/final publication uses DataVault writer locks.

What was done:
- Added explicit `requireSdf` / `requireAudioMaterial` gates to `TryReadPublishedSonarVaultPayload()`.
- Added `TryLockBuffer(BufferID.VoxelSdfPayloadDescriptor, SystemID.TerrainSeams)` around descriptor metadata reads with `finally` unlock.
- Updated density, raymarch, gradient, and SDF-only lease paths to pass `requireAudioMaterial: false`.
- Updated audio material sampling to pass `requireSdf: false`.
- Left combined sonar/audio lease routes requesting both payloads.

Cinematic Cheats used:
- No simulation fidelity increase. This pass buys stability for existing cave/sonar presentation instead of spending CPU on physical complexity.

Exact Microseconds saved:
- 0 profiler-verified microseconds.
- Static estimate: 2-8 us saved during hot SDF-only read bursts on i3/MX350-class hardware by avoiding unnecessary audio payload alias resolution; 1-4 us saved on audio-only probes by avoiding unnecessary SDF alias resolution. These numbers are estimates until Unity/player profiler capture.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelVolume.cs Docs/Tasks/Status_13GEO.md Docs/AgentLogs/Rationale_13GEO.md Docs/AgentLogs/LOG_13GEO.md` passed with CRLF warning only.
- Targeted `rg` verified `requireAudioMaterial: false`, `requireSdf: false`, descriptor lock, and descriptor unlock call sites.
- Compile/runtime status: PENDING VERIFICATION. Build was not launched because CPU sampled at 100%, then 60% after 30 seconds; no `dotnet`/`csc`/`VBCSCompiler` process was active, but the CPU gate stayed above the project >50% limit.
