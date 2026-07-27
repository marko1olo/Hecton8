# ЗАДАЧА ДЛЯ CLINE / GPT-5.6-SOL — P0 БЛОКЕР: Коллизия пещер

**Проект:** `C:\hades\Hecton8` (Unity 6000.5.0f1 URP, branch `main`)
**Правила:** Одна структура/функция за раз. Git commit после каждого шага. Только `git -C c:\hades\Hecton8 <команда>`.

---

## КОНТЕКСТ

`HectonVoxelVolume.cs:1748` — метод `AssignColliderChunkBakeMesh` содержит буквально `return false`.
Это означает: коллайдер-меш пещер **никогда не публикуется**. Игрок проплывает сквозь все стены.

Вот цепочка дефекта:
- `GetColliderChunkBakeMesh` → `null` (`:1743`)
- `IsDeferredColliderChunkUploadReady` → `false` (`:1738`)
- `collider.sharedMesh = null` — единственная запись `sharedMesh` в файле (`:1982`)
- `ApplyChunkedColliderMeshesAsync` (`:14255-14301`) корректно считает `chunkTriangleIndices`, `bucketOffsets`, `bucketCounts`, `localRemap` — но commit-цикл (`:14309-14352`) **их не читает вообще**
- Корневой `MeshCollider` принудительно выключен (`:13795-13796`)
- Прокси-боксы игнорируют игрока: `Physics.IgnoreLayerCollision(Player/Vehicle/PlayerVehicle, VoxelProxy, true)` (`:7996-8001`)

---

## ЧТО НУЖНО СДЕЛАТЬ (строго по порядку)

### Шаг 1 — Прочитай файлы перед правкой
```
C:\hades\Hecton8\Assets\_Project\Scripts\World\VoxelVolume\HectonVoxelVolume.cs
```
Найди и прочти: `AssignColliderChunkBakeMesh` (`:1748`), `GetColliderChunkBakeMesh` (`:1743`), `IsDeferredColliderChunkUploadReady` (`:1738`), `ApplyChunkedColliderMeshesAsync` (`:14255-14352`).

### Шаг 2 — Реализовать `AssignColliderChunkBakeMesh`
Вместо `return false` — реализовать хранение меша:
```csharp
// примерная сигнатура (сверь с реальным кодом файла):
private bool AssignColliderChunkBakeMesh(int index, Mesh mesh)
{
    if (mesh == null || index < 0 || index >= _colliderChunkMeshes.Length)
        return false;
    _colliderChunkMeshes[index] = mesh;
    return true;
}
```
Найди поле `_colliderChunkMeshes` (или его аналог — grep по файлу). Если поля нет — объяви `Mesh[] _colliderChunkMeshes` рядом с другими chunk-полями и инициализируй в `Awake`/`Initialize`.

### Шаг 3 — Реализовать `GetColliderChunkBakeMesh`
```csharp
private Mesh GetColliderChunkBakeMesh(int index)
{
    if (index < 0 || index >= _colliderChunkMeshes.Length) return null;
    return _colliderChunkMeshes[index];
}
```

### Шаг 4 — Исправить `IsDeferredColliderChunkUploadReady`
Должен возвращать `true` когда меш есть. Найди реальную логику вокруг строки `:1738` и убери хардкод `return false`.

### Шаг 5 — Подключить меши в commit-цикле `ApplyChunkedColliderMeshesAsync`
Найди строки `:14309-14352`. Там должен быть цикл по чанкам — в нём нужно:
1. Собрать `Mesh` из `chunkTriangleIndices[bucketOffsets[i] .. +bucketCounts[i]]` с компактизацией вершин через готовый `localRemap`.
2. Установить explicit bounds из `state.BoundsCenterLocal` с флагом `MeshUpdateFlags.DontRecalculateBounds`.
3. Вызвать `Physics.BakeMesh(mesh.GetInstanceID(), false)` (non-convex).
4. Вызвать `AssignColliderChunkBakeMesh(i, mesh)`.

### Шаг 6 — Включить реальный MeshCollider
Найди строки `:13795-13796` где `MeshCollider` принудительно выключен. Раскомментируй или убери принудительный disable — он должен включаться когда `_bakeState == VoxelBakeState.Complete`.

### Шаг 7 — Git commit
```powershell
git -C c:\hades\Hecton8 add Assets/_Project/Scripts/World/VoxelVolume/HectonVoxelVolume.cs
git -C c:\hades\Hecton8 commit -m "fix(voxel/collider): implement AssignColliderChunkBakeMesh + wire commit-cycle — cave collision now publishes real MeshCollider"
```

---

## ПРИЁМКА (что считается успехом)
- `AssignColliderChunkBakeMesh` больше не возвращает `return false`
- `GetColliderChunkBakeMesh` возвращает реальный Mesh
- `IsDeferredColliderChunkUploadReady` не хардкодит `false`
- В commit-цикле `ApplyChunkedColliderMeshesAsync` создаётся Mesh из chunkTriangleIndices
- Код компилируется (проверь `dotnet build` или Unity batchmode если доступно)

## ЗАПРЕЩЕНО
- Трогать файлы, не связанные с задачей
- Делать `git add -A` (только per-file add)
- Фиктивная проверка ("должно работать") без реального кода
