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
