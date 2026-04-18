**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Tools Enterprise Sprint

Do not replace this file wholesale. Append progress and keep the checklist honest.

## Goal

Bring every player-held tool in Hecton-8 to a complete endgame-quality state.

This does not mean "good enough for now".
This means every tool should eventually feel like a real late-game expedition instrument:
- clear role in exploration / survival / combat / construction
- stable runtime behavior
- strong and readable player feedback
- proper PDA / loadout / inventory integration
- useful late-game interactions and tactical value
- reliable validation and test coverage

Primary reference:
- Subnautica-style tool usefulness and clarity
- but adapted to Hecton-8 tone, systems, and deeper expedition feel

## Full Tool Roster

### Core expedition tools
- [ ] `ScannerTool`
- [ ] `RepairTool`
- [ ] `BuilderTool`
- [ ] `LaserCutter`

### Utility / support tools
- [ ] `FlashlightTool`
- [ ] `PropulsionTool`
- [ ] `SalvageSamplerTool`
- [ ] `BeaconDeployerTool`
- [ ] `EnvironmentalAnalyzerTool`

### Combat / defense tools
- [ ] `KnifeTool`
- [ ] `StunPistolTool`
- [ ] `HarpoonLauncherTool`

## Enterprise baseline checklist

Each tool should eventually satisfy this baseline:
- [ ] correct `ItemData` + `ToolMetadata` + held prefab linkage
- [ ] deterministic equip / use / holster behavior through `PlayerToolManager`
- [ ] clear success / fail / cooldown feedback
- [ ] no silent no-op when target or context is invalid
- [ ] meaningful `HUDNotification` output
- [ ] field-log integration if the action is tactically relevant
- [ ] PDA/loadout readiness remains coherent
- [ ] compile clean
- [ ] validator coverage or smoke coverage exists

## Endgame functionality target

By the end of this sprint, each tool should answer:
- [ ] why do I keep this tool in late game?
- [ ] what can it do that no other tool covers as well?
- [ ] how does it help exploration, survival, base work, combat, or logistics?
- [ ] does the player understand when it works, why it fails, and what to do next?
- [ ] is it worth a quick-slot in a real expedition loadout?

If the answer is weak, the tool is not done.

## Tool control and usability target

The tool system itself must also improve, not only each tool script.

Things to build toward:
- [ ] better quick-slot logic and switching safety
- [ ] clearer active / inactive / unavailable states
- [ ] stronger PDA loadout control
- [ ] good field-side feedback when a tool is missing, blocked, overheated, empty, or context-invalid
- [ ] sensible default loadouts for early, mid, and late game
- [ ] clean separation between:
  - held tool prefab
  - world pickup
  - inventory item
  - PDA/loadout assignment

Subnautica-style inspiration points:
- each tool has a strong identity
- quick slots matter
- switching tools feels deliberate
- the PDA explains the state of the expedition, not just the raw inventory

Important clarification:
- tool loadout presets are slot arrangements, not free tool rewards
- they define which already-owned tools should sit in quick slots for a given mission type
- the player should still mostly acquire the tools themselves through crafting, discovery, barter, or progression
- presets are useful for:
  - testing
  - development balancing
  - later player-facing loadout management in PDA

## Current audit

### Stronger already
- `ScannerTool`: strong scan loop, scan-log integration, field feedback
- `LaserCutter`: strong heat/deconstruct loop, recovery intel, field-log integration
- `BuilderTool` + `PlayerBuilder`: construction loop exists, PDA integration exists
- `SalvageSamplerTool`: usable first-pass with recovery intel and field-log integration

