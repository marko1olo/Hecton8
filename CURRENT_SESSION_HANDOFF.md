# CURRENT SESSION HANDOFF

Статус: актуальное саммари для нового диалога.  
Дата: 2026-04-01

## Читать первым

Этот файл нужен, чтобы новый диалог не начинал работу с нуля.

Здесь сохранено:
- как со мной надо работать
- как отвечать пользователю
- что уже реально сделано
- что реально проверено в Unity
- что сейчас является правильной следующей целью

---

## Жёсткие правила общения с пользователем

- Всегда писать простым и понятным русским языком.
- Не использовать жаргон без перевода.
- Не писать `family` без перевода.
- Вместо этого писать:
  - `категория объектов`
  - `тип наполнения`
  - `большой участок воды`
  - `общий профиль мира`
- Всегда объяснять по схеме:
  - `Что сделал`
  - `Что это значит простыми словами`
  - `Что это даёт в игре`
  - `Что проверил`
  - `Что осталось проблемой`
- Всегда быть честным.
- Не врать про Unity-проверку, если она реально не прогонялась.
- Не топтаться на месте.
- Делать сразу как под финальный продукт, enterprise / AAA уровень.
- Если место слабое или решение плохое, говорить это прямо.
- Не перегружать ответ пустой “умной” терминологией.

---

## Личные установки пользователя, которые нельзя терять

- Нужен не прототип ради прототипа, а готовый коммерческий продукт.
- Стиль проекта: `Master Grade`, `Enterprise level`, `AAA feeling`.
- Это не коридорная игра и не набор уровней с боссами.
- Это большая подводная песочница с памятью места, интересом исследования, риском и наградой.
- Мир должен быть:
  - живым
  - густым
  - интересным
  - масштабным
  - оптимизированным
- Нужно не “умно на бумаге”, а реально работающее наполнение мира.
- Пользователь не любит, когда агент останавливается слишком часто.
- Нужно работать проактивно и самостоятельно, если нет скрытого опасного решения.

---

## Важные факты о мире игры

- Размер карты: `15 x 15 км`.
- Большой не-донный слой мира: примерно `6 x 8 км`.
- Это огромная площадь воды.
- Значит:
  - существ должно быть сильно больше
  - жизнь мира нельзя строить только вокруг игрока коротким кольцом
  - нужны чанки и слои стриминга
  - причём не только для фауны

Пользователь отдельно зафиксировал:
- чанки нужны не только для существ
- чанки должны работать для:
  - LOD
  - флоры
  - построек
  - обломков
  - ресурсов
  - существ
  - крупных угроз

Это уже выбрано как правильная цель.

---

## Что уже реально сделано по миру

### 1. Вода и биомы

Сделан верхний слой “характера воды”:
- `9` типов воды
- они уже не в жёстком коде, а в data-driven профилях

Сделан слой матричных биомов:
- `108` биомов
- у биомов есть своя память места
- биомы умеют влиять на:
  - средний слой наполнения
  - крупные акценты
  - настроение фауны

Сделаны отчёты:
- [PROCEDURAL_WATER_PATTERN_REPORT.md](C:/hades/Hecton8/PROCEDURAL_WATER_PATTERN_REPORT.md)
- [PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md](C:/hades/Hecton8/PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md)
- [PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md](C:/hades/Hecton8/PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md)

### 2. Procedural world fill

Сделан рабочий proxy-слой мира:
- природный фон
- карманы
- крупные ориентиры
- руины
- точки спавна

Есть field-driven preview и scatter-слой.

Главные документы:
- [PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md](C:/hades/Hecton8/PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md)
- [Что_и_как_исправляем_—_живой_план.md](C:/hades/Hecton8/Что_и_как_исправляем_—_живой_план.md)

### 3. AI существ

Сделан сильный базовый мозг существ:
- шум
- свет
- память
- расследование
- предупреждение
- преследование
- ложный заход
- защита территории
- защита гнезда
- помощь соседей
- совместная охота
- разные сценарии крупных угроз

Есть парк видов:
- мирные
- территориальные
- хищники
- левиафаны

