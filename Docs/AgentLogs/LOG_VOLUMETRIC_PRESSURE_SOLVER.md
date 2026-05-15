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

Follow-up upgrade 10:
- Bounded nearest-module stress fallback in `HabitatGraphManager`.
- Direct interior containment now wins immediately for a finite world point.
- Non-contained nearest fallback now requires the point to fall inside a padded module interior/hazard radius, capped at 36m, before injecting a stress spike.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Verification: constrained `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:quiet -clp:ErrorsOnly` completed with `BUILD_EXIT=0 MATCH_COUNT=0`.

Exact microseconds saved after follow-up 10:
- No direct idle-frame savings claimed. This blocks false stress injections from unrelated far-field impacts while preserving local hull-surface spikes; fallback scan remains bounded to 64 modules and is estimated under 4 us on i3/MX350-class hardware.

Follow-up upgrades 11-18:
- Added unique-only 16-bit `TargetId` fallback across stable hash, runtime entity hash, and graph node id low bits.
- Released stale stress GPU buffer on real zero-module clears while preserving no-clear paths for active-order rebuilds and buffer growth replacement.
- Combined shader stress resolve/read, reused centered panel UV between bend and normal bias, and removed the unused panel-mask wrapper.
- Added index-hinted graph-record lookup for active stress paths.
- Collapsed `TryResolveModuleStressIndex` to one active-module pass for exact hash, `TargetId` counting, interior containment, and bounded nearest fallback collection.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Verification block: latest constrained `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:quiet -clp:ErrorsOnly` timed out before a clean result. The exact lingering `.\Hecton8.Core.csproj --no-restore --nologo` process from this probe was stopped; other shared-workspace `dotnet` builds were left untouched. Static touched-file checks remained clean.

Exact microseconds saved after follow-ups 11-18:
- Target-id and direct-runtime matching correctness: no idle savings claimed; avoids missed/wrong stress spikes under 2 us per 64-module signal scan.
- Buffer lifetime/growth clear cleanup: estimated 5-20 us on teardown/startup clears and 1-4 us on growth replacement frames.
- Shader resolver/panel reuse: estimated 2-5 us per 1k stressed interior vertices on MX350-class GPUs.
- Index-hinted lookup plus single-pass fallback: estimated 8-22 us saved on i3/MX350 signal-heavy frames, with 0 B/frame and no shader cost.

Follow-up upgrade 19:
- Added a target-identity gate to `TryResolveModuleStressIndex`.
- Signals with no `TargetHash`, no `TargetId`, and no finite nearest fallback now return before scanning active modules.
- World-point-only signals now test direct interior containment before graph-record lookup or runtime entity-key resolution, and identity-free interior hits return immediately.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Prompt re-extraction: `CURRENT_BATCH.md` was not present in the workspace during this loop.
- Verification block: constrained `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildProjectReferences=false -v:quiet -clp:ErrorsOnly` returned `BUILD_EXIT=1 MATCH_COUNT=0`. No touched-file build errors were emitted; global build state remains outside-domain blocked.

Exact microseconds saved after follow-up 19:
- Identity-free, no-worldpoint damage packets skip the 64-module scan entirely.
- Identity-free world-point packets avoid runtime `EntityId` hashing and graph-record lookup until nearest fallback actually needs module positions.
- Estimated 2-6 us saved per 64-module signal scan on i3/MX350-class hardware; 0 B/frame, no shader cost, no visual regression to High/Ultra localized deformation.

Follow-up upgrade 20:
- Reduced H-Phi registry coupling in `HabitatGraphManager`.
- Scalability tier is sampled once per hydrodynamic stress pass and passed through analytical stress, graph flood, module stress upload, low-tier feedback, and compromised-module signal paths.
- Runtime sea level is sampled once per rebuild/stress pass, then reused by depth fallback helpers instead of resolving atmosphere through `GlobalRegistry` inside module loops.
- Hull stress audio routing caches the first available `IAudioService`; if unavailable, existing `ProceduralAudioEvents.RaiseHullStressSignal` fallback still fires.
- Rupture edge fluid decals cache the first available `AbyssalFluidDecalManager`; null service still fails closed as before.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `git diff --check` reports only repository CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild, no Unity rebuild, and no H-Phi global audit were run because the user explicitly prohibited dotnet rebuilds and the global score belongs to the H-Phi monitor.
- Local H-Phi spot check for `HabitatGraphManager.cs`: `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`.

