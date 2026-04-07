# HECTON-8 — BUILD / PLAYTEST ISSUES LEDGER

Status: `PENDING VERIFICATION`
Ledger Start Date: `2026-04-05`

## Purpose

This file tracks confirmed build and playtest observations.

Rules:

- Only log real observations from builds, live runs, or manual playtests
- Do not log abstract ideas here
- Do not mark anything fully solved without new evidence
- Every item remains `PENDING VERIFICATION` until a new build or user check confirms the fix
- Player build is the main arbiter, not editor feel
- Use `[c]` for code-fixed issues that are closed for current coding work but still await build or user confirmation
- Use `[x]` only after new proof from build, live run, or explicit user confirmation

## Entry Template

```md
## Build Entry — YYYY-MM-DD — Build Name / Version
- Build Size:
- Scene:
- Hardware:
- General Feel:
- Main Irritant:
- Main Visual Flaw:
- Main UX Flaw:
- Main Content Gap:
- New Blocker: yes / no

### [ ] Issue Name
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Need User Check: yes / no
- Need Build Check: yes / no
- Need In-World Swim Check: yes / no
- Why:
- Evidence:
- Problems:
- Short Comment:
- Next Step:

- Did:
- Result:
- Failed:
- Broke:
- Remaining:
```

## Build Entry — 2026-04-05 — User Build Report
- Build Size: `~500 MB`
- Scene: `02_HECTON_WORLD`
- Hardware: `MX350 target context`
- General Feel: smoother than editor; underwater base feel already promising even before real content fill
- Main Irritant: surfacing hitch and broken oxygen refill
- Main Visual Flaw: gas giant depth illusion and blurry terrain/rocks in close-up
- Main UX Flaw: pause cursor missing; pause buttons not yet fully audited
- Main Content Gap: underwater world still lacks full life, caves, ruins, and density layers
- New Blocker: `yes`

### [c] Surface Transition Hitch
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: this is an immediate feel-breaker during normal play
- Evidence: user report says the game can hitch when moving from underwater to above water while turning the camera
- Problems: editor is not reliable as final truth because build is smoother overall
- Short Comment: code fix accepted; closed for current coding work, waiting for build proof
- Next Step: build swim verification while rotating camera across the surface

- Did: diagnosed live runtime mismatch at the waterline (`Atmosphere=UNDERWATER` while `Visuals=false`, `Movement=false`, `Survival depth≈0`) and replaced it with one shared hysteresis contract based on `HectonPlayerMovement.CurrentDepth` for atmosphere, underwater visuals, and survival.
- Result: editor runtime no longer splits state at the surface boundary; current readback keeps `Atmosphere=surface`, `Visuals=false`, `Survival depth≈0.0049` on the same near-surface frame instead of contradictory surface/underwater states.
- Failed: build verification not run yet; could not force a scripted underwater transition sweep because Unity MCP runtime code execution fails on this machine (`mono.exe: filename or extension is too long`).
- Broke: no compile errors from the patch; console still shows unrelated warnings from `Dynamic Decals` and one generic `Leak Detected : Persistent allocates 8 individual allocations` warning after recompilation.
- Remaining: real swim test in player build while rotating the camera across the surface; confirm hitch is gone under build timing, not just editor runtime. Closed for coding unless new evidence reopens it.

### [c] Surface Oxygen Refill Missing
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: survival trust collapses if surface safety does not work
- Evidence: user report says oxygen does not refill correctly when surfacing
- Problems: likely tied to surface-state truth and crossing logic
- Short Comment: code fix accepted; closed for current coding work, waiting for build proof
- Next Step: build swim verification with depleted oxygen and natural surfacing

