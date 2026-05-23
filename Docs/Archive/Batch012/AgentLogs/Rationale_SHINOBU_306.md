# SHINOBU_306 Rationale

Status: LOOP 22 BLOCKED BY CONCURRENT UNITY DOTNET WRITER: CURRENT SHINOBU SOURCE DRIFTED AGAIN; PREBUILD GUARD ADDED; PRIOR HECTON8.CORE DOTNET BUILD GREEN; REBUILD GATED BY UNITY DOTNET CPU 100%; UNITY RUNTIME PENDING

## Pre-Code Decisions

Problem: Mission requires visual/behavioral fish variation without material cloning, per-entity managed stats, or UnityEngine random.
Solution: Use one authoritative `ulong GeneticMask` packed from deterministic AUP/world seed data; feed that seed into `Unity.Mathematics.Random.CreateFromIndex` for Burst-safe trait rolls; expose static bit extraction methods for CPU/Burst and mirror unpacking in shader code.
Rejected Alternatives: `Material.SetColor`, renderer `.material`, prefab variants, managed dictionaries, C# properties on DTOs, and `UnityEngine.Random` are rejected because they break SRP batching, allocate or fragment state, and desync deterministic rollback.
Scalability potential: Low uses base species color plus cheap size/aggression extraction; Middle enables hue shift; High enables pattern overlay; Ultra spends saved CPU on richer shader presentation while gameplay truth stays one mask.
Hardware Impact: Expected gain on i3/MX350 is from avoiding material clones, managed stat lookups, and per-fish renderer mutation. Exact microseconds are PENDING VERIFICATION until static compile/profiler evidence exists.

Problem: Runtime struct layout can punish ARM64 if `ulong GeneticMask` is unaligned.
Solution: Audit existing DTO before editing. If adding/changing DTO layout, use explicit offsets or natural 8-byte order and record offset proof.
Rejected Alternatives: `[StructLayout(Pack=1)]`, appended byte fields without padding audit, or hidden properties are rejected due to ARM64 unaligned access and CS1612 copy risks.
Scalability potential: Same DTO supports all tiers; quality affects shader/cadence only, not truth layout.
Hardware Impact: Expected gain is fewer unaligned loads and stable blind memcpy snapshots. Exact value PENDING VERIFICATION.

Problem: Other agents may be editing fauna, renderer, and DataVault code in parallel.
Solution: Discover existing owners and prefer isolated partial files/contracts. Do not invent direct dependencies or new global routes without matrix proof.
Rejected Alternatives: New monolithic `HectonGeneticsManager` or private buffers are rejected because they create compile walls and authority duplication.
Scalability potential: Stateless kernel can run at spawn cadence on low hardware and feed richer presentation on high hardware.
Hardware Impact: Compile-time and runtime impact are controlled by isolated files and unmanaged data routes; exact cost PENDING VERIFICATION.

## Implemented Decisions

Problem: Previous genome layout spent 16 bits on hue and had no explicit pattern or biolum frequency lane.
Solution: Repartitioned `FaunaGenome64` into one 8-byte mask: size 8, speed 8, aggression 8, hue 8, pattern 4, biolum frequency 8, mutation flags 4, reserved hash 16. Existing mutation flags moved to bits 44..47.
Rejected Alternatives: Adding separate fields to `FaunaGeneticTraits`, ScriptableObject variant rows, or material color properties was rejected because it duplicates truth and breaks deterministic snapshot simplicity.
Scalability potential: Low uses size/aggression CPU extraction only; Middle admits hue; High admits pattern; Ultra adds stronger biolum pulse and richer tint while the mask layout stays fixed.
Hardware Impact: Expected i3/MX350 gain is fewer cache lines versus multiple floats and no genetic `SetColor`; exact microseconds pending profiler because Unity compile was blocked.

Problem: SHINOBU procedural swarm needed one-draw-call GPU unpack, not per-fish material mutation, and float bitcasts of arbitrary uint masks can become NaN/Inf payloads.
Solution: Replaced the former float4 custom payload with explicit `BoidCustomDataDTO` (`uint GeneticLow`, `uint GeneticHigh`, `float PanicOrSkip`, `float QualityWeight`) and changed the shader to consume the uint halves directly.
Rejected Alternatives: A second material per phenotype, `MaterialPropertyBlock` per boid, a new global signal lane, and float bitcasts were rejected. The existing custom-data buffer remains the instance route but now uses a typed 16-byte ABI.
Scalability potential: Low quality suppresses hue/pattern contribution; Middle/High/Ultra progressively spend shader ALU on visible diversity without changing draw count or authority.
Hardware Impact: Uses an existing 16-byte instance payload, so additional upload bandwidth is 0 bytes for the SHINOBU swarm route; added shader integer ALU is gated by continuous quality.

