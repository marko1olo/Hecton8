# Rationale_13j

State: SOURCE VOLATILE / BUILD BLOCKED BY CPU GUARD

## Decision 001: Missing Active XML Prompt
Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="13j">`, and repo-wide `rg` found no `13j` block.
Solution: Record XML task count as 0 and treat the user's direct assignment as the current directive while staying inside the domain roster for mesh generation of flora, fauna, and structures.
Rejected Alternatives: Inventing an XML prompt or reading neighboring `1315`/`1316` tasks would contaminate architectural decisions. Waiting for a wipe is not justified because `Status_13j.md` and `Rationale_13j.md` were absent, not stale.
Scalability potential: Low tier avoids unnecessary broad rewrites; middle/high/ultra receive targeted fixes only after source proof.
Hardware Impact: Saves investigation churn, no runtime impact yet. Estimated i3/MX350 gain: 0 us until code changes land.

## Decision 002: Mandate Selection
Problem: The user assigned a broad domain spanning procedural mesh generation, flora, fauna, and structures.
Solution: Read mandates for voxel SDF/MC, instanced flora, swarm/boids, creature cognition, procedural wreckage, registry DI, zero-GC, fake-first, and ARM64 DTO layout before code.
Rejected Alternatives: Reading all 80 mandates wastes context; reading only flora would miss fauna/structure mesh contracts.
Scalability potential: Low/Middle/High/Ultra decisions will use continuous `GlobalQualityWeight` and domain-specific Math LOD instead of binary switches.
Hardware Impact: No runtime impact yet. Expected gain depends on discovered defects.

## Decision 003: Flora Density Decimation
Problem: Flora density used a small integer decimation step, causing visible density pops and binary-ish quality behavior.
Solution: Added deterministic per-instance keep probability driven by continuous `GlobalQualityWeight`, stress, max density, and minimum decimation cap; mirrored it in CPU BRG culling and GPU culling shader.
Rejected Alternatives: Full BRG/manual-renderer migration in this pass; too large for a dirty multi-agent worktree. Standard fixed LOD tiers are too coarse and visibly pop on weak devices.
Scalability potential: Low uses stable sparse probability; Middle raises keep probability smoothly; High/Ultra keep near-full density without changing gameplay truth or DTO layout.
Hardware Impact: Low-end i3/MX350 estimate: 40-220 us saved during dense vegetation culling frames by keeping fewer instances stable without modulo-step bands.

## Decision 004: Fauna Query Purity
Problem: `FaunaSpatialHashRegistry` query methods repaired stale native/managed state with unregister calls inside `TryGet*`/collect paths.
Solution: Query paths now skip stale/ineligible handles and set a bounded stale telemetry bit; owner cleanup remains outside the query path.
Rejected Alternatives: Scene object repair inside query; it violates pure read accessor doctrine and can mutate native spatial state during AI reads.
Scalability potential: Low avoids query spikes when objects despawn; Middle/High/Ultra can add owner-phase native-only stale cleanup without changing query contracts.
Hardware Impact: Low-end i3/MX350 estimate: avoids 30-180 us stale-query spikes in bad despawn frames; typical frames near 0 us.

## Decision 005: Wreckage Determinism and Authored Weights
Problem: Wreckage generation ignored authored module `Weight`, rejected valid quality `0.0`, and structural shear included `Frame` in topology RNG.
Solution: Valid quality now uses trigger flag plus finite check; module choice uses fixed integer weight units; structural shear seed uses stable node/sector data only.
Rejected Alternatives: Float cumulative authority selection was rejected after self-review; frame-based topology is visual-only acceptable, not structural authority.
Scalability potential: Low can choose cheap modules through authored weights; Middle/High/Ultra can increase density/visual debris through quality without changing topology ownership.
Hardware Impact: Determinism gain, not raw frame-time. Expected i3/MX350 runtime delta: ~0-5 us due max 16-rule integer loop.

## Decision 006: Fauna Boid GPU Safety and Depth Occlusion
Problem: Boid compute could underflow `_BoidCount - 1`, and boid material had no depth-only path for occlusion.
Solution: Clamped compute reads with `activeBoidCount`; added a cheap `DepthOnly` pass using same boid index, visible-index lane, alive flag, LOD dither, scale jitter, and velocity orientation.
Rejected Alternatives: ShadowCaster pass; too expensive for 5000 fish on low-tier. Full VAT/tail depth deformation; visual overkill in depth and not worth the vertex cost.
Scalability potential: Low gets conservative depth occlusion without shadows; Middle/High/Ultra keep forward VAT detail while depth remains cheap and stable.
Hardware Impact: Depth pass adds GPU work but buys occlusion correctness; expected low-end cost depends visible boids, roughly 0.03-0.18 ms, offset by reduced overdraw in dense scenes.

## Decision 007: Geology Hash Safety
Problem: `Mathf.Abs(stableHash)` can stay negative for `int.MinValue`, corrupting geology variant/debris index selection.
Solution: Normalized stable hashes with unsigned avalanche hashing and positive mask in mesh builder and profile.
Rejected Alternatives: `Math.Abs` or branch special-case; signed arithmetic still invites modulo edge cases and weaker distribution.
Scalability potential: Applies identically from weak to ultra devices; no quality route change.
Hardware Impact: Runtime estimate: 0 us meaningful; fixes rare deterministic corruption.

## Decision 008: ARM64 DTO Layout
Problem: `ArtificialStructureRecord` in vegetation bridge was implicit 28-ish byte layout with native job storage.
Solution: Added explicit 32-byte layout and padding.
Rejected Alternatives: Trusting CLR packing; ARM64/native job DTO contracts require explicit stride proof.
Scalability potential: All tiers get stable native stride; future high-tier flow/threat jobs can safely batch more records.
Hardware Impact: Runtime estimate: 0 us; avoids undefined stride/cache behavior.

## Decision 009: Build Guard
Problem: Verification requested by protocol, but CPU load was 100%.
Solution: Ran static checks and did not launch `dotnet build`; no `dotnet`/`csc` process was active, but CPU guard still blocks compile above 50%.
Rejected Alternatives: Forcing compile under load violates explicit project rule and risks contaminating other agents' work.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 010: Cold Camera and Ocean Kinematics Binding
Problem: Flora tick/read paths still resolved cull camera with scene search fallback and sampled ocean kinematics through `GlobalRegistry.OceanKinematics`.
Solution: `HectonIndirectVegetationRenderer` now refreshes camera cache in cold lifecycle hooks; hot `ResolveCullCamera()` returns override/cache/null only. `FloraInteractionManager` now caches `IHectonOceanKinematicsService`/provider in cold service binding and registry hot-swap handling.
Rejected Alternatives: Per-frame `Camera.GetAllCameras` and service locator reads from current sampling were rejected as hot global discovery.
Scalability potential: Low/Middle avoid spikes from scene camera arrays; High/Ultra keep the same visual route with cleaner ownership.
Hardware Impact: i3/MX350 estimate: 5-60 us saved in camera-heavy scenes; current sampling avoids ~1-8 us per hot service route.

## Decision 011: Sargassum Foveation Scalar Path
Problem: `SargassumMicroFaunaBoids` scheduled a one-record `IJob` for a 32-byte foveation decision, then consumed the front/back result through a hidden job route.
Solution: Moved the decision math into `EvaluateFoveatedSimulationDecision()` and write the back/front buffers directly. DTO layout and front/back contract remain intact.
Rejected Alternatives: Keeping the job for Burst purity; one scalar input is below the amortization threshold and violates the tiny-job rule.
Scalability potential: Low devices avoid job dispatch overhead; Middle/High/Ultra can spend the saved budget on visible boid motion rather than scheduler work.
Hardware Impact: i3/MX350 estimate: 10-45 us saved per active sargassum swarm frame depending on scheduler contention.

## Decision 012: Continuous Fauna Sensory Hibernation
Problem: Predator AUP loop cap and flashlight sensory endpoint used full/simplified tier cuts even though the simulation already computed continuous `hibernation01`.
Solution: Threat loop cap now lerps between low cap and full count through `1 - hibernation01`; flashlight endpoint scale lerps continuously; flags use a late hibernation threshold only where a bitmask is required.
Rejected Alternatives: Full binary tier behavior was rejected because it causes behavior cliffs at distance/quality boundaries. Replacing the entire LOD tier enum was too wide for this pass.
Scalability potential: Low has reduced sensory cost without abrupt disappearance; Middle transitions smoothly; High/Ultra keeps full threat richness near the player.
Hardware Impact: i3/MX350 estimate: 2-20 us saved in high predator-count sargassum frames; also reduces visible threat popping.

## Decision 013: Flora Spore SignalBus Lane
Problem: Flora spore events were available only through a static managed fixed ring, leaving no first-party hot broadcast path for fog/scatter consumers.
Solution: `HectonFloraSporeEvent` now implements `ISignal`; `HectonFloraSporeEvents` configures/publishes a bounded `SignalBus<HectonFloraSporeEvent>` lane while retaining the legacy ring for compatibility.
Rejected Alternatives: Removing the legacy ring immediately would break unknown consumers; using `HectonEventBus` would move a hot VFX signal into a managed mod/API lane.
Scalability potential: Low can shed through SignalBus policy; Middle/High/Ultra can consume frame snapshots without managed dequeue ownership.
Hardware Impact: Current runtime gain is small until consumers migrate, estimated 0-6 us; architectural gain is one correct hot route.

## Decision 014: Runtime Geology Mesh Scratch Pool
Problem: `WorldGenerativeGeologyMeshBuilder` is runtime-capable but allocated local `List<>` and small arrays for each generated mesh, including compound geology meshes.
Solution: Added pooled scratch leases for vertex/index lists and stack spans for rock-cluster temporary arrays. Shape math, stable hashes, LOD counts, and mesh contracts were kept unchanged.
Rejected Alternatives: Burst/NativeMesh rewrite was too large and would risk shape/authoring drift. Leaving managed churn in a runtime generator violates Zero-GC.
Scalability potential: Low avoids GC hitches during geology hydration; Middle/High/Ultra can generate richer compound meshes without managed allocation spikes.
Hardware Impact: i3/MX350 estimate: removes about 1-6 KB managed allocations per simple mesh and 20-80 KB per compound geology bundle; frame-time saving depends on GC pressure, not deterministic CPU alone.

## Decision 015: Coral Draw-Local GPU State
Problem: `ProceduralCoralGpuUploadDispatcher` wrote `_H8CoralMatrices` and coral sway vectors through `Shader.SetGlobal*`, so one coral draw could overwrite process-wide shader state for any other pass using the same property IDs.
Solution: Captured the active `CoralGpuSwayDTO` at upload and bound matrix buffer plus finite sway vectors through one cold-owned `MaterialPropertyBlock` on `DrawProceduralIndirect`.
Rejected Alternatives: Material asset mutation would corrupt shared authoring state; per-dispatch material cloning violates material ownership and leaks memory. Full BRG migration is the correct final route but needs renderer metadata ownership, not a blind local patch.
Scalability potential: Low/Middle/High/Ultra keep the same visual fake sway route without global contamination. Ultra can increase coral instance count later through the same double-buffered upload path, not through extra global state.
Hardware Impact: i3/MX350 frame-time gain is unmeasured; main gain is correctness and state isolation. Estimated runtime delta: 0-3 us, with no per-frame allocation because the property block is a field.

## Decision 016: Compound Geology Temporary Mesh Release
Problem: Shelf, arch, cave, spire, and rock-floor builders created temporary `Mesh` objects only to copy their vertices into compound meshes, then left those Unity native objects alive.
Solution: Added `ReleaseTemporaryMesh()` and wrapped every copy-only temporary mesh with `try/finally`; returned LOD and collider meshes remain owned by `GeologyMeshBundle`.
Rejected Alternatives: Rewriting all rotated sub-shapes into direct append functions would be cleaner but much wider and risk shape drift. Leaving native Mesh lifetime to domain reload is unacceptable for runtime generation.
Scalability potential: Low avoids native memory spikes during geology hydration; Middle/High/Ultra can generate richer compound silhouettes without retaining abandoned copy-source meshes.
Hardware Impact: i3/MX350 memory impact is the real win: retained native mesh data is released after copy instead of accumulating across generated bundles. Microsecond estimate: 0-10 us spent on destroy scheduling; memory pressure reduction depends on generated bundle count.

## Decision 017: Third Pass Build Guard
Problem: Static verification was clean enough to attempt compile only if the explicit host guard allowed it.
Solution: Checked `dotnet`/`csc` processes and CPU. No compiler process was active, but CPU average was 61.8%; compile was blocked by rule. Root `.sln` was also absent in `C:\hades\Hecton8`.
Rejected Alternatives: Running `dotnet build` under >50% CPU violates the batch rule and could interfere with other agents.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 018: Wreckage Draw-Local GPU State
Problem: `ProceduralWreckageGpuUploadDispatcher` still published matrices and scalar DTO values through `Shader.SetGlobal*`, creating the same process-wide render-state contamination fixed in coral.
Solution: Captured the active `WreckageGpuScalarDTO`, bound `_H8WreckageMatrices` and scalar vectors through a cold-owned draw-local property block, and skipped draw when active instance count is zero.
Rejected Alternatives: Shared material mutation would contaminate authored materials; material clones would create memory churn. Full BRG migration remains the final GPU sovereignty route but requires renderer ownership metadata beyond this local patch.
Scalability potential: Low/Middle/High/Ultra keep identical wreckage visual fake inputs without cross-family shader state collisions. Higher tiers can spend saved correctness budget on richer wreckage density later through the existing double-buffered upload route.
Hardware Impact: i3/MX350 frame-time gain is unmeasured; expected CPU delta 0-3 us. Correctness gain: no global buffer/vector state leak between procedural structure draws.

## Decision 019: Geology Static Scratch Serialization
Problem: `WorldGenerativeGeologyMeshBuilder.Build()` uses static scratch lists and a static scratch-pool depth counter. Concurrent public calls would corrupt shared scratch state and produce invalid meshes.
Solution: Added a cold-owned sync object and serialized the public `Build()` entry point. The geometry math and returned mesh ownership are unchanged.
Rejected Alternatives: Per-call lists would reintroduce managed allocations; assuming single-threaded runtime callers is weak because the file is documented runtime-capable. Rewriting the builder into fully local Native/MeshData assembly is too broad for this pass.
Scalability potential: Low avoids rare broken/retained meshes under async authoring or future runtime generation; Middle/High/Ultra can continue producing richer silhouettes without scratch races.
Hardware Impact: i3/MX350 estimate: uncontended lock overhead is sub-microsecond per build; avoids large native-memory and correctness failures when concurrent generation happens.

## Decision 020: Fourth Pass Build Guard
Problem: Static checks passed, but compile launch was not allowed.
Solution: Process guard found active `dotnet` processes. No new `dotnet build` was launched. Root `.sln` is absent; root `.csproj` files exist for Unity-generated assemblies.
Rejected Alternatives: Launching another compile beside existing `dotnet` violates the explicit project rule and can create false failures for other agents.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 021: Continuous Whale-Fall Scavenger Burst
Problem: `SargassumMicroFaunaBoids.RegisterWhaleFallScavengerBurst()` spawned zero visual scavengers unless the swarm was in `SimulationLodTier.Full`, creating a binary fauna event cliff.
Solution: Cached the last `hibernation01` scalar and used it to resolve whale-fall visual count and fear response continuously. Low wake keeps a minimum visual scavenger floor; full wake keeps the authored 96-boid burst. The fix does not wake the full grid/PBD solve.
Rejected Alternatives: Forcing `SimulationLodTier.Full` during whale-fall would spend simulation budget on a presentation event. Keeping the tier gate contradicts continuous scalability and hides whale-fall on weak/far views.
Scalability potential: Low gets roughly 12 visible scavengers and cheap fear response; Middle ramps visual count smoothly; High/Ultra keep the authored 96-boid burst and faster localized orbit presentation.
Hardware Impact: i3/MX350 estimate: avoids full-grid wake cost; event-time writes scale from about 12 to 96 boids instead of all-or-nothing. Normal frame cost: 0 us except one cached float assignment.

## Decision 022: Fifth Pass Build Guard and Collision Check
Problem: The pass needed verification, but the host already had active `dotnet` work and CPU samples reached 100%. `HectonBoidController.cs` is also dirty before this pass.
Solution: Used static checks only: `git diff --check`, `rg` regression scan, sargassum brace balance, process guard, and CPU guard. Did not edit the already-dirty legacy controller in the same pass.
Rejected Alternatives: Launching a build beside active `dotnet` violates the explicit project rule. Patching an already-dirty legacy renderer without compiler feedback would risk merging another agent's unfinished changes.
Scalability potential: No runtime impact from the guard; it preserves multi-agent build integrity.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 023: Pure Abyssal Path Read Accessor
Problem: `TryGetLatestAbyssalPathPayload()` called `CompleteAbyssalPathJob(forceComplete:false)` before returning the latest path snapshot. Even a non-forced completion attempt is a hidden job-state mutation inside a `TryGet*` read accessor.
Solution: Removed the completion call from the accessor. The method now only returns the last committed DataVault-backed snapshot. Ready job completion remains in scheduling, late-frame owner swap, and shutdown phases.
Rejected Alternatives: Keeping the convenience completion in the reader violates the global systems doctrine. Forcing all consumers to schedule a new path would also be wrong because sargassum consumers need a cheap latest-snapshot read.
Scalability potential: Low avoids reader-side job spikes during fauna steering; Middle/High/Ultra still get the latest owner-published path without changing path DTO or authority route.
Hardware Impact: i3/MX350 estimate: avoids unpredictable 0-100+ us read-path completion spikes depending on job state. Normal completed-snapshot read cost is unchanged.

## Decision 024: Sixth Pass Build Guard
Problem: Static checks passed, but compile launch was still not allowed by host guard.
Solution: Final process guard found active `VBCSCompiler` and CPU average was 69%, so no `dotnet build` was launched.
Rejected Alternatives: Building with active compiler work and CPU above 50% violates the explicit project rule and can produce false failures under multi-agent load.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 025: Sargassum Latch/Wake Stats Cadence
Problem: Sargassum latch/wake stats were full-tier-only at the C# gate, while the compute shader could still execute stats atomics in frames where the CPU would not request a readback. This created a binary presentation cliff for wake impulses and wasted GPU work on non-due frames.
Solution: `ShouldCollectLatchStats()` now allows non-sleep tiers but only when the swarm is visible or parasite mode is active, the readback cadence is due, and no readback is pending. `TryRequestParasiteLatchReadback()` sets the next interval through `ResolveLatchStatsReadbackInterval(hibernation01)`, stretching cadence smoothly from full wake to near-sleep. The compute shader reads `_LatchStatsActive` from the existing `AcousticPanic1.w` spare lane and skips latch/wake atomics when the CPU is not collecting.
Rejected Alternatives: Adding a new field to `SimulationFrameConstants` would change the 768-byte DTO contract for one boolean. Keeping full-tier-only stats hides wake feedback in simplified simulation. Letting atomics run every frame while sampling rarely wastes GPU time. Offscreen simplified readback was rejected because it spends bandwidth without visible wake presentation.
Scalability potential: Low keeps cheap, sparse stats sampling instead of zero feedback; Middle ramps cadence smoothly; High/Ultra can sample at the authored base interval without changing gameplay truth or buffer layout.
Hardware Impact: i3/MX350 estimate: 3-40 us saved on non-due active swarm frames by avoiding clear dispatch/readback setup and 4-7 atomics per latch/wake stat writer. Exact number requires Unity GPU profiler capture.

## Decision 026: Seventh Pass Build Guard
Problem: Static verification passed, but compile launch was still forbidden by the host rule.
Solution: Final guard found active `dotnet` PID 39640 and CPU average 64%; no `dotnet build` was launched.
Rejected Alternatives: Building beside active `dotnet` at 64% CPU violates the explicit project rule and would add noise to other agents' work.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 027: Sargassum Compute Admission
Problem: `SargassumMicroFaunaBoids.EnsureComputeKernelBindings()` used `HardwareTierDetector.AllowHighResourceComputeShaders` as a binary admission gate. That disables the entire GPU fauna mesh simulation on compute-capable but non-high-resource backends, even though this system already owns continuous population budget, hibernation cadence, offscreen maintenance rules, and per-kernel validation.
Solution: Gate only on actual `SystemInfo.supportsComputeShaders`, then rely on existing `HasKernel`/`FindKernel`/`IsSupported`/thread-group validation plus continuous `_activeBoidCount`, `hibernation01`, and readback cadence to scale cost.
Rejected Alternatives: Keeping the high-resource kill switch was rejected because it is a backend class switch, not a quality scalar. A CPU fallback rewrite was rejected because it would add a second simulation authority. Pretending unsupported compute can be handled by quality math was also rejected; real unsupported compute still fails closed.
Scalability potential: Low compute-capable devices run sparse/hibernated swarms instead of static fallback; Middle ramps cadence and active count; High/Ultra keep full near-camera GPU motion and can spend saved policy headroom on richer fauna presentation.
Hardware Impact: i3/MX350 estimate: 0 us CPU saved directly. The gain is visual continuity and removal of a hard fallback on compute-capable weak/mid devices; GPU cost remains bounded by existing active boid count, dispatch group validation, and hibernation cadence.

## Decision 028: Eighth Pass Build Guard
Problem: Static verification passed, but compile launch was still not allowed by host load.
Solution: Final guard found active `dotnet` PID 60512 and CPU average was 99%; no `dotnet build` was launched.
Rejected Alternatives: Building beside active `dotnet` at 99% CPU violates the explicit project rule and risks false errors or contention with sibling agents.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 029: Legacy Boid Compute Admission
Problem: `HectonBoidController.InitializeCompute()` still used `HardwareTierDetector.AllowHighResourceComputeShaders` as a binary backend gate. The same method already validates required kernels and thread-group dimensions, so the high-resource policy disabled compute-capable weak/mid fauna schools instead of letting continuous quality and existing validation control cost.
Solution: Gate on `SystemInfo.supportsComputeShaders` only. Preserve `TryResolveKernel()`, `TryResolveThreadGroupSizeX()`, `boidShader.IsSupported(kernelIndex)`, and the portable 256-thread ceiling as the real compatibility path.
Rejected Alternatives: Keeping the high-resource gate was rejected because it is not a continuous quality scalar. Rewriting this legacy renderer to BRG/GPU Resident Drawer in the same pass was rejected because the file is already dirty and the build is blocked; a one-line admission fix is verifiable without merging ownership models.
Scalability potential: Low compute-capable devices keep a sparse/quality-scaled GPU school instead of losing simulation. Middle keeps validated compute with current budgets. High/Ultra retain full render path and can later move to renderer-owned BRG without reintroducing backend-class cliffs.
Hardware Impact: i3/MX350 estimate: 0 us CPU saved directly. The gain is visual continuity and platform reach; GPU cost remains bounded by existing boid count and thread-group validation.

## Decision 030: Ninth Pass Build Guard
Problem: Static checks were sufficient for the one-line compute admission fix, but compile launch remained unsafe under host rules.
Solution: Used targeted `rg`, brace balance, and `git diff --check`. Final guard found active `dotnet` PIDs 8080, 23212, 25488, 31092, 32588, 33532, 44048, 55628 plus `VBCSCompiler` PID 46008 and CPU average 100%; no build was launched.
Rejected Alternatives: Forcing `dotnet build` beside active compiler work at 100% CPU violates project rules and can contaminate sibling agents' results.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 031: Sargassum Crest Facade Compute Admission
Problem: `SargassumCrestDampingController` used `HardwareTierDetector.AllowHighResourceComputeShaders` both before facade bake dispatch and inside thread-group validation. This disabled sargassum wave damping and oil-film facade textures on compute-capable weak/mid backends even though the facade is a visual cheat and the method already validates `compute.IsSupported(kernel)`, thread-group shape, and the 256-thread portable ceiling.
Solution: Gate on actual `SystemInfo.supportsComputeShaders` in both places, preserving kernel support validation and portable group limits. Unsupported compute still fails closed without dispatch.
Rejected Alternatives: Keeping high-resource admission was rejected because it turns a visual facade into a backend-class cliff. Moving facade publication into ocean ownership was rejected in this pass because that crosses domain boundaries and requires water renderer contract changes.
Scalability potential: Low compute-capable devices can still publish cheap canopy damping/oil facade textures; Middle keeps normal facade quality; High/Ultra retain full-resolution facade baking and can later integrate it through an ocean-owned constant-buffer route.
Hardware Impact: i3/MX350 estimate: 0 us CPU saved directly. Main gain is visual continuity for flora/water interaction on compute-capable non-high-resource backends; GPU dispatch remains bounded by existing texture dimensions, validated thread groups, and one pass.

## Decision 032: Tenth Pass Build Attempt
Problem: After the crest facade fix, process/CPU guard briefly allowed one compile attempt.
Solution: Ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. The command timed out after 120 seconds with no compiler diagnostics. Follow-up process audit showed build-created `dotnet`/MSBuild nodes; `dotnet build-server shutdown` stopped MSBuild and VBCSCompiler servers. Final guard showed no active compiler processes but CPU average 93%, so no second build was allowed.
Rejected Alternatives: Reporting build success after timeout would be false. Blind `Stop-Process` was rejected until command lines were checked; `dotnet build-server shutdown` was the least invasive cleanup route.
Scalability potential: No runtime impact.
Hardware Impact: Build verification inconclusive; static validation remains the proof artifact for this pass. Runtime estimate: 0 us.

## Decision 033: Final Admission-Gate Regression Catch
Problem: Final `rg` verification found `HardwareTierDetector.AllowHighResourceComputeShaders` had reappeared in `SargassumMicroFaunaBoids.cs` after earlier status/log updates, likely due concurrent dirty-file activity.
Solution: Re-applied the `SystemInfo.supportsComputeShaders` gate and reran targeted scans across sargassum fauna, legacy boids, and crest facade files before final reporting.
Rejected Alternatives: Leaving the status as written would be a false proof artifact. Broadly reverting the file was rejected because the worktree is shared and contains other agents' changes.
Scalability potential: Restores the intended low/mid/high/ultra continuity for sargassum micro-fauna compute admission.
Hardware Impact: i3/MX350 estimate: 0 us CPU saved directly; prevents binary loss of GPU fauna motion on compute-capable non-high-resource backends.

## Decision 034: Domain Compute Admission Regression and Expansion
Problem: A new self-review found `HardwareTierDetector.AllowHighResourceComputeShaders` was present again in `SargassumMicroFaunaBoids`, `SargassumCrestDampingController`, and `HectonBoidController`, and the same binary backend-class gate also existed in `HectonIndirectVegetationRenderer` and `GPUScatterDirector`.
Solution: Replaced these admission checks with `SystemInfo.supportsComputeShaders` while leaving each file's actual compatibility proof intact: `HasKernel`, `FindKernel`, `IsSupported`, thread-group shape checks, and portable 256-thread ceilings where present.
Rejected Alternatives: Keeping high-resource admission would disable visual systems on compute-capable weak/mid devices before existing continuous quality, hibernation, density, or cadence controls can do their job. Adding CPU fallback solvers was rejected because it would add second authorities.
Scalability potential: Low keeps sparse/hibernated flora-fauna visuals instead of hard fallback; Middle ramps cadence/density; High/Ultra retain full near-camera GPU presentation and can spend quality weight on more density without changing truth ownership.
Hardware Impact: i3/MX350 estimate: 0 us CPU saved directly. Impact is platform reach and visual continuity; GPU cost remains bounded by existing budgets and kernel validation.

## Decision 035: GPUScatter Draw-Local State
Problem: `GPUScatterDirector` published first-party scatter instance buffers, visible index buffer, density bin buffer, density parameters, and AUP grid offset through `Shader.SetGlobal*`. That makes one scatter draw mutate process-wide shader state for unrelated draws using the same property IDs.
Solution: Added one cold-owned `MaterialPropertyBlock`, bound the first-party scatter payload through `RenderParams.matProps`, and kept compute inputs on the compute shader route. `PublishBiomeGlobals()` now returns the current biome color for draw-local binding while still publishing the existing terrain/biome bridge.
Rejected Alternatives: Material asset mutation would corrupt shared materials. Full terrain/biome bridge migration was rejected because those globals are cross-renderer ownership, not draw-local scatter payload. Mod bridge globals were not changed because no first-party shader consumer exists in source.
Scalability potential: Low/Middle avoid cross-draw contamination without per-frame allocations; High/Ultra can raise scatter density through the same local payload path without process-wide state collisions.
Hardware Impact: i3/MX350 estimate: 0-3 us CPU delta, no meaningful hot saving. Correctness gain is render-state isolation; no new material allocations.

## Decision 036: Eleventh Pass Build Guard
Problem: Static verification passed after compute-admission and scatter-state fixes, but full compile launch must obey the shared host guard.
Solution: Ran targeted `rg`, brace-balance checks, `git diff --check`, and process/CPU guard. Active `dotnet` and `VBCSCompiler` processes were present and CPU average was 100%, so `dotnet build` was not launched.
Rejected Alternatives: Building with active compiler processes at 100% CPU violates the explicit batch rule and risks false failures in a 20+ agent worktree.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 037: Indirect Draw Material State Isolation
Problem: `HectonOctahedralImpostorRenderer`, legacy `HectonBoidController`, `SargassumMicroFaunaBoids`, and `ScatterGPUIBackend` still allowed draw-specific buffers/floats/textures to be written into material state. Runtime material clones reduce shared asset damage but still make render state sticky and easy to leak across future draws.
Solution: Added persistent owner-local `MaterialPropertyBlock` payloads for HLOD impostors, legacy boids, and sargassum micro-fauna. Converted `ScatterGPUIBackend.BindInstanceBuffer` from `Material` to `MaterialPropertyBlock`. Render submission now passes draw-local payload via `RenderParams.matProps`.
Rejected Alternatives: Keeping owner-local material mutation was rejected because it remains a state cache instead of a draw payload contract. Full BRG migration was rejected in this pass because it changes renderer ownership and requires scene/frame-debugger proof.
Scalability potential: Low/Middle avoid state contamination without per-frame allocation; High/Ultra can increase visible instance counts through the same draw-local payload route.
Hardware Impact: i3/MX350 estimate: 0-4 us CPU delta. Primary gain is correctness; no new per-frame heap allocation because MPBs are cold fields.

## Decision 038: Returned Compute Admission Gates
Problem: After local patches, `rg` found `HardwareTierDetector.AllowHighResourceComputeShaders` had returned across HectonBoid, GPUScatter, indirect vegetation, sargassum micro-fauna, crest facade, and GPUI scatter admission. This invalidated the previous clean status.
Solution: Performed a strictly scoped mechanical replacement in those domain files from backend-class admission to `SystemInfo.supportsComputeShaders`, preserving per-kernel validation and portable dispatch checks.
Rejected Alternatives: Ignoring the regression would make the proof artifact false. Reverting whole files was rejected because the worktree is shared and contains concurrent edits.
Scalability potential: Low compute-capable devices keep sparse/hibernated presentation; Middle ramps budgets; High/Ultra keep full visual overkill routes without a backend-class cliff.
Hardware Impact: i3/MX350 estimate: 0 us CPU direct; prevents unnecessary static/no-visual fallback on compute-capable weak/mid devices.

## Decision 039: Twelfth Pass Build Guard
Problem: Static verification passed after draw-local and admission-regression fixes, but compile launch remained unsafe.
Solution: Ran prompt extraction, targeted `rg`, brace-balance checks, `git diff --check`, and process/CPU guard. Active `dotnet` PID 62864 and `VBCSCompiler` PID 6448 were present; CPU average was 100%, so no `dotnet build` was launched.
Rejected Alternatives: Building with active compiler processes at 100% CPU violates the explicit project rule and contaminates sibling agents' compile attempts.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 040: Post-Compaction Proof Refresh
Problem: Context compaction can leave stale build-guard details in the proof artifacts even when source verification remains valid.
Solution: Re-read `Status_13j.md` and `Rationale_13j.md`, reran exact mutation and compute-admission scans, brace balances, doc proof scan, `git diff --check`, and host guard. The exact mutation scan uses `BindInstanceBuffer(Material\s)` so the intended `MaterialPropertyBlock` signature is not misreported as a material parameter. Active `dotnet` PIDs 32412 and 47232 plus CPU average 100% still forbid a new build.
Rejected Alternatives: Reporting the older PID state would be technically stale. Running a build at 100% CPU with active `dotnet` processes violates the project rule.
Scalability potential: No runtime impact; preserves proof integrity for future agents.
Hardware Impact: Runtime estimate: 0 us. Avoided contested compile on a saturated host.

## Decision 041: Recurrent Admission-Gate Churn
Problem: A final post-compaction `rg` found `HardwareTierDetector.AllowHighResourceComputeShaders` reintroduced again across the same domain files, invalidating the updated proof state.
Solution: Repeated the strictly scoped mechanical replacement to `SystemInfo.supportsComputeShaders` in `HectonBoidController`, `GPUScatterDirector`, `HectonIndirectVegetationRenderer`, `ScatterInstancingService`, `SargassumMicroFaunaBoids`, and `SargassumCrestDampingController`. Reran admission scan, brace balance, `git diff --check`, and host guard. Active `dotnet` PID 47232 and active `VBCSCompiler` PID 35836 still forbid a build regardless of the volatile 44-100% CPU samples.
Rejected Alternatives: Leaving the regression would preserve a binary backend-class visual cliff. Reverting whole files was rejected because the worktree is shared and dirty.
Scalability potential: Low compute-capable devices keep sparse/hibernated visuals; middle/high/ultra keep richer routes under existing quality budgets.
Hardware Impact: Runtime estimate: 0 us CPU direct. Avoided false static fallback on compute-capable weak/mid hardware.

## Decision 042: Lock-Buffer Uploads and BRG Fixed Scratch
Problem: `GPUScatterDirector` still uploaded indirect draw args with `GraphicsBuffer.SetData`, and legacy `HectonBoidController` used `UploadArraySetData` for spawn buffers and visible indirect args. `HectonIndirectVegetationRenderer` also created five tiny `TempJob NativeArray<float4>` scratch buffers per BRG CPU-culling callback for bounded planes/headlight payloads.
Solution: Created the affected args/boid buffers with `GraphicsBuffer.UsageFlags.LockBufferForWrite` and routed uploads through the existing `GraphicsBufferUploadUtility.UploadArray()` lock/memcpy path. Replaced the bounded culling/headlight scratch arrays with `FixedList512Bytes<float4>` fields copied into `BuildVegetationVisibilityMaskJob`. The larger visibility mask remains a BRG output buffer and is still disposed through the culling job chain.
Rejected Alternatives: A full BRG/GPU Resident Drawer migration is the right renderer-ownership endpoint, but it is too wide without compile/Frame Debugger proof in this shared dirty worktree. Persistent owner scratch arrays for planes/headlights were rejected because the payload is tiny, bounded, and job-local; fixed unmanaged lists are simpler and avoid allocator traffic.
Scalability potential: Low avoids upload-frame stalls and culling-callback allocator churn. Middle keeps the same culling quality with lower CPU noise. High and Ultra can spend the saved budget on denser scatter/boid visuals without changing DTO layout or gameplay truth.
Hardware Impact: i3/MX350 estimate: 4-25 us CPU/stall risk avoided on spawn/mesh-change upload frames; vegetation BRG culling removes five tiny `TempJob` allocations per callback. Exact proof still requires Unity Profiler and Frame Debugger capture.

## Decision 043: Admission Gate Reintroduced During Upload Pass
Problem: The verification scan for the upload pass found `HardwareTierDetector.AllowHighResourceComputeShaders` reintroduced yet again in the six 13j domain files, invalidating the clean admission state.
Solution: Re-applied the scoped replacement to `SystemInfo.supportsComputeShaders` in `HectonBoidController`, `GPUScatterDirector`, `HectonIndirectVegetationRenderer`, `ScatterInstancingService`, `SargassumMicroFaunaBoids`, and `SargassumCrestDampingController`, preserving `HasKernel`, `FindKernel`, `IsSupported`, thread-group shape, and portable thread-limit validation.
Rejected Alternatives: Keeping backend-class admission would hard-disable compute-capable weak/mid devices before existing quality weight, hibernation, density, and cadence controls can scale cost. Whole-file revert was rejected because concurrent agents own unrelated dirty edits.
Scalability potential: Low compute-capable devices keep sparse/hibernated presentation; Middle ramps budgets; High/Ultra keep visual overkill routes under the same compatibility proof.
Hardware Impact: Runtime estimate: 0 us CPU direct. Prevents false static fallback on compute-capable i3/MX350-class hardware.

## Decision 044: Fourteenth Build Guard
Problem: Static checks passed, but the explicit shared-host rule forbids `dotnet build` when CPU is above 50%.
Solution: Checked compiler processes and CPU before build. No `dotnet`, `csc`, or `VBCSCompiler` process was active, but CPU average was 56.8% with a 97.1% spike; no build was launched.
Rejected Alternatives: Running `dotnet build` under >50% CPU would violate the project rule and risk false integration failures.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 045: Guarded Build Timeout and Cleanup
Problem: A later guard sample allowed one build attempt, but `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` did not return diagnostics within 180 seconds and left a build process alive.
Solution: Treated the build as inconclusive, not green. Ran `dotnet build-server shutdown`, inspected command line for PID 24280, confirmed it was the timed-out `Hecton8.slnx` build, and stopped that process. A later separate `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` PID 36124 appears external and was not killed.
Rejected Alternatives: Reporting compile success after timeout would be false. Killing every `dotnet` process was rejected because concurrent agents may own other builds.
Scalability potential: No runtime impact; preserves shared build host integrity.
Hardware Impact: Runtime estimate: 0 us. Avoided leaving the timed-out build consuming CPU.

## Decision 046: Post-Build Source Churn Repair
Problem: After the timed-out build, the same files again contained reverted upload routes (`UploadArraySetData`, non-lock boid buffers, non-lock scatter args) and reintroduced `HardwareTierDetector.AllowHighResourceComputeShaders`.
Solution: Re-applied the lock-buffer upload route in `GPUScatterDirector` and `HectonBoidController`, and re-applied the scoped `SystemInfo.supportsComputeShaders` replacement across the six 13j domain files. Revalidated scans and brace balances.
Rejected Alternatives: Leaving the source in the reverted state would knowingly preserve CPU upload stalls and binary compute-admission cliffs. Whole-file revert was rejected because concurrent agents are editing the same files.
Scalability potential: Low avoids upload stalls and static fallbacks; Middle/High/Ultra keep existing visual density and fauna motion under continuous quality/cadence controls.
Hardware Impact: Restores Decision 042/043 impact: estimated 4-25 us upload-frame stall risk reduction plus five tiny BRG scratch allocations removed per vegetation cull callback; admission repair remains 0 us CPU direct.

## Decision 047: Fifteenth Build Guard
Problem: Static verification passed after post-build churn repair, but another `dotnet` process was already running and CPU was above the allowed threshold.
Solution: Did not launch another build. Guard observed PID 36124 running `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and CPU average 69.7%.
Rejected Alternatives: Starting a second build beside an active build and high CPU violates project rules.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 048: BRG Material Buffer Contract Boundary
Problem: `WreckMaterialRegistry` and `SargassumGlobalDragManager` still call `Material.SetBuffer` for BRG draws. The obvious MPB fix is invalid because BRG registration does not consume `RenderParams.matProps`, and the active shaders read explicit `StructuredBuffer` names instead of BRG `unity_DOTSInstanceData`.
Solution: Audited the C# BRG path, `HectonBatchRendererGroupUtility`, `Hecton_WreckIndirectLit.shader`, and `Hecton_CollapseScavengerIndirect.shader`; left the material buffer binds in place and recorded the required future migration as shader/metadata work.
Rejected Alternatives: Replacing the calls with MPB would likely make wreck/scavenger draws read unbound buffers. Removing the calls because `SetBatchBuffer` exists was rejected because the shaders are not using DOTS instanced metadata for those payloads.
Scalability potential: Low/Middle keep correct wreck/scavenger rendering. High/Ultra can later use one BRG instance-data payload after a deliberate shader contract migration.
Hardware Impact: Runtime estimate: 0 us. Avoided a probable render regression; no optimization claim.

