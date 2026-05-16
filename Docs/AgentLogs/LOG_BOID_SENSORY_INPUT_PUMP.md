## 2026-05-16 BOID_SENSORY_INPUT_PUMP

What was wrong:

- GPU micro-fauna boids already reacted to encounter predator AUP data, sonar, and massive threats, but the fixed `StructuredBuffer<float4>` threat path did not carry player/submarine light and acoustic sensory stimuli.
- The assigned `Assets/_Project/Scripts/AI/Boids/` domain path does not exist. The active boid implementation is the Sargassum GPU system under `Assets/_Project/Scripts/World/` plus its compute and instanced render shaders.
- Flashlight influence existed as a separate abyssal headlight panic scalar, not a slotted light ray threat that could produce beam parting and reactive fish albedo.

What was done:

- Added persistent `_boidSensoryThreatsNative` plus `_boidSensoryThreatBuffer` with 16 `float4` slots.
- Slot 0 now receives the submarine/player AUP-normalized runtime position with a clamped threat radius.
- Slot 1 now receives the flashlight/light endpoint, radius grows and shrinks from `SubmarineLightsChangedSignal` and player flashlight state.
- Slots 2-4 now receive recent `SignalBus<AcousticPingSignal>` positions and decay `w` by `dt * SensoryAcousticPingDecayMetersPerSecond`.
- `_PredatorAUPBuffer` is now the sensory threat array. Encounter predator data is isolated into `_EncounterPredatorAUPBuffer` so old predator scatter still works.
- The compute shader evaluates sensory threats camera-relative, clamps active radius to 0.1, and sets `BOID_FLAG_LIGHT_STIMULUS` for boids inside the beam.
- The instanced fish shader reads `BOID_FLAG_LIGHT_STIMULUS` and multiplies albedo/biolum response for beam entries.

Cinematic Cheats used:

- Low tier: flashlight is a sphere at the light cone endpoint, with shortened endpoint scale.
- Full tier: slot 1 becomes a capsule SDF using closest point on the player-to-endpoint segment.
- Render response is a one-bit state flag, not a per-fragment beam SDF or extra render buffer sample.

Exact Microseconds saved:

- Rejected player `Transform.position` polling: estimated 3 us/frame saved and no scene graph dependency.
- Rejected managed `Vector4[]` threat staging: estimated 5 us/frame saved and 0 GC.
- Rejected destructive light queue drain: estimated 2 us/frame saved by bounded SignalBus snapshot scan and no consumer starvation.
- Rejected CPU multi-sphere flashlight beam: estimated 10 us GPU/frame saved on MX350 low tier.
- Rejected render-side threat buffer/SDF sampling: estimated 8 us GPU/frame saved at dense fish visibility.
- Total estimated saved budget versus rejected implementation: 28 us/frame on i3/MX350.

Validation:

