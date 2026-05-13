# Rationale: TECH_RESEARCHER_EVOLVER

Runtime status: PENDING VERIFICATION

## Decision 0: Scope Control

Problem: The batch contains 19 documentation/audit tasks across mandates, source scans, and build review.
Solution: Treat .agents-skills and stable Docs files as the owned domain. Source code edits are out of scope unless a mandate task requires a compile-safe documentation support file.
Rejected Alternatives: Runtime refactors during a mandate audit were rejected because this role is Tech Researcher, not Integrator or subsystem owner.
Scalability potential: Low keeps mandates strict for MX350/i3; Middle adds measured tier gates; High and Ultra spend saved CPU/GPU budget on richer visuals through explicit quality tiers.
Hardware Impact: Documentation changes do not directly save frame time. Expected impact is enforcement: preventing coroutine/string/singleton regressions that would otherwise cause GC spikes on i3/MX350.

## Decision 1: Evidence Class

Problem: Unity 6000.x features can drift by package version and official docs.
Solution: Use local package evidence first, then official Unity documentation as external evidence for RenderGraph, Awaitable, GPU Resident Drawer, GPU occlusion, and Addressables 2.7.6.
Rejected Alternatives: Blog or forum summaries rejected; only local package files and official Unity docs are acceptable for mandate evolution.
Scalability potential: Low avoids false feature mandates; Middle/High/Ultra can adopt engine features only when package and capture evidence exist.
Hardware Impact: Avoids expensive or unavailable feature paths on MX350, especially GPU occlusion and VRS-like assumptions.

## Decision 2: Purge Verification Boundaries

Problem: The prompt requests purge verification, but runtime source ownership belongs to multiple agents.
Solution: Use static scans to verify `IEnumerator`, singleton facades, dead interfaces, and asmdef coupling; log deletion candidates instead of editing runtime ownership surfaces.
Rejected Alternatives: Directly deleting interfaces or singleton facades was rejected because public API mutation during a batch requires owner confirmation or legacy wrappers.
Scalability potential: Low/Middle/High/Ultra all benefit from removing coroutine and singleton debt, but only if owning agents remove call sites without breaking init order.
Hardware Impact: Static audit found no runtime coroutine allocation sites in core systems. Immediate saved time is 0 us; avoided future coroutine iterator allocations are approximately 100 B per call and hitch-risk rather than deterministic microseconds.

## Decision 3: ASMDEF Verdict

Problem: `Hecton8.Core.asmdef` must be shattered and not depend on UI.
Solution: Mark direct `Hecton8.UI` coupling as absent, but mark the core asmdef as not truly shattered due to broad domain/package references.
Rejected Alternatives: Treating absence of `Hecton8.UI` as full pass was rejected because `Unity.TextMeshPro`, `UnityEngine.UI`, URP, Addressables, Logistics, Cartography, Input, and third-party render packages still make Core too broad.
Scalability potential: Cleaner asmdefs reduce compile blast radius and let Low/MX350 builds strip UI/render paths from non-UI core tests.
Hardware Impact: Runtime frame gain is 0 us. Editor/build iteration gain is expected but unmeasured.

## Decision 4: Compile Failure Handling, Loop 1

Problem: `dotnet build Hecton8.Core.csproj --no-restore` failed after the first audit loop.
Solution: Classify as global dependency failure because this agent edited only documentation and logs. Record the failure for Integrator instead of patching cross-domain runtime code.
Rejected Alternatives: Fixing Cartography, UI, Biolum, Flora, Submarine, and signal contract errors from a Tech Researcher pass was rejected as cross-domain mutation without ownership.
Scalability potential: None until Integrator repairs compile graph. Mandate edits remain source-neutral.
Hardware Impact: 0 us saved. Build remains PENDING VERIFICATION due unrelated compile errors.

## Decision 5: Unity 6000 Mandate Upgrades

Problem: Existing mandates still allowed or ambiguously tolerated legacy URP CommandBuffer paths, Task-based Unity orchestration, manual flora culling, Addressables ref-count-only residency claims, and string-producing UI text updates.
Solution: Upgrade the mandates with hard RenderGraph, Awaitable, GPU Resident Drawer, Addressables AssetLoadMode, and TMP `SetCharArray` gates.
Rejected Alternatives: Soft recommendations were rejected because agents will route around optional guidance under schedule pressure.
Scalability potential: Low/MX350 gets strict avoidance of hidden render and GC costs; Middle/High/Ultra can spend saved CPU/GPU budget on richer HLOD residency, occlusion-heavy scenes, and higher visual density once profiler proof exists.
Hardware Impact: Immediate runtime gain is 0 us because no runtime code changed. Expected prevention value: avoids legacy render blit chains, task storms, and HUD string allocations that typically show as frame spikes on i3/MX350.

