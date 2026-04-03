# WORLD_RUNTIME_STABILIZATION_PLAN_2026-04-02

## Активный безопасный pass — 2026-04-03

- Текущая практическая цель: убрать первый тяжёлый `scatter` burst из первых секунд плавания без изменения состава мира.
- Принятое безопасное решение:
  - не трогать правила cave/geology
  - не крутить `radiusCells` на лету
  - не менять meaning-level scatter selection
  - увести первый startup rebuild под `SceneBootstrap` до `ActivatePlayer()`
- Это считается безопасным направлением, потому что:
  - меняет только момент выполнения тяжёлой работы
  - не меняет сами правила появления surface/cave content
  - не повторяет сломанный путь с runtime `scatter-radius-scale`

## Назначение документа

Этот документ фиксирует не "разбор текущего косяка", а рабочий roadmap по тому,
как довести runtime world pipeline до production-ready состояния под реальные
ограничения проекта:

- GPU: MX350 2 GB VRAM
- RAM: 12 GB
- CPU: Core i5-1135G7
- Жанр и сцена: подводный мир, где дальняя читаемость ограничена сильнее, чем на суше

Документ нужен как инженерная карта оптимизации, а не как постмортем.

## Что уже видно по текущему runtime

### Текущие живые значения из сцены

- `WorldProceduralScatterDirector.cellSize = 22`
- `WorldProceduralScatterDirector.radiusCells = 7`
- это даёт окно `15 x 15 = 225` клеток
- текущий `WorldChunkStreamingProfile`:
  - `fullSimulationRadius = 180`
  - `midSimulationRadius = 420`
  - `visualResidencyRadius = 900`

### Что это значит

- Мы не "гоняем 15 x 15 км", но мы действительно каждый rebuild думаем о слишком
  большом количестве клеток как о едином окне принятия решений.
- Для подводной игры это уже слишком жирный режим как baseline, потому что:
  - под водой игрок редко читает мелкий clutter дальше нескольких сотен метров;
  - системная стоимость идёт не только от рендера, а от sampling, rules, heatmap,
    reconcile, pooling, variant selection и generated geology triggers.

### Главный технический вывод

- Нынешняя архитектура слишком монолитна.
- Surface scatter, landmarks, cave markers, generated geology и fauna anchors ещё
  слишком сильно завязаны друг на друга.
- Поэтому "оптимизация" верхнего слоя слишком легко превращается в изменение
  состава мира.

## Production-ready цель

Нужна система, в которой одновременно выполняются четыре условия:

1. Мир не пропадает от локальной perf-правки.
2. CPU-нагрузка контролируется бюджетами, а не надеждой "авось placeholder дешёвый".
3. GC и runtime churn не растут линейно вместе с насыщением контента.
4. Рост сложности ассетов не ломает игру сразу при переходе с кубов на реальный арт.

## Главные направления оптимизации

## 1. Разделить смысловые слои runtime

Сейчас одна из ключевых проблем в том, что слишком много решений живёт рядом.

Нужно жёстко мыслить отдельными слоями:

- surface clutter
- cluster accents
- landmarks
- cave markers
- generated geology
- fauna anchors / threat zones

### Что это даст

- Оптимизация surface clutter перестанет иметь право выключать caves.
- Каждая подсистема получит собственные бюджеты и собственную диагностику.
- Перформанс начнёт локализоваться по владельцу, а не по "большому scatter pass".

## 2. Уйти от full rebuild как базового режима

Текущий режим слишком дорогой концептуально:

- игрок сместился по миру;
- система снова мыслит большим клеточным окном;
- много правил проверяется заново, даже если большая часть внутренней области не изменилась.

### Целевой подход

- incremental update
- border-in / border-out processing
- пересчёт только реально вошедших и вышедших областей
- повторное использование уже валидных низкоуровневых данных

### Что нельзя кэшировать без защиты

- готовые placements
- решения proxy/final
- cave-semantic outcomes
- всё, что уже связано с runtime state и генеративной логикой