### Weaker / likely next hardening targets
- `BeaconDeployerTool`: deploy/retract works, persistent beacon network now exists, next pass is richer operator workflow and authored beacon use-cases
- `EnvironmentalAnalyzerTool`: mostly HUD text, weak persistence / operational impact
- `PropulsionTool`: physically works, but lacks strong operator feedback and tactical logging
- `KnifeTool`: basic melee, no tactical readout / confirmation path
- `StunPistolTool`: basic disable loop, weak feedback and no recovery reporting
- `HarpoonLauncherTool`: basic hit/reel loop, weak feedback and no mission history
- `FlashlightTool`: safe adapter, but minimal expedition-grade reporting

### 2026-03-29 - builder field-guidance pass
- `PlayerBuilder` now exposes clearer operational labels for the active module:
  - family code
  - build role
  - clearer purpose text
- builder advice is now more useful in the field:
  - missing cost explains the module purpose before listing needed materials
  - blocked placement explains whether the socket is aligned but volume is obstructed
  - ready and snapped-ready states now tell the player why this module matters
- `NotifyBuildableSelection`, `NotifyMissingResources`, `NotifyBuildBlocked`, and `NotifyBuildPlaced` now carry richer builder context instead of only raw status text
- `BuilderStatusOverlay` now shows:
  - module family short code beside the module name
  - role line instead of only a generic power line
  - contextual build advice in the bottom line instead of a static controls legend
- `PDAConstructionTab` now mirrors the same language:
  - purpose line on module cards
  - directive text now reuses the live builder advice instead of generic canned text
- verified through Unity MCP:
  - compile clean
  - short game run clean
  - console clean

## Ordered pass plan

### Pass 1 - Shared hardening
- [ ] unify field-operation feedback across all non-core tools
- [ ] ensure no important tool acts silently on failure
- [ ] keep logs/backlog updated
- [x] review tool switching and slot control flow as part of the same pass
- [x] add a first real loadout-preset system for exploration / construction / recovery / defense

### Pass 2 - Utility tool depth
- [ ] `BeaconDeployerTool`
- [ ] `EnvironmentalAnalyzerTool`
- [ ] `PropulsionTool`
- [ ] `FlashlightTool`

### Pass 3 - Combat / defense depth
- [ ] `KnifeTool`
- [ ] `StunPistolTool`
- [ ] `HarpoonLauncherTool`

### Pass 4 - Core parity pass
- [ ] re-audit `ScannerTool`
- [ ] re-audit `RepairTool`
- [ ] re-audit `BuilderTool`
- [ ] re-audit `LaserCutter`

## Progress log

### 2026-03-28
- Sprint initialized.
- Roster fixed as the 12-tool baseline for the enterprise hardening pass.
- First active target: bring the weaker non-core tools up to the same feedback/field-log baseline as scanner/salvage/cutter.
- Main goal clarified:
  - all tools must reach full endgame usefulness, not just early prototype completeness
  - tool management and control flow are now part of the sprint, not a separate afterthought
- Tool control pass started:
  - added `ToolLoadoutPreset`
  - added authoring menu to rebuild starter presets
  - added `PlayerToolManager.ApplyLoadoutPreset(...)`
  - added `ToolLoadoutProvisioner.startupPreset`
- PDA loadout control pass continued:
  - `PDALoadoutTab` now has a live preset strip
  - player can apply `EXPLORATION / CONSTRUCTION / FIELD RECOVERY / DEFENSE` directly from PDA
  - loadout summary now shows whether the current slot layout matches a known preset or is `CUSTOM`
  - this is for slot management only, not free tool rewards
- `RepairTool` pass started:
  - primary use now gives clear messages for:
    - no target
    - invalid target
    - module already sealed
    - active repair
    - full restoration complete
  - secondary use now works as a quick module diagnostic ping
  - repair actions now write into the field operations log
- `EnvironmentalAnalyzerTool` pass continued:
  - target reads now archive persistent analyzer intel into `ScanLogSystem`
  - suit diagnostics now also archive a persistent suit-status entry
  - this pushes analyzer closer to a real expedition instrument instead of a temporary text popup
- `StunPistolTool` pass started:
  - secondary action now works as a tactical target check
  - it tells the player whether the target is vulnerable or already disrupted
  - recovery from stun now also leaves a proper field-log trace
