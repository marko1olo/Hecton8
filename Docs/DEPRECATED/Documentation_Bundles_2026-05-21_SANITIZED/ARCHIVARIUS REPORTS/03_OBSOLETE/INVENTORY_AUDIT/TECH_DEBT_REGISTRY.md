# HECTON-8 TECH DEBT REGISTRY
Date: 2026-05-04
Status: DEPRECATED

## Scribe-Compliance Audit | Status: CONTINUOUS

---

### SCOPE
`Assets/_Project/Scripts/` — first-party gameplay, world, UI, audio, and core systems.
Third-party packages (`Packages/`, `Assets/_ThirdParty/`) are noted for context but are NOT actionable by the Hecton-8 team.

---

### CRITICAL / BLOCKING

| File | Line | Marker | Content | Context |
|------|------|--------|---------|---------|
| `Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs` | 599 | `LayerMask.NameToLayer` | Called inside lazy resolver; previously threw `UnityException` in constructor path (per log history) | Runtime hot path — cached field NOT static readonly |
| `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | `LayerMask.NameToLayer` | "Terrain " (with trailing space) layer lookup; known crash vector | Constructor/static init hazard |
| `Assets/_Project/Scripts/World/CullingManager.cs` | 658-662 | `LayerMask.NameToLayer` | 5 layer lookups in `InitializeLayerCache()`; called from `Update()` | Hot path — must be static readonly |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 437-446 | `LayerMask.NameToLayer` | 4 lazy lookups inside `EnsureLayersCached()` | Called per frame until cached — violates static readonly rule |
| `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs` | 183-204 | `LayerMask.NameToLayer` | 8 layer lookups in `EnsureLayersCached()` | Same pattern — must be static readonly |

---

### FIRST-PARTY `// TODO` ENTRIES

| File | Line | Content |
|------|------|---------|
| `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs` | 244 | `// TODO: Integrate with screen fade system` |
| `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs` | 171 | `// TODO: Trigger death sequence, respawn, etc.` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | 35 | `// TODO: Initialize networking (e.g., Mirror, Netcode)` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | 41 | `// TODO: Start server` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | 47 | `// TODO: Start client` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | 53 | `// TODO: Stop network` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | 57 | `// TODO: Add network messages, player sync, etc.` |
| `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` | 550 | `// TODO: Use default fallback biome` |

---

### FIRST-PARTY `// FIXME` ENTRIES

| File | Line | Content |
|------|------|---------|
| *(none in first-party scripts)* | — | — |

---

### FIRST-PARTY `// BUG` ENTRIES

| File | Line | Content |
|------|------|---------|
| *(none in first-party scripts)* | — | — |

---

### FIRST-PARTY `// HACK` ENTRIES

| File | Line | Content |
|------|------|---------|
| `Assets/_Project/Scripts/UI/PDAShellChrome.cs` | 31 | `SYSTEM STATE // HACKED` (narrative string, not a code hack) |

---

### TOP 10 DIRTIEST FILES (by TODO/FIXME/BUG/HACK density)

| Rank | File | Count | Primary Markers |
|------|------|-------|-----------------|
| 1 | `HectonNetworkManager.cs` | 5 | 5× TODO — entire networking subsystem is stubbed |
| 2 | `CameraJuiceSystem.cs` | 1 | 1× TODO — missing fallback biome integration |
| 3 | `ClimbableLadder.cs` | 1 | 1× TODO — missing screen fade integration |
| 4 | `HectonPlayerHealth.cs` | 1 | 1× TODO — death sequence not implemented |
| 5 | `PlayerSwimBlockoutRig.cs` | 1 | NameToLayer anti-pattern (log-confirmed crash history) |
| 6 | `CullingManager.cs` | 1 | NameToLayer cluster (5 layers) in hot path |
| 7 | `AcousticOcclusionUtility.cs` | 1 | NameToLayer cluster (8 layers) in lazy cache |
| 8 | `PlayerCriticalProceduralAudioRenderer.cs` | 1 | NameToLayer cluster (4 layers) in lazy cache |
| 9 | `HectonCrestOceanDepthCacheBootstrap.cs` | 1 | NameToLayer with trailing-space bug |
| 10 | `BuoyancyObject.cs` | 1 | NameToLayer("Water") in runtime logic |

---

### UNITY 6 API COMPATIBILITY

| File | Line | Deprecated API | Replacement | Severity |
|------|------|----------------|-------------|----------|
| `HectonUnderwaterVisuals.cs` | 529 | `FindFirstObjectByType<T>()` | `FindAnyObjectByType<T>()` | 🟠 HIGH (deprecated in Unity 6) |
| `HectonAtmosphereManager.cs` | 118 | `FindFirstObjectByType<T>()` | `FindAnyObjectByType<T>()` | 🟠 HIGH |
| `HectonAtmosphereManager.cs` | 387 | `FindObjectsByType<T>()` | Cache array in `Awake()` | 🟠 HIGH (allocates) |
| `WorldProceduralScatterDirector.cs` | 1148 | `FindFirstObjectByType<T>()` | `FindAnyObjectByType<T>()` | 🟡 MEDIUM (Editor path) |
| `InputManager.cs` | 123 | `FindFirstObjectByType<T>()` | `FindAnyObjectByType<T>()` | 🟡 MEDIUM (bootstrap path) |
| Third-party: Crest | Various | `RenderGraphSettings` | N/A (deprecated 6000.4) | 🟡 Minor (guarded by `#if`) |
| Third-party: MapMagic | Various | `FindObjectsOfType` | N/A | ✅ Editor-only |
| Third-party: A* Pathfinding | Various | `FindObjectOfType` | N/A | ✅ Editor-only |

**Note:** All third-party deprecated API usage is Editor-only or guarded by `#if UNITY_EDITOR`.

---

### SKYBOX MATERIAL DEBT

| File | Line | Issue | Required Fix | Severity |
|------|------|-------|--------------|----------|
| `HectonRenderPipelineValidator.cs` | 28 | References `Mat_Skybox_Final.mat` (legacy material) | Update to `Mat_HectonSky.mat` | 🔴 **CRITICAL** |
| `Assets/_Project/Art/Materials/Mat_Skybox_Final.mat` | N/A | Legacy skybox material still in project | Archive or delete | 🟡 MEDIUM |

**Context:** `CODEX_BACKLOG.md` (L1546-1558) documents that `RenderSettings.skybox` was corrected from `Mat_Skybox_Final` to `Mat_HectonSky`. However, `HectonRenderPipelineValidator.cs` still references the legacy material path.

---

### NOTES
- `HectonNetworkManager.cs` is 100% stub — every public method body is a TODO. Recommend either implementing or removing before production.
- No `// KOSTYL` or `// VREMENNO` markers found in first-party scripts (only in archived docs).
- Third-party debt (Crest, Shader Graph, Shapes, etc.) is extensive but out of scope for Hecton-8 team action.
- **Unity 6 LTS Compatibility:** First-party code has 5 instances of deprecated `FindFirstObjectByType` — should migrate to `FindAnyObjectByType` (Unity 6 API).
