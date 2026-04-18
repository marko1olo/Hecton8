**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# План задач C/HADES/HECTON8 (рабочий каталог `C:\hades\Hecton8`)

> Это основной трек задач для проекта Submerge (HECTON-8) на базе текущего репозитория. 
> Любое изменение задач должно фиксироваться в issue/PR как «done» после проверки на работоспособность.

## 1. Стратегия и цели проекта

- Сформировать замысел ключевого визуального стиля: Deep Sea Noir + научно-техническая тематика + атмосфера затонувшей базы.
- Обеспечить модульную сборку: графика + эффекты + сценарий + UI + оптимизация памяти на уровне 2ГБ VRAM.
- Основная связка middleware:
  - Crest (вода),
  - MapMagic (генерация ландшафта),
  - MicroSplat (орбазинг),
  - VLB (волны/дым),
  - GPU Instancer,
  - Odin Inspector,
  - Easy Save.
- Уделить внимание стабильности: тесты эскапирования высоты, логика слежения за трансформами, контроллер камеры, корректное отключение объектов при стриминге World (Storm-breaker).

## 2. Ближайшие итерации (минимальный MVP)

- Фаза 1: Визуальный сет Deep Sea Noir.
  - Финализировать цветовой набор, освещение, весенние профили постпроцесса, fog/volumetric.
  - Прототипировать главный мир: морское дно, руины, точечные источники света, скрипты переходов.

- Фаза 2: Игровая механика, база и интерфейсы.
  - Реализовать систему управления персонажем + PDA + инвентарь.
  - Добавить интерактивность объектов, сбор ресурсов, крафт ремесла.

- Фаза 3: Процедурная генерация уровня.
  - Задать сетку abyss/nodes, алгоритмы заполнения, повторы.

- Фаза 4: Производительность.
  - Низкополигонная LOD-ферма, кастомный culling, упрощённая геометрия для дальнего вида.

- Фаза 5: Подготовка релиза и требования.
  - Требования: 30 FPS на MX350, не более 2GB VRAM, приемлемые нагруженные сцены.

## 3. Техническая поддержка и качество

- Оптимизация VRAM, URP Volumetric, с учётом low/medium/high.
- Автоматические проверки состояния ассетов (Crest/MapMagic). Сделать workflow asset health check через PR.
- Отслеживать порядок вызовов Update/FixedUpdate/LateUpdate и проблемы GC.

## 3.1. Четвёртый столп из README (2)
- Уточнить точные target hardware constraints:
  - Texture max 2048x2048 для стен/ландшафта, 1024x1024/512x512 для пропсов.
  - Не более 2 GB VRAM.
  - Никакой tesellation, только Normal/Parallax.
  - Zero-GC в Update/FixedUpdate/LateUpdate.
  - Без LINQ/FindObjectOfType/GetComponent в горячих циклах.

## 3.2. Core Pillars (из README)
- Технологический уют, мегалофобия, тяжелый инжиниринг, мародерство.
- Проверить, что каждая задача соответствует этим принципам.

## 4. Управление задачами status-driven

- [ ] Развивать ядро feature:
  - Плавное наплывание (Aegir phases)
  - Tidal lock drift
  - Вес ресурсов/энергия
  - Аддитивный стриминг мира
  - Система бартерного PDA
  - Процедурная сеть abyss nodes

- [ ] Технический долг:
  - Автоматический генератор TODO в `task.md`.
  - Проверка CurrentVolume transform и корректного применения.

## 5. Процесс в работе

1. Каждый таск оформляется в issue и разбивается на подзадачи, задачи выполняются по очереди.
2. Команда кодеров соблюдает стандарты, ведёт комментарии и коды состояния.
3. Закрытие задачи по критерию: на MX350 30 FPS при 2GB VRAM, отсутствие багов, документирование.

---

## 6. Сравнение с README (2).md и недостатки

- README содержит детальный манифест, цели по CPU/GPU (MX350), архитектурные стандарты и структуру директорий, которые в основном НЕ отражены в старом файле задач. Надо добавить:
  - строжайшие гайдлайны по папкам `Assets/Plugins`, `Assets/_Project`, `Assets/Scenes`, `Assets/Scripts`.
  - правила работы с Additive Scene Loading (00_BOOTSTRAP, 01_MAIN_MENU, 02_HECTON_WORLD, XX_SANDBOX).
  - Prefab-centric workflow (запрет на правку сцены, использование Prefab Mode/Variants).
  - data-driven настройки (SO вместо хардкода) и Git протоколы (LFS, .gitignore, консоль ошибок).
- README даёт структуру Asset Stack (Crest, MapMagic, MicroSplat, VLB, Odin, Easy Save, Candice, Feel). Нужно в тасках отдельно проверить эти интеграции:
  - очистка демо-контента
  - фиксы (ForceIncludeInstancing, Sirenix PDB)
  - отключение тяжёлых модулей (Tessellation/Parallax в MicroSplat, лишние VLB устаревшие методы).

> Это фикс: `HADES_HECTON8_tasks.md` теперь синхронизирован с ключевыми пунктами из `README (2).md`. Проверить и дополнить остальные темы на следующем спринте.

## 7. Концепт геймплея (полный набор идей)

