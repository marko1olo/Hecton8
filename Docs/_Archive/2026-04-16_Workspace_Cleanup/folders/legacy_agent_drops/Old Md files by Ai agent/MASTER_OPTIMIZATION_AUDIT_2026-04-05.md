# HECTON-8 Master Optimization Audit

Date: `2026-04-05`
Scene: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
Unity: `6000.4.1f1`
Target hardware: `i5-1135G7 + NVIDIA MX350 2 GB VRAM`
Status: `PENDING VERIFICATION`

## Scope

This document is a fact-only audit of:
- current CPU/runtime bottlenecks
- current memory / VRAM state
- runtime architecture risks
- optimization priorities that do not blunt the visual target

No gameplay code was changed in this pass.

---

## Source Matrix

| Source | State | Trust level | Notes |
|---|---|---:|---|
| `PERF_DIAGNOSTIC_BUNDLE_2026-04-04.md` | historical validated runtime logs | High | Best source for scatter / slow-tick timing history |
| Unity MCP `Memory` / `Render` counters | current editor idle | Medium | Real current editor state, but editor-heavy |
| Unity MCP short play probe #1 | live runtime sample | Medium | Probe captured while MCP still reported `playmode_transition` |
| Unity MCP short play probe #2 | live runtime sample | Medium | Better warmup length, but MCP still reported transition state |
| Unity MCP `FrameTimingManager` samples | live runtime sample | Medium | Useful for non-SlowTick frame shape, but still captured in `playmode_transition` |
| Unity MCP editor windows readback | current editor truth | High | Confirms profiler/editor-window contamination is real, not guessed |
| Unity MCP console + profiler-hook readback | live runtime sample | Medium | Good for tooling diagnosis; not proof of gameplay cost on its own |
| Scene object readback via Unity MCP | live scene truth | High | Used for HUD/visor camera and RT path verification |
| `CURRENT_SESSION_HANDOFF.md` / runtime audit docs | historical engineering context | Medium | Useful for what was already hardened, not final truth |

---

## Memory / VRAM Table

### Historical context already in repo

| Metric | Older snapshot in bundle |
|---|---:|
| Total Used / Resident | `6.73 GB` resident |
| Total Allocated | `11.37 GB` |
| Native | `4.65 GB` |
| Managed | `3.55 GB` |
| Graphics (estimated) | `0.68 GB` |
| Textures | `0.80 GB` |
| Render Textures | `426.5 MB` |
| GC Alloc in frame | `2.5 KB` |

### Current editor idle sample

| Metric | Value |
|---|---:|
| Total Used Memory | `9.18 GB` |
| Total Reserved Memory | `12.91 GB` |
| App Resident Memory | `8.19 GB` |
| Texture Memory | `2.34 GB` |
| Gfx Used Memory | `2.37 GB` |
| Video Memory Bytes | `2.06 GB` |
| Render Textures | `1325` |
| Render Texture Bytes | `1.91 GB` |
| GC Reserved Memory | `3.90 GB` |
| GC Used Memory | `1.70 GB` |
| Profiler Used Memory | `1.83 GB` |
| GC Allocated In Frame | `12.6 KB` |

Verdict:
- editor idle is catastrophically inflated for MX350 if taken as product truth
- this sample is useful only as a red flag and for editor-overhead diagnosis

### Live play probe #1

| Metric | Value |
|---|---:|
| Total Used Memory | `7.69 GB` |
| App Resident Memory | `7.98 GB` |
| Texture Memory | `1.08 GB` |
| Gfx Used Memory | `1.11 GB` |
| Render Textures | `659` |
| Render Texture Bytes | `644.67 MB` |
| GC Reserved Memory | `3.90 GB` |
| GC Used Memory | `1.43 GB` |
| GC Allocated In Frame | `2032 B` |
| CPU Total Frame Time | `6.19 ms` |

Notes:
- far lower than editor idle
- still captured while MCP reported `playmode_transition`

### Live play probe #2

| Metric | Value |
|---|---:|
| Total Used Memory | `7.52 GB` |
| App Resident Memory | `7.94 GB` |
| Texture Memory | `966.09 MB` |
| Gfx Used Memory | `977.07 MB` |
| Render Textures | `611` |
| Render Texture Bytes | `531.42 MB` |
| GC Reserved Memory | `3.90 GB` |
| GC Used Memory | `1.43 GB` |
| GC Allocated In Frame | `29.7 KB` |

