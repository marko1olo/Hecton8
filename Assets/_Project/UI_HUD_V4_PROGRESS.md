# HECTON-8 HUD v4 Progress

## Current Direction

- Preferred rendering path: `Shapes` on `HUD_Render_Camera`.
- Reason: this project already uses `ImmediateModeShapeDrawer` successfully, and the visor/HUD style needs crisp vector graphics more than traditional uGUI layout.
- `Suit_HUD_Canvas` is deprecated and inactive in the scene. It remains useful as reference only.
- `HUD_Render_Camera` is the live HUD path on the player prefab and scene.

## Scene Findings

- Active scene: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Live player stack:
  - `Main Camera`
  - `SpaceCamera`
  - `HUD_Render_Camera`
  - `Suit_Visor`
- Actual URP render topology in this scene:
  - `SpaceCamera` = `Base`
  - `Main Camera` = world `Overlay`
  - `HUD_Render_Camera` = HUD `Overlay`
- `HUD_Render_Camera` currently has `HectonSuitHUD`.
- `HUD_Render_Camera` also has `HectonSuitHUD_v4` and now a dedicated presentation orchestrator.
- `Suit_Visor` uses visor material/shader path and is the right long-term anchor for immersive projected HUD.
- `RT_HUD_Display.renderTexture` already exists and is referenced by `Mat_Visor_Glass`.
- `VisorHUDController` is attached to `Suit_Visor` in scene and prefab in safe `Disabled` projection mode.
- `SuitHUDPresentationController` is now attached to `HUD_Render_Camera` in the active scene.
- `SuitHUDPresentationController` is now also wired into `Assets/_Project/Prefabs/Player.prefab`.
- Active scene state was normalized to:
  - `presentationMode = ModernOverlay`
  - standard fallback profile assigned
  - shared visor RT assigned for future projected mode
  - legacy `HectonSuitHUD` explicitly disabled in scene
  - `ModernOverlay` now routes v4 back to `HUD_Render_Camera`, which is the correct host for this scene topology

## Design Goal

Reference target is a cinematic, minimal, Subnautica-like cockpit/helmet composition:

- Bottom-left: 3 circular gauges with strong hierarchy and calm spacing.
- Right side: large `DEPTH` and smaller `TEMPERATURE`.
- Subtle visor-frame signals instead of noisy sci-fi clutter.
- UI should feel diegetic, not like a flat debug overlay.
- Keep a NASA-punk / premium dive-tech tone.

## Architecture Decision

New prototype goes into `HectonSuitHUD_v4.cs` as a separate layer instead of rewriting `HectonSuitHUD.cs` immediately.

Why:

- Safer rollout.
- Lets us compare old engineering HUD vs new cinematic HUD.
- Easier to toggle, test, and iterate.
- Better handoff for future agents.

## Multi-Suit Direction

User clarified an important requirement: HUD must not be a single hardcoded layout.

Target architecture now:

- Base HUD system stays shared.
- Active suit decides which telemetry blocks are visible.
- Different suits may use different visual density and slightly different styling.
- Heavy/industrial suits can expose more telemetry.
- Lighter suits can stay cleaner and more minimal.

Implementation direction:

- Add optional `SuitHUDProfile` ScriptableObject.
- `SuitData` can point to a profile, but old suit assets remain valid when the field is null.
- `HectonSuitHUD_v4` resolves the current suit through `HectonPlayerMovement.CurrentSuit`.
- If no explicit HUD profile exists, HUD v4 uses safe inference from suit physics, so legacy suits still get a coherent layout.

## Data Sources for v4

- `HectonSurvivalSystem`
  - oxygen
  - energy
  - integrity
  - depth
- `PlayerFlashlight`
  - on/off
  - heat
  - overheated/flicker state
- `PlayerPDA`
  - open/closed state
- `HectonPlayerMovement`
  - current active `SuitData`
  - camera yaw for heading
- `HectonUnderwaterVisuals`
  - current depth
  - light factor

## Temperature Strategy

There is no clean public gameplay temperature system yet.

Temporary enterprise-safe approach:

- Use a graceful ambient estimate derived from depth and underwater light factor.
- Keep this logic isolated in HUD v4 so it can be replaced later by a real environment temperature provider.

## Implemented This Round

- Filled previously empty `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`.
- Added a Shapes-driven prototype HUD layer with:
  - left circular O2 / PWR / HLT gauges
  - right telemetry block for depth + temperature
  - subtle visor framing
  - flashlight / PDA micro-state strip
  - runtime auto-resolve for common references
  - event + polling hybrid data sync
- Added suit-aware HUD architecture hooks:
  - new `SuitHUDProfile.cs`
  - optional `SuitData -> SuitHUDProfile` reference
  - fallback suit-based telemetry inference when profile is missing
  - suit label + heading strip support in v4
- Created first concrete HUD profile assets:
  - `HUDProfile_Light_Expedition`
  - `HUDProfile_Heavy_Atlas`
  - `HUDProfile_Standard_Default`
