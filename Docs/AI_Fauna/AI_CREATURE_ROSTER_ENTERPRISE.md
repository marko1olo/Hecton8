# AI Creature Roster Enterprise

Date: 2026-05-07
Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
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

## ÃÂ§Ã‘â€šÃÂ¾ Ã‘ÂÃ‘â€šÃÂ¾

- ÃÂ­Ã‘â€šÃÂ¾ ÃÂ½ÃÂ°ÃÂ±ÃÂ¾Ã‘â‚¬ Ã‘â‚¬ÃÂµÃÂ°ÃÂ»Ã‘Å’ÃÂ½Ã‘â€¹Ã‘â€¦ ÃÂ¿Ã‘â‚¬ÃÂ¾Ã‘â€žÃÂ¸ÃÂ»ÃÂµÃÂ¹ ÃÂ²ÃÂ¸ÃÂ´ÃÂ¾ÃÂ².
- ÃËœÃ‘â€¦ ÃÂ¼ÃÂ¾ÃÂ¶ÃÂ½ÃÂ¾ ÃÂ¿ÃÂ¾ÃÂ´ÃÂ²ÃÂµÃ‘Ë†ÃÂ¸ÃÂ²ÃÂ°Ã‘â€šÃ‘Å’ ÃÂº ÃÂ¿Ã‘â‚¬ÃÂµÃ‘â€žÃÂ°ÃÂ±ÃÂ°ÃÂ¼ ÃÂ¸ ÃÂ¿ÃÂ¾Ã‘â€šÃÂ¾ÃÂ¼ Ã‘â‚¬ÃÂ°Ã‘ÂÃÂºÃÂ¸ÃÂ´Ã‘â€¹ÃÂ²ÃÂ°Ã‘â€šÃ‘Å’ ÃÂ¿ÃÂ¾ ÃÂ±ÃÂ¸ÃÂ¾ÃÂ¼ÃÂ°ÃÂ¼.
- ÃÅ¾Ã‘ÂÃÂ½ÃÂ¾ÃÂ²ÃÂ½ÃÂ¾ÃÂ¹ Ã‘Æ’ÃÂ¿ÃÂ¾Ã‘â‚¬ ÃÂ·ÃÂ´ÃÂµÃ‘ÂÃ‘Å’: ÃÂ¼ÃÂ½ÃÂ¾ÃÂ³ÃÂ¾ Ã‘â‚¬ÃÂ°ÃÂ·ÃÂ½Ã‘â€¹Ã‘â€¦ Ã‘â€¦ÃÂ¸Ã‘â€°ÃÂ½ÃÂ¸ÃÂºÃÂ¾ÃÂ² ÃÂ¸ ÃÂ»ÃÂµÃÂ²ÃÂ¸ÃÂ°Ã‘â€žÃÂ°ÃÂ½ÃÂ¾ÃÂ².

## ÃÅ“ÃÂ¸Ã‘â‚¬ÃÂ½ÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’

### Shore Skimmer

- `ID`: `creature.ambient.shore_skimmer`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ“ÃÂµÃÂ»ÃÂºÃÂ°Ã‘Â Ã‘ÂÃ‘â€šÃÂ°ÃÂ¹ÃÂ½ÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ Ã‘ÂÃÂ¿ÃÂ¾ÃÂºÃÂ¾ÃÂ¹ÃÂ½ÃÂ¾ÃÂ¹ ÃÂ²ÃÂ¾ÃÂ´Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂ°Ã‘â€˜Ã‘â€š ÃÂ¶ÃÂ¸ÃÂ²ÃÂ¾ÃÂ¹ Ã‘â€žÃÂ¾ÃÂ½ Ã‘Æ’ ÃÂ¿ÃÂ¾ÃÂ²ÃÂµÃ‘â‚¬Ã‘â€¦ÃÂ½ÃÂ¾Ã‘ÂÃ‘â€šÃÂ¸ ÃÂ¸ ÃÂ² Ã‘ÂÃÂ¿ÃÂ¾ÃÂºÃÂ¾ÃÂ¹ÃÂ½Ã‘â€¹Ã‘â€¦ ÃÂ°Ã‘â‚¬ÃÂºÃÂ°Ã‘â€¦.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.littoral_passive, fauna.family.reef_ambush
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.littoral_karst, biome.family.fossil_reef

### Kelp Raylet

