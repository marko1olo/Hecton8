# RENDER_GI_RELAY_SYNC Rationale

STATUS: PENDING VERIFICATION

## Decision 0 - Prompt Identity And Mandate Intake
Problem: The user supplied Agent Identity LIGHTING_TECH and Prompt ID RENDER_GI_RELAY_SYNC; the batch tag is keyed by prompt ID, not role.
Solution: Use `RENDER_GI_RELAY_SYNC` as the file/log identity and `LIGHTING_TECH` as domain. Loaded eight mandates before code: registry, zero-GC, native jobs, lighting, URP render hot path, AUP, telemetry, and visual fake first.
Rejected Alternatives: Searching by `LIGHTING_TECH` alone found BIOLUMINESCENCE_DIRECTOR first and would have executed the wrong lighting prompt. Reading archived batches was rejected; active file is `Docs/Tasks/CURRENT_BATCH.md`.
Scalability potential: Low uses discrete SH states and baked/darkness fakes. Middle uses 10Hz SH interpolation. High adds smoother palette response. Ultra spends saved cycles on stronger visual palette and controlled overkill, not physics truth.
Hardware Impact: Correct prompt isolation prevents cross-agent edits. Expected low-end gain is procedural: avoids touching unrelated systems and preserves i3/MX350 compile stability. Estimated runtime gain: 0 us until code lands.

## Decision 1 - Registry GI Authority
Problem: Lighting ownership had direct `RenderSettings` writers but no actual `LightingManager.Instance` singleton to purge.
Solution: Added `IGIRelaySystem` plus `GlobalRegistry.GIRelay`, `RegisterGIRelayRuntime`, and `UnregisterGIRelayRuntime`. `HectonCelestialEngine` now yields atmosphere/probe authority when the relay is active.
Rejected Alternatives: Creating a new `LightingManager.Instance` compatibility singleton was rejected because it recreates the dependency being purged. Scene-wide discovery was rejected because it is cold-path fragile and hot-path hostile.
Scalability potential: Low/MX350 gets one registry pointer and four SH snap states. Middle gets 10Hz native interpolation. High/Ultra can consume the same relay state for stronger visual palette and reflection overkill without another authority.
Hardware Impact: Service lookup remains O(1). Estimated saving: 8 us from avoiding object discovery; avoided GPU stall risk is measured in milliseconds when legacy ambient/probe updates are abused.

## Decision 2 - Celestial Signal Read Model
Problem: Prompt names `CelestialTimeSignal`, while the project already publishes `CelestialRuntimeSnapshot` and `GlobalTimeSyncSignal` from the celestial runtime.
Solution: Consume `GlobalRegistry.CelestialRuntimeSnapshot` and its sequence as the allocation-free CelestialTime read model. Weather lightning uses the existing NativeQueue-backed `WeatherEvents` signal.
Rejected Alternatives: Adding a parallel `CelestialTimeSignal` duplicate was rejected because it would fragment the bus and create false dependencies for other agents.
Scalability potential: Low through Ultra share the same snapshot. The tier switch only changes SH math, not signal topology.
Hardware Impact: Snapshot read is pointer/static field cost. Estimated 2 us per 10Hz tick on i3/MX350.

## Decision 3 - Lighting Assembly Boundary
Problem: The prompt demands `Hecton8.Lighting` isolation, but this repository keeps the actionable registry contracts in `Hecton8.Core/GlobalRegistryContracts.cs`; `Hecton8.Core.Contracts` is an empty marker assembly.
Solution: Added `Hecton8.Lighting.asmdef` as a leaf assembly referencing `Hecton8.Core` and Unity packages only. This keeps lighting code out of monolithic Core while using the existing contract owner.
Rejected Alternatives: Moving large contract sets between assemblies was rejected as cross-agent architectural churn. Reflection-based registry calls were rejected as slow and brittle.
Scalability potential: Low/Middle/High/Ultra behavior stays in one leaf assembly with no extra downstream gameplay dependency.
Hardware Impact: Runtime impact 0 us; compile isolation reduces accidental rebuild scope after Unity regenerates projects.

