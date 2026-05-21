# HECTON-8 STRING LITERAL CRIMES
Date: 2026-05-04
Status: DEPRECATED

## Scribe-Compliance Audit | Status: CONTINUOUS

---

### RULE REMINDER (from AGENTS.md)
- [FORBID] `GameObject.Find` at runtime.
- [FORBID] `GameObject.FindWithTag` at runtime.
- [FORBID] `Animator.SetTrigger/SetBool/SetFloat` with literal strings.
- [FORBID] `tag == "string"` — use `CompareTag`.
- [FORBID] `LayerMask.NameToLayer("...")` in hot paths — cache as `static readonly int`.

---

### 🔴 `GameObject.Find` IN FIRST-PARTY SCRIPTS

All occurrences below are in **runtime or editor-bootstrap** code. Editor-only usage is noted.

| File | Line | Literal | Context |
|------|------|---------|---------|
| `Assets/_Project/Editor/ObjectSpawner.cs` | 22 | `ContainerName` | Editor tool — acceptable |
| `Assets/_Project/Scripts/Editor/BiomeMatrixBootstrapAuthoring.cs` | 110 | `ManagersRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` | 380 | `"Tool_Staging"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` | 588 | `"Tool_TrialRange"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs` | 302 | `rootPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs` | 309 | `parentName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs` | 315 | `fabricatorName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs` | 346 | `rootPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/HectonRockRuntimeBootstrapAuthoring.cs` | 42 | `ManagersRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/HectonRockRuntimeBootstrapAuthoring.cs` | 46 | `RockRuntimeRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/LoreSystemsBootstrapUtility.cs` | 22 | `"--- SYSTEMS ---/LoreSystems"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/LoreSystemsBootstrapUtility.cs` | 26 | `"--- SYSTEMS ---"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/MainMenuSettingsPanelAuthoring.cs` | 566 | `gameObjectName` | Editor tool |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 64 | `"Player"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 98 | `ManagersRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 180 | `"Player"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 181 | `RelayOneName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 182 | `RelayTwoName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 211 | `ActiveHudOverlayName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/RelayRouteAuthoringUtility.cs` | 349 | `objectName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs` | 105 | `RootPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs` | 202 | `currentPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/ScanIntelValidator.cs` | 88 | `"--- GAMEPLAY ---/Item_Titanium"` | Editor validator |
| `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs` | 222 | `SkyRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` | 297 | `ProxyRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 82 | `ManagersRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 88 | `"Player"` | Editor bootstrap (fallback after `FindWithTag`) |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 440 | `ManagersRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 650 | `RockRuntimeRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 858 | `WorldRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 936 | `WorldRootName` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1061 | `"--- WORLD ---/Resource_FieldSources"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1086 | `StarterReefFieldPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1111 | `"--- WORLD ---/Fabrication_Outpost"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1136 | `"Fabrication_Trial"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1161 | `"Tool_Staging"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1192 | `lanePath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1227 | `objectPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1260 | `objectPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 1351 | `objectPath` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldStreamingWiringValidator.cs` | 322 | `"Player"` | Editor validator (fallback after `FindWithTag`) |
| `Assets/_Project/Scripts/Editor/WorldStreamingWiringValidator.cs` | 457 | `ManagersRootName` | Editor validator |
| `Assets/_Project/Scripts/Editor/BarterBootstrapAuthoring.cs` | 95 | `"Player"` | Editor bootstrap |

**VERDICT**: All `GameObject.Find` calls are inside `#if UNITY_EDITOR` or `Editor/` folders. **No runtime violations found in first-party code.**

---

### 🔴 `GameObject.FindWithTag` IN FIRST-PARTY SCRIPTS