- `ID`: `creature.ambient.kelp_raylet`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ“ÃÂ¸Ã‘â‚¬ÃÂ½ÃÂ°Ã‘Â Ã‘Ë†ÃÂ¸Ã‘â‚¬ÃÂ¾ÃÂºÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ Ã‘ÂÃ‘â‚¬ÃÂºÃÂ¸Ã‘â€¦ ÃÂ·ÃÂ°Ã‘â‚¬ÃÂ¾Ã‘ÂÃÂ»ÃÂµÃÂ¹ ÃÂ¸ Ã‘â‚¬ÃÂ¸Ã‘â€žÃÂ¾ÃÂ².
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂ°Ã‘â€˜Ã‘â€š ÃÂ¼Ã‘ÂÃÂ³ÃÂºÃ‘Æ’Ã‘Å½ ÃÂºÃ‘â‚¬Ã‘Æ’ÃÂ¿ÃÂ½Ã‘Æ’Ã‘Å½ ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ Ã‘â€šÃÂ°ÃÂ¼, ÃÂ³ÃÂ´ÃÂµ ÃÂ¼ÃÂ¸Ã‘â‚¬ ÃÂ´ÃÂ¾ÃÂ»ÃÂ¶ÃÂµÃÂ½ ÃÂºÃÂ°ÃÂ·ÃÂ°Ã‘â€šÃ‘Å’Ã‘ÂÃ‘Â ÃÂ±ÃÂ¾ÃÂ³ÃÂ°Ã‘â€šÃ‘â€¹ÃÂ¼, ÃÂ° ÃÂ½ÃÂµ ÃÂ±ÃÂ¾ÃÂµÃÂ²Ã‘â€¹ÃÂ¼.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.littoral_passive, fauna.family.crystal_skittish
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.fossil_reef, biome.family.crystal_growth

### Silt Drifter

- `ID`: `creature.ambient.silt_drifter`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€ÃÂ¾ÃÂ½ÃÂ½Ã‘â€¹ÃÂ¹ ÃÂ¼ÃÂ¸Ã‘â‚¬ÃÂ½Ã‘â€¹ÃÂ¹ Ã‘ÂÃÂ±ÃÂ¾Ã‘â‚¬Ã‘â€°ÃÂ¸ÃÂº ÃÂ¾Ã‘ÂÃÂ°ÃÂ´ÃÂ¾Ã‘â€¡ÃÂ½ÃÂ¾ÃÂ¹ ÃÂ²ÃÂ¾ÃÂ´Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂµÃÂ»ÃÂ°ÃÂµÃ‘â€š Ã‘â‚¬ÃÂµÃ‘ÂÃ‘Æ’Ã‘â‚¬Ã‘ÂÃÂ½Ã‘Æ’Ã‘Å½ ÃÂ²ÃÂ¾ÃÂ´Ã‘Æ’ ÃÂ¶ÃÂ¸ÃÂ²ÃÂ¾ÃÂ¹, ÃÂ½ÃÂ¾ ÃÂ½ÃÂµ ÃÂ°ÃÂ³Ã‘â‚¬ÃÂµÃ‘ÂÃ‘ÂÃÂ¸ÃÂ²ÃÂ½ÃÂ¾ÃÂ¹.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.sediment_scavengers, fauna.family.abyssal_sparse
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.sediment_drift, biome.family.abyssal_silt, biome.family.granite_escarpment

### Wall Glider

- `ID`: `creature.ambient.wall_glider`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ“ÃÂ¸Ã‘â‚¬ÃÂ½ÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ Ã‘ÂÃ‘â€šÃÂµÃÂ½, Ã‘Æ’Ã‘ÂÃ‘â€šÃ‘Æ’ÃÂ¿ÃÂ¾ÃÂ² ÃÂ¸ ÃÂ³Ã‘â‚¬ÃÂµÃÂ±ÃÂ½ÃÂµÃÂ¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂµÃÂ»ÃÂ°ÃÂµÃ‘â€š ÃÂ¼ÃÂ°Ã‘â‚¬Ã‘Ë†Ã‘â‚¬Ã‘Æ’Ã‘â€šÃ‘â€¹ ÃÂ²ÃÂ´ÃÂ¾ÃÂ»Ã‘Å’ Ã‘ÂÃ‘â€šÃÂµÃÂ½ ÃÂ¶ÃÂ¸ÃÂ²Ã‘â€¹ÃÂ¼ÃÂ¸ ÃÂ¸ Ã‘â€¡ÃÂ¸Ã‘â€šÃÂ°ÃÂµÃÂ¼Ã‘â€¹ÃÂ¼ÃÂ¸.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Siphoner