## Decision 4 - Reflection Probe Cull
Problem: Realtime reflection probe updates can stall the render pipeline, but first-party code search found no `RenderProbe()` call path.
Solution: GI relay exposes one optional low-res `WaterVolume` cubemap and binds it through global shader texture and `RenderSettings.customReflection` only when assigned.
Rejected Alternatives: Dynamic probe render/update was rejected as pipeline-stall prone. Per-biome cubemap churn was rejected because depth palette already carries the visual read.
Scalability potential: Low can leave cubemap unassigned and rely on palette. Middle uses a single low-res cubemap. High/Ultra can author a stronger cubemap without runtime probe renders.
Hardware Impact: Avoided reflection probe stall risk: roughly 1000-5000 us depending GPU and probe resolution. Hot-path cost is one guarded global bind on state change.

## Decision 5 - Native SH Lerp
Problem: Unity `SphericalHarmonicsL2` helper calls hide layout and invite managed struct construction patterns.
Solution: Store 27 coefficients in preallocated `NativeArray<float>` for day/night and four snap states. A Burst `IJob` writes the output coefficient array; main thread blits the exact 27-float payload to `RenderSettings.ambientProbe`.
Rejected Alternatives: Calling `AddAmbientLight` on every lighting shift was rejected because it rebuilds SH through managed API and does not expose layout verification.
Scalability potential: Low/MX350 skips interpolation and selects four states. Middle interpolates at 10Hz. High/Ultra use the same saved cost for richer depth/moon palette output.
Hardware Impact: Estimated 10-25 us per SlowTick on i3/MX350, amortized to 1-3 us per rendered frame at 10Hz. GPU stall avoidance is the real gain.

## Compile Check 1
Problem: First compile verification after tasks 1-5 hit a dependency wall unrelated to the GI relay.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. The failure was in missing pre-existing namespaces/types: `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `Hecton8.Cartography`, `IDataVault`, `SystemID`, and cartography signal types.
Rejected Alternatives: Editing unrelated memory/cartography/physics dependency seams was rejected as outside LIGHTING_TECH authority.
Scalability potential: No runtime effect.
Hardware Impact: 0 us. Status remains PENDING VERIFICATION until the dependency wall is cleared or Unity compile gives a cleaner assembly graph.

## Decision 6 - Unsafe SH Push
Problem: The GI relay must write Unity SH without allocating or rebuilding helper structs every lighting shift.
Solution: Added `ValidateSphericalHarmonicsLayout` for 27 floats and `TryPushAmbientProbeFrom`, which uses `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, `UnsafeUtility.AddressOf`, and `UnsafeUtility.MemCpy` into a retained `_ambientProbe` field. The relay sets `AmbientMode.Custom` before assigning `RenderSettings.ambientProbe`.
Rejected Alternatives: `new SphericalHarmonicsL2()` and `AddAmbientLight` were rejected because they hide layout and create managed API churn. Reflection into RenderSettings internals was rejected as brittle.
Scalability potential: Low uses the same blit with snap-state coefficients. Middle/High/Ultra only change coefficient quality, not the push path.
Hardware Impact: Estimated 1-3 us amortized per rendered frame at 10Hz; avoided ambient pipeline stalls remain the primary savings.

## Decision 7 - Depth Palette SH Tint
Problem: Depth mood needs to read as GI change without simulating water-volume light transport.
Solution: The Burst job applies a shallow cyan to deep black 1D palette to all 27 coefficients by channel. Depth comes from absolute depth sources and normalizes to 500m.
Rejected Alternatives: Volumetric GI, per-probe cube generation, and particle light scattering were rejected as over-budget for i3/MX350.
Scalability potential: Low keeps the same tint over four snap states. Middle interpolates. High/Ultra can raise palette strength and author richer cubemaps without changing math shape.
Hardware Impact: Estimated 3-6 us per SlowTick. Visual gain buys abyss readability without frame-time debt.

## Decision 8 - Underwater Surface Emission Handoff
Problem: The prompt requires `HectonUnderwaterVisuals` surface color to follow moon/celestial state; shader globals alone would not guarantee Crest material state after its own updates.
Solution: Added `HectonUnderwaterVisuals.ApplyGIRelaySurfaceEmission` and call it from the relay. It updates `_HectonWaterSurfaceEmission`, `_HectonUnderwaterSurfaceColor`, and selected Crest diffuse/subsurface color properties with finite color validation.
Rejected Alternatives: Searching all renderers/materials was rejected as allocation-prone and outside the relay contract. Direct dependency on missing future systems was rejected.
Scalability potential: Low updates one or two known materials at 10Hz. High/Ultra can feed richer material properties from the same method.
Hardware Impact: Estimated 4-12 us per SlowTick when the ocean material exists; no per-renderer scan.

