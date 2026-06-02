# Rationale 1601 - ORBITAL_CELESTIAL_AND_LIGHTING_DIRECTOR

Date: 2026-06-01
Status: STATIC VERIFIED / RUNTIME VISUAL PENDING

## Decision 000 - Domain And Mandate Gate

Problem: The orbit prompt requests shader, lighting, texture import, and C# visual-sync work with strict MX350 constraints.
Solution: Domain locked to Echelon 7 Atmosphere & Celestial. Mandates selected for URP render hot paths, noir shader discipline, cinematic-cheat first, frame/VRAM budget, zero-GC, ARM64 DTO alignment, execution phases, and texture upload settings.
Rejected Alternatives: Editing the scene/shader before mandate selection; using physical planet meshes; treating bloom/lens artifacts as unconditional low-tier features.
Scalability potential: Low uses shader impostor math, hard silhouettes, low sample count. Middle adds stable texture/flow detail. High adds richer atmospheric blend. Ultra spends saved cost on denser visual layers without changing gameplay truth.
Hardware Impact: Static source phase only. Expected low-end benefit comes from removing CPU orbital transforms and avoiding physical planet/ring geometry; measured gain PENDING VERIFICATION on i3/MX350.

## Decision 001 - N-Body Scan Result

Problem: The prompt requires removal of realtime celestial mechanics, but direct scan found no active `Transform.Rotate`, `RotateAround`, or Unity `Update` loops in assigned Prologue/Environment script lanes.
Solution: Treat transform-based visual placement in `OrbitalRelativityDirector.ApplyPresentation` as the actual purge target. It moves planet/cloud/star transforms and toggles planet mesh renderers in VISUAL_SYNC; this is presentation, not gameplay truth, but it still keeps physical celestial objects alive.
Rejected Alternatives: Deleting `PlanetRotation.cs`; it already disables itself on enable and contains no hot path. Deleting `OrbitalRelativityDirector`; it owns prologue approach state, feedback signals, handoff, blackbox telemetry, and GlobalRegistry `IOrbitalDirector` contract.
Scalability potential: Low removes mesh renderer path and uses skybox impostor only. Middle keeps shader texture bands and flow. High/Ultra add denser procedural ring/atmosphere/star details through quality scalar.
Hardware Impact: Removing mesh/renderer toggles and transform presentation should reduce main-thread presentation churn and draw calls. Exact microseconds PENDING VERIFICATION; static estimate 15-40 us on i3/MX350 scene frame due to fewer transform writes and renderer state checks.

## Decision 002 - Texture Import Audit

Problem: Aegir source textures are 4096x2048, but import settings are inconsistent. Static sky impostor path does not need runtime mip streaming, and Standalone should be BC7.
Solution: Use `Assets/_Project/Art/TEXTURES/Aegir_storms.png` as primary BC7 Standalone texture after disabling mipmaps. Leave duplicate `_PROLOGUE_CONTENT` textures as candidates for quarantine unless material references require them. `ring.png` is already no-mip BC7 Standalone.
Rejected Alternatives: Importing new 8K sources during this pass; it risks VRAM budget and user explicitly wants no extra variant churn. Editing PNG binary files; import metadata is the correct Unity control plane.
Scalability potential: Low samples one Aegir texture plus ALU ring/star fakes. Middle adds flow distortion. High samples optional secondary texture if already resident. Ultra increases ALU richness, not texture count explosion.
Hardware Impact: Disabling mips on fixed sky texture saves roughly one third of texture residency for that import. BC7 2048 equirect base is about 5.33 MB without mips versus about 7.1 MB with mips; actual Unity residency PENDING VERIFICATION.

## Decision 003 - Shader Math Plan