### 7.1. Core Gameplay Loop
- [ ] Исследование: зона 15x15 км, секции: The Spine, Drowned Factories, Abyssal Face, The Wound.
- [ ] Сбор ресурсов: лом, руда, органика, биолуминисценция.
- [ ] Обслуживание оборудования: тюнинг батискафа/систем; сжигание энергии (PDA, фонари, насосы).
- [ ] Выживание: управление кислородом, давлением, температурой, радиацией.
- [ ] Крафт и апгрейд: инструменты, броня, модули базы.
- [ ] Полная система ресурсов и крафта:
  - полный список сырья, биоматериалов, химии и промежуточных компонентов
  - data-driven `ItemData` для всех ключевых ресурсов
  - полноценные рецепты: сырьё -> компонент -> инструмент/апгрейд/модуль
  - реальные world-sources: лом, рудные узлы, биосбор, sealed caches
  - отказ от простого copper-only economy
  - опора на [RESOURCE_CRAFTING_FOUNDATION.md](C:/hades/Hecton8/RESOURCE_CRAFTING_FOUNDATION.md)
- [ ] Прогресс: сбора данных, восстановления ИИ, захвата новых зон.
- [ ] Риск: хищники, разгерметизация, MCU (взрывы), коллапсы.

### 7.2. Физика и управление (из README)
- [ ] Реализовать плавучую механику для батискафа + buoyancy (Crest + собственный код). 
- [ ] Обработка входа: WASD, прыжки, акселерация; с учётом инерции воды.
- [ ] Реакция на давление: параметры DepthExposure (0..1), модификаторы урона/шансов поломки.

### 7.3. Интерфейс и PDA (Hecton-OS)
- [ ] HUD/AR стиль: моноширинный шрифт, жесткие рамки, статические глитчи.
- [ ] Батарея/О2/Гидравлика/Температура -> визуальные кластеры.
- [ ] Exchange/barter system внутри PDA.

### 7.4. Базы и стриминг мира
- [ ] Additive Scene Loading: полноценный Bootstrapping.
- [ ] Стриминг чанков: лоды, culling, отключение компонентов вне зоны видимости.
- [ ] Система площадок для постройки: ресурсы, враги, защита.

### 7.5. Нейроагаенты и AI
- [ ] Интеграция Candice AI, поведение дронов и мутантов.
- [ ] Логика счастья/агрессии: реакция на шум, освещение.
- [ ] Возможность терраформирования в зоне ошибок.

### 7.6. Критически важные несделанные элементы
- [ ] Всех из README 0.1-0.3 пока надо формализовать в задачах, разбить на subtask.
- [ ] Не прописаны check-листы по Asset-стеку, включая урезку демо и конфиг в старых плагинах.
- [ ] Нет отдельного модуля для сцены Sandbox, он нужен для изоляции и тестов.
- [ ] Текущий файл пока не имеет связки с .kiro/specs/hecton8-enterprise-roadmap/tasks.md на детальном уровне (требуются ссылки/перенос).
- [ ] Профайл-метрики (FPS, GC, drawcalls) не заведены как достижимая цель в тасках.

### 7.7. Полный roadmap реализации (что реализовать)
- [ ] Инициализационный стек
  - [ ] Bootstrap + глобальные менеджеры GameManager, SaveSystem, AudioMixer, InputManager.
  - [ ] Additive Scene Loading: 00_BOOTSTRAP, 01_MAIN_MENU, 02_HECTON_WORLD, XX_SANDBOX.
  - [ ] Scene streaming: активная загрузка/выгрузка зон, чекпоинты.

- [ ] Core engine
  - [ ] Input System (Player + UI) + InputManager singleton + zero-GC callbacks.
  - [ ] Movement System: Rigidbody / CharacterController + вода/инерция.
  - [ ] PDA/UI System: баг-репорт, inventory, barter, система задач.
  - [ ] Survival System: O2, температура, давление, радиация, статусы.

- [ ] World generation
  - [ ] MapMagic integration + chunk streaming, biome masks.
  - [ ] Crest ocean/underwater renderer + buoyancy/wave intersection.
  - [ ] MicroSplat terrain shader + tris, LOD, без tessellation.

- [ ] AI & entities
  - [ ] Candice behavior trees для мутантов, дронов, NPC.
  - [ ] State machine для охраны, кражи, бегства.
  - [ ] Pooling enemy/spawner system + navigation mesh updates.

- [ ] Interaction & loot
  - [ ] Сбор лута: удар, резка, физика добычи.
  - [ ] Крафт и апгрейды: рецепты, материалы, UI.
  - [ ] Weight/encumbrance и связь с энергозатратами.

- [ ] Performance & tech
  - [ ] VRAM budget checks, texture atlas + compression.
  - [ ] URP renderer features: VLB, volumetric fog, shadow quality toggles.
  - [ ] Profiling benchmarks: FPS, memory, GC, draw calls.
  - [ ] Job System + Burst для сложных расчётов (шум, AI, физические сетки).

- [ ] Tools & workflow
  - [ ] Prefab workflow (variants, nested prefabs, no scene edits).
  - [ ] Git/LFS rules, CI pipeline на компиляцию + утилиты.
  - [ ] Документация: architecture, coding standards, profiling reports.

> По каждому пункту создаем отдельный issue, вешаем на спринт, закрываем по проверке на MX350.

