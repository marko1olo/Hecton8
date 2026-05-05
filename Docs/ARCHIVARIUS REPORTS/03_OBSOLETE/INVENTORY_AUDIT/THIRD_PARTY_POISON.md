# THIRD-PARTY POISON (ANTI-CORRUPTION LAYER AUDIT) — HECTON-8 Static Audit
Date: 2026-05-04
Status: DEPRECATED


**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandate:** AGENTS.md — "Third-Party Asset Integrity" · "MapMagic: only via MapMagicBridge" · "Crest (ocean, URP)"

---

## I. Audit Scope

Scan for **direct** references to third-party APIs inside first-party gameplay/runtime scripts:
- `Crest` namespace (Crest.OceanRenderer, Crest.WaveHarmonic, etc.)
- `MapMagic` namespace (MapMagic.Core, MapMagic.Nodes, etc.)
- `FMOD` namespace (if present)

**Expected Pattern:** All third-party coupling MUST be isolated in Bridge classes:
- `MapMagicBridge.cs` — single source of truth for terrain/biome queries
- `HectonCrestOceanDepthCacheBootstrap.cs` — ocean kinematics provider
- No direct `using Crest;` or `using MapMagic;` in gameplay code

---

## II. Findings Summary

| File | Line | Third-Party Coupling | Severity | Verdict |
|---|---|---|---|---|
| `HectonPlayerMovement.cs` | 1822+ | Direct `NativeArray` allocations for Crest wave queries | 🔴 **CRITICAL** | **ARCHITECTURAL FRAUD** — extract to `IHectonOceanKinematics` |
| `HectonMapMagicVegetationBridge.cs` | Multiple | Direct `MapMagic.Core.MapMagicObject` references | 🟡 **ACCEPTABLE** | Bridge class — legitimate owner |
| `HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | `LayerMask.NameToLayer("Terrain ")` (trailing space) | 🟠 **HIGH** | Known crash vector |
| `WorldProceduralScatterDirector.cs` | 1148 | `FindFirstObjectByType<MapMagicBridge>` | 🟡 **MEDIUM** | Editor/bootstrap path |
| `MapMagicBridge.cs` | 397-400 | Direct `MapMagicBridge.RuntimeMapMagicObject` access | ✅ **COMPLIANT** | Self-reference — legitimate |
| `BeaconRuntime.cs` | 116 | `Shader.Find("Universal Render Pipeline/Lit")` | 🔴 **HIGH** | Breaks SRP Batching |
| `SargassumGlobalDragManager.cs` | 1838, 1857 | `Shader.Find("Universal Render Pipeline/Lit")` | 🔴 **HIGH** | Breaks SRP Batching |
| `ImpostorSystem.cs` | 726-728 | `Shader.Find("Universal Render Pipeline/Unlit")` + fallback | 🟠 **MEDIUM** | Fallback path — rare |
| `HectonIndirectVegetationRenderer.cs` | 1072, 2109 | `Shader.Find("Universal Render Pipeline/Lit")` | 🔴 **HIGH** | Breaks SRP Batching |

**Note:** Editor scripts (`ConstructionBootstrapAuthoring.cs`, `HectonMaterialChannelPackValidator.cs`, etc.) using `Shader.Find` are **ACCEPTABLE** (editor-only baking paths).

---

## III. CRITICAL VIOLATION: `HectonPlayerMovement.cs`

### 🔴 Direct Crest Coupling (Line 1822+)

**Finding:** `HectonPlayerMovement.cs` contains direct NativeArray allocations for Crest ocean wave sampling:

```csharp
// Approximate pattern found (line 1822+):
NativeArray<float> waveHeights = new NativeArray<float>(..., Allocator.TempJob);
// Direct Crest API call to sample waves
```

**Mandate Violation:**
- AGENTS.md: "Third-Party Asset Integrity: DO NOT write custom runtime wrappers, material clones, or overrides for complex 3rd-party assets (Crest, MapMagic)"
- AGENTS.md: "Crest (ocean, URP)" — expected to be accessed via `IHectonOceanKinematics` interface

**Expected Pattern:**
```csharp
// CORRECT: Route through ocean kinematics provider
IHectonOceanKinematics ocean = GlobalRegistry.OceanKinematics;
float waveHeight = ocean.SampleWaveHeight(position);
```

**Required Fix:**
1. Extract Crest sampling logic into `HectonCrestOceanKinematics` class
2. Implement `IHectonOceanKinematics` interface
3. Register via `GlobalRegistry`
4. Replace direct Crest calls in `HectonPlayerMovement.cs` with interface calls

---

## IV. ACCEPTABLE PATTERNS (Bridge Classes)

### `HectonMapMagicVegetationBridge.cs` — Legitimate Owner

This file is the **designated Bridge** for MapMagic integration. Direct `MapMagic.Core.MapMagicObject` references here are **ARCHITECTURALLY CORRECT**:

- Owns `RuntimeMapMagicObject` reference
- Provides `TryGetHeight()`, `TryGetBiomeIndex()` APIs
- Isolates all MapMagic coupling from gameplay code

**Status:** ✅ **COMPLIANT**

### `MapMagicBridge.cs` — Legacy Bridge

Older bridge class, still legitimate:
- Provides `Instance` singleton accessor
- Routes terrain/biome queries

**Note:** Two bridges (`MapMagicBridge` + `HectonMapMagicVegetationBridge`) create **architectural ambiguity**. Recommend consolidation.

---

## V. MEDIUM SEVERITY: Editor/Bootstrap Paths

### `WorldProceduralScatterDirector.cs` (Line 1148)

```csharp
MapMagicBridge bridge = FindFirstObjectByType<MapMagicBridge>(FindObjectsInactive.Include);
```

**Context:** Editor/bootstrap initialization path, not hot path.

**Verdict:** 🟡 **ACCEPTABLE** — one-time resolution, not per-frame.

### `HectonCrestOceanDepthCacheBootstrap.cs` (Lines 104, 107)

```csharp
LayerMask.NameToLayer("Terrain ")  // Note trailing space!
```

**Risk:** Known crash vector if "Terrain " layer doesn't exist.

**Required Fix:** Cache as `static readonly int` in `Awake()`:
```csharp
private static readonly int _TerrainLayer = LayerMask.NameToLayer("Terrain");
```

---

## VI. Third-Party Package Health

| Package | Deprecated API Usage | Unity 6 Guards | Verdict |
|---|---|---|---|
| **Crest** | `RenderGraphSettings` (deprecated in 6000.4) | `#if UNITY_6000_0_OR_NEWER` present | 🟡 Minor warnings |
| **MapMagic** | `FindObjectsOfType` (Editor only) | N/A (Editor tooling) | ✅ Acceptable |
| **GPU Instancer** | `FindObjectsByType` (Editor) | N/A | ✅ Acceptable |
| **Feel/MMTools** | `FindObjectOfType` (Editor) | N/A | ✅ Acceptable |
| **A* Pathfinding** | `FindObjectOfType` (Editor) | N/A | ✅ Acceptable |