Notes:
- better than probe #1 on textures / gfx / RTs
- GC frame sample was materially worse than probe #1
- MCP still reported `playmode_transition`, so this is directional, not gold-standard runtime truth

### Real conclusion from the table

1. Editor snapshots were overstating product VRAM pressure.
2. Even after removing editor inflation, runtime memory is still too high for the MX350 target.
3. The dangerous values in live play are:
   - `Texture Memory ~ 966 MB`
   - `Gfx Used ~ 977 MB`
   - `RenderTexture Bytes ~ 531 MB`
4. On a `2 GB` MX350, that leaves too little real headroom once backbuffers, driver overhead, transient RTs, ocean/URP passes, and spikes are included.
5. `GC Reserved ~ 3.90 GB` and `GC Used ~ 1.43 GB` during play are still abnormal for the target class machine, even allowing for editor pollution.

### Additional live variance found during follow-up probing

| Metric | Follow-up sample A | Follow-up sample B |
|---|---:|---:|
| Texture Memory | `949.03 MB` | `2.07 GB` |
| Gfx Used Memory | `959.28 MB` | `2.11 GB` |
| Total Used Memory | `6.99 GB` | `8.18 GB` |
| GC Reserved Memory | `3.63 GB` | `3.63 GB` |
| GC Used Memory | `1.34 GB` | `1.35 GB` |
| GC Allocated In Frame | `20.2 KB` | `45.0 KB` |
| Render Textures | `602` | `1288` |
| Render Texture Bytes | `488.80 MB` | `1.68 GB` |

Important reading:
- follow-up sample A looked similar to the earlier lighter play probes
- follow-up sample B jumped back toward editor-heavy pressure
- both were still captured while MCP reported `playmode_transition`
- practical conclusion: current play telemetry is materially unstable because the editor/tooling state is unstable

That does not make the lighter numbers â€œcorrectâ€ or the heavier numbers â€œfakeâ€.

It means the current session can move between two very different render-memory postures depending on editor/tooling state, and that alone is dangerous for MX350 headroom.

---

## CPU Table

### Latest validated runtime from bundle

| Metric | Latest validated |
|---|---:|
| SlowTick total spike | `104.71 ms` |
| `WorldProceduralScatterDirector` | `84.77 ms` |
| `FaunaDirector` | `11.52 ms` |
| `ScavengePopulator` | `1.97 ms` |
| `MapMagicBridge` | `0.90 ms` |
| Scatter rebuild | `69.36 ms` |
| Scatter sample | `51.37 ms` |
| Scatter post | `29.04 ms` |
| Scatter reconcile | `10.49 ms` |
| Scatter spawn | `4.29 ms` |
| Scatter rescue | `6.78 ms` |
| Desired placements | `8` |
| Active placements | `0` |

### Hard conclusion

1. The primary runtime offender is still `WorldProceduralScatterDirector`.
2. The project is not currently GPU-bound in the captured worst hitches.
3. `FaunaDirector` improved materially, but remains secondary pressure.
4. `active=0` with `desired>0` is still a structural bad signal:
   - expensive rebuild work
   - weak visible product output
5. `rescue` regression is real and unresolved.

### Non-SlowTick frame timing snapshots

| Sample | CPU frame | Main thread | Render thread | GPU |
|---|---:|---:|---:|---:|
| live frame A | `17.50 ms` | `11.12 ms` | `4.08 ms` | `11.25 ms` |
| live frame B | `7.48 ms` | `5.50 ms` | `1.90 ms` | `4.79 ms` |
| live frame C | `18.70 ms` | `11.49 ms` | `10.09 ms` | `14.32 ms` |

What this proves:
- outside the huge `SlowTick` spikes, the frame still carries real render-thread and GPU cost
- the frame is not shaped like â€œscripts only and rendering irrelevantâ€
- but these numbers are still nowhere near the `100+ ms` stall class seen on the scatter spikes

Hard reading:
- `WorldProceduralScatterDirector` remains the main explanation for the worst freezes
- render thread / GPU / camera stack remain the main explanation for why the game can still feel heavy even between those spikes

---

## Live Scene Render Stack