Problem: Aegir must be drawn in a sky/background shader without sphere mesh or raymarch cost.
Solution: Fragment ray-sphere hit path: assume skybox view ray already normalized; compute `oc = rayOrigin - center`, `b = dot(oc, rd)`, `c = dot(oc, oc) - r2`, `h = b*b - c`. If `h <= 0`, skip planet. Only on hit: `t = -b - sqrt(h)`, `p = oc + rd*t`, `n = p * rsqrt(dot(p,p))`. Low tier can skip longitude and use band projection from `n.y` plus signed horizontal axis; higher quality uses fast atan2 polynomial for equirect UV.
Rejected Alternatives: Physical sphere mesh; N-body/RotateAround; atmosphere raymarch. Standard `normalize` in fragment hot branch rejected in favor of `rsqrt`.
Scalability potential: Low: 1 texture sample, smoothstep rim. Middle: flow UV distortion. High: ring shadow and storm overlay. Ultra: additional procedural scatter term. All driven by `_H8GlobalQualityWeight`.
Hardware Impact: One confirmed-hit `sqrt` is cheaper than mesh draw + depth/normal passes. Avoided per-pixel raymarch. ALU cost PENDING shader compiler proof.

## Decision 004 - GPU DTO Layout

Problem: C# sky coordinator needs a stable HLSL constant layout without ARM64 or CBUFFER misalignment.
Solution: `CelestialParametersDTO` as `[StructLayout(LayoutKind.Explicit, Size = 64)]`: offset 0 `Vector4 SunDirection`, offset 16 `Vector4 PlanetCenterRadius`, offset 32 `Vector4 RingPlaneInner`, offset 48 `Vector4 OrbitScalars`. HLSL mirrors four float4 lanes.
Rejected Alternatives: Separate scalars through many `Shader.SetGlobalFloat` calls; mixed `float3`/scalar fields that depend on implicit C# packing.
Scalability potential: Low/Middle/High/Ultra all consume same DTO layout. Quality changes presentation math only, not DTO shape.
Hardware Impact: Four vector global uploads are predictable. Constant buffer path remains preferred if available later; proof PENDING compile/runtime.

## Decision 005 - Report Discipline

Problem: Prompt requests JSON reports, user explicitly rejects useless JSON dumps and binary dumps as proof.
Solution: Use disk ledgers required by AGENTS (`Status_1601.md`, `Rationale_1601.md`, final `LOG_1601.md`) and actual shader/material files. JSON report generation is suppressed unless a validator test requires a machine-readable artifact.
Rejected Alternatives: Creating `Docs/Reports/ORBITAL_RENDERING_OPTIMIZATION_1601.json` before runtime evidence exists.
Scalability potential: Reporting has no runtime path.
Hardware Impact: No runtime cost.

## Decision 006 - Physical Aegir Mesh Purge

Problem: `GasGiant_Aegir.prefab` was a physical sphere MeshRenderer in the orbit scene and `OrbitalRelativityDirector.ApplyPresentation` still moved/toggled legacy celestial renderers.
Solution: Disabled the prefab root and MeshRenderer, assigned the scene skybox to the new shader material, and changed `ApplyPresentation` to keep legacy planet/cloud/Aegir renderers disabled while only pushing shader globals.
Rejected Alternatives: Deleting prefab YAML components; it would churn component IDs and break scene overrides for no runtime gain. Keeping mesh hidden only by camera culling was rejected because the hierarchy would still claim a drawable celestial body.
Scalability potential: Low has zero Aegir draw geometry and one sky pass. Middle/High/Ultra spend the saved draw call and transform budget on shader bands, rings, atmosphere, and star density.
Hardware Impact: Removes one MeshRenderer draw candidate and per-frame celestial transform writes. Static estimate on i3/MX350: 15-40 us main-thread/render submission saved before GPU fill-rate effects; runtime profiler proof pending because unrelated compile errors block play validation.

## Decision 007 - Ephemeris Route

