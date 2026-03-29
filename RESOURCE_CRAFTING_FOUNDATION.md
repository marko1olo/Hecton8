# RESOURCE CRAFTING FOUNDATION

## Goal

Build a full, data-driven resource and crafting system for Hecton-8.

It should support:
- scavenging
- mining
- biology harvesting
- tool crafting
- suit upgrades
- base building
- power production
- repair and maintenance

The player should recognize clear progression:
- early survival materials
- mid-game engineering materials
- late-game deep-zone materials

## Design Rules

- Small number of core raw materials that combine into many useful parts.
- Intermediate components matter more than huge recipe bloat.
- Every resource should have at least one clear purpose.
- Tools must map to resource sources:
  - `Scanner` finds
  - `Knife` harvests soft bio material
  - `Laser Cutter` opens sealed caches / hard shells
  - `Salvage Sampler` recovers salvage-grade matter
  - `Repair Tool` consumes maintenance parts indirectly
  - `Builder` consumes structural parts
- Keep progression readable:
  - raw -> refined -> component -> device/module

## Resource Families

### 1. Common Structural Materials

- `Titanium Scrap`
  - main early structural metal
  - foundations, lockers, simple housings
- `Copper Ore`
  - wiring, beacons, basic tools
- `Iron Composite`
  - frames, braces, heavy mounts
- `Silica Shards`
  - glass, optics, sensor windows

### 2. Conductive / Electronic Materials

- `Silver Ore`
  - signal paths, sensor wiring, contacts
- `Gold Ore`
  - high-grade electronics, chips, analyzer parts
- `Cobalt Alloy`
  - durable electronics housings, weapon housings
- `Rare Earth Dust`
  - late precision electronics, advanced guidance

### 3. Energy / Chemical Materials

- `Sulfur Clumps`
  - energetic compounds, cutting charge internals
- `Electrolyte Salts`
  - cells, power regulation, coolant chemistry
- `Hydrocarbon Resin`
  - sealants, polymers, repair compounds
- `Thermal Gel`
  - heat handling, flashlight cooling, cutter stabilization

### 4. Biological Materials

- `Fiber Kelp`
  - fibers, mesh, suit lining
- `Membrane Tissue`
  - filters, soft seals, pressure bladders
- `Enzyme Coral`
  - catalysts, med compounds, bioprocessing
- `Biolum Paste`
  - illumination chemistry, markers, late organic tech

### 5. Deep / High-Pressure Materials

- `Nickel Ore`
  - reinforced hull work, pressure systems
- `Lithium Crystal`
  - energy storage, advanced cells, structural reinforcement
- `Tungsten Chunk`
  - heavy tools, drill/cutter/harpoon bodies
- `Abyssal Crystal`
  - late-game precision modules, experimental tech

## Intermediate Components

These are the real backbone of the crafting tree.

### Basic Components

- `Copper Wire`
- `Glass Panel`
- `Fiber Mesh`
- `Sealant Pack`
- `Battery Cell`
- `Lubricant Resin`

### Engineering Components

- `Circuit Board`
- `Sensor Package`
- `Pressure Seal`
- `Reinforced Plate`
- `Hydraulic Actuator`
- `Cooling Cartridge`
- `Beacon Core`

### Advanced Components

- `High-Capacity Cell`
- `Guidance Module`
- `Relay Matrix`
- `Abyss Pressure Shell`
- `Precision Lens`
- `Stabilizer Coil`

## Resource Uses by Tool / System

### Tools

- `Flashlight`
  - Copper Wire
  - Glass Panel
  - Battery Cell
  - Cooling Cartridge

- `Scanner`
  - Copper Wire
  - Sensor Package
  - Precision Lens

- `Repair Tool`
  - Sealant Pack
  - Copper Wire
  - Hydraulic Actuator

- `Laser Cutter`
  - Tungsten Chunk
  - Battery Cell
  - Cooling Cartridge
  - Sulfur Clumps

- `Beacon Deployer`
  - Copper Wire
  - Beacon Core
  - Biolum Paste

- `Environmental Analyzer`
  - Sensor Package
  - Circuit Board
  - Precision Lens

- `Salvage Sampler`
  - Hydraulic Actuator
  - Sensor Package
  - Sealant Pack

- `Propulsion Tool`
  - High-Capacity Cell
  - Stabilizer Coil
  - Hydraulic Actuator

- `Harpoon Launcher`
  - Tungsten Chunk
  - Guidance Module
  - Fiber Mesh

- `Stun Pistol`
  - High-Capacity Cell
  - Circuit Board
  - Stabilizer Coil

- `Knife`
  - Titanium Scrap
  - Fiber Mesh

### Suit / Survival

- oxygen upgrades:
  - Fiber Mesh
  - Membrane Tissue
  - Lithium Crystal

- pressure upgrades:
  - Reinforced Plate
  - Abyss Pressure Shell
  - Nickel Ore

- power upgrades:
  - Battery Cell
  - High-Capacity Cell
  - Electrolyte Salts

### Construction

- Foundations / corridors:
  - Titanium Scrap
  - Iron Composite

- Utility modules:
  - Copper Wire
  - Circuit Board
  - Hydraulic Actuator

- Power modules:
  - Lithium Crystal
  - Electrolyte Salts
  - Relay Matrix

## Progression Tiers

### Tier 0: Survival Start

- Titanium Scrap
- Copper Ore
- Fiber Kelp
- Silica Shards

Unlocks:
- flashlight
- beacon
- basic scanner
- simple base pieces

### Tier 1: Functional Engineering

- Silver Ore
- Gold Ore
- Membrane Tissue
- Hydrocarbon Resin
- Sulfur Clumps

Unlocks:
- repair tool
- analyzer
- salvage sampler
- better fabrication components

### Tier 2: Heavy Operations

- Lithium Crystal
- Nickel Ore
- Tungsten Chunk
- Thermal Gel

Unlocks:
- propulsion
- harpoon
- stun pistol
- reinforced construction

### Tier 3: Deep Endgame

- Cobalt Alloy
- Rare Earth Dust
- Abyssal Crystal
- Biolum Paste

Unlocks:
- deep pressure gear
- advanced relays
- endgame power systems
- experimental tool upgrades

## Required Implementation Steps

### Data

- add all raw resources as `ItemData`
- add intermediate components as `ItemData`
- tag them by category:
  - raw metal
  - crystal
  - organic
  - chemical
  - component

### World Sources

- salvage pickups
- resource nodes
- cuttable caches
- biological harvest nodes
- rare deep deposits

### Crafting

- fabricator recipes for:
  - raw -> component
  - component -> tool
  - component -> upgrade
- scan-gated unlocks where needed

### UI

- fabricator categories:
  - Materials
  - Components
  - Tools
  - Suit
  - Construction
- PDA should show:
  - discovered resources
  - missing ingredients
  - unlocked blueprints

## First Build Slice

The first practical slice should be:

1. Add 8-12 core raw resources.
2. Add 6-8 intermediate components.
3. Rebuild starter fabricator recipes around those components.
4. Convert current simple copper-only tool recipes into real multi-part recipes.
5. Add world authoring for at least:
   - scrap metal
   - copper
   - fiber
   - silica
   - sulfur
   - silver

That will turn the current placeholder craft loop into a real resource economy.