## Decision 9 - Depth Shadow Cascade Optimizer
Problem: Shadow cascades are wasted below deep water where directional light is visually faint.
Solution: Added hysteresis: baseline above shallow water, one cascade after 200m, zero cascades after 500m, with ascent exit thresholds at 170m and 450m.
Rejected Alternatives: Dynamic per-light volumetric attenuation and per-camera shadow reconfiguration were rejected for unpredictability and churn.
Scalability potential: Low and Middle aggressively save GPU cost. High/Ultra can spend the saved budget on water/biolum visuals while still disabling cascades in abyssal depths.
Hardware Impact: Estimated 50-300 us GPU-side savings at depth depending scene caster count.

## Decision 10 - Fog And Ambient Authority Purge
Problem: `HectonUnderwaterVisuals` and `HectonCelestialEngine` still wrote `RenderSettings.ambientLight`/trilight values while GI relay was active, creating an authority race.
Solution: Patched both systems to preserve fog/sun behavior but skip ambient writes when `GlobalRegistry.GIRelay.IsAmbientProbeAuthorityActive` is true. GI relay now owns `_HectonAtmosphereColor`, `_HectonFogLOD`, and ambient probe updates.
Rejected Alternatives: Removing the systems entirely was rejected because they still own camera fog, sky readability, and sun intensity. Last-writer-wins ordering was rejected as nondeterministic across agents.
Scalability potential: Low through Ultra share one ambient authority. Fog remains cheap shader-global state and can scale visually by tier later.
Hardware Impact: Estimated 2-5 us per SlowTick saved by avoiding duplicate ambient updates; more important, removes pipeline-stall risk from competing settings writes.

