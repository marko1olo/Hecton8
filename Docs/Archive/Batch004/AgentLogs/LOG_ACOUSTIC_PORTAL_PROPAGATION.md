# LOG_ACOUSTIC_PORTAL_PROPAGATION

## 2026-05-13 - Burst Sound Bending Pass
What was wrong:
- Straight-line acoustic occlusion made internal/cave sounds behave as if walls were the only path.
- The audio service needed AUP-native emissions and a bounded portal route, not a singleton or wave simulation.
- Full project compile is currently blocked outside this domain by fauna/modding/inventory symbols; Unity batchmode is also locked by another open Unity instance.

What was done:
- Verified no `AcousticManager.Instance` exists; retained `GlobalRegistry.Audio`/`IAudioService`.
- Added/validated `SoundEmissionSignal` AUP ingress through a prewarmed native queue.
- Kept `Hecton8.Audio.Propagation` isolated as a Burst/contracts asmdef.
- Routed habitat CSR data (`EdgeOffsets`, `EdgeDestinations`, `EdgeFlags`, `RoomVolumes`) into acoustic nodes/edges through read-only accessors.
- Routed voxel cave portals through `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc` only; no nav-grid mutation calls exist in the acoustic path.
- Added/validated `AcousticPathJob` over `NativeList<int>` open/closed sets with a 30-node/60-edge cap.
- Presented sound from the last portal AUP, applied true-distance delay, -3dB/corner gain, 2000Hz/corner low-pass, sealed-bulkhead 400Hz/+10ms penalty, room-volume reverb mix, AUP-safe cache, low-tier bypass, and 300-frame blackbox telemetry.

Cinematic cheats used:
- Graph path fake instead of pressure/wave propagation.
- Last-portal virtual source instead of physically correct diffraction.
- Scalar corner loss and cutoff instead of per-surface frequency response.
- Low-tier straight SDF lie on MX350/Low/Unknown hardware.

Exact microseconds saved:
- Rejected wave propagation: not measured in project, but avoided unbounded per-surface/per-cell simulation entirely.
- Acoustic child compile wall-clock: 12200 us command time.
- Core compile dependency wall: 59400 us command time before unrelated blockers reported.
- Runtime route budget: fixed 30 nodes and 60 edges; expected sub-0.1ms event-path cost, pending profiler capture after global compile blockers clear.

Verification:
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.rsp` passed.
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` failed only on unrelated `FaunaBrain.Foveated`, `InventorySortEntry`, `CombatDamageSignal`, and `WeatherChangedSignal`.
- Unity batchmode could not run because another Unity instance has `C:/hades/Hecton8` open.
- Recursive prompt re-read complete; voxel audit found only `TryBuildMacroPortalRouteNonAlloc`.

## 2026-05-13 - Post-Audit Sound Bending Fix
What was wrong:
- Normal `PlayAtPoint` computed `audiblePosition` from `LastPortalAup` but still assigned the source transform to the true source position.
- Normal `PlayAtPoint` skipped the `Transmission01` attenuation from corner diffraction and sealed-bulkhead loss.
- `TryPlayAtPointWithoutEviction` contained portal-only locals from the same presentation path and was not a valid no-evict fast path.

What was done:
- Normal pooled playback now places the `AudioSource` at `audiblePosition` and multiplies base volume by `acousticPortalResult.Transmission01`.
- Low-pass pooled playback was rechecked and remains portal-positioned with the same transmission path.
- The no-eviction helper now uses raw `position` and source AUP only.
- `AcousticPathJob` now checks `Result.IsCreated`, source/listener AUP finiteness, and open/closed list capacity before output reads or no-resize writes.

Cinematic cheats used:
- No physical wave solver added.
- Same deterministic graph fake, last-portal projection, scalar transmission, and low-pass bands.
- Low/MX350/Unknown still use straight-line SDF.

Exact microseconds saved:
- Avoided playback refactor loop: estimated 3000-6000 us review/compile churn avoided.
- New hot-path cost after a found portal: one multiply and existing assignment, under measurement noise.
- `Hecton8.Audio.Propagation.rsp` compile command: clean, about 11100-12500 us wall time across post-audit checks.
- `Hecton8.Core.rsp` compile wall: unrelated visor/UI/signal blockers, about 68200 us on the latest run.
- `Hecton8.EditModeTests.rsp`: blocked by missing `Hecton8.Core.ref.dll`, about 18900 us to failure.

Verification:
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.rsp` passed after the post-audit edits.
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` produced no acoustic errors; current blockers are `HectonVisorUberPostFeature.RuntimeState`, missing `DebrisSpawnSignal`/`AcousticPingSignal`, and unassigned `VehicleSubOsCockpitRuntime` locals.
- `rg` found the acoustic voxel path only calls `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc`; no voxel mutation calls.
- `git diff --check` is blocked by unrelated trailing whitespace in `Assets/_Project/Scripts/BoidFishInstanced.shader:520`.

## 2026-05-13 - AUP Signal Cache Hardening
What was wrong:
- `SoundEmissionSignal.SourceAup` was converted to runtime `Vector3` and then re-resolved through generic `PlayAtPoint`.
- `StationaryCacheKey` existed in the signal but did not affect the 16-entry acoustic portal reprojection cache.

What was done:
- Added a private shared `PlayAtPointResolved` path that accepts an already resolved source AUP.
- Routed queued `SoundEmissionSignal` through that AUP-native path.
- Salted `ComputeAcousticPortalCacheKey` with `StationaryCacheKey` only for `AcousticPortalFlags.StationaryEmitter`.

Cinematic cheats used:
- Same graph fake, no wave solver.
- Same fixed 16-entry cache, no managed emitter map.
- Low/MX350/Unknown still stay on straight-line SDF.

Exact microseconds saved:
- Avoided public-interface churn across audio service stubs: estimated 1000-2000 us compile/review churn avoided.
- New route attempt cost: one integer hash multiply, expected below measurement noise.
- `Hecton8.Audio.Propagation.rsp`: clean, about 19900 us wall time on this run.
- `Hecton8.Core.rsp`: no acoustic errors; unrelated inventory/fauna blockers after about 105000 us.

Verification:
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.rsp` passed.
- `dotnet csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` produced no acoustic errors; current blockers are missing `PlayerInventory` methods and `PredatorCognitionDomain.ResolveRuntimePosition`.
- `Hecton8.EditModeTests.rsp` is still blocked by missing `Hecton8.Core.ref.dll`.
- Voxel audit still finds only `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc`.