Problem: The prompt names `EphemerisTableDTO`, but static source search found no such DTO or Data Monolith section exposed to this domain.
Solution: Cached `ICelestialRuntimeSnapshotReadModel` from `GlobalRegistry.CelestialRuntimeSnapshotReadModel`. This is the current first-party precomputed celestial owner route and avoids creating a second fact owner. Snapshot directions are sanitized into `CelestialParametersDTO` once during `LateFrameTick` presentation.
Rejected Alternatives: Polling `GlobalRegistry` every frame; inventing a new Data Monolith reader; reading `static_data.h8bin` directly from this graphics director.
Scalability potential: Same 64-byte DTO drives Low/Middle/High/Ultra. Quality changes fidelity only, not authority or data layout.
Hardware Impact: Four vector globals from cached snapshot read. No managed allocation in modified presentation path; expected cost is below 5 us on i3/MX350 aside from Unity shader global internal work.

## Decision 008 - GPU Parameter ABI

Problem: C# and HLSL need a stable no-GC bridge for sun, Aegir center/radius, ring plane, ring radii, flow speed, shadow scalar, and continuous quality.
Solution: Added `[StructLayout(LayoutKind.Explicit, Size = 64)] CelestialParametersDTO` with four `Vector4` lanes and mirrored HLSL globals `_H8AegirSunDirection`, `_H8AegirPlanetCenterRadius`, `_H8AegirRingPlaneInner`, `_H8AegirOrbitScalars`.
Rejected Alternatives: Many string-based shader property lookups; allocating arrays; `CommandBuffer` allocation before a proven reusable command-buffer owner exists.
Scalability potential: Low samples only Aegir bands. Middle enables flow distortion. High adds stronger atmosphere and rings. Ultra raises star grid and rim richness through the same scalar.
Hardware Impact: Constant data footprint is 64 bytes. Upload count is four vector lanes plus one compatibility float; no per-frame heap allocation in the modified bridge.

## Decision 009 - Aegir Sky Shader

Problem: Aegir and rings must render without a physical sphere, without raymarching, and without unguarded expensive transcendental functions on MX350.
Solution: Added `Hecton_AegirSky.shader`: unlit background pass, ray-sphere planet hit, ray-plane rings, BC7 Aegir band texture, quality-gated flow sample, dot-product atmosphere, and procedural hashed starfield.
Rejected Alternatives: Mesh sphere shader, volumetric atmosphere raymarch, cubemap background, unguarded `sin/cos/pow/normalize`.
Scalability potential: Low uses one texture sample and hard silhouettes. Middle adds flow UV. High/Ultra increase star density and atmosphere contribution using `GlobalQualityWeight`.
Hardware Impact: Static shader scan found zero `pow(`, `sin(`, or `normalize(` calls in the new shader. Hot planet branch uses one confirmed-hit `sqrt`; no loops.

## Decision 010 - Ring Shadow Math

Problem: Ring shadows need to affect the gas giant surface without Unity shadow cascades.
Solution: For each planet hit, project the surface point toward the sun, intersect the ring plane analytically, compare squared radius against inner/outer squared radii, and multiply color by a shadow scalar when inside the ring band.
Rejected Alternatives: Directional light shadow maps, ring mesh receiving/casting shadows, depth prepass.
Scalability potential: Low keeps hard binary shadow. Middle/High/Ultra modulate lane darkness with hashed ring strata while preserving the same branch count.
Hardware Impact: Shadow path runs only after a confirmed planet hit. Uses dot products, one plane divide, squared-radius comparisons, and no shadow-map samples.

## Decision 011 - Continuous Atmosphere Quality

Problem: The sky shader needs quality scaling without binary tier switches or shader keyword variants.
Solution: `_H8GlobalQualityWeight` and `_H8AegirOrbitScalars.w` are consumed as a continuous scalar for atmosphere intensity, flow-map contribution, star density, and ring brightness. The DTO shape does not change across quality.
Rejected Alternatives: `shader_feature` / `multi_compile` low-ultra branches; separate low/high shader assets; changing gameplay ownership based on quality.
Scalability potential: Low uses one band sample, hard rim, sparse stars. Middle increases flow and star field. High adds stronger atmosphere/ring response. Ultra raises visual density with the same code path.
Hardware Impact: No variant explosion. Low-end MX350 avoids extra texture samples through scalar suppression; high-end spends the same ABI on richer visible layers.