- Bound those profiles to:
  - `Suit_Light_Default`
  - `Suit_Heavy_ATLAS`
- Important data note:
  - `Standard_Suit_V1.asset` is `SurvivalStats`, not `SuitData`.
  - Therefore the standard profile belongs as HUD fallback/default, not as a direct profile reference on that asset.
- Upgraded helmet-interface visual language:
  - style-aware visor framing
  - style-aware palette resolution
  - optional depth-trend telemetry
  - heavier industrial framing for dense suits
  - lighter expedition framing for cleaner suits
  - gauge tick marks and soft local glow
  - center reticle and lower status ribbon
  - stronger telemetry side-panel presence without switching to heavy UI widgets
- Hardened `VisorHUDController`:
  - supports disabled/shared/runtime projection modes
  - can auto-resolve `Suit_Visor` renderer and `HUD_Render_Camera`
  - can use shared RT safely instead of always creating transient textures
  - no longer throws when projection is disabled and no RT is active
- Added hard diagnostics for invisible-HUD debugging:
  - `HectonSuitHUD_v4` now tracks matched vs rejected camera draw calls
  - `_debugForceVisibilityProbe = true` renders a bright full-screen probe banner
  - if the probe is still invisible while matched draws increase, the bug is in final URP overlay composition rather than HUD layout/data

## Next High-Value Steps

1. Decide whether legacy `HectonSuitHUD` stays as a fallback-only component or is fully retired from the player stack.
2. Run visual verification in play mode for:
   - `ModernOverlay`
   - `ModernProjectedSharedRT`
   - `ModernProjectedRuntimeRT`
3. Tune `Mat_Visor_Glass` against real projected HUD brightness/distortion once visor projection is enabled.
4. Add a dedicated data provider for environmental temperature when gameplay system exists.
5. If suit swapping is expected in-session, verify presentation changes hot-swap cleanly when `CurrentSuit` changes.
6. Add a deliberate critical-state visual escalation layer instead of relying only on static color swaps.
7. Use the visibility probe result to choose the next branch:
   - probe visible: continue polish/tuning
   - probe invisible with matched draw counts rising: inspect Shapes pass order / overlay composition path, not widget styling

## Fog Finding

- `Assets/_Project/Data/biom/0_ShallowGrave.asset` does contribute to the blue terrain look:
  - `depthFogDensity: {x: 0.022, y: 0.012, z: 0.014}`
  - `fogColor: {r: 0.05678178, g: 0.28103185, b: 0.41509432, a: 1}`
- But this asset is not the full root cause.
- `HectonBiomeProfile` documents that:
  - `depthFogDensity` affects Crest water only
  - `fogColor` feeds URP `RenderSettings.fogColor`
- `HectonUnderwaterVisuals` drives global URP fog, and `TerrainMaster.shader` mixes URP fog explicitly.
- Result: terrain can visibly blue-shift from fog while sky / gas giant / parts of water remain outside the same fog path.
- Treat this as a shader-pipeline consistency issue, not just a bad biome asset value.

## Notes For Future Agents

- Do not revive `Suit_HUD_Canvas` as the primary path unless there is a very specific reason.
- Favor `Shapes` for diegetic visor HUD.
- Treat `HectonSuitHUD.cs` as stable fallback/reference and `HectonSuitHUD_v4.cs` as the future-facing visual prototype branch.
- Before changing visor projection, verify whether `HUD_Render_Camera` should stay in the main camera stack or move fully to render-texture output.
- Do not hardcode one universal telemetry set for all suits. Suit-specific reduction/expansion is now a core design requirement.

## Runtime Stabilization Notes

- `GameTickManager` was hardened against runtime state loss after play-mode transitions:
  - lazy `EnsureInitialized()` now restores tick lists before update/fixed/slow loops
  - duplicate slow-tick coroutine creation is prevented
  - root detachment before `DontDestroyOnLoad` keeps persistent managers valid
- `GasGiantRotationDriver` no longer throws when renderer/property block is absent.
- Crest `OceanBuilder.CleanUp()` was guarded against partially initialized mesh state.
- `InputManager` no longer hard-fails when only the `Player` action map is available:
  - `Player` map is mandatory
  - `UI` map is optional
- `HectonPlayerMovement` now has an emergency vertical-swim fallback independent of the input asset:
  - ascend: `Space`
  - descend: `LeftCtrl`, `C`, `Q`
  - if a `ControlScheme` asset is assigned, it becomes the authoritative fallback key source
- Mouse look was reduced from a raw `2.0` scale to a saner baseline for Input System mouse delta.
- Input resilience was extended beyond vertical swim fallback:
  - `HectonPlayerMovement` now rebinds itself if `InputManager` is recreated mid-session
  - movement and look can fall back to raw `Keyboard.current` / `Mouse.current` input if the player action map is unavailable
  - `PlayerPDA`, `PlayerFlashlight`, and `PlayerToolManager` now re-subscribe safely when `InputManager` changes instance
  - `InputManager.Awake()` explicitly clears shutdown state so a legitimate re-created manager is not blocked by stale static teardown flags
