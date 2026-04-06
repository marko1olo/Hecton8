# HECTON-8 — BUILD / PLAYTEST ISSUES LEDGER

Status: `PENDING VERIFICATION`
Ledger Start Date: `2026-04-05`

## Purpose

This file tracks confirmed build and playtest observations.

Rules:

- Only log real observations from builds, live runs, or manual playtests
- Do not log abstract ideas here
- Do not mark anything solved without new evidence
- Every item remains `PENDING VERIFICATION` until a new build or user check confirms the fix
- Player build is the main arbiter, not editor feel

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
- Status: [ ] / [~] / [x] / [!] / [?]
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

### [!] Surface Transition Hitch
- Status: [!]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: this is an immediate feel-breaker during normal play
- Evidence: user report says the game can hitch when moving from underwater to above water while turning the camera
- Problems: editor is not reliable as final truth because build is smoother overall
- Short Comment: must be solved and verified in build
- Next Step: isolate transition cost across water, atmosphere, camera, sound, and post-processing

- Did: diagnosed live runtime mismatch at the waterline (`Atmosphere=UNDERWATER` while `Visuals=false`, `Movement=false`, `Survival depth≈0`) and replaced it with one shared hysteresis contract based on `HectonPlayerMovement.CurrentDepth` for atmosphere, underwater visuals, and survival.
- Result: editor runtime no longer splits state at the surface boundary; current readback keeps `Atmosphere=surface`, `Visuals=false`, `Survival depth≈0.0049` on the same near-surface frame instead of contradictory surface/underwater states.
- Failed: build verification not run yet; could not force a scripted underwater transition sweep because Unity MCP runtime code execution fails on this machine (`mono.exe: filename or extension is too long`).
- Broke: no compile errors from the patch; console still shows unrelated warnings from `Dynamic Decals` and one generic `Leak Detected : Persistent allocates 8 individual allocations` warning after recompilation.
- Remaining: real swim test in player build while rotating the camera across the surface; confirm hitch is gone under build timing, not just editor runtime.

### [!] Surface Oxygen Refill Missing
- Status: [!]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: survival trust collapses if surface safety does not work
- Evidence: user report says oxygen does not refill correctly when surfacing
- Problems: likely tied to surface-state truth and crossing logic
- Short Comment: this is a core survival blocker
- Next Step: bind refill to the shared surface truth contract with hysteresis and fail-safe checks

- Did: survival oxygen flow now uses the same shared surface hysteresis contract and explicit surface refill path instead of unconditional underwater-style drain.
- Result: refill logic is now present in gameplay code and bound to the same surface truth used by atmosphere and visuals; near-surface runtime readback holds the player in surface state instead of flickering underwater.
- Failed: direct refill proof is still missing because automated oxygen field manipulation could not be executed through Unity MCP on this machine.
- Broke: no compile errors observed from the survival change.
- Remaining: lower oxygen during live play, surface naturally, and confirm refill resumes immediately in build and during in-world swim.

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
- Result: promoted into workflow rule
- Failed: nothing
- Broke: nothing
- Remaining: maintain this discipline on all future passes

## Next Build Question

After each new build, ask one short question:

`What breaks belief in the world the most right now?`
