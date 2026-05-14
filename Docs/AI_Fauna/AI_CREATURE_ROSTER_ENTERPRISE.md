# AI Creature Roster Enterprise

Date: 2026-05-07
Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- May 13 DOC_AUDIT R15 fauna override: roster IDs currently align with a real static data set (`22` recursive creature archetype assets, `22` fauna data templates, `108` fauna biome datasets, `13` fauna family profiles, `6` generated proxy prefabs), but this document's encoding-damaged prose is not production writing, runtime truth, or scene-wiring proof.
- R15 runtime boundary: `FaunaDirector` contains a real registry-backed `IFaunaSim` owner when active, but current static scene/prefab/asset GUID search did not prove it is serialized into production content. Bootstrap can fall back to `DemiurgeFaunaSimulationService.Shared`, which reports ready but has `ResidentSlotCapacity = 0`; that fallback proves service-slot safety, not visible creatures.
- R15 smoke boundary: `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` is a failed/invalid artifact in the current filesystem, not a PASS.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## 2026-05-04 Current-State Boundary

- This roster is retained as reference, but much of the prose is encoding-damaged.
- Treat stable `ID`, fauna family, and biome family fields as pointers only.
- Do not use this prose as production writing, runtime truth, or final design copy until it is re-authored from source.
- Current runtime fauna truth must be checked in source, registries, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## Ð§Ñ‚Ð¾ ÑÑ‚Ð¾

- Ð­Ñ‚Ð¾ Ð½Ð°Ð±Ð¾Ñ€ Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ñ… Ð¿Ñ€Ð¾Ñ„Ð¸Ð»ÐµÐ¹ Ð²Ð¸Ð´Ð¾Ð².
- Ð˜Ñ… Ð¼Ð¾Ð¶Ð½Ð¾ Ð¿Ð¾Ð´Ð²ÐµÑˆÐ¸Ð²Ð°Ñ‚ÑŒ Ðº Ð¿Ñ€ÐµÑ„Ð°Ð±Ð°Ð¼ Ð¸ Ð¿Ð¾Ñ‚Ð¾Ð¼ Ñ€Ð°ÑÐºÐ¸Ð´Ñ‹Ð²Ð°Ñ‚ÑŒ Ð¿Ð¾ Ð±Ð¸Ð¾Ð¼Ð°Ð¼.
- ÐžÑÐ½Ð¾Ð²Ð½Ð¾Ð¹ ÑƒÐ¿Ð¾Ñ€ Ð·Ð´ÐµÑÑŒ: Ð¼Ð½Ð¾Ð³Ð¾ Ñ€Ð°Ð·Ð½Ñ‹Ñ… Ñ…Ð¸Ñ‰Ð½Ð¸ÐºÐ¾Ð² Ð¸ Ð»ÐµÐ²Ð¸Ð°Ñ„Ð°Ð½Ð¾Ð².

## ÐœÐ¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ

### Shore Skimmer

- `ID`: `creature.ambient.shore_skimmer`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐœÐµÐ»ÐºÐ°Ñ ÑÑ‚Ð°Ð¹Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÐ¿Ð¾ÐºÐ¾Ð¹Ð½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”Ð°Ñ‘Ñ‚ Ð¶Ð¸Ð²Ð¾Ð¹ Ñ„Ð¾Ð½ Ñƒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð¸ Ð² ÑÐ¿Ð¾ÐºÐ¾Ð¹Ð½Ñ‹Ñ… Ð°Ñ€ÐºÐ°Ñ….
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.littoral_passive, fauna.family.reef_ambush
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.littoral_karst, biome.family.fossil_reef

### Kelp Raylet

- `ID`: `creature.ambient.kelp_raylet`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐœÐ¸Ñ€Ð½Ð°Ñ ÑˆÐ¸Ñ€Ð¾ÐºÐ°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÑ€ÐºÐ¸Ñ… Ð·Ð°Ñ€Ð¾ÑÐ»ÐµÐ¹ Ð¸ Ñ€Ð¸Ñ„Ð¾Ð².
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”Ð°Ñ‘Ñ‚ Ð¼ÑÐ³ÐºÑƒÑŽ ÐºÑ€ÑƒÐ¿Ð½ÑƒÑŽ Ð¶Ð¸Ð·Ð½ÑŒ Ñ‚Ð°Ð¼, Ð³Ð´Ðµ Ð¼Ð¸Ñ€ Ð´Ð¾Ð»Ð¶ÐµÐ½ ÐºÐ°Ð·Ð°Ñ‚ÑŒÑÑ Ð±Ð¾Ð³Ð°Ñ‚Ñ‹Ð¼, Ð° Ð½Ðµ Ð±Ð¾ÐµÐ²Ñ‹Ð¼.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.littoral_passive, fauna.family.crystal_skittish
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.fossil_reef, biome.family.crystal_growth

