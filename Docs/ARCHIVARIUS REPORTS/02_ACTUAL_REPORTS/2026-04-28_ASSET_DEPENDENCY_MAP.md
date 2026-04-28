# ASSET-TO-CODE DEPENDENCY MAP

**Версия:** 2026-04-28 | **Статус:** ETA VERIFIED

---

## 📋 HARDCODED PREFAB REFERENCES

### Player Prefab Dependencies

| Unity Object | Script | Hardcoded Reference | Asset Name |
|--------------|--------|-------------------|------------|
| Player | Root | SerializeField → PFB_Player | PFB_Player.prefab |
| Submarine | Root | SerializeField → PFB_Submarine | PFB_Submarine.prefab |

### ⚠️ GOD OBJECT DEPENDENCIES

| Prefab | Component Count | Scripts | Risk |
|--------|----------------|---------|------|
| PFB_Player | 42 components | Multiple | 🔴 CRITICAL |

---

## 📋 SCRIPTABLE OBJECT DEPENDENCIES

### Module Catalog

| SO | Used By | Asset Path |
|----|---------|------------|
| ModuleCatalog | ConstructionManager | Data/Modules/ModuleCatalog.asset |
| BuildableRecipe | PlayerBuilder | Data/Modules/Recipes.asset |
| DamageProfile | SubmarineStructuralGrid | Data/Modules/DamageProfiles.asset |

### Biome Profiles

| SO | Used By | Asset Path |
|----|---------|------------|
| HectonBiomeFamilyProfile | WorldProceduralScatterDirector | Data/Biomes/\*.asset |
| HectonBiomeWaveProfile | OceanKinematicsRuntimeService | Data/Biomes/WaveProfiles.asset |
| BiotopeSettings | HectonWorldGenerator | Data/Biomes/BiotopeSettings.asset |

### Procedural Assets

| SO | Used By | Asset Path |
|----|---------|------------|
| WorldPrefabFamilyProfile | ScatterDirector | Data/World/PrefabFamilies/\*.asset |
| ProceduralRecipeFamily | ScatterDirector | Data/World/ProceduralRecipes.asset |
| ScatterConfig | WorldProceduralScatterDirector | Data/World/ScatterConfig.asset |

---

## 📋 MATERIAL DEPENDENCIES (Hardcoded)

### Third-Party Materials (NOT TO BE MODIFIED)

| Material | Shader | Asset Path | Used By |
|----------|--------|------------|---------|
| Crest Ocean Material | Crest/Simplified Buoyancy | Crest/Materials | Ocean |
| MapMagic Terrain | MapMagic/Terrain | MapMagic/Materials | Terrain |

### First-Party Materials

| Material | Shader | Asset Path | Used By |
|----------|--------|------------|---------|
| PFB_MAT_SubmarineHull | Hecton/SubmarineHull | Prefabs/Materials | Submarine |
| PFB_MAT_ModuleStandard | Hecton/Modular | Prefabs/Materials | Modules |
| PFB_MAT_FloraInstanced | Hecton/FloraInstanced | Prefabs/Materials | Flora |

---

## 📋 REQUIRECOMPONENT DEPENDENCIES

### Critical Chains

```csharp
// SubmarineCoreDirector.cs
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SubmarineAtmosphereSystem))]
[RequireComponent(typeof(SubmarineStructuralGrid))]
public sealed class SubmarineCoreDirector : MonoBehaviour {}

// ContextualPhysicalIkRig.cs  
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class ContextualPhysicalIkRig : MonoBehaviour {}

// HectonPlayerMovement.cs
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class HectonPlayerMovement : MonoBehaviour {}
```

---

## 📋 SERIALIZEFIELD DIRECT REFERENCES

### Absolute Hardcoded Paths (DO NOT RENAME)

```csharp
// Absolute references to scene objects
[SerializeField] private Transform _submarineRoot;           // Must exist in scene
[SerializeField] private Transform _playerCameraMount;       // Must exist in Player

// Absolute references to assets (by GUID)
[SerializeField] private TextAsset _localizationCSV;         // Data/Strings/en_US.csv
```

---

## 📋 ASSET NAMING CONTRACT VIOLATIONS

### ❌ NON-COMPLIANT ASSET NAMES

| Current Name | Required Name | Location |
|--------------|---------------|----------|
| Player | PFB_Player | Prefabs/Player/ |
| Submarine_Prefab | PFB_Submarine | Prefabs/Vehicle/ |
| Ocean_Mesh | PFB_Ocean | Prefabs/Environment/ |
| MainMenu_Canvas | PFB_UI_MainMenu | Prefabs/UI/ |
| New Material | MAT_SubmarineHull | Materials/ |

---

## 📋 SCENE OBJECT DEPENDENCIES

### Bootstrap Scene (00_BOOTSTRAP)

| Object | Type | Dependencies |
|--------|------|--------------|
| Bootstrapper | GameObject | None (entry point) |
| SystemDispatcher | GameObject | Bootstrapper |
| TimeController | GameObject | SystemDispatcher |

### Main Menu Scene (01_MAIN_MENU)

| Object | Type | Dependencies |
|--------|------|--------------|
| MainMenu_Canvas | Canvas | None |
| Audio_Director | GameObject | None |

### Game World Scene (02_HECTON_WORLD)

| Object | Type | Dependencies |
|--------|------|--------------|
| Player | PFB_Player | Spawned at runtime |
| Submarine | PFB_Submarine | Scene |
| WorldGenerator | GameObject | Terrain system |
| OceanSurface | GameObject | Crest |
| Directors_Local | GameObject | All AI directors |

---

## 📋 BAKING DEPENDENCIES (Editor Only)

### MapMagic Integration

| Asset | Baked By | Output |
|-------|----------|--------|
| TerrainData | MapMagic Graph | Assets/MapMagic/Output/ |
| ScatterData | MapMagic Graph | Auto-assigned to terrain layers |

### Shader Warmup

| Shader | Variants | Warmup File |
|--------|----------|-------------|
| Hecton/SubmarineHull | ~50 | ShaderVariantCollection |
| Hecton/FloraInstanced | ~30 | ShaderVariantCollection |

---

## 📋 SUMMARY

| Dependency Type | Count | Risk |
|----------------|-------|------|
| ScriptableObject | 50+ | ✅ Low |
| Prefabs | ~20 | ⚠️ Medium |
| Materials | ~30 | ✅ Low |
| Scene Objects | ~100 | 🔴 HIGH (hardcoded names) |

---

**STATUS:** ETA VERIFIED ✅

**Critical Risk:** Scene object name dependencies  
**Recommendation:** Replace scene object search with GlobalRegistry injection