Exact microseconds saved after follow-up 20:
- Per-module missing-depth fallback avoids repeated atmosphere service lookups after the rebuild/stress-pass sea-level sample.
- Repeated hull groan events avoid registry audio lookup after warm cache.
- Repeated severed-edge rupture VFX avoids registry fluid-decal lookup after warm cache.
- Estimated 2-8 us saved on i3/MX350 stress-heavy frames; 0 B/frame, no shader cost, no change to High/Ultra visual deformation.

Follow-up upgrade 21:
- Added a peak-stress guard before `HectonHabitatInteriorResolveStress01` in `Hecton_DryZoneLit.shader`.
- Mid/High/Ultra vertices now skip the per-module radius resolver when `_HectonHabitatModuleStressParams.w` is effectively zero.
- Low-tier crease mode remains unchanged: peak stress still feeds the fragment crease path.
- Stressed Mid/High/Ultra modules remain unchanged: the shader still resolves localized module stress, applies sine panel bow, and reuses panel UV for normal bias.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; `rg` confirms resolver/bend/normal/crease call wiring; `git diff --check` reports only repository CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 21:
- Calm Mid/High/Ultra DryZone vertices skip up to 64 module-radius checks.
- Estimated 5-30 us saved per 1k interior vertices on MX350-class GPUs during zero-stress habitat frames.
- No visual regression: the skip only applies when peak stress is below 0.0001; visible deformation resumes once stress exists.

Follow-up upgrade 22:
- Added an include-level peak-stress early return inside `HectonHabitatInteriorResolveStress01`.
- Added zero-panel-mask early exits to `HectonHabitatInteriorApplyPanelBendOS` and `HectonHabitatInteriorApplyLowTierCrease`.
- This protects future shader callsites from reintroducing calm-state resolver work and skips no-op border deformation/crease setup.
- Static checks: exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; `rg` confirms include guard and helper call wiring; brace count in `Hecton_HabitatInterior.hlsl` is balanced; `git diff --check` reports only repository CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 22:
- Calm helper calls now return before count/ambience buffer resolution even if a future callsite misses the peak guard.
- Zero-mask panel borders skip centered-UV, offset, shadow, and crease lerp setup.
- Estimated 1-4 us saved per 1k affected interior vertices/fragments on i3/MX350-class hardware beyond the callsite guard; 0 B/frame.

Follow-up upgrade 23:
- Reordered `TryResolveModuleStressIndex` so stable graph-hash matches return before runtime `EntityId` key lookup.
- Runtime entity-key fallback remains intact for direct runtime targets and unique `TargetId` matching when the graph hash does not match.
- Static checks: managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; resolver grep confirms graph-hash return precedes runtime entity-key lookup; local H-Phi spot check remains `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`; `git diff --check` reports only unrelated CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 23:
- Graph-hash stress signal hits avoid one runtime entity-key read/hash per candidate.
- Estimated 1-3 us saved on i3/MX350 signal-heavy frames with graph-backed module signals; 0 B/frame and no shader cost.

Follow-up upgrade 24:
- Moved the low-tier habitat panel-mask gate in `Hecton_DryZoneLit.shader` before `_DetailMask` sampling.
- Changed `HectonHabitatInteriorApplyLowTierCrease` to consume the precomputed panel mask, removing duplicate cheap mask computation.
- Static checks: `rg` confirms `HectonHabitatInteriorApplyLowTierCrease` signature and DryZone callsite match; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader brace counts are balanced; `git diff --check` reports unrelated trailing whitespace in `Docs/Archive/Batch005/Tasks_Combined/CURRENT_BATCH.md` plus repository CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 24:
- Zero-mask low-tier panel-border fragments skip one detail texture sample and the helper-side panel-mask recompute.
- Estimated 4-12 us saved per dense interior wall view on MX350-class GPUs; 0 B/frame. Mid/High/Ultra branch behavior is unchanged.