- `dotnet build Hecton8.Core.csproj` attempted three times.
- Build is blocked by unrelated integration dependencies: `GlobalRegistry.cs` missing `ProceduralLadderClimbRuntime`, later project-wide missing fields/types in `LockstepStateValidator`, `GlobalSignals`, `HectonFloatingOrigin`, `GameBootstrapper`, and others.
- No build diagnostics reference `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, or `BoidFishInstanced.shader`.
- `git diff --check` on touched files produced no whitespace errors; only repository line-ending warnings.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Inquisition Pass

What was wrong:

- The original XML marked blackbox logging N/A, but the later stability mandate required a last-300-frame sensory heartbeat.
- The shader beam mode used an HLSL `bool` flag, which is avoidable risk for Metal translation.
- Beam-reactive fish had a static brighten only; the visual response did not spend the saved low-tier cycles.

What was done:

- Added vault `BufferID.SargassumBoidSensoryBlackBox`.
- Added a 300-entry `BoidSensoryBlackBoxEntry` ring, packed to 64 bytes, owned by `GlobalDataVault` under `SystemID.WorldSargassum`.
- Recorded frame index, state hash, flags, active threat count, submarine slot, flashlight slot, and acoustic ping radii every sensory upload.
- Added anomaly-only dump to `Docs/AgentLogs/Dump_BOID_SENSORY_INPUT_PUMP.bin`.
- Added struct size validation so dispatch fails if the packed blackbox layout drifts.
- Replaced the compute shader beam `bool` with a `uint` mask for Metal/Apple Silicon hygiene.
- Added triangle-wave beam albedo/biolum pulse in the instanced fish shader.

Cinematic Cheats used:

- Toaster mode remains a flashlight endpoint sphere and decayed ping radii.
- God-mode still uses capsule SDF parting in compute, with a cheap pulse in render instead of per-fragment threat sampling.
- Blackbox I/O is anomaly-only, not frame logging.

Exact Microseconds saved:

- Rejected per-frame text logging: avoids unbounded I/O stalls; expected save is workload-dependent, but prevents MicroSD hitches.
- Rejected per-fragment beam SDF: estimated 8 us GPU/frame saved at dense fish visibility.
- Rejected local managed telemetry list: estimated 3 us/frame saved and 0 GC.
- Added sensory blackbox hashing cost: estimated 1-2 us/frame CPU on i3/MX350.
- Added beam pulse cost: estimated 3-5 fragment ALU on visible fish, no extra buffer fetch.

Validation:

- `dotnet build Hecton8.Core.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_Polish2.txt` and is blocked by missing `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs`.
- `dotnet build Assembly-CSharp.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_AssemblyCSharp.txt` and is blocked by missing RealtimeCSG files plus existing bootstrap/bucketing errors.
- Scans found no diagnostics for `SargassumMicroFaunaBoids.cs`, `H8Memory.cs`, `SargassumMicroFaunaBoids.compute`, or `BoidFishInstanced.shader`.
- `git diff --check` on touched code files produced no whitespace errors; only repository line-ending warnings.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Data-Sovereignty Pass

What was wrong:

- Sensory data used vault allocation, but `_boidSensoryThreatsNative` and `_boidSensoryBlackBox` still existed as local persistent NativeArray fields.
- Boid-originated debris, acoustic ping, and swarm dispersion events still used the legacy `GlobalSignals.Publish` wrapper.

What was done:

- Replaced sensory persistent NativeArray fields with `VaultBufferHandle<float4>` and `VaultBufferHandle<BoidSensoryBlackBoxEntry>`.
- Added vault handle ensure/resolve helpers and passed transient vault views through sensory slot writing, ping decay, upload, and blackbox recording.
- Replaced three `GlobalSignals.Publish` calls with direct typed `SignalBus<T>.Push` lanes.

Cinematic Cheats used:

- No new physical simulation was added. The low-tier endpoint sphere, full-tier capsule SDF, and triangle-wave light pulse remain the visual fake stack.
- No frame logging was added; blackbox I/O remains anomaly-only.

Exact Microseconds saved:

- Removed local persistent sensory collection ownership: 0 GC, fixed vault bytes unchanged.
- Direct typed lane publishing avoids legacy wrapper queue pressure during bursts: estimated 1-3 us/frame saved when predator kill/frenzy/dispersion signals fire.
- Vault handle resolution adds ~1 us/frame during active simulation upload, accepted to enforce data sovereignty.

Validation:

- `dotnet build Hecton8.Core.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_Polish3.txt`.
- Build remains blocked by unrelated `LockstepStateValidator`, `EcosystemDirector`, and `SubmarineFluidDynamics` errors.
- No diagnostics reference `SargassumMicroFaunaBoids.cs`, `H8Memory.cs`, `SargassumMicroFaunaBoids.compute`, or `BoidFishInstanced.shader`.
- Static scans: no `_boidSensoryThreatsNative`, no `_boidSensoryBlackBox` local data field, no `GlobalSignals.Publish`, no `EventBus`, no managed delegates, no `void Update()`, no `string.Format`, no `Transform.position` in the sensory surface.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Full Native-State Eviction

What was wrong:

- The sensory pump had been cleaned, but the surrounding active boid runtime still cached vault-backed state as persistent local `NativeArray` fields.
- Inactive statistical swarm rings used a nested local `NativeArray<T>` view.
- Predator bite staging still used a local persistent `NativeQueue<BoidKillSignal>`, even though the bite job is single-threaded and bounded.

What was done:

- Replaced persistent boid runtime `NativeArray` fields with `VaultBufferHandle<T>` handles for static obstacles, boid state, food-chain telemetry, leviathan path/node state, foveated LOD state, simulation frame constants, and threat-grid upload staging.
- Refactored `NativeRingBuffer<T>` to hold only a vault handle and cursor/count metadata.
- Replaced predator bite queue staging with vault-backed fixed `BoidKillSignal` slots plus a vault-backed count buffer.
- Added `BufferID.SargassumKillSignals` and `BufferID.SargassumKillSignalCount`.
- Set non-GPU vault/job structs touched by this pass to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = ...)]` and added `UnsafeUtility.SizeOf` layout gates.

