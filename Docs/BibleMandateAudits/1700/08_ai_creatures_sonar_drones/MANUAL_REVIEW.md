# AI / Creatures / Sonar / Drones Manual Review

Status: STATIC REVIEW - NO AI/CREATURE PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
- `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

## What Exists

- AI/creature/sonar/drones bibles exist and require state cadence, sensory truth, black-box telemetry, and bounded quality scaling.
- `NativeAudioFrameRingBuffer` has a strong fixed-storage SPSC shape and does not show managed hot allocation in the reviewed write route.
- `TopographicalSonarSynthesizer` schedules scan/fade work with job fences and uses graphics buffers for point rendering.
- `EcosystemRuntimeInstaller` can ensure ecosystem systems exist during scene bootstrap.
- `SargassumMicroFaunaBoids` owns GPU buffers/jobs/readback routes for microfauna presentation and has a continuous-density scaling obligation under ecosystem/creatures/rendering bibles.

## What Is Missing / Not Proven

- `FaunaBrain` clones runtime materials per fauna presentation route. That is a batching/material ownership risk for crowds unless the actor count is low or MPB/GPU instance data is used.
- `EcosystemRuntimeInstaller` dynamically creates a root and adds components. Production scenes should prefer an authored bootstrap prefab and leave installer behavior as recovery.
- Sonar proof still needs confidence/staleness gameplay proof, ping cadence proof, and GPU upload budget proof.
- `SargassumMicroFaunaBoids` clones an owner-local boid material and uses async readback; this needs SRP/material and cadence proof before crowd/ecology acceptance.
- `DroneFleetManager` contains mock repair/mining signal routes, mock SDF grid routes, fallback chassis specs, and a procedural drone material route. This is not acceptable production truth without strict disabling/proof.

## Current Classification

- `FaunaBrain.cs`: `YELLOW_BATCHING_RISK`.
- `EcosystemRuntimeInstaller.cs`: `YELLOW_BOOTSTRAP_PREFAB_ROUTE_REQUIRED`.
- `NativeAudioFrameRingBuffer.cs`: `GREEN_STATIC_RING_BUFFER_SHAPE`.
- `TopographicalSonarSynthesizer.cs`: `YELLOW_UI_SENSOR_PROOF_REQUIRED`.
- `SargassumMicroFaunaBoids.cs`: `YELLOW_GPU_BOID_MATERIAL_READBACK_PROOF_REQUIRED`.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.

## Required Next Proof

- AI black-box last-300-frame rings under predator/prey/path stress.
- Fauna material/SRP batcher proof or conversion to shared materials plus MPB/GPU data.
- Sonar confidence/staleness and ping spam proof.
- Microfauna GPU/material/readback proof and production drone truth proof; mock drone routes must be editor/test/headless-only or disabled for release gameplay.
