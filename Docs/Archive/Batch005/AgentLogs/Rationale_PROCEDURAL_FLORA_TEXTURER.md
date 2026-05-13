# Rationale: PROCEDURAL_FLORA_TEXTURER

## 2026-05-13 Initial Scope

Problem: Procedural L-System flora meshes lack reliable UVs; runtime UV unwrap/recalculation would spend CPU time on a presentation problem.
Solution: Build a world-space triplanar PBR shader using shared Albedo/Normal/ORM atlases, vertex color R for cheap root-tip SSS/AO illusion, and shader global biome tint.
Rejected Alternatives: Runtime mesh UV generation, per-instance material mutation, and physics-backed flora variance. Standard Unity mesh UV rebuild is too slow, alloc-prone, and unnecessary for a visual surface projection problem.
Scalability potential: Low uses MatCap fake with no triplanar texture samples. Middle uses triplanar atlas projection. High adds seeded breakup and normal detail. Ultra can spend saved CPU/GPU budget on denser flora and stronger biolum emission.
Hardware Impact: i3/MX350 gains come from avoiding runtime UV generation and material clones; expected CPU savings are task-dependent and remain PENDING VERIFICATION until profiler capture. Shader cost is bounded by Math LOD.

Problem: Batch requests `_HectonFloatingOffset` subtraction, while the AUP mandate prefers baked absolute coordinates/UV3 for triplanar stability.
Solution: Implement the batch-required `_HectonFloatingOffset` path in the shader and record the UV3-bake path as the rejected higher-effort alternative for this pass.
Rejected Alternatives: Ignoring origin offset or changing procedural mesh contracts in another agent's domain. Both would break domain boundaries.
Scalability potential: Low tier can bypass world triplanar entirely. High and Ultra tiers keep stable projection through explicit origin compensation.
Hardware Impact: Offset subtraction is one vector subtract in vertex/fragment data flow; estimated cost below measurement noise, PENDING VERIFICATION.

Problem: Flora biome tint needs to react to narrative biome changes without direct references to narrative, atmosphere, or renderer owners.
Solution: Add a graphics-material bridge that consumes `BiomeChangedSignal` from the typed SignalBus and writes stable shader globals. It emits no events and does not mutate materials.
Rejected Alternatives: Direct subscription to narrative systems, static C# events, or `Renderer.material` writes. Those create hard dependencies, hidden allocations, or SRP Batcher breaks.
Scalability potential: Low tier keeps the same global tint with zero per-renderer data. Middle/High/Ultra can drive stronger biome coloration through material strength without new CPU work.
Hardware Impact: Idle cost is one empty `ReadOnlySpan` check per tick; signal-frame cost is proportional to biome signal count. Exact microseconds are PENDING VERIFICATION.

Problem: Batch asks for `Hecton8.Graphics.Materials` -> Contracts, but `SignalBus<T>` and `BiomeChangedSignal` currently live in `Hecton8.Core`, not `Hecton8.Core.Contracts`.
Solution: Create the materials asmdef with Contracts references and a direct Core reference only for the existing SignalBus lane. No public contract migration was attempted.
Rejected Alternatives: Moving signal definitions into Contracts or duplicating signal structs locally. Both would risk cross-agent compile damage and type-lane desync.
Scalability potential: The asmdef creates a narrow place to move to Contracts later when signal lanes are owned there.
Hardware Impact: Assembly boundary has no runtime frame cost.

Problem: Full triplanar projection costs texture samples; runtime UV unwrap costs CPU and allocations.
Solution: Use shader triplanar projection for Albedo/Normal/ORM and a `_MATH_LOD_LOW` MatCap branch that bypasses triplanar entirely on MX350/i3 style tiers.
Rejected Alternatives: Runtime UV recalculation, per-mesh unwrap, or extra procedural texture generation. These spend CPU and memory on a surface-detail problem.
Scalability potential: Low = MatCap fake, Middle = triplanar atlas, High = sharper blends and seeded breakup, Ultra = saved CPU budget buys denser flora and stronger biolum pass.
Hardware Impact: High path is 9 atlas samples per shaded pixel. Low path is one MatCap sample. Exact GPU microseconds are PENDING VERIFICATION.