## Decision 6: Compile Contention Handling, Loop 2

Problem: Second compile attempt timed out after 124s and active `dotnet build .\Assembly-CSharp.csproj` plus MSBuild node processes were present.
Solution: Stop launching more compile attempts in parallel with the active build owner and record timeout as a verification blocker.
Rejected Alternatives: Killing all `dotnet` processes was rejected because other agents may own the active compile; launching another full build violates the repository quality gate.
Scalability potential: None; this is workflow hygiene.
Hardware Impact: Avoids saturating the i5-1135G7-class host during parallel agent work.

## Decision 7: Native/Burst/Data Diagnostics

Problem: Native allocations and Burst intrinsics were governed by broad principles but not precise registration or package-version law.
Solution: Add Sentinel registration requirements for raw/native allocation sites and bind SIMD guidance to local Burst 1.8.28 evidence.
Rejected Alternatives: Allowing raw `UnsafeUtility.Malloc` with comments only was rejected; allowing v512 or Burst 2.0 language was rejected because local package evidence does not support it.
Scalability potential: Low uses auto-vectorized/v128-safe paths; Middle/High/Ultra may use guarded v256 only with benchmark proof and visual upside.
Hardware Impact: Immediate runtime gain is 0 us. Prevents native leak drift and false SIMD assumptions on i3/MX350.

## Decision 8: Cross-Domain Authority Boundary

Problem: Logistics and thermodynamics both mention heat/coolant/flow, risking dual ownership.
Solution: Logistics owns graph transport and coolant availability; thermodynamics owns heat state and diffusion. Flow consumes read-only heat summaries for VFX/current fakes.
Rejected Alternatives: Direct `LogisticsNode` reads inside flow or thermodynamics jobs were rejected as hidden coupling and polling risk.
Scalability potential: Low keeps only dirty summaries; High/Ultra can increase visual heat/updraft fidelity without mutating resource authority.
Hardware Impact: 0 us immediate; avoids future per-frame graph polling and cache-hostile cross-domain reads.

## Decision 9: Quality Gates and AUP Snap-Fence

Problem: MX350 budgets and origin-shift drift evidence were too spread across mandates.
Solution: Formalize hard budget rows in `Docs/QUALITY_GATES.md` and add 300-frame snap-fence telemetry to the AUP mandate.
Rejected Alternatives: Single-frame shift validation and vague "profile it" language were rejected.
Scalability potential: Low has coarse drift probes and strict budgets; Ultra can use full 300-frame debug ring and spend headroom on visual continuity.
Hardware Impact: 0 us immediate. Expected risk reduction: earlier detection of physics/render/nav drift and budget violations before they reach runtime QA.

## Decision 10: Engineering Data Bulk Enforcement

Problem: Static audit found 64 of 75 mandate files missing an `Engineering Data` section.
Solution: Append a concise mandatory evidence table to every missing mandate so future agents have a uniform gate for hot path, MX350, ownership, and failure-mode proof.
Rejected Alternatives: Leaving the defect as a report-only finding was rejected because the task explicitly required mandate polish.
Scalability potential: Low/MX350 gates are now visible in every mandate; High/Ultra paths must still state visual benefit and evidence.
Hardware Impact: 0 us immediate. Prevents unverifiable claims from entering mandate updates.

## Decision 11: Final Verification Blocker

Problem: Final dry-run compile cannot be honestly reported green. Earlier `Hecton8.Core.csproj` build failed with 24 global errors; later compile lane is occupied by active dotnet/MSBuild processes.
Solution: Mark final verification as dependency-blocked while preserving the exact blocker class and not mutating cross-domain runtime code.
Rejected Alternatives: Killing active dotnet processes was rejected because the workspace is shared with other agents. Launching another build was rejected by `QUALITY_GATES.md` compile-owner discipline.
Scalability potential: None.
Hardware Impact: Avoids CPU saturation and false build status.

## OMEGA POLISH CHANGES