Problem: Loaded scene fauna still had legacy `Material.SetColor` calls in presentation code.
Solution: Removed targeted `SetColor` calls and uploaded exact mask bytes as two `Vector4` lanes for the Leviathan organic shader fallback. Non-genetic runtime material clone residue remains documented because corpse/infection/death shader state still depends on that owner path.
Rejected Alternatives: Full conversion of all `FaunaBrain` presentation state to property blocks was rejected during this pass because unrelated agents already had dirty edits in the same massive file and a broad rewrite would create a compile wall.
Scalability potential: Loaded fauna receives the same Low/Middle/High/Ultra shader scaling as procedural swarm, but the authoritative mask remains CPU-owned.
Hardware Impact: Genetic color mutations no longer call `Material.SetColor`; exact CPU/VRAM gain pending profiler. Existing non-genetic material clone route remains a known residue, not claimed eradicated.

Problem: AUP seed quality was vulnerable if local positions were bucketed too coarsely, raw float bits drifted, or loaded-fauna fallback hashed floating-origin `Vector3`.
Solution: `BuildAupSeed` folds grid coordinates plus millimeter-quantized local AUP lanes; `BuildDoubleAupSeed` folds raw finite `double` bits and millimeter-quantized absolute lanes. `FaunaGeneticsManager` now delegates AUP proof path to `FaunaGenome64.BuildAupSeed` and falls back to stable world/species/biome data only.
Rejected Alternatives: Wall-clock seed, absolute `float3` hashing, current moving swarm position, and slot-index-dependent render masks were rejected due rollback drift and visible phenotype changes during movement/compaction.
Scalability potential: Same deterministic seed feeds all quality tiers; quality never mutates gameplay truth.
Hardware Impact: Integer/FNV folding is fixed-cost and avoids managed RNG overhead. Estimated gain is determinism, not measurable frame-time until spawn batches are profiled.

Problem: Post-polish audit found two precision/proof defects: the double-AUP path did not explicitly include raw IEEE-754 bits, and telemetry `CompiledGenomeCount` could under-report when no mutation batch ran but valid masks remained active in Vault.
Solution: Fold `math.asulong(double)` low/high lanes before the millimeter-quantized folds, and write telemetry compiled count as `max(last mutation batch count, active valid masks)`.
Rejected Alternatives: Raw `float3` hashing was rejected because it loses edge-of-map identity; reporting only last mutation batch count was rejected because it hides steady-state compiled genomes from the 300-frame black box.
Scalability potential: No tier changes; this tightens determinism/proof while preserving continuous shader quality scaling.
Hardware Impact: Adds six integer folds on cold/spawn seed compilation only. On i3/MX350 this is below profiler resolution compared with avoided material mutation; exact runtime delta remains pending Unity profiler proof.

Problem: The stricter mandate names `Unity.Mathematics.Random` for deterministic genetic generation, while the first pass used a local LCG after the AUP hash.
Solution: Keep FNV/AUP hash as the deterministic seed source, then construct `Unity.Mathematics.Random.CreateFromIndex` for size/speed/aggression/hue/pattern/biolum rolls and mutation roll decisions. Removed `NextLcg` from `FaunaGenome64`.
Rejected Alternatives: `UnityEngine.Random`, `System.Random`, wall-clock seed, and keeping a local LCG for trait rolls were rejected because they weaken the explicit deterministic RNG proof expected by the prompt.
Scalability potential: No quality tier changes; the same mask truth feeds Low/Middle/High/Ultra shader expression.
Hardware Impact: `Unity.Mathematics.Random` is an unmanaged struct and adds no managed allocation. Exact timing remains pending because CPU/build gate is closed.

