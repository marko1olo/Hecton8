# Rationale_THERMODYNAMICS_LEAD

Status: PENDING VERIFICATION

## D0: Prompt, Domain, Mandates

Problem: Thermodynamics task touches physics, survival, rendering, save, audio, and AUP surfaces; direct concrete references would collide with concurrent agents.
Solution: Treat thermodynamics as domain 65, own heat diffusion in a registry service, and use contracts/signals for cross-domain effects.
Rejected Alternatives: Direct edits across concrete managers as primary dependency; trigger-collider temperature zones; Texture3D logic storage.
Scalability potential: Low tier uses nearest-source math; Middle/High/Ultra can spend saved cycles on richer frost/haze and denser diffusion.
Hardware Impact: NativeArray 32^3 float grid costs 128KB per buffer, 256KB double-buffered before source/insulation metadata. Acceptable on i3/MX350 when bypassed on Low/MX350.

## D1: Service Contract and Signal Lane

Problem: Existing thermal code exposed manager-style behavior; prompt requires no singleton dependency and an AUP temperature signal.
Solution: Added `IThermodynamicsService.TryGetThermalGridReadback(...)`, kept thermodynamics registered through `GlobalRegistry`, and added `TemperatureChangedSignal` as a 64-byte explicit payload pushed through `SignalBus`.
Rejected Alternatives: Reintroduce `AbyssalThermalManager.Instance`; mirror to legacy C# events; use managed delegates from thermal tick.
Scalability potential: Low uses direct service sampling; Ultra can consume full-grid readback for VFX/AI without physics coupling.
Hardware Impact: Signal push is a native queue enqueue. Estimate 0.5-1.5 us per temperature event on i3/MX350, zero managed allocation after lane initialization.

## D2: 32^3 Native Thermal Grid

Problem: A global water temperature cannot freeze submarines or localize geyser heat.
Solution: Replaced the 2D thermal plane with 32x32x32 `NativeArray<float>` read/write/source buffers plus insulation and 2D visual projection. Jacobi runs as a Burst `IJobParallelFor`.
Rejected Alternatives: Texture3D as logic storage; per-cell GameObjects; collider volumes; full CFD or particle heat simulation.
Scalability potential: Low/MX350 bypass grid and sample nearest heat source; Middle uses grid; High/Ultra can raise visual response density around the same native data.
Hardware Impact: 32768 cells * 6-neighbor Jacobi is bounded. Estimated cold tick cost: Low 0 us grid, Middle 350-700 us per 0.2Hz tick, Ultra same core cost plus optional visual overkill.

## D3: Voxel SDF Insulation and Deep Brine

Problem: Thermal diffusion must respect solid voxel mass and deep brine must sit below freezing.
Solution: Rebuild source/insulation fields from `HectonVoxelVolume.GetSDFDensity(...)`; positive SDF density damps diffusion. Ambient resolves to -2C when runtime Y depth is below -1000m, including no-vent grid fills.
Rejected Alternatives: Physics overlap checks, voxel collider queries, material lookups, or per-frame scene scans.
Scalability potential: Toaster path avoids SDF grid rebuild; High/Ultra can spend cycles on sharper ice/frost reaction.
Hardware Impact: SDF sampling is main-thread cold tick work, not per-frame. Low tier saves the whole pass; MX350 avoids 32768 SDF reads.

## D4: Persistent Vent Registry and +200C Injection

Problem: Runtime vents previously entered thermodynamics directly from geology; prompt requires active thermal vents through `PersistentWorldRegistry`.
Solution: Added bounded `PersistentThermalVentRecord[16]` snapshot in `PersistentWorldRegistry`, geology bridge writes/registers/unregisters, and thermodynamics syncs by revision before rebuilding vents. Source injection uses at least +200C, scaled by vent weights.
Rejected Alternatives: Unbounded lists, dictionary lookups during diffusion, or making geology own thermal sampling.
Scalability potential: Low uses DistanceSq against synced vents; High/Ultra diffuses the same PWR-sourced vent field.
Hardware Impact: 16-record managed array cold allocation only; sync is O(16) slow tick. Estimated under 5 us on i3/MX350 when revision changes.

## D5: Consequence Hooks

Problem: Thermal field must affect presentation, submarine mobility, structural damage, audio, and room gas without new hard dependencies.
Solution: Shader gets local temperature and screen-space frost below 0C; submarine context receives a thermal speed multiplier; rapid hot-to-cold transition emits packet-native `CombatDamageSignal`, `AcousticPingSignal`, and `TemperatureChangedSignal`; gas solver stores room temperature and halves scrubber removal below 0C.
Rejected Alternatives: Applying force drag to rotors, direct integrity component calls, audio manager calls, or per-room MonoBehaviour temperature polling.
Scalability potential: Low still applies gameplay consequences from direct sample; Ultra can layer more visual frost and acoustic resonance on the same signals.
Hardware Impact: Gameplay hooks are branch-heavy but O(1). Estimated 2-8 us per fixed tick for player/sub samples; gas job adds one float read and branch per room.

## D6: AUP, Save Delta, Black Box

Problem: Thermal grid must survive origin shifts, persist useful deltas, and dump last-state evidence on NaN.
Solution: `AupShiftSignal` now reaches `SignalBus`; thermal manager shifts logical grid origin and cached vents. RLE staging compresses non-ambient cells into `SaveBinaryStorage.ThermalGridRleRun`. Blackbox dump path is `Docs/AgentLogs/Dump_THERMODYNAMICS_LEAD.bin` and telemetry records `PlayerAmbientTemp`.
Rejected Alternatives: Rebuild grid from world origin every shift; save full 32768-float map; log text telemetry per frame.
Scalability potential: Low saves no dense grid churn; High/Ultra can use the RLE output for richer restore visuals.
Hardware Impact: AUP shift is O(vents + cached arrays) only on shift. RLE scan is 32768 cells per completed cold tick, estimated 100-250 us on i3/MX350.

## D7: Compile Wall

Problem: `Hecton8.Core.csproj` cannot isolate thermodynamics compile because project references are already missing before thermal code is reached.
Solution: Ran three build attempts after targeted fixes and retained logs: `Build_THERMODYNAMICS_LEAD_01.log`, `_02.log`, `_03.log`. Unity MCP validation unavailable because no Unity session exists.
Rejected Alternatives: Editing unrelated missing assemblies (`Core.Memory.Layout`, `Audio.Propagation`, CCD, brine, tether, inventory algorithms); claiming Burst verification without compiler evidence.
Scalability potential: None; dependency wall blocks objective verification.
Hardware Impact: No runtime estimate possible for Burst output until Unity/assembly references are restored.