### Что можно кэшировать безопасно

- seafloor sample
- slope
- curvature
- biome family
- zone kind
- heatmap input basis

## 3. Ввести жёсткие дистанционные бюджеты по типам контента

Подводный мир нельзя оптимизировать так же, как открытую наземную карту.

### Целевые зоны интереса

- `0–80 м`
  - full detail
  - final variants
  - interactables
  - gameplay-critical anchors
- `80–180 м`
  - proxy detail
  - reduced simulation
  - крупные силуэты
- `180–350 м`
  - landmarks only
  - без мелкого clutter
- `350+ м`
  - почти ничего мелкого
  - только то, что реально нужно для навигационной читаемости

### Принцип

- Не все типы контента имеют право жить на одной и той же дальности.
- Мелкий surface clutter и cave/geology не должны иметь одинаковые бюджеты.

### Что уже запрещено текущим опытом

- Нельзя динамически менять `radiusCells` scatter-директора на живом маршруте игрока
  через streaming budget path.
- Причина:
  - такая схема инициирует дополнительные invalidate/rebuild события прямо во время движения
  - формально окно становится меньше, но фактические hitch/spike могут стать хуже
- Следствие для roadmap:
  - оптимизация должна идти через удешевление sampling, reconcile и content budgets
  - а не через частое runtime-переключение самого размера scatter окна

## 4. Разнести budgets по семействам и ролям

Сейчас уже видно, что семейства ведут себя очень по-разному.

Например:

- `family.coral.low`
- `family.coral.branching`
- `family.kelp.tall`
- `family.cave.entrance`
- `family.landmark.spire`
- `family.creature.spawn.passive`

### Нужен не общий scatter budget, а разные budget-классы

- cheap ground filler
- cluster focal points
- structural landmarks
- cave-capable markers
- spawn anchors
- generated geology roots

### Что это даст

- Массовые дешёвые семейства можно агрессивнее резать по дальности и плотности.
- Редкие структурные семейства можно держать дольше, не распухая по числу объектов.
- Cave-capable families можно защищать от случайного "перекрытия" бюджетом кораллов.

## 5. Отдельно harden-ить cave / geology path

Generated geology не должна существовать как случайный побочный эффект surface scatter.

### Целевой контракт

- cave-capable placement выбран
- cave marker зарегистрирован
- geology request создан
- geology root создан
- voxel/generated mesh применён

На каждом шаге должны быть:

- counters
- trace
- инварианты

### Инварианты

- Если рядом cave marker, geology requests не могут быть нулевыми.
- Если вокруг игрока ожидается cave контент, `geoBindings/geoRoots/geoVoxels` не могут оставаться в нуле без отдельного объяснения.
- "Лучшая производительность" не считается успехом, если cave path умер.

## 6. Снизить стоимость sampling и rules evaluation

Placeholder уже показал, что проблема не только в рендере.

Стоимость сидит в:

- sampling seafloor
- biome / zone resolution
- heatmap evaluation
- rules matching
- candidate scoring
- reconcile path

### Что нужно сделать

- сделать sampling-layer дешевле и переиспользуемее
- уменьшить количество ненужных rule checks на клетку
- рано отсекать нерелевантные families
- не тащить все типы контента через одинаково тяжёлую стадию

### Production-ready принцип

- сначала дешёвый coarse filter
- потом только более дорогой semantic evaluation

## 7. Убрать runtime pool expansion как нормальный путь

Pool misses на placeholder уже тревожный сигнал.

### Почему это плохо уже сейчас

- даже placeholder-объекты дают churn
- при real assets это станет больнее:
  - больше mesh memory
  - больше renderer state
  - больше material cost
  - больше collider/setup cost

### Цель

- горячие семейства не должны системно расширять пул на живом маршруте игрока
- warmup должен считаться по реальному runtime спросу
- warmup budgets должны быть class-aware, а не одинаковыми для всего

