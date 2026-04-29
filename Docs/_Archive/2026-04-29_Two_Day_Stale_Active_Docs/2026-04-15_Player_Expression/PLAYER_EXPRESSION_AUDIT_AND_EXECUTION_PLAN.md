# HECTON-8 Player Expression Audit And Execution Plan

Status: `PENDING VERIFICATION`  
Date: `2026-04-15`

## Hard Verdict

There is no real player-expression system in shipping runtime.

The project has fragments:
- functional tool loadouts
- functional fabrication
- functional construction registry
- functional suit stat progression
- functional HUD profile assets

It does not have the missing owner layer:
- no runtime owner for player identity
- no save owner for identity selection
- no PDA owner for identity switching
- no unlock/cosmetic catalog for expression
- no base decor vertical
- no meaningful suit visual breadth

That means the player can optimize, but cannot meaningfully express preference, role, or taste.

## Verified Current State

### Tools

Verified assets:
- `12` tool prefabs in `PlayerToolManager.knownToolPrefabs`
- `12` tool item assets in `Assets/_Project/Data/Items/Tools`
- `4` authored loadout presets in `Assets/_Project/Data/Tools/Presets`
- `31` crafting recipes in `Assets/_Project/Data/Crafting/Recipes`

What is ready:
- live quick-slot runtime
- loadout switching
- inventory linkage
- durability integration
- PDA loadout UX

What is missing:
- role identity bound to a loadout
- mastery progression per tool family
- cosmetic/tool presentation differentiation
- authored “why this kit says something about the player” layer

### Bases

Verified assets:
- `5` buildables in `Assets/_Project/Data/Construction`
- live `ConstructionManager`
- live construction PDA tab
- live fabricator runtime

What is ready:
- foundation for placement, registration, save/load, and craft/build loop

What is missing:
- this is not enough content for base identity
- no interior decor vertical
- no habitat personalization loop
- no display/utility vanity modules
- no “my base looks like mine” outcome

### Suits / HUD

Verified assets:
- `2` suit assets in `Assets/_Project/Data/Survival`
- `5` suit upgrades in `Assets/_Project/Data/Lore/SuitUpgrades`
- `3` HUD profiles in `Assets/_Project/Data/HUD`

What is ready:
- mechanical suit baseline
- mechanical upgrade manager
- HUD profile architecture

What is missing:
- almost all cosmetic breadth
- no save-backed identity choice
- no player-facing suit presentation selection
- no suit label / role identity system
- too few upgrades and too few authored suit variants

## Critical Gaps

1. Player expression is promised by system contracts and absent in runtime ownership.
2. Base identity is practically nonexistent because `5` buildables is still prototype scale.
3. Suit progression exists as stats, not as identity.
4. Tool presets exist as utility, not as fantasy.
5. Production scene still contains workshop contamination: `Tool_Staging`, `Fabrication_Trial`, `__TEMP_DENSE_KELP_PREVIEW`.

## Execution Tracks

### Track 01 — Player Identity Runtime

Goal:
- make player identity a first-class saved system

Implementation:
- add `PlayerExpressionProfile` data assets
- add `PlayerExpressionManager` runtime/save owner
- route active profile into HUD runtime
- expose identity switching in PDA loadout tab

### Track 02 — Suit Presentation Pack

Goal:
- convert suit progression from raw stats into visible role identity

Implementation:
- expand HUD profile pack
- bind expression profiles to suit recommendation + loadout recommendation
- expand suit upgrade catalog beyond current `5`

### Track 03 — Tool Fantasy Pack

Goal:
- make tool choice signal intent, not only function

Implementation:
- add authored role kits per expression profile
- expand tool-family progression and unlock cadence
- add more authored tool-specific content hooks

### Track 04 — Base Identity Vertical

Goal:
- make bases readable as personal spaces, not only utility pads

Implementation:
- decor catalog
- display/storage/utility vanity set
- fabrication reward loop for decor unlocks
- module variants by identity family

### Track 05 — Scene Truth Cleanup

Goal:
- stop shipping workshop state

Implementation:
- remove or suppress trial/staging contamination from production play path
- verify bootstrap path only

## Started In This Pass

1. Create the missing owner layer for suit identity and expression.
2. Persist identity in save data.
3. Feed identity into live HUD systems.
4. Expose identity switching inside existing PDA loadout flow.
5. Seed first authored expression profile pack.

## Non-Negotiable Follow-Up

After this first pass, the next largest missing chunks are still:
- interior decor
- large base module catalog expansion
- larger suit catalog
- larger suit upgrade catalog
- stronger tool-family progression

Without those, expression remains partial.