Problem: Tasks 15-18 were missing in the first static pass, leaving no dedicated genetics black box, no designer bridge, no CSV profile route, and no live mask visualization.
Solution: Added Vault-owned `GeneticsTelemetryEntry[300]`, `FaunaGeneticsTuningDTO`, `FaunaGeneticsProfileDTO`, CSV scratch, raw dump writer, UI Toolkit tuner, cold `ReadOnlySpan<byte>` profile parser, and SceneView mask gizmo.
Rejected Alternatives: Private NativeArrays, managed dictionaries, `float.Parse`, string-only telemetry, and runtime debug GameObjects were rejected because they violate DataVault ownership, zero-GC hot-path policy, or editor/runtime boundary discipline.
Scalability potential: Low uses narrow visual admission; Middle/High/Ultra increase shader expression while the same profile/tuning DTOs remain active. Designers can alter ranges without recompiling C#.
Hardware Impact: Telemetry is fixed 64-byte rows; CSV/gizmo/tuner are cold/editor. Low-end silicon pays no gameplay allocation tax.

Problem: Generated Unity project metadata was stale and would report false missing-type diagnostics for SHINOBU_306 source files during external dotnet verification.
Solution: Kept runtime contracts in already-owned `FaunaGenome64.cs` and refreshed `Hecton8.Core.csproj` with SHINOBU_306 source entries so compile verification will see the same files Unity will import.
Rejected Alternatives: Leaving new runtime contracts in an unlisted file was rejected because previous narrow compile evidence already showed missing-type blockers. Broad project regeneration was rejected because Unity owns generated project files and another compiler is active.
Scalability potential: No runtime effect.
Hardware Impact: Prevents wasted compile attempts caused by stale source inclusion.

Problem: Verification initially hit active compiler guards, then a real SHINOBU_306 compile error, then an external dependency wall.
Solution: Waited for CPU/compiler gate, ran `dotnet build Hecton8.Core.csproj -v:minimal`, fixed the two SHINOBU_306 AUP blit errors by changing `CompileFaunaGenomeJob.SpawnAups` to `NativeArray<AbsoluteUniversePositionBlit>`, then reran the build. Second build produced zero SHINOBU_306 errors and failed only in `PredatorCognitionDomain.cs`/`FaunaBrain.cs` steering symbols.
Rejected Alternatives: Editing predator steering symbols was rejected because that is outside SHINOBU_306 ownership and would be architectural sabotage without the SHINOBU_303 route card.
Scalability potential: No runtime effect.
Hardware Impact: Prevented repeated compile churn after the dependency wall was proven.

Problem: RNG hardening needs compile reverify, but a no-restore attempt stopped before C# and the CPU gate then closed.
Solution: Recorded NETSDK1004 (`Temp/obj/Hecton8.Core/project.assets.json` missing) as restore/build-gate evidence, not a source error. Deferred restore-enabled build while CPU samples stayed above 50%.
Rejected Alternatives: Launching restore/build under 67-82% CPU was rejected by AGENTS compile discipline.
Scalability potential: No runtime effect.
Hardware Impact: Avoided build spam under high CPU load.

Problem: Restore-enabled C# compile exposed a SHINOBU_306 namespace miss in `ShinobuEcosystemBalancer`: `FaunaGenome64` is owned by `Hecton8.Ecosystem`, while the render payload job lives in the AI/Ecosystem namespace.
Solution: Fully qualified both render-payload calls as `Hecton8.Ecosystem.FaunaGenome64.*`, avoiding a broad `using` import and avoiding any new assembly dependency.
Rejected Alternatives: Adding a new wrapper or moving `FaunaGenome64` was rejected because it would create a compile-wall surface for a two-call namespace issue.
Scalability potential: No runtime effect; the same 16-byte GPU custom payload route remains.
Hardware Impact: No per-frame cost. Reverify is deferred while CPU gate is closed at 99%.

Problem: Compile proof had to be rerun after namespace and RNG hardening without violating CPU/compiler gates.
Solution: Waited for CPU to drop to 16% with no active dotnet/csc/VBCSCompiler process, then ran `dotnet build .\Hecton8.Core.csproj -v:minimal -maxcpucount:1`.
Rejected Alternatives: Full solution build and Unity import were rejected because the task needed narrow C# source proof and runtime/editor verification remains a separate heavier gate.
Scalability potential: No runtime effect.
Hardware Impact: Build completed in 68.66 seconds with 0 warnings and 0 errors, proving no current SHINOBU_306 C# compile wall in Hecton8.Core.

Problem: The CSV profile bridge could not express a species with zero pattern variation because `patternMask=0` was converted to `15`, and profile application forced the mask to at least `1`.
Solution: Preserve parsed `patternMask` exactly and apply it directly with `patternIndex &= profile.PatternMask & 0x0F`, so zero becomes an authored no-pattern contract while omitted/malformed cells still default through parser fallback.
Rejected Alternatives: Keeping the forced nonzero mask was rejected because it blocks designer-authored visual restraint for leviathan-like species and makes the CSV bridge dishonest.
Scalability potential: Low stays base-color readable; Middle/High/Ultra can still admit patterns for species whose CSV mask enables them. Quality controls visual expression, not profile truth.
Hardware Impact: No added runtime cost; one integer `and` remains the profile clamp.