Problem: The final anti-bloat pass required proof that mandate edits did not add runtime bloat, vague advice, or cross-domain source mutations.
Solution: Scoped diff audit was run against `.agents-skills`, `Docs/QUALITY_GATES.md`, atlas files, and TECH_RESEARCHER logs. The scan hits were mandate-ban text only: `GlobalRegistry.Get<T>()`, `string.Format`, and `.ToString()` appear as forbidden examples, not as implementation code. No runtime C# files were edited by this agent.
Rejected Alternatives: Re-running another full compile while external build lanes were active was rejected. Patching Cartography, UI, Biolum, Flora, Submarine, or signal contracts from this role was rejected as cross-domain runtime mutation.
Scalability potential: Low keeps mandated fallback paths cheap: no coroutines, no per-frame strings, dirty EventBus signals, declared RenderGraph resources, and requested-asset residency. Middle requires profiler proof before extra fidelity. High and Ultra may spend saved CPU/GPU time on GPU Resident Drawer density, richer HLOD, visual thermal/fog overkill, and longer telemetry rings where hardware allows.
Hardware Impact: Measured runtime gain is 0 us because this pass changed mandates/docs only. Preventive impact is enforcement: no runtime `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, or `Vector3.magnitude` were introduced by TECH_RESEARCHER_EVOLVER. Projected savings remain PENDING PROFILER after owning agents convert systems.

Final Git Diff:
- Scoped tracked diff before final log/status updates: 77 tracked files, 1025 insertions, 9 deletions.
- New/untracked TECH_RESEARCHER artifacts: `Docs/PROJECT_ATLAS.md`, root `PROJECT_ATLAS.md`, `Docs/Tasks/Status_TECH_RESEARCHER_EVOLVER.md`, `Docs/AgentLogs/Rationale_TECH_RESEARCHER_EVOLVER.md`, `Docs/AgentLogs/RECON_TECH_RESEARCHER_EVOLVER.md`.
- Mandate coverage proof: 75 mandate `.txt` files scanned; `MISSING_ENGINEERING_DATA=0`.
- Verification state: `dotnet build Hecton8.Core.csproj --no-restore` is not green. First attempt failed with 24 global C# errors outside this agent's documentation edits; later verification was blocked by active external dotnet/MSBuild build lanes.

## Decision 12: Continuation Static Correction

Problem: A patient recheck found that the generated atlas under-reported current first-party asmdefs and `Docs/QUALITY_GATES.md` still had wording that could be misread as current compile success.
Solution: Re-scan `Assets/_Project/**/*.asmdef`, update both atlas files to 24 first-party rows after concurrent workspace changes, mark Lighting/QA Core dependency as debt, and convert the 2026-05-11 build line to historical evidence only. Clarify MX350 VRAM guard/hard ceiling and physics planning/absolute gates.
Rejected Alternatives: Running `dotnet build` was rejected because the user explicitly forbade it. Runtime fixes in Lighting, QA, Cartography, UI, or Core were rejected because TECH_RESEARCHER owns mandate/research authority files, not subsystem source.
Scalability potential: Low/MX350 now has explicit guard/hard VRAM semantics and physics load-shed wording. Middle/High/Ultra keep richer visuals only after profiler proof and graph dependency cleanup.
Hardware Impact: 0 us measured runtime gain. This prevents false acceptance criteria and stale graph decisions; actual frame-time savings require subsystem owners to implement the gates and provide profiler evidence.

## Decision 13: Voxel Mandate Source Alignment

Problem: Static review of the open voxel engine showed current source is Marching Cubes with float working-density buffers and sbyte quantized extraction, while voxel mandates still claimed half runtime density and Transvoxel primary mesh with Marching Cubes forbidden.
Solution: Update voxel mandates to make current source truth explicit: MC primary remains valid, float/sbyte density path remains current, Transvoxel/fp16 are future migration targets gated by owner, feature flag, fallback, seam proof, profiler proof, and save compatibility.
Rejected Alternatives: Editing `HectonVoxelEngine.cs` was rejected because TECH_RESEARCHER owns mandate evolution, not voxel runtime. Leaving stale mandates was rejected because future agents would be forced into architectural churn and potential compile breakage.
Scalability potential: Low/MX350 preserves the existing float-to-sbyte MC path and bake staging. Middle/High/Ultra can pursue Transvoxel or fp16 memory compression only after captured seam quality and profiler evidence prove it buys visual density without breaking the current fallback.
Hardware Impact: 0 us measured runtime gain. Preventive impact: avoids wasted runtime rewrites and protects the current Black Box/physics bake staging contract from stale documentation.

## Decision 14: Player/Pipe Static Recheck Boundary

Problem: The open workspace pointed at player movement and pipe logistics. Static source review found no coroutine purge regression in `HectonPlayerMovement.cs`, and pipe logistics had dispatcher/NativeArray/Black Box structure, but player hot-path helper chains still read static `GlobalRegistry.*` properties such as Settings, Fluid, OceanKinematics, Thermodynamics, SargassumDrag, and ResourceDistribution from `Tick`/`FixedTick` call paths.
Solution: Keep runtime source unchanged because TECH_RESEARCHER does not own player or pipe code in this batch. Tighten the GlobalRegistry mandate with `RULE-09C`: static registry convenience properties are treated like `GlobalRegistry.Get<T>()` in hot paths, dependencies must be cached or refreshed by dirty lifecycle signals, and rare direct lookup exceptions need cadence rationale.
Rejected Alternatives: Editing `HectonPlayerMovement.cs` service caching was rejected as cross-domain runtime mutation under the domain boundary. Running `dotnet build` was rejected because the user explicitly forbade it. Treating pipe dump `Debug.Log*` calls as hot-path violations was rejected because they are guarded crash/dump paths, not normal tick cadence.
Scalability potential: Low/MX350 benefits from fewer repeated registry reads in player/physics loops once owning agents apply the mandate. Middle/High/Ultra can spend the recovered player tick headroom on richer water response, VR comfort smoothing, and high-tier environmental visuals while retaining cached service snapshots.
Hardware Impact: 0 us measured runtime gain. Preventive estimate only: removing repeated service slot reads from `HectonPlayerMovement.Tick` and `FixedTick` helper chains is expected to save microseconds, not milliseconds; the main value is deterministic dependency ownership and less hidden cross-domain polling.

## Decision 15: Logistics Mandate Source Alignment

Problem: The logistics mandate still said `LogisticsTick` never runs on the main thread and forbade `MonoBehaviour per pipe/cable segment`, which contradicted current source evidence where `FluidPipeGraphRuntime`, `LogisticsPipeNode`, and `WaterPumpModule` are Unity adapters while transport truth sits in shared scheduler/job/native data.
Solution: Narrow the rule to the actual authoritative boundary: graph/pressure solve must stay Burst/job/native and off the main thread; main-thread cadence may only schedule/consume jobs and touch Unity-owned objects/signals. Endpoint MonoBehaviours are allowed as cold authoring/registration/pool lifecycle adapters, but per-segment simulation/update remains forbidden.
Rejected Alternatives: Editing pipe runtime was rejected because TECH_RESEARCHER owns mandate authority, not pipe implementation. Keeping the stale wording was rejected because future agents would perform pointless rewrites against already acceptable adapter surfaces. Running `dotnet build` was rejected because the user explicitly forbade it.
Scalability potential: Low/MX350 keeps the cheap shared graph solve and shader/dirty visual proxies. Middle/High/Ultra can increase pressure solve cadence and visual flow richness without moving resource truth into component loops.
Hardware Impact: 0 us measured runtime gain. Preventive impact is avoiding false refactor work and preserving job/native pipe pressure math; actual savings remain PENDING PROFILER.

## Decision 16: Player Service Snapshot Mandate

Problem: `HectonPlayerMovement.cs` has profiler markers and a 300-frame Black Box, but static evidence still shows `GlobalRegistry.*` reads in movement-related `Tick` / `FixedTick` helper chains. The vehicle kinematics mandate described `PlatformCache` rigor but did not define the matching service snapshot boundary for water, ocean, thermal, sargassum, resource-field, or VR settings dependencies.
Solution: Add a player/vehicle service snapshot policy to the kinematics/AUP mandate. Movement owners must consume cached summaries refreshed by lifecycle, service-rebound, dirty settings, or budgeted slow cadence; hot-path helpers inherit the ban. The Black Box must record provider validity/version/fallback fields so post-mortem data can prove which service snapshot drove movement.
Rejected Alternatives: Editing `HectonPlayerMovement.cs` was rejected as cross-domain runtime mutation under TECH_RESEARCHER scope. Relying only on `RULE-09C` was rejected because player kinematics needs domain-specific evidence fields and environment provider boundaries. Running `dotnet build` was rejected because the user explicitly forbade it.
Scalability potential: Low/MX350 uses cached summaries and cheap fallbacks. Middle/High/Ultra can increase water/ocean/thermal visual response fidelity without turning GlobalRegistry into a live movement data bus.
Hardware Impact: 0 us measured runtime gain. Preventive estimate: replacing repeated registry reads with cached snapshots should save microseconds in player tick/fixed chains; larger value is deterministic post-mortem evidence and less hidden cross-domain polling.

## Decision 17: Mandate Pseudocode Zero-GC Scrub

Problem: A self-audit of recently touched mandates found examples that still contradicted the purge posture: `for each` / `foreach` graph and penetration loops, `PlatformCache singleton` wording, and a high-G delta guard using `Length(cache.delta_position)`.
Solution: Convert examples to index/range iteration, replace the platform delta magnitude check with squared-distance comparison, and rename PlatformCache ownership from singleton to shared owner. These are mandate/source-guidance edits only.
Rejected Alternatives: Leaving the examples as "only pseudocode" was rejected because future agents copy mandate kernels into runtime. Runtime player/pipe edits were rejected because TECH_RESEARCHER owns mandate authority, not subsystem implementation. Running `dotnet build` was rejected because the user explicitly forbade it.
Scalability potential: Low/MX350 gets cheaper copied patterns by default: no iterator ambiguity, no unnecessary sqrt in spike guards, and no singleton wording in kinematic cache ownership. Middle/High/Ultra keep the same deterministic patterns while spending saved headroom on richer visuals.
Hardware Impact: 0 us measured runtime gain. Preventive estimate only: avoids future hot-path iterator and sqrt debt; concrete microseconds require owning agents to implement or preserve the corrected patterns.

## Decision 18: Black Box Export Mandate Alignment

Problem: The central post-mortem mandate allocated a 300-frame native telemetry ring but still told exporters to copy only 50 preceding states. Current source evidence for player, pipe, voxel, habitat, and drone systems uses 300-frame Black Boxes with `Docs/AgentLogs/Dump_*.bin` paths, so the central rule could cause future systems to discard the exact history required for crash analysis.
Solution: Update `DBG_Telemetry_Crash_Reporting_PostMortem.txt` so critical-system exports dump the full 300-entry ring oldest-to-newest, use owner-specific dump paths, avoid per-fault managed task allocation, use indexed NaN sweeps, and avoid magnitude/sqrt checks in telemetry paths.
Rejected Alternatives: Editing runtime dump code was rejected because current source already aligns better than the mandate and TECH_RESEARCHER owns the rule layer. Keeping a 50-frame export window was rejected because it violates the Black Box requirement. Running `dotnet build` was rejected because the user explicitly forbade it.
Scalability potential: Low/MX350 keeps telemetry memory bounded at roughly tens of KB per critical system and avoids crash-time allocation. Middle/High/Ultra can add richer owner-specific fields while preserving the 300-frame minimum and path convention.
Hardware Impact: 0 us measured runtime gain. Preventive impact is post-mortem fidelity and fewer crash-time allocations; any runtime savings from squared/indexed telemetry paths are microsecond-level and pending profiler evidence.

## Decision 19: Engineering Data Header Normalization

Problem: Follow-up static audit found 13 mandate evidence sections still labeled `Engineering Data:` instead of the standardized `## Engineering Data` heading. The evidence existed, but automation that searches headings would report false missing coverage. A first atlas check also used the wrong row pattern and falsely under-counted coverage.
Solution: Normalize the 13 labels to `## Engineering Data` across 11 mandate files and correct the atlas scanner to match the actual table format. Preserve duplicate/richer evidence tables where a mandate has more than one local evidence block.
Rejected Alternatives: Merging duplicate evidence tables was rejected because local context would be lost. Rewriting atlas content was rejected after corrected scan proved 24/24 first-party asmdef coverage. Running `dotnet build` was rejected because the user explicitly forbade it.
Scalability potential: Low/MX350 review gates become easier to enforce mechanically. Middle/High/Ultra evidence sections remain searchable and consistent when quality-tier claims are reviewed.
Hardware Impact: 0 us runtime gain. This is documentation automation hygiene; it prevents false audit failures and missed evidence gates.
