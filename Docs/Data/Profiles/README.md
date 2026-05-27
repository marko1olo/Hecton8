# Documentation Data Profiles

Date: 2026-05-26
Status: STATIC AUTHORING DATA
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / CSV_DATA

This folder stores editor/authoring CSV profiles that are not root documentation.

Current profiles:

- `ambient_lighting_profiles.csv`
- `flora_biome_sway_profiles.csv`
- `water_extinction_profiles.csv`
- `water_optics_profiles.csv`

These files are authoring inputs or tuning bridges. They are not runtime proof and are not Data Monolith payload authority.

## Schema Boundary

Each CSV starts with parser-safe `#` metadata:

| File | Schema | Parser / Owner |
|---|---:|---|
| `ambient_lighting_profiles.csv` | `1 / 0x760905B1` | `Hecton8.Lighting.InteriorGIProbeVolumeRuntime` |
| `flora_biome_sway_profiles.csv` | `1 / 0x65A1C2BE` | `Hecton8.World.FloraAmbientSway.FloraAmbientSwayRuntime` |
| `water_extinction_profiles.csv` | `1 / 0xA8609F4B` | `Hecton8.VFX.VolumetricFogExtinctionCsvParser` |
| `water_optics_profiles.csv` | `1 / 0x92C5A907` | `Hecton8.Rendering.WaterOptics.WaterOpticsRuntime` |

The current parsers skip comment lines and do not enforce schema hashes. Treat the hash as authoring review metadata until an importer/validator gate is added.
