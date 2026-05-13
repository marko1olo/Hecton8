# Rationale_ORBITAL_DROP_REENTRY_VFX

## Baseline Decisions

Problem: The transition must hide a 1-2 second world residency spike without adding physics, scene loads, or runtime allocations.  
Solution: Use the Cinematic Cheat mandate: a camera/window-space plasma shader and scalar state machine, driven by EventBus signals.  
Rejected Alternatives: Full atmospheric particle simulation, volumetric re-entry plasma, coroutine loading screens, and direct `SceneManager.LoadSceneAsync` handoffs. These are too expensive or cross-domain.  
Scalability potential: Low uses solid white/orange fade; Middle enables one procedural layer; High/Ultra enable layered plasma/cloud scatter and stronger HDR.  
Hardware Impact: Low tier i3/MX350 avoids procedural shader branch during chunk hydration; expected CPU gain versus simulated particles: 200-800 microseconds, GPU/fill-rate preserved by one overlay pass.

Problem: The prompt names `WorldHydratedSignal`, but current contracts expose residency hydration through `SectorHydratedSignal`.  
Solution: Consume `SectorHydratedSignal` as the concrete world hydration notification and document the alias in status/logs.  
Rejected Alternatives: Editing dirty core `GlobalSignals.cs` to add a duplicate `WorldHydratedSignal`, or creating a hard dependency on `WorldChunkResidencyManager`. Both increase merge risk across active agents.  
Scalability potential: Signal alias remains cheap on all tiers and allows later integrator remap without VFX rewrites.  
Hardware Impact: No per-frame object search or manager lookup; saves roughly 20-80 microseconds versus runtime discovery on low-end silicon.

Problem: Blackbox telemetry is mandatory, but core telemetry files are already dirty and broad.  
Solution: Implement a local fixed 300-frame native ring in the VFX controller and dump `Dump_ORBITAL_DROP_REENTRY_VFX.bin` on NaN/fault.  
Rejected Alternatives: Patching shared telemetry now would risk conflicting with other agents. Managed logs in hot path are rejected.  
Scalability potential: Identical cost profile on Low through Ultra; fault-only file IO.  
Hardware Impact: Fixed ring write is a few scalar stores; estimated below 2 microseconds per frame on i3/MX350.

Problem: The VFX seam needs prompt-owned prologue packets, but the producer agent is separate and may compile later.  
Solution: Add unmanaged signal structs plus a cold `PrologueReentrySignalLanes.Warm()` prewarm call. The VFX controller consumes snapshots and never references the producer.  
Rejected Alternatives: Direct C# reference to `ORBITAL_MECHANICS_DIRECTOR`, UnityEvents, coroutines, or managed delegates. These create ordering and allocation risk under 20+ agent concurrency.  
Scalability potential: Low through Ultra share identical CPU path; quality is decided in shader by `_PlasmaLowTier`.  
Hardware Impact: Signal snapshots are contiguous native storage; estimated 10-40 us saved versus managed event graph and object lookup on i3/MX350.

Problem: Material creation via Unity tool initially quantized HDR colors as byte-scaled floats.  
Solution: Create the material through Unity asset APIs, then set HDR color properties through `execute_code` with exact float values.  
Rejected Alternatives: Raw `.mat` YAML editing is forbidden-risky and brittle. Standard non-HDR colors were rejected because they flatten the whiteout/plasma read.  
Scalability potential: Low uses same material but skips procedural branch; High/Ultra retain HDR overkill.  
Hardware Impact: Material authoring has no runtime cost; HDR shader intensity buys stronger concealment without extra draw calls.

Problem: Hydration signals can appear before the orbital whiteout because world streaming is global.  
Solution: Consume `SectorHydratedSignal` only as fade authorization after the local phase has entered `Whiteout`; earlier sector hydration is recorded but ignored for fading.  
Rejected Alternatives: Fading on any hydration packet, or hard-coding `WorldChunkResidencyManager` state. Both can break the cinematic seam.  
Scalability potential: Same deterministic state machine across all tiers.  
Hardware Impact: One branch in late-frame; avoids false restart and keeps frame-spike concealment intact.

Problem: The prompt requires water droplets on `INTERNAL_FLOOD_RENDERER`, but a direct method call from re-entry VFX would couple two presentation owners.  
Solution: Add unmanaged `VisorDropletSignal` and let `InternalFloodWaterlineRuntime` consume it through `SignalBus<T>`. Re-entry publishes this lane at the 0.5 fade crossing with `DebrisSpawnSignal(MassiveSplash)`.  
Rejected Alternatives: Direct reference to `InternalFloodWaterlineRuntime`, UnityEvent, or shared static mutable droplet state. Those increase ordering, allocation, and merge risk.  
Scalability potential: Low uses shorter droplet duration and lower debris quantity; High/Ultra get longer visor persistence and heavier splash quantity.  
Hardware Impact: One signal scan in the existing flood tick; estimated below 5 microseconds on i3/MX350 and no managed allocation.

