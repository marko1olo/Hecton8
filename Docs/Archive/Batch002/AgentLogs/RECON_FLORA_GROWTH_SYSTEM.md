# RECON_FLORA_GROWTH_SYSTEM

STATUS: PENDING VERIFICATION

Scope: `Assets/_Project/Art/Materials/`
Heuristic: material path/name contains flora tokens (`flora`, `plant`, `kelp`, `sargassum`, `coral`, `algae`, `organic`, `mushroom`, `shroom`, `vine`, `grass`, `leaf`, `biolum`).
Allowed shader contract: `Hecton_IndirectVegetation` or shaders including `Hecton_CoreLit.hlsl` / named `Hecton8_CoreLit`.

## Summary

- Flora-like materials scanned: 21
- Contract-compliant materials: 9
- Non-compliant materials: 12

## Non-Compliant Materials

| Material | Shader | Shader Path |
| --- | --- | --- |
| `Assets/_Project/Art/Materials/MAT_SargassumFoamDamping.mat` | `Crest/Inputs/Foam/Sargassum Damping` | `Assets/_Project/Art/Shaders/Crest_SargassumFoamDamping.shader` |
| `Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat` | `Hecton8/BoidFishInstanced` | `Assets/_Project/Scripts/BoidFishInstanced.shader` |
| `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat` | `Crest/Inputs/Albedo/Sargassum Oil Film` | `Assets/_Project/Art/Shaders/Crest_SargassumOilFilm.shader` |
| `Assets/_Project/Art/Materials/MAT_SargassumWaveDamping.mat` | `Crest/Inputs/Animated Waves/Sargassum Damping` | `Assets/_Project/Art/Shaders/Crest_SargassumWaveDamping.shader` |
| `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggNest.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggShell.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantBud.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantCanopy.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_leaf_scraps.mat` | `UNKNOWN_OR_BUILTIN` | none resolved from project shader GUID |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_type2.mat` | `Hecton8/Flora/SargassumMaster` | `Assets/_Project/Art/Shaders/Hecton_SargassumMaster.shader` |

## Compliant Materials

| Material | Shader | Shader Path |
| --- | --- | --- |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat` | `GPUInstancer/Hecton8/Flora/CoralMaster` | `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat` | `GPUInstancer/Hecton8/Flora/CoralMaster` | `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat` | `GPUInstancer/Hecton8/Flora/CoralMaster` | `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat` | `GPUInstancer/Hecton8/Flora/CoralMaster` | `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat` | `GPUInstancer/Hecton8/Flora/CoralMaster` | `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat` | `GPUInstancer/Hecton8/Flora/KelpMaster` | `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat` | `GPUInstancer/Hecton8/Flora/KelpMaster` | `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat` | `GPUInstancer/Hecton8/Flora/KelpMaster` | `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` |
| `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat` | `GPUInstancer/Hecton8/Flora/KelpMaster` | `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` |