Problem: 10,000 instances sharing the same mesh and atlas will visibly tile unless every instance gets deterministic macro-variance.
Solution: Read matrix `m31` as requested when available, hash object origin as fallback, and use the seed to offset all triplanar UV axes.
Rejected Alternatives: MaterialPropertyBlock seed, per-renderer material clone, or CPU-side mesh UV noise. These break batching, add allocations, or push visual variance into CPU geometry.
Scalability potential: Low bypasses triplanar offsets. Middle/High/Ultra use the same atlas with progressively richer material response, so visual variety scales without extra textures.
Hardware Impact: Seed read is vertex-stage. Hash/UV offset ALU is fragment-stage only outside `_MATH_LOD_LOW`; exact microseconds are PENDING VERIFICATION.

Problem: Generated flora needs synchronized bioluminescence without becoming another clock owner.
Solution: Use `_BiolumMasterPhase.y` and `_BiolumIntensity.x` as shader globals, with ORM alpha and vertex height as cheap masks.
Rejected Alternatives: Local script timers, per-object emission animation, or particle-driven glow. Those add CPU ownership and drift from the global biolum director.
Scalability potential: Low keeps one cheap pulse. High/Ultra can raise material emission and density using the same global phase.
Hardware Impact: Fragment pulse math is small but unprofiled; runtime result remains PENDING VERIFICATION.

Problem: Low-end hardware cannot pay full triplanar sample cost for dense generated flora.
Solution: Add `_MATH_LOD_LOW` branch using a single MatCap sample, while non-low tiers keep the triplanar atlas projection.
Rejected Alternatives: Reduce texture resolution only, or keep full triplanar on all tiers. Resolution alone does not remove ALU/sampler pressure; full triplanar on MX350 is not defensible without capture.
Scalability potential: Low = one MatCap fake. Middle = full triplanar. High = sharpened blending. Ultra = same shader budget can be spent on more dense flora and stronger emission.
Hardware Impact: The low branch cuts 9 atlas samples to 1 sample per shaded pixel. Exact microseconds saved are PENDING VERIFICATION.

Problem: Presentation code can accidentally become an event source and couple graphics to gameplay.
Solution: The bridge consumes `BiomeChangedSignal` and writes shader globals only; it emits no EventBus/SignalBus messages.
Rejected Alternatives: Visual tint changed signals or gameplay callbacks. No consumer needs them for a shader tint.
Scalability potential: One global tint scales to all flora renderers without per-renderer data.
Hardware Impact: No event emission overhead; read-side cost remains PENDING VERIFICATION.

Problem: Prompt says HLOD should push `m31` seed, but current HLOD culling uses `m31` as packed bounds radius.
Solution: Leave HLOD intact, mark Task 17 as dependency-blocked, and keep the shader seed fallback from object origin when `m31` is unavailable or reserved by culling.
Rejected Alternatives: Change `InstanceCulling.compute` to treat `m31` as seed or reinterpret radius as seed. That would corrupt culling bounds and cause invisible or overdrawn instances.
Scalability potential: Correct fix is a shared packing contract, for example separate radius and seed channels or a documented packed float format owned by HLOD/BRG.
Hardware Impact: No runtime change made. Avoided a culling regression that would cost either missing visuals or excess overdraw on MX350.

Problem: SRP Batcher and dependent texture read risk must be verified without hiding the current project compile wall.
Solution: Keep all material properties inside `UnityPerMaterial`, inspect texture reads, read shader compiler logs, and report global compile blockage as external.
Rejected Alternatives: Ignore the compile wall or edit unrelated `HectonFluidEngine`/UI Tools dependencies. That would violate domain boundaries.
Scalability potential: Low tier avoids all triplanar dependent reads; higher tiers accept them for visual richness until GPU capture proves otherwise.
Hardware Impact: Shader compiler success is proven. Frame cost and dependent read stalls are PENDING VERIFICATION until a real GPU frame capture.

Problem: Recursive audit required normal map sampled three times and UDN blending, with rsqrt optimization if needed.
Solution: Confirmed X/Y/Z normal atlas samples and UDN-style composition, then replaced hot triplanar normal normalizations with an explicit `rsqrt` helper.
Rejected Alternatives: Leave `SafeNormalize` everywhere or remove UDN composition. Generic normalize is less explicit for this mandated audit; removing UDN would violate the prompt.
Scalability potential: Low tier bypasses this path. Middle/High/Ultra retain higher-quality normal response with bounded ALU.
Hardware Impact: Shader compiler accepts the rsqrt path. Actual ALU savings remain PENDING VERIFICATION without GPU capture.

## OMEGA POLISH CHANGES