## Decision 012 - Procedural Starfield

Problem: The orbit shot needs a star background without another texture atlas or sky cubemap import.
Solution: Hash the sky ray into coarse and fine cell fields, gate brightness by density, and use a triangle-wave twinkle from `_Time.y`.
Rejected Alternatives: Sine twinkle requested in prompt; rejected because mandate scan forbids avoidable transcendental shader cost. Texture atlas stars rejected because the existing sky pass can generate them with ALU and no VRAM.
Scalability potential: Low has sparse fixed stars. Middle increases density. High/Ultra increase visible twinkle and fine-star probability continuously.
Hardware Impact: Zero texture residency for stars. Static scan confirms no `sin(`, `pow(`, or `normalize(` in `Hecton_AegirSky.shader`.

## Decision 013 - Orbit Lighting Authority

Problem: `01_ORBIT` needed hard contrast lighting and black space without ambient leak.
Solution: Scene YAML and bootstrap now enforce black ambient, skybox clear, assigned Aegir sky material, cold blue directional light at 5.5 intensity, hard shadows, and zero bounce.
Rejected Alternatives: Soft ambient fill, warm cinematic light, physical fill lights, and local hidden area-light imitation.
Scalability potential: Low keeps a readable silhouette through hard key and sky shader rim. Middle/High/Ultra spend saved ambient/fill cost on bloom, ring shadow, and atmosphere.
Hardware Impact: One directional light route remains. Removing ambient/fill complexity is expected to save a small but stable render-state cost; exact microseconds require profiler after host contention clears.

## Decision 014 - Bloom Scaling

Problem: Bloom is required for the first orbit frame but cannot become an unconditional post stack tax.
Solution: Bootstrap enables URP post-processing and creates a scene-local global `VolumeProfile` only in cold setup. Bloom threshold, intensity, scatter, high-quality filtering, and max iterations are all driven by `GlobalQualityWeight`.
Rejected Alternatives: Lens flare GameObjects per shot; fixed high-quality bloom; obsolete URP `skipIterations`; creating a persistent asset during runtime bootstrap.
Scalability potential: Low uses low intensity and skipped iterations. Middle raises scatter. High enables stronger bloom. Ultra enables high-quality filtering and more iterations.
Hardware Impact: Cold allocations are outside gameplay hot path. Low-end cost is bounded by lower bloom iteration count; high-end gets visual overkill only when quality scalar allows it.

## Decision 015 - Build Suppression

Problem: The prompt requested compilation, but current operator instruction forbids `dotnet build` after minor edits, and host telemetry showed `CPU_LOAD=100` plus a running `dotnet` PID 25280.
Solution: No build launched. Verification used Unity console read, shader/text/static route scans, asmdef JSON parsing, scene YAML checks, file hashes, and a compile-none asset refresh. A shader helper name collision was fixed by renaming `FastAtan2` to `H8AegirFastAtan2`.
Rejected Alternatives: Starting a heavyweight build into saturated CPU; claiming compile proof without checking Unity console; leaving helper names in global include namespace.
Scalability potential: No runtime effect.
Hardware Impact: Avoided burning host CPU and blocking other agents. Static checks cost milliseconds instead of minutes.

## Decision 016 - Drift Proof Boundary