Cinematic Cheats used:

- No new simulation truth was added. The visual stack remains low-tier endpoint sphere, high-tier capsule SDF, and triangle-wave beam pulse.
- Predator bite staging is now fixed-slot data, not a dynamic queue.

Exact Microseconds saved:

- Removed one local persistent native queue from the boid runtime: 0 steady-state GC; estimated 2-4 us saved during predator bite bursts from array/count drain versus queue enqueue/dequeue.
- Removed persistent local `NativeArray` ownership across the boid runtime: fixed vault memory unchanged, local owner state removed.
- Added vault handle resolution across active paths: estimated 3-6 us CPU on active boid frames, pending Unity Profiler proof.

Validation:

- `dotnet build Hecton8.Core.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_Polish7.txt` and succeeded.
- Remaining compiler warning: duplicate source inclusion for `EcosystemPopulationBalancer.cs`, unrelated to this task surface.
- No diagnostics reference `SargassumMicroFaunaBoids.cs`, `H8Memory.cs`, `SargassumMicroFaunaBoids.compute`, or `BoidFishInstanced.shader`.
- Static scans: no local `NativeQueue`, no `Allocator.Persistent`, no `private NativeArray<`, no `new NativeArray`, no old native field names, no `GlobalSignals.Publish`, no `EventBus`, no managed delegates, no `void Update()`, no `string.Format`, no `Transform.position`, no `Allocator.Temp` in the boid/shader surface.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Signal Recency and Stale-Light Correction

What was wrong:

- The boid sensory pump capped typed light and acoustic snapshot reads correctly, but it read the oldest entries when the lane held more events than the local cap.
- A submarine light remove/clear/brownout signal could leave a stale signal-light endpoint alive if the player flashlight was still on.
- The acoustic ping slot cursor used signed integer modulo, leaving a long-session overflow path to a negative slot.

What was done:

- Changed `SubmarineLightsChangedSignal` and `AcousticPingSignal` loops to scan the newest capped snapshot window.
- Cleared cached signal-light intensity on remove/clear/brownout unconditionally; player flashlight stimulus now uses the player-origin fallback instead of stale signal-light origin.
- Changed the acoustic ping write cursor to `uint` and kept the fixed three-slot ring.

Cinematic Cheats used:

- No real acoustic propagation or light cone physics were added. The low-tier endpoint sphere, full-tier capsule SDF, and triangle-wave fish flash remain the sensory fake stack.

Exact Microseconds saved:

- Newest-window indexing adds 0 us/frame versus the old bounded loop; it changes start index only.
- Removing stale signal-light intensity avoids false beam avoidance and visual flash; no measurable microsecond saving claimed.
- Unsigned cursor guard removes a long-session overflow fault path with 0 us/frame cost.