### Active render features on `PC_Renderer`

| Feature | Active |
|---|---:|
| `ScreenSpaceAmbientOcclusion` | Yes |
| `ShapesRenderFeature` | Yes |
| `DecalRendererFeature` | Yes |
| `ScreenSpaceShadows` | Yes |

### Active volumes

| Volume | Effects |
|---|---|
| `Global Volume` | `Tonemapping`, `Bloom`, `Vignette`, `ColorAdjustments` |
| `Main Camera Profile` | `ChromaticAberration`, `Vignette` |

### HUD / visor path verified in live scene

| Object | Verified fact |
|---|---|
| `HUD_Render_Camera` | inactive object, still configured and referenced correctly |
| `HUD_Render_Camera` | `allowHDR=false`, `allowMSAA=false`, `renderShadows=false`, `requiresDepth=false`, `requiresColor=false`, `allowXRRendering=false`, `allowHDROutput=false` |
| `Suit_Visor` | uses `VisorHUDController` in `SharedRenderTexture` mode |
| Shared RT asset | only explicit first-party RT asset found: `RT_HUD_Display.renderTexture` |
| Shared RT asset size | `1280x720`, no mipmaps, no MSAA |
| `Suit_HUD_Canvas` | active overlay path is `SuitHUDV4CanvasOverlay` |

### Active camera truth from live scene

| Camera | Role | Important facts |
|---|---|---|
| `SpaceCamera` | active base camera | `renderPostProcessing=false`, `renderShadows=false`, `requiresDepth=false`, `requiresColor=false`, camera stack contains `Main Camera` |
| `Main Camera` | active overlay camera | has `UnderwaterRenderer`, active local `Volume`, `renderPostProcessing=true`, `renderShadows=true`, `requiresDepth=true`, `requiresColor=true` |
| `HUD_Render_Camera` | inactive secondary camera | optimized flags are still correct; not the active source of the current RT pressure |

Important implication:
- the active runtime stack is not a single-camera story
- `Main Camera` is still carrying Crest underwater + post stack + depth/color requirements
- `SpaceCamera` + `Main Camera` together are a more credible ongoing render-cost source than the currently inactive HUD camera alone

### Important implication

The first-party visor path is no longer the main smoking gun for the absurd editor-side RT number.

The current explicit first-party RT setup is already relatively restrained.

Most of the huge RT footprint is therefore likely coming from:
- editor/profiler overhead
- URP internal render targets
- ocean / post-processing / camera pipeline transients
- volume / camera stack side effects

That does not make the problem safe. It only changes where to investigate next.

---

## Tooling / Profiling Hygiene Findings

### Editor contamination is now confirmed, not guessed

Live editor windows during the probe included:
- `Profiler`
- `Project Auditor`

---

## 2026-04-05 Asset Memory Pass

### Confirmed importer problem

Large first-party sky / cloud textures were still imported with:
- `enableMipMap: 1`
- `streamingMipmaps: 0`
- `isReadable: 0`

That is a bad fit for the MX350 target because it keeps full mip chains resident longer than necessary while giving back none of the CPU-side readability cost.

### What was changed

Enabled `streamingMipmaps: 1` on these first-party assets:
- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
- `Assets/_Project/Art/TEXTURES/clouds.png`
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/TEXTURES/Sky/eb2.png`
- `Assets/_Project/Art/TEXTURES/Sky/bo3.png`
- `Assets/_Project/Art/TEXTURES/Sky/bo2.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod1.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod2.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
- `Assets/_Project/Art/Skyboxes/panorama_den.png`
- `Assets/_Project/Art/Skyboxes/panorama_noch.png`
- `Assets/_Project/Art/Skyboxes/panorama_shtorm.png`

### Why this pass is safe

This pass did **not**:
- reduce source resolution
- disable mipmaps
- change texture format
- change shaders
- alter scene or prefab wiring

It only allows Unity to stream already-existing mip chains instead of pinning them harder than necessary.

### Additional readback from the scan

- Active first-party sky textures inspected were already clamped to `maxTextureSize: 2048`
- No `isReadable: 1` texture importers were found under the active `_Project/Art` texture set that was inspected in this pass
- `isReadable: 1` hits that did appear were in `_PROLOGUE_CONTENT` mesh importer data, not in the main world sky texture stack