- `BeaconDeployerTool` pass started:
  - beacon deployment is no longer stored in a temporary static list inside the tool
  - added `BeaconNetworkSystem` with save/load support
  - deployed beacons now get stable labels like `BEACON-01`
  - PDA `Data Log` now shows active beacon count and nearest marker
  - scene now has a live `BeaconNetworkSystem` on `Player`
- `PropulsionTool` pass started:
  - secondary action can now acquire a stable tractor lock instead of only applying a raw pull force
  - locked targets can be held in front of the player, released, or launched
  - invalid / lost / heavy targets now give clear operator feedback
  - propulsion handling now reads more like a late-game utility tool and less like a generic physics shove
- `FlashlightTool` pass started:
  - added real beam profiles: `STANDARD / FLOOD / FOCUS`
  - secondary action now cycles beam mode instead of only showing a shallow status popup
  - flashlight status now reports beam mode, energy, heat, and cooldown state
  - overheated lamp now gives a clear cooldown message instead of feeling dead or broken
- `HarpoonLauncherTool` pass started:
  - successful shots can now create a short tether lock on suitable targets
  - secondary action now first uses that tether for a stronger reel before falling back to a raw reel impulse
  - this gives harpoon a real linked combat/control cycle instead of two mostly separate buttons
- `KnifeTool` pass started:
  - secondary action now reads the target instead of doing nothing special
  - wounded bioforms, weakened resource nodes, and damaged modules now give a quick close-range readout
  - critically weakened targets can receive a stronger precision strike
  - this gives the knife a proper emergency-finish and close-inspection role
- `EnvironmentalAnalyzerTool` pass advanced:
  - target reads now produce real risk assessments instead of flat text
  - analyzer now classifies item / resource / module / bioform / mass-object targets
  - each read now carries a plain recommendation about what to do next
  - suit diagnostics now classify hull / oxygen / power / pressure status into clear severity bands
  - analyzer output is now closer to an expedition decision tool instead of a generic scanner popup
- `ScannerTool` re-audit started:
  - scanner now has real scan modes instead of one flat pulse
  - `EXPEDITION` gives a broad sweep
  - `RESOURCE` prioritizes resource signatures and cached pickups
  - `STRUCTURE` prioritizes modules and authored intel contacts
  - scan result text now changes by mode instead of always saying only `CONTACTS N`
  - field log now records which kind of sweep was completed and what it actually found
- `BeaconDeployerTool` logistics pass advanced:
  - secondary action no longer behaves like a blind retract button at all distances
  - when far away, it now reports the nearest active beacon and distance
  - when close enough, it retracts the nearest beacon cleanly
  - deploy and retract feedback now also reports the active grid count
- `RepairTool` diagnostics pass advanced:
  - quick diagnosis now reports real module state, not only a percent
  - it distinguishes sealed, heavy damage, critical damage, flooded, draining, and no-power flooded cases
  - repair start now uses the same diagnosis layer, so the tool tells the player what kind of repair situation they are entering
  - diagnosis entries now carry a direct recommendation about what to do next
- `LaserCutter` clarity pass advanced:
  - secondary action now works as a real cutter diagnosis instead of being empty
  - it distinguishes no target, resource contact, general cuttable contact, recovery-ready module, and locked module
  - recovery mode now reports actual deconstruction progress while the beam is held
  - overheat recovery now also gives a clear “core stable” return message
- `SalvageSamplerTool` clarity pass advanced:
  - primary action now reports active extraction instead of only speaking when it fails
  - secondary action now distinguishes recovery-ready salvage, active resource nodes, depleted nodes, process-only targets, and invalid targets
  - successful recovery now reports the actual recovered item name
  - shared collectible inspection was moved into `ToolHitUtility` so salvage logic stays clean and reusable