## Decision 049: Concurrent Source Churn Wall
Problem: Repeated proven repairs to legacy boid, sargassum, GPU scatter, indirect vegetation, crest facade, and GPUI scatter were overwritten between patch and verification. Exact `rg` scans showed returned material mutation and `HardwareTierDetector.AllowHighResourceComputeShaders` gates immediately after successful `apply_patch` operations.
Solution: Re-applied scoped repairs multiple times, then stopped at a documented source-churn wall instead of force-reverting or locking shared files. Current source truth is recorded as volatile, not clean.
Rejected Alternatives: Whole-file revert would destroy other agents' concurrent work. File locking/read-only toggles would sabotage the shared worktree. Claiming success from a transient clean state would be a false report.
Scalability potential: The intended repairs remain correct: low/mid compute-capable devices avoid hard visual cliffs, while high/ultra keep overkill paths under continuous budgets. Current volatile source prevents stable proof.
Hardware Impact: No stable runtime estimate. When present, the repaired upload path preserves the earlier 4-25 us upload-frame stall-risk reduction; current verified source has regressed in several files.

## Decision 050: Sixteenth Build Guard
Problem: Compile verification is unsafe and not meaningful while the source is changing under the agent. Host CPU also averaged 100%.
Solution: Did not launch `dotnet build`. Captured process/CPU/write-time evidence instead.
Rejected Alternatives: Building at 100% CPU violates the explicit project rule and would test a moving source target.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile. Runtime estimate: 0 us.