**Note:** All deprecated API usage in third-party packages is **Editor-only** or guarded by `#if UNITY_EDITOR`. No runtime hot path violations.

---

## VII. Recommendations

### Immediate (Next Sprint)

1. **Extract Crest Kinematics** — Create `HectonCrestOceanKinematics : IHectonOceanKinematics`
2. **Remove Direct Crest Calls** — Replace all `HectonPlayerMovement.cs` Crest sampling with interface calls
3. **Fix LayerMask Cache** — `HectonCrestOceanDepthCacheBootstrap.cs` trailing space bug

### Medium-Term (Architecture Cleanup)

4. **Consolidate Bridges** — Merge `MapMagicBridge` + `HectonMapMagicVegetationBridge` into single owner
5. **Anti-Corruption Audit** — Scan for `using GPUInstancer;`, `using Shapes;` in gameplay code

---

## VIII. Compliance Status

| Category | Status | Notes |
|---|---|---|
| Crest Coupling | 🔴 **NON-COMPLIANT** | Direct API calls in `HectonPlayerMovement.cs` |
| MapMagic Coupling | ✅ **COMPLIANT** | Properly isolated in Bridge classes |
| FMOD Coupling | ✅ **N/A** | FMOD not present in codebase |
| Editor-Only Third-Party | ✅ **COMPLIANT** | Deprecated APIs confined to Editor |

---

**STATUS:** 🔴 **CRITICAL FIX REQUIRED** — Crest anti-corruption layer breach in `HectonPlayerMovement.cs`
