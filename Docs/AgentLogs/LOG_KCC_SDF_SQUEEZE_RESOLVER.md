# LOG_KCC_SDF_SQUEEZE_RESOLVER

## 2026-05-16 - SDF Tight Gap Traversal
What was wrong:
- Existing player kinematics sampled voxel SDF but treated solid overlap as `FaultSolidTeleport`, forcing hard snap/velocity zero on jagged cave edges.
- No `KCCManager.Instance` or player `OnCollisionStay` debt existed in the searched Gameplay/Physics KCC scope; the actual debt was the solid-overlap fallback.
- Player kinematic hot state was still locally owned before the vault handoff path.

What was done:
- Added `Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs`.
- Integrated SDF squeeze before `PlayerKinematicsBodyJob` solid-fault handling.
- Preferred `GlobalDataVault` for player position, velocity, and intended movement SOA buffers.
- Read/wrote `BufferID.PlayerKinematicState` as `LockstepPlayerKinematicState`.
- Published squeeze feedback through existing lanes: `PlayerStateSignal`, `HapticRequest`, `AcousticPingSignal`, `PhysiologyStateSignal`, and `IGasDynamicsSolver.TryApplyPlayerRoomCarbonDioxideEquivalentPressure`.
- Added KCC SDF telemetry flags and `Dump_KCC_SDF_SQUEEZE_RESOLVER.bin` to the fault dump set.
- Added Unity `.meta` files for the new KCC folder and job script.

Cinematic cheats used:
- Low/MX350: 4-tap tetrahedral SDF gradient instead of 6-axis gradient.
- High/Ultra: reuse SDF normal for micro camera roll; no extra body-twist physics.
- Homeostasis: when `SignalBusRegistry.SystemStress01 > 0.8`, sample every 5 frames and interpolate cached push-out.
- Scrape response is signal-driven fake feedback, not physical material simulation.

Exact microseconds saved:
- No `CapsuleCast`/collision callback repair path: estimated 35-80 us saved per active low-tier squeeze frame.
- DataVault SOA sharing: estimated 5-15 us saved by removing duplicate hot kinematic copies.
- Homeostasis 5-frame cadence: estimated 25-60 us saved across sustained squeeze frames.
- Signal-driven scrape feedback instead of concrete audio/haptic polling: estimated 8-12 us saved per active feedback frame.
- Total expected active squeeze saving on i3/MX350: 73-167 us per frame, before profiler validation.

Validation:
- `dotnet build Hecton8.Core.csproj` initially exposed KCC integration errors; fixed `SignalBusRegistry.SystemStress01` and moved `ApplyForwardSpeedPenalty` into the runtime class.
- Final `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` still fails with 178 foreign errors outside this prompt, including HectonVoxelEngine/Core.Contracts references, FaunaBrain missing `_slot`, HarvestableOutcrop missing `DebrisSpawnSignal`, Bootstrap contract reference failures, LockstepStateValidator missing ghost replay fields, GlobalSignals missing unrelated signal types, and HectonFloatingOrigin missing shader vault bridge.
- Final Roslyn log `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_core_final.exit.txt` contains no `PlayerKinematicsRuntime` or `SdfSqueezeJob` errors.

## 2026-05-16 - Multiplatform Data Sovereignty Pass
What was wrong:
- Prior DataVault eviction only covered position, velocity, and intended movement. Flow velocity, last-valid position, sync states, hand targets, telemetry ring/cursor, fault flags, probe batches, and SDF squeeze results still used local-primary NativeArrays.
- NativeArray payload structs in the resolver path used sequential Pack=4 layout instead of explicit byte offsets.

What was done:
- Added `BufferID.PlayerKinematicFlowVelocity` through `BufferID.PlayerKinematicSdfSqueezeResults`.
- Routed every `PlayerKinematicsRuntime` persistent NativeArray through `AllocateRuntimeArray`, with H8Memory left only as cold fallback when the vault is unavailable.
- Converted resolver payloads to explicit `Pack = 1` layouts: `SdfSqueezeResult`, runtime telemetry, sync state, accumulator state, hand target, and player telemetry.
- Re-scanned the resolver path for `Update`, `string.Format`, legacy `EventBus`, managed delegates, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, and `OnCollisionStay`; none were found.
- Confirmed KCC has no compute/shader files, so this pass adds no Metal thread-group or DirectX-only shader risk.

Cinematic cheats used:
- Low tier still uses 4-tap tetrahedral SDF and slow-cadence interpolation under stress.
- High/Ultra still buy camera roll, scrape haptics, and acoustic fabric scratch from the same SDF normal/stress scalar.
- Salt/silt/hull-dent overkill remains downstream VFX territory; locomotion emits typed signals and does not add collision probes to fake art.

Exact microseconds saved:
- DataVault eviction expansion: estimated 2-8 us saved from reduced duplicate cache churn.
- Explicit payload layout: 0 us direct frame saving; removes ARM64 padding ambiguity and crash class.
- No new per-frame disk I/O; Steam Deck MicroSD impact remains 0 us outside fault dumps.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` captured in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_data_vault_pass.exit.txt`.
- Build still fails with 3 foreign errors only: missing `Hecton8.AI.Sensory`, missing `TetherFiredSignal`, and missing `AcousticEchoHuntResult`.
- Captured log contains no diagnostics naming `SdfSqueezeJob`, `PlayerKinematicsRuntime`, `HectonPlayerState`, or `H8Memory`.