Validation:

- `dotnet build Hecton8.Core.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_Polish8.txt` and succeeded with 0 warnings and 0 errors.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Shader NaN Vaccination

What was wrong:

- The CPU threat writer clamped radii, but malformed GPU threat slots could still reach shader `max`, `dot`, direct segment division, and capsule closest-point math.

What was done:

- Added finite payload rejection before encounter and sensory threat radius math.
- Hardened `ClosestPointOnSegment` for non-finite sample/start/end, segment length, and projection.
- Replaced direct segment projection division with `projection * rcp(max(segmentLengthSq, EPSILON))`.

Cinematic Cheats used:

- Preserved the same scalability split: low-tier endpoint sphere, high-tier capsule SDF. No real raymarching or physical acoustic propagation added.

Exact Microseconds saved:

- No speedup claimed. This pass buys stability.
- Added finite checks are estimated under 1 us/frame on i3/MX350 because the loop remains capped at 16 threats and capsule math only runs for the flashlight slot on full tier.

Validation:

- `dotnet build Hecton8.Core.csproj --no-restore` logged to `Build_BOID_SENSORY_INPUT_PUMP_Polish9.txt` and succeeded with 0 warnings and 0 errors.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Sensory GraphicsBuffer Ping-Pong

What was wrong:

- The sensory threat upload used `LockBufferForWrite`, but still wrote into one GraphicsBuffer that the compute shader also read in the same simulation cadence.
- The first Polish10 validation attempt hit `NETSDK1004` because `Temp/obj/Hecton8.Core/project.assets.json` had been removed.

What was done:

- Split the boid sensory threat GPU resource into `_boidSensoryThreatBufferA` and `_boidSensoryThreatBufferB`.
- Selected the upload buffer from `_frameParity` and rebound `_PredatorAUPBuffer` to that selected buffer before `CSMain` dispatch.
- Kept the CPU staging data in the `GlobalDataVault` handle; no private `NativeArray` ownership was reintroduced.
- Ran `dotnet restore Hecton8.Core.csproj`, then reran the no-restore build.

Cinematic Cheats used:

- No new physical truth. The visual stack remains endpoint sphere on low tier, capsule SDF on high tier, and shader-side light flash.

Exact Microseconds saved:

- No measured profiler number is claimed. The change removes a potential driver stall class.
- Estimated win under upload/read contention: 0-2 us/frame on MX350/Steam Deck. Normal-frame cost is unchanged except one extra GraphicsBuffer resource.
- VRAM delta: +256 B payload for the second 16-slot float4 sensory buffer, plus Unity object overhead.

Validation:

- `Restore_BOID_SENSORY_INPUT_PUMP_Polish10.txt`: restore succeeded.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish10.txt`: `dotnet build Hecton8.Core.csproj --no-restore` succeeded with 0 warnings and 0 errors.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Dirty Upload Gate

What was wrong:

- The sensory upload was ping-ponged, but unchanged fixed-slot payloads still locked and copied into the parity-selected GraphicsBuffer every dispatch.
- GPU interop structs still showed Pack=4 in static scans, which is correct but needed source-level explanation to prevent blind Quest-layout edits.

What was done:

- Added a full 16-slot sensory payload hash over the vault-resolved `NativeArray<float4>`.
- Added per-buffer upload validity/hash fields for `_boidSensoryThreatBufferA` and `_boidSensoryThreatBufferB`.
- Skips `GraphicsBufferUploadUtility.UploadNativeArray` only when the selected parity buffer already holds the same payload hash.
- Resets the upload cache when sensory buffers are recreated or released.
- Added comments explaining Pack=4 GPU/HLSL interop structs and the existing layout validation gate.

Cinematic Cheats used:

- No new simulation truth. The cheap visual contract remains endpoint sphere on low tier, capsule SDF on high tier, and shader pulse on fish in beam.

Exact Microseconds saved:

- Hash cost: estimated under 1 us/frame for 16 `float4` slots on i3/MX350.
- Saved work on unchanged parity-buffer frames: one buffer lock plus 256 B memcpy, estimated 0-2 us/frame depending on driver contention. No measured profiler proof; status remains static/build verified.
- VRAM delta remains +256 B payload from the second sensory buffer.

Validation:

- `Build_BOID_SENSORY_INPUT_PUMP_Polish11.txt`: failed on external `ArchitectEyeVisualizer` errors.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish11_Strike2.txt`: failed on external `ArchitectEyeVisualizer` errors.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish11_Strike3.txt`: succeeded with 0 errors and 2 `MSB3026` output-copy retry warnings caused by another process holding the DLL.
- Static debt scan found no `void Update`, `string.Format`, local `NativeArray`, `NativeQueue`, `GlobalSignals.Publish`, managed delegates, `Allocator.Temp`, legacy `ComputeBuffer` type, `SetData`, or `GetData` in the boid/shader surface.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Compile-Wall Recheck

What was wrong:

- A fresh global `Hecton8.Core.csproj` recheck no longer reaches the boid sensory surface because unrelated files are changing under concurrent agents.

What was done:

- Ran the remaining compile-wall probes and captured the exact external blockers.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish12.txt`: duplicate `ArchitectEyeVisualizer.ValidatePackedStructSizes` plus ambiguous `LaserCutterEventPayload` errors in `AbyssalThermalManager` and `PlayerCriticalProceduralAudioRenderer`.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish12_Strike2.txt`: build command timed out with an empty log; the spawned process later exited without writing diagnostics.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish12_Strike3.txt`: missing `ThreadGroupSize`, `ThreadGroupShift`, and `ClearKernelTileSize` in `HectonMarineSnowRenderer`.
- Scanned the strike logs for `SargassumMicroFaunaBoids`, `BoidFishInstanced`, `H8Memory`, and `Sargassum`; no boid sensory diagnostics were present.
- Re-read the Omega section after core tasks were checked. `CURRENT_BATCH.md` has no standalone `<POLISH_MANDATE>` tag; the BOID XML block's `[VI. OMEGA POLISH MANDATE]` requires `STATUS: MUST BE "VERIFIED MASTER GRADE"`.

Cinematic Cheats used:

- None. This was validation and dependency-wall accounting only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed in this pass.

Validation:

- Latest global project build is blocked by external systems.
- Last successful post-boid-source build remains `Build_BOID_SENSORY_INPUT_PUMP_Polish11_Strike3.txt` with 0 errors and 2 external DLL-copy retry warnings.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Pre-Upload Sanitizer and Ordered Dump

What was wrong:

- The compute shader had non-finite guards, but the CPU upload path could still cache corrupt or stale vault slot payloads in the sensory GraphicsBuffer dirty gate.
- The blackbox dump path wrote ring memory order instead of chronological order and could reduce detailed sanitizer findings to a generic anomaly constant.

What was done:

- Added `SanitizeBoidSensoryThreatSlots` before sensory upload hashing and `GraphicsBufferUploadUtility.UploadNativeArray`.
- Sanitizer zeros non-finite fixed slots, clamps positive radii below 0.1 m, clears inactive slots, and returns a slot-specific anomaly hash.
- `RecordBoidSensoryBlackBox` now preserves the pre-upload anomaly hash and only folds in a second hash when the blackbox state itself is invalid.
- `TryDumpBoidSensoryBlackBox` now writes oldest-to-newest from the 300-frame ring cursor, sets the dump sentinel before disk I/O, and catches `IOException`/`UnauthorizedAccessException`.

Cinematic Cheats used:

