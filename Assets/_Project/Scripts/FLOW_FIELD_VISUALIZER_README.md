# Flow Field Visualizer

## Naznachenie

`FlowFieldVisualizer` — redaktorskiy gizmo-instrument dlya prosmotra polya techeniy v stsene.
On risuet vyborku po setke poverh:

- globalnogo techeniya iz `HectonFluidEngine` / `CurrentManager`
- lokalnyh authored-obemov `CurrentVolume`

Instrument nuzhen dlya nastroyki vody, proverki lokalnyh current volumes i bystroy
diagnostiki napravleniya/sily potoka pryamo v Scene View.

## Chto umeet

- vyborka po pryamougolnoy setke `AreaSize` x `GridResolution`
- stili otrisovki `Arrows`, `Lines`, `Cones`, `Dots`
- tsvetovaya kodirovka sily potoka
- filtratsiya slabyh znacheniy cherez `CullWeakFlows` + `MinFlowStrength`
- podpisi sily v m/s cherez `ShowForceLabels`
- optsionalnyy async/job-pereschet dlya bolshih setok
- profili nastroek cherez `FlowFieldProfile`

## Kak rabotaet

1. Komponent visit v stsene i risuet gizmos tolko v `OnDrawGizmosSelected`.
2. Pri izmenenii nastroek vizualizator pomechaet kesh kak dirty.
3. Pri sleduyuschem draw on pereschityvaet grid-pozitsii i flow vectors.
4. Dlya krupnyh setok mozhet zapuskat job i zavershat ee cherez editor update.
5. Job/Burst-put uvazhaet flagi istochnikov:
   - pri `ShowGlobalCurrent = false` globalnyy phantom current ne podmeshivaetsya;
   - pri otsutstvii `HectonFluidEngine` lokalnye `CurrentVolume` vse ravno mogut schitatsya cherez job-put.

## Klyuchevye nastroyki

- `AreaSize`: razmer oblasti vyborki v metrah.
- `GridResolution`: plotnost vyborki po X/Z.
- `SampleHeight`: Y-offset otnositelno obekta vizualizatora.
- `MaxGridResolution`: zhestkiy clamp protiv slishkom tyazhelyh setok.
- `AsyncThreshold`: s kakogo chisla tochek imeet smysl job-put.
- `AsyncTimeout`: posle kakogo vremeni job prinuditelno zavershaetsya na main thread.
- `ShowGlobalCurrent`: uchityvat globalnoe phantom-techenie.
- `ShowLocalCurrents`: uchityvat `CurrentVolume`.
- `OnlySelectedVolumes`: ogranichit raschet spiskom `SelectedVolumes`.

## Profili

`FlowFieldProfile` hranit serializuemyy nabor parametrov vizualizatora.

Tipovoy stsenariy:

```csharp
FlowFieldProfile profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
profile.CaptureFrom(visualizer);
profile.ApplyTo(visualizer);
```

Cherez editor menu mozhno sozdat asset profilya:

- `Hecton/Tools/Create Flow Field Profile`

Menyu ispolzuet unikalnyy asset path i ne perezapisyvaet suschestvuyuschiy profil.

## Ogranicheniya i zamechaniya

- Instrument redaktorskiy; on ne prednaznachen dlya runtime HUD/FX.
- `UseParticleEffects` goditsya tolko dlya vizualnogo preview v redaktore i mozhet
  bystro zaspamit stsenu pri plotnoy setke.
- Esli `HectonFluidEngine` otsutstvuet, vizualizator vse ravno prodolzhit rabotat
  po lokalnym `CurrentVolume`.
- Preview-particles schitayutsya vremennymi editor-resursami i polnostyu ochischayutsya
  pri otklyuchenii komponenta, chtoby ne ostavlyat hidden objects v stsene.
- Vysokie razresheniya setki vse ravno dorogie: job-put ubiraet friz, no ne delaet
  raschet besplatnym.

## Svyazannye fayly

- `Assets/_Project/Scripts/FlowFieldVisualizer.cs`
- `Assets/_Project/Scripts/FlowFieldProfile.cs`
- `Assets/_Project/Scripts/CurrentVolume.cs`
- `Assets/_Project/Scripts/Editor/FlowFieldVisualizerEditor.cs`
- `Assets/_Project/Scripts/Editor/FlowFieldVisualizerTests.cs`