Problem: SHINOBU procedural swarm genetics used `StableSeed` that was originally LCG-derived from spawn jitter, and parent reproduction mutated `StableSeed`, allowing phenotype drift after reproduction.
Solution: Derive `AmbientEntityAupDTO.StableSeed` at spawn from `FaunaGenome64.BuildAupSeed(in aup, sectorHash, speciesHash, rollIndex)`, keep parent `StableSeed` stable during reproduction, and derive child `StableSeed` from child spawn AUP/sector/species.
Rejected Alternatives: Building render masks from current AUP every frame was rejected because moving fish would change phenotype. Keeping LCG seed input was rejected because the assignment explicitly requires AUP-seeded genetics.
Scalability potential: Same spawn truth feeds Low/Middle/High/Ultra shader expression; `GlobalQualityWeight` still changes only presentation richness.
Hardware Impact: Spawn-only integer folds replace LCG-derived genetic seed input. Per-frame renderer upload cost is unchanged; recompile proof is pending because CPU/compiler gate is closed.

Problem: Loop 13 source changes require compile reverify, but the workstation is still above the AGENTS compile ceiling.
Solution: Rechecked the gate before build; CPU averaged 79%, so no `dotnet build` was launched. Updated status/log/self-audit/report to state that Loop 13 remains under static evidence plus prior green Core build.
Rejected Alternatives: Launching a narrow Core build under 79% CPU was rejected because it violates the explicit CPU/compiler discipline and risks competing with other agents or Unity background work.
Scalability potential: No runtime effect; this is evidence hygiene only.
Hardware Impact: Avoids build IO/CPU contention on the shared machine. Runtime microseconds saved: 0.

Problem: A static self-read found a local SHINOBU copy of the genetic packer and AUP seed fold, creating a shadow compiler next to the authoritative `FaunaGenome64`.
Solution: Converted the SHINOBU helpers into thin delegates to `Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed`, `BuildStableEntitySeed`, and `CompileGeneticMaskFromSeed`; removed duplicate local pack/FNV/quantization helpers; child seeds now use child AUP plus child sector/species lane, not parent seed in the world lane.
Rejected Alternatives: Keeping duplicated bit math was rejected because any future bit-layout change could silently desynchronize shader/custom-data phenotypes from headless fauna genomes.
Scalability potential: No quality tier change; the same authoritative mask truth still drives Low/Middle/High/Ultra shader expression.
Hardware Impact: Per-frame cost remains one wrapper call into the same integer math. Runtime microseconds saved: 0 measured; architectural risk reduced.

Problem: Russell's source audit proved the prior Loop 15 status was ahead of the file state: `ShinobuEcosystemBalancer` still contained local FNV/pack helpers after a concurrent edit drift.
Solution: Re-applied the collapse against the current source and verified no local `PackFaunaGeneticMask`, `FoldFnv32`, `QuantizeMetersToMillimeters`, or `childSectorHash ^ meta.StableSeed` remain. Documentation now states the narrower truth: `Unity.Mathematics.Random` hits remain in deterministic non-genetic spawn/jitter and authoritative routes, not as a local bit-layout owner.
Rejected Alternatives: Leaving the false status line was rejected because it would let the shader ABI and headless genome compiler diverge later. Removing all `Unity.Mathematics.Random` from SHINOBU was rejected because deterministic random is still valid for spawn/jitter and is explicitly required for genetics.
Scalability potential: No quality tier change. Low/Middle/High/Ultra still consume one authoritative mask and continuous shader gates.
Hardware Impact: Runtime microseconds saved: 0 measured. Risk reduction is prevention of future layout divergence and false proof.

