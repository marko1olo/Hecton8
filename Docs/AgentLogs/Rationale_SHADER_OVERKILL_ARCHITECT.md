# Rationale_SHADER_OVERKILL_ARCHITECT

Status: SHADERS CRYSTALLIZED / VISUAL ORGASM READY - COMPILE BLOCKED BY EXTERNAL WORLD-GPR DEPENDENCY
Agent: SHADER_OVERKILL_ARCHITECT

## Decision 001 - Active Dependency Logs Missing
Problem: The prompt mandates reading `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md`, but both active files are absent.
Solution: Record the absence as an evidence gap, inspect current shader/C# implementation sources, and avoid claiming inherited proof from missing logs.
Rejected Alternatives: Reading archived batch logs as authority was rejected because current AGENTS hygiene forbids stale-batch log use unless explicitly current; treating absent logs as read was rejected as fake reporting.
Scalability potential: Low/Middle/High/Ultra remain shader-tier driven; missing logs do not block implementing tier gates, but they block claiming historical runtime proof.
Hardware Impact: Estimated runtime gain from this decision is 0 us; it prevents incorrect dependency assumptions on i3/MX350.

## Decision 002 - Mandate Set
Problem: The shader library crosses SRP batching, AUP precision, Resident Drawer, GraphicsBuffer instance data, dither transparency, caustics, and zero-GC C# property IDs.
Solution: Read `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `REND_DescriptorBinding_Reality_Check`, `REND_GPU_Sovereignty`, `MATH_AUP_Determinism_Sync`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, and `OPT_Zero_GC_Policy_AllocFree_Mandate`.
Rejected Alternatives: Generic URP shader implementation was rejected because the project requires specific AUP and SRP/Resident Drawer constraints; adding a render pass was rejected because the prompt asks for shader core library first.
Scalability potential: Low disables POM/caustics/bending; Middle enables cheaper caustic/detail; High enables POM/caustics/bending; Ultra increases visual overkill through stricter samples and richer emission without changing CPU path.
Hardware Impact: Estimated low-end gain versus fragmented shader/material path is 30-120 us CPU SetPass overhead pending Frame Debugger proof; shader GPU cost remains tier-gated.

## Decision 003 - Uber Library Instead Of Fragmented Shader Passes
Problem: Caustics, rust, deformation, fog cutout, and emission were requested as one shader core to reduce pass and SetPass fragmentation.
Solution: Created `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl` as a single URP-compatible HLSL library with feature branches and `_MATH_LOD_LOW` stripping. All material parameters live in one `UnityPerMaterial` CBUFFER.
Rejected Alternatives: Separate rust/deformation/caustics shaders were rejected because they multiply SetPass calls; runtime material mutation was rejected because it damages SRP Batcher and Resident Drawer behavior.
Scalability potential: Low returns albedo/roughness with no POM, caustics, bending, or biolum. Middle enables cheap surface detail. High enables caustics and deformation. Ultra/GOD spends saved CPU budget on POM, spectral emission, and denser fake lighting.
Hardware Impact: Estimated i3/MX350 gain is 30-120 us CPU by avoiding pass churn; GPU load on low silicon is reduced by stripping the heavy branches.

## Decision 004 - AUP Before Matrix World Position
Problem: At 100 km scale, computing world position before applying the universe offset preserves float jitter in the matrix multiply.
Solution: Subtract `_TotalUniverseOffset.xyz` from the instance/object matrix translation before multiplying object-space position.
Rejected Alternatives: Subtracting after world position calculation was rejected because it cleans the final value but not the precision loss inside the multiply.
Scalability potential: Low through Ultra use the same deterministic coordinate baseline; higher visual tiers do not buy precision with unstable world coordinates.
Hardware Impact: Cost is three subtracts per vertex; estimated 0-5 us GPU per dense visible batch. Visual gain is stable sub-pixel hull/detail positioning on low-end and high-end hardware.

## Decision 005 - GraphicsBuffer Path For Resident Drawing
Problem: Per-renderer material values and CPU property writes fragment batches and create GC/driver pressure.
Solution: Added `StructuredBuffer<H8UberNoirInstanceData>` carrying object/world matrices plus seed/fade/flags, gated by `H8_UBERNOIR_USE_INSTANCE_BUFFER`.
Rejected Alternatives: MaterialPropertyBlock was rejected as a hot-path batch breaker; direct dependency on a future drawer class was rejected because 20+ agents are working in parallel.
Scalability potential: Low devices use the same binding path with cheaper material math. Ultra devices can push more resident instances and per-instance seed variation without material clones.
Hardware Impact: Estimated i3/MX350 benefit is 20-80 us CPU in dense resident draws, pending Profiler proof.

## Decision 006 - Fake-First Caustics, Bending, Rust, And Biolum
Problem: Physically correct refraction, corrosion growth, hull deformation, and spectral emission would exceed the 0.1 ms suspicion threshold.
Solution: Use controlled cinematic cheats: analytical caustic waves with optional lookup texture, shader vertex bowing from stress fields, 16-tap rust POM only in high tiers, and phase-driven spectral emission.
Rejected Alternatives: Screen-space fluid/refraction simulation, CPU mesh deformation, decal stacks, and script-animated emission were rejected as slower and less predictable.
Scalability potential: Low disables all overkill. Middle can retain tint/detail. High/Ultra can spend ALU/texture budget where the camera sees it.
Hardware Impact: Low-end avoids 80-500 us GPU pressure from heavy material branches; Ultra intentionally spends those cycles on visible surface richness.

## Decision 007 - NaN And Texture Stall Discipline
Problem: High-contrast lighting and POM can poison the frame with NaNs or repeated texture stalls if raw math and repeated ORM fetches are used.
Solution: Wrapped all `pow()`/`rsqrt()` calls in safe helpers and sampled `_MaskMap` exactly once before distributing metallic/occlusion/smoothness.
Rejected Alternatives: Raw HLSL intrinsics and separate M/R/O texture samples were rejected as fragile and bandwidth-wasteful.
Scalability potential: Low uses the single packed ORM sample and avoids POM. Ultra keeps the packed ORM discipline while adding rust-detail taps only where the tier allows it.
Hardware Impact: Estimated i3/MX350 savings from packed ORM discipline is 10-60 us GPU in material-heavy views; NaN guards are correctness insurance, not a measurable win.

## Decision 008 - Compile Verification Boundary
Problem: Unity batchmode could not complete because the project has existing `GroundPenetratingRadarRuntime.cs` compile errors in the World/GPR domain.
Solution: Ran Unity 6000.4.1f1 batchmode, scanned the log for owned shader/C# names and shader errors, and marked Task 15 as blocked by dependency rather than editing another domain.
Rejected Alternatives: Fixing World/GPR from the rendering task was rejected as domain breach; claiming a clean compile was rejected as fake reporting.
Scalability potential: None until the external compile blocker is removed. The shader remains tiered and static-audited.
Hardware Impact: 0 us direct gain. This prevents introducing unverified cross-domain edits on low-end targets.

## Decision 009 - Polish Mandate Missing
Problem: The required post-core `<POLISH_MANDATE>` tag is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Mark the polish phase as blocked by batch-file input, record the absence, and keep the implemented shader under static anti-bloat checks instead of inventing a hidden mandate.
Rejected Alternatives: Parsing unrelated neighboring batch content or fabricating polish requirements was rejected as prompt contamination.
Scalability potential: Existing Low/Middle/High/Ultra gates remain intact; no additional polish commands can be executed without the missing tag.
Hardware Impact: 0 us runtime gain. The decision prevents unscoped churn.

## Decision 010 - Low-Tier UberNoir Cost Collapse
Problem: `_MATH_LOD_LOW` still paid for normal-map sampling, shadow-coordinate setup, view half-vector/specular math, and unconditional blue-noise evaluation even though the low path only needs base albedo, packed roughness/occlusion, ambient, and main diffuse.
Solution: Moved low-tier surface sampling into an early base+ORM-only path, changed low-tier lighting to `GetMainLight()` without shadow coordinates/specular/caustics, and gated blue-noise dither out of `_MATH_LOD_LOW`.
Rejected Alternatives: Keeping a common fragment body was rejected because it hid discarded work on MX350. Full material split was rejected because it reintroduces SetPass/material fragmentation.
Scalability potential: Low/TOASTER now buys stability with fewer samples and less ALU. High/Ultra retain normal maps, rust POM, caustics, spectral emission, and exact normals.
Hardware Impact: Estimated low-end gain is 30-200 us GPU across dense material views, pending Unity/Profiler proof after the external compile blocker is removed.

## Decision 011 - Rust And Caustic Texture Stall Gates
Problem: Clean materials sampled `_RustDetailMap` before proving rust was active, and all non-low variants compiled optional caustic texture sampling.
Solution: Added a rust scalar early-out before rust detail sampling and wrapped `_HectonCausticsMap` sampling behind `H8_UBERNOIR_CAUSTICS_TEXTURED`.
Rejected Alternatives: Always sampling rust/caustics and trusting runtime multipliers was rejected because zero contribution still consumes texture bandwidth and contributes to variant pressure.
Scalability potential: Low skips both paths. Middle/High can run procedural caustics only. Ultra/GOD can opt into texture caustics and 16-tap rust POM when visual payoff is visible.
Hardware Impact: Estimated MX350 gain is 10-90 us on clean materials and 5-40 us where procedural caustics replace a texture lookup, pending GPU capture.

## Decision 012 - Materials Asmdef H-Phi Cleanup
Problem: `Hecton8.Graphics.Materials.asmdef` referenced `Hecton8.World.Contracts`, but the current materials code uses only `Hecton8.Core`, `Hecton8.Core.Signals`, and Unity shader APIs.
Solution: Removed the unused World contract reference to reduce false domain coupling.
Rejected Alternatives: Leaving the reference "just in case" was rejected because H-Phi tracks architectural coupling as debt, and future world dependencies should enter through explicit signals/contracts.
Scalability potential: Runtime path unchanged; compile graph remains narrower for rendering materials.
Hardware Impact: 0 us runtime. Static H-Phi audit after the no-rebuild pass reported `RuntimeHPhiNarrow=0.010534799` and `RuntimeHPhiRisk=0.000573240`.

## Decision 013 - Low-Tier Caustic Publisher Gate
Problem: Low-tier caustic compute resources were correctly released, but `AnalyticalCausticsService` could still publish nonzero `_HectonProjectedCausticsParams.x`, allowing procedural caustics in CoreLit consumers that do not compile the UberNoir low variant.
Solution: Thread the `lowTier` decision into `PublishShaderGlobals` and force caustic intensity to zero when low tier or disabled depth is active.
Rejected Alternatives: Shader-only stripping was rejected because global shader parameters are shared by multiple consumers; leaving intensity active after resource release was rejected as an invisible low-tier GPU tax.
Scalability potential: Low/TOASTER gets no caustic ALU or texture pressure from global consumers. Middle/High/Ultra retain analytical caustics when budget and scene conditions justify them.
Hardware Impact: Estimated i3/MX350 gain is 15-80 us GPU in water/hull views with caustic receivers, pending GPU capture after the external compile blocker is removed.

## Decision 014 - Binary Layout And Native Lifetime Hardening
Problem: Caustic GPU upload data, black-box telemetry entries, and the AUP culling job payload were structurally important but relied on implicit managed layout, and disposed NativeArray scratch handles were not reset to default.
Solution: Added explicit sequential pack/size to caustic GPU and telemetry structs, explicit sequential layout to the AUP shift job payload, and default-reset disposed NativeArray fields after release.
Rejected Alternatives: Relying on C# default layout was rejected because H-Phi rewards binary-safe rendering code and GPU upload payloads must be deterministic; allocation churn was rejected in favor of keeping existing persistent NativeArrays.
Scalability potential: Low through Ultra share the same predictable upload and black-box memory model. Higher tiers can push more waves/instances without adding managed allocations or layout ambiguity.
Hardware Impact: Runtime microsecond gain is 0 us claimed. The benefit is lower integration risk and cleaner H-Phi memory-alignment evidence; latest no-rebuild audit reports `MemoryAlignment=0.504761905` and `AupPrecisionRisk=0`.

## Decision 015 - Instance Buffer Zero-Count Guard
Problem: The UberNoir resident-drawer path could read `_H8UberNoirInstanceData[bufferOffset]` when the instance buffer keyword was compiled but `_UberNoirInstanceParams.y` was zero or `_UberNoirInstanceParams.z` disabled the path.
Solution: Build a default Unity matrix instance first, then only index the `StructuredBuffer` when the use flag is set and the declared count is positive.
Rejected Alternatives: Assuming the drawer always binds a non-empty buffer was rejected because disabled variants, editor import, and fallback materials must not perform undefined GPU reads.
Scalability potential: Low/Middle fallback materials render safely without a resident buffer. High/Ultra resident batches keep the same fast path when the buffer is valid.
Hardware Impact: Estimated cost is 0-2 us vertex-side branch overhead in fallback cases; benefit is avoiding undefined GPU memory reads and hard-to-triage platform instability on Vulkan/Metal/DX12.

## Decision 016 - No-Rebuild H-Phi Evidence Boundary
Problem: The user explicitly forbade dotnet rebuilds, but the final shader safety patch changed source state after the previous H-Phi reading.
Solution: Re-ran only the static `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` audit with a longer timeout after the first 120-second attempt expired.
Rejected Alternatives: Running `dotnet build` or Unity compilation was rejected because it violates the direct user order and remains blocked by external World/GPR compile errors.
Scalability potential: Static audit confirms `AupPrecisionRisk=0`, `UnityUpdateMethods=2`, and increased `StructLayoutAttributes=953`; runtime scalability still requires profiler capture after the external compile blocker is cleared.
Hardware Impact: 0 us runtime. Evidence quality improved; latest static audit reports `RuntimeHPhiNarrow=0.010750800`, `RuntimeHPhiRisk=0.000587147`, and `MemoryAlignment=0.504761905`.

## Decision 017 - Dynamic Resolution Hysteresis And Asset Mutation Guard
Problem: `ThermalDynamicResolutionAdapter` could react to a single pressure frame and mutate the active URP asset `renderScale`/`upscalingFilter` as a fallback, creating state flicker and ScriptableObject/project-setting dirtiness risk.
Solution: Added a 3-frame pressure hysteresis and 15-frame recovery hysteresis, packed counters into existing telemetry reserve bits, removed runtime upscaling-filter mutation, and changed the fallback path to resize scalable buffers without writing URP asset fields.
Rejected Alternatives: Leaving instant scale flips was rejected by the state hysteresis mandate. Mutating the URP asset in code was rejected because the project requires authored pipeline assets and no runtime project-setting drift.
Scalability potential: Low/MX350 sheds resolution only after sustained pressure and recovers slowly. High/Ultra keep full scale unless real sustained pressure appears, preserving visual stability.
Hardware Impact: Estimated gain is 5-40 us of avoided render-state churn/jitter during unstable pressure windows; measured proof absent until profiler capture.

## Decision 018 - Global Flora Tint Finite And Play-Mode Guard
Problem: Serialized flora tint/strength values could become non-finite and then be published as global shader vectors; the bridge could also attempt runtime registry registration outside play mode.
Solution: Sanitize tint and strength before `Shader.SetGlobalVector`, preserve the cached-change early-out, and guard registry registration with `Application.isPlaying`.
Rejected Alternatives: Trusting inspector data was rejected because rendering globals feed many materials and NaN propagation is a project-level failure mode. Edit-mode registry side effects were rejected because shader preview globals do not require runtime tick ownership.
Scalability potential: All tiers receive stable globals; the visual tint fake remains cheap on low hardware and usable as biome richness on high hardware.
Hardware Impact: 0 us speed claimed. Correctness gain is preventing shader global poisoning and edit-mode runtime-registry noise.

## Decision 019 - Post-Follow-Up H-Phi Reverification
Problem: The DRS/flora safety pass changed rendering source state after the previous H-Phi measurement, while the user explicitly forbade dotnet rebuilds.
Solution: Reran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` with a 300s timeout and recorded the latest static-source metrics.
Rejected Alternatives: Running `dotnet build`, `dotnet rebuild`, or a Unity compilation was rejected because it violates the direct user order and remains blocked by external World/GPR compile errors. Reusing stale H-Phi numbers was rejected as fake evidence.
Scalability potential: Low-tier rendering now keeps hysteretic DRS behavior and finite shader globals; High/Ultra keep the overkill path without one-frame scale oscillation or shader global NaN risk.
Hardware Impact: 0 us runtime claimed from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010750800`, `RuntimeHPhiRisk=0.000587147`, `AllSourceHPhiNarrow=0.009572479`, `AllSourceHPhiRisk=0.000482295`, `ArchitecturalPurity=0.996447602`, `MemoryAlignment=0.504761905`, `StructLayoutAttributes=954`, `AupPrecisionRisk=0`.

## Decision 020 - Underwater Visuals Component Lookup Hygiene
Problem: `HectonUnderwaterVisuals` carried runtime `GetComponent<T>` and `GetComponentInParent<T>` lookup debt in camera recovery paths, increasing H-Phi static coupling risk and violating the preferred `TryGetComponent` pattern.
Solution: Replaced runtime camera/component probes with `TryGetComponent(out T)` and added a zero-allocation parent `Transform` walk that preserves first-parent-camera semantics without `GetComponentInParent<T>`.
Rejected Alternatives: A full camera-stack rewrite was rejected because this presentation hub owns Crest fallback, editor preview, gameplay camera composition, and underwater ownership. Editing Crest wrappers or moving camera ownership to another domain was rejected as higher-risk cross-domain churn.
Scalability potential: Low/MX350 avoids extra Unity lookup debt during cold camera recovery and keeps the same visual ownership path. High/Ultra remain behavior-equivalent while cleaner static coupling leaves room for later RenderGraph/camera-stack work.
Hardware Impact: Runtime speed gain is estimated at 0-5 us on rare camera recovery frames, not hot path. Static H-Phi evidence after the pass: `GetComponentCalls=532`, `MemoryAlignment=0.505023797`, `AupPrecisionRisk=0`.