Problem: Shader used honest shaping math where visual fakery is sufficient.
Solution: Replaced dynamic `pow` shaping in blend/emission paths with multiply/lerp shaping, replaced blend divisions with `rcp`, and replaced remaining local shader normalizations with explicit `rsqrt`.
Rejected Alternatives: Preserve exact artist exponent curves. The prompt prioritized cheap cinematic lies over honest math; material controls still preserve visual range.
Scalability potential: Low tier still bypasses triplanar. High/Ultra keep sharpened blends without variable exponent `pow`.
Hardware Impact: Exact microseconds saved are PENDING VERIFICATION. Static cost removed: variable `pow` in emission, variable `pow` in high-tier blend, two blend divisions, and local normalize calls.

Problem: Managed bridge could hide GC or string bloat.
Solution: Audited the bridge for `foreach`, `string.Format`, interpolated strings, and `.ToString()`; none are present. Hot path uses indexed span loop and struct shader globals.
Rejected Alternatives: Add logging for state transitions. Logs would add string work and are unnecessary for presentation tint.
Scalability potential: One bridge instance can feed global shader state for all flora without per-renderer data.
Hardware Impact: Expected managed allocation remains 0 B/frame; profiler proof is PENDING VERIFICATION.

Problem: Polish required `dotnet build Hecton8.Core.csproj`, but the current core project is red from unrelated assembly/reference migration work.
Solution: Ran the command and recorded the result as an external compile dependency. Unity shader import and `validate_script` for the new bridge are clean.
Rejected Alternatives: Edit unrelated Core, Fluid, Audio, Inventory, Physics, or World files. That would violate the assigned graphics/materials domain.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; build health remains blocked outside this task.

## AUP CONTRACT HARDENING

Problem: The prompt mandates subtracting `_HectonFloatingOffset`, but the live project currently publishes `_TotalUniverseOffset` and `_HectonFloatingOriginOffset`; leaving the shader on `_HectonFloatingOffset` alone would make the origin-safe path inert in current scenes.
Solution: Keep the prompt path intact for any future explicit `_HectonFloatingOffset` publisher, but fallback to `_TotalUniverseOffset` absolute-space projection when `_HectonFloatingOffset` is zero or absent. This matches existing Hecton core shaders that add `_TotalUniverseOffset` for AUP-stable visual phase.
Rejected Alternatives: Add a new C# global publisher, duplicate floating-origin ownership in the materials bridge, or ignore the mismatch. A new publisher would cross domain ownership and risk fighting `HectonFloatingOrigin`; ignoring the mismatch would make the shader pass syntax checks while failing live origin-shift behavior.
Scalability potential: Low tier still bypasses triplanar texture projection. Middle/High/Ultra get stable world projection through the same shader without per-renderer state, and future high-end density does not create extra C# work.
Hardware Impact: Adds a small finite check, dot, lerp, and vector add/subtract in the projection helper. Exact microseconds remain PENDING VERIFICATION; cost should be below texture-sampling pressure and is traded for avoiding visible texture sliding on origin shifts.

## LOOP 7 HARDENING

Problem: `ProceduralFloraBiomeTintBridge` reset shader globals on disable but kept `_lastBiomeHash`; after re-enable, the next signal for the same biome could be skipped and leave default tint active.
Solution: Reset `_lastBiomeHash` to `uint.MaxValue` on enable and disable so the first subsequent `BiomeChangedSignal` always republishes the resolved tint.
Rejected Alternatives: Poll current biome state or force-publish every frame. Polling would add a new cross-domain dependency; every-frame global writes would waste CPU and churn render globals for a presentation tint.
Scalability potential: Low/Middle/High/Ultra all keep one global tint write only on actual lifecycle or biome state changes.
Hardware Impact: 0.000 us steady-state added; one integer assignment on enable/disable only. Avoids a visual stale-state bug without hot-path work.

Problem: Generated L-System meshes can arrive with missing, zero, or non-finite normals; the triplanar blend would then receive zero weights and produce black surfaces in the PBR path.
Solution: Add vertex-stage finite/nonzero normal guards in both ForwardLit and ShadowCaster passes, falling back to an up-axis normal before world transform and shadow bias.
Rejected Alternatives: Runtime mesh normal recalculation or CPU-side mesh cleanup. Those violate the shader-only/no-runtime-UV-rebuild intent and add CPU/GC risk for a condition the shader can safely degrade.
Scalability potential: Low tier MatCap and full PBR tiers both get deterministic fallback normals. High/Ultra retain proper authored normals when present; bad generated assets degrade visibly but predictably.
Hardware Impact: Adds a small vertex-stage finite check and dot comparison. Expected cost is below fragment texture pressure and prevents full-pixel black failure on degenerate procedural meshes.