- `ID`: `creature.ambient.brine_siphoner`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¡Ã‘â€šÃ‘â‚¬ÃÂ°ÃÂ½ÃÂ½ÃÂ°Ã‘Â ÃÂ¼ÃÂ¸Ã‘â‚¬ÃÂ½ÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ Ã‘â€¦ÃÂ¸ÃÂ¼ÃÂ¸Ã‘â€¡ÃÂµÃ‘ÂÃÂºÃÂ¸Ã‘â€¦ ÃÂºÃÂ°Ã‘â‚¬ÃÂ¼ÃÂ°ÃÂ½ÃÂ¾ÃÂ² ÃÂ¸ ÃÂ²ÃÂµÃÂ½Ã‘â€šÃÂ¾ÃÂ².
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂÃ‘Æ’ÃÂ¶ÃÂµÃÂ½, Ã‘â€¡Ã‘â€šÃÂ¾ÃÂ±Ã‘â€¹ Ã‘â€šÃÂ¾ÃÂºÃ‘ÂÃÂ¸Ã‘â€¡ÃÂ½ÃÂ°Ã‘Â ÃÂ¸ Ã‘ÂÃÂµÃ‘â‚¬ÃÂ²ÃÂ¸Ã‘ÂÃÂ½ÃÂ°Ã‘Â ÃÂ²ÃÂ¾ÃÂ´ÃÂ° ÃÂ½ÃÂµ ÃÂ±Ã‘â€¹ÃÂ»ÃÂ° ÃÂ¼Ã‘â€˜Ã‘â‚¬Ã‘â€šÃÂ²ÃÂ¾ÃÂ¹.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine, biome.family.volcanic_glass

### Lantern Sifter

- `ID`: `creature.ambient.lantern_sifter`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ ÃÂµÃÂ´ÃÂºÃÂ°Ã‘Â ÃÂ¼ÃÂ¸Ã‘â‚¬ÃÂ½ÃÂ°Ã‘Â ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½Ã‘Å’ ÃÂ¾Ã‘â€šÃÂºÃ‘â‚¬Ã‘â€¹Ã‘â€šÃÂ¾ÃÂ¹ ÃÂ³ÃÂ»Ã‘Æ’ÃÂ±ÃÂ¸ÃÂ½Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂ°Ã‘â€˜Ã‘â€š ÃÂ¾Ã‘â€°Ã‘Æ’Ã‘â€°ÃÂµÃÂ½ÃÂ¸ÃÂµ Ã‘â‚¬ÃÂµÃÂ´ÃÂºÃÂ¾ÃÂ¹ ÃÂ¶ÃÂ¸ÃÂ·ÃÂ½ÃÂ¸ ÃÂ´ÃÂ°ÃÂ¶ÃÂµ ÃÂ² ÃÂ¿ÃÂ¾ÃÂ·ÃÂ´ÃÂ½ÃÂµÃÂ¹ ÃÂ¿Ã‘Æ’Ã‘ÂÃ‘â€šÃÂ¾Ã‘â€šÃÂµ.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.abyssal_sparse, fauna.family.hadal_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.abyssal_silt, biome.family.metallic_hadal, biome.family.rift_void

## ÃÂ¢ÃÂµÃ‘â‚¬Ã‘â‚¬ÃÂ¸Ã‘â€šÃÂ¾Ã‘â‚¬ÃÂ¸ÃÂ°ÃÂ»Ã‘Å’ÃÂ½Ã‘â€¹ÃÂµ

### Nursery Shellguard

- `ID`: `creature.territorial.nursery_shellguard`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€”ÃÂ°Ã‘â€°ÃÂ¸Ã‘â€šÃÂ½ÃÂ¸ÃÂº ÃÂºÃÂ»ÃÂ°ÃÂ´ÃÂ¾ÃÂº ÃÂ¸ ÃÂ±ÃÂµÃÂ·ÃÂ¾ÃÂ¿ÃÂ°Ã‘ÂÃÂ½Ã‘â€¹Ã‘â€¦ ÃÂºÃÂ°Ã‘â‚¬ÃÂ¼ÃÂ°ÃÂ½ÃÂ¾ÃÂ².
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ºÃÂ¾ÃÂºÃÂ°ÃÂ»Ã‘Å’ÃÂ½Ã‘â€¹ÃÂ¹ ÃÂ·ÃÂ°Ã‘â€°ÃÂ¸Ã‘â€šÃÂ½ÃÂ¸ÃÂº ÃÂ³ÃÂ½ÃÂµÃÂ·ÃÂ´ÃÂ°. ÃÂ¡ÃÂ½ÃÂ°Ã‘â€¡ÃÂ°ÃÂ»ÃÂ° ÃÂ´ÃÂ°ÃÂ²ÃÂ¸Ã‘â€š, ÃÂ¿ÃÂ¾Ã‘â€šÃÂ¾ÃÂ¼ Ã‘ÂÃ‘â‚¬Ã‘â€¹ÃÂ²ÃÂ°ÃÂµÃ‘â€šÃ‘ÂÃ‘Â.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.reef_ambush, fauna.family.littoral_passive
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.fossil_reef, biome.family.littoral_karst

