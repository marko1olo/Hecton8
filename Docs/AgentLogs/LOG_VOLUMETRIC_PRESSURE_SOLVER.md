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
- Rebuild policy: no `dotnet` rebuild and no Unity rebuild were run by explicit user instruction. Static checks are run separately in this loop.

Exact microseconds saved after follow-up 25:
- Calm visible-module dirty ticks skip one stress buffer ensure/upload.
- Estimated 6-18 us saved on i3/MX350 active-order, tier-change, or calm rebuild frames; 0 B/frame and no visual regression once stress rises above epsilon.