- Did: survival oxygen flow now uses the same shared surface hysteresis contract and explicit surface refill path instead of unconditional underwater-style drain.
- Result: refill logic is now present in gameplay code and bound to the same surface truth used by atmosphere and visuals; near-surface runtime readback holds the player in surface state instead of flickering underwater.
- Failed: direct refill proof is still missing because automated oxygen field manipulation could not be executed through Unity MCP on this machine.
- Broke: no compile errors observed from the survival change.
- Remaining: lower oxygen during live play, surface naturally, and confirm refill resumes immediately in build and during in-world swim. Closed for coding unless new evidence reopens it.

### [!] Pause Cursor Missing / Pause Button Audit Needed
- Status: [!]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: broken pause flow makes the game feel unfinished immediately
- Evidence: user report says `Esc` pause opens without a visible cursor and all buttons need checking
- Problems: input restore and menu state may still be fragile
- Short Comment: product shell issue, not optional polish
- Next Step: verify cursor, input map switching, `Esc` flow, and all button actions in build

- Did: traced the pause ownership conflict and added one shared pause truth in `PauseMenuController`, then switched `HectonPlayerMovement`, `PlayerInteraction`, and `PlayerFlashlight` to block gameplay/cursor reclaim while pause is open instead of only checking PDA/fabricator state.
- Result: the direct code-level conflict is removed; pause now has an explicit fail-safe gameplay block even if UI action-map switching degrades, and Unity recompilation completed with no new errors from the patch.
- Failed: live cursor-state proof is still incomplete on this machine because Unity MCP `execute_code` still fails with `mono.exe: filename or extension is too long`, and the existing `UIRuntimeSmokeTester` stalled after `PASS PDA open Inventory` before producing a pause result.
- Broke: no new compile errors; console still shows only pre-existing `Dynamic Decals` warnings, plus transient MCP serializer warnings during the abandoned smoke attempt.
- Remaining: real runtime check of `Cursor.visible`, `Cursor.lockState`, `Esc` open/close, and every pause button action in build; current status remains `PENDING VERIFICATION`.

### [!] Gas Giant Does Not Read As Distant
- Status: [!]
- Need User Check: partial success confirmed
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: sky scale is a major immersion pillar
- Evidence: user report says the gas giant looks too near because it sits incorrectly against the cloud layer
- Problems: hard giant silhouette needed atmospheric depth, but any camera-centered overlay sphere created visible parallax mismatch and read as a screen-space patch
- Short Comment: perceptual blocker
- Next Step: keep the giant huge, keep the new giant-anchored veil, verify it in build and across day/night states, and decide whether horizon compression should become a weather-state dial instead of one static default

