# AI Creature Roster Enterprise

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-01 Current-State Boundary

- This roster is retained as reference, but much of the prose is encoding-damaged.
- Treat stable `ID`, fauna family, and biome family fields as pointers only.
- Do not use this prose as production writing, runtime truth, or final design copy until it is re-authored from source.
- Current runtime fauna truth must be checked in source, registries, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## Что это

- Это набор реальных профилей видов.
- Их можно подвешивать к префабам и потом раскидывать по биомам.
- Основной упор здесь: много разных хищников и левиафанов.

## Мирная жизнь

### Shore Skimmer

- `ID`: `creature.ambient.shore_skimmer`
- `Зачем нужен`: Мелкая стайная жизнь спокойной воды.
- `Суть`: Даёт живой фон у поверхности и в спокойных арках.
- `Подходит для`: fauna.family.littoral_passive, fauna.family.reef_ambush
- `Биомы`: biome.family.littoral_karst, biome.family.fossil_reef

### Kelp Raylet

- `ID`: `creature.ambient.kelp_raylet`
- `Зачем нужен`: Мирная широкая жизнь ярких зарослей и рифов.
- `Суть`: Даёт мягкую крупную жизнь там, где мир должен казаться богатым, а не боевым.
- `Подходит для`: fauna.family.littoral_passive, fauna.family.crystal_skittish
- `Биомы`: biome.family.fossil_reef, biome.family.crystal_growth

### Silt Drifter

- `ID`: `creature.ambient.silt_drifter`
- `Зачем нужен`: Донный мирный сборщик осадочной воды.
- `Суть`: Делает ресурсную воду живой, но не агрессивной.
- `Подходит для`: fauna.family.sediment_scavengers, fauna.family.abyssal_sparse
- `Биомы`: biome.family.sediment_drift, biome.family.abyssal_silt, biome.family.granite_escarpment

### Wall Glider

- `ID`: `creature.ambient.wall_glider`
- `Зачем нужен`: Мирная жизнь стен, уступов и гребней.
- `Суть`: Делает маршруты вдоль стен живыми и читаемыми.
- `Подходит для`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Биомы`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Siphoner

- `ID`: `creature.ambient.brine_siphoner`
- `Зачем нужен`: Странная мирная жизнь химических карманов и вентов.
- `Суть`: Нужен, чтобы токсичная и сервисная вода не была мёртвой.
- `Подходит для`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Биомы`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine, biome.family.volcanic_glass

### Lantern Sifter

- `ID`: `creature.ambient.lantern_sifter`
- `Зачем нужен`: Редкая мирная жизнь открытой глубины.
- `Суть`: Даёт ощущение редкой жизни даже в поздней пустоте.
- `Подходит для`: fauna.family.abyssal_sparse, fauna.family.hadal_apex
- `Биомы`: biome.family.abyssal_silt, biome.family.metallic_hadal, biome.family.rift_void

## Территориальные

### Nursery Shellguard

- `ID`: `creature.territorial.nursery_shellguard`
- `Зачем нужен`: Защитник кладок и безопасных карманов.
- `Суть`: Локальный защитник гнезда. Сначала давит, потом срывается.
- `Подходит для`: fauna.family.reef_ambush, fauna.family.littoral_passive
- `Биомы`: biome.family.fossil_reef, biome.family.littoral_karst

### Archway Sentinel

- `ID`: `creature.territorial.archway_sentinel`
- `Зачем нужен`: Сторож арок, стен и узких проходов.
- `Суть`: Держит маршрут и выталкивает игрока с прохода.
- `Подходит для`: fauna.family.escarpment_watchers, fauna.family.ridge_hunters
- `Биомы`: biome.family.granite_escarpment, biome.family.rift_spine

## Хищники

### Pocket Ambusher

- `ID`: `creature.hunter.pocket_ambusher`
- `Зачем нужен`: Короткая засада из опасных карманов.
- `Суть`: Сидит в укрытии и наказывает жадный заход в карман.
- `Подходит для`: fauna.family.reef_ambush, fauna.family.sediment_scavengers
- `Биомы`: biome.family.fossil_reef, biome.family.sediment_drift

### Needle Hunter

- `ID`: `creature.hunter.needle_hunter`
- `Зачем нужен`: Быстрый режущий хищник яркой воды.
- `Суть`: Резко входит и резко выходит. Ломает комфорт скоростью.
- `Подходит для`: fauna.family.crystal_skittish, fauna.family.reef_ambush
- `Биомы`: biome.family.crystal_growth, biome.family.littoral_karst

### Ridge Pack Cutter

