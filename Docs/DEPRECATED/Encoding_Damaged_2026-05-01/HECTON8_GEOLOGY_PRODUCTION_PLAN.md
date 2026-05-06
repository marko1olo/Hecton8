# HECTON-8 Geology Production Plan
Date: 2026-05-01
Status: DEPRECATED

## Коротко
Готовые geology-префабы не трогаем и не удаляем. Они остаются в проекте как fallback.

Основной production-path:
- кодом генерятся новые geology meshes
- из них собираются новые final prefabs
- на них назначается существующий triplanar rock shader/material
- family assets начинают ссылаться на новые generated finals
- старые готовые префабы остаются запасным путём и не ломаются

## Жёсткие правила
- Не переписывать и не удалять `Forest_Rock_Shelf`, `Mossy_Forest_Rock`, `Nordic_Beach_Rock`, `Nordic_Beach_Rock_Formation`, `Rock_Skala`.
- Не считать старые готовые префабы основным путём.
- Не писать validator/report/status markdown.
- Не писать editor tool, который только проверяет.
- Не делать runtime-first. Цель: заранее сгенерённые production assets в проекте.
- Не делать UV workflow. Triplanar обязателен.
- Не делать placeholder meshes.
- Если не хватает внешнего ресурса, остановка только по blocked-формату.

## Owner-система
### Основные владельцы
- `WorldProceduralGeologyFinalAuthoring`
  Главный authoring owner. Создаёт реальные mesh assets и prefab assets.
- `WorldProceduralGeologyProfileAuthoring`
  Держит profiles для всех geology categories.
- `WorldProceduralFinalVariantAuthoring`
  Привязывает generated finals к family assets.
- `WorldProceduralProxyAuthoring`
  Остаётся владельцем family/rule/proxy bootstrap. Через него добавляется shelf/cliff family.

### Новый файл
- `WorldProceduralGeologyMeshBuilder`
  Чистый production builder, который строит mesh data для geology finals.

## Что именно создаётся
### Категории
- `family.rock.small_floor`
- `family.rock.cluster.medium`
- `family.rock.arch.large`
- `family.cave.entrance`
- `family.landmark.spire`
- `family.rock.shelf.large`

### Количество generated finals
- Small floor rocks: 10
- Medium clusters: 10
- Shelf / cliff large: 8
- Rock arch large: 6
- Cave entrance: 6
- Landmark spire: 6

### Что есть у каждого generated asset
- LOD0 mesh
- LOD1 mesh
- LOD2 mesh
- collider setup
- assigned triplanar rock material
- saved prefab in project
- linked final variant in family asset

## Где лежат production assets
### Meshes
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/`

Папки:
- `RockSmallFloor`
- `RockClusterMedium`
- `RockShelfLarge`
- `RockArchLarge`
- `CaveEntrance`
- `LandmarkSpire`

### Prefabs
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/`

Старые готовые префабы остаются на месте. Новые generated finals лежат рядом, отдельными prefab assets.