- Did: traced the actual render order and found the root cause in the celestial camera path instead of size alone: `SpaceCamera` renders the `Celestial` layer as the base pass, and that same layer contains both `Sky_System/Sphere` and `GasGiant_Aegir`, so the existing sky dome clouds never draw any atmospheric veil over the gas giant. The first pass used a cloud/haze overlay shell, but the user rejected it, and then correctly identified the deeper bug: a camera-centered overlay sphere will always drift against the giant under lateral camera motion. Reworked the solution into the correct place: `SG_GasGiant_Master.shader` now applies the distance veil directly on the giant using the fragment view ray, sky-linked color, and `NightBlend`; `HectonCelestialEngine` now feeds the giant the same live sky colors as the rest of the atmosphere. After that, the remaining defect was no longer giant scale but horizon balance, so the next pass was moved to the actual source of truth: `HectonCelestialEngine` now compresses horizon luminance before pushing colors into both the sky and the gas giant, and `Mat_GasGiant` now carries a stronger horizon extinction curve so the lower arc loses contrast into the same atmospheric band instead of sitting on top of it. This pass adds the missing architectural layer for the future cloud problem: a separate soft `celestial occlusion` field in `SG_GasGiant_Master.shader`, sampled from the shared sky cloud atlas at low frequency and used only as optical transmittance/detail loss, not as the visible cloud layer itself. The legacy `Sphere_CloudOverlay` object is disabled in the live scene.
- Result: user reported the giant now looks much better. The atmospheric softening is now anchored to the giant instead of a fake disc in front of the camera, so the left/right edge inconsistency from the overlay patch is gone in the current live scene. The follow-up pass reduces the chalk-white horizon and makes the giant dissolve harder at the waterline, so the lower arc reads less like a clean sticker edge. The new soft occlusion field avoids the old giant-vs-cloud contradiction: celestial objects can now lose transmittance from atmospheric structure without having the ugly visible cloud shapes stamped directly on them. Scene-view and `SpaceCamera` readback after switching the occlusion field to spherical UV no longer show the earlier vertical-streak artifact from horizon-projected UVs.
- Result Addendum: the next objective readback showed a second issue after the first haze wins: the lower half of the giant was flattening into an overly uniform lavender plate, while the left horizon band was still too white. The latest pass narrows the giant extinction into a true horizon band instead of a broad half-disc wash, restores more structure in the middle of the planet, and cools the sky horizon material directly so the background no longer blows out into a near-white wall.
- Result Addendum: the follow-up pass split the problem one step further into `horizon band` and `bottom arc`. That preserves the upper and middle structure while letting the very lowest edge merge harder into the horizon. Current scene-view and `SpaceCamera` readback now match the intended shape more closely: top remains readable, middle is moderated, and the bottom arc is the part that gets eaten first.
- Result Addendum: the next pass tightened the `bottom arc` itself with a steeper response curve and slightly stronger veil/desaturation values. This keeps the extra extinction concentrated at the very edge instead of bleeding back into the middle of the disc. Current readback shows the lowest edge merging harder while the upper and middle zones remain readable.
- Result Addendum: the next objective gameplay screenshot exposed the last remaining shape error more clearly: the very bottom center was improving, but the lower side silhouette still read as a clean circular arc. The giant shader now welds not only the bottom center but the lower horizon-facing limb as a soft crescent, suppressing rim light and pushing that narrow silhouette strip into the same haze color as the horizon band. This is the correct physical shape for long atmospheric path length: not a flat strip, but a lower edge crescent.
- Result Addendum: a further coefficient pass pushed that lower horizon-facing limb crescent harder by reducing local detail/contrast and increasing haze tint along the side silhouette, not just at the bottom-center strip. `SpaceCamera` readback now shows the lower-left arc less clean than before, although the weld is still not absolute.
- Result Addendum: after the lower edge was pushed hard enough, a second regression appeared above the horizon: the bottom weld held, but the zone just above it became too clean again and the atmosphere stopped reading except at the waterline. The giant shader now has a separate `air-mass shoulder` between the narrow horizon weld and the cleaner upper disc. This shoulder reduces detail, saturation, and contrast in the lower-mid band without collapsing the top third into a flat wash.
- Result Addendum: the first `air-mass shoulder` pass fixed the missing middle haze but introduced a new energy bug: it was mathematically separate from the horizon band and too close to `_SkyHazeColor`, so the image broke into `white strip -> cleaner disc` and the giant became too bright and too blue above the horizon. The current pass replaces that stepped shoulder with a continuous broad air-mass curve above the horizon, keeps the narrow horizon band only as the extra lower-edge boost, and adds a separate air-mass darken term so the giant reads as behind haze instead of simply being painted with brighter milk.
- Result Addendum: after the user locked in a good lower horizon band manually, the remaining issue was isolated to the middle and upper thirds. The shader now has a dedicated `upper haze` lobe on top of the broad air-mass curve, aimed only at the upper/mid disc and modulated by the existing low-frequency celestial occlusion field. This keeps the current lower merge intact while letting the upper giant sit behind more atmosphere without stamping visible cloud shapes onto it.
- Failed: build verification is still missing, and automated day/night sweep is still blocked by MCP `execute_code` failing on this machine with `mono.exe: filename or extension is too long`. `02_HECTON_WORLD` remains dirty and unsaved. The old compile blocker from `WorldCaveDirector.cs` is now cleared, so the new `HectonCelestialEngine` feed is no longer blocked at compile time, but it still lacks build/runtime proof.
- Broke: the intermediate overlay-sphere path was a false solution and has been retired from the live runtime path.
- Remaining: verify horizon behavior and night darkening in build, then decide whether to delete the retired overlay assets entirely or keep them only as dead experiments outside the runtime path.
- Lesson: atmospheric depth cues must be attached either to the rendered object itself or to the same world-space ray logic as the rest of the sky. Camera-centered proxy geometry is not “cheap atmosphere”; it is guaranteed parallax debt.

