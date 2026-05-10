Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HUD V4 Tuning

## Latest Active Left-Block Knobs

The current left vitals block is no longer the failed slanted-bar version.
It is now a compact radial-gauge cluster on `HUD_V4_CanvasRoot`.

Object:
- `Suit_HUD_Canvas`

Component:
- `SuitHUDV4CanvasOverlay`

Relevant fields:
- `gaugeClusterOffset`
- `gaugeClusterSize`
- `gaugeColumnSpacing`
- `gaugeRingSize`
- `gaugeRingThickness`
- `gaugeIconSize`
- `gaugeValueOffsetY`
- `gaugeLabelOffsetY`

Symptom -> what to turn:
- gauges overlap each other:
  - increase `gaugeColumnSpacing`
  - if needed increase `gaugeClusterSize.x`
- gauges are too small:
  - increase `gaugeRingSize`
- rings are too thick / visually dirty:
  - decrease `gaugeRingThickness`
- number is off-center inside the ring:
  - tweak `gaugeValueOffsetY`
- label under ring collides or sits too low:
  - tweak `gaugeLabelOffsetY`
- whole left block sits too far into screen:
  - decrease `gaugeClusterOffset.x`
- whole left block sits too low/high:
  - tweak `gaugeClusterOffset.y`

Rabochiy HUD: `--- UI ---/Suit_HUD_Canvas/HUD_V4_CanvasRoot`

Komponent:
- [SuitHUDV4CanvasOverlay.cs](C:/hades/Hecton8/Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs)

## Chto krutit v inspektore

Obekt: `Suit_HUD_Canvas`

Osnovnoe:
- `overallScale`: obschiy masshtab vsego HUD
- `chromeAlpha`: sila verhney/nizhney/bokovoy vuali

Pozitsii blokov:
- `headerOffset`: verhniy zagolovok
- `telemetryOffset`: pravyy depth/temp/pressure blok
- `telemetrySize`: razmer pravogo bloka
- `gaugeClusterOffset`: levyy vitals-blok
- `gaugeClusterSize`: razmer levogo vitals-bloka
- `statusOffset`: nizhnyaya tsentralnaya stroka
- `reticleOffset`: tsentralnyy pritsel

Novyy bar-blok:
- `gaugeRowSpacing`: vertikalnyy shag mezhdu `OXYGEN / HEALTH / ENERGY`
- `gaugeBarWidth`: dlina slanted bar
- `gaugeBarHeight`: tolschina bar
- `gaugeIconSize`: razmer ikonki sleva
- `gaugeValueOffsetX`: naskolko chislo vyneseno vpravo ot bar
- `gaugeLabelOffsetX`: tonkaya podstroyka label/sub otnositelno bar

## Prakticheskie simptomy

Esli bars naezzhayut drug na druga:
- uvelichit `gaugeRowSpacing`
- pri neobhodimosti uvelichit `gaugeClusterSize.y`

Esli bars slishkom dlinnye i lezut v tsentr ekrana:
- umenshit `gaugeBarWidth`
- sdvinut `gaugeClusterOffset.x` blizhe k krayu

Esli chisla otorvany ot bars:
- umenshit `gaugeValueOffsetX`

Esli ikonki slishkom krupnye ili davyat na tekst:
- umenshit `gaugeIconSize`
- pri neobhodimosti podvinut `gaugeLabelOffsetX`

Esli levyy blok slishkom shumnyy:
- umenshit `chromeAlpha`
- umenshit `overallScale`

## Tekuschaya logika metrik

Levyy blok ispolzuet tolko realnye dannye:
- `OXYGEN` -> `HectonSurvivalSystem.Oxygen`
- `HEALTH` -> `HectonSurvivalSystem.Integrity`
- `ENERGY` -> `HectonSurvivalSystem.Energy`

Ne ispolzuetsya:
- `Food`
- `Water`

Prichina:
- v tekuschem gameplay-kode etih metrik net
- risovat ih seychas bylo by feykom