### Archway Sentinel

- `ID`: `creature.territorial.archway_sentinel`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¡Ã‘â€šÃÂ¾Ã‘â‚¬ÃÂ¾ÃÂ¶ ÃÂ°Ã‘â‚¬ÃÂ¾ÃÂº, Ã‘ÂÃ‘â€šÃÂµÃÂ½ ÃÂ¸ Ã‘Æ’ÃÂ·ÃÂºÃÂ¸Ã‘â€¦ ÃÂ¿Ã‘â‚¬ÃÂ¾Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¾ÃÂ².
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂµÃ‘â‚¬ÃÂ¶ÃÂ¸Ã‘â€š ÃÂ¼ÃÂ°Ã‘â‚¬Ã‘Ë†Ã‘â‚¬Ã‘Æ’Ã‘â€š ÃÂ¸ ÃÂ²Ã‘â€¹Ã‘â€šÃÂ°ÃÂ»ÃÂºÃÂ¸ÃÂ²ÃÂ°ÃÂµÃ‘â€š ÃÂ¸ÃÂ³Ã‘â‚¬ÃÂ¾ÃÂºÃÂ° Ã‘Â ÃÂ¿Ã‘â‚¬ÃÂ¾Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ°.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.granite_escarpment, biome.family.rift_spine

## ÃÂ¥ÃÂ¸Ã‘â€°ÃÂ½ÃÂ¸ÃÂºÃÂ¸

### Pocket Ambusher

- `ID`: `creature.hunter.pocket_ambusher`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ¡ÃÂ¾Ã‘â‚¬ÃÂ¾Ã‘â€šÃÂºÃÂ°Ã‘Â ÃÂ·ÃÂ°Ã‘ÂÃÂ°ÃÂ´ÃÂ° ÃÂ¸ÃÂ· ÃÂ¾ÃÂ¿ÃÂ°Ã‘ÂÃÂ½Ã‘â€¹Ã‘â€¦ ÃÂºÃÂ°Ã‘â‚¬ÃÂ¼ÃÂ°ÃÂ½ÃÂ¾ÃÂ².
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂ¡ÃÂ¸ÃÂ´ÃÂ¸Ã‘â€š ÃÂ² Ã‘Æ’ÃÂºÃ‘â‚¬Ã‘â€¹Ã‘â€šÃÂ¸ÃÂ¸ ÃÂ¸ ÃÂ½ÃÂ°ÃÂºÃÂ°ÃÂ·Ã‘â€¹ÃÂ²ÃÂ°ÃÂµÃ‘â€š ÃÂ¶ÃÂ°ÃÂ´ÃÂ½Ã‘â€¹ÃÂ¹ ÃÂ·ÃÂ°Ã‘â€¦ÃÂ¾ÃÂ´ ÃÂ² ÃÂºÃÂ°Ã‘â‚¬ÃÂ¼ÃÂ°ÃÂ½.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.reef_ambush, fauna.family.sediment_scavengers
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.fossil_reef, biome.family.sediment_drift

### Needle Hunter

- `ID`: `creature.hunter.needle_hunter`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€˜Ã‘â€¹Ã‘ÂÃ‘â€šÃ‘â‚¬Ã‘â€¹ÃÂ¹ Ã‘â‚¬ÃÂµÃÂ¶Ã‘Æ’Ã‘â€°ÃÂ¸ÃÂ¹ Ã‘â€¦ÃÂ¸Ã‘â€°ÃÂ½ÃÂ¸ÃÂº Ã‘ÂÃ‘â‚¬ÃÂºÃÂ¾ÃÂ¹ ÃÂ²ÃÂ¾ÃÂ´Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂ ÃÂµÃÂ·ÃÂºÃÂ¾ ÃÂ²Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ¸ Ã‘â‚¬ÃÂµÃÂ·ÃÂºÃÂ¾ ÃÂ²Ã‘â€¹Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š. Ãâ€ºÃÂ¾ÃÂ¼ÃÂ°ÃÂµÃ‘â€š ÃÂºÃÂ¾ÃÂ¼Ã‘â€žÃÂ¾Ã‘â‚¬Ã‘â€š Ã‘ÂÃÂºÃÂ¾Ã‘â‚¬ÃÂ¾Ã‘ÂÃ‘â€šÃ‘Å’Ã‘Å½.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.crystal_skittish, fauna.family.reef_ambush
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.crystal_growth, biome.family.littoral_karst

### Ridge Pack Cutter