Сделаны документы:
- [AI_ENTERPRISE_LAYER_PLAN.md](C:/hades/Hecton8/AI_ENTERPRISE_LAYER_PLAN.md)
- [AI_CREATURE_ROSTER_ENTERPRISE.md](C:/hades/Hecton8/AI_CREATURE_ROSTER_ENTERPRISE.md)
- [AI_CREATURE_ROSTER_REPORT.md](C:/hades/Hecton8/AI_CREATURE_ROSTER_REPORT.md)
- [AI_FAUNA_ARCHETYPE_REPORT.md](C:/hades/Hecton8/AI_FAUNA_ARCHETYPE_REPORT.md)
- [AI_FAUNA_WORLD_INTEGRATION_REPORT.md](C:/hades/Hecton8/AI_FAUNA_WORLD_INTEGRATION_REPORT.md)

### 4. AI реально посажен в мир

Уже было реально подтверждено через Unity:
- профилей видов: `22`
- наборов фауны по биомам: `108`
- биомов без мирной жизни: `0`
- биомов без угроз: `0`
- профилей видов без префаба: `0`

Важный баланс, уже выбранный правильно:
- левиафаны не должны сидеть “в каждой дыре”
- обычные поздние резервные биомы очищены от лишних левиафанов
- пара левиафанов ближе к поверхности допустима, если место огромное и запоминающееся

Последнее подтверждённое состояние по большим угрозам:
- левиафан-записей: `14`
- тяжёлые хищники вместо левиафанов стоят в части тяжёлых мест
- обычные резервные биомы с левиафанами: `нет`

### 5. Общий чанковый мир

Это теперь уже не идея, а живая цель с кодовым фундаментом.

Сделан общий профиль мира:
- [WorldChunkStreamingProfile.asset](C:/hades/Hecton8/Assets/_Project/Data/World/Streaming/WorldChunkStreamingProfile.asset)

Ключевые параметры:
- мир: `15000 м`
- чанк: `192 м`
- внутренняя клетка: `64 м`
- большая зона: `768 м`

Сделаны документы:
- [WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md](C:/hades/Hecton8/WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md)
- [AI_FAUNA_CHUNK_STREAMING_PLAN.md](C:/hades/Hecton8/AI_FAUNA_CHUNK_STREAMING_PLAN.md)

На общий профиль мира уже реально посажены:
- существа
- ресурсы
- бюджеты scatter и коллайдеров
- стриминг директор
- дистанции world slice / LOD-кольца

Файлы, которые уже реально переведены на общий профиль мира:
- [FaunaDirector.cs](C:/hades/Hecton8/Assets/_Project/Scripts/FaunaDirector.cs)
- [ScavengePopulator.cs](C:/hades/Hecton8/Assets/_Project/Scripts/ScavengePopulator.cs)
- [ScatterBudgetController.cs](C:/hades/Hecton8/Assets/_Project/Scripts/ScatterBudgetController.cs)
- [WorldStreamingDirector.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldStreamingDirector.cs)
- [WorldSliceDirector.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldSliceDirector.cs)
- [WorldRuntimeBootstrapAuthoring.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs)
- [WorldStreamingWiringValidator.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/WorldStreamingWiringValidator.cs)

---

## Что реально было проверено в Unity

Последнее честно подтверждённое состояние:

- compile clean после фиксов save system
- console errors: `0`
- новые ошибки от последней волны чанкового мира не появились

Реально были прогнаны:
- `Build World Chunk Streaming Profile`
- `Rebuild World Runtime Stack`
- `Validate World Streaming Wiring`
- AI authoring и отчёты по фауне

Реально подтверждено:
- `FaunaDirector` использует общий профиль мира
- `ScavengePopulator` использует общий профиль мира
- `ScatterBudgetController` использует общий профиль мира
- `WorldStreamingDirector` использует общий профиль мира
- `WorldSliceDirector` использует общий профиль мира

Подтверждённые runtime-значения:
- `FaunaDirector._debugRuntimeChunkSize = 192`
- `FaunaDirector._debugRuntimeMacroZoneSize = 768`
- `FaunaDirector._debugRuntimeLargeThreatSpawnOuter = 420`
- `FaunaDirector._debugRuntimeLargeThreatCullDistance = 900`

