# Tool Matrix

## 2026-03-29 - Environmental Analyzer Upgrade

- `EnvironmentalAnalyzerTool` now classifies:
  - carried items
  - resource nodes
  - base modules
  - bioforms
  - mass objects
- target analysis now includes:
  - severity
  - summary
  - recommended next action
- suit analysis now clearly distinguishes:
  - hull critical
  - oxygen critical
  - low power
  - safe-depth exceedance
  - stable expedition state

## 2026-03-29 - Builder Readiness Upgrade

- `PlayerBuilder` now exposes a readable readiness state:
  - `OFFLINE`
  - `NO MODULE`
  - `MISSING COST`
  - `PLACEMENT BLOCKED`
  - `READY`
  - `SNAPPED READY`
- builder messaging now includes a real cost digest like `COPPER 1/3`
- deploy and recover actions now write clearer field-operation entries
- `BuilderTool` screen tint now distinguishes blocked placement from missing materials

## 2026-03-29 - Scanner Mode Upgrade

- `ScannerTool` now has three real working modes:
  - `EXPEDITION`
  - `RESOURCE`
  - `STRUCTURE`
- secondary action now cycles scanner mode instead of doing nothing useful
- scan output now changes by mode:
  - broad contact count
  - resource and pickup emphasis
  - structure and intel emphasis
- field log now records what kind of sweep was performed and what it found

## 2026-03-29 - Beacon Logistics Upgrade

- `BeaconDeployerTool` secondary action now behaves by distance:
  - far away -> reports nearest beacon and range
  - close enough -> retracts nearest beacon
- deploy and retract messages now also report active beacon-grid count
- beacon tool now reads more like a navigation/logistics device and less like a raw spawn/remove helper

## 2026-03-29 - Repair Diagnostics Upgrade

- `RepairTool` diagnosis now distinguishes:
  - `SEALED`
  - `PATCHING`
  - `HEAVY DAMAGE`
  - `CRITICAL DAMAGE`
  - `FLOODED`
  - `DRAINING`
  - `NO POWER`
- repair feedback now includes a simple recommendation instead of only a bare percent number
- repair start and quick diagnosis now share the same real module-state logic

## 2026-03-29 - Laser Cutter Clarity Upgrade

- `LaserCutter` secondary action now diagnoses the current target instead of doing nothing
- it distinguishes:
  - `NO TARGET`
  - `RESOURCE CONTACT`
  - `CUTTABLE CONTACT`
  - `RECOVERY READY`
  - `MODULE LOCKED`
- deconstruction mode now reports progress while the beam is held
- overheat cooldown now clearly reports when the cutter core is stable again

## 2026-03-29 - Salvage Sampler Clarity Upgrade

- `SalvageSamplerTool` primary action now reports active extraction when work is actually happening
- secondary action now distinguishes:
  - `RECOVERY READY`
  - `RESOURCE NODE`
  - `NODE DEPLETED`
  - `PROCESS TARGET`
  - `INVALID TARGET`
- recovery feedback now shows the recovered item name when available
- `ToolHitUtility` now exposes a reusable collectible peek helper for salvage-style tools

Do not delete notes from this file.
Append changes instead of replacing history where possible.
Use this as the source of truth for tool data, prefab status, and rollout order.

## Current rollout

As of this pass:
- `ItemData` assets created for 12 tools under `Assets/_Project/Data/Items/Tools`
- `ToolMetadata` assets created for 12 tools under `Assets/_Project/Data/Tools`
- world pickup prefabs created for all 12 tools under:
  - `Assets/_Project/Prefabs/Items/Tools`
- every tool `ItemData` now has a non-null `worldPrefab`
- held prefab scaffolds created for:
  - `Scanner`
  - `Repair`
  - `Builder`
  - `Laser Cutter`
- first-pass runtime scripts added for:
  - `Flashlight Tool Adapter`
  - `Propulsion Tool`
  - `Salvage Sampler`
  - `Beacon Deployer`
  - `Environmental Analyzer`
  - `Survival Blade`
  - `Stun Pistol`
  - `Harpoon Launcher`
- held prefab scaffolds created for those eight tools under `Assets/_Project/Prefabs/Tools/Held`
- placeholder visuals added to those four prefabs as colored cube bodies
- placeholder visuals added to the seven new prefabs as colored cube bodies
- `PlayerToolManager.toolPrefabs[0..3]` assigned to those four held prefabs
- live scene now also has:
  - `PlayerFlashlight` on `Player`
  - `DiveLamp_Light` under `Player/Main Camera`
  - `FlashlightTool` therefore has a real runtime light backend, not just data/prefab scaffolding
