# RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS

Status: source candidate for production article admission; all runtime, publication, generated-page, native-localization and Unity placement gates remain false.

Purpose: keep Aegir/HECTON-8 public astronomy useful without freezing final simulation constants in prose.

## Packets

- `P421_RAN_AEGIR_PUBLIC_DISTANCE_BAND`: public ten-light-year-class no-FTL distance band.
- `P422_AEGIR_LOCAL_WINDOW_BAND_TABLE`: ascent and transfer windows as hours-to-days orbital pressure.
- `P423_HECTON8_MOON_LADDER_PUBLIC_BAND`: HECTON-8 inside a multi-moon Aegir claim system.
- `P424_BLACK_KEEL_TRANSFER_ORBIT_BAND`: Black Keel's high custody lane and payload/person recovery gap.
- `P425_PUBLIC_EPHEMERIS_TABLE_HANDOFF_RULE`: prose explains bands; exact orbital constants remain table-owned.

## Boundary

- Runtime reads baked static data only after importer admission.
- No runtime JSON or markdown parser.
- No runtime translation generation.
- `en_US` is source authority; all non-EN rows are draft adaptations pending native review.
- `canonical_importer_ready`, `runtime_ready`, `data_monolith_ready`, `h8bin_ready`, `unity_placement_ready`, `generated_page_ready`, `native_localization_ready` and `publication_ready` remain false until fresh proof exists for each gate.
