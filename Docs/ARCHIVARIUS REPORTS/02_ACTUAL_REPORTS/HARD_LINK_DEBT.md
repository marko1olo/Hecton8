# HARD LINK DEBT — Asset-to-Code Violation Map
Date: 2026-05-04
Status: REFERENCE


> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md § Addressables · § Resources.Load [FORBID] · § AssetDatabase [Editor-only]  

---

## 1. EXECUTIVE SUMMARY

| Category | Count | Severity |
|----------|-------|----------|
| `Resources.Load` in first-party runtime | **0** | ✅ Clean |
| `AssetDatabase.LoadAssetAtPath` in first-party runtime | **0** | ✅ Clean |
| Hardcoded asset-string paths in first-party runtime | **4** | 🔴 DEBT |
| `Resources.Load` in third-party runtime | **7+ packages** | 🟡 External — do not patch without mandate |
| `AssetDatabase.LoadAssetAtPath` in third-party editor | N/A | 🟢 Editor-only, acceptable |

---

## 2. FIRST-PARTY HARDCODED PATHS (VIOLATIONS)

These scripts embed literal project paths. They bypass Addressables and will break if folder structure changes or if assets are moved to bundles.

### 🔴 `SargassumCollapseChunk.cs`
```csharp
private const string ScrapPickupPrefabAssetPath =
    "Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab";
```
- **Used at:** Runtime spawn fallback in cut response.
- **Fix:** Convert to `AssetReferenceGameObject` Addressable or pre-cached `AsyncOperationHandle`.

### 🔴 `HectonFabricatorUI.cs`
```csharp
private const string HologramShaderPath =
    "Assets/_Project/Art/Shaders/Hecton_FabricatorHologram.shader";
```
- **Used at:** Editor-only cold path (`#if UNITY_EDITOR`) via `AssetDatabase.LoadAssetAtPath`.
- **Fix:** Shader should be assigned via serialized field or ShaderVariantCollection warmup. Runtime must never reach this path.

### 🔴 `HectonScanMarkerSystem.cs`
```csharp
private const string MarkerShaderPath =
    "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
```
- **Used at:** Same pattern as FabricatorUI — editor-only cold resolve.
- **Fix:** Serialize shader reference or bake into SRP global shader table.

### 🔴 `DepthZoneDirector.cs`
```csharp
private const string DepthZoneDataRoot = "Assets/_Project/Data/Lore/DepthZones";
```
- **Used at:** Runtime catalog scan for `TextAsset` JSON files.
- **Fix:** Migrate to Addressables label `"DepthZoneData"` + `Addressables.LoadAssetsAsync<TextAsset>()`.

---

## 3. FIRST-PARTY EDITOR-ONLY AssetDatabase USAGE (ACCEPTABLE)

All found usages are wrapped in `#if UNITY_EDITOR` or live inside `Editor/` folders. They do not leak to runtime builds.

| Script | Path Target | Purpose |
|--------|-------------|---------|
| `AcousticZoneController.cs` | `AudioMixer` · `AudioClip` assets | Editor fallback for missing serialized refs |
| `HectonFabricatorUI.cs` | `Shader` asset | Editor cold-load hologram shader |
| `HectonFluidEngine.cs` | `ComputeShader` asset | Editor cold-load GPU buoyancy compute |
| `HectonArtVramAudit.cs` | `Texture` assets | Editor audit tool |
| `ItemShaderSetupUtility.cs` | `Shader` · `GameObject` prefabs | Editor batch setup utility |
| `SargassumGenerator.cs` | `Shader` · `Material` · `Mesh` | Editor procedural asset generator |
| `CrestMigrationTool.cs` | `Material` · `WaveSpectrum` | One-shot migration editor tool |
| `FaunaBiomeBootstrapAuthoring.cs` | `ScriptableObject` assets | Editor data authoring |
| `ResourceCraftingBootstrapAuthoring.cs` | `ItemData` · `RecipeData` | Editor content bootstrap |
| `HectonMaterialKeywordSanitizer.cs` | `Material` assets | Editor sanitization pass |
| `HectonShaderVariantStripper.cs` | `RenderPipelineAsset` | Editor build processor |
| `LocalizationCjkCoverageValidator.cs` | `TMP_FontAsset` | Editor validation |