### Silt Drifter

- `ID`: `creature.ambient.silt_drifter`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð”Ð¾Ð½Ð½Ñ‹Ð¹ Ð¼Ð¸Ñ€Ð½Ñ‹Ð¹ ÑÐ±Ð¾Ñ€Ñ‰Ð¸Ðº Ð¾ÑÐ°Ð´Ð¾Ñ‡Ð½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”ÐµÐ»Ð°ÐµÑ‚ Ñ€ÐµÑÑƒÑ€ÑÐ½ÑƒÑŽ Ð²Ð¾Ð´Ñƒ Ð¶Ð¸Ð²Ð¾Ð¹, Ð½Ð¾ Ð½Ðµ Ð°Ð³Ñ€ÐµÑÑÐ¸Ð²Ð½Ð¾Ð¹.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.sediment_scavengers, fauna.family.abyssal_sparse
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.sediment_drift, biome.family.abyssal_silt, biome.family.granite_escarpment

### Wall Glider

- `ID`: `creature.ambient.wall_glider`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐœÐ¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÑ‚ÐµÐ½, ÑƒÑÑ‚ÑƒÐ¿Ð¾Ð² Ð¸ Ð³Ñ€ÐµÐ±Ð½ÐµÐ¹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”ÐµÐ»Ð°ÐµÑ‚ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ñ‹ Ð²Ð´Ð¾Ð»ÑŒ ÑÑ‚ÐµÐ½ Ð¶Ð¸Ð²Ñ‹Ð¼Ð¸ Ð¸ Ñ‡Ð¸Ñ‚Ð°ÐµÐ¼Ñ‹Ð¼Ð¸.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Siphoner

- `ID`: `creature.ambient.brine_siphoner`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¡Ñ‚Ñ€Ð°Ð½Ð½Ð°Ñ Ð¼Ð¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ Ñ…Ð¸Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð² Ð¸ Ð²ÐµÐ½Ñ‚Ð¾Ð².
- `Ð¡ÑƒÑ‚ÑŒ`: ÐÑƒÐ¶ÐµÐ½, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ñ‚Ð¾ÐºÑÐ¸Ñ‡Ð½Ð°Ñ Ð¸ ÑÐµÑ€Ð²Ð¸ÑÐ½Ð°Ñ Ð²Ð¾Ð´Ð° Ð½Ðµ Ð±Ñ‹Ð»Ð° Ð¼Ñ‘Ñ€Ñ‚Ð²Ð¾Ð¹.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine, biome.family.volcanic_glass

### Lantern Sifter

- `ID`: `creature.ambient.lantern_sifter`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð ÐµÐ´ÐºÐ°Ñ Ð¼Ð¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¾Ð¹ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”Ð°Ñ‘Ñ‚ Ð¾Ñ‰ÑƒÑ‰ÐµÐ½Ð¸Ðµ Ñ€ÐµÐ´ÐºÐ¾Ð¹ Ð¶Ð¸Ð·Ð½Ð¸ Ð´Ð°Ð¶Ðµ Ð² Ð¿Ð¾Ð·Ð´Ð½ÐµÐ¹ Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ðµ.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.abyssal_sparse, fauna.family.hadal_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.abyssal_silt, biome.family.metallic_hadal, biome.family.rift_void

## Ð¢ÐµÑ€Ñ€Ð¸Ñ‚Ð¾Ñ€Ð¸Ð°Ð»ÑŒÐ½Ñ‹Ðµ

### Nursery Shellguard

- `ID`: `creature.territorial.nursery_shellguard`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð—Ð°Ñ‰Ð¸Ñ‚Ð½Ð¸Ðº ÐºÐ»Ð°Ð´Ð¾Ðº Ð¸ Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ñ‹Ñ… ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð².
- `Ð¡ÑƒÑ‚ÑŒ`: Ð›Ð¾ÐºÐ°Ð»ÑŒÐ½Ñ‹Ð¹ Ð·Ð°Ñ‰Ð¸Ñ‚Ð½Ð¸Ðº Ð³Ð½ÐµÐ·Ð´Ð°. Ð¡Ð½Ð°Ñ‡Ð°Ð»Ð° Ð´Ð°Ð²Ð¸Ñ‚, Ð¿Ð¾Ñ‚Ð¾Ð¼ ÑÑ€Ñ‹Ð²Ð°ÐµÑ‚ÑÑ.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.reef_ambush, fauna.family.littoral_passive
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.fossil_reef, biome.family.littoral_karst