- `ID`: `creature.hunter.ridge_pack_cutter`
- `Зачем нужен`: Стайный хищник гребней и стен.
- `Суть`: Один держит фронт, другие режут с флангов.
- `Подходит для`: fauna.family.ridge_hunters, fauna.family.escarpment_watchers
- `Биомы`: biome.family.granite_escarpment, biome.family.rift_spine

### Brine Stalker

- `ID`: `creature.hunter.brine_stalker`
- `Зачем нужен`: Тягучий охотник токсичной и сервисной воды.
- `Суть`: Любит тяжёлую воду, шрамы сервиса и горячие карманы.
- `Подходит для`: fauna.family.chemical_specialists, fauna.family.thermal_hostile
- `Биомы`: biome.family.chemosynthetic_brine, biome.family.tectonic_spine

### Armor Breaker

- `ID`: `creature.hunter.armor_breaker`
- `Зачем нужен`: Тяжёлый металлический охотник поздней глубины.
- `Суть`: Не самый быстрый, но очень опасен на близкой дистанции.
- `Подходит для`: fauna.family.metal_predators, fauna.family.hadal_apex
- `Биомы`: biome.family.metallic_hadal, biome.family.rift_void

### Heat Lurker

- `ID`: `creature.hunter.heat_lurker`
- `Зачем нужен`: Горячий засадный хищник вулканических губ.
- `Суть`: Работает у горячих выбросов и резких узких маршрутов.
- `Подходит для`: fauna.family.thermal_hostile, fauna.family.rift_stalkers
- `Биомы`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Shadow Interceptor

- `ID`: `creature.hunter.shadow_interceptor`
- `Зачем нужен`: Редкий перехватчик пустоты.
- `Суть`: Строит страх ожиданием и длинным перехватом.
- `Подходит для`: fauna.family.abyssal_sparse, fauna.family.void_apex
- `Биомы`: biome.family.abyssal_silt, biome.family.rift_void

### Silt Flatmaw

- `ID`: `creature.hunter.silt_flatmaw`
- `Зачем нужен`: Осадочный засадник для ресурсной воды.
- `Суть`: Ждёт добычу у дна и карает жадный сбор ресурсов.
- `Подходит для`: fauna.family.sediment_scavengers, fauna.family.ridge_hunters
- `Биомы`: biome.family.sediment_drift, biome.family.granite_escarpment

## Левиафаны

### Halo Crown Leviathan

- `ID`: `creature.leviathan.halo_crown`
- `Зачем нужен`: Круговой левиафан давления.
- `Суть`: Ломает безопасность кругом и поздним входом.
- `Подходит для`: fauna.family.hadal_apex, fauna.family.void_apex
- `Биомы`: biome.family.rift_void, biome.family.abyssal_silt

### Gate Warden Leviathan

- `ID`: `creature.leviathan.gate_warden`
- `Зачем нужен`: Сторож глубокого прохода.
- `Суть`: Держит маршрут и выдавливает игрока из узкого места.
- `Подходит для`: fauna.family.hadal_apex, fauna.family.rift_stalkers
- `Биомы`: biome.family.rift_spine, biome.family.volcanic_hadal, biome.family.metallic_hadal

### Rift Lancer Leviathan

- `ID`: `creature.leviathan.rift_lancer`
- `Зачем нужен`: Рифтовый левиафан резкого рывка.
- `Суть`: Пугает ложным заходом и ловит на резком сближении.
- `Подходит для`: fauna.family.rift_stalkers, fauna.family.void_apex
- `Биомы`: biome.family.rift_void, biome.family.rift_spine

### Black Choir Leviathan

- `ID`: `creature.leviathan.black_choir`
- `Зачем нужен`: Левиафан позднего ужаса.
- `Суть`: Строит страх ожиданием, звуком и поздним контактом.
- `Подходит для`: fauna.family.void_apex, fauna.family.hadal_apex
- `Биомы`: biome.family.rift_void, biome.family.abyssal_silt

### Furnace Maw Leviathan

- `ID`: `creature.leviathan.furnace_maw`
- `Зачем нужен`: Вулканический сторож горячих шахт.
- `Суть`: Жмёт на маршруте и добавляет ложные проходы перед реальной атакой.
- `Подходит для`: fauna.family.thermal_hostile, fauna.family.hadal_apex
- `Биомы`: biome.family.volcanic_glass, biome.family.volcanic_hadal

### Void Ribbon Leviathan

- `ID`: `creature.leviathan.void_ribbon`
- `Зачем нужен`: Быстрый перехватчик пустоты.
- `Суть`: Длинный тёмный перехватчик для открытой глубины.
- `Подходит для`: fauna.family.void_apex, fauna.family.abyssal_sparse
- `Биомы`: biome.family.abyssal_silt, biome.family.rift_void