### Materials
- один общий generated geology material на базе `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- либо набор category materials на том же shader path
- новый shader не создаётся

## Как работает mesh builder
### Общий принцип
Builder не делает один базовый силуэт. Он строит сложную форму сразу из нескольких процедурных масс и деталей.

На каждый объект:
- главный объём
- вторичные массы
- сколы
- полки
- трещинные рёбра
- выступы
- асимметрия
- шумовая деформация
- layered erosion look

### По категориям
#### Small floor rocks
- низкие, тяжёлые, читаемые сверху
- не превращаются в одинаковые булыжники
- 10 разных форм
- контроль по вершинам: не раздувать сверх нужного

#### Medium clusters
- 2-5 связанных масс
- cover silhouette
- разные оси наклона
- читаемая масса без мусорной мелочи

#### Shelf / cliff large
- большие уступы
- слоистые свесы
- cliff face
- shelf extension
- тяжёлая боковая масса
- отдельная production family, не суррогат arch/cluster

#### Rock arch large
- две тяжёлые опоры
- верхний мост
- асимметрия
- fracture detail
- underside detail
- 6 разных типов, не одна и та же арка

#### Cave entrance
- читаемый вход
- боковые губы
- верхний свод
- глубина входа
- внешний debris/seam
- несколько разных форм портала

#### Landmark spire
- сильный дальний силуэт
- массивная база
- сужение вверх
- вторичные выступы
- не превращать в простой столб

## LOD контракт
Для всех generated geology finals:
- 3 visible LOD
- thresholds: `0.6 / 0.15 / 0.04`
- `CrossFade` включён
- LOD1 режет среднюю мелочь
- LOD2 держит только главный силуэт

Отдельные правила:
- арка на LOD2 остаётся аркой
- cave entrance на LOD2 остаётся входом
- shelf/cliff на LOD2 остаётся уступом
- spire на LOD2 остаётся шпилем

## Коллизии
### Small floor
- 1-3 primitive colliders максимум

### Cluster medium
- 2-3 primitive colliders

### Shelf / cliff
- упрощённые коллизии под поверхность и край

### Arch
- коллизия не должна убивать проход под аркой

### Cave entrance
- коллизия не должна закрывать вход

### Spire
- грубая простая коллизия

Общее правило:
- не ставить `MeshCollider` на LOD0

## Материал и шейдер
Используется существующий:
- `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`

Что делается:
- создаётся managed generated geology material stack на этом shader path
- instancing включается там, где это нужно
- нового parallel shader pipeline нет
- UV unwrap нет

## Family / rule linkage
### Что меняется
- `family.rock.small_floor` переводится на generated finals
- `family.rock.cluster.medium` переводится на generated finals
- `family.rock.arch.large` получает несколько generated finals
- `family.cave.entrance` получает несколько generated finals
- `family.landmark.spire` получает несколько generated finals
- добавляется `family.rock.shelf.large`

### Что не меняется
- старые готовые prefabs остаются fallback
- proxy path остаётся
- существующие ручные большие prefabs не ломаются

## Порядок работы без распыления
### Этап 1
Сделать `WorldProceduralGeologyMeshBuilder`
- один builder
- все 6 categories
- генерация LOD0/1/2

### Этап 2
Переделать `WorldProceduralGeologyFinalAuthoring`
- убрать сборку geology из готовых камней как основной путь
- создавать mesh assets + prefab assets
- сразу назначать material + colliders + LODGroup

### Этап 3
Расширить `WorldProceduralGeologyProfileAuthoring`
- small
- cluster
- shelf
- arch
- cave
- spire

### Этап 4
Расширить `WorldProceduralFinalVariantAuthoring`
- все generated finals привязать к family assets
- старые ручные assets не удалять

### Этап 5
Расширить `WorldProceduralProxyAuthoring`
- добавить `family.rock.shelf.large`
- добавить rule для shelf/cliff geology
- оставить старое как fallback path

### Этап 6
Сгенерировать реальные project assets
Результат физически лежит в проекте:
- meshes
- prefabs
- materials if needed
- updated family links

## Что считается итогом работы
Сделано только если есть:
- реальные mesh assets в проекте
- реальные generated prefabs в проекте
- материал назначен
- family assets указывают на generated finals
- старые готовые prefabs остались как fallback
- shelf/cliff geology появился как production asset line

## Что остаётся PENDING VERIFICATION
- shader compile inside Unity
- фактический визуал в сцене
- collider behavior для arch/cave
- GPUI registration where needed
- MapMagic scatter activation where needed
- profiler/log evidence

## Зафиксированное решение
- существующие готовые geology prefabs не ломаем
- не заменяем их удалением
- они остаются fallback
- основной production путь: новые generated geology finals кодом
- scope не распыляется: один builder, один authoring path, шесть geology categories, заранее сохранённые assets