- `ID`: `creature.hunter.ridge_pack_cutter`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¡Ã‘â€šÃÂ°ÃÂ¹ÃÂ½Ã‘â€¹ÃÂ¹ Ã‘â€¦ÃÂ¸Ã‘â€°ÃÂ½ÃÂ¸ÃÂº ÃÂ³Ã‘â‚¬ÃÂµÃÂ±ÃÂ½ÃÂµÃÂ¹ ÃÂ¸ Ã‘ÂÃ‘â€šÃÂµÃÂ½.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÅ¾ÃÂ´ÃÂ¸ÃÂ½ ÃÂ´ÃÂµÃ‘â‚¬ÃÂ¶ÃÂ¸Ã‘â€š Ã‘â€žÃ‘â‚¬ÃÂ¾ÃÂ½Ã‘â€š, ÃÂ´Ã‘â‚¬Ã‘Æ’ÃÂ³ÃÂ¸ÃÂµ Ã‘â‚¬ÃÂµÃÂ¶Ã‘Æ’Ã‘â€š Ã‘Â Ã‘â€žÃÂ»ÃÂ°ÃÂ½ÃÂ³ÃÂ¾ÃÂ².
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.ridge_hunters, fauna.family.escarpment_watchers
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Stalker

- `ID`: `creature.hunter.brine_stalker`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¢Ã‘ÂÃÂ³Ã‘Æ’Ã‘â€¡ÃÂ¸ÃÂ¹ ÃÂ¾Ã‘â€¦ÃÂ¾Ã‘â€šÃÂ½ÃÂ¸ÃÂº Ã‘â€šÃÂ¾ÃÂºÃ‘ÂÃÂ¸Ã‘â€¡ÃÂ½ÃÂ¾ÃÂ¹ ÃÂ¸ Ã‘ÂÃÂµÃ‘â‚¬ÃÂ²ÃÂ¸Ã‘ÂÃÂ½ÃÂ¾ÃÂ¹ ÃÂ²ÃÂ¾ÃÂ´Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ºÃ‘Å½ÃÂ±ÃÂ¸Ã‘â€š Ã‘â€šÃ‘ÂÃÂ¶Ã‘â€˜ÃÂ»Ã‘Æ’Ã‘Å½ ÃÂ²ÃÂ¾ÃÂ´Ã‘Æ’, Ã‘Ë†Ã‘â‚¬ÃÂ°ÃÂ¼Ã‘â€¹ Ã‘ÂÃÂµÃ‘â‚¬ÃÂ²ÃÂ¸Ã‘ÂÃÂ° ÃÂ¸ ÃÂ³ÃÂ¾Ã‘â‚¬Ã‘ÂÃ‘â€¡ÃÂ¸ÃÂµ ÃÂºÃÂ°Ã‘â‚¬ÃÂ¼ÃÂ°ÃÂ½Ã‘â€¹.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine

### Armor Breaker

- `ID`: `creature.hunter.armor_breaker`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¢Ã‘ÂÃÂ¶Ã‘â€˜ÃÂ»Ã‘â€¹ÃÂ¹ ÃÂ¼ÃÂµÃ‘â€šÃÂ°ÃÂ»ÃÂ»ÃÂ¸Ã‘â€¡ÃÂµÃ‘ÂÃÂºÃÂ¸ÃÂ¹ ÃÂ¾Ã‘â€¦ÃÂ¾Ã‘â€šÃÂ½ÃÂ¸ÃÂº ÃÂ¿ÃÂ¾ÃÂ·ÃÂ´ÃÂ½ÃÂµÃÂ¹ ÃÂ³ÃÂ»Ã‘Æ’ÃÂ±ÃÂ¸ÃÂ½Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂÃÂµ Ã‘ÂÃÂ°ÃÂ¼Ã‘â€¹ÃÂ¹ ÃÂ±Ã‘â€¹Ã‘ÂÃ‘â€šÃ‘â‚¬Ã‘â€¹ÃÂ¹, ÃÂ½ÃÂ¾ ÃÂ¾Ã‘â€¡ÃÂµÃÂ½Ã‘Å’ ÃÂ¾ÃÂ¿ÃÂ°Ã‘ÂÃÂµÃÂ½ ÃÂ½ÃÂ° ÃÂ±ÃÂ»ÃÂ¸ÃÂ·ÃÂºÃÂ¾ÃÂ¹ ÃÂ´ÃÂ¸Ã‘ÂÃ‘â€šÃÂ°ÃÂ½Ã‘â€ ÃÂ¸ÃÂ¸.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.metal_predators, fauna.family.hadal_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.metallic_hadal, biome.family.rift_void

### Heat Lurker