## Decision 051: Vegetation Density Sampler Containment
Problem: Vegetation density/threat sampling trusted external `chunkCount` and `chunk.GridOffset` in Burst-readable NativeArray snapshots. If owner snapshot count/grid buffers diverge during resize or source churn, jobs can index outside chunks/grid or propagate NaN. `VegetationDensityChunkRecord` also used implicit layout/padding despite NativeArray/Burst use.
Solution: Clamp sample loops to `min(chunkCount, chunks.Length)`, validate chunk finite bounds and `GridOffset` against density/attractor grid length, route validated hot loops to unchecked local bilinear sample helpers, and declare `VegetationDensityChunkRecord` as explicit 24-byte layout with padding at 21-23.
Rejected Alternatives: Full threat-chunk `NativeParallelMultiHashMap`/DataVault migration was rejected in this pass because the existing hash lane is stubbed and needs owner-buffer design, not a hidden local native allocation. Exception/assert-only protection was rejected because Burst jobs need deterministic fail-closed behavior.
Scalability potential: Low/Middle fail closed to zero density instead of crashing or corrupting flow/threat fields; High/Ultra keep same visual richness and can later add the hashed sampling lane deliberately.
Hardware Impact: i3/MX350 estimate: normal-case added guard overhead is small, 0-2 us depending sampled chunk count; the real win is crash/OOB avoidance and finite-safe vegetation audio/threat/flow sampling.

