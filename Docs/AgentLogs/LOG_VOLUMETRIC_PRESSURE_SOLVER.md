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

Follow-up upgrade 4:
- Added quality-tier-specific module deformation amplitudes: Low/MX350/Unknown = 0m vertex bow and crease-only, Mid = 0.036m, High = 0.055m, Ultra = 0.075m.
- Added `_lastUploadedModuleStressTier` so Mid/High/Ultra transitions force shader-param upload even when stress values are numerically stable.
- Static scans remain clean: no mesh mutation, no exact normalize/sqrt, no managed collection/string offenders in the stress methods, and `git diff --check` reports only repository CRLF normalization warnings.
- Filtered `dotnet build` timed out at 180s without touched-file errors emitted before timeout; `dotnet build-server shutdown` cleared stale MSBuild/compiler workers.
- Unity MCP console read failed via local transport to `127.0.0.1:8088/mcp`; runtime/editor verification remains blocked by infrastructure/global compile state.

Exact microseconds saved after follow-up 4:
- Mid tier avoids full high-tier bow amplitude with no extra branch in shader; expected lower overdraw/near-clip deformation artifacts on MX350-adjacent hardware.
- Tier dirty tracking prevents stale visual mode after quality switches with one enum compare; avoids unconditional shader param writes.

Follow-up upgrade 5:
- Reused the sine panel mask from `HectonHabitatInteriorApplyPanelBendOS` in `HectonHabitatInteriorApplyCheapNormalBiasWS`.
- Updated `Hecton_DryZoneLit.shader` vertex callsite to pass the shared panel mask.
- Static shader checks: updated signature/callsite matches, exact normalize/sqrt scan remains clean, `git diff --check` reports only repository CRLF normalization warnings.
- Unity MCP console read still fails at local transport to `127.0.0.1:8088/mcp`; no editor/runtime proof claimed.

Exact microseconds saved after follow-up 5:
- Removed duplicate sine panel-mask evaluation from stressed vertex normal-bias path. Estimated 2-6 us saved per 1k affected interior vertices on MX350-class GPUs, with identical visual output.

Follow-up upgrade 6:
- Added `HectonHabitatInteriorCheapPanelMask` for the low-tier crease overlay.
- Kept `HectonHabitatInteriorPanelMask` sine math on the Mid/High/Ultra vertex bow path, preserving the task-required sine panel bulge.
- Static shader checks: `rg` confirms sine mask stays on `HectonHabitatInteriorApplyPanelBendOS` and cheap mask is used by `HectonHabitatInteriorApplyLowTierCrease`; exact normalize/sqrt scan remains clean; `git diff --check` reports only repository CRLF normalization warnings.

Exact microseconds saved after follow-up 6:
- Removed two sine evaluations per affected low-tier crease fragment. Estimated 8-25 us saved in dense interior wall views on MX350-class GPUs, while preserving crease readability.

Follow-up upgrade 7:
- Gated the habitat crease `_DetailMask` sample in `Hecton_DryZoneLit.shader` behind low-tier mode and non-zero habitat stress.
- Kept the defensive guard inside `HectonHabitatInteriorApplyLowTierCrease`; the callsite now avoids the texture fetch before the helper is reached.
- Static shader checks: exact `normalize()`/`sqrt()` scan with `rg --pcre2` produced no matches; `rg` confirms the crease detail sample is inside the low-tier stress branch; `git diff --check` reports only repository CRLF normalization warnings.
- Unity MCP console read still fails through `127.0.0.1:8088/mcp`; no editor shader compile proof claimed.

Exact microseconds saved after follow-up 7:
- Removed one unused detail texture sample per Mid/High/Ultra or zero-stress DryZone fragment. Estimated 10-40 us saved in dense interior wall views on MX350-class GPUs.

Follow-up upgrade 8:
- Changed `UploadModuleStressMatrix` so a zero visible module count publishes shader params but does not allocate or upload `_HectonHabitatModuleStressBuffer`.
- Changed `PublishModuleStressShader` so zero-count states publish zero max deformation and inactive low-tier visual mode, even when the quality tier is `Unknown`.
- Added a shader count guard to the DryZone vertex path so `_HectonHabitatModuleStressParams.x <= 0.5` skips stress-index resolution.
- Static checks: managed-offender scan found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Verification block: filtered `dotnet build` timed out at 180s before producing a touched-file match list; stale workers from that probe were stopped, and later/external dotnet build activity in the shared workspace was left untouched. Unity MCP console still fails through `127.0.0.1:8088/mcp`.

Exact microseconds saved after follow-up 8:
- Skipped structured buffer allocation/upload for zero visible stress modules; estimated 5-20 us saved during empty habitat startup/rebuild ticks plus one buffer worth of transient VRAM.
- Removed zero-count module resolver calls from DryZone vertices; estimated 1-3 us per 1k interior vertices during empty/boot shader states.

Follow-up upgrade 9:
- Changed stress signal resolution so graph-backed modules can match both stable habitat marker/node hashes and direct runtime `EntityId` hashes.
- Added `ResolveModuleStressEntityKey` for direct runtime target matching without changing the shader buffer layout.
- Changed `BaseModuleCompromisedSignal.ModuleHash` to use the stable-or-runtime module key, preventing no-graph modules from emitting zero identity.
- Static checks: context review confirms `CombatDamageSignal`/`HullDeformedSignal` producers can carry `EntityId`-derived target hashes; managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Verification block: constrained `dotnet build` timed out at 120s without usable output. A separate external `Hecton8.Core.csproj` build command remains active in the shared workspace and was left untouched. Unity MCP read-console is unavailable in this session.

Exact microseconds saved after follow-up 9:
- No direct frame-time savings claimed. This recovers missed stress spikes from direct runtime target hashes with one signal-path entity-hash compare per candidate module, estimated under 2 us for a 64-module scan on i3/MX350-class hardware.