- `ID`: `creature.hunter.heat_lurker`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€œÃÂ¾Ã‘â‚¬Ã‘ÂÃ‘â€¡ÃÂ¸ÃÂ¹ ÃÂ·ÃÂ°Ã‘ÂÃÂ°ÃÂ´ÃÂ½Ã‘â€¹ÃÂ¹ Ã‘â€¦ÃÂ¸Ã‘â€°ÃÂ½ÃÂ¸ÃÂº ÃÂ²Ã‘Æ’ÃÂ»ÃÂºÃÂ°ÃÂ½ÃÂ¸Ã‘â€¡ÃÂµÃ‘ÂÃÂºÃÂ¸Ã‘â€¦ ÃÂ³Ã‘Æ’ÃÂ±.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂ ÃÂ°ÃÂ±ÃÂ¾Ã‘â€šÃÂ°ÃÂµÃ‘â€š Ã‘Æ’ ÃÂ³ÃÂ¾Ã‘â‚¬Ã‘ÂÃ‘â€¡ÃÂ¸Ã‘â€¦ ÃÂ²Ã‘â€¹ÃÂ±Ã‘â‚¬ÃÂ¾Ã‘ÂÃÂ¾ÃÂ² ÃÂ¸ Ã‘â‚¬ÃÂµÃÂ·ÃÂºÃÂ¸Ã‘â€¦ Ã‘Æ’ÃÂ·ÃÂºÃÂ¸Ã‘â€¦ ÃÂ¼ÃÂ°Ã‘â‚¬Ã‘Ë†Ã‘â‚¬Ã‘Æ’Ã‘â€šÃÂ¾ÃÂ².
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.thermal_hostile, fauna.family.rift_stalkers
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Shadow Interceptor

- `ID`: `creature.hunter.shadow_interceptor`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ ÃÂµÃÂ´ÃÂºÃÂ¸ÃÂ¹ ÃÂ¿ÃÂµÃ‘â‚¬ÃÂµÃ‘â€¦ÃÂ²ÃÂ°Ã‘â€šÃ‘â€¡ÃÂ¸ÃÂº ÃÂ¿Ã‘Æ’Ã‘ÂÃ‘â€šÃÂ¾Ã‘â€šÃ‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂ¡Ã‘â€šÃ‘â‚¬ÃÂ¾ÃÂ¸Ã‘â€š Ã‘ÂÃ‘â€šÃ‘â‚¬ÃÂ°Ã‘â€¦ ÃÂ¾ÃÂ¶ÃÂ¸ÃÂ´ÃÂ°ÃÂ½ÃÂ¸ÃÂµÃÂ¼ ÃÂ¸ ÃÂ´ÃÂ»ÃÂ¸ÃÂ½ÃÂ½Ã‘â€¹ÃÂ¼ ÃÂ¿ÃÂµÃ‘â‚¬ÃÂµÃ‘â€¦ÃÂ²ÃÂ°Ã‘â€šÃÂ¾ÃÂ¼.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.abyssal_sparse, fauna.family.void_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.abyssal_silt, biome.family.rift_void

### Silt Flatmaw

- `ID`: `creature.hunter.silt_flatmaw`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ¾Ã‘ÂÃÂ°ÃÂ´ÃÂ¾Ã‘â€¡ÃÂ½Ã‘â€¹ÃÂ¹ ÃÂ·ÃÂ°Ã‘ÂÃÂ°ÃÂ´ÃÂ½ÃÂ¸ÃÂº ÃÂ´ÃÂ»Ã‘Â Ã‘â‚¬ÃÂµÃ‘ÂÃ‘Æ’Ã‘â‚¬Ã‘ÂÃÂ½ÃÂ¾ÃÂ¹ ÃÂ²ÃÂ¾ÃÂ´Ã‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€“ÃÂ´Ã‘â€˜Ã‘â€š ÃÂ´ÃÂ¾ÃÂ±Ã‘â€¹Ã‘â€¡Ã‘Æ’ Ã‘Æ’ ÃÂ´ÃÂ½ÃÂ° ÃÂ¸ ÃÂºÃÂ°Ã‘â‚¬ÃÂ°ÃÂµÃ‘â€š ÃÂ¶ÃÂ°ÃÂ´ÃÂ½Ã‘â€¹ÃÂ¹ Ã‘ÂÃÂ±ÃÂ¾Ã‘â‚¬ Ã‘â‚¬ÃÂµÃ‘ÂÃ‘Æ’Ã‘â‚¬Ã‘ÂÃÂ¾ÃÂ².
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.sediment_scavengers, fauna.family.ridge_hunters
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.sediment_drift, biome.family.granite_escarpment

## Ãâ€ºÃÂµÃÂ²ÃÂ¸ÃÂ°Ã‘â€žÃÂ°ÃÂ½Ã‘â€¹

### Halo Crown Leviathan