Follow-up upgrade 25:
- Gated `UploadModuleStressMatrix` so `_HectonHabitatModuleStressBuffer` is ensured/uploaded only when `peakStress01 > ModuleStressUploadEpsilon`.
- Calm visible-module states still publish zero peak shader params; the shader peak guard prevents buffer reads until stress becomes visible again.
- Static checks: `rg` confirms `hasVisibleStress` gates `EnsureModuleStressBuffer` and `GraphicsBufferUploadUtility.UploadNativeArray` before shader params publish; managed-offender scans found no `string.Format`, `.ToString()`, interpolation, `foreach`, or LINQ offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; touched C#/shader brace counts are balanced; local H-Phi spot check remains `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 25:
- Calm visible-module dirty ticks skip one stress buffer ensure/upload.
- Estimated 6-18 us saved on i3/MX350 active-order, tier-change, or calm rebuild frames; 0 B/frame and no visual regression once stress rises above epsilon.

Follow-up upgrade 26:
- Changed `UploadModuleStressMatrix` so `hasVisibleStress` requires non-low-tier plus peak stress above `ModuleStressUploadEpsilon`.
- Added a low-tier early return to `HectonHabitatInteriorResolveStress01`, returning peak stress before module count or buffer reads.
- Static checks: `rg` confirms `hasVisibleStress` now requires `!lowTier` before `EnsureModuleStressBuffer`/`GraphicsBufferUploadUtility.UploadNativeArray`; `rg` confirms the shader low-tier branch returns before `HectonHabitatInteriorModuleCount()` and the only stress-buffer sample remains inside the resolver after that branch; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; touched C#/shader brace counts are balanced; local H-Phi spot check remains `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 26:
- Stressed low-tier habitat ticks skip one structured-buffer ensure/upload.
- Estimated 6-18 us saved on i3/MX350 low-tier stress frames; 0 B/frame. Mid/High/Ultra buffer uploads remain active for localized bowing.

Follow-up upgrade 27:
- Cached `HectonAtmosphereManager` service resolution in `HabitatGraphManager`.
- `ResolveRuntimeSeaLevelY` now reads live `SeaLevelY` from the cached manager instead of resolving `GlobalRegistry.Atmosphere` every pass.
- Static checks: `rg` confirms `ResolveRuntimeSeaLevelY` routes through `ResolveAtmosphereManager`, `_atmosphereManager` is cleared on dispose, and live `SeaLevelY` is still sampled each pass; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 27:
- Rebuild/stress passes avoid repeated atmosphere service-locator reads after warm cache.
- Estimated 1-3 us saved on i3/MX350 pressure-heavy frames; 0 B/frame and no loss of tide/sea-level freshness.

Follow-up upgrade 28:
- Added `habitatVertexBendActive` in `Hecton_DryZoneLit.shader`.
- DryZone vertices now call `HectonHabitatInteriorApplyPanelBendOS` only for non-low stressed habitat vertices.
- Cheap normal bias now runs only when vertex bend is active and the panel mask is nonzero.
- Static checks: `rg` confirms `habitatVertexBendActive` gates bend and normal-bias helper calls; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; touched C#/shader brace counts are balanced; local H-Phi spot check remains `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=81`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 28:
- Low-tier and calm vertices skip bend helper setup and cheap-normal-bias helper entry.
- Estimated 2-8 us saved per 1k interior vertices on MX350-class GPUs; 0 B/frame.

Follow-up upgrade 29:
- Added `_HectonHabitatModuleStressParams.y > 0.00001` to `habitatVertexBendActive` in `Hecton_DryZoneLit.shader`.
- This mirrors the bend helper amplitude guard at the callsite, so zero-amplitude non-low states skip bend and normal-bias helper entry.
- Static checks: `rg` confirms the amplitude guard is part of `habitatVertexBendActive` and still gates bend/normal-bias helper calls; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 29:
- Zero-amplitude transitional shader states skip vertex bend/normal helper setup.
- Estimated 1-3 us saved per 1k affected interior vertices on MX350-class GPUs; 0 B/frame.