## 8. Удержать zero-GC mindset в hot paths

Даже если главная боль сейчас не в одной "утечке", recurring GC spikes уже сами по
себе недопустимы для целевого железа.

### Что важно

- rebuild path не должен аллоцировать как попало
- reconcile path не должен плодить временные структуры
- generated geometry path должен максимально переиспользовать память
- pooling должен уменьшать churn, а не переносить его в другой участок кадра

### Практический ориентир

- hot gameplay path стремится к `0 B`
- редкие небольшие всплески допустимы
- многомегабайтные регулярные spikes в обычном движении по миру недопустимы

### Важная оговорка по честной диагностике

- Диагностический код сам не должен производить мусор, который потом выглядит как "игровой GC".
- Уже подтверждён отдельный анти-паттерн:
  - renderer ownership audit, запускаемый прямо на GC spikes, создавал собственные аллокации
  - это искажало картину и делало trace менее честным
- Следствие:
  - любые тяжёлые audits должны быть либо rate-limited, либо запускаться вручную, либо висеть только на редких batch spikes
  - profiling path должен подчиняться тем же правилам zero-GC, что и gameplay path, иначе он портит измерение

## 9. Разделить CPU-диагностику по владельцам

Нельзя дальше жить только с общей картиной "scatter тормозит".

Нужно отдельно видеть:

- sample cost
- rule evaluation cost
- candidate sort / selection cost
- reconcile cleanup cost
- reconcile spawn cost
- fauna publish cost
- cave marker creation cost
- geology request scheduling cost
- actual voxel mesh build cost

### Цель

- каждый spike должен быстро привязываться к конкретному владельцу
- нельзя снова уходить в ситуацию, где "ускорили что-то" и незаметно убили другой слой

## 10. Перевести world runtime на regression gate

Нужен не разовый ручной успех, а повторяемая инженерная проверка.

### Минимальный маршрут прогона

- старт на поверхности
- движение по surface clutter зоне
- уход в область cave-capable контента
- вход в область generated geology
- возврат

### Что сохраняем на каждый прогон

- trace file
- консоль
- counters по scatter
- counters по geology
- pool misses
- render stats

### Когда прогон считается провальным

- caves/geology исчезли
- `voxel.reconcile requests = 0` в ожидаемой зоне
- surface perf "улучшилась", но контент обеднён
- появились новые first-party runtime/compile errors

## Порядок выполнения работ

## Этап A. Восстановление честного baseline

- вернуть корректность мира
- подтвердить, что caves/geology реально живы
- зафиксировать baseline counters

## Этап B. Guardrails

- добавить инварианты
- добавить диагностику по cave/geology path
- сделать так, чтобы регресс ловился сразу

## Этап C. Архитектурное разделение

- ослабить связность между surface scatter и cave/geology
- начать разносить budgets по ролям контента

## Этап D. Incremental runtime

- переводить rebuild на delta-update
- кэшировать только безопасные низкоуровневые данные

## Этап E. Hard optimization

- снижать sampling cost
- снижать rules cost
- снижать reconcile cost
- добивать pooling и GC
- ужимать дальние budgets под подводную читаемость

## Что считаю правильной стратегией прямо сейчас

- Не делать "ещё одну смелую микрооптимизацию" в монолитном директоре.
- Сначала восстановить честный baseline мира.
- Параллельно уже думать как лид:
  - где разрезать систему,
  - какие budgets переводить на distance bands,
  - какие контент-классы должны жить отдельно,
  - что можно безопасно кэшировать,
  - где мы обязаны иметь guardrails.

## Ближайшие рабочие цели

- подтвердить восстановление cave path на честном прогоне
- зафиксировать baseline counters для scatter/geology
- определить первый безопасный срез на архитектурное разделение:
  - либо cave markers отделяем от surface clutter,
  - либо сначала режем rebuild на incremental border update
- после этого уже делать следующую волну реальной оптимизации, а не гадания
