# Hecton8 108 Biome Matrix Plan

## What Is Done

- Added a future-ready 108-biome data layer.
- It does **not** replace the current small runtime MapMagic biome palette yet.
- It exists in parallel so the world can be designed for the full lore matrix now.

## New Runtime/Data Pieces

- `Assets/_Project/Scripts/HectonBiomeMatrixProfile.cs`
- `Assets/_Project/Scripts/HectonBiomeMatrixCatalog.cs`
- `Assets/_Project/Scripts/BiomeMatrixDirector.cs`
- `Assets/_Project/Scripts/HectonBiomeFamilyProfile.cs`
- `Assets/_Project/Scripts/Editor/BiomeMatrixBootstrapAuthoring.cs`

## New Data

- Catalog:
  - `Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset`
- Profiles:
  - `Assets/_Project/Data/Biomes/MatrixProfiles`
- Families:
  - `Assets/_Project/Data/Biomes/FamilyProfiles`
- Atmosphere profiles:
  - `Assets/_Project/Data/Biomes/AtmosphereProfiles`
- Fauna family profiles:
  - `Assets/_Project/Data/Biomes/FaunaFamilies`
- Play profiles:
  - `Assets/_Project/Data/Biomes/PlayProfiles`

## Important Truth

The source lore document is strong, but not every one of the 108 slots is equally detailed.

Because of that:
- all 108 biome slots now exist as real assets
- explicitly described biomes from the lore matrix are authored with real names and descriptions
- missing detailed slots are created as honest placeholders

Current validation result:
- `108 total biome slots`
- `44 placeholders`
- `13 biome families`

This is correct and intentional.
It keeps the architecture complete without pretending missing lore details already exist.

## How It Works

The 108-biome matrix is:
- `27 depth tiers`
- `4 cardinal regions`
- total `108 biome slots`

`BiomeMatrixDirector` resolves the current matrix biome from:
- player depth
- player cardinal quadrant relative to world origin

This gives the project a future-proof biome identity layer for:
- route planning
- atmosphere
- fauna families
- loot families
- landmark identity
- progression pacing

Each biome slot now also resolves into a biome family that describes:
- geology character
- gameplay character
- atmosphere mood
- navigation style
- hazard style
- landmark language
- resource emphasis
- real primary / secondary / tertiary resource references
- signature crafted component reference
- atmosphere profile reference
- fauna family reference
- recommended tool loadout reference
- play profile reference
- future near / mid / far world links

Each biome family now also has a play profile that answers simple gameplay questions:
- why the player goes there
- how readable the routes are
- how often the biome gives safe pockets
- how dense common resources are
- how strong the pull of rare rewards is
- how much combat / creature pressure it puts on the player
- how much environmental pressure it puts on the player

This keeps the matrix aligned with a Subnautica-like exploration rhythm:
- readable landmarks
- safe pockets between risk spikes
- clear reasons to enter a biome
- different route pressure by depth and region

Each of the 108 matrix slots now also gets direct player-facing framing:
- visit purpose
- common reward hook
- rare reward hook
- landmark identity
- safe pocket identity
- risk summary
- route / landmark / reward / survival pressure scores

This means the matrix is no longer only lore and geology.
It now starts answering the practical gameplay question:
"Why should the player go there, and what kind of expedition does that place ask for?"

## Why This Is The Right Approach

We do **not** force the current MapMagic graph to jump from a small runtime palette straight to 108 layers now.

That would be expensive, messy, and risky.

Instead:
- current runtime visual biomes stay stable
- future 108-biome world logic is built now
- later we connect:
  - MapMagic masks
  - zone plans
  - world families
  - atmosphere
  - fauna
  - resource distribution

without rebuilding the whole architecture

## Next Steps

1. Link 108-biome matrix to zone plans.
   - define which biome slots dominate which world zones

2. Use the biome-family layer as the production bridge.
   - map families to resources
   - map families to atmosphere
   - map families to landmark language
   - map families to fauna families

3. Connect matrix biomes to future:
   - world families
   - resource families
   - fauna families
   - atmosphere overrides
   - encounter rhythm
   - landmark language
   - play profiles

4. Keep current runtime 6-8 biome palette stable until the large graph migration is actually planned.