- Lesson Addendum: when the horizon looks wrong, fix the shared sky-response first and only then tune object-specific extinction. If the sky and the giant are not driven by the same atmospheric color logic, the eye reads the giant as pasted in front immediately.
- Lesson Addendum: visible clouds and celestial occlusion are not the same system. The visible cloud layer can stay art-driven and high-character, while celestial objects should read a separate low-frequency transmittance field that only controls extinction, softness, and detail loss.
- Lesson Addendum: the separation is now implemented in both directions. `SG_GasGiant_Master.shader` reads a soft low-frequency occlusion field for the giant, and `Hecton_AlienSky_Master.shader` now has a separate celestial transmittance field that can dim stars, sun scatter, and halo without reusing the visible cloud silhouettes as direct masking.
- Lesson Addendum: broad full-disc fading is the wrong shape. Atmospheric loss has to be concentrated into a narrow horizon band; otherwise the giant stops feeling distant and starts feeling like a uniformly fogged matte sphere.
- Lesson Addendum: even `horizon band` alone is too coarse. The most believable extinction shape is two-stage: a moderate horizon band for the lower third, then a much tighter bottom arc that almost welds the final edge into the horizon band without killing the middle of the disc.
- Lesson Addendum: the bottom arc needs a steeper response curve than the broader horizon band. If both use the same softness, the extra extinction leaks upward and flattens the middle of the giant.
- Lesson Addendum: even a tight `bottom arc` is still incomplete if it only attacks the lowest center pixels. The last giveaway is usually the lower side silhouette. The physically useful shape is a `horizon-facing limb crescent`: horizon attenuation plus grazing-angle attenuation, so the lower side edge dissolves first without fogging the whole disc.
- Lesson Addendum: a believable distant planet needs three stacked distance zones, not one: a broad `air-mass shoulder` for the lower-mid band, a stronger `horizon band` for the lower third, and only then the tight `bottom arc / limb weld` at the final edge. If the shoulder is missing, the horizon looks fixed but the disc above it snaps back to a clean poster.
- Lesson Addendum: the broad upper haze must be continuous with the horizon haze. If it is computed as a separate leftover band or tinted too directly toward bright haze color, the eye sees an abrupt transition: a white strip at the horizon and then an unnaturally clean blue planet above it. The broad air mass should mainly reduce contrast, saturation, and brightness, while the true white milk belongs near the horizon itself.
- Lesson Addendum: once the lower band is artist-approved, stop touching it. Add any remaining distance cue as a separate upper haze layer. That preserves the hand-tuned horizon while giving the middle and upper thirds their own atmospheric coverage. The safest modulation source is the existing low-frequency celestial occlusion field, not the visible cloud layer.
- Failed Addendum: previewing night via direct `_NightBlend` material override is still not a trustworthy final oracle on this project. The current `SpaceCamera` capture path shows only weak visual response even after strengthening the giant's night branch, so night verification remains pending until a cleaner runtime/view path is available.
- Failed Addendum: `game_view` screenshot capture is still not a trustworthy oracle for this scene on this machine; the latest MCP capture from `Main Camera` returned a black frame, so scene-view remains the only usable visual readback during editor-side tuning.

### [!] Terrain / Rock Close-Up Blur
- Status: [!]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: close-up terrain blur weakens world credibility
- Evidence: user report says rocks and terrain look blurrier in game than expected from editor
- Problems: could be material tiling, runtime terrain settings, streaming, or build-only LOD behavior
- Short Comment: must be solved by comparison, not guesswork
- Next Step: run an identical editor/build terrain parity pass and separate material vs streaming causes