- live scene also has:
  - `--- WORLD ---/Tool_Staging`
  - `ToolStagingSpawner` authoring helper on that root
  - all 12 tool world prefabs laid out in-scene for future pickup/drop testing
- live player scene setup also now has:
  - `ToolLoadoutProvisioner` on `Player`
  - startup full-kit inventory seeding enabled
  - startup core 4-slot loadout assignment enabled

## Matrix

| Tool | ItemData | ToolMetadata | Held Prefab | Runtime Script | Status |
|---|---|---|---|---|---|
| Scanner | `Item_Tool_Scanner` | `ToolMetadata_Scanner` | `Tool_Scanner_Held` | `ScannerTool` | scaffold ready |
| Repair Tool | `Item_Tool_Repair` | `ToolMetadata_Repair` | `Tool_Repair_Held` | `RepairTool` | scaffold ready |
| Builder Tool | `Item_Tool_Builder` | `ToolMetadata_Builder` | `Tool_Builder_Held` | `BuilderTool` | scaffold ready |
| Laser Cutter | `Item_Tool_LaserCutter` | `ToolMetadata_LaserCutter` | `Tool_LaserCutter_Held` | `LaserCutter` | scaffold ready |
| Flashlight | `Item_Tool_Flashlight` | `ToolMetadata_Flashlight` | `Tool_Flashlight_Held` | `FlashlightTool` + existing `PlayerFlashlight` system | first-pass adapter + prefab |
| Propulsion Tool | `Item_Tool_Propulsion` | `ToolMetadata_Propulsion` | `Tool_Propulsion_Held` | `PropulsionTool` | first-pass runtime + prefab |
| Salvage Sampler | `Item_Tool_SalvageSampler` | `ToolMetadata_SalvageSampler` | `Tool_SalvageSampler_Held` | `SalvageSamplerTool` | first-pass runtime + prefab |
| Beacon Deployer | `Item_Tool_BeaconDeployer` | `ToolMetadata_BeaconDeployer` | `Tool_BeaconDeployer_Held` | `BeaconDeployerTool` | first-pass runtime + prefab |
| Environmental Analyzer | `Item_Tool_EnvAnalyzer` | `ToolMetadata_EnvAnalyzer` | `Tool_EnvAnalyzer_Held` | `EnvironmentalAnalyzerTool` | first-pass runtime + prefab |
| Survival Blade | `Item_Tool_Knife` | `ToolMetadata_Knife` | `Tool_Knife_Held` | `KnifeTool` | first-pass runtime + prefab |
| Stun Pistol | `Item_Tool_StunPistol` | `ToolMetadata_StunPistol` | `Tool_StunPistol_Held` | `StunPistolTool` | first-pass runtime + prefab |
| Harpoon Launcher | `Item_Tool_HarpoonLauncher` | `ToolMetadata_HarpoonLauncher` | `Tool_HarpoonLauncher_Held` | `HarpoonLauncherTool` | first-pass runtime + prefab |

## Notes

- `FlashlightTool` is an adapter over the existing `PlayerFlashlight` system. It does not create a second lighting pipeline.
- `Tool_Flashlight_Held` exists as a scaffold prefab with placeholder visual and bound `ItemData/ToolMetadata`.
- The four created held prefabs are logic scaffolds only. They still need proper visuals, audio refs, and presentation tuning.
- The eight newly added/updated prefabs are also scaffolds. They have real `PlayerTool` scripts and bound `ItemData/ToolMetadata`, but their behavior is intentionally first-pass:
  - `FlashlightTool` = safe wrapper for toggle/status over `PlayerFlashlight`
  - `PropulsionTool` = push/pull rigidbodies
  - `SalvageSamplerTool` = cut/sample + secondary item pickup
  - `BeaconDeployerTool` = world beacon drop + nearest-beacon cleanup
  - `EnvironmentalAnalyzerTool` = target/suit readout via `HUDNotification`
  - `KnifeTool` = short-range melee hit
  - `StunPistolTool` = damage + temporary AI disable runtime helper
  - `HarpoonLauncherTool` = ranged hit + secondary reel impulse
- `ItemCatalog` was expanded to include the new tool item assets.
- All 12 tool items now support `DROP -> world pickup prefab` at the data level.
- Full scene-level tool rack now exists independently of the player's 4 active quick slots.
- There is now a formal provisioning path for:
  - adding tool items into inventory
  - assigning held prefabs into tool slots
  - notifying HUD/PDA when assignments change