Ещё важный факт:
- `MapMagicBridge.IsAvailable = false` в текущем preview-состоянии сцены
- значит часть world preview всё ещё живёт через fallback-путь

---

## Что уже было исправлено отдельно

### Save system compile blockers

Были реально исправлены:
- [SaveManager.cs](C:/hades/Hecton8/Assets/_Project/Scripts/SaveManager.cs)
- [SaveMetadata.cs](C:/hades/Hecton8/Assets/_Project/Scripts/SaveMetadata.cs)

Смысл:
- сохранения снова компилируются нормально
- есть совместимость со старыми UI / editor вызовами
- работают реальные slot file path
- включена миграция save data при загрузке

Это важно не потерять.

---

## Что ещё НЕ сделано

Вот честный список, который нельзя в новом диалоге объявлять “готовым”:

- флора ещё не посажена массово на общий чанковый профиль мира
- обломки ещё не посажены массово на общий чанковый профиль мира
- постройки ещё не посажены массово на общий чанковый профиль мира
- большие угрозы ещё не разведены по всем местам до финальной режиссуры встречи
- нет полного финального play-pass по огромной карте
- нет финальной сцены bootstrap / main menu / sandbox блока
- save system ещё не закрыт как полный продакшн-цикл для всего мира

---

## Что сейчас является ПРАВИЛЬНОЙ следующей целью

Следующая большая цель уже выбрана:

### Не отдельные мелкие фиксы, а единый стриминг большого мира

Надо продолжать не в сторону новых абстракций, а в сторону:
- общего чанкового мира
- единой логики стриминга
- единой логики дальних и ближних слоёв

Правильный следующий порядок:

1. Посадить на общий профиль мира ещё и:
- флору
- обломки
- постройки

2. Довести большие угрозы до режима больших участков мира:
- не “спавн по маленькому чанку”
- а хозяева больших зон

3. После этого уже честно смотреть:
- где мира мало
- где слишком пусто
- где слишком шумно
- где нужно больше жизни
- где нужно меньше тяжёлых угроз

---

## Ключевая инженерная мысль, которую нельзя потерять

Для карты `15 x 15 км` нельзя делать мир только по схеме:
- “игрок плывёт, рядом что-то досыпаем”

Нужна взрослая схема:

- дальняя жизнь как данные
- дальние дешёвые видимые слои
- средняя симуляция
- ближняя полная симуляция

И это должно работать не только для существ, а для всего:
- LOD
- флора
- ресурсы
- обломки
- постройки
- существа
- крупные угрозы

Это уже выбрано как основа final-версии.

---

## Что читать в новом диалоге кроме этого файла

Сначала:
- [Что_и_как_исправляем_—_живой_план.md](C:/hades/Hecton8/Что_и_как_исправляем_—_живой_план.md)
- [WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md](C:/hades/Hecton8/WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md)
- [AI_ENTERPRISE_LAYER_PLAN.md](C:/hades/Hecton8/AI_ENTERPRISE_LAYER_PLAN.md)
- [AI_FAUNA_WORLD_INTEGRATION_REPORT.md](C:/hades/Hecton8/AI_FAUNA_WORLD_INTEGRATION_REPORT.md)

Потом по необходимости:
- [PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md](C:/hades/Hecton8/PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md)
- [PROCEDURAL_WATER_PATTERN_REPORT.md](C:/hades/Hecton8/PROCEDURAL_WATER_PATTERN_REPORT.md)
- [PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md](C:/hades/Hecton8/PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md)
- [PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md](C:/hades/Hecton8/PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md)

---

## Короткая стартовая формула для нового диалога

Если новый диалог начинается с нуля, правильная короткая рамка такая:

- Мы делаем большую подводную песочницу enterprise-уровня.
- Мир огромный, значит всё должно жить через общий чанковый стриминг.
- Уже сделаны:
  - вода и биомы
  - procedural fill
  - сильный AI
  - посадка AI в мир
  - общий профиль мира для части систем
- Сейчас главный следующий шаг:
  - посадить на общий профиль мира остальные большие слои мира
  - и довести это до финального цельного стриминга