- Did: issue recorded from build report
- Result: root cause audit is no longer blind. `VoxelChunk` is not the current visible terrain path; active close-up rock landmarks in `__PROCEDURAL_SCATTER_WORLD` are currently `proxyOnly` instances such as `SCATTER_family.rock.arch.large_*`, built from cube proxy prefabs. Their materials were also objectively empty on the active path: `MAT_family_rock_arch_large.mat`, `MAT_family_rock_cluster_medium.mat`, and `MAT_family_rock_small_floor.mat` were `URP/Lit` materials with no albedo or normal textures assigned.
- Did Addendum: patched `WorldProceduralScatterDirector` so final-variant eligibility is no longer frozen only by stale cached `placement.SupportsFinalVariant`; reconcile/signature/rebuild logic now re-resolves support from the current family asset. This is aimed at letting families that now have `finalReady && !proxyOnly` variants stop sticking to old proxy-only placements.
- Did Addendum: patched the active rock proxy materials and the `MAT_family_rock_arch_large_Placeholder` material with real albedo/normal/AO texture references, so even before runtime final-variant proof the live proxy path is no longer rendering empty flat-tint materials.
- Did Addendum: cleared the unrelated compile blocker that was freezing this verification path. `WorldCaveDirector` was still calling removed `MapMagicBridge.SampleHeight`; it now uses `TryGetHeight` with fail-safe fallback, `caveSpawnProbability` is finally wired back in as the intended biome-evaluation gate, and duplicate `using` noise was removed from `HectonVoxelEngine`.
- Failed: the runtime part is still `PENDING VERIFICATION` because close-range rock families have not yet been proven to rebuild into their `final` or `final.placeholder` variants during live runtime after the compile unblock. The active rock arch proxy geometry is also still cube-based placeholder form, so the material pass improves surface detail but cannot by itself turn placeholder blocks into final rock silhouettes.
- Broke: no new compile errors were introduced by the scatter or cave compile-fix passes; console now reports only the unrelated `Dynamic Decals` obsolete warnings and one earlier MCP warning about unsupported `_Smoothness` conversion during a material tool call.
- Remaining: verify that close-range rock families now rebuild into their `final` or `final.placeholder` variants in runtime, then run editor/build parity at the same spot and only after that decide whether terrain texture density itself still needs retuning.

### [ ] Build Smoother Than Editor
- Status: [ ]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: this is not a bug; it is a production rule reminder about truth source
- Evidence: user explicitly reports build feels smoother than editor
- Problems: editor-heavy debugging can still waste time if treated as final truth
- Short Comment: use build as arbiter
- Next Step: keep player-build-first discipline for P0 blockers and perceptual quality

- Did: observation recorded
- Result: promoted into workflow rule. Standalone profiler screenshots from `2026-04-06` reinforce the same conclusion: the player build baseline is materially better than editor play mode, so editor-only spikes must not be treated as final truth without build capture.
- Failed: nothing
- Broke: nothing
- Remaining: maintain this discipline on all future passes

### [~] Standalone Player Profiling Snapshot
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: performance work now has real standalone evidence instead of editor noise
- Evidence: attached standalone player profiler screenshots from `Shinobu - Submerge`
- Problems: the build console warned that the player was built with uncompiled code changes, so the captured player may lag behind the latest source edits; GPU timings were not available in the screenshots (`GPU --ms`), and current MCP profiler attachment is not active
- Short Comment: baseline build performance is not a blanket CPU disaster; the real blockers are intermittent spike classes
- Next Step: re-capture the same build with named scenarios (`idle swim`, `surface crossing`, `PDA/pause open`, `dense world route`) and correlate each spike to a concrete action

- Did: extracted the standalone screenshots into one frame table:

| Frame | CPU Frame | Primary Marker | Read |
| --- | ---: | --- | --- |
| `3327` | `14.72 ms` | `WaitForLastPresent ≈ 9.06 ms` | Healthy baseline frame. Real gameplay + render work is much lower than total frame time; a large part is present/frame-pacing wait. |
| `3676` | `42.18 ms` | `WaitForLastPresent / DXGI.WaitOnSwapChain ≈ 36.14 ms` | Present-bound miss. Main thread total looks scary, but the frame is dominated by waiting, not by script saturation. |
| `2483` | `22.19 ms` | `Coroutine: MoveNext ≈ 10.01 ms` | Real intermittent CPU hitch. Matches the project pattern where `GameTickManager` still runs a global `SlowTickRoutine()` coroutine. |
| `3826` | `53.55 ms` | `EventSystem.Update() ≈ 42.89 ms` -> `GameObject.ActivateAwakeRecursively ≈ 23.54 ms` | Real CPU spike from UI activation cascade. `Collect ≈ 2.27 ms` is visible on the same frame. |

- Result: the screenshots separate the frame into two different problems instead of one fake general slowdown:
  1. Baseline standalone frames are often `present-bound`, not logic-bound.
  2. The real CPU hitches are intermittent and currently fall into two buckets:
     - UI activation storms
     - coroutine / slow-tick spikes
  3. The current geometry load does not read as the main blocker from these screenshots alone: visible counters sit roughly around `73-117` batches, `~101k-346k` triangles, `~181k-346k` vertices, `~0.73 GB` total memory, `~366 MB` texture memory, `239` materials, `~16.1k-16.6k` objects, and `~82-85 MB` GC used memory.
  4. The `3676` render-thread screenshot supports the same interpretation: it spends `~40.2 ms` mostly in `Semaphore.WaitForSignal / WaitForGfxCommandsFromMainThread`, which is consistent with a present-bound frame rather than a render-thread work explosion.

- Failed: GPU-side truth is still incomplete because the screenshots do not expose actual GPU frame time, and MCP currently reports profiler disabled; its fallback rendering snapshot is not trustworthy as a live player oracle here.
- Broke: nothing.
- Remaining: audit and reduce:
  - `EventSystem` -> `GameObject.Activate/ActivateAwakeRecursively` spikes
  - UI `SetActive` cascades in `PlayerPDA`, `PDAInventoryTab`, `PauseMenuController`, and HUD roots
  - `GameTickManager.SlowTickRoutine()` / coroutine spike ownership
  - only after that decide whether a broader render reduction pass is even justified

### [c] UI Activation Cascade In PDA / Pause / HUD Roots
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: standalone frame `3826` already showed a real CPU spike from `EventSystem.Update() -> GameObject.ActivateAwakeRecursively`
- Evidence: standalone profiler screenshots from `2026-04-06`
- Problems: build still has unrelated compile blockers in other files, so a clean end-to-end compile oracle is currently contaminated
- Short Comment: code pass applied; closed for current implementation work until new build evidence
- Next Step: capture new standalone profile around `open PDA`, `switch PDA tabs`, `open pause`, and `resume gameplay`

- Did: replaced UI root visibility churn with cached visibility gates in the confirmed hot stack. `PlayerPDA` shell and tabs now use warmed `CanvasGroup` visibility instead of repeated hierarchy wake/sleep; `PDAInventoryTab` now hides item blocks, detail widgets, selection/hover markers, and action roots without `SetActive`; `PDALoadoutTab` action buttons, preset cards, and suggested-action root now use cached `CanvasGroup` visibility; `PDADataLogTab` now defers hidden-tab refresh work instead of refreshing in the background; `PauseMenuController` section switching now uses per-panel `CanvasGroup` visibility; `SuitHUDV4CanvasOverlay` root hide/show no longer toggles the overlay root active state.
- Result: the known `ActivateAwakeRecursively` path is now attacked at the actual sources instead of at profiler symptoms. The intended runtime effect is fewer UI activation spikes, less activation-adjacent GC on open/switch frames, and lower `EventSystem` cost when toggling PDA/pause/HUD visibility.
- Failed: standalone before/after capture for `open PDA`, `switch PDA tab`, `open pause`, and `resume gameplay` still has not been re-run, and Unity MCP `execute_code` remains blocked on this machine by `mono.exe: filename or extension is too long`.
- Broke: the unrelated compile contamination that previously blocked this verification path is now cleared. Current compile readback shows warnings and editor-inspector null spam, but no new `CS` errors from the UI pass.
- Remaining: rebuild after clearing unrelated compile blockers, then compare standalone profiler frames before/after for `PDA open`, `tab switch`, `pause open`, `pause close`, and `idle gameplay with HUD active`.