- `StunPistolTool` tactical pass advanced:
  - secondary action now gives a real target read instead of only saying vulnerable/disrupted
  - it now distinguishes aggressive threats, panic response, patrol contacts, dormant contacts, fractured targets, and already-disrupted targets
  - stun feedback now carries a plain recommendation about whether to fire, disengage, reposition, or finish another target
  - repeated secondary checks are now latched, so holding the button no longer spams the same target assessment
- `PropulsionTool` cargo-assessment pass advanced:
  - tractor and impulse handling now classify targets by mass band instead of giving only flat success/fail text
  - it now distinguishes anchored structures, unsafe heavy masses, light cargo, normal workload cargo, and heavy-but-safe cargo
  - lock, hold, launch, and invalid-target states now all tell the player what to do next
  - propulsion feedback is now closer to a late-game logistics tool instead of only a physics shove
- `BeaconDeployerTool` navigation-role pass advanced:
  - newly placed beacons now get a meaningful field role instead of being only another marker in the grid
  - the tool now distinguishes `ANCHOR`, `LOCAL MARK`, `RELAY`, and `FRONTIER`
  - nearest-beacon checks now explain what the marker is doing in the network, not just how far away it is
  - beacon deployment is now closer to a real route-building and logistics tool
- `HarpoonLauncherTool` control-readout pass advanced:
  - the harpoon now evaluates whether the target is a hostile bioform, weakened bioform, safe cargo, overloaded cargo, or an anchored object
  - tether and reel feedback now explains whether the line should be used for control, recovery, spacing, or abandoned
  - the tool is now closer to a real strike-and-control weapon instead of only a hit-and-pull gimmick
- `FlashlightTool` expedition-guidance pass advanced:
  - the lamp now explains not only the beam mode, but also what that mode is good for in the field
  - it now distinguishes normal readiness, low energy, rising heat, and cooling lockout
  - flood, focus, and standard are now described as real expedition roles instead of just three names
  - flashlight feedback is now closer to a real deep-exploration support tool
- `EnvironmentalAnalyzerTool` expedition-risk pass advanced:
  - suit diagnostics now warn not only about disasters, but also about approaching bad states
  - it now distinguishes oxygen watch, power watch, hull warning, and pressure watch before a full emergency starts
  - field items are now classified by role: tools, materials, equipment, consumables, and components
  - depleted resource nodes and sleeping bioforms now read correctly instead of looking like generic valid targets
- `ScannerTool` sweep-interpretation pass advanced:
  - scan pulses now explain what the result means, not only how many contacts were found
  - each scan mode now gives a concrete next-step recommendation
  - sparse, dense, structural, and resource sweeps now feel more like expedition decisions and less like raw counters
- `KnifeTool` close-quarters readout pass advanced:
  - blade readouts now explain whether a target is dormant, hostile, fractured, dense, salvageable, or already depleted
  - the knife now gives clearer advice about when to finish, when to avoid, and when to swap to another tool
  - this makes the blade closer to a real emergency field tool instead of only a melee backup
- `RepairTool` service-priority pass advanced:
  - repair diagnosis now includes not only damage state, but also service priority
  - the tool now distinguishes `CRITICAL RESPONSE`, `IMMEDIATE SERVICE`, `ACTIVE SERVICE`, `FINAL PASS`, `STABILIZING`, `SERVICE BLOCKED`, and `SERVICE COMPLETE`
  - service reads now tell the player more clearly whether to repair now, wait for drainage, restore power first, or stop work
- `BuilderTool` and `PlayerBuilder` pass advanced:
  - builder now exposes a proper readiness state instead of only raw booleans
  - missing materials now report a real cost digest instead of a vague warning
  - placement blocked / snapped ready / ready / offline states now carry plain advice
  - build and deconstruct actions now leave clearer field-operation records
  - builder screen color now reflects blocked state separately from missing-cost state