### Archway Sentinel

- `ID`: `creature.territorial.archway_sentinel`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¡Ñ‚Ð¾Ñ€Ð¾Ð¶ Ð°Ñ€Ð¾Ðº, ÑÑ‚ÐµÐ½ Ð¸ ÑƒÐ·ÐºÐ¸Ñ… Ð¿Ñ€Ð¾Ñ…Ð¾Ð´Ð¾Ð².
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”ÐµÑ€Ð¶Ð¸Ñ‚ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚ Ð¸ Ð²Ñ‹Ñ‚Ð°Ð»ÐºÐ¸Ð²Ð°ÐµÑ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ñ Ð¿Ñ€Ð¾Ñ…Ð¾Ð´Ð°.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.granite_escarpment, biome.family.rift_spine

## Ð¥Ð¸Ñ‰Ð½Ð¸ÐºÐ¸

### Pocket Ambusher

- `ID`: `creature.hunter.pocket_ambusher`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐšÐ¾Ñ€Ð¾Ñ‚ÐºÐ°Ñ Ð·Ð°ÑÐ°Ð´Ð° Ð¸Ð· Ð¾Ð¿Ð°ÑÐ½Ñ‹Ñ… ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð².
- `Ð¡ÑƒÑ‚ÑŒ`: Ð¡Ð¸Ð´Ð¸Ñ‚ Ð² ÑƒÐºÑ€Ñ‹Ñ‚Ð¸Ð¸ Ð¸ Ð½Ð°ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÑ‚ Ð¶Ð°Ð´Ð½Ñ‹Ð¹ Ð·Ð°Ñ…Ð¾Ð´ Ð² ÐºÐ°Ñ€Ð¼Ð°Ð½.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.reef_ambush, fauna.family.sediment_scavengers
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.fossil_reef, biome.family.sediment_drift

### Needle Hunter

- `ID`: `creature.hunter.needle_hunter`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð‘Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹ Ñ€ÐµÐ¶ÑƒÑ‰Ð¸Ð¹ Ñ…Ð¸Ñ‰Ð½Ð¸Ðº ÑÑ€ÐºÐ¾Ð¹ Ð²Ð¾Ð´Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð ÐµÐ·ÐºÐ¾ Ð²Ñ…Ð¾Ð´Ð¸Ñ‚ Ð¸ Ñ€ÐµÐ·ÐºÐ¾ Ð²Ñ‹Ñ…Ð¾Ð´Ð¸Ñ‚. Ð›Ð¾Ð¼Ð°ÐµÑ‚ ÐºÐ¾Ð¼Ñ„Ð¾Ñ€Ñ‚ ÑÐºÐ¾Ñ€Ð¾ÑÑ‚ÑŒÑŽ.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.crystal_skittish, fauna.family.reef_ambush
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.crystal_growth, biome.family.littoral_karst

### Ridge Pack Cutter

- `ID`: `creature.hunter.ridge_pack_cutter`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¡Ñ‚Ð°Ð¹Ð½Ñ‹Ð¹ Ñ…Ð¸Ñ‰Ð½Ð¸Ðº Ð³Ñ€ÐµÐ±Ð½ÐµÐ¹ Ð¸ ÑÑ‚ÐµÐ½.
- `Ð¡ÑƒÑ‚ÑŒ`: ÐžÐ´Ð¸Ð½ Ð´ÐµÑ€Ð¶Ð¸Ñ‚ Ñ„Ñ€Ð¾Ð½Ñ‚, Ð´Ñ€ÑƒÐ³Ð¸Ðµ Ñ€ÐµÐ¶ÑƒÑ‚ Ñ Ñ„Ð»Ð°Ð½Ð³Ð¾Ð².
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.ridge_hunters, fauna.family.escarpment_watchers
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Stalker

- `ID`: `creature.hunter.brine_stalker`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¢ÑÐ³ÑƒÑ‡Ð¸Ð¹ Ð¾Ñ…Ð¾Ñ‚Ð½Ð¸Ðº Ñ‚Ð¾ÐºÑÐ¸Ñ‡Ð½Ð¾Ð¹ Ð¸ ÑÐµÑ€Ð²Ð¸ÑÐ½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð›ÑŽÐ±Ð¸Ñ‚ Ñ‚ÑÐ¶Ñ‘Ð»ÑƒÑŽ Ð²Ð¾Ð´Ñƒ, ÑˆÑ€Ð°Ð¼Ñ‹ ÑÐµÑ€Ð²Ð¸ÑÐ° Ð¸ Ð³Ð¾Ñ€ÑÑ‡Ð¸Ðµ ÐºÐ°Ñ€Ð¼Ð°Ð½Ñ‹.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine

### Armor Breaker

- `ID`: `creature.hunter.armor_breaker`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¢ÑÐ¶Ñ‘Ð»Ñ‹Ð¹ Ð¼ÐµÑ‚Ð°Ð»Ð»Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð¾Ñ…Ð¾Ñ‚Ð½Ð¸Ðº Ð¿Ð¾Ð·Ð´Ð½ÐµÐ¹ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: ÐÐµ ÑÐ°Ð¼Ñ‹Ð¹ Ð±Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹, Ð½Ð¾ Ð¾Ñ‡ÐµÐ½ÑŒ Ð¾Ð¿Ð°ÑÐµÐ½ Ð½Ð° Ð±Ð»Ð¸Ð·ÐºÐ¾Ð¹ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ð¸.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.metal_predators, fauna.family.hadal_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.metallic_hadal, biome.family.rift_void

### Heat Lurker

- `ID`: `creature.hunter.heat_lurker`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð“Ð¾Ñ€ÑÑ‡Ð¸Ð¹ Ð·Ð°ÑÐ°Ð´Ð½Ñ‹Ð¹ Ñ…Ð¸Ñ‰Ð½Ð¸Ðº Ð²ÑƒÐ»ÐºÐ°Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… Ð³ÑƒÐ±.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ Ñƒ Ð³Ð¾Ñ€ÑÑ‡Ð¸Ñ… Ð²Ñ‹Ð±Ñ€Ð¾ÑÐ¾Ð² Ð¸ Ñ€ÐµÐ·ÐºÐ¸Ñ… ÑƒÐ·ÐºÐ¸Ñ… Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð¾Ð².
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.thermal_hostile, fauna.family.rift_stalkers
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Shadow Interceptor

- `ID`: `creature.hunter.shadow_interceptor`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð ÐµÐ´ÐºÐ¸Ð¹ Ð¿ÐµÑ€ÐµÑ…Ð²Ð°Ñ‚Ñ‡Ð¸Ðº Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð¡Ñ‚Ñ€Ð¾Ð¸Ñ‚ ÑÑ‚Ñ€Ð°Ñ… Ð¾Ð¶Ð¸Ð´Ð°Ð½Ð¸ÐµÐ¼ Ð¸ Ð´Ð»Ð¸Ð½Ð½Ñ‹Ð¼ Ð¿ÐµÑ€ÐµÑ…Ð²Ð°Ñ‚Ð¾Ð¼.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.abyssal_sparse, fauna.family.void_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.abyssal_silt, biome.family.rift_void

### Silt Flatmaw

- `ID`: `creature.hunter.silt_flatmaw`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐžÑÐ°Ð´Ð¾Ñ‡Ð½Ñ‹Ð¹ Ð·Ð°ÑÐ°Ð´Ð½Ð¸Ðº Ð´Ð»Ñ Ñ€ÐµÑÑƒÑ€ÑÐ½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð–Ð´Ñ‘Ñ‚ Ð´Ð¾Ð±Ñ‹Ñ‡Ñƒ Ñƒ Ð´Ð½Ð° Ð¸ ÐºÐ°Ñ€Ð°ÐµÑ‚ Ð¶Ð°Ð´Ð½Ñ‹Ð¹ ÑÐ±Ð¾Ñ€ Ñ€ÐµÑÑƒÑ€ÑÐ¾Ð².
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.sediment_scavengers, fauna.family.ridge_hunters
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.sediment_drift, biome.family.granite_escarpment

## Ð›ÐµÐ²Ð¸Ð°Ñ„Ð°Ð½Ñ‹

### Halo Crown Leviathan

- `ID`: `creature.leviathan.halo_crown`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: ÐšÑ€ÑƒÐ³Ð¾Ð²Ð¾Ð¹ Ð»ÐµÐ²Ð¸Ð°Ñ„Ð°Ð½ Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ñ.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð›Ð¾Ð¼Ð°ÐµÑ‚ Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ÑÑ‚ÑŒ ÐºÑ€ÑƒÐ³Ð¾Ð¼ Ð¸ Ð¿Ð¾Ð·Ð´Ð½Ð¸Ð¼ Ð²Ñ…Ð¾Ð´Ð¾Ð¼.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.hadal_apex, fauna.family.void_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.rift_void, biome.family.abyssal_silt

### Gate Warden Leviathan