- `ID`: `creature.leviathan.halo_crown`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÅ¡Ã‘â‚¬Ã‘Æ’ÃÂ³ÃÂ¾ÃÂ²ÃÂ¾ÃÂ¹ ÃÂ»ÃÂµÃÂ²ÃÂ¸ÃÂ°Ã‘â€žÃÂ°ÃÂ½ ÃÂ´ÃÂ°ÃÂ²ÃÂ»ÃÂµÃÂ½ÃÂ¸Ã‘Â.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ºÃÂ¾ÃÂ¼ÃÂ°ÃÂµÃ‘â€š ÃÂ±ÃÂµÃÂ·ÃÂ¾ÃÂ¿ÃÂ°Ã‘ÂÃÂ½ÃÂ¾Ã‘ÂÃ‘â€šÃ‘Å’ ÃÂºÃ‘â‚¬Ã‘Æ’ÃÂ³ÃÂ¾ÃÂ¼ ÃÂ¸ ÃÂ¿ÃÂ¾ÃÂ·ÃÂ´ÃÂ½ÃÂ¸ÃÂ¼ ÃÂ²Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¾ÃÂ¼.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.hadal_apex, fauna.family.void_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.rift_void, biome.family.abyssal_silt

### Gate Warden Leviathan

- `ID`: `creature.leviathan.gate_warden`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ¡Ã‘â€šÃÂ¾Ã‘â‚¬ÃÂ¾ÃÂ¶ ÃÂ³ÃÂ»Ã‘Æ’ÃÂ±ÃÂ¾ÃÂºÃÂ¾ÃÂ³ÃÂ¾ ÃÂ¿Ã‘â‚¬ÃÂ¾Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ°.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂµÃ‘â‚¬ÃÂ¶ÃÂ¸Ã‘â€š ÃÂ¼ÃÂ°Ã‘â‚¬Ã‘Ë†Ã‘â‚¬Ã‘Æ’Ã‘â€š ÃÂ¸ ÃÂ²Ã‘â€¹ÃÂ´ÃÂ°ÃÂ²ÃÂ»ÃÂ¸ÃÂ²ÃÂ°ÃÂµÃ‘â€š ÃÂ¸ÃÂ³Ã‘â‚¬ÃÂ¾ÃÂºÃÂ° ÃÂ¸ÃÂ· Ã‘Æ’ÃÂ·ÃÂºÃÂ¾ÃÂ³ÃÂ¾ ÃÂ¼ÃÂµÃ‘ÂÃ‘â€šÃÂ°.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.hadal_apex, fauna.family.rift_stalkers
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.rift_spine, biome.family.volcanic_hadal, biome.family.metallic_hadal

### Rift Lancer Leviathan

- `ID`: `creature.leviathan.rift_lancer`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: ÃÂ ÃÂ¸Ã‘â€žÃ‘â€šÃÂ¾ÃÂ²Ã‘â€¹ÃÂ¹ ÃÂ»ÃÂµÃÂ²ÃÂ¸ÃÂ°Ã‘â€žÃÂ°ÃÂ½ Ã‘â‚¬ÃÂµÃÂ·ÃÂºÃÂ¾ÃÂ³ÃÂ¾ Ã‘â‚¬Ã‘â€¹ÃÂ²ÃÂºÃÂ°.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÅ¸Ã‘Æ’ÃÂ³ÃÂ°ÃÂµÃ‘â€š ÃÂ»ÃÂ¾ÃÂ¶ÃÂ½Ã‘â€¹ÃÂ¼ ÃÂ·ÃÂ°Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¾ÃÂ¼ ÃÂ¸ ÃÂ»ÃÂ¾ÃÂ²ÃÂ¸Ã‘â€š ÃÂ½ÃÂ° Ã‘â‚¬ÃÂµÃÂ·ÃÂºÃÂ¾ÃÂ¼ Ã‘ÂÃÂ±ÃÂ»ÃÂ¸ÃÂ¶ÃÂµÃÂ½ÃÂ¸ÃÂ¸.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.rift_stalkers, fauna.family.void_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.rift_void, biome.family.rift_spine

### Black Choir Leviathan