- No new physics. The low tier remains endpoint sphere, high tier remains capsule SDF, and visual response stays shader flag/pulse driven.
- The sanitizer protects the fake from turning into driver poison on Quest/Android/Metal.

Exact Microseconds saved:

- No measured profiler number is claimed.
- Sanitizer cost: fixed 16 `float4` scan, estimated under 1 us/frame on i3/MX350.
- Saved failure cost is unbounded but rare: prevents non-finite slot data from entering the GPU upload cache.
- Dump I/O remains 0 us/frame in normal play; anomaly-only write is about 19.2 KB.

Validation:

- Static debt scan found no `void Update`, `string.Format`, local `NativeArray`, `NativeQueue`, `GlobalSignals.Publish`, managed delegates, `Allocator.Temp`, legacy `ComputeBuffer`, `SetData`, or `GetData` in the boid/shader surface.
- `git diff --check` on touched files produced no whitespace errors; only repository LF-to-CRLF warnings.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish13.txt`: failed on 112 external errors in `DiegeticGyroCompassRuntime`, `HeavyTowWinch`/`TetherSignals`, and `EcosystemDirector`.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish13_Strike2.txt`: failed on 24 external errors in `DiegeticGyroCompassRuntime` and `EcosystemDirector`.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish13_Strike3.txt`: `dotnet` exited `-1` with an empty log.
- Scans found no diagnostics referencing `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, `BoidFishInstanced.shader`, or `H8Memory.cs`.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Acoustic Panic Recency and NaN Guard

What was wrong:

- The sensory threat slots were using the newest acoustic ping window, but the secondary acoustic panic/scatter path still sampled the oldest movement and ping signals under burst pressure.
- CPU acoustic panic state could accept malformed radius/duration/strength/origin if called from a corrupted producer.
- Compute shader acoustic panic could still feed non-finite radius/origin/time into `smoothstep`, seed hashing, and normalization.

What was done:

- Changed `ConsumeMovementAcousticSignals` and `ConsumeAcousticPingSignals` to iterate the newest capped `SignalBus<T>.GetFrameSnapshot()` window.
- Added finite input rejection to `RegisterAcousticPanicBurst` before it mutates the acoustic panic state.
- Added finite guards to `ResolveAcousticPanicChaos` for position, origin, radius, strength, time remaining, radius square, distance square, seed, and simulation time.

Cinematic Cheats used:

- Kept acoustic response as a cheap deterministic panic fake: capped signal windows, dot-product radius gate, hash noise, L1 normalization.
- Rejected wider event history and physical sound propagation. Fish only need believable scatter from current pings and engine noise.

Exact Microseconds saved:

- Newest-window indexing adds 0 us/frame versus the old capped loops because loop counts are unchanged.
- Shader finite checks are estimated under 1 us/frame on i3/MX350 when acoustic panic is active; no profiler proof claimed.
- Prevented failure cost is stability, not a measured speedup: non-finite acoustic payloads no longer poison the GPU path.

Validation:

- Static debt scan found no `void Update`, `string.Format`, local `NativeArray`, `NativeQueue`, `GlobalSignals.Publish`, managed delegates, `Allocator.Temp`, legacy `ComputeBuffer`, `SetData`, or `GetData` in the boid/shader surface.
- `git diff --check` on touched files produced no whitespace errors; only repository LF-to-CRLF warnings.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish14.txt`: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1` succeeded with 0 warnings and 0 errors.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Finite Signal Scalar and Normalize Hardening

What was wrong:

- Light/acoustic producer payloads could carry NaN scalar values into `math.saturate`, then into sensory radius, acoustic panic, predator fear, or swarm-dispersed GPU-facing state.
- Shared CPU and compute L1 normalize helpers still used direct division and could return non-finite fallback vectors.
- `WriteBoidSensoryThreatSlot` converted any finite negative radius into an active minimum-radius threat.

What was done:

