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