- `ID`: `creature.leviathan.black_choir`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€ºÃÂµÃÂ²ÃÂ¸ÃÂ°Ã‘â€žÃÂ°ÃÂ½ ÃÂ¿ÃÂ¾ÃÂ·ÃÂ´ÃÂ½ÃÂµÃÂ³ÃÂ¾ Ã‘Æ’ÃÂ¶ÃÂ°Ã‘ÂÃÂ°.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: ÃÂ¡Ã‘â€šÃ‘â‚¬ÃÂ¾ÃÂ¸Ã‘â€š Ã‘ÂÃ‘â€šÃ‘â‚¬ÃÂ°Ã‘â€¦ ÃÂ¾ÃÂ¶ÃÂ¸ÃÂ´ÃÂ°ÃÂ½ÃÂ¸ÃÂµÃÂ¼, ÃÂ·ÃÂ²Ã‘Æ’ÃÂºÃÂ¾ÃÂ¼ ÃÂ¸ ÃÂ¿ÃÂ¾ÃÂ·ÃÂ´ÃÂ½ÃÂ¸ÃÂ¼ ÃÂºÃÂ¾ÃÂ½Ã‘â€šÃÂ°ÃÂºÃ‘â€šÃÂ¾ÃÂ¼.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.void_apex, fauna.family.hadal_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.rift_void, biome.family.abyssal_silt

### Furnace Maw Leviathan

- `ID`: `creature.leviathan.furnace_maw`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€™Ã‘Æ’ÃÂ»ÃÂºÃÂ°ÃÂ½ÃÂ¸Ã‘â€¡ÃÂµÃ‘ÂÃÂºÃÂ¸ÃÂ¹ Ã‘ÂÃ‘â€šÃÂ¾Ã‘â‚¬ÃÂ¾ÃÂ¶ ÃÂ³ÃÂ¾Ã‘â‚¬Ã‘ÂÃ‘â€¡ÃÂ¸Ã‘â€¦ Ã‘Ë†ÃÂ°Ã‘â€¦Ã‘â€š.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€“ÃÂ¼Ã‘â€˜Ã‘â€š ÃÂ½ÃÂ° ÃÂ¼ÃÂ°Ã‘â‚¬Ã‘Ë†Ã‘â‚¬Ã‘Æ’Ã‘â€šÃÂµ ÃÂ¸ ÃÂ´ÃÂ¾ÃÂ±ÃÂ°ÃÂ²ÃÂ»Ã‘ÂÃÂµÃ‘â€š ÃÂ»ÃÂ¾ÃÂ¶ÃÂ½Ã‘â€¹ÃÂµ ÃÂ¿Ã‘â‚¬ÃÂ¾Ã‘â€¦ÃÂ¾ÃÂ´Ã‘â€¹ ÃÂ¿ÃÂµÃ‘â‚¬ÃÂµÃÂ´ Ã‘â‚¬ÃÂµÃÂ°ÃÂ»Ã‘Å’ÃÂ½ÃÂ¾ÃÂ¹ ÃÂ°Ã‘â€šÃÂ°ÃÂºÃÂ¾ÃÂ¹.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.thermal_hostile, fauna.family.hadal_apex
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Void Ribbon Leviathan

- `ID`: `creature.leviathan.void_ribbon`
- `Ãâ€”ÃÂ°Ã‘â€¡ÃÂµÃÂ¼ ÃÂ½Ã‘Æ’ÃÂ¶ÃÂµÃÂ½`: Ãâ€˜Ã‘â€¹Ã‘ÂÃ‘â€šÃ‘â‚¬Ã‘â€¹ÃÂ¹ ÃÂ¿ÃÂµÃ‘â‚¬ÃÂµÃ‘â€¦ÃÂ²ÃÂ°Ã‘â€šÃ‘â€¡ÃÂ¸ÃÂº ÃÂ¿Ã‘Æ’Ã‘ÂÃ‘â€šÃÂ¾Ã‘â€šÃ‘â€¹.
- `ÃÂ¡Ã‘Æ’Ã‘â€šÃ‘Å’`: Ãâ€ÃÂ»ÃÂ¸ÃÂ½ÃÂ½Ã‘â€¹ÃÂ¹ Ã‘â€šÃ‘â€˜ÃÂ¼ÃÂ½Ã‘â€¹ÃÂ¹ ÃÂ¿ÃÂµÃ‘â‚¬ÃÂµÃ‘â€¦ÃÂ²ÃÂ°Ã‘â€šÃ‘â€¡ÃÂ¸ÃÂº ÃÂ´ÃÂ»Ã‘Â ÃÂ¾Ã‘â€šÃÂºÃ‘â‚¬Ã‘â€¹Ã‘â€šÃÂ¾ÃÂ¹ ÃÂ³ÃÂ»Ã‘Æ’ÃÂ±ÃÂ¸ÃÂ½Ã‘â€¹.
- `ÃÅ¸ÃÂ¾ÃÂ´Ã‘â€¦ÃÂ¾ÃÂ´ÃÂ¸Ã‘â€š ÃÂ´ÃÂ»Ã‘Â`: fauna.family.void_apex, fauna.family.abyssal_sparse
- `Ãâ€˜ÃÂ¸ÃÂ¾ÃÂ¼Ã‘â€¹`: biome.family.abyssal_silt, biome.family.rift_void