- Added `SaturateFinite01` and used it for submarine-light intensity, acoustic ping intensity, movement volume, sonar intensity, acoustic panic strength, VAT hit flash intensity, and swarm-dispersed signal intensity.
- Hardened `RegisterPredatorFearBurst`, `TryPublishSwarmDispersedSignal`, `RegisterVatHitReaction`, and the massive-displacement bridge with finite/positive checks before they mutate GPU-facing boid state.
- Replaced C# and compute-shader L1 normalize direct divisions with guarded reciprocal multiplication and finite fallback handling.
- Changed sensory slot writes to clear non-positive radius inputs instead of promoting them to the minimum active radius.

Cinematic Cheats used:

- Kept the Dear Lie intact: low-tier endpoint sphere, high-tier capsule SDF, acoustic panic as bounded hash/L1 vector fake.
- No physical sound propagation or wider history buffers were added.

Exact Microseconds saved:

- No profiler number is claimed.
- Added finite scalar checks are estimated under 1 us/frame on i3/MX350 for the capped signal windows.
- Reciprocal normalization is neutral to slightly cheaper than direct division on shader backends; the real gain is preventing NaN propagation and backend recovery stalls.

Validation:

- Static debt scan found no `void Update`, `string.Format`, local `NativeArray`, `NativeQueue`, `GlobalSignals.Publish`, managed delegates, `Allocator.Temp`, legacy `ComputeBuffer`, `SetData`, or `GetData` in the boid/shader surface.
- `git diff --check` on touched code files produced no whitespace errors; only repository LF-to-CRLF warnings.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish15.txt` and `Build_BOID_SENSORY_INPUT_PUMP_Polish15_Strike2.txt`: blocked by external `LockstepStateValidator.ValidateBinaryLayout`.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish15_Strike3.txt`: blocked by external missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`.
- Log scans found no diagnostics referencing `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, `BoidFishInstanced.shader`, or `H8Memory.cs`.

## 2026-05-16 BOID_SENSORY_INPUT_PUMP Headlight Shader Frame-Constant Guard

What was wrong:

- The sensory threat buffer path was sanitized, but shader helper paths for headlight photophobia and high-tier player curtain parting still trusted frame constants.
- A malformed player position, forward vector, panic radius, boid body radius, or headlight panic scalar could bypass the slot sanitizer and produce NaN acceleration.
- Shader L1 fallback vectors were finite-checked but returned raw, not normalized.

What was done:

- Added finite guards to `ResolveHeadlightPhotophobiaForce` before panic, axial cone, radial cone, and force math.
- Added finite guards to `ResolvePlayerCurtainPartingForce` before high-tier curtain split math.
- Changed shader `CheapNormalizeL1` fallback handling to finite-check and normalize fallbacks through guarded `rcp`.

Cinematic Cheats used:

- Preserved the cheap low-tier sphere lie and the high-tier capsule/curtain beam-parting behavior.
- Rejected removing the visual helper; the fix is validation around the fake, not visual downgrade.

Exact Microseconds saved:

- No profiler number is claimed.
- Added finite checks are estimated under 1 us/frame on i3/MX350 when the light/curtain helpers are active.
- Avoided cost is GPU NaN propagation on mobile/Metal, not measured steady-state frame time.

Validation:

- Static debt scan found no `void Update`, `string.Format`, local `NativeArray`, `NativeQueue`, `GlobalSignals.Publish`, managed delegates, `Allocator.Temp`, legacy `ComputeBuffer`, `SetData`, or `GetData` in the boid/shader surface.
- `git diff --check` on touched code/docs produced no whitespace errors; only repository LF-to-CRLF warnings.
- `Build_BOID_SENSORY_INPUT_PUMP_Polish16.txt`: blocked by external `ArchitectEyeVisualizer.DebugSignal`.
- No diagnostics referenced `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, `BoidFishInstanced.shader`, or `H8Memory.cs`.