Problem: A procedural shader can become the frame problem it is supposed to hide on MX350.  
Solution: Keep one shader and one material, but branch to a solid HDR white/orange fade when `_PlasmaLowTier` is set. Voronoi and shared-noise sampling stay High/Ultra only.  
Rejected Alternatives: Separate low-tier material asset or CPU-selected shader variants. Variant sprawl and material swaps are unnecessary for one overlay pass.  
Scalability potential: Low = solid fade; Middle = one procedural read path; High/Ultra = layered Voronoi, Rayleigh tint, and shared noise modulation.  
Hardware Impact: Low-tier path skips 18 Voronoi cell evaluations and one texture sample per pixel; estimated 0.05-0.25 ms GPU saved depending overlay resolution.

Problem: The VFX lane warmer originally used local mnemonic hashes for atmospheric/completion packets while `GlobalSignals` owns those packet contracts.  
Solution: Align cold prewarm capacity and FNV lane hashes with `GlobalSignals` for `AtmosphericReentrySignal` and `PrologueCompleteSignal`; only VFX-owned state/droplet lanes keep VFX-local capacity decisions.  
Rejected Alternatives: Leaving local hash aliases (`ATRY`, `PCOM`) would perturb signal telemetry under 20+ agents. Forcing `GlobalSignals` edits was rejected because that file is already dirty and core-owned.  
Scalability potential: Low through Ultra get identical queue topology; telemetry remains stable for postmortem tooling.  
Hardware Impact: Runtime neutral after cold prewarm; avoids late NativeQueue allocation during the whiteout spike and preserves the existing 32/8 core packet budgets.

Problem: Lighting must shift from orbital black to ocean cyan without invoking expensive volumetrics or extra scene state.  
Solution: Drive `RenderSettings.ambientLight` and `RenderSettings.ambientProbe` L0 coefficients from a cached scalar over the configured 2 second transition.  
Rejected Alternatives: Dynamic skybox swap, realtime reflection probe refresh, or volumetric atmosphere were rejected as too slow and cross-domain.  
Scalability potential: Low gets the same cheap cyan readability; High/Ultra rely on shader HDR overkill while ambient remains deterministic.  
Hardware Impact: Scalar lerp plus one cached ambient write when changed; estimated below 5 microseconds on i3/MX350 and no extra render pass.

Problem: AUP origin shifts during ocean handoff can invalidate world-space VFX and expose the loading seam.  
Solution: Keep the plasma overlay in camera-local space at a clamped 0.08 m local Z and use `ZTest Always` in the shader. Only signal payloads carry AUP for telemetry/audio/debris handoff.  
Rejected Alternatives: World-space plasma shell around the capsule or atmospheric billboard clouds; both risk precision jitter and culling errors during rebases.  
Scalability potential: Low through Ultra retain identical culling safety; quality changes are shader-only.  
Hardware Impact: One local transform correction when drift is detected; avoids background overdraw during full whiteout and can save GPU time during chunk load.

Problem: The prompt requires sharing noise with `ABYSSAL_CURRENT_ADVECTION` without spending new VRAM on a re-entry texture.  
Solution: Sample the existing global `_HectonPrebakedVectorNoise3D` produced by `HectonFluidEngine`; `_SharedNoiseTex` remains optional/null and falls back to shader defaults.  
Rejected Alternatives: Importing a bespoke plasma flipbook, creating a runtime Texture2D, or baking a separate Voronoi LUT. These add VRAM and asset churn for a one-pass screen concealment.  
Scalability potential: Low bypasses noise entirely; Middle/High/Ultra reuse the fluid vector noise for plasma breakup.  
Hardware Impact: Saves a unique 2D/3D plasma texture allocation; High-tier cost is one shared 3D texture sample per pixel only outside low-tier mode.

Problem: Zero-GC proof must separate cold setup from the hot late-frame path.  
Solution: Hot path uses spans from `SignalBus<T>`, scalar math, cached shader/property writes, fixed NativeArray telemetry, and unmanaged signals. `rg` found no `Instantiate`, `LoadScene`, coroutine, LINQ, or per-frame managed collections in the re-entry controller.  
Rejected Alternatives: UnityEvents, coroutine fades, managed log traces, particle allocations, and loading-screen scene operations.  
Scalability potential: Low through Ultra share allocation-free frame behavior; only shader branch cost changes.  
Hardware Impact: Expected managed allocation per frame: 0 bytes. Cold allocations are documented NativeArray/NativeQueue setup and material asset authoring outside the spike.

Problem: Blackbox requirements need postmortem state without relying on dirty shared telemetry code.  
Solution: `OrbitalDropReentryVfxController` writes every late-frame scalar state into a fixed 300-entry NativeArray ring and mirrors high-level state through `ReentryVfxStateSignal`; NaN/fault paths write `Docs/AgentLogs/Dump_ORBITAL_DROP_REENTRY_VFX.bin`.  
Rejected Alternatives: Managed rolling logs, `List<T>` telemetry, or patching shared crash telemetry during a dirty multi-agent batch. These create allocation or merge risk.  
Scalability potential: Identical data across Low/Middle/High/Ultra; higher tiers do not get special hidden state.  
Hardware Impact: One 48-byte struct write per frame; estimated below 2 microseconds on i3/MX350, fault-only disk IO.