- First runtime smoke-pass is now confirmed:
  - full tool kit successfully entered inventory at play start
  - core 4-slot loadout remained assigned in `PlayerToolManager`
  - flashlight runtime binding stayed alive in play mode
- There is still a legacy `Item_Builder_Device.asset` in the project; treat it as legacy until we intentionally migrate or remove it.
- 2026-03-28 enterprise hardening pass:
  - `BeaconDeployerTool` now emits deploy/retract/cap-trim feedback into the field log instead of acting as a silent marker spawner.
  - `EnvironmentalAnalyzerTool` now archives target and suit diagnostic reads into the field log.
  - `PropulsionTool` now reports no-lock / invalid-target / heavy-target states and logs successful push/pull impulses.
  - `KnifeTool` now reports contact / no-contact and logs melee engagement results.
  - `StunPistolTool` now reports `target disrupted`, `no bioform circuit`, and `no target lock` states with field-log entries.
  - `HarpoonLauncherTool` now reports strike / clear-shot / reel failure / reel success with field-log entries.
  - `FlashlightTool` now records lamp toggles and status checks in the field log.
- `Validate Tool Stack` still passes clean after this pass.
- 2026-03-28 loadout management pass:
  - added asset-based loadout presets for:
    - `EXPLORATION`
    - `CONSTRUCTION`
    - `FIELD RECOVERY`
    - `DEFENSE`
  - presets are authored under `Assets/_Project/Data/Tools/Presets`
  - `PlayerToolManager` can now apply a preset directly
  - `ToolLoadoutProvisioner` can now use a `startupPreset` instead of only the hardcoded core quick slots
- 2026-03-28 PDA loadout pass:
  - `PDALoadoutTab` now surfaces those presets directly in the player-facing PDA
  - the loadout screen can apply presets without going through dev-only helpers
  - the summary line now reports the matched preset name or `CUSTOM`
  - presets still mean "slot arrangement", not "grant items for free"
- 2026-03-28 repair pass:
  - `RepairTool` now gives explicit operator feedback for no-target, invalid-target, sealed-target, repair-in-progress, and repair-complete states
  - secondary action now works as a quick module condition readout
  - repair operations now write into the field log
- 2026-03-28 analyzer persistence pass:
  - `EnvironmentalAnalyzerTool` now archives target analysis into `ScanLogSystem`
  - suit diagnostics now also produce persistent suit-status entries
  - analyzer is now closer to a real knowledge-gathering tool instead of only transient HUD text
- 2026-03-28 stun pass:
  - `StunPistolTool` now has a useful secondary action for checking target disruption state
  - stun recovery now writes back into the field log
  - the tool now supports a more tactical combat loop instead of only blind primary-fire usage
- 2026-03-28 beacon network pass:
  - added `BeaconNetworkSystem` as a real saved gameplay system instead of a temporary static list inside `BeaconDeployerTool`
  - deployed markers now receive stable labels like `BEACON-01`
  - active beacon positions and light/color state now persist through save/load
  - `PDADataLogTab` now shows active beacon count and nearest marker summary
  - live scene now carries `BeaconNetworkSystem` on `Player`
- 2026-03-29 propulsion handling pass:
  - `PropulsionTool` can now acquire a tractor lock on a valid rigidbody
  - locked targets can be held in front of the player, released, or launched
  - invalid, too-heavy, and lost-lock states now report clearly instead of failing as a vague shove/pull
  - compile and short game run stayed clean after the pass
- 2026-03-29 flashlight mode pass:
  - `PlayerFlashlight` now supports `STANDARD / FLOOD / FOCUS` beam profiles
  - `FlashlightTool` secondary action now cycles beam mode and logs a richer status snapshot
  - operator feedback now includes beam mode, energy, heat, and cooldown state
  - compile and short game run stayed clean after the pass
- 2026-03-29 harpoon tether pass:
  - `HarpoonLauncherTool` now creates a short tether lock on valid struck targets
  - secondary action first consumes that tether for a stronger reel, then falls back to the old raw reel path
  - the tool now has a more coherent strike -> control -> reel loop
  - compile and short game run stayed clean after the pass
- 2026-03-29 knife tactical pass:
  - `KnifeTool` now has a secondary tactical readout instead of only a plain slash
  - it can assess nearby bioforms, resource nodes, and base modules
  - critically weakened targets can now receive a stronger precision strike
  - compile and short game run stayed clean after the pass