Problem: Task 16 asked for ephemeris drift validation, but no `EphemerisTableDTO` exists in this project.
Solution: Added `OrbitalSkyEphemerisDrift1601EditTests.cs` against the existing public `HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke` route. The test asserts deterministic repeat, finite normalized directions, bounded eclipse scalar, and nonzero gas giant drift after 300 seconds.
Rejected Alternatives: Inventing a new ephemeris table owner; writing an integration test that needs play mode or Data Monolith boot.
Scalability potential: Test is editor-only and has no runtime path.
Hardware Impact: No player cost. Test execution is deferred until host contention clears.

## Decision 017 - Eclipse Light Fade

Problem: Ring/planet eclipse state must visibly affect the main orbit light without scene search or a second truth owner.
Solution: Bootstrap resolves the scene directional `Light` once and passes it to `OrbitalRelativityDirector`. `EclipseOcclusion01` is read from the cached celestial snapshot, packed into `CelestialParametersDTO.SunDirection.w`, uploaded to shader, and smoothed into directional intensity by `ApplyEclipseLighting`.
Rejected Alternatives: Per-frame `FindObjectOfType<Light>`; direct hot polling of `GlobalRegistry`; hard on/off eclipse pop; shadow-map based ring eclipse.
Scalability potential: Low uses the fade only. Middle/High/Ultra combine fade with shader ring shadow, atmosphere darkening, and bloom response.
Hardware Impact: One nullable light check, a few scalar ops, and one intensity write per presentation tick. Static hotpath scan found no allocation markers.

## Decision 018 - Metric Evidence Format

Problem: The original prompt asked for a JSON report, while the active operator instruction explicitly rejects useless JSON and binary proof dumps.
Solution: Use required markdown ledgers and final log plus SHA-256 file hashes. JSON validator report is suppressed.
Rejected Alternatives: Creating `Docs/Reports/ORBITAL_RENDERING_OPTIMIZATION_1601.json` for bureaucracy.
Scalability potential: No runtime effect.
Hardware Impact: No runtime cost and no extra disk churn beyond required ledgers.

## Decision 019 - Final Asset Hashes

Problem: The deliverable must be provable on disk without running a heavyweight build.
Solution: Captured SHA-256 hashes for the sky shader, master material, C# coordinator, bootstrap, and ephemeris test in the final log.
Rejected Alternatives: Screenshot-only proof; chat-only report; Unity build proof under CPU contention.
Scalability potential: No runtime effect.
Hardware Impact: File hashing is cold verification only.

## Decision 020 - Shader Helper Namespace Collision

Problem: Unity console reported `Shader error in 'HECTON/Sky/Hecton_AegirSky': redefinition of 'FastAtan2'` on d3d11.
Solution: Renamed the helper to `H8AegirFastAtan2` and updated the only call site. Re-ran static shader scans and read Unity console after compile-none asset refresh; no 1601 shader error remained.
Rejected Alternatives: Removing custom atan2 and relying on a potentially more expensive standard function; ignoring console because static grep passed.
Scalability potential: No quality behavior change.
Hardware Impact: Same ALU path, no extra instructions intended. Exact shader compiler instruction count still pending host availability.

## Decision 021 - Presentation Phase Closure

Problem: `ApplyPresentation()` still had cold direct call sites in `OnEnable` and `ResetRuntimeState(true)`, which weakened the claim that presentation is VISUAL_SYNC/LateFrame-only.
Solution: Replaced both direct calls with `QueueOrbitalPresentation()`. The actual shader/global upload path now executes through the pending flag consumed by `LateFrameTick`.
Rejected Alternatives: Leaving cold direct calls and documenting them as harmless; that would not satisfy the integrator protocol.
Scalability potential: Same visual fidelity. Phase ordering is cleaner across weak and strong hardware because simulation settles before presentation reads it.
Hardware Impact: No extra runtime cost. Removes an enable/reset timing edge rather than adding work.

## Decision 022 - Queue Method Dependency Flattening

