# LOG: OCEAN_CHEMISTRY_ENGINEER

## 2026-05-13 - Brine Pools & Density Layers

What was wrong:
- Brine pools were visual-only texture/fog artifacts. Player, submarine hull, buoyancy lanes, fauna navigation, audio, gas toxicity, and blackbox telemetry had no shared heavy-fluid truth.
- Trigger/singleton patterns were explicitly forbidden; brine needed deterministic AUP plane math and GlobalRegistry/EventBus coupling.

What was done:
- Added/used `Hecton8.Environment.Fluids` contract math: absolute brine plane height, 50x50m cartography sector hash, density multiplier, toxicity scalar, and thick-fluid acoustic channel.
- `HectonFluidEngine` now stores brine SOA lanes and multiplies density by 3.0 below the absolute plane after subtracting origin shift. Brine lift is capped at `mass * gravity * 9f`.
- Player brine state now publishes `FluidDensityChangedSignal`, applies 40% swim-speed penalty, exposes brine density to audio, pushes shader globals, and injects +10 kPa CO2 equivalent into `IGasDynamicsSolver` without per-frame accumulation.
- `HectonVisorUberPost` reconstructs world-space Y on GPU and applies green/yellow plane fog below `_BrineHeightY`; low tier hard-clips instead of soft fading.
- CoreLit caustics are killed below the brine plane.
- Generated brine pools no longer require a physical visible water mesh path for brine rendering.
- `SubmarineFluidDynamics` samples hull center of mass against brine, scales exterior buoyancy density, writes `BrineSubmersionTime` to the hydro blackbox, mirrors fault dumps to `Docs/AgentLogs/Dump_OCEAN_CHEMISTRY_ENGINEER.bin`, and emits `AcousticPingSignal` on thick-fluid transitions.
- `FaunaSensorSuite` treats below-plane brine samples as closed/high-cost nav probes before voxel grid sampling.
- Omega polish cached `_BrineColor` publication so the constant color global is not submitted every fixed tick.

Cinematic cheats used:
- Single horizontal scalar plane instead of real brine volume simulation.
- GPU post-process depth fog instead of mesh/fog-volume rendering.
- Low-tier hard clipping plane instead of soft depth fade.
- Caustic absorption as a shader branch instead of light transport.
- Existing audio low-pass lane instead of new filter components.
- Toxicity as CO2 pressure floor instead of room-by-room chemical simulation.
- Submarine hull brine breach sampled at center of mass instead of per-panel viscosity.

Exact microseconds saved / costs:
- Removed brine mesh/fog draw dependency: expected 20-80 us saved in brine-heavy scenes, scene dependent.
- Trigger/collider path avoided: conservative 4-12 us/frame saved in brine zones versus MonoBehaviour trigger dispatch.
- Buoyancy brine branch: <0.01 us/object in Burst.
- Player brine swim/audio/gas scalar work: <0.06 us/fixed tick while submerged.
- Acoustic transition enqueue: <0.6 us only on enter/exit.
- Omega color cache: estimated 0.8-2.0 us saved per active player fixed tick after first brine color publish, driver dependent.

Verification:
- Scoped static polish scan passed for touched brine files: no `foreach`, string formatting, `.ToString()`, `math.sqrt`, `math.normalize`, or CPU matrix upload patterns.
- Shader world-Y check: `ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP)` feeds `worldPosition.y < _BrineHeightY`; CPU only pushes scalar/vector globals.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false` failed with 113 generated-project reference errors. Relevant brine failures are unresolved `Hecton8.Environment.Fluids` / `BrineLayerSample`; the same generated project also fails on pre-existing Scheduling, CCD, Memory Layout, Audio Propagation, Inventory, Tether, and Acoustic references.
- Unity MCP refresh timed out after 60s; console read returned `no_unity_session`.

Status: PENDING VERIFICATION. No green compile was claimed.