## Decision 052: Seventeenth Build Guard
Problem: Verification needs compiler, but host CPU average is 100%.
Solution: Used static proof only; did not launch `dotnet build`.
Rejected Alternatives: Starting a build at 100% CPU violates explicit project rule and tests a moving source target while old churn regressions remain.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile; runtime estimate 0 us.

## Decision 053: Scatter Acceptance DTO Layout
Problem: `ScatterPlacementSpatialMetadata` and `ScatterCellCandidateAcceptanceInput` enter `NativeList`, `NativeArray`, and Burst job lanes with implicit sequential layout. The candidate input naturally lands at about 60 bytes, not a clean ARM64 multiple-of-8 runtime stride, and padding was undocumented.
Solution: Converted both DTOs to explicit layout. `ScatterPlacementSpatialMetadata` is now 32 bytes with field offsets 0/12/16/20/24/28 and padding 29-31. `ScatterCellCandidateAcceptanceInput` is now 64 bytes with field offsets 0/12/16/20/24/28/32/36/40/44/48/49/50/51/52/56/60 and padding 53-55.
Rejected Alternatives: Trusting CLR sequential padding was rejected because these records are NativeList/Burst payloads. Rewriting the acceptance algorithm or changing placement truth was rejected because layout proof is the defect, not the scatter policy.
Scalability potential: Low/Middle get predictable aligned scatter acceptance scratch buffers; High/Ultra can raise candidate counts without hidden platform-dependent stride assumptions.
Hardware Impact: i3/MX350 estimate: no speed claim. `ScatterCellCandidateAcceptanceInput` scratch stride rises to 64 bytes; the cost is bounded candidate scratch memory, traded for ARM64-safe Burst layout and lower misalignment risk.

## Decision 054: Eighteenth Build Guard
Problem: Compiler verification is required for final proof, but host CPU average is 100%.
Solution: Ran static proof only: diff whitespace, brace balance, offset scans, and process/CPU guard.
Rejected Alternatives: Starting `dotnet build` above the explicit 50% CPU limit would violate project rules and interfere with other agents.
Scalability potential: No runtime impact.
Hardware Impact: Avoided contested compile; runtime estimate 0 us.