## LOOP 8 SHADOW INTEGRATION

Problem: The procedural flora shader compiled main-light shadow variants but the PBR path used `GetMainLight()` without shadow coordinates, so full-tier generated flora ignored authored main-light shadows.
Solution: Use `TransformWorldToShadowCoord(input.positionWS)` and `GetMainLight(shadowCoord)` only outside `_MATH_LOD_LOW`, then apply `distanceAttenuation * shadowAttenuation` to direct diffuse, specular, and subsurface terms.
Rejected Alternatives: Add shadows to the low-tier MatCap path or leave all tiers unshadowed like older kelp/coral variants. Low tier should not pay the shadow-coordinate/sample cost; leaving high tier unshadowed fails the AAA visual target when dense flora intersects terrain, wrecks, or player lights.
Scalability potential: Low remains cheap and unshadowed. Middle/High/Ultra buy contact and cast-shadow readability with the existing main-light shadow map, while ambient, rim, and biolum keep silhouettes readable in deep-sea darkness.
Hardware Impact: Adds one shadow-coordinate transform and main-light shadow sample in non-low variants only. Exact GPU microseconds remain PENDING VERIFICATION; the cost is gated away from MX350 low tier.

## LOOP 9 LOW-TIER AND REGISTRATION HARDENING

Problem: `_MATH_LOD_LOW` bypassed triplanar texture sampling in the fragment shader, but the vertex shader still computed AUP projection and per-instance seed data that the low variant does not need.
Solution: Gate vertex outputs by `_MATH_LOD_LOW`: low tier writes raw `positionWS` to `projectWS` and zeroes `seed`, while non-low retains AUP projection and seed resolution.
Rejected Alternatives: Leave low-tier hidden ALU intact or remove emission hash entirely. Hidden ALU violates the low-tier mandate; removing emission hash would flatten the cheap biolum look more than necessary.
Scalability potential: Low reduces vertex setup work. Middle/High/Ultra keep stable projection and seed variation for visual richness.
Hardware Impact: Low tier avoids projection finite checks, offset selection, object matrix seed read, and fallback object-origin hash per vertex. Exact microseconds remain PENDING VERIFICATION.

Problem: The tint bridge tried dispatcher registration only in `OnEnable`; if boot order made `GlobalRegistry.TryRegisterUpdatable` fail before the dispatcher was ready, biome signals would never be consumed until another enable cycle.
Solution: Add a shared `TryRegisterTick()` and call it from both `OnEnable()` and `Start()`, preserving zero per-frame polling while covering normal Unity boot order.
Rejected Alternatives: Add `Update()` retry or poll biome state directly. `Update()` would violate dispatcher ownership and add a Unity loop; direct biome polling would introduce a cross-domain dependency.
Scalability potential: One bridge still feeds global shader state for all flora. No per-renderer data, no material mutation, no recurring registration scan.
Hardware Impact: 0.000 us steady-state added. One extra cold `TryRegisterUpdatable` attempt during startup if needed.

## LOOP 10 LOW-TIER EMISSION HASH REMOVAL

Problem: `_MATH_LOD_LOW` no longer needed triplanar projection, but its emission still consumed `projectWS` and ran a world-space hash per fragment.
Solution: Add `ResolveProceduralBioEmissionLow()` using the global biolum phase/intensity only, set low-tier `projectWS` to zero, and keep spatial hash breakup only in non-low triplanar tiers.
Rejected Alternatives: Keep the world hash in low tier or remove low-tier emission entirely. The hash spends ALU and can visibly shift with runtime origin movement; removing emission loses the cheap biolum identity that sells the biome.
Scalability potential: Low gets stable cheap pulse. Middle/High/Ultra keep spatial organic breakup and atlas projection for richer close-range flora.
Hardware Impact: Low tier removes one per-fragment `HectonProceduralBioHash13(positionWS * 0.03125 + height01)` path and no longer needs runtime position in `projectWS`. Exact GPU microseconds remain PENDING VERIFICATION.

## LOOP 11 LOW-TIER INTERPOLATOR STRIP

Problem: `_MATH_LOD_LOW` no longer consumes `projectWS` or `seed`, but the low variant still declared those interpolators and wrote dummy values from the vertex shader.
Solution: Guard both varyings and their vertex assignments with `#if !defined(_MATH_LOD_LOW)`, leaving them available only to the non-low triplanar path.
Rejected Alternatives: Keep dummy values for source simplicity, or split the shader into separate low/high files. Dummy interpolators spend bandwidth on the exact tier that needs the cheapest path; file split would increase maintenance and variant drift without adding visual quality.
Scalability potential: Low removes unused vertex-to-fragment payload. Middle/High/Ultra retain projection, seed offsets, shadowed PBR, triplanar normals, and spatial emission breakup for visual overkill.
Hardware Impact: Low tier drops two unused interpolator payloads and two dummy vertex assignments. Exact microseconds remain PENDING VERIFICATION until GPU capture on MX350/i3.

