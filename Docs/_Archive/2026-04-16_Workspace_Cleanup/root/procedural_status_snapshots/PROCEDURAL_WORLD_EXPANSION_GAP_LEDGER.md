**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Procedural World Expansion Gap Ledger

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`

This is the root-level execution ledger for what is still missing before HECTON-8 has a real procedural asset pipeline for more than flora.

It is not a replacement for `/Docs` as the clean execution entry.
It exists as the blunt backlog and gap list for expansion work across every major vertical.

## Current Reality

The project already has:

- one shared runtime scatter owner via `WorldProceduralScatterDirector`
- one shared family/profile/rule/variant model via `WorldPrefabFamilyProfile`
- live family/rule authoring via `WorldProceduralProxyAuthoring`
- shared final linking via `WorldProceduralFinalVariantAuthoring`
- placeholder fallback finals via `WorldProceduralPlaceholderAuthoring`
- the strongest vertical stack in `flora`
- a now-hardened `geology` profile/final/validator/report path
- a now-hardened `structural` final/validator/report path
- a now-hardened `world_support` final/validator/report path
- a shared world family validator/report that separates real finals from placeholder-only families
- a normative bootstrap path that rebuilds:
  - starter construction finals
  - world-support finals
  - explicit geology profiles
  - geology finals
  - shared first-wave final linking

The project does **not** yet have a complete universal “procedurally create everything” system.

## Vertical Readiness

### ORGANIC

Current state:

- strongest vertical
- separate intake/material/texture/validator/report stack exists
- real imported texture coverage exists for `6/9` flora families
- shared world family validator now reads `11/11` organic families as real-final
- organic misc validator/report path now exists for:
  - `family.egg.cluster`
  - `family.plant.giant`
- organic misc real finals now exist for:
  - `family.egg.cluster`
  - `family.plant.giant`
- current organic misc validator truth:
  - `PASS families=2, realFinalFamilies=2, placeholderOnlyFamilies=0, warnings=0`
- remaining coral families still lack imported authored texture sets

Still missing:

- imported texture coverage for `family.coral.massive`
- imported texture coverage for `family.coral.plate`
- imported texture coverage for `family.coral.brittle`
- runtime visual proof for authored flora under real scene lighting
- profiler proof for shader cost / culling / GPUI path
- authored texture/shader contract for `family.egg.cluster`
- authored texture/shader contract for `family.plant.giant`

### GEOLOGICAL

Current state:

- shared world routing is live
- runtime geology integration/seam stack is live
- geological families now read `5/5` real-final in the live validator/report pass
- explicit geology profiles now exist for:
  - `family.rock.arch.large`
  - `family.cave.entrance`
  - `family.landmark.spire`
- geology validator/report now exists
- large-form geological real finals now exist for:
  - `family.rock.arch.large`
  - `family.cave.entrance`
  - `family.landmark.spire`
- current validator truth:
  - `PASS families=5, realFinalFamilies=5, placeholderOnlyFamilies=0, explicitProfiles=3, emergencyFallbacks=0, warnings=0`

Still missing:

- geology-specific authored material/shader contract
- validator rule for geological texture source / material stack
- scene/runtime seam proof under actual play conditions
- profiler proof for seam execution and large-form visibility cost

### STRUCTURAL

Current state:

- structural families already exist in shared family/rule stack
- live domains already exist:
  - `RuinModule`
  - `Debris`
  - `PowerRoute`
  - `ServiceScar`
- structural families now read `7/7` real-final in the live validator/report pass
- real finals now cover:
  - `family.ruin.module.single`
  - `family.ruin.cluster.medium`
  - `family.ruin.megastructure`
  - `family.route.power`
  - `family.service.scar`
  - `family.debris.scatter`
  - `family.debris.field`
- structural validator/report path now exists
- current validator truth:
  - `PASS families=7, realFinalFamilies=7, placeholderOnlyFamilies=0, warnings=0`

Still missing:

- structural texture-source contract
- explicit distinction between reusable structural shells and debris clutter
- runtime visual proof and profiler proof

### INTERIOR_DECOR

Current state:

- no dedicated vertical yet
- must reuse structural/shared family stack
- no formal family set yet

Still missing:

- interior family taxonomy
- interior family profiles
- interior placement rules
- socket/room-driven placement contract
- interior validator/report stack
- interior material/shader contract
- interior clutter density / duplication rules
- interior-specific LOD and culling contract
- final prefab library
- runtime verification

### COLONY_PARTS

Current state:

- no dedicated vertical yet
- should reuse `RuinModule`, `Landmark`, `PowerRoute`, `ServiceScar` before new domains are added

Still missing:

- colony family taxonomy
- colony family profiles
- colony part intake/final-linking path
- landmark-scale colony validation rules
- colony-specific material/shader contract
- colony-specific large-form LOD gate
- colony-specific streaming rules
- final prefab library
- runtime verification

### WORLD_SUPPORT

Current state:

- shared family ownership exists for:
  - `CreatureSpawn`
  - `ResourcePocket`
  - `HazardPocket`
  - `SafePocket`
- world-support validator/report path now exists
- world-support families now read `9/9` real-final in the live validator/report pass
- current validator truth:
  - `PASS families=9, realFinalFamilies=9, placeholderOnlyFamilies=0, largeThreatZones=4, warnings=0`
- real finals now cover:
  - `family.creature.spawn.passive`
  - `family.creature.spawn.predator`
  - `family.creature.zone.large_threat`
  - `family.creature.zone.abyss_apex`
  - `family.creature.zone.reef_apex`
  - `family.creature.zone.ruin_apex`
  - `family.pocket.resource`
  - `family.pocket.hazard`
  - `family.pocket.safe`

Still missing:

- runtime visual/perf proof

## Cross-System Gaps

These are missing across multiple verticals:

- one vertical-specific validator/report pair for the unfinished categories:
  - `INTERIOR_DECOR`
  - `COLONY_PARTS`
- one category material contract per unfinished major category
- one category texture/source contract where textures matter
- one category LOD/budget gate per unfinished major category
- one honest readiness report that separates:
  - proxy only
  - placeholder final
  - real final
  - runtime verified
- scene-level visual verification
- profiler evidence for every major runtime-heavy vertical
- one organic-misc validator/report path if `egg/plant` need stricter material/shader enforcement beyond shared family truth

## Execution Order

Work in this order:

1. complete remaining `ORGANIC` authored texture coverage
2. `INTERIOR_DECOR`
3. `COLONY_PARTS`
4. scene/runtime/profiler verification for `WORLD_SUPPORT`
5. scene/runtime/profiler verification for `GEOLOGICAL` and `STRUCTURAL`

## Immediate Next Steps

The next concrete code steps are:

1. complete authored texture/material coverage for `family.coral.massive`
2. complete authored texture/material coverage for `family.coral.plate`
3. complete authored texture/material coverage for `family.coral.brittle`
4. decide whether `family.egg.cluster` and `family.plant.giant` stay on flat-color organic misc materials or get a dedicated authored texture/shader pass
5. add `INTERIOR_DECOR` validator/report plus first real finals
6. add `COLONY_PARTS` validator/report plus first real finals
7. gather scene/runtime/profiler evidence for `WORLD_SUPPORT`, `GEOLOGICAL`, and `STRUCTURAL`

## Final Rule

No new parallel runtime stacks.

Every new vertical must plug into:

- `WorldProceduralScatterDirector`
- `WorldPrefabFamilyProfile`
- `WorldProceduralProxyAuthoring`
- `WorldProceduralFinalVariantAuthoring`

Anything else is architecture drift.