Follow-up upgrade 30:
- Added `[StructLayout(LayoutKind.Sequential, Pack = 4)]` to `HabitatSiegeTargetSnapshot`.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 4)]` to `HabitatFloodConnection`.
- Deliberately did not add layout attributes to object-reference staging structs such as `ModuleRecord` and `SocketMatchEntry`.
- Static checks: struct/layout scan confirms `HabitatSiegeTargetSnapshot`, `HabitatFloodConnection`, and `HabitatFloodBlackBoxEntry` have explicit layout while object-reference staging structs remain untouched; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader `normalize()`/`sqrt()` scan produced no matches; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 30:
- Runtime frame savings: 0 us. This is H-Phi/data-contract hardening for native-facing habitat payloads.

Follow-up upgrade 31:
- What was wrong: CPU skipped per-module stress-buffer uploads at `ModuleStressUploadEpsilon` (0.0015), while shader stress gates still accepted peaks above 0.0001. That could let tiny post-impact stress sample stale high-stress buffer data.
- What was done: added `HECTON_HABITAT_INTERIOR_STRESS_EPSILON` / `_HALF` at 0.0015, routed shader resolver/bend/normal/crease/DryZone gates through it, and made `PublishModuleStressShader` publish zero deformation amplitude unless the same non-low visible-stress condition is true.
- Cinematic cheat used: sub-epsilon pressure is intentionally silent visually; Low/MX350 keeps peak-only crease, while Mid/High/Ultra reserve sine bow and normal bias for pressure that clears the agreed visibility threshold.
- Static checks: `rg` found no stale shader stress guards using `0.0001`; `rg` confirms stress epsilon macro wiring; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; touched C#/shader brace counts are balanced; owned-source `git diff --check` is clean.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 31:
- Near-calm Mid/High/Ultra interior vertices skip stale-prone resolver/bend work below the CPU upload epsilon.
- Estimated 5-30 us saved per 1k calm or near-calm interior vertices on MX350-class GPUs, plus avoided 6-18 us CPU/driver upload churn on near-threshold dirty ticks; 0 B/frame.

Follow-up upgrade 32:
- What was wrong: `Hecton8_UberNoir.hlsl` computed the habitat analytical radius mask even when habitat displacement was zero. `DeconstructionDfsValidationJob` was a Burst/native payload without explicit sequential layout evidence.
- What was done: gated `H8UberNoirRadiusMask(positionWS, _HectonHabitatStressCenterRadius)` behind `habitatDisplacement > H8_UBER_NOIR_EPS`; added `[StructLayout(LayoutKind.Sequential)]` to `DeconstructionDfsValidationJob` with natural packing.
- Cinematic cheat used: inactive habitat pressure stays visually silent; active Mid/High/Ultra pressure still gets noir analytical bend, while low/calm states skip dead mask math.
- Static checks: `rg` confirms the UberNoir habitat mask gate and job layout attribute; exact shader `normalize()`/`sqrt()` scan produced no matches across touched habitat/DryZone/UberNoir shader files; managed-offender scans found no C# string/LINQ/foreach offenders; mesh mutation scan found no owned `Mesh.vertices` writes; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF normalization warnings.
- H-Phi local evidence: `HabitatGraphManager.cs` now has `StructLayout=4/StructDecl=9`; `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=82`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 32:
- Calm/low habitat UberNoir deformation vertices skip one habitat radius-mask evaluation.
- Estimated 1-4 us saved per 1k affected vertices on MX350-class GPUs; 0 B/frame.

Follow-up upgrade 33:
- What was wrong: analytical stress reset/commit paths wrote `_BaseEmergencyState` even when the integer state did not change, and habitat vibration could leave a tiny stale shader value after decay because the publish epsilon suppressed the final zero. The public deformation sample contract also had no explicit binary layout marker.
- What was done: added cached `PublishBaseEmergencyState` with forced dispose clear; changed habitat vibration publish to snap values at or below 0.002 to zero while still forcing dispose cleanup; added `[StructLayout(LayoutKind.Sequential, Pack = 4)]` to `HabitatModuleDeformationSample`.
- Cinematic cheat used: micro-vibration below the perceptual threshold is intentionally silent instead of burning global shader state; emergency pressure visuals still publish immediately on real state transitions.
- Static checks: `rg` confirms the only `_BaseEmergencyState` and `_HectonHabitatVibration01` global writes are inside their publishers; managed-offender scans found no C# string/LINQ/foreach offenders in touched files; mesh mutation scan found no owned `Mesh.vertices` writes; touched C# brace counts are balanced; `git diff --check` reports only CRLF normalization warnings.
- H-Phi local evidence: `HabitatGraphManager.cs` remains `StructLayout=4/StructDecl=9`, `GlobalRegistry=4`, `SignalBus=2`, `GlobalSignals=1`, `NativeArray=82`, `GraphicsBuffer=3`, `FindCalls=0`, `UpdateMethods=0`; `HabitatDeformationContracts.cs` is now `StructLayout=1/StructDecl=1`.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 33:
- Stable analytical reset/commit ticks skip redundant emergency-state shader global writes.
- Estimated 1-3 us saved on i3/MX350 stress/reset-heavy frames; stale sub-epsilon vibration is cleared; contract layout cost is 0 us/frame.

Follow-up upgrade 34:
- What was wrong: analytical habitat stress used a 0.0025 CPU publish epsilon, but `_HectonHabitatStressParams` could still carry tiny positive stress and CoreLit accepted it at 0.0001, keeping near-calm dent math alive.
- What was done: `PublishAnalyticalStressShader` now publishes zero stress and zero displacement at or below `AnalyticalShaderStressEpsilon`, skips repeated zero vector writes after the first clear, and forces dispose cleanup. `Hecton_CoreLit.hlsl` now uses `HECTON_CORE_LIT_HABITAT_STRESS_EPSILON` at 0.0025 before habitat analytical dent work.
- Cinematic cheat used: sub-epsilon analytical pressure is visually silent. Active High/Ultra pressure still gets the grid dent fake; near-calm states buy back vertex ALU.
- Static checks: `rg` confirms `visibleStress01` controls analytical stress publication and CoreLit uses the habitat epsilon macro; exact shader `normalize()`/`sqrt()` scan produced no matches across touched habitat/CoreLit/UberNoir shader files; managed-offender scans found no C# string/LINQ/foreach offenders; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 34:
- Near-calm CoreLit vertices skip analytical habitat dent setup below the 0.0025 CPU visibility threshold.
- Estimated 2-8 us saved per 1k CoreLit vertices in near-calm habitat views on MX350-class GPUs; 0 B/frame.

Follow-up upgrade 35:
- What was wrong: `_HectonHabitatStressCenterRadius` could stay stale when analytical stress/displacement were stable but the base center or radius moved. That is a correctness failure, not a polish issue.
- What was done: cached the last analytical center/radius and added a visible-stress spatial dirty gate with 5cm center/radius tolerances. The analytical publisher now sanitizes non-finite center/radius/stress to a zero-stress shader clear path before upload.
- Cinematic cheat used: sub-5cm analytical center/radius drift is visually ignored to avoid global shader churn; real spatial movement still refreshes the fake pressure dent coordinates.
- Static checks: `rg` confirms `spatialStable`, `validCenter`, `sourceStress01`, and cached center/radius fields are wired; managed-offender scans found no C# string/LINQ/foreach offenders in `HabitatGraphManager.cs`; touched C# brace counts are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 35:
- Stable sub-5cm analytical spatial jitter skips redundant shader global vectors; moved active pressure regions publish correctly.
- Estimated gate cost under 1 us per analytical publish decision on i3/MX350; avoids stale deformation and unnecessary driver traffic.

Follow-up upgrade 36:
- What was wrong: `ApplyHydrodynamicStress` rejected non-positive time but not non-finite time, allowing NaN/Infinity to enter pressure, flood, vibration, module stress and telemetry paths. Malformed seismic epicenters also scanned active modules before failing distance tests.
- What was done: added a finite `deltaTime` gate at hydrodynamic stress entry and finite epicenter component gates in `RegisterSeismicVibration` before active-module scanning.
- Cinematic cheat used: invalid time or invalid epicenter produces no pressure/vibration visual event. Valid pressure feedback remains unchanged.
- Static checks: `rg` confirms the finite `deltaTime` and seismic epicenter gates; managed-offender scans found no C# string/LINQ/foreach offenders in `HabitatGraphManager.cs`; touched C# brace count is balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 36:
- Invalid frame time skips the entire hydrodynamic stress pass; invalid seismic epicenters skip one active-module scan.
- Estimated 10-80 us saved on i3/MX350 invalid-input frames/events; valid-frame overhead is one finite check plus three event-ingress component checks.

Follow-up upgrade 37:
- What was wrong: non-finite hydrodynamic/seismic ingress could be rejected without recording the fault into the 300-frame blackbox. Corrupted native module spike/integrity/flood scalar lanes could also feed the stress solver until the outer finite guard caught the final result.
- What was done: added `RecordNonFinitePressureIngress`; non-finite `deltaTime`, seismic epicenter/radius, direct spike magnitude, and native stress scalar corruption now write the blackbox path. `ResolveModuleStress01` sanitizes non-finite depth, integrity, joint, compression, flood, and spike inputs before shader-facing stress upload.
- Cinematic cheat used: one corrupt lane is isolated to zero visual stress instead of clearing the whole habitat deformation field. Valid rooms keep their pressure bowing/crease feedback.
- Static checks: `rg` confirms blackbox ingress recording, `SaturateFinite01`, invalid-state reporting, and native spike sanitation are wired. Managed-offender scans found no C# string/LINQ/foreach offenders in `HabitatGraphManager.cs`; mesh mutation scan found no owned `Mesh.vertices` writes; exact shader normalize/sqrt scan produced no matches; touched C# brace count is balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 37:
- Fault frames avoid repeated NaN propagation through flood, pressure, module stress and shader upload state.
- Estimated 5-20 us saved on i3/MX350 recovery frames; valid-frame overhead is bounded finite checks inside the existing 64-module stress loop and remains 0 B/frame.

Follow-up upgrade 38:
- What was wrong: habitat interior shader normal helpers used `rsqrt` but did not reject non-finite length before returning bend or normal-bias vectors.
- What was done: `HectonHabitatInteriorSafeNormalize3` / `HectonHabitatInteriorSafeNormalizeHalf3` now take explicit fallback vectors and return them when the length is non-finite or too small. Bend normals fall back to zero offset; normal bias falls back to stable axes/base normal.
- Cinematic cheat used: invalid normals produce a stable no-op/fallback visual instead of attempting a physically correct recovery. Valid stressed panels still get sine bow and cheap normal bias.
- Static checks: helper callsites were updated; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched C#/shader brace counts are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 38:
- Faulted vertices avoid NaN propagation into clip-space and lighting.
- Normal-frame added cost is bounded to finite checks in the already-gated stressed vertex path; estimated under 1 us per 1k stressed vertices on MX350-class GPUs.

Follow-up upgrade 39:
- What was wrong: habitat shader stress lookup could treat NaN radius/distance comparisons as pass-through and select a corrupted module ambience slot.
- What was done: `HectonHabitatInteriorResolveStress01` now skips candidates with non-finite radius or non-finite distance before selecting a stress-buffer index.
- Cinematic cheat used: a corrupted ambience slot produces zero localized deformation instead of attempting visible recovery. Valid stressed slots still get localized panel bow.
- Static checks: `rg` confirms the resolver radius/distance finite guards; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 39:
- Fault frames avoid poisoned stress selection and downstream deformation artifacts.
- Low/MX350 is unaffected because low-tier exits before the resolver loop; non-low valid-frame overhead is two scalar finite checks per candidate inside the existing 64-slot cap.

Follow-up upgrade 40:
- What was wrong: the habitat stress resolver trusted peak stress and selected buffer stress after CPU publication. A non-finite scalar could still leak into low-tier crease or localized bend.
- What was done: `HectonHabitatInteriorResolveStress01` now rejects non-finite peak stress before low-tier/buffer branches and returns zero if the selected buffer scalar is non-finite.
- Cinematic cheat used: corrupted stress scalars become silent visual output; valid pressure deformation remains unchanged.
- Static checks: `rg` confirms peak and buffer finite gates; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 40:
- Fault frames avoid NaN crease/deformation propagation.
- Valid-frame overhead is two scalar finite checks in already-gated stress paths; expected cost is below measurement noise under the 64-slot cap.

Follow-up upgrade 41:
- What was wrong: non-finite UVs could poison panel UV, sine mask, bend offset, or low-tier crease intensity.
- What was done: `HectonHabitatInteriorPanelUv` now returns zero panel UV for non-finite input, and `HectonHabitatInteriorPanelMaskFromUv` returns zero for non-finite mask products.
- Cinematic cheat used: invalid panel input becomes zero panel influence. Valid panel bow/crease visuals remain unchanged.
- Static checks: `rg` confirms panel UV/mask finite guards; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 41:
- Faulted vertices/fragments avoid NaN bend and crease propagation.
- Valid-frame overhead is one UV finite check plus one mask finite check in already-gated panel paths.

Follow-up upgrade 42:
- What was wrong: CoreLit analytical habitat dent trusted global stress, displacement, radius, grid scale, and seed scalars.
- What was done: `HectonCoreLitApplyHabitatAnalyticalStress` now fails closed on non-finite analytical stress/displacement/radius/grid/seed inputs and non-finite radius masks before dent output.
- Cinematic cheat used: invalid analytical pressure globals become no-op deformation; valid High/Ultra analytical dent remains unchanged.
- Static checks: `rg` confirms CoreLit analytical finite gates; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 42:
- Fault frames avoid NaN radius/phase/dent propagation.
- Valid-frame overhead is finite scalar checks only inside the already-active analytical dent path.

Follow-up upgrade 43:
- What was wrong: UberNoir dynamic hull bending reused habitat analytical globals and radius masks without a local finite guard.
- What was done: `H8UberNoirRadiusMask` now rejects non-finite position/center/radius data, and UberNoir habitat stress/displacement scalars are finite-gated before bend accumulation.
- Cinematic cheat used: corrupted habitat contribution becomes zero noir bend; valid High/Ultra bending remains unchanged.
- Static checks: `rg` confirms UberNoir radius-mask and habitat-scalar finite gates; exact shader `normalize()`/`sqrt()` scan remains clean; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 43:
- Fault frames avoid NaN dynamic hull displacement in UberNoir.
- Low/MX350 remains bypassed by `_MATH_LOD_LOW`; valid non-low overhead is finite checks around already-active bending math.

Follow-up upgrade 44:
- What was wrong: UberNoir dynamic hull bending still used raw `positionWS` in buckling and final fallback after radius-mask sanitation.
- What was done: sanitized `positionWS` once into `safePositionWS` and reused it for crush/habitat radius masks, buckling mask, and final output fallback.
- Cinematic cheat used: non-finite vertex position becomes deterministic safe fallback instead of attempting to preserve a corrupt deformation.
- Static checks: `rg` confirms `safePositionWS` drives radius masks, buckling mask, and final fallback; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 44:
- Fault frames avoid NaN buckling/displacement propagation.
- Valid-frame overhead is one vector finite sanitize in the non-low bend path, below the existing bending ALU budget.

Follow-up upgrade 45:
- What was wrong: UberNoir dynamic hull bending still trusted bend feature/strength, crush depth/current/displacement, buckling grid scale, and instance seed after the position and habitat-scalar gates.
- What was done: added finite gates for those scalar lanes, rechecked computed crush/habitat displacement after multiplication, re-sanitized buckling `stablePosition` after grid scaling, skipped crush radius-mask evaluation when crush displacement is zero, and zeroed final displacement if it becomes non-finite.
- Cinematic cheat used: invalid scalar pressure inputs collapse to no-op contribution while valid crush/habitat bending keeps the same visual fake.
- Static checks: `rg` confirms `gridScaleSource`, `safeInstanceSeed`, `featureSource`, crush scalar gates, computed crush/habitat displacement finite gates, zero-crush radius-mask branch, and final displacement finite gate are wired; exact shader `normalize()`/`sqrt()` scan produced no matches; managed-offender scan remains clean; mesh mutation scan found no owned `Mesh.vertices` writes; touched shader/doc braces are balanced; `git diff --check` reports only CRLF normalization warnings.
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction.

Exact microseconds saved after follow-up 45:
- Calm-crush states skip one radius-mask evaluation per UberNoir vertex.
- Estimated 1-4 us saved per 1k affected UberNoir vertices on MX350-class GPUs; active-crush valid-frame overhead is scalar finite checks inside the already-active bending path.