## Compile Check 2
Problem: Second compile verification after tasks 6-10 could not complete cleanly because live MSBuild contention left node processes active.
Solution: Attempted `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; command timed out. Child MSBuild nodes spawned by the timed-out attempt were identified by parent PID and terminated. No Unity assembly project exists for `Hecton8.Lighting.csproj` yet.
Rejected Alternatives: Killing all dotnet processes was rejected because other agents are running concurrently. Editing unrelated asmdef/project generation was rejected.
Scalability potential: No runtime effect.
Hardware Impact: 0 us. Status remains PENDING VERIFICATION; compile evidence is still blocked by project-level dependency/build contention.

## Decision 11 - Predator Eye Glow Scalar
Problem: Eclipse predator readability needs to increase without touching fauna renderer/material instances.
Solution: GI relay writes `_FaunaEmissiveMultiplier` from the celestial eclipse scalar. The multiplier remains 1 until `EclipseScalar > 0.5`, then ramps to 2.35.
Rejected Alternatives: Per-fauna renderer enumeration and temporary lights were rejected as allocation and CPU/GPU churn.
Scalability potential: Low gets one global scalar. High/Ultra can author stronger fauna shaders against the same scalar.
Hardware Impact: Estimated 1-2 us per SlowTick; no renderer traversal.

## Decision 12 - Reflection Probe Cull Confirmation
Problem: Realtime reflection probe renders are a known stall, but the repo mostly contains static `m_ReflectionProbeUsage` flags.
Solution: First-party script search found no runtime `RenderProbe()` path. GI relay binds one optional low-res `WaterVolume` cubemap and its depth palette via globals and `RenderSettings.customReflection`.
Rejected Alternatives: Realtime probe generation, per-biome probes, and cubemap cycling were rejected as pipeline-stall bait.
Scalability potential: Low can leave the cubemap null. Middle uses the single low-res cubemap. High/Ultra can use a higher-authored cubemap without runtime rendering.
Hardware Impact: Avoided stall estimate remains 1000-5000 us when probe churn would otherwise occur.

## Decision 13 - Two-Frame Lightning SH Overlay
Problem: Lightning must spike GI for exactly two frames without object creation or coroutine timing.
Solution: The relay consumes `WeatherEventType.Lightning`, sets a two-frame budget, memcpys current SH output into `_shLightningScratch`, boosts L0 RGB, pushes the overlay in `LateFrameTick`, and restores base SH after the second tick.
Rejected Alternatives: Creating a transient light or coroutine was rejected because it allocates and loses exact frame control under time-scale changes.
Scalability potential: Same two-frame path on all tiers. High/Ultra can increase authored scalar; Low keeps the same native scratch path.
Hardware Impact: Estimated 2-4 us per active lightning frame; no GameObject or material churn.

## Decision 14 - AUP-Safe Depth
Problem: Floating origin makes runtime transforms unsafe for depth decisions.
Solution: Resolve depth from `BiomeMatrix.CurrentDepthMeters`, then player movement depth, then underwater visuals depth, then `CurrentAup.ToAbsoluteDouble3().y` as absolute fallback. Non-finite values are rejected.
Rejected Alternatives: `transform.position.y` and camera local Y were rejected because origin shifts corrupt them.
Scalability potential: Same absolute read across tiers. Low spends one fallback branch only when primary depth providers are unavailable.
Hardware Impact: Estimated 1-2 us per SlowTick.

## Decision 15 - No Update SH Path
Problem: Per-frame ambient work is unnecessary and competes with frame budget.
Solution: `HectonGIRelaySystem` implements `ISlowTickable` and `ILateFrameTickable`; no `Update()` exists. SH jobs schedule at SlowTick cadence, while LateFrame only commits completed jobs and handles the exact two-frame lightning overlay.
Rejected Alternatives: FastTick or MonoBehaviour Update interpolation was rejected because shader smoothing can carry visual interpolation.
Scalability potential: Low through Ultra use 10Hz CPU lighting state. High/Ultra can smooth in shader with `_HectonGIRelayState`.
Hardware Impact: Estimated 10-25 us avoided per frame compared with per-frame SH blend.

## Decision 16 - Math LOD Snap States
Problem: i3/MX350 target should not interpolate all SH coefficients when tier is weak.
Solution: `IsLowTier()` treats `Unknown`, `Low`, `Mx350`, and `H8_LOW_MEMORY_PROFILE` as snap mode. The Burst job selects one of four 27-float SH state blocks.
Rejected Alternatives: A balanced middle-ground path was rejected; tier logic must strongly favor toaster behavior and spend saved cycles on visuals at high tier.
Scalability potential: Low uses four states. Middle interpolates. High/Ultra can increase authored profile richness without changing relay ownership.
Hardware Impact: Estimated 4-8 us saved per SlowTick on low tier.

## Decision 17 - Zero-GC Audit
Problem: GI changes must not allocate managed memory in runtime lighting paths.
Solution: Persistent `NativeArray` storage owns day/night/snap/output/lightning/telemetry buffers. The relay uses a retained `_ambientProbe` field and no `new SphericalHarmonicsL2()`. Reflection probe generation and renderer scans are absent.
Rejected Alternatives: Coroutines, `List<>` telemetry, dynamic material scans, and transient SH structs were rejected as GC sources.
Scalability potential: All tiers stay 0 B/frame in the relay. High/Ultra visual overkill must use authored assets or shader work, not managed churn.
Hardware Impact: Prevents GC spikes; target is 0 B/frame.

## Decision 18 - Unsafe Layout Guard And Compile Wall
Problem: Native SH blit is only safe if Unity `SphericalHarmonicsL2` is exactly 27 floats.
Solution: `ValidateSphericalHarmonicsLayout` compares `SHCoefficientCount * UnsafeUtility.SizeOf<float>()` to `UnsafeUtility.SizeOf<SphericalHarmonicsL2>()` before every blit; mismatch records telemetry and dumps the blackbox.
Rejected Alternatives: Blind `MemCpy` was rejected because Unity layout changes would corrupt memory. Standard managed SH helpers were rejected as too opaque.
Scalability potential: Same guard on all tiers; overhead is a small integer compare on 10Hz commit.
Hardware Impact: 0 us material benefit; avoids catastrophic undefined behavior.

## Decision 19 - GI Blackbox Telemetry
Problem: A lighting crash or NaN without state history is unacceptable.
Solution: Added fixed 300-entry native ring with frame, sequence, flags, state hash, `ShadowCascadeLevel`, depth, time, eclipse, fog, and lightning scalar. Dump path is `Docs/AgentLogs/Dump_RENDER_GI_RELAY_SYNC.bin` on non-finite input or SH layout mismatch.
Rejected Alternatives: Managed log strings and growing collections were rejected because they allocate and lose the last-frame binary state.
Scalability potential: Same fixed memory on all tiers. High/Ultra do not increase telemetry allocation.
Hardware Impact: Estimated 1-3 us per SlowTick; fixed memory footprint.

## Compile Check 3
Problem: Final compile verification still cannot prove the relay due an unrelated project compile wall.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`. It emitted one error: `Assets/_Project/Scripts/LaserCutter.cs(1424,66): string does not contain a definition for AsSpan`.
Rejected Alternatives: Editing `LaserCutter.cs` was rejected as outside LIGHTING_TECH and unrelated to GI relay. Stopping with a failed build hidden in chat was rejected; status remains PENDING VERIFICATION on disk.
Scalability potential: No runtime effect.
Hardware Impact: 0 us. GI relay remains pending verification until external compile wall is fixed.

