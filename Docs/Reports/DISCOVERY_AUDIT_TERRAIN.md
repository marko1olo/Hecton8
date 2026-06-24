# Discovery Audit: Terrain and Architectural Rules

## ФАЗА 1: Координаты и Геометрия (Калейдоскоп и Шипы)

### [ПРОБЛЕМА]
1. **Зеркальный Калейдоскоп (MacroView.png):** Использование `math.abs(wrapX - period)` создает Triangle Wave. Это приводит к тому, что шум зеркально отражается по осям X и Z. Ландшафт выглядит как калейдоскоп, а не как непрерывный мир.
2. **Инфернальные Шипы (CaveEntrance.png):** При использовании `StrataShelvingStrength` в `ProceduralCaveSdfCarveJob` идет *сложение* плотности (density) в SDF там, где strataFrac равна нулю. Если strataRestore слишком велико, оно выталкивает плотность в огромный плюс, образуя гигантские шипы вместо гладких пещерных полов.

### [ДОКУМЕНТ/КОД ЛОКАЛИЗОВАН]
- `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs` (строка 334 и 150).
- `Assets/_Project/Scripts/World/WorldProceduralCaveSdfJobs.cs` (строка 144-147).

### [МОЯ ОШИБКА]
Вместо того чтобы сделать настоящий бесшовный шум (например, сэмплинг 4D-шума по тору через `sin`/`cos`, или просто использовать большой непериодический сдвиг), я применил жесткий фолдинг (triangle wave) координат. Это грубое нарушение, так как шум перестает быть направленным и зеркалится. В SDF я допустил неконтролируемое прибавление плотности (создание массы) вместо вычитания (carving).

### [ПЛАН ИСПРАВЛЕНИЯ]
- **Координаты:** В `WorldMacroGeologyFields.cs` убрать `math.abs(wrapX - period)`. Чтобы шум был бесшовным и не зеркальным, мы либо используем цилиндрический маппинг: `angle = (absoluteX / period) * 2 * PI`, `nx = cos(angle), ny = sin(angle)`, либо используем встроенный в Unity/Burst `noise.snoise` с параметром period, либо просто берем безопасный `fmod` с иррациональным шагом (как в `ProceduralCaveSdfCarveJob`: `Fmod(absX, 6627.0)`), что достаточно для огромного бесшовного мира без визуальных стыков в пределах игровой зоны.
- **SDF:** В `WorldProceduralCaveSdfJobs.cs` ограничить добавление плотности: `strataRestore` не должно превышать значение, которое выводит плотность в "камень", либо использовать `math.min(newDensity, currentDensity)` для гарантированного вычитания (carve) и ограничения восстанавливаемой массы полов.

## ФАЗА 2: PBR Материалы и Terrain (Отвал Текстур)

### [ПРОБЛЕМА]
**Отвал PBR и Текстур (CanyonView.png):** Каньон серый и плоский.

### [ДОКУМЕНТ/КОД ЛОКАЛИЗОВАН]
- `Assets/_Project/Scripts/World/HectonTerrainMaterialInjector.cs`
- `Assets/_Project/Shaders/HectonTerrain.shader`
- `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`

### [ЦИТАТА ИЗ АРХИТЕКТУРЫ]
"Material identity must be data-routed. No runtime Shader.Find, uncached string property churn, or uncontrolled material instancing in hot paths."

### [МОЯ ОШИБКА]
`HectonTerrainMaterialInjector.cs` создает инстанс материала для каждого чанка, назначает массивы текстур (`_Control`), но **не включает ключевые слова шейдера** (Keywords). В `HectonTerrain.shader` определены:
`#pragma shader_feature_local _TERRAIN_BLEND_HEIGHT`
`#pragma shader_feature_local _NORMALMAP`
`#pragma shader_feature_local _MASKMAP`
Без их программного включения через `_instancedMaterial.EnableKeyword("_NORMALMAP")` шейдер отбрасывает Normal и Height Blend вычисления, оставляя плоский серый albedo.

### [ПЛАН ИСПРАВЛЕНИЯ]
В `HectonTerrainMaterialInjector.ForceUpdate()` добавить вызовы:
```csharp
_instancedMaterial.EnableKeyword("_NORMALMAP");
_instancedMaterial.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
_instancedMaterial.EnableKeyword("_MASKMAP");
```
Это вернет детальный PBR-рендеринг, нормалмапы и правильный Biplanar blending, зависящий от высоты (Height Blend), к жизни.