- 2026-03-29 stun tactical readout pass:
  - `StunPistolTool` secondary action now evaluates real bioform state instead of only reporting vulnerable/disrupted
  - it now distinguishes aggressive, panicked, patrolling, dormant, fractured, downed, and already-disrupted targets
  - stun feedback now includes a direct tactical recommendation for the operator
  - secondary checks are now latched to avoid repeated spam while the button is held
  - compile and short game run stayed clean after the pass
- 2026-03-29 propulsion cargo assessment pass:
  - `PropulsionTool` now evaluates target mass bands instead of reporting only generic success/fail states
  - it distinguishes anchored structures, masses beyond safe handling, light cargo, normal cargo, and heavy-but-safe cargo
  - lock / hold / launch feedback now includes direct handling advice for the operator
  - compile and short game run stayed clean after the pass
- 2026-03-29 beacon navigation-role pass:
  - `BeaconDeployerTool` now classifies markers by field role: `ANCHOR / LOCAL MARK / RELAY / FRONTIER`
  - deployment feedback now explains how the new marker extends the network
  - nearest-beacon checks now report role and practical use, not only distance
  - compile and short game run stayed clean after the pass
- 2026-03-29 harpoon control-readout pass:
  - `HarpoonLauncherTool` now classifies hostile bioforms, weakened targets, safe cargo, overloaded cargo, and anchored objects
  - tether and reel feedback now gives direct advice about control, spacing, recovery, or disengagement
  - compile and short game run stayed clean after the pass
- 2026-03-29 flashlight expedition-guidance pass:
  - `PlayerFlashlight` now exposes a real operational summary and recommendation layer
  - `FlashlightTool` now reports readiness, low energy, rising heat, and cooling lockout in plain language
  - `STANDARD / FLOOD / FOCUS` now read as actual expedition roles instead of only mode names
  - compile and short game run stayed clean after the pass
- 2026-03-29 analyzer expedition-risk pass:
  - `EnvironmentalAnalyzerTool` now reports intermediate suit risk bands, not only full emergencies
  - item targets are now classified by field role instead of all looking like generic pickups
  - depleted resource nodes and sleeping bioforms now get correct readouts
  - compile and short game run stayed clean after the pass
- 2026-03-29 scanner sweep-interpretation pass:
  - `ScannerTool` now adds practical recommendations to scan results instead of only reporting counts
  - resource, structure, and expedition sweeps now explain what the player should do next
  - compile and short game run stayed clean after the pass
- 2026-03-29 knife close-quarters readout pass:
  - `KnifeTool` now gives more useful close-range readouts for bioforms, resource nodes, and modules
  - dormant, hostile, fractured, dense, salvageable, and depleted targets now read differently
  - compile and short game run stayed clean after the pass
- 2026-03-29 repair service-priority pass:
  - `RepairTool` diagnosis now includes explicit service priority in addition to damage state
  - repair reads now distinguish blocked service, stabilizing drain cycles, active service, final pass, and critical response
  - compile and short game run stayed clean after the pass
- 2026-03-29 builder field-guidance pass:
  - `PlayerBuilder` now exposes family, role, and purpose-oriented guidance for the active module
  - `BuilderStatusOverlay` now shows module family code, richer role information, and live build advice
  - builder HUD and notifications now explain why placement is ready, snapped, blocked, or missing cost instead of only showing a raw state
  - compile and short game run stayed clean after the pass
## 2026-03-29 - Tool Progression Link

- `Scanner` now has real progression value beyond archive text:
  - unlocked scan entries can gate fabrication blueprints
- Starter fabrication blueprints currently linked to scan data:
  - `Field Beacon`
  - `Environmental Analyzer`
  - `Salvage Sampler`

Meaning:
- tools are now connected to find -> scan -> unlock -> craft loop
## 2026-03-29 - Tool progression link

- Tool progression is now connected to fabrication:
  - scan unlocks can gate recipes
  - starter fabrication recipes exist for:
    - `Beacon Deployer`
    - `Environmental Analyzer`
    - `Salvage Sampler`
    - `Flashlight`
    - `Scanner`
    - `Repair Tool`
- Live delivery points now exist in scene:
  - `Fabrication_Trial/Trial_Fabricator`
  - `--- WORLD ---/Fabrication_Outpost/Forward_Fabricator`
- `HectonFabricatorUI` is now attached to the live HUD canvas, so fabrication is no longer code-only.
