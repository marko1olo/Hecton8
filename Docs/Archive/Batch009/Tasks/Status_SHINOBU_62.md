# Status_SHINOBU_62

Agent: SHINOBU_62
Domain: OCEAN_SURFACE_AND_ATMOSPHERE_DIRECTOR
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`, second duplicate `SHINOBU_62` block, role `OCEAN_SURFACE_AND_ATMOSPHERE_DIRECTOR`, starts at line 2332.
Task count: 20
State: IN_PROGRESS - Gerstner/Burst CPU truth, HLSL AUP phase parity, GlobalQualityWeight wave interpolation, no-skybox scattering, telemetry, tuner, and self-audit are recorded; compile is gated.
Contamination note: a parallel stale writer keeps restoring flora/fauna text to SHINOBU_62 files. For this user request, flora/fauna authority is rejected.

## Task Matrix

- [x] 01 Binary graveyard reconnaissance; cold OSHINO lookup plus emergency mock weather. Rejected fatal missing payload; 0 us hot path.
- [x] 02 Flat water eradication; no Unity Plane or standard Skybox route. Rejected flat/texture sky. GPU math path only.
- [x] 03 CS1612 purge; hot DTO scan has no `{ get; set; }` / `{ get; private set; }`. Rejected property-backed NativeArray structs.
- [x] 04 ARM64 padding; `WaveParametersDTO` 32B, atmosphere/weather/telemetry 64B. Rejected `Pack=1`.
- [x] 05 Blind buoyancy mock; 10,000-query Burst mock path exists. Rejected submarine dependency.
- [x] 06 Burst Gerstner evaluator; `EvaluateWaves(double3 AUP, float time, NativeArray<WaveParametersDTO>...)` wraps meters/phase before `sincos`. Rejected FFT/fluid sim.
- [x] 07 Waterline breach; camera AUP sample emits `WaterlineBreachSignal`. Rejected trigger collider.
- [x] 08 Atmosphere CBuffer; Rayleigh/Mie/planet parameters drive analytical sky. Rejected default skybox.
- [x] 09 Foam/whitecaps; HLSL Jacobian scalar with quality gate. Rejected particles.
- [x] 10 Wind advection; weather publishes global flow vectors. Rejected direct kelp dependency.
- [x] 11 Continuous mesh LOD; radial grid params lerp by `GlobalQualityWeight`. Rejected binary hardware switch.
- [x] 12 Storm surge link; narrative/global state overrides wave and sky scalars. Rejected cutscene-only weather.
- [x] 13 AUP localized shader projection; HLSL phase uses `cameraLocalXZ + _H8OceanCameraAupLocalProjection.xy`. Rejected absolute GPU coordinates and local-only phase.
- [x] 14 Rain disturbance; shader hash-normal ripple. Rejected rain collision particles.
- [x] 15 Physics buoyancy broadcast; local/AUP batch jobs return heights/normals. Rejected per-object virtual sampling.
- [x] 16 Zero-init boot allocation; Vault handles only, overwritten buffers may use uninitialized memory. Rejected private persistent NativeArrays.
- [x] 17 Telemetry recorder; 300-frame ring and dump path. Rejected chat-only crash forensics.
- [x] 18 Weather tuner editor; UI Toolkit `Atmosphere & Wave Tuner`. Rejected IMGUI.
- [x] 19 CSV override ingestor; native byte parser and endian-defensive legacy float reader. Rejected runtime string split/JSON.
- [x] 20 Live wave profiler gizmo; blue SceneView lines sample CPU wave truth. Rejected decorative-only gizmo.

## Verification

- Exact-file forbidden scan: PASS for touched ocean runtime/contracts/editor/HLSL/tests.
- Hot-path scan: PASS; no private persistent NativeCollections, no hot accessor properties, no arbitrary `.Complete()`.
- Burst/alias scan: PASS; exact Burst flags and `[NoAlias]` arrays.
- Shader parity scan: PASS; HLSL uses wrapped camera AUP projection before phase.
- Diff hygiene: PASS; line-ending warnings only.
- Compile: not relaunched; latest CPU gate sampled 100%, and prior forced build is blocked upstream by unrelated duplicate methods in `AssetLifecycleGovernor.cs`.