## OMEGA POLISH CHANGES
Problem: Polish audit found the Burst job still used an honest sine daylight curve and scalar divisions where cheaper deterministic approximations were enough.
Solution: Replaced `math.sin` daylight with a triangle-wave day curve; added reciprocal constants for day/depth normalization; changed low-tier SH mode from an integer boolean to a bitmask flag check; kept SH depth as a 1D palette fake; guarded `SystemDispatcher` and `RenderSettingsLifecycleGuard` ambient restores while GI relay owns ambient probe.
Rejected Alternatives: Keeping sine for smoothness was rejected because shader smoothing can hide the triangle curve at 10Hz. Unity batchmode compile during concurrent editor work was rejected; it would collide with other active agents and the live Unity process.
Scalability potential: Low/MX350: four snap states, triangle daylight, 1D depth palette, one-cubemap reflection. Middle: 10Hz SH interpolation. High: same relay with stronger shader smoothing/material response. Ultra: authored cubemap/material overkill using saved cycles, no realtime probe generation.
Hardware Impact: Triangle daylight saves estimated 1-2 us per SlowTick vs sine on i3/MX350. Reciprocal normalization saves sub-us but removes division drift. Bitmask branch has no measurable cost but matches Burst-hotpath policy. Ambient restore guard avoids unexpected `DynamicGI.UpdateEnvironment()` under GI authority.