### [~] GPU / Present Pacing Track Still Separate
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: some scary CPU totals are actually `present wait`, not script overload
- Evidence: standalone frames `3327` and `3676` are dominated by `WaitForLastPresent / DXGI.WaitOnSwapChain`
- Problems: current screenshots do not include trustworthy GPU frame times (`GPU --ms`), and MCP profiler attachment is not live
- Short Comment: do not mix this track with UI or slow-tick CPU hitches
- Next Step: recapture standalone with real `GPU` timings enabled and scenario labels

- Did: separated the render/present track in the ledger and master plan so future passes do not falsely blame script systems for present-bound frames.
- Result: the project now has an explicit rule: `WaitForLastPresent / DXGI.WaitOnSwapChain` must be treated as a separate render/pacing investigation, not as proof that gameplay CPU is overloaded.
- Failed: no new GPU timing evidence yet.
- Broke: nothing.
- Remaining: collect player build captures with actual GPU milliseconds before attempting any broad render cuts.

### [~] Fauna SlowTick Spike / `SmallPassiveProxy` Pool Exhaustion
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: live runtime log now points to a concrete world offender instead of a vague `SlowTick` bucket
- Evidence: user console log from `2026-04-06` shows `[TickProfiler] SlowTick spike total=19.64ms ... FaunaDirector=12.05ms` and repeated `[ObjectPoolManager] 'SmallPassiveProxy': Pool exhausted, expanding by 4`
- Problems: the old fauna pool warmup used one static reserve of `8`, while live runtime streaming settings can increase `_runtimeMaxSpawnsPerTick` far above that after scene start
- Short Comment: active runtime offender; this is not editor noise
- Next Step: run the same swim route again and confirm whether `SmallPassiveProxy` expansion warnings stop and whether `FaunaDirector` drops out of the top `SlowTick` offender slot

- Did: cleared the hard runtime crash in `WorldProceduralScatterDirector` by removing the invalid `NativeArray<ScatterCandidate>` use from `CandidateMap`; `ScatterCandidate` contains managed references and cannot live in `NativeArray<T>`. The cache now uses managed arrays in cold/runtime cache space instead of invalid job memory. Then patched `FaunaDirector` pool warmup so reserve targets are derived from live runtime streaming limits instead of a dead constant `8`, and so a later runtime settings refresh can reopen warmup when those limits grow. `SmallPassiveProxy` now gets a stronger reserve target than ordinary fauna prefabs because it is the prefab named in the live expansion warnings.
- Did Addendum: patched both gameplay spawn sites in `FaunaDirector` to call `ObjectPoolManager.Spawn(..., allowExpand:false)` instead of the default expanding path. This closes the remaining zero-GC hole where `SlowTick` could still trigger runtime `Instantiate` via pool expansion when reserve was temporarily exhausted.
- Result: compile state remains clean of `CS` errors after the fauna/scatter pass. The runtime scatter blocker that previously aborted `WorldProceduralScatterDirector.Awake()` is code-fixed, and the fauna director no longer locks its warmup to a one-time static reserve disconnected from live activation limits.
- Result Addendum: fauna is now fail-soft under pool pressure. If reserve is insufficient, the director skips that spawn attempt instead of injecting pool expansion and allocation spikes into gameplay.
- Failed: no new in-world proof yet that `SmallPassiveProxy` warnings are gone, because the user has not supplied the next live swim/build log after this patch and `execute_code` remains unusable on this machine.
- Broke: no new compile errors from `FaunaDirector` or `WorldProceduralScatterDirector`. The remaining console noise is editor selection null spam (`GameObjectInspector` / `SerializedObjectNotCreatableException`) plus unrelated warnings.
- Remaining: re-run the same underwater route in live game/build, capture the next `TickProfiler` line, and confirm:
  - `FaunaDirector` no longer dominates the top offender list at the same magnitude
  - `SmallPassiveProxy` no longer expands on-demand
  - close-up fish density still looks acceptable after the stronger prewarm