Problem: Queue methods called `TryRegisterUpdateLane()`, which could turn a hot `Tick` queue operation into a conditional GlobalRegistry route when registration state was missing.
Solution: Removed registration calls from queue methods. Registration is now only from cold enable and dispatcher hot-swap paths.
Rejected Alternatives: Keeping the guard because it usually exits early; hot paths need structural proof, not usual-case behavior.
Scalability potential: Low devices avoid surprise registry checks during stress frames. High-end devices get identical visuals with cleaner phase contracts.
Hardware Impact: Saves a branch path and possible registry touch in edge frames; exact microseconds are below profiler noise but the dependency boundary is now provable.

## Decision 023 - Roslyn Apex Gate

Problem: The APEX protocol needs durable source proof, not chat claims, JSON dumps, or markdown tables.
Solution: Added `OrbitalApexIntegrator1601EditTests.cs`. It parses source with Roslyn and fails if hot methods resolve cold dependencies, if `ApplyPresentation` moves outside `LateFrameTick`, if telemetry write locks are not single-lock try/finally, if build process strings enter the domain, or if Aegir shader reintroduces expensive calls/variants.
Rejected Alternatives: Shell-only grep report; manual checklist; broad build spam.
Scalability potential: No runtime effect; protects the architecture against future drift.
Hardware Impact: Editor-only source parser. No player cost.

## Decision 024 - Compile Throttle Compliance

Problem: Unity reported a real 1601 compile error in `OrbitalSkyEphemerisDrift1601EditTests.cs`, but dotnet build remains prohibited and host CPU later reached 100.
Solution: Fixed the test namespace to match existing `HectonCelestialEngineEditTests` usage and requested one Unity script compile. No `dotnet build` was launched. After that compile, console reports only unrelated DropPod errors.
Rejected Alternatives: Leaving a known 1601 compile error; launching external dotnet build; continuing to edit while Unity was not ready.
Scalability potential: No runtime effect.
Hardware Impact: One necessary Unity script compile to clear a known 1601 error; no repeated build spam.

## Decision 025 - Tick Delta Ownership

Problem: `ApplyEclipseLighting` used `Time.deltaTime` while executing from the tick-owned visual presentation lane. That violates the project rule that ITickable systems use the dispatcher-provided delta, not Unity global time.
Solution: Added `_presentationDeltaTime`, assigned from sanitized `Tick(float deltaTime)`, reset it with runtime state, and used it for eclipse fade smoothing in `ApplyEclipseLighting`.
Rejected Alternatives: Leaving `Time.deltaTime` because it is only visual; visual sync is still a phase contract. Passing a parameter through every presentation method was rejected because the stored scalar is already a fixed-size owner-local field and keeps call sites stable.
Scalability potential: Low, middle, high, and ultra use identical timing ownership. Quality affects visual intensity, not phase timing.
Hardware Impact: Same arithmetic count, no allocation, no Unity global time read. Expected CPU delta is negligible; the architectural gain is deterministic timing and replay-safe presentation.

## Decision 026 - APEX Guard Expansion

Problem: The first APEX guard caught direct hot dependency lookups but did not explicitly guard private presentation helpers or Unity global time reads.
Solution: Expanded `HotMethodNames` to include presentation helpers and queue methods, rejected any hot `GlobalRegistry.` expression, and added a Roslyn test proving the eclipse fade uses `_presentationDeltaTime` instead of `Time.deltaTime`.
Rejected Alternatives: Relying on one-off shell scans; allowing only chat-level proof.
Scalability potential: No runtime effect. Prevents future drift on every hardware lane.
Hardware Impact: Editor-only AST parsing. Player cost is zero.

## Decision 027 - Celestial Shader Upload Dirty Gate