- Active tool HUD pass advanced:
  - `PlayerTool` now exposes a shared operational summary and directive API
  - `PlayerToolManager` now exposes current-tool summary and directive for HUD/UI consumers
  - a separate `ToolStatusOverlay` was attempted, but Unity did not import the type reliably despite a clean compile
  - instead, the current tool summary and directive were moved into `HUDQuickBar`, which is already a stable scene/UI path
  - the dead experimental `ToolStatusOverlay` scene object was removed
- Shared active-tool HUD pass continued:
  - `HUDQuickBar` now refreshes current tool status on a light timer, so live heat, repair, salvage, and tractor states are not frozen between slot switches
  - `RepairTool`, `SalvageSamplerTool`, `PropulsionTool`, and `LaserCutter` now provide real shared summary/directive strings instead of falling back to generic base text
  - this starts turning the whole tool stack into one readable expedition-control layer instead of many disconnected one-off messages
- Shared active-tool HUD coverage completed for the full roster:
  - `BeaconDeployerTool`, `EnvironmentalAnalyzerTool`, `KnifeTool`, `StunPistolTool`, and `HarpoonLauncherTool` now also provide dedicated shared summary/directive strings
  - all 12 tools now feed one common active-slot status layer instead of only ad-hoc per-tool popups
  - this closes the base readability problem for quick-slot use and sets up the next step: richer authored gameplay situations for each tool
- Shared active-tool control layer hardened:
  - `Hecton/Validation/Validate Tool Operational HUD` now proves that the full held-tool roster actually overrides the shared status methods
  - `PDALoadoutTab` now shows the live active-tool summary and directive, so tool state is readable both on HUD and inside PDA loadout management
- Authored field-scenario pass started:
  - `Hecton/Authoring/Rebuild Tool Trial Range` now creates a reusable live scene range for cargo, salvage, service modules, and beacon route checks
  - this is the first real authored bridge between enterprise tool logic and repeatable in-world validation
- Authored field-scenario pass expanded:
  - `Tool Trial Range` now also builds:
    - `Lane_DarkRoute` for flashlight guidance and close dark-space salvage
    - `Lane_ScanCorridor` for scanner/analyzer authored POIs
  - `Hecton/Validation/Validate Tool Trial Range` now passes clean
  - the range is no longer only a loose scene helper; it is now a validated authored fixture for repeated live checks
- Context-aware expedition guidance pass started:
  - `FlashlightTool` now reads nearby forward context and recommends beam usage more intelligently:
    - close pickups -> `FLOOD`
    - distant probes / hazards / modules -> `FOCUS`
    - near service faces -> `STANDARD`
  - `EnvironmentalAnalyzerTool` now properly reads:
    - `PickupItem`
    - `ScannableTarget`
  - this closes a real authored-world gap for the new dark route and scan corridor lanes
- Scanner live-read pass advanced:
  - `ScannerTool` now remembers the latest sweep result for a short window and feeds that back into the active-tool HUD
  - scanner recommendations are now more specific for authored POIs:
    - hazard probes
    - resource pockets
    - structural relays
    - expedition checkpoints
  - this makes the scan corridor act like a real route-reading tool, not only a contact counter
- Salvage lane authoring pass advanced:
  - `Lane_Salvage` now contains:
    - recoverable pickups
    - one active `ResourceNode`
    - one depleted `ResourceNode`
  - this gives real authored states for:
    - `SalvageSamplerTool`
    - `LaserCutter`
    - `EnvironmentalAnalyzerTool`
    - `KnifeTool`
  - `Validate Tool Trial Range` still passes after the new resource-node coverage
- Salvage/service diagnosis pass advanced:
  - `SalvageSamplerTool` now reads resource nodes through parent colliders and distinguishes:
    - active dense node
    - weakened node
    - nearly-open node
    - depleted node
  - `LaserCutter` now reads modules and resource nodes through parent colliders, not only the exact hit collider
  - `KnifeTool` node readouts now include clearer break-state percentages
  - this makes the authored salvage lane much more useful for real field decisions
