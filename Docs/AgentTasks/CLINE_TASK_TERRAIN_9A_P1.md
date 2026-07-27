Проект: C:\hades\Hecton8 (Unity 6000.5.0f1 URP, branch main)
ПРАВИЛА: одна функция за раз, git commit после каждого шага, только git -C c:\hades\Hecton8 <команда>, только per-file git add.

---

P1 БАГ (Террейн, Раздел 9a): Потеря точности float на больших координатах.
На X≈777 000 м 12-метровые дифф-пробы округляются в один float, из-за чего возникают плоские/зебра-пятна на террейне.
Файл: `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs`
Строка: ~622 (`float2 warpedPos = (float2)warpedPosD;`)

Затронуты термы: `mBase`/`billowMountains` (~726-728), `tilt` (~794), `microGravel` (~1174).

ЗАДАНИЕ:
1. Прочитай файл `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs`. Найди объявление `warpedPos` и логику `warpedPosD`. Учти, что нужно вычитать `chunkOriginAup` (или аналогичную локальную привязку) из `warpedPosD` в `double` ДО высокочастотных термов, и кастовать в `float` только после этого вычитания, чтобы вычисления происходили около нуля и ULP-точность не зависела от абсолютной позиции.
2. Изучи, как формируется `warpedPosD` и где применяется `warpedPos`.
3. Внеси хирургическую правку: обеспечь вычисление локальной позиции в `double` до каста во `float2`.
4. Сделай коммит:
`git -C c:\hades\Hecton8 add Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs`
`git -C c:\hades\Hecton8 commit -m "fix(terrain/9a): use double precision for local chunk origin math to prevent float loss at large coords"`

Приёмка: `warpedPos` кастуется во `float` только после вычитания `chunkOriginAup` (или аналога) в пространстве `double`.