Problem: Aegir celestial DTO globals were uploaded every late presentation tick even when the snapshot, ring parameters, and quality scalar were unchanged. That wastes CPU-to-GPU state traffic on compact and handheld lanes.
Solution: Added `_uploadedCelestialParameters` and `_celestialParametersUploaded`. `ApplyPresentation` now calls `UploadCelestialGlobalsIfDirty`, which compares four Vector4 lanes against the last upload and only writes shader globals when changed. Clear/reset invalidates the upload cache.
Rejected Alternatives: Dirty-gating every scalar in the prologue presentation body; distance, speed, heat, and whiteout are expected to change during approach and would add branch work for little return. CommandBuffer was rejected because no reusable owner exists in this domain.
Scalability potential: Low and middle lanes avoid repeated unchanged Aegir vector uploads. High and ultra lanes keep the same visual overkill shader path and still receive updates when quality or ephemeris changes.
Hardware Impact: Saves up to six shader global vector/float writes on unchanged celestial frames. Exact microseconds require profiler proof; static expected gain is small but deterministic and removes avoidable bandwidth churn.

## Decision 028 - Continuous Orbit Bloom

Problem: Orbit bootstrap enabled visible bloom at minimum quality and used `quality > 0.72f` for high-quality filtering. That violates continuous `GlobalQualityWeight` discipline and the compact/minimum bloom prohibition.
Solution: Replaced the linear always-on bloom with `bloomWeight = quality * quality`, zero volume weight at quality 0, continuous threshold/intensity/scatter/max-iteration scaling, and fixed `highQualityFiltering` false. High lanes buy overkill through stronger intensity/scatter and more iterations, not a binary filter switch.
Rejected Alternatives: Disabling Bloom with a boolean threshold; that would be another binary quality branch. Keeping high-quality filtering on high lanes was rejected because the bool cannot scale continuously.
Scalability potential: Minimum/compact has zero visible bloom weight. Middle ramps in gently. High/ultra get stronger bloom through continuous parameters.
Hardware Impact: Removes visible minimum-tier bloom and avoids high-quality filtering cost. Exact render-pass savings are pending profiler; static proof shows no binary `quality >` branch remains in the bootstrap.

## Decision 029 - Deterministic Orbit Camera And Shadow Budget

Problem: Orbit bootstrap used `Mathf.Max` to preserve existing camera far clip and key light intensity, and forced `LightShadowResolution.VeryHigh`. That lets stale scene values inflate z range, light output, and shadow cost.
Solution: Set camera far clip exactly to `OrbitCameraFarClipMeters`, set key light intensity exactly to `OrbitKeyLightIntensity`, and use `LightShadowResolution.FromQualitySettings` while keeping hard shadows and zero bounce.
Rejected Alternatives: Keeping `VeryHigh` because the orbit shot is cinematic; quality settings must own platform shadow resolution. Disabling hard shadows entirely was rejected because the prompt and art direction require hard contrast.
Scalability potential: Low/compact follows project quality shadow budget. Middle/high/ultra can raise shadow resolution through quality settings without per-scene override drift.
Hardware Impact: Prevents accidental overdraw/z precision loss from oversized far clip and avoids forced VeryHigh shadow maps on MX350-class devices.

## Decision 030 - Aegir Flow Texture And Ring Sqrt Removal

Problem: The Aegir shader used a quality-threshold branch for flow texture sampling and a second `sqrt` for decorative ring lane phase. That violates continuous quality discipline and spends ALU/texture bandwidth on non-authoritative detail.
Solution: Removed `_AegirFlowTex` from shader and material, replaced flow sampling with procedural band drift driven by continuous `flowWeight`, and replaced ring lane phase with squared-radius math. The only standalone `sqrt` left is the ray-sphere hit solve.
Rejected Alternatives: Keeping branch-gated flow sample for high visual detail; it adds a second texture sample and a binary threshold. Removing storm motion entirely was rejected because high-tier visual movement is still needed.
Scalability potential: Low gets one texture sample and static-stable bands. Middle/high/ultra get continuous procedural drift without extra texture residency.
Hardware Impact: Saves one texture sample on planet pixels and one `sqrt` on ring pixels. Exact frame gain needs shader compiler/profiler proof.