- Service-lane module pass advanced:
  - `RepairTool` now resolves `BaseModule` through parent colliders in all main paths:
    - primary repair
    - secondary diagnosis
    - live HUD diagnosis
  - `LaserCutter` now distinguishes service-module states more clearly:
    - flooded module
    - breached module
    - sealed locked module
  - this makes the authored service lane much closer to real expedition maintenance checks
- Cargo-lane descriptor pass started:
  - added `FieldTargetDescriptor` as a shared authored semantic layer for live field targets
  - `Tool Trial Range` now tags cargo crates, route markers, salvage pickups, scan pickups, scannable probes, and resource nodes with explicit roles
  - `PropulsionTool` and `HarpoonLauncherTool` now read those roles and produce authored guidance for:
    - precision cargo
    - work cargo
    - heavy salvage
    - overweight lane blockers
  - `Validate Tool Trial Range` still passes after descriptor coverage was added
- Beacon-route authored guidance pass started:
  - `BeaconDeployerTool` now reads nearby authored route markers through `FieldTargetDescriptor`
  - deployment and nearest-beacon checks can now align to:
    - `ANCHOR`
    - `RELAY`
    - `FRONTIER`
  - this turns the beacon lane into a real route-discipline fixture instead of a decorative marker row
- Descriptor-aware recon pass started:
  - `EnvironmentalAnalyzerTool` now reads authored route markers and cargo roles through `FieldTargetDescriptor`
  - `ScannerTool` now sees authored:
    - cargo contacts
    - route markers
    - resource cache roles
    - expedition checkpoints
  - this makes the scan/dark/cargo lanes read like one coherent authored field system instead of separate one-off helpers
- Flashlight route-awareness pass started:
  - `FlashlightTool` now also understands authored route markers and cargo roles through `FieldTargetDescriptor`
  - this aligns dark-route guidance with the same semantic layer used by analyzer, scanner, propulsion, harpoon, and beacon workflows
- Shared authored-semantics refactor completed:
  - added `FieldTargetSemantics.cs` as the common route/cargo interpretation layer
  - refactored these tools to read the shared helper instead of carrying duplicate route/cargo switches:
    - `FlashlightTool`
    - `EnvironmentalAnalyzerTool`
    - `PropulsionTool`
    - `HarpoonLauncherTool`
    - `BeaconDeployerTool`
  - quality gates remain green after the refactor:
    - `Validate Tool Trial Range`
    - `Validate Tool Operational HUD`
- Trial-range runtime harness started:
  - added `ToolTrialRangeRuntimeSmokeTester.cs`
  - it contains separate `Logistics` and `Recon` runtime passes for:
    - `Propulsion / Harpoon / Beacon`
    - `Flashlight / Analyzer / Scanner`
  - the harness is now attached to `Player` with `runOnStart = false` by default
- Combat authored lane started:
  - `Tool Trial Range` now includes `Lane_CombatContacts`
  - authored targets added:
    - `Combat_Dormant`
    - `Combat_Aggressive`
    - `Combat_Fractured`
    - `Combat_Down`
    - `Combat_Checkpoint`
  - these targets use new `FieldTargetRole` combat states instead of relying on fragile live-AI scene setup
- Combat semantic layer started:
  - `FieldTargetDescriptor` now supports:
    - `BioformDormant`
    - `BioformAggressive`
    - `BioformFractured`
    - `BioformDown`
  - `FieldTargetSemantics` now provides shared combat assessments for:
    - analyzer
    - stun pistol
    - knife
    - harpoon
- Combat tool pass started:
  - `StunPistolTool` now reads authored combat descriptors when a live AI is not present
  - `KnifeTool` now reads authored combat descriptors for close-range tactical guidance
  - `HarpoonLauncherTool` now reads authored combat descriptors for control/tether guidance
  - `EnvironmentalAnalyzerTool` inherits combat authored readouts through the shared semantic layer
  - `ScannerTool` now counts and reports descriptor-driven bioform contacts in expedition sweeps