Problem: Russell also flagged two FrostTick-stage `GlobalRegistry.AmbientBiota` reads in `EcosystemDirector`, creating a hot-polling route during macro-swarm hydration/dehydration.
Solution: Added a cached `IAmbientBiotaService` field, cold registry cache, hot-swap listener registration, and `OnGlobalRegistryServiceReplaced` handling for AmbientBiota plus existing cached neighbors; hydration/dehydration now read the cached field.
Rejected Alternatives: Polling `GlobalRegistry.AmbientBiota` inside those methods was rejected under GlobalRegistry cold identity doctrine. Creating a new signal or service route was rejected because AmbientBiota already owns the fact and GlobalRegistry already publishes rebinding.
Scalability potential: No visual tier change. Macro-swarm hydration keeps the same continuous quality inputs; only dependency access moves out of the hot path.
Hardware Impact: Avoids two registry lookups per residency hydration/dehydration attempt. Exact microseconds pending profiler; expected low-end gain is small but removes architectural jitter.

Problem: Boole's shader ABI audit needed to be captured in durable evidence instead of chat-only notification.
Solution: Recorded that `BoidCustomDataDTO` matches the 16-byte uint/float ABI, procedural and Leviathan shaders unpack the declared bit layout, genetic mask route uses `SetVector`/buffer lanes rather than `SetColor`, and no blocking shader keyword explosion was found.
Rejected Alternatives: Treating subagent output as final runtime proof was rejected because it is static only; Unity shader import, Frame Debugger, RenderDoc, Play Mode, GCMonitor, and profiler proof remain pending.
Scalability potential: Confirmed shader paths use continuous `GlobalQualityWeight` lerp/smooth gates without binary keywords.
Hardware Impact: No new runtime cost; evidence prevents accidental float-bitcast or material-color regressions.

Problem: The editor scanner source could append the SHINOBU_306 report once but would leave stale counts on later menu runs because it returned when the report section already existed.
Solution: Added a small object replacement path that finds the existing `shinobu_306_oop_variant_scanner` object, balances braces with string escaping, and replaces only that section. BuildJson now emits the durable report wording for genetic vs non-genetic residue.
Rejected Alternatives: Full JSON DOM parsing was rejected for this editor-only companion because Unity's available package surface is uncertain and a narrowly scoped object replacement is sufficient for one known top-level section.
Scalability potential: No runtime tier effect. It preserves evidence hygiene for future source scans.
Hardware Impact: Editor-only file IO/string work. Runtime microseconds saved: 0.

Problem: A concurrent Unity/background writer repeatedly restored the local genetic helper body in `ShinobuEcosystemBalancer.cs` after three delayed patch attempts, making direct deletion unstable while Unity dotnet processes held the machine at 100% CPU.
Solution: Patch the active call route instead of fighting the contested helper block. Initial spawn, render payload mask generation, child reproduction, and macro hydration now call `Hecton8.Ecosystem.FaunaGenome64` directly. The restored local helper definitions are dead residue and must be removed by the integrator after the writer releases the file.
Rejected Alternatives: Killing Unity dotnet was rejected because it is outside this agent's authority and risks user/editor state. Continuing to rewrite the helper body while the writer is active was rejected after three strikes because it creates false proof churn.
Scalability potential: Low/Middle/High/Ultra still consume the authoritative mask route. Dead helper residue does not feed the active shader/custom-data path after call-site bypass.
Hardware Impact: Runtime microseconds saved: 0 measured. Compile/build proof remains gated by CPU 100% and active Unity dotnet processes.

Problem: Loop 17 left truthful but weak architecture: active call sites bypassed the shadow helper, yet `ShinobuEcosystemBalancer.cs` still contained duplicate local pack/FNV/mm-quantization code that could be reused by a future edit.
Solution: When the gate briefly opened at CPU 46.5% with no compiler process, removed local `PackFaunaGeneticMask`, `FoldFnv32`, and `QuantizeMetersToMillimeters`; retained only thin `BuildFaunaAupSeed`, `BuildFaunaStableEntitySeed`, and `CompileFaunaGeneticMaskFromSeed` delegates to `Hecton8.Ecosystem.FaunaGenome64`; kept active call sites fully qualified to the same owner.
Rejected Alternatives: Leaving duplicate helper code as "equivalent" was rejected because equivalent code becomes divergent code after the next bit-layout edit. Running `dotnet build` after the source scan was rejected because Unity/Roslyn immediately raised `dotnet` PID 11792 and `csc` PID 15984 with CPU 81.9%.
Scalability potential: Low/Middle/High/Ultra all keep one authoritative `ulong` truth; `GlobalQualityWeight` changes only shader expression, not mask ownership or DTO layout.
Hardware Impact: Runtime microseconds saved: 0 measured. Architectural gain is removal of one duplicate integer compiler and prevention of shader/headless divergence; rebuild proof is pending behind the active compiler gate.