## Decision 031 - Legacy Presentation Shader Global Dirty Gate

Problem: `ApplyPresentation` still wrote seven legacy float shader globals every late-frame presentation tick. The values are valid presentation state, but unconditional upload is avoidable CPU-to-GPU state churn.
Solution: Added 32-byte `PresentationShaderGlobalsDTO` with two Vector4 lanes, built it inside `ApplyPresentation`, and moved all legacy float `Shader.SetGlobalFloat` calls into `UploadPresentationShaderGlobalsIfDirty`. Clear/reset invalidates the upload cache.
Rejected Alternatives: Leaving the writes because distance normally changes during approach; unchanged frames still exist during zero-delta, pause, reset, or stable visual phases. A CommandBuffer was rejected because this domain does not own a persistent render-command route.
Scalability potential: Low and middle skip repeated global writes during stable frames. High and ultra keep identical visuals and spend saved CPU/GPU sync budget on shader density, not redundant state traffic.
Hardware Impact: Saves up to seven legacy shader global float writes on unchanged presentation frames. Exact profiler measurement is blocked by external compile errors and CPU saturation.

## Decision 032 - Finite Quality And Eclipse Scalar Guard

Problem: `BuildCelestialParameters` and orbit Bloom read `HomeostasisBrain.GlobalQualityWeight` through direct `math.saturate`. If that global ever becomes NaN, shader globals or post-process settings can inherit NaN and destabilize the orbit frame. `EclipseOcclusion01` also entered the DTO without a local clamp.
Solution: Reused finite-safe `ResolveQuality01` in `BuildCelestialParameters`, added the same helper to `PrologueOrbitSceneBootstrap`, and saturated `snapshot.EclipseOcclusion01` before packing it into `SunDirection.w`.
Rejected Alternatives: Trusting upstream quality/eclipsing owners; presentation code must defend the GPU and post stack from malformed scalar input. Adding exception/log spam was rejected because it would add hot/noisy managed behavior.
Scalability potential: Low, middle, high, and ultra keep the same continuous quality curve. Malformed quality falls back to full presentation instead of corrupting shader/post values.
Hardware Impact: Adds one finite select in cold/post setup and reuses it in presentation parameter build. Cost is below measurable frame noise; prevents NaN-driven render instability.

## Decision 033 - Dead Transform Orbit Fake Purge

Problem: After Aegir moved to shader impostor presentation, `ResolveOrbitalWindowOffset`, `ResolveGasGiantBackdropPosition`, and their serialized orbit arc knobs remained as dead transform-orbit code. Dead code invites future agents to revive the physical presentation path.
Solution: Removed the unused helper methods and obsolete serialized fields. APEX guard now rejects those method/field names and direct planet/gas-giant localPosition presentation writes.
Rejected Alternatives: Leaving the helpers because they no longer execute; the domain requirement is architectural clarity, not dormant alternate routes.
Scalability potential: All quality levels now share the same sky-impostor route without a stale mesh/backdrop orbit branch.
Hardware Impact: No runtime delta because the code was dead. Maintenance gain is concrete: fewer serialized knobs and no dormant transform presentation path.

## Decision 034 - Ring Mask Squared Radius Flattening

Problem: The Aegir shader ring mask multiplied inner/outer radii inside the mask helper for both visible rings and planet ring shadow.
Solution: Precomputed `ringInnerSq` and `ringOuterSq` once in the fragment path and changed the ring helper to `HardRingMaskSq`.
Rejected Alternatives: Leaving the duplicate multiplies because they are small; the shader already carries one required ray-sphere sqrt, so cheap repeated ALU still gets removed when the proof is simple.
Scalability potential: Low through ultra use the same analytic ring path with fewer redundant operations.
Hardware Impact: Saves duplicate radius-square multiplies on ring pixels and planet ring-shadow pixels. Exact frame impact is below profiler noise but directionally correct for MX350-class ALU budgets.
