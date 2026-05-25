# SUBNAUTICA 2 PLAYER LOOP TO HECTON-8 GAP MATRIX

Date: 2026-05-17

Status: ACTIVE PLAYER-LOOP GAP MATRIX / RUNTIME PENDING

## Clean-Room Boundary

- Do not decompile Subnautica, Subnautica 2, or mod binaries.

- Do not copy proprietary assets, screenshots, audio, meshes, game data, or code.

- Public open-source mod repositories may be studied for API shape, but GPL/AGPL

  code cannot be imported into HECTON-8 without license review.

- Screenshot and trailer images are used only as visual-reference analysis.

## External Signal Checked

Primary public facts:

- Subnautica 2 Early Access is live. Steam lists release and Early Access release

  date as 14 May 2026.

- Steam positions the game as single-player plus online co-op with up to three

  friends, with base building, crafting, scanning/studying biodiversity, Tadpole

  submersible traversal, tools/equipment/vehicles expanding during Early Access,

  and 2-3 years as the expected Early Access duration.

- Official Unknown Worlds release post confirms Early Access started on 2026-05-14

  and can be played alone or with up to three friends.

- Official Unknown Worlds roadmap image says:

  - EA 1.1 quality-of-life update: Biomods System, Blight Encounters, Wrecks

    Gameplay, Vehicle Docking and Fabrication, PDA Databank, Voicelogs Priority

    System, more passive biomod slots, storage cache, sprint.

  - EA 1.2 co-op-centric update: HUD Signals, Base Builder Tool, Pinned Recipes

    System, Voice Chat, Emotes, Player Trading, Player Revive, Additional

    Customizations.

  - Future: expand the world, new biomes, creatures, resources, tools, vehicle,

    and next story chapter.

- Steam requirements are Windows/DX12/50 GB with GTX 1660/RX 5500 XT minimum and

  RTX 3070/RX 6700 XT recommended.

- SteamDB records detected technologies including Unreal Engine, EOS, FMOD,

  Intel oneTBB, DLSS/FrameGen/Reflex/Streamline, Ogg/Vorbis, and XAudio2. SteamDB

  labels Steam Deck compatibility as Steam Deck Verified in third-party metadata.

  Treat that label as public storefront metadata, not HECTON-8 platform proof.

- Secondary gameplay guides report that Biomods are unlocked through a Bioscanner

  / Biolab loop tied to scanning fauna/flora, and that co-op currently shares data

  entries, craftable recipes, vehicles, and world storage. Treat this as player

  observation, not source-of-truth.

## HECTON-8 Static Snapshot / Runtime Proof Pending

Static counts from this pass:

- `Assets/_Project/Data/Lore/AudioLogs`: 5 authored audio-log assets.

- `Assets/_Project/Data/Crafting/Recipes`: 41 authored recipe assets.

- `Assets/_Project/Data/Tools`: 13 authored `ToolMetadata_*.asset` files. Current known orphan/extra metadata is `ToolMetadata_LogicSpanner.asset`.

- `Assets/_Project/Data/Survival`: 13 authored survival/suit profile assets.

- Scene/prefab search for `NarrativeDiscovery` or `AudioLogPickup`: 0 authored

  placement hits in prefabs/scenes/assets during this pass.

- Scene/prefab search for scan surfaces found only one asset hit:

  `Assets/_Project/Prefabs/Item_Titanium.prefab`.

Important implementation reality:

- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`

  - Real system, not a facade.

  - Uses a hash-only `NativeQueue<uint>` playback queue with capacity 16.

  - Has discovered hash sets/dictionaries, packed save bits, encrypted fragment

    state, and fallback legacy string save list.

  - Missing compared to Subnautica 2 roadmap surface: no proven authored density,

    no explicit voice-log priority/interrupt policy, and no route gate that fails

    builds when early POIs are absent.

- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`

  - Reads PDA catalog and packed unlock words from the lore database.

  - Has catalog/lore hash caches and archive presentation.

  - Missing: no hard proof of visible, diegetic, first-hour PDA route in play;

    route proof remains separate from UI framework proof.