## LOOP 12 LOCAL VARIANT BLOAT REMOVAL

Problem: `Hecton_ProceduralBio.shader` declared `_QUALITY_MX350` as a local shader feature, but no code path referenced it. The real low-tier path is `_MATH_LOD_LOW`, so `_QUALITY_MX350` only created an unused variant.
Solution: Remove `_QUALITY_MX350` from the local shader feature pragma and keep `_QUALITY_HIGH` for the actual high-tier blend-sharpening branch.
Rejected Alternatives: Leave the unused keyword for consistency with older kelp shaders, or add a redundant `_QUALITY_MX350` branch. Consistency does not justify variant bloat; a redundant branch would duplicate `_MATH_LOD_LOW` ownership and make scalability harder to reason about.
Scalability potential: Low remains controlled by the global math LOD keyword. Middle uses the default triplanar path. High/Ultra can opt into `_QUALITY_HIGH` without carrying a dead MX350 local variant.
Hardware Impact: Removes one unused local shader feature variant from this shader. Exact import/build-time and runtime memory savings are PENDING VERIFICATION until shader variant collection stats are captured.

## LOOP 13 LOW-TIER POSITION/VIEW PAYLOAD STRIP

Problem: `_MATH_LOD_LOW` still carried `positionWS` and `viewDirWS` varyings even though the low MatCap path does not need shadow coordinates, specular half-vector, or rim lighting.
Solution: Guard `positionWS`, `viewDirWS`, and their vertex assignments with `#if !defined(_MATH_LOD_LOW)`, then move `viewDirWS` reconstruction into the non-low fragment path.
Rejected Alternatives: Keep the shared varying layout for readability, or add low-tier rim/specular to justify the payload. Shared layout wastes bandwidth on weak devices; low-tier rim/specular would spend extra ALU on a path intentionally sold by MatCap and emission.
Scalability potential: Low now carries only clip position, normal, vertex color, fog, and instance/stereo support. Middle/High/Ultra retain world position, projection, seed, and view direction for shadowed PBR and visual overkill.
Hardware Impact: Low tier drops two more vertex-to-fragment payloads and one view-direction vertex calculation. Exact GPU microseconds remain PENDING VERIFICATION until MX350 capture.

## LOOP 14 BRIDGE AUTHORING METADATA

Problem: `ProceduralFloraBiomeTintBridge` had serialized fields without inspector tooltips and a public `Tick` method without XML documentation, making the bridge easier to misuse during scene wiring.
Solution: Add a local header, precise tooltips, and XML docs for the `Tick` implementation without changing the signal-drain logic.
Rejected Alternatives: Leave style debt because runtime behavior was already correct, or add runtime validation/logging. Style debt slows integration; runtime logging would add string work and does not improve the hot path.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The bridge remains one global tint publisher for all procedural flora materials.
Hardware Impact: 0.000 us runtime change. Metadata improves authoring only; no measured runtime savings claimed.

Final Git Diff:
- New: `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`
- New: `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader.meta`
- New: `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef`
- New: `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef.meta`
- New: `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs`
- New: `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs.meta`
- New: `Docs/Tasks/Status_PROCEDURAL_FLORA_TEXTURER.md`
- New: `Docs/AgentLogs/Rationale_PROCEDURAL_FLORA_TEXTURER.md`

Diff evidence:
- Shader adds URP ForwardLit/ShadowCaster procedural bio material with SRP Batcher `UnityPerMaterial` CBUFFER.
- Shader adds X/Y/Z triplanar sampling for Albedo/Normal/ORM atlases, UDN-style triplanar normal blend, rsqrt normalization, `_HectonFloatingOffset` projection safety, `_MATH_LOD_LOW` MatCap branch, `_BiolumMasterPhase` emission, and `m31` seed read with object-origin fallback.
- C# bridge adds allocation-free indexed `ReadOnlySpan<BiomeChangedSignal>` consumption and global shader tint writes only on biome hash changes.
- ASMDEF adds `Hecton8.Graphics.Materials` with Contracts references plus `Hecton8.Core` for existing SignalBus location.