### What is still not proven

- No clean before/after VRAM numbers were captured yet because the editor capture path remains contaminated
- This pass is designed to reduce residency pressure, but current status remains `PENDING VERIFICATION` until a clean runtime capture confirms movement in `Texture Memory` / `Gfx Used Memory`

### Follow-up root surface pass

Applied the same importer discipline to additional large first-party surface textures that were still carrying `streamingMipmaps: 0`:
- `Assets/_Project/Art/TEXTURES/terrain.png`
- `Assets/_Project/Art/TEXTURES/FLOOR.png`
- `Assets/_Project/Art/TEXTURES/FLOOR1.png`
- `Assets/_Project/Art/TEXTURES/Meshy_AI_Alien_barnacles_clust_0301230506_texture.png`

Important limitation:
- text-side GUID search did not prove these are direct YAML dependencies of `02_HECTON_WORLD`
- Unity `execute_code` dependency probing is currently unreliable in this session because MCP keeps failing with `mono.exe: The filename or extension is too long`

Interpretation:
- these importer changes remain low-risk because they still do not cut resolution or alter formats
- but their product impact is less certain than the confirmed sky/cloud streaming pass above

---

## 2026-04-05 Scatter CPU Pass

### Confirmed waste removed

In `WorldProceduralScatterDirector`, the rescue/injection tail was re-scanning `_desiredPlacements` to rebuild `_occupiedCellBuffer` multiple times for the same cluster layer inside a single rebuild pass.

That repeated work was structurally redundant because:
- the active layer did not change during those cluster rescue stages
- newly accepted rescue placements were already appended into `_occupiedCellBuffer`
- no rescue-path removal step invalidated the buffer in between

### What was changed

Added a tiny occupied-buffer validity cache in `WorldProceduralScatterDirector`:
- invalidate once at the start of `ClearScatterWorkingBuffers()`
- rebuild `_occupiedCellBuffer` only when the requested layer changes or the cache is invalid
- reuse the same cluster occupied set across:
  - cluster accent rescue injection
  - generic cluster rescue injection
  - exact preferred cluster family rescue injection

### Why this is safe

This pass did **not** change:
- placement scoring
- candidate ordering
- budgets
- rescue targets
- family selection rules

It only removes redundant occupied-cell reconstruction work.

### Verification state

- Unity returned to ready state after compile
- no new first-party compile errors were surfaced
- console still contains the same editor/package noise as before
- no clean `WorldScatterProfiler` before/after numbers are available yet in a clean runtime session

Status remains `PENDING VERIFICATION`.

### Additional scatter rescue pass

Removed another deterministic waste path in `WorldProceduralScatterDirector`:
- `clusterCandidates` was being sorted repeatedly across sequential cluster rescue stages inside the same rebuild
- preferred cluster family injection and generic/pattern cluster rescue were reordering the same candidate set again even though the source dictionary had not changed

What changed:
- sort ordered cluster rescue candidates once in `InjectRescuePlacementsIfNeeded`
- pass that ordered list through the cluster rescue helpers instead of rebuilding the same ordering again

Why this is safe:
- no scoring change
- no candidate inclusion change
- no placement budget change
- only repeated ordering work was removed

Status is still `PENDING VERIFICATION` until a clean runtime capture confirms movement in scatter rebuild / post / rescue timings.
- `MCP For Unity`
- `Scene`
- `Game`
- `Console`
- two `Inspector` windows

At the same time, Unity MCP kept reporting:
- `play_mode.is_playing=true`
- `play_mode.is_changing=true`
- `activity.phase=playmode_transition`
- `reasons=[tick]`

Hard implication:
- the editor never settled into a clean post-transition runtime state during the probe
- current RT / memory / frame telemetry is therefore contaminated by active editor observation, not only by the game

### Console-noise localization is clearer now

Current high-noise entries were:
- `Lifecycle ERROR ... NullReferenceException`
- multiple `SerializedObjectNotCreatableException` from URP volume editors
- `Assembly Definition cannot be found for Assembly-CSharp-firstpass`
- `Assembly Definition cannot be found for Domain_Reload`

What was disproven:
- `manage_scene validate` on `02_HECTON_WORLD` returned `missingScripts=0`
- so the earlier flood of `The referenced script (Unknown) on this Behaviour is missing!` is not currently proven to come from the active world scene itself

