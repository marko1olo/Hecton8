# HECTON-8 Content Pack POI / Unlock Matrix

Status: working source matrix.
Purpose: connect world packets to POI placement, unlock events, and future static data.

This is authoring data. Runtime should consume baked IDs and flags.

## Tag Families

Biome Tags:

- biome.shallow_reef
- biome.storm_shelf
- biome.kelp_shallows
- biome.drowned_infrastructure
- biome.industrial_descent
- biome.thermal_field
- biome.deep_abyss
- biome.atlas_bottom

POI Tags:

- poi.crash_shelf
- poi.service_buoy
- poi.signal_mast
- poi.worker_locker
- poi.shift_board
- poi.pressure_casket
- poi.cable_forest
- poi.repair_scar
- poi.drone_nest
- poi.evacu_manifest

Unlock Tags:

- unlock.first_scan
- unlock.first_shelter
- unlock.black_keel_handshake
- unlock.first_uplink_window
- unlock.first_deep_reach_marker
- unlock.first_barnard_mark
- unlock.first_blue_debt_sample
- unlock.first_atlas_category_error
- unlock.first_repair_drone_nest
- unlock.first_evac_manifest

## Matrix

| Packet | Required Tags | Optional Tags | Unlock Output | Player Result |
|---|---|---|---|---|
| CP01 Arrival | poi.crash_shelf, biome.shallow_reef or biome.storm_shelf | poi.service_buoy, biome.kelp_shallows | unlock.first_shelter, unlock.first_scan | Player survives first loop and understands the capsule cannot leave. |
| CP02 Black Keel | poi.signal_mast, unlock.first_shelter | moon visibility tag, weather window tag | unlock.black_keel_handshake, unlock.first_uplink_window | Player learns rescue is real but conditional. |
| CP03 Colony / Barnard | poi.worker_locker or poi.shift_board | poi.evacu_manifest | unlock.first_barnard_mark | Professional job starts becoming personal. |
| CP04 Blue Debt | poi.pressure_casket, biome.industrial_descent | poi.cable_forest, biome.thermal_field | unlock.first_blue_debt_sample | Player gets value, risk, and carrier pressure. |
| CP05 Atlas Scars | poi.repair_scar | poi.drone_nest, biome.deep_abyss | unlock.first_atlas_category_error | Player sees Atlas repair logic as physical horror. |

## Seed Variation Rules

Allowed:

- POI location.
- POI condition.
- first tool/sample/note variant.
- local danger.
- signal window timing.
- visible sky state.
- optional text fragment.

Forbidden:

- changing who caused HECTON-8.
- changing Atlas directive truth.
- changing Black Keel structural role.
- changing blue debt into magic or simple ore.
- changing Barnard hook into random unrelated flavor.
- changing localization or quality level into gameplay truth.

## World Density Targets

Low density seed:
One major packet every long exploration arc. Fewer optional notes. More silence.

Middle density seed:
Major packet plus one supporting fragment per biome transition.

High density seed:
Major packet, supporting fragments, alternate source voices, and visual dressing.

Ultra presentation:
Same truth and unlocks, more environmental layers, VO variants, signal artifacts, sky detail, and object-level dressing.
