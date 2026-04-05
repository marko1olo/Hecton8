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
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: sky scale is a major immersion pillar
- Evidence: user report says the gas giant looks too near because it sits incorrectly against the cloud layer
- Problems: hard giant silhouette still fights the sky depth read, but cloud shapes over the giant were visually rejected and cannot stay as the fix
- Short Comment: perceptual blocker
- Next Step: keep the giant huge, remove cloud-over-giant masking, and validate a haze-only depth cue in gameplay/build

- Did: traced the actual render order and found the root cause in the celestial camera path instead of size alone: `SpaceCamera` renders the `Celestial` layer as the base pass, and that same layer contains both `Sky_System/Sphere` and `GasGiant_Aegir`, so the existing sky dome clouds never draw any atmospheric veil over the gas giant. The first pass used a cloud+haze overlay shell, but the user rejected it because low-quality planet clouds crossing the giant looked fake. Reworked the approach into a separate haze-only shader `Hecton_AegirHazeOverlay.shader`, created `Mat_AegirHazeOverlay.mat`, kept the second live sky shell in `Sky_System`, and synced `HectonCelestialEngine` to drive the overlay disc from the real giant angle without reducing the giant's scale.
- Result: current live play-mode capture shows the giant still enormous and readable, with the cloud-over-giant artifact removed and no magenta regression from the isolated shader path. Compile/refresh completed with no new console errors on this pass.
- Failed: this is still not build-verified and not user-verified; the only proof is editor-side play capture. The scene instance carrying `Sphere_CloudOverlay` is still unsaved because `02_HECTON_WORLD` was already dirty and should not be silently saved over unrelated user edits.
- Broke: the shared-shader attempt briefly regressed into magenta during development; that path was rolled back and isolated into the dedicated haze shader. No new live console errors remained after the isolated pass compiled.
- Remaining: user verdict on whether haze-only gives enough distance without ruining the giant, then a deliberate scene save and build check for horizon cases.

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
- Result: now part of the active P0 track
- Failed: no fix applied yet
- Broke: nothing
- Remaining: terrain parity audit and build confirmation

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