- Trial-range runtime harness expanded:
  - `ToolTrialRangeRuntimeSmokeTester` now includes a `Combat` pass for:
    - `EnvironmentalAnalyzerTool`
    - `StunPistolTool`
    - `KnifeTool`
    - `HarpoonLauncherTool`
- Service semantic layer expanded:
  - `Lane_ServiceModules` targets now carry `FieldTargetDescriptor` roles:
    - `ServiceDamaged`
    - `ServiceFlooded`
    - `ServiceControl`
  - analyzer, scanner, and flashlight can now read service modules through the same authored semantic layer as cargo, route, recon, and combat lanes
- Loadout advice layer started:
  - added `FieldLoadoutAdvisor.cs`
  - the advisor now recommends practical presets from the live forward target:
    - `EXPLORATION`
    - `CONSTRUCTION`
    - `FIELD RECOVERY`
    - `DEFENSE`
  - `PDALoadoutTab` now shows:
    - matched preset
    - recommended preset
    - field advice summary
  - `HUDQuickBar` now appends the recommended preset to the live active-tool directive
  - `PDADataLogTab` footer now surfaces recommended field kit advice when a relevant target is ahead
- Trial-range endgame coverage expanded:
  - `ToolTrialRangeRuntimeSmokeTester` now covers:
    - `Logistics`
    - `Recon`
    - `Recovery`
    - `Service`
    - `Combat`
    - `Construction`
  - each pass now prints its own explicit `PASS/FAIL` line so runtime checks are easier to read in the console
- `LaserCutter` operational HUD is now target-aware in normal ready state:
  - aimed resource nodes now show live cutter diagnostics without needing a separate secondary ping
  - aimed service modules now expose recovery/lock/contact state directly through summary + directive
- Honest note:
  - short Unity MCP runtime probes still do not always surface the new pass logs back reliably
  - validators and compile/play remain green, so this is still a tooling-observability tail, not a product blocker
- Endgame operations lane started:
  - added `Lane_EndgameOps` to `Tool_TrialRange`
  - this lane now chains multiple field roles in one route:
    - route anchor
    - work cargo
    - salvage pickup
    - flooded service module
    - hazard probe
    - aggressive combat contact
    - route frontier
  - product goal:
    - test tools and loadout advice as one expedition flow instead of separate isolated cubes
- Endgame advice/runtime coverage started:
  - `ToolTrialRangeRuntimeSmokeTester` now includes an `Endgame` pass
  - that pass verifies loadout recommendation transitions across the mixed lane:
    - `FIELD RECOVERY`
    - `CONSTRUCTION`
    - `EXPLORATION`
    - `DEFENSE`
- PDA loadout control improved:
  - `PDALoadoutTab` now has a direct `APPLY RECOMMENDED` action
  - field advice is no longer just text; the player can apply the recommended preset from the loadout screen immediately
  - when the recommended preset is already active, the button switches to a stable `RECOMMENDED ACTIVE` state
- Construction late-game semantics started:
  - added authored construction roles:
    - `ConstructionSocket`
    - `ConstructionBlocked`
    - `ConstructionClear`
  - these are now understood by:
    - `FieldLoadoutAdvisor`
    - `ScannerTool`
    - `EnvironmentalAnalyzerTool`
    - `FlashlightTool`
- Added a dedicated `Lane_ConstructionOps` to `Tool Trial Range`:
  - `Construct_SocketBase`
  - `Construct_ClearLane`
  - `Construct_Blocker`
  - `Construct_SocketGuide`
  - this gives the builder/construction loop its own authored late-game surface instead of piggybacking only on service-module checks
- Player-choice framing improved:
  - loadout advice text is now softer and advisory instead of sounding mandatory
  - `PDALoadoutTab` now says `SUGGESTED` rather than `RECOMMENDED`
  - added `Lane_ChoiceHub` so the trial range now includes an explicit multi-branch choice point instead of only linear lanes
  - choice hub branches currently point toward:
    - recovery
    - construction
    - defense