This matters because it narrows the contamination suspect list toward:
- editor tooling / inspectors / package editor code
- non-scene objects
- other loaded assets or transient editor-created objects

### Built-in dev runtime profiler hook is currently not operational

Facts:
- `__DEV_RuntimePerformanceProfiler` exists in the active world scene and is active in hierarchy
- I switched its dev settings for a live probe without saving the scene
- in play, readback still showed:
  - `_debugProfilingActive=false`
  - all stat names still `Unresolved`
  - `_debugTraceFilePath=None`
  - `_debugWindowCount=0`
- MCP `execute_code` failed twice with:
  - `mono.exe: The filename or extension is too long`
- `manage_components.set_property` cannot adjust that component while in play mode

Hard conclusion:
- the project has a good profiler hook on paper
- through the current MCP toolchain, that hook is not yet a reliable operational capture path
- for now, external profiler counters remain the only trustworthy automated data path

---

## Codebase Footprint

Repo scan of `Assets/_Project/Scripts`:

| Area | Count |
|---|---:|
| Total `.cs` files | `299` |
| Root runtime folder | `224` |
| `Editor` | `38` |
| `UI` | `15` |
| `Gameplay` | `6` |
| `Tools` | `5` |
| `Interaction` | `5` |
| `Visor` | `3` |
| `Input` | `2` |
| `Items` | `1` |

This is not a small project anymore. Optimization must be systemic, not “fix one file and declare victory”.

---

## Runtime Architecture Findings

| Area | Facts | Risk |
|---|---|---|
| `WorldProceduralScatterDirector` | still owns the main validated CPU stall; huge class with scatter, reconcile, rescue, spawn, diagnostics, pattern logic in one runtime surface | Very high |
| `FaunaDirector` | big system, improved, but still secondary offender and still tied to pool pressure / biome / registry complexity | High |
| `MapMagicBridge` | bootstrap and tile caching improved; still bridge-critical for terrain/biome truth | Medium |
| `WorldRuntimeReferenceUtility` | shared cache helper reduces repeated searches, but still falls back to `GameObject.Find*` and `FindAnyObjectByType` when bootstrap is not ready | Medium |
| `GameTickManager` | architecture is correct for the project, but slow-tick spike logging still adds observability noise and editor coupling when profiling | Medium |
| HUD / visor stack | modern active path is much cleaner now; legacy HUD files still exist and must stay clearly retired | Medium |
| Runtime search debt | many remaining `Find*` hits in non-editor scripts are smoke testers, bootstrap, or diagnostics, not hot-loop faults; still noise and startup pressure | Medium |
| Native update debt | true gameplay runtime mostly moved to tick interfaces, but stragglers remain in visual helpers, profiler code, and legacy/deprecated classes | Medium |

---

## Search / Update Debt Snapshot

### Update-like methods still surfaced by grep in non-editor scripts

Real or probable live methods still present in runtime-side files include:
- `GameTickManager`
- `BuoyancyObject`
- `SkySystemFollowCamera`
- `PlayerThrusterAudio`
- `RuntimePerformanceProfiler`
- `HectonSuitHUD_v4`
- `HUDQuickBar`
- `HUDNotification`

Also surfaced:
- deprecated / legacy files
- comment-only matches

Conclusion:
- the project is much cleaner than a normal Unity codebase
- but native update debt is not fully gone
- the remaining debt is concentrated in visual helpers, diagnostics, and old HUD paths

### Search debt still surfaced in non-editor runtime-side scripts

Real remaining runtime-side search users include:
- `MapMagicBridge`
- `WorldRuntimeReferenceUtility`
- `SceneBootstrap`
- `HectonPlayerSpawner`
- `SkySystemFollowCamera`
- `RuntimePerformanceProfiler`
- multiple smoke testers

Conclusion:
- most hot-path search debt has already been attacked
- the remainder is mainly bootstrap, fallback, diagnostics, or smoke infrastructure
- this is worth cleaning, but it is not the current top CPU villain

---

## What Is Actually Wrong Right Now

### 1. CPU problem

`WorldProceduralScatterDirector` is still too expensive for the target hardware.

The project already proved that this system can move numbers a lot, but the remaining cost is still unacceptable because:
- `sample` is too expensive
- `post` is too expensive
- `active=0` while work is still paid
- `rescue` regressed