> **Rule Quote:** AGENTS.md — "[FORBID] Resources.Load." No runtime first-party code calls it. Editor-only `AssetDatabase` usage is compliant.

---

## 4. THIRD-PARTY RUNTIME `Resources.Load` DEBT

These are **not first-party** and must not be patched without an explicit external-patch mandate. Listed for completeness and bundle-planning.

| Package | File | Asset Loaded | Risk |
|---------|------|--------------|------|
| **MapMagic** | `TerrainTile.cs` | `MapMagicDefaultTerrainData` (TerrainData) | Runtime terrain init — blocks on sync load |
| **VolumetricLightBeam** | `Config.cs` | `Noise3D_64x64x64` · `DustParticles` · `VLBDitheringNoise` · `VLBBlueNoise` | Startup sync load; small but unbudgeted |
| **Shapes** | `ShapesConfig.cs` | `"Shapes Config"` | Startup singleton resolve |
| **Shapes** | `ShapesAssets.cs` | `"Shapes Assets"` | Startup singleton resolve |
| **MasterAudio** | `MasterAudio.cs` | Dynamic audio clips via resourceFileName | Runtime audio loading — high latency risk |
| **MasterAudio** | `SingletonScriptable.cs` | Scriptable singleton | Startup |
| **Easy Save 3** | `ES3ResourcesStream.cs` | `TextAsset` (save data) | Runtime save/load — unavoidable if using ES3 Resources location |
| **Easy Save 3** | `ES3GlobalReferences.cs` | `ES3GlobalReferences` asset | Startup |
| **Easy Save 3** | `ES3Settings.cs` | `ES3Defaults` asset | Startup |
| **RealtimeCSG** | `MaterialUtility.cs` | Multiple materials & textures | Editor + runtime CSG preview |

> **AGENTS.md Rule:** "[FORBID] A* Pathfinding, DOTween, Easy Save 3, Master Audio — replaced by custom Native/Burst/DSP subsystems."  
> MasterAudio and EasySave3 are listed as forbidden, yet they remain in the project. This is architectural drift. Do **not** remove them in this pass; flag for dedicated purge milestone.

---

## 5. THIRD-PARTY `AssetDatabase.LoadAssetAtPath` IN RUNTIME SCRIPTS

One third-party script uses `AssetDatabase` inside a runtime-visible file (guarded by `#if UNITY_EDITOR`, but the file sits in `Runtime/`):

| Package | File | Asset | Note |
|---------|------|-------|------|
| **Crest** | `WaterRenderer.Editor.cs` | `Water Volume.mat` | File name says Editor; runs in `Reset()` which is Editor-only, but file location is misleading. No runtime leak. |

---

## 6. RECOMMENDED REMEDIATION ROADMAP

| Priority | Task | Owner | Effort |
|----------|------|-------|--------|
| P0 | Convert `SargassumCollapseChunk` prefab path to Addressables `AssetReference` | Gameplay / World | 1h |
| P1 | Remove hardcoded shader paths from `HectonFabricatorUI` and `HectonScanMarkerSystem`; use serialized `Shader` fields | UI / VFX | 30m |
| P1 | Migrate `DepthZoneDirector` JSON root scan to Addressables label query | World / Lore | 2h |
| P2 | Schedule MasterAudio removal & replacement by `SpatialAudioManager` (Native DSP) | Audio | Milestone |
| P2 | Schedule EasySave3 removal & replacement by `SaveManager` (LZ4 + XXHash3) | SaveSystem | Milestone |
| P3 | Audit MapMagic `Resources.Load` for Addressables-compatible terrain data injection | World | Spike |

---

*Debt map generated by ARCHIVARIUS. Do not hand-edit without updating the regression model.*
