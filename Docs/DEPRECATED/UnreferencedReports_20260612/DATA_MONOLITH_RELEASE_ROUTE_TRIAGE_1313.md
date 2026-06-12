# DATA_MONOLITH_RELEASE_ROUTE_TRIAGE_1313

Date: 2026-05-25
Agent: 1313
Evidence class: STATIC_SOURCE_TOKEN_TRIAGE_NO_DOTNET_NO_UNITY
Source: `Docs/Reports/DATA_MONOLITH_RELEASE_ROUTE_SCAN_1313.json`

## Verdict

The global release parser purge is still rejected.

Raw candidate count: 281.
Strict blocking candidate count after triage: 262.
CSV state/UI/noise bucket: 19.

## Strict Blocking Breakdown

- CSV callable method declarations: 121.
- CSV callable invocations: 100.
- Managed text/file/json/split operations: 41.

Managed file/parser breakdown:

- `managedTextFileRead`: 22.
- `managedWholeFileByteRead`: 7.
- `managedTextStreamReader`: 6.
- `managedJsonDeserialize`: 5.
- `managedStringSplit`: 1.

## Highest-Pressure CSV Route Files

- `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs`: 9.
- `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs`: 8.
- `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`: 8.
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`: 6.
- `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`: 6.
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`: 5.
- `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs`: 5.
- `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`: 5.
- `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs`: 5.
- `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs`: 5.

## Highest-Pressure Managed File/JSON Files

- `Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs`: 4.
- `Assets/_Project/Scripts/Gameplay/Combat/StatusEffectsEditorFacade.cs`: 2.
- `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`: 2.
- `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs`: 2.
- `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`: 2.
- `Assets/_Project/Scripts/Gameplay/Combat/ArmorPenetrationEditorFacade.cs`: 2.
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`: 2.
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`: 2.
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs`: 2.
- `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs`: 2.

## Noise Bucket Examples

The 19 non-blocking/noise entries are not counted as strict parser invocations by this triage. They still indicate CSV concepts in production files and should be fenced or renamed during domain migration.

- `CsvReloadVersion` counters.
- Runtime UI button labels that mention CSV loaders.
- Pending reload flags such as `_pendingCsvReload`.

## Scanner Rule Patch

`OOP_StaticData_Scanner` and `H8DataMonolithReleaseBuildGate` were tightened after this triage: callable CSV route detection now uses generic `Csv + parser verb` matching, so names like `LoadQualityProfilesCsv` and `LoadLightingProfilesCsv` are no longer dependent on narrow hard-coded route names.

## Not Fixed

The 262 strict blocking candidates are cross-domain migration work. Editing them blindly from 1313 would violate domain ownership. The release gate must continue to fail until those domains either move authoring parsers behind editor-only paths or consume baked binary Data Monolith sections.
