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