## OMEGA POLISH STATIC AUDIT
Problem: Need evidence that polish did not introduce banned hot-path patterns.
Solution: Targeted scans over `HectonGIRelaySystem.cs` found no `math.sin`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString(`, or `new SphericalHarmonicsL2`; found expected `(Flags & SHJobLowTierSnapMask) != 0u`. `git diff --check` over touched files reported only CRLF normalization warnings.
Rejected Alternatives: Repo-wide scan only was rejected because vendor/other-agent files create noise unrelated to LIGHTING_TECH.
Scalability potential: No new tier risk.
Hardware Impact: 0 B/frame retained.

## OMEGA CROSS-DOMAIN JUSTIFICATION
Problem: Polish audit asked whether files outside the lighting folder were touched.
Solution: Cross-domain edits were limited to authority boundaries needed to purge ambient writers: `GlobalRegistry*` for `IGIRelaySystem`, `HectonCelestialEngine`/`HectonUnderwaterVisuals` for ambient handoff, and `SystemDispatcher`/`RenderSettingsLifecycleGuard` for restore-path guards. These are direct GI authority interfaces, not feature creep.
Rejected Alternatives: A private lighting singleton was rejected; it would violate the prompt. Event-only handoff was rejected because existing ambient writers needed immediate guard checks.
Scalability potential: One registry authority scales across all tiers and avoids future direct dependency loops.
Hardware Impact: Avoids multi-ms render setting churn and dynamic GI refresh under active relay.

## OMEGA FINAL DIFF
Problem: Final diff must be documented without claiming ownership of unrelated concurrent repository changes.
Solution: GI-owned files added/changed: `Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs`, `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef`, `Assets/_Project/Scripts/Lighting.meta`, `Assets/_Project/Scripts/Lighting/*.meta`, `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`, `Assets/_Project/Scripts/Core/GlobalRegistry.cs`, `Assets/_Project/Scripts/HectonCelestialEngine.cs`, `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`, `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, `Assets/_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs`, `Docs/Tasks/Status_RENDER_GI_RELAY_SYNC.md`, `Docs/AgentLogs/Rationale_RENDER_GI_RELAY_SYNC.md`. `git diff --stat` for tracked touched files showed `6 files changed, 1880 insertions(+), 95 deletions(-)` because new lighting/status/rationale files are untracked and because shared Core files already contain concurrent agent edits.
Rejected Alternatives: Presenting repo-wide `git status` as my diff was rejected; the tree is dirty from many agents.
Scalability potential: No runtime effect.
Hardware Impact: 0 us.

## Compile Check 4
Problem: Core compile result changed after the unrelated `LaserCutter` wall moved.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`; it succeeded with 0 warnings and 0 errors. `HectonGIRelaySystem.cs` is in the newly added `Hecton8.Lighting` asmdef and does not yet have a generated `Hecton8.Lighting.csproj`, so Unity assembly validation remains pending.
Rejected Alternatives: Creating a fake project file was rejected because it would not match Unity asmdef compilation. Forcing Unity batchmode was rejected under concurrent editor/agent activity.
Scalability potential: No runtime effect.
Hardware Impact: 0 us. Status stays PENDING VERIFICATION by prompt and missing Unity assembly validation.

## Decision 20 - Post-Recheck Lightning And Surface Emission Authority
Problem: Static recheck found two authority leaks: `_lightningScalar` could carry a stronger old strike into a later weaker strike, and the relay called `ApplyGIRelaySurfaceEmission` every SlowTick even when the material color and target had not changed.
Solution: Reset `_lightningScalar` after the base probe is restored, start new strikes from their own saturated intensity unless an active two-frame strike is being extended, and track the last underwater visual target so material emission handoff only runs on color/target change. `HectonUnderwaterVisuals.ApplyCrestMaterial` now reapplies the active GI tint after Crest material refreshes, preserving authority without 10Hz redundant material writes.
Rejected Alternatives: Leaving the max scalar persistent was rejected because it violates exact two-frame lightning semantics across separate events. Calling the underwater visual handoff every SlowTick was rejected because Crest refreshes can reassert GI tint locally without relay-driven material churn.
Scalability potential: Low/MX350 avoids repeated material property writes when moon/eclipse emission is stable. Middle/High keep the same GI authority. Ultra can still get richer authored material response because Crest refresh now preserves active GI tint at the material boundary.
Hardware Impact: Estimated 4-12 us saved per stable 10Hz SlowTick when ocean materials exist; lightning correctness cost is 0 us outside active strikes. No `dotnet build` was launched during this recheck per user instruction.

## Decision 21 - Second Recheck API, State Cache, And Material Decay
Problem: Recheck found one probable Unity API mismatch (`RenderSettings.customReflectionTexture`), redundant 10Hz global writes, modulo in telemetry recording, and a material-tint decay bug where max-with-current-color could preserve an old brighter moon/eclipse tint after the relay color fell.
Solution: Switched reflection binding to `RenderSettings.customReflection`, invalidated relay shader caches on enable/disable, gated fog LOD, fauna emissive, and WaterVolume global writes by state change, changed telemetry cursor to branch wrapping with chronological dump order, and made `ApplyGIRelaySurfaceEmission` rebuild Crest base material before applying a changed GI tint.
Rejected Alternatives: Keeping the old reflection property was rejected because Unity exposes `customReflection` as the stable RenderSettings cubemap hook. Keeping max-with-current material color was rejected because it only supports brightening, not decay. Keeping modulo was rejected because the ring size is fixed and branch wrap is cheaper and clearer.
Scalability potential: Low/MX350 gets fewer render-state calls and reversible water tint. Middle/High retain 10Hz relay responsiveness. Ultra can carry richer material overkill without stale brightening artifacts because base material refresh remains the tint authority boundary.
Hardware Impact: Estimated 1-3 us saved per stable SlowTick from guarded globals and branch wrap; 4-12 us material churn remains avoided except on actual emission changes. No `dotnet build` was launched during this pass per user instruction.