- `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`

  - Explicitly warns if no `NarrativeDiscovery` or `AudioLogPickup` exists.

  - This is the strongest local admission: "framework exists, player-facing POIs

    are not placed" is already encoded as runtime validation, but it is warning

    only, not a build blocker.

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`

  - Real first-hour milestone/quest/audio/scan/crafting bridge.

  - Uses queued first-hour events, quest activation/completion, scan/craft/audio

    listeners, and contextual route guidance.

  - Missing: authored route density proof. A director without placed resources,

    scans, logs, and UI route is not a playable first hour.

- `Assets/_Project/Scripts/RecipeData.cs`, `CraftingSystem.cs`, `Fabricator.cs`,

  `HectonFabricatorUI.cs`

  - Real fabrication path: scan-gated recipes, fast inventory mask checks, native

    unlock mask, diegetic recipe list and hologram UI.

  - Missing: pinned recipe buffer, storage-cache/material-query UX, and a single

    tested route from scan -> recipe unlock -> craft -> PDA feedback -> save/load.

- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`,

  `SuitUpgradeData.cs`, `SuitUpgradeResolver.cs`, `HazardMutationProfile.cs`

  - HECTON-8 has the raw material for a better answer to Biomods: suit upgrades,

    hazard mutations, upgrade bitmask, black-box telemetry, save integration.

  - Missing: unified active/passive adaptation slot ledger, Biolab-equivalent

    station contract, scan/hazard evidence requirements, and zero-GC UI affordance.

  - Current manager still uses managed string sets/lists for installed/broken/

    unlocked IDs around save and notifications; acceptable cold path only if kept

    out of gameplay ticks and backed by hash/mask runtime truth.

- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`

  - Strong subsystem: deterministic wreck WFC, loot records, pickable debris,

    lore fragment records, laser-cutter interaction, BRG path, 300-frame blackbox.

  - Missing: proof that at least one wreck is placed on the first real route, and

    proof that wreck state is exported as baked/static world sidecar data instead

    of remaining a purely runtime generator burden.

  - Also note H-Phi debt: it owns many local native containers. That may be

    reasonable for generator working memory, but final persisted truth should be

    a DataVault/content payload contract.

- `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs` and

  `Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs`

  - This area is no longer paper. `ActiveSplineData` and `DockingSplineSample`

    use fixed-size layouts without `Pack = 1`; `DockTelemetryEntry` still uses

    packed layout and requires Construction/Vehicle owner alignment review before

    runtime/platform claims.

  - Active splines and telemetry use `GlobalDataVault` handles.

  - Docking emits `DockingCompleteSignal` / `DockingFailedSignal`, records a

    300-frame ring, samples flow, supports math LOD, and bridges docked cargo

    crates into base logistics.

  - Missing: vehicle fabrication route proof, moonpool scene/prefab proof,

    vehicle spawn/save/reload proof, and ContentAuthority payload records for

    vehicle/dock recipes and modules.

- `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs`

  - Runtime networking manager is still a stub with TODOs.

  - The co-op Merkle state protocol doc is strong, but docs are not runtime.

  - Subnautica 2's co-op surface means HECTON-8 should make state contracts

    co-op-safe now, not promise co-op now.

## Tactical Borrowing Contracts

### 1. Voicelog Priority Is A P0 Narrative Contract

Subnautica 2 roadmap explicitly calls out Voicelogs Priority System. HECTON-8

already has a queue, but queue order is not the same as priority arbitration.

Required H8 contract:

- `AudioLogData` or an external static table needs: priority tier, interrupt class,

  cooldown group, route-critical flag, replay suppression class, subtitle policy.

- The queue must make deterministic decisions:

  - route-critical can preempt ambient;

  - ambient never blocks survival warnings;

  - duplicate log hashes dedupe;

  - queue overflow drops lowest priority and records telemetry;

  - playback state persists by hash/bit, never by display string.

- Build gate must fail if first-hour required logs have no authored pickup or

  narrative discovery route.

### 2. Biomods Should Become HECTON-8 Adaptations

Do not copy the "Biomod" fantasy. HECTON-8 should answer with pressure/noir

adaptations:

- Active slots: emergency gill purge, nerve dampener, sonar pulse, ballast burst.

- Passive slots: pressure acclimation, radiation lattice, low-O2 metabolism,

  cold blood perfusion, acoustic camouflage.

- Unlock evidence sources:

  - species scan;

  - hazard exposure threshold;

  - wreck blackbox specimen;

  - medical station / suit bench analysis.

- Runtime truth: `ulong activeMask`, `ulong passiveMask`, fixed slot counts,

  hash-only source requirements, fixed save DTO.

- Minimum quality: stat deltas and simple VFX.
- Maximum quality: visor tissue shimmer, salt crystallization, biolum veins, subtle
  suit mesh morphs, with no change to gameplay truth.

### 3. Wreck Gameplay Must Be A Route, Not A Generator Demo

The local wreck generator is technically promising. The missing contract is player

route proof.

P0 route:

1. First signal points to a small wreck.

2. Player uses repair/scanner/cutter capability chain.

3. Wreck gives one tool recipe, one lore/PDA event, one survival resource, and one

   visible world-state change.

4. Save/reload preserves opened/looted/scanned state.

5. Build gate fails if the route has no placed or generated authored entry.

### 4. Vehicle Docking And Fabrication Need One End-To-End Thread

Docking code is real. The product contract is not satisfied until fabrication and

placement meet it.

Required route:

- craft vehicle/dock-related recipe;

- place or construct moonpool/dock module;

- spawn/build compatible vehicle;

- dock, transfer cargo, charge/service vehicle;

- undock;

- save/reload;

- verify 300-frame docking telemetry exists on failure.

### 5. Pinned Recipes And Storage Cache Are Not Cosmetic

Subnautica 2 roadmap puts pinned recipes and storage cache into near-term QoL.

For HECTON-8 this is foundation, because crafting frustration kills the first

hour faster than missing overkill visuals.

Required H8 contract:

- Fixed `PinnedRecipeLedger` in runtime/save:

  - `uint recipeHash[MaxPinned]`;

  - `byte pinFlags[MaxPinned]`;

  - no strings in tick.

- Fabricator/PDA reads local inventory plus `BaseLogisticsNetwork` accessible

  stock and shows missing materials.

- UI colors can be high-level, but data source must be zero-GC and deterministic.

- Co-op future: pinned recipes are per-player intent; storage totals are host/world

  state.

### 6. Co-Op-Ready State Now, Co-Op Runtime Later

Subnautica 2 has co-op and is already hardening voice/trade/revive/HUD signals.

H8 runtime networking is not ready. The tactical move is state authority now:

- Inventory transfer is a transaction record, not a UI operation.

- Data/PDA/recipe unlocks must declare ownership: player, world, faction, or host.

- Base edit permissions must exist before multiplayer, even in local singleplayer

  loopback tests.

- Vehicle ownership and cargo access must be explicit.

- Local loopback Merkle/state replay should pass before any network transport is

  selected.

## P0 Work Orders

### P0-1 Narrative Route Density Gate

Owner domains: Narrative, Quest, World, UI.

Acceptance:

- At least one `AudioLogPickup` and three `NarrativeDiscovery` route points are

  authored or generated into the first playable route.

- At least six scannable targets have valid prefab/scene/generic route proof.

- `HectonLoreSystemsRoot` warnings become build-blocking for release profiles.

- First-hour playtest proves: pickup/scan -> PDA archive unlock -> save/reload.

### P0-2 Voicelog Priority Arbiter

Owner domains: Narrative, Audio, UI.

Acceptance:

- Playback queue supports priority, interrupt class, cooldown group, and route

  critical flag.

- Queue overflow is deterministic and telemetry-backed.

- Survival warnings cannot be blocked by ambient lore.

- Unit test covers two simultaneous route logs plus one ambient log.

### P0-3 Adaptation Slot Ledger

Owner domains: Survival, Tools, PDA, Save.

Acceptance:

- Fixed active/passive adaptation slot mask exists in runtime and save.

- At least six adaptations are data-authored with scan/hazard/wreck requirements.

- Suit upgrade manager exposes hash/mask truth to UI; managed string sets remain

  cold-authoring/save compatibility only.

- One adaptation unlock path is proved through scanner or hazard exposure.

### P0-4 Pinned Recipe And Storage Cache

Owner domains: Inventory, Fabricator, PDA, Logistics.

Acceptance:

- Pinned recipe ledger is fixed-size and hash-only.

- Fabricator/PDA can report missing ingredients from carried inventory plus

  reachable base storage.

- No hot-path `string.Format`, LINQ, or per-frame allocation.

- Save/reload preserves pinned recipes.

### P0-5 Docking/Fabrication Route Proof

Owner domains: Construction, Vehicles, Fabricator, Save, World.

Acceptance:

- One vehicle/dock fabrication route is playable from scan/recipe to dock/undock.

- Dock cargo bridge talks to logistics and survives save/reload.

- Failed docking writes `Dump_DOCKING_AUTOPILOT_SPLINE.bin`.

- Moonpool/dock assets are ContentAuthority-addressable or DataMonolith-indexed,

  not scene-only tribal knowledge.

### P0-6 Co-Op Local Loopback State Audit

Owner domains: Save, Network, Inventory, PDA, Construction.

Acceptance:

- No full multiplayer claim.

- Local loopback exercises shared world unlocks, per-player inventory intent,

  trade transaction stub, revive-state DTO stub, and base edit permission DTO.

- Merkle/hash state stays stable through save/load.

- `HectonNetworkManager` is either replaced by a real local transport harness or

  demoted to non-runtime placeholder.

## P1 Work Orders

- Wreck sidecar payload: export wreck object batch, opened/looted/scanned flags,

  and visibility/proxy data into static content/save payloads.

- Accessibility/platform parity: match Steam Deck-readable UI, controller glyphs,

  text scale, subtitle options, camera comfort, and custom volume controls as

  explicit platform checklist items.

- High-fidelity visual spend: once route contracts pass, add visor salt, volumetric
  silt wakes, hull dents, pressure sparks, and high-fidelity POM/raymarch/SSS as
  optional Overkill payloads.

- Modding overlay: external data mods need safe handlers for PDA entries, scan

  entries, recipes, loot tables, and adaptation definitions.

## Direct Verdict

HECTON-8 has stronger low-level engineering than the phrase "Subnautica-like"

implies: docking, wreck generation, fabricator internals, and first-hour director

are real code. The weak point is still product truth: authored routes, player

visible PDA/log density, adaptation-slot contract, pinned recipe QoL, and co-op

state ownership.

The next useful borrowing is not graphics or files. It is their public loop

pressure:

- every system must be reachable by the player;

- every route must survive save/load;

- every QoL feature must be a data contract, not a UI patch;

- co-op readiness must be designed into state ownership before networking exists.

## Sources

- Official Unknown Worlds roadmap page:

  https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap

- Official roadmap image inspected:

  https://unknownworlds.com/_next/image?q=75&url=https%3A%2F%2Fd17c72h1ypygg7.cloudfront.net%2FSN_2_2026_Roadmap_rev_10c96dff45.jpg&w=3840

- Official Early Access release post:

  https://unknownworlds.com/en/news/subnautica-2-early-access-released

- Steam store page:

  https://store.steampowered.com/app/1962700/Subnautica_2/

- SteamDB screenshots/metadata page:

  https://steamdb.info/app/1962700/screenshots/

- Steam gameplay trailer still inspected:

  https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/257333715/b1d8aeafbffae6530e2c9439fbbec1a16f86b314/movie_full.jpg

- Steam library hero image inspected:

  https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1962700/5775c2fdf39291633dd59b87ece66ab0431aa9ec/library_hero_2x.jpg

- Secondary Biomods guide, gameplay observation only:

  https://games.gg/subnautica-2/guides/subnautica-2-all-biomods-and-how-to-unlock-them/

- Secondary co-op guide, gameplay observation only:

  https://www.gamespot.com/articles/how-co-op-multiplayer-works-in-subnautica-2/1100-6539887/