Problem: The post-Loop-18 validation pass caught a new external rewrite: call sites returned to `ShinobuEcosystemBalancer.BuildFauna*`, and the helper body diverged from `FaunaGenome64` by hashing raw `math.asuint(aup.Local*)` floats and composing masks from random low/high uints instead of the declared bit layout.
Solution: Treat direct call-site ownership as unstable under the active Unity/background writer and make the wrapper body the hard boundary: all `BuildFauna*` helpers now delegate directly to `Hecton8.Ecosystem.FaunaGenome64`. Ten-second delayed scan verified the delegates remained and the raw float-bit helper did not return.
Rejected Alternatives: Fighting the call-site rewrite indefinitely was rejected because a thin delegate preserves one authoritative compiler while avoiding merge churn. Accepting the raw float-bit helper was rejected because it loses millimeter-quantized AUP seed semantics and bypasses the declared size/speed/aggression/hue/pattern/biolum bit layout.
Scalability potential: No tier split. The wrapper route still yields one authoritative `ulong` mask; shader `GlobalQualityWeight` controls only presentation richness.
Hardware Impact: Runtime microseconds saved: 0 measured. Correctness gain is deterministic AUP seed and shader ABI preservation. Rebuild proof remains blocked by CPU 100%.

Problem: A later Loop 20 mandate re-read proved the batch prompt expects the active fauna route to stay mathematically authoritative at the call-site level too; the same writer drift restored divergent wrapper math again.
Solution: Patch both defenses instead of choosing one. The four active SHINOBU genetics call sites now call `Hecton8.Ecosystem.FaunaGenome64` directly, and the residual `BuildFauna*` methods are pure delegates to the same owner if a future writer moves call sites back.
Rejected Alternatives: Wrapper-only was rejected because writer drift can hide a divergent helper behind normal-looking call sites. Direct-only was rejected because writer drift can move call sites back to wrappers. Keeping both direct call sites and delegate wrappers gives the current file two local barriers without adding a new owner.
Scalability potential: Low/Middle/High/Ultra still consume one authoritative `ulong`; quality only changes shader expression and telemetry/proof richness.
Hardware Impact: Runtime microseconds saved: 0 measured. Risk reduction is prevention of raw-float AUP hash and random low/high mask regressions. Build proof remains blocked by active `dotnet`/`csc` and CPU 100%.

Problem: The writer drift was independently reproduced by Tesla, and another agent overwrote `RENDERING_OPTIMIZATION_REPORT.json`, removing SHINOBU_306 evidence sections. The existing editor scanner counted material/random residue but did not detect the compiler drift class.
Solution: Re-patched direct call sites plus wrappers, extended `OOP_Variant_Scanner` with compiler-drift counters (`math.asuint(aup.Local*)`, random low/high mask composition, local pack helpers, wrapper delegates, direct `FaunaGenome64` calls), and restored SHINOBU_306 report sections through JSON upsert without deleting the new SHINOBU_325 section.
Rejected Alternatives: Relying on chat/subagent warnings was rejected because the CTO reads files, not chat. Making `ShinobuEcosystemBalancer.cs` read-only was rejected because it would sabotage Unity/editor workflows and other agents.
Scalability potential: The scanner has no runtime tier effect. It protects the invariant that all tiers consume the same authoritative `ulong` mask while shader quality scales presentation only.
Hardware Impact: Runtime microseconds saved: 0. Editor scanner overhead is cold/editor-only. Build proof remains blocked by active `VBCSCompiler.exe`.

Problem: The concurrent Unity `dotnet` writer reverted `ShinobuEcosystemBalancer.cs` again after Loop 21. Current source is wrapper-routed through a divergent raw-float AUP helper and random low/high mask composer.
Solution: Stop claiming the current source route is green. Add `FaunaGeneticsMaskBuildGuard` as an editor prebuild guard so this drift blocks builds instead of silently passing. Mark Task 02 and Task 20 as blocked by concurrent writer until Unity releases the file and the authoritative `FaunaGenome64` route can stick.
Rejected Alternatives: Continuing to re-patch every 10-15 seconds was rejected because it creates false evidence while Unity keeps saving stale source. Killing Unity/dotnet or making the file read-only was rejected because that can corrupt editor state or sabotage other agents.
Scalability potential: No runtime tier effect. The guard preserves the invariant that quality scaling remains presentation-only by refusing drifted mask compilers.
Hardware Impact: Runtime microseconds saved: 0. Build safety gained: current drift cannot pass a player build through the editor prebuild path.