- `ID`: `creature.leviathan.gate_warden`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð¡Ñ‚Ð¾Ñ€Ð¾Ð¶ Ð³Ð»ÑƒÐ±Ð¾ÐºÐ¾Ð³Ð¾ Ð¿Ñ€Ð¾Ñ…Ð¾Ð´Ð°.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”ÐµÑ€Ð¶Ð¸Ñ‚ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚ Ð¸ Ð²Ñ‹Ð´Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð¸Ð· ÑƒÐ·ÐºÐ¾Ð³Ð¾ Ð¼ÐµÑÑ‚Ð°.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.hadal_apex, fauna.family.rift_stalkers
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.rift_spine, biome.family.volcanic_hadal, biome.family.metallic_hadal

### Rift Lancer Leviathan

- `ID`: `creature.leviathan.rift_lancer`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð Ð¸Ñ„Ñ‚Ð¾Ð²Ñ‹Ð¹ Ð»ÐµÐ²Ð¸Ð°Ñ„Ð°Ð½ Ñ€ÐµÐ·ÐºÐ¾Ð³Ð¾ Ñ€Ñ‹Ð²ÐºÐ°.
- `Ð¡ÑƒÑ‚ÑŒ`: ÐŸÑƒÐ³Ð°ÐµÑ‚ Ð»Ð¾Ð¶Ð½Ñ‹Ð¼ Ð·Ð°Ñ…Ð¾Ð´Ð¾Ð¼ Ð¸ Ð»Ð¾Ð²Ð¸Ñ‚ Ð½Ð° Ñ€ÐµÐ·ÐºÐ¾Ð¼ ÑÐ±Ð»Ð¸Ð¶ÐµÐ½Ð¸Ð¸.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.rift_stalkers, fauna.family.void_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.rift_void, biome.family.rift_spine

### Black Choir Leviathan

- `ID`: `creature.leviathan.black_choir`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð›ÐµÐ²Ð¸Ð°Ñ„Ð°Ð½ Ð¿Ð¾Ð·Ð´Ð½ÐµÐ³Ð¾ ÑƒÐ¶Ð°ÑÐ°.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð¡Ñ‚Ñ€Ð¾Ð¸Ñ‚ ÑÑ‚Ñ€Ð°Ñ… Ð¾Ð¶Ð¸Ð´Ð°Ð½Ð¸ÐµÐ¼, Ð·Ð²ÑƒÐºÐ¾Ð¼ Ð¸ Ð¿Ð¾Ð·Ð´Ð½Ð¸Ð¼ ÐºÐ¾Ð½Ñ‚Ð°ÐºÑ‚Ð¾Ð¼.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.void_apex, fauna.family.hadal_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.rift_void, biome.family.abyssal_silt

### Furnace Maw Leviathan

- `ID`: `creature.leviathan.furnace_maw`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð’ÑƒÐ»ÐºÐ°Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ ÑÑ‚Ð¾Ñ€Ð¾Ð¶ Ð³Ð¾Ñ€ÑÑ‡Ð¸Ñ… ÑˆÐ°Ñ…Ñ‚.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð–Ð¼Ñ‘Ñ‚ Ð½Ð° Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ðµ Ð¸ Ð´Ð¾Ð±Ð°Ð²Ð»ÑÐµÑ‚ Ð»Ð¾Ð¶Ð½Ñ‹Ðµ Ð¿Ñ€Ð¾Ñ…Ð¾Ð´Ñ‹ Ð¿ÐµÑ€ÐµÐ´ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð¹ Ð°Ñ‚Ð°ÐºÐ¾Ð¹.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.thermal_hostile, fauna.family.hadal_apex
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Void Ribbon Leviathan

- `ID`: `creature.leviathan.void_ribbon`
- `Ð—Ð°Ñ‡ÐµÐ¼ Ð½ÑƒÐ¶ÐµÐ½`: Ð‘Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹ Ð¿ÐµÑ€ÐµÑ…Ð²Ð°Ñ‚Ñ‡Ð¸Ðº Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ñ‹.
- `Ð¡ÑƒÑ‚ÑŒ`: Ð”Ð»Ð¸Ð½Ð½Ñ‹Ð¹ Ñ‚Ñ‘Ð¼Ð½Ñ‹Ð¹ Ð¿ÐµÑ€ÐµÑ…Ð²Ð°Ñ‚Ñ‡Ð¸Ðº Ð´Ð»Ñ Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¾Ð¹ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.
- `ÐŸÐ¾Ð´Ñ…Ð¾Ð´Ð¸Ñ‚ Ð´Ð»Ñ`: fauna.family.void_apex, fauna.family.abyssal_sparse
- `Ð‘Ð¸Ð¾Ð¼Ñ‹`: biome.family.abyssal_silt, biome.family.rift_void