## 2026-03-29 - Scan to Blueprint Foundation

- Added real blueprint gating to crafting.
- `RecipeData` now supports optional `requiredScanEntryId`.
- `Fabricator` now hides locked recipes until matching scan-log entries are unlocked.
- `HectonFabricatorUI` now shows `SCAN DATA REQUIRED` instead of falsely implying there are no recipes when blueprints are merely locked.
- Added repeatable authoring/validation workflow:
  - `Hecton/Authoring/Rebuild Starter Fabrication Kit`
  - `Hecton/Validation/Validate Starter Fabrication Kit`
- Added starter fabrication content:
  - `Recipe_FieldBeacon`
  - `Recipe_EnvAnalyzer`
  - `Recipe_SalvageSampler`
- Added live scene object:
  - `Fabrication_Trial/Trial_Fabricator`

Result:
- scanner progress now has real downstream value for tool progression
- tools are no longer isolated from crafting progression

## 2026-03-29 - Real fabricator UI + world station

- Closed a real gap in the crafting loop:
  - `HectonFabricatorUI` was missing from the live HUD scene entirely
  - it is now attached to `--- UI ---/Suit_HUD_Canvas`
- Hardened `HectonFabricatorUI.cs` so it no longer depends on perfect manual setup:
  - auto-resolves `hudCamera`
  - auto-resolves `PlayerInventory`
  - auto-resolves a usable TMP font
  - subscribes to `RebindingManager` safely without forcing a dummy singleton into existence
- Expanded `FabricationBootstrapAuthoring.cs`:
  - starter fabrication rebuild now creates both:
    - `Fabrication_Trial/Trial_Fabricator`
    - `--- WORLD ---/Fabrication_Outpost/Forward_Fabricator`
- Expanded starter fabrication recipes:
  - `Field Beacon`
  - `Environmental Analyzer`
  - `Salvage Sampler`
  - `Dive Flashlight`
  - `Survey Scanner`
  - `Repair Tool`
- Added dev smoke hook:
  - `FabricationRuntimeSmokeTester`
  - attached to `Player`
  - kept disabled by default after probe
- Result:
  - scan-gated recipes now have both UI and world delivery
  - fabrication is no longer locked to a trial-only prop

## 2026-03-29 - Tools now sit on a real resource baseline

- Added a live resource economy baseline under the tool layer:
  - 20 raw resources
  - 19 intermediate components
  - 19 component recipes
- Starter fabrication is no longer raw-copper straight into tools.
- The live fabricator path now supports:
  - crafting core components
  - crafting starter tools from those components
- This is the first real step toward full endgame tool progression instead of placeholder economy glue.

## 2026-03-30 - Power and service field lane added

- Extended the authored field-target layer with explicit power roles:
  - `PowerGeneration`
  - `PowerRelay`
  - `PowerLoad`
- Added a dedicated `Lane_PowerOps` to `Tool_TrialRange`:
  - `Power_CurrentTurbine`
  - `Power_RelayPylon`
  - `Power_ServicePump`
  - `Power_ServiceRoute`
  - `Power_ExposedGuide`
- These targets are now understood by:
  - `FieldTargetSemantics`
  - `FieldLoadoutAdvisor`
  - `ScannerTool`
  - `EnvironmentalAnalyzerTool`
  - `FlashlightTool`
- Expanded `ToolTrialRangeRuntimeSmokeTester` with a dedicated `power` pass so the new lane sits inside the same live tool loop as logistics, recon, service, combat, construction, and endgame.
- Validation through Unity MCP:
  - `Hecton/Authoring/Rebuild Tool Trial Range`
  - `Hecton/Validation/Validate Tool Trial Range` -> `PASS`
  - `Hecton/Validation/Validate Tool Operational HUD` -> `PASS`
- Scene saved:
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