Problem: A one-shot ocean audio event does not satisfy the prompt's crossfade wording.  
Solution: During hydrated fade, emit bounded `AcousticPingSignal` packets every 0.1 seconds for 2 seconds: `PlasmaRoar` intensity descends while `OceanWaves` intensity rises. The handoff remains EventBus-only.  
Rejected Alternatives: Direct mixer automation, audio source references, coroutines, or long-lived managed fade objects. Those cross ownership boundaries and allocate.  
Scalability potential: Low through Ultra use the same low-cadence scalar stream; top-tier audio systems can interpret the denser signal shape for richer procedural response.  
Hardware Impact: Roughly 40 unmanaged signal packets over the full 2 second crossfade; amortized CPU cost is below 10 microseconds per packet and no managed allocation.

Problem: The plasma pass must hide all background during residency without fighting depth or creating a shader compile failure.  
Solution: URP transparent overlay pass uses `Queue=Overlay`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `ZTest Always`, and `Cull Off`. `ShaderUtil.GetShaderMessages` reports zero messages and `shader.isSupported=True`.  
Rejected Alternatives: Depth-tested cockpit glass material, camera stack overlay scene, or stencil setup. These are more fragile during streaming.  
Scalability potential: Low gets full-screen solid fade; High/Ultra get procedural plasma using the same transparency contract.  
Hardware Impact: `ZTest Always` with near camera-local placement removes depth uncertainty and can prevent background shading from visually leaking during whiteout; estimated savings depend on URP renderer order but protects the 1-2 second load seam.

## OMEGA POLISH CHANGES

Problem: The high-tier shader still used honest distance functions after the low-tier branch.  
Solution: Replaced Voronoi `sqrt(best)` with squared-distance plasma cells, replaced radial `length(centered)` with `dot(centered, centered)`, and replaced `pow(x, 2)` cloud shaping with `x * x`.  
Rejected Alternatives: 1D LUT asset was rejected because it adds VRAM/import state; squared-distance plasma keeps the same one-pass shader and uses the existing shared noise path.  
Scalability potential: Low bypasses this entirely; Middle/High/Ultra get cheaper high-tier breakup with no variant or texture cost.  
Hardware Impact: Removes two Voronoi square roots plus one radial square root and one pow per high-tier pixel. Estimated GPU savings: 0.01-0.08 ms depending overlay resolution and tier.

Problem: Polish required proof that domain boundaries and zero-GC constraints were still intact.  
Solution: Re-read the prompt and Polish Mandate, re-ran scoped scans for `sqrt`, `normalize`, `length`, `pow`, `foreach`, `string.Format`, `.ToString()`, string interpolation, `LoadScene`, `StartCoroutine`, and `Instantiate` on owned files. No matches remain for shader sqrt/normalize/length/pow; `Select-String` found no managed foreach/string-format/interpolation debt in owned files.  
Rejected Alternatives: Broad repo-wide cleanup was rejected because the worktree contains many other agents' edits and third-party packages outside this domain.  
Scalability potential: Same as core implementation; only the high-tier shader got cheaper.  
Hardware Impact: Hot-path managed allocation remains 0 B/frame; shader ALU cost is lower on non-low tiers.

Problem: Final build verification is globally red.  
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` after Omega polish. It still fails on external assembly/reference walls (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `SoundEmissionSignal`, `MacroSwarm`, `AcousticAup`, etc.); visible diagnostics did not name the re-entry VFX files. Unity MCP lost its active Unity instance after refresh, so post-polish in-editor shader compile could not be rerun.  
Rejected Alternatives: Editing global compile dependencies is outside the VFX domain and would collide with active agents.  
Scalability potential: Status remains pending on global integration, not VFX architecture.  
Hardware Impact: No runtime cost; this is an integration blocker only.

### Cinematic Cheats Used
- Camera-local plasma overlay with `ZTest Always` instead of atmospheric simulation.
- Solid HDR white/orange low-tier fade instead of procedural fire on MX350.
- Squared-distance Voronoi/radial fakes instead of exact square-root distances.
- L0 ambient probe cyan blend instead of realtime sky/probe refresh.
- Shared `_HectonPrebakedVectorNoise3D` instead of a unique plasma noise texture.

### Final Git Diff
- Added: `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef`
- Added: `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs`
- Added: `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs`
- Added: `Assets/_Project/Art/Shaders/Hecton_OrbitalDropReentryPlasma.shader`
- Added: `Assets/_Project/Art/Materials/VFX/MAT_OrbitalDropReentryPlasma.mat`
- Added: `Docs/Tasks/Status_ORBITAL_DROP_REENTRY_VFX.md`
- Added: `Docs/AgentLogs/Rationale_ORBITAL_DROP_REENTRY_VFX.md`
- Added: `Docs/AgentLogs/LOG_ORBITAL_DROP_REENTRY_VFX.md`
- Modified: `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs` (+28 lines tracked diff)
- Note: Unity-generated `.meta` files for new assets may be present in the worktree; unrelated dirty files were not touched.