### 2. VRAM / RT problem

The first-party shared HUD RT is not the whole problem.

The real problem is broader:
- too many render textures even in play probe
- too much texture memory for the target
- too much combined gfx + RT budget for MX350 headroom

### 3. Managed memory problem

The play probe still shows:
- `GC Reserved ~ 3.90 GB`
- `GC Used ~ 1.43 GB`

That is too large for comfort even after discounting editor overhead.

This is not the same as “GC per frame is the main hitch”. It is not.

It means the managed heap posture of the running project is heavy and risky.

### 4. Profiling clarity problem

Console noise still contains:
- `LifecycleManagement ... NullReferenceException`
- multiple `SerializedObjectNotCreatableException`
- Project Auditor warnings

These are likely editor-side, but they contaminate trust in profiling sessions and slow iteration.

### 5. Tooling path problem

The project already has a scene-level runtime profiler hook, but the current measurement workflow is still partly broken because:
- Unity MCP keeps observing the session as `playmode_transition`
- the dev profiler hook did not auto-start in the live run
- MCP runtime code execution failed on the current machine with a path/filename-length error

That means some of the remaining optimization work is blocked not by lack of code, but by a weak capture path.

---

## Strategic Priority Order

### Priority 1: scatter CPU, not generic cleanup

Continue targeting:
- `WorldProceduralScatterDirector.sample`
- `WorldProceduralScatterDirector.post`
- `active=0 / desired>0`
- `rescue` regression

Reason:
- this is still the only repeatedly validated gameplay stall with hard numbers

### Priority 2: real RT inventory, camera by camera

Do not keep treating RT pressure as one visor problem.

Need a live inventory of:
- every camera active in play
- every temporary RT class created by URP / Crest / post stack
- whether any per-camera post stack can be stripped on secondary cameras
- whether internal Scene/Game views and profiler windows are distorting current editor measurements

### Priority 3: managed memory posture

Need a dedicated investigation into why play still keeps:
- high GC reserved
- high GC used

Probable buckets to validate:
- editor/profiler contamination
- scriptable/object lifetime retention
- cached content that is justified vs unjustified
- inactive-but-live systems loaded too early

### Priority 4: profiling hygiene

Until editor-side lifecycle/volume noise is separated cleanly, every runtime pass wastes time arguing with contaminated telemetry.

### Priority 5: make the capture path operational again

Need one of these to become reliable:
- clean long play profiling with minimized editor windows
- working scene `RuntimePerformanceProfiler` hook
- live memory snapshot from play that is not polluted by editor-transition state

---

## Safe Next Actions

| Order | Action | Why |
|---|---|---|
| 1 | Capture one clean long play session with profiler attached but editor noise minimized | current live probes are useful but not clean enough |
| 2 | Record stable counters after `20-30s` in play and after a known scatter rebuild | needs a real runtime baseline |
| 3 | Audit active render-target producers by camera / renderer feature / ocean pass | RT budget still too high |
| 4 | Investigate why scatter spends work while `active=0` | this is direct wasted CPU |
| 5 | Inspect `GC Reserved` and large managed owners in Memory Profiler snapshot taken from live play, not editor idle | current heap posture is too heavy |
| 6 | Stabilize the measurement path: close editor-heavy windows, stop `playmode_transition` probes, or restore a working runtime profiler hook | current telemetry is too unstable |
| 7 | Keep legacy HUD / deprecated controllers retired and out of scene truth | avoid hidden visual/runtime drift |

---

## Non-Negotiable Conclusions

1. The project has already made real CPU progress.
2. The main CPU problem is still scatter, not generic GC theory.
3. The project is not currently telling one clean VRAM story:
   - editor numbers are much worse
   - live play numbers are better
   - live play numbers are still too heavy for MX350 safety
4. The explicit first-party RT setup is not the main cause of the worst RT totals anymore.
5. The project now needs measurement discipline more than random micro-refactors.

---

## Current Position

The project is no longer in “everything is broken” territory.

It is in a more dangerous territory:
- several systems are already partially optimized
- the remaining bottlenecks are narrower
- wrong next steps can now waste a lot of time without moving the real product numbers

Status remains `PENDING VERIFICATION`.