| File | Line | Tag | Context |
|------|------|-----|---------|
| `Assets/_Project/Scripts/Editor/ScanIntelValidator.cs` | 38 | `"Player"` | Editor validator |
| `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` | 86 | `"Player"` | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldStreamingWiringValidator.cs` | 320 | `"Player"` | Editor validator |

**VERDICT**: All `FindWithTag` calls are editor-only. **No runtime violations.**

---

### 🟡 `LayerMask.NameToLayer` IN FIRST-PARTY SCRIPTS (Hot Path Risk)

| File | Line | Layer String | Context | Risk |
|------|------|-------------|---------|------|
| `Assets/_Project/Scripts/BuoyancyObject.cs` | 517 | `"Water"` | Runtime method | 🔴 HIGH — no caching |
| `Assets/_Project/Scripts/Fauna/FaunaPOI.cs` | 43 | `"FaunaPOI"` | Commented out | 🟡 LOW |
| `Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs` | 160 | `"Player"` | Runtime method | 🔴 HIGH — no caching |
| `Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs` | 599 | `FirstPersonToolsLayerName` | Runtime lazy init | 🔴 HIGH — log-confirmed crash history |
| `Assets/_Project/Scripts/Gameplay/SolarPanel.cs` | 438 | `"Water"` | Runtime method | 🔴 HIGH — no caching |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 437-446 | `"Player"`, `"TriggerZone"`, `"TransparentFX"`, `"FirstPersonTools"` | Lazy cache in runtime | 🟡 MEDIUM — caches after first call |
| `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs` | 183-204 | 8 layers | Lazy cache in runtime | 🟡 MEDIUM — caches after first call |
| `Assets/_Project/Scripts/World/CullingManager.cs` | 658-662 | `"Debris"`, `"Particles"`, `"Props"`, `"Flora"`, `"Terrain"` | Runtime `Update()` | 🔴 CRITICAL — called every frame until cached |
| `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | `"Terrain"`, `"Terrain "` | Runtime init | 🔴 HIGH — trailing space typo + crash history |
| `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` | 1358, 2341 | `"Sockets"` | Editor bootstrap | 🟢 LOW |

**RECOMMENDATION**: Convert ALL runtime `NameToLayer` calls to `private static readonly int` fields initialized in `Awake` or static constructor. `CullingManager` is the worst offender (called from `Update()`).

---

### 🟢 `Animator.SetTrigger/SetBool/SetFloat` WITH LITERAL STRINGS

| File | Line | Method | Literal | Context |
|------|------|--------|---------|---------|
| *(none in first-party scripts)* | — | — | — | — |

**VERDICT**: All first-party `Animator` calls use cached `StringToHash` fields (`_HashSwimSpeed`, `_openTriggerHash`, `_closeTriggerHash`, etc.). **COMPLIANT.**

Third-party violations exist in `Feel/MMFeedbacks`, `Candice AI`, `MMTools` — out of scope.

---

### 🔴 `tag == "string"` IN FIRST-PARTY SCRIPTS

| File | Line | Expression | Context |
|------|------|-----------|---------|
| *(none in first-party scripts)* | — | — | — |

**VERDICT**: No `tag == "..."` violations found in `Assets/_Project/Scripts/`. **COMPLIANT.**

Third-party violations exist in `Candice AI` (~40 instances) and `Bakery` — out of scope.

---

### SUMMARY

| Category | First-Party Violations | Severity | Action Required |
|----------|----------------------|----------|-----------------|
| `GameObject.Find` | 0 runtime | 🟢 | None |
| `GameObject.FindWithTag` | 0 runtime | 🟢 | None |
| `Animator.Set*(string)` | 0 | 🟢 | None |
| `tag == "string"` | 0 | 🟢 | None |
| `LayerMask.NameToLayer` | 9 files | 🔴 | **Cache as static readonly** |

**PRIMARY ACTION**: Fix `LayerMask.NameToLayer` caching in:
1. `CullingManager.cs` — move out of `Update()`
2. `BuoyancyObject.cs`
3. `SolarPanel.cs`
4. `EnvironmentalHazard.cs`
5. `PlayerSwimBlockoutRig.cs` — already has crash history
6. `HectonCrestOceanDepthCacheBootstrap.cs` — fix trailing space typo