### [~] Camera Turn Overshoot / Reverse Lean After Mouse Stop
- Status: [~]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: camera feel breaks trust immediately if horizontal look gives a reverse tail after release
- Evidence: user report from `2026-04-06` says horizontal mouse turns can accumulate and then lean/shift back in the opposite direction after the mouse stops
- Problems: the live camera juice stack applies spring-driven `swim roll` and `turn sway`, so release-phase overshoot can read like false head inertia instead of believable underwater mass
- Short Comment: active feel blocker
- Next Step: user/build verification while doing sharp left-right mouse turns at surface swim and in deeper water

- Did: patched `CameraJuiceProcessor` so the horizontal `swim roll` and `turn sway` tracks cannot spring past their target on release. The old spring behaviour could cross zero and create a visible opposite-direction tail after mouse stop. The new helper clamps to the target and zeroes the spring velocity as soon as an overshoot is detected, instead of letting the effect rebound through the center.
- Result: the code path now specifically attacks the reported symptom without deleting the whole camera-mass layer. The intended runtime effect is: the camera can still lean and sway during the turn, but when input stops it should settle to neutral instead of kicking to the opposite side.
- Failed: no user/build proof yet. I have only compile confirmation and code-path inspection, not a new live swim check.
- Broke: no new compile errors; console remains limited to editor selection null spam.
- Remaining: verify in live game/build with:
  - steady left-right mouse sweeps underwater
  - fast flick then release
  - same test near the surface where bob + sway stack together

### [~] Surface Jump / Shoreline Climb Reliability

- Status: [~]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Shore Check: yes
- Why: if the player cannot reliably jump or climb out of shallow shoreline geometry, surface trust collapses even if oxygen/surface-state code is technically correct
- Evidence: user report from `2026-04-06 15:56` says jumping on the surface does not work and climbing slopes/shoreline edges feels blocked
- Problems: `HectonPlayerMovement` only accepted jump on the exact `_isWalking && _isGrounded` frame, while shoreline mode could drop out of `walking` on shallow-water/slope transitions and let `surface lock` fight the same movement window
- Short Comment: active surface locomotion blocker
- Next Step: user/build verification on shoreline and shallow incline routes

- Did: added a short `jump buffer` and `shore ground grace` in `HectonPlayerMovement` so shallow shoreline movement no longer depends on a single exact grounded frame. The jump request now survives briefly until the next valid shore-support frame, shallow-water walk mode can hold through tiny ground-check gaps, and `surface lock` is suppressed during that shallow grace window instead of pushing against the same movement.
- Result: the code path now targets the reported symptom directly. Intended runtime effect: pressing jump near the waterline should still fire when the player is in a valid shallow-ground transition, and shoreline climbing should stop dropping into false swim/surface-lock behaviour on tiny contact losses.
- Failed: no live build proof yet. I have compile/console confirmation only, not a new shoreline traversal test.
- Broke: no new compile errors detected; console remains limited to editor inspector null spam.
- Remaining: verify in live game/build with:
  - jump spam while partially submerged at the shoreline
  - walking up shallow wet slopes and rock lips
  - surfacing against an incline and then trying to climb out without losing control

## Next Build Question

After each new build, ask one short question:

`What breaks belief in the world the most right now?`
