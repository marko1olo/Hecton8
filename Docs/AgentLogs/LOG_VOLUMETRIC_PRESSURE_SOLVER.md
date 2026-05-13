# VOLUMETRIC_PRESSURE_SOLVER Report

What was wrong:
- Habitat pressure/damage produced gameplay stress but interior wall geometry stayed flat.
- No per-module shader scalar existed for pressure bowing.
- Runtime CPU mesh deformation is forbidden for this path.

What was done:
- Extended `HabitatGraphManager` with persistent per-module `NativeArray<float>` stress lanes, previous-stress delta tracking, decaying impact spikes, compromised hysteresis, and a native `GraphicsBuffer` upload path.
- Added `Hecton_HabitatInterior.hlsl` and wired `Hecton_DryZoneLit.shader` to bend interior vertices in object space from module stress.
- Added low-tier MX350 path: vertex deformation disabled, peak-stress crease overlay applied through detail mask.
- Added cheap rsqrt normal bias so bowed panels do not retain completely flat lighting.
- Added `BaseModuleCompromisedSignal` event lane and publish path when stress reaches the deformation threshold.
- Extended habitat blackbox version to write `PeakModuleStress` and deformation sequence; invalid stress dumps to `Docs/AgentLogs/Dump_VOLUMETRIC_PRESSURE_SOLVER.bin`.
- Created `Hecton8.Habitat.Deformation.Contracts` asmdef and read-model contract for future consumers without moving the runtime graph owner.

Cinematic Cheats used:
- Shader vertex bow instead of CPU mesh mutation.
- Sine UV panel mask: `sin(uv.x*pi) * sin(uv.y*pi)`.
- Low-tier detail-map crease instead of vertex displacement.
- Peak scalar fallback on low tier to skip per-module vertex index lookup.
- rsqrt safe-normal bias instead of exact normalize.

Exact microseconds saved:
- CPU mesh mutation rejected: estimated 120-800 us/frame saved on i3/MX350 during active stress scenes.
- Managed material/property-array uploads rejected: estimated 20-90 us/frame and 0 B/frame GC saved.
- Low-tier vertex index scan bypass: estimated 2-5 us per 1k interior vertices on MX350.
- Exact normal removal in new shader include: estimated 1-3 us per 1k affected vertices versus exact normalize on low-end GPU.

Verification:
- `dotnet build Hecton8.Core.csproj -v:minimal`: FAILED due 107 unrelated missing namespace/type references before habitat verification.
- Unity MCP refresh/read console: unavailable, `no_unity_session`.
- Scoped scan found no runtime `Mesh.vertices` mutation in owned habitat paths.
- Scoped anti-bloat scan found no new managed `foreach`, LINQ, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `.normalized`, or HLSL `normalize()` in owned additions.
- `git diff --check` returned only repository CRLF normalization warnings for existing files.

Status:
- PENDING VERIFICATION. Global compile wall is outside this task and outside ECHELON 6 ownership.

Follow-up upgrade:
- Corrected stress-buffer ordering to match the renderer's `BaseModule` active module order.
- Added active-order hash reset for transient stress state.
- Added low-tier shader-param dirty tracking.
- Added one-shot module stress dump guard.
- Scoped build filter found no touched-file compiler errors; full project build still fails on unrelated global missing references.

Follow-up upgrade 2:
- Clamped CPU module stress publication to the same 64 visible module slots used by `_HectonModuleAmbienceDataBuffer`.
- Changed `HectonHabitatInteriorResolveStressIndex` to return a sentinel when no module radius contains the vertex; shader stress read now resolves that to zero stress instead of leaking slot 0.
- Re-ran serialized `dotnet build` with node reuse/analyzers/shared compilation disabled. Result: exit code 1, 130 global error lines, 0 errors matching `HabitatGraphManager`, `GlobalSignals`, `HabitatDeformation`, `Hecton_Habitat`, `DryZoneLit`, `ManualOverridePulledSignal`, or `BaseModuleCompromisedSignal`.
- Unity MCP refresh timed out waiting for editor readiness; console read still reports `no_unity_session`.

Exact microseconds saved after follow-up 2:
- Stress render path capped at 64 modules: avoids wasted CPU stress/upload work above shader-visible capacity; estimated 2-12 us saved in oversized bases on i3/MX350.
- No-match sentinel prevents false slot-0 deformation/crease; visual correctness fix with no extra texture fetch and one cheap comparison already present in the read path.

Follow-up upgrade 3:
- Split module stress clearing so active-order resets do not publish a zero shader vector immediately before replacement upload.
- Added runtime instance-id fallback for no-graph modules in stress order hashing and direct target hash resolution.
- Re-ran static checks: no mesh mutation, no exact normalize/sqrt, no managed collection/string offenders in the stress methods, and `git diff --check` reports only repository CRLF normalization warnings.
- Re-ran serialized `dotnet build`: exit code 1, 139 global error lines, 0 errors matching pressure-solver touched files or contracts.
- Unity console is reachable; current errors are unrelated `Assets/_Project/Scripts/Audio/Virtualization/AudioVirtualizationJobs.cs(189,27)` and `(190,27)` `CS1615 ref` errors plus one entry-point exception.

Exact microseconds saved after follow-up 3:
- Removed redundant zero shader publication on stress order rebuild ticks; estimated 1-4 us saved on driver-bound MX350 frames during pooled module activation or graph rebuild.
- Runtime-key fallback prevents spike/hysteresis migration for no-graph modules without adding managed state; cost is one instance-id read only on fallback modules.
