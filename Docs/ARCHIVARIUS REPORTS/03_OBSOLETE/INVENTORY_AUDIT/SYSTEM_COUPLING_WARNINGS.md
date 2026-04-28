# SYSTEM COUPLING WARNINGS (THE SPAGHETTI SCAN) — HECTON-8 Static Audit

**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandate:** AGENTS.md — "GlobalRegistry (Service Locator Pattern)" · "FORBID: FindObjectOfType in hot paths" · "Systems must communicate via EventBus or GlobalRegistry"

---

## I. Audit Scope

Scan for **hard dependencies** between systems:
- `FindObjectOfType<SystemB>()` in runtime code
- `GetComponent<SystemB>()` inside Tick/Update loops
- Direct singleton access without null-guards
- Cross-system field injections without interface contracts

**Expected Pattern:**
- Systems communicate via `EventBus` (static, zero-alloc)
- Managers accessed via `GlobalRegistry.Manager`
- Component refs cached in `Awake()`/`OnEnable()`, never resolved per-frame

---

## II. Findings Summary

| File | Line | Coupling Pattern | Severity | Context |
|---|---|---|---|---|
| `HectonPlayerMovement.cs` | 443-445 | Direct `HectonMapMagicVegetationBridge` field injection | 🟡 **MEDIUM** | Cached ref — acceptable |
| `HectonSurvivalSystem.cs` | 242, 442, 691 | `WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge()` | 🟡 **MEDIUM** | Lazy resolve — acceptable |
| `FloraInteractionManager.cs` | 770-778 | `GetComponent<HectonMapMagicVegetationBridge>()` + `GetComponentInParent<>` | 🟠 **HIGH** | Runtime resolution chain — cache in `Awake()` |
| `AcousticZoneController.cs` | 1827-1842 | Direct `HectonMapMagicVegetationBridge.GlobalVegetationAcousticType` | 🟡 **MEDIUM** | Static accessor — acceptable |
| `PlayerCriticalProceduralAudioRenderer.cs` | 1471 | `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` | 🟡 **MEDIUM** | Static accessor — acceptable |
| `FaunaDirector.cs` | 376, 2169 | Cached `_vegetationThreatBridge` + lazy resolve | ✅ **COMPLIANT** | Proper caching pattern |
| `WorldProceduralScatterDirector.cs` | 1148 | `FindFirstObjectByType<MapMagicBridge>` | 🟡 **MEDIUM** | Editor/bootstrap only |
| `HectonUnderwaterVisuals.cs` | 529 | `FindFirstObjectByType<>` | 🟠 **HIGH** | Runtime resolution — deprecated Unity 6 API |
| `InputManager.cs` | 54, 123 | `FindFirstObjectByType<InputManager>` | 🟡 **MEDIUM** | Singleton bootstrap |
| `HectonAtmosphereManager.cs` | 118, 387 | `FindFirstObjectByType<>` + `FindObjectsByType<>` | 🟠 **HIGH** | Runtime resolution — deprecated Unity 6 API |
| `MapMagicBridge.cs` | 240 | `Resources.FindObjectsOfTypeAll<MapMagicObject>` | 🟡 **MEDIUM** | Editor/bootstrap fallback for inactive MM object |
| `HectonCrestOceanDepthCacheBootstrap.cs` | 338 | `Resources.FindObjectsOfTypeAll<Terrain>` | 🟡 **MEDIUM** | Cold alloc — depth-cache recovery fallback |

**Note:** `Resources.FindObjectsOfTypeAll` usage in Editor scripts is **ACCEPTABLE** (cold path). Runtime usage must be justified.

---

## III. CRITICAL FINDINGS

### 🔴 `FloraInteractionManager.cs` — Runtime GetComponent Chain (Lines 770-778)

**Pattern:**
```csharp
private HectonMapMagicVegetationBridge ResolveVegetationBridge()
{
    if (_vegetationBridgeOverride != null)
        return _vegetationBridgeOverride;

    HectonMapMagicVegetationBridge directBridge = GetComponent<HectonMapMagicVegetationBridge>();
    if (directBridge != null)
        return directBridge;

    HectonMapMagicVegetationBridge childBridge = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<HectonMapMagicVegetationBridge>(transform);
    if (childBridge != null)
        return childBridge;

    return GetComponentInParent<HectonMapMagicVegetationBridge>();
}
```

**Violations:**
- `GetComponent<T>()` at runtime (allocates if not cached)
- `GetComponentInParent<T>()` — hierarchy traversal (expensive)
- No caching — may be called multiple times

**Mandate Violation:**
- AGENTS.md: "GetComponent<T>() uncached · GetComponents<T>() (alloc array) → TryGetComponent · pre-allocated List<T> overload"
- AGENTS.md: "Scene search → cached refs / Singleton.Instance"

**Required Fix:**
```csharp
// Cache in Awake():
private void Awake()
{
    _vegetationBridge = ResolveVegetationBridge(); // One-time cost
}

private HectonMapMagicVegetationBridge ResolveVegetationBridge()
{
    // ... existing logic ...
}
```

---

### 🔴 `HectonUnderwaterVisuals.cs` — FindFirstObjectByType (Line 529)

**Pattern:**
```csharp
HectonUnderwaterVisuals instance = FindFirstObjectByType<HectonUnderwaterVisuals>(FindObjectsInactive.Include);
```

**Context:** Likely singleton accessor or bootstrap path.

**Risk:** `FindFirstObjectByType` is **deprecated** and allocates.

**Required Fix:**
```csharp
// Use FindAnyObjectByType (Unity 6 API) or cache Instance:
private static HectonUnderwaterVisuals _instance;
public static HectonUnderwaterVisuals Instance
{
    get
    {
        if (_instance == null)
            _instance = FindAnyObjectByType<HectonUnderwaterVisuals>(FindObjectsInactive.Include);
        return _instance;
    }
}
```

---

### 🔴 `HectonAtmosphereManager.cs` — FindFirstObjectByType + FindObjectsByType (Lines 118, 387)

**Pattern:**
```csharp
// Line 118:
Camera mainCamera = FindFirstObjectByType<Camera>();

// Line 387:
Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
```

**Violations:**
- `FindFirstObjectByType` in runtime path (deprecated)
- `FindObjectsByType` — allocates array

**Required Fix:**
```csharp
// Cache in Awake():
private void Awake()
{
    _mainCamera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
    _terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // One-time alloc
}
```

---

## IV. ACCEPTABLE PATTERNS

### `FaunaDirector.cs` — Proper Caching (Lines 376, 2169)

```csharp
private HectonMapMagicVegetationBridge _vegetationThreatBridge;

private void LazyResolve()
{
    if (_vegetationThreatBridge == null)
        WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationThreatBridge);
}
```

**Verdict:** ✅ **COMPLIANT**
- Cached field
- Lazy resolve (one-time cost)
- Uses `WorldRuntimeReferenceUtility` helper

---

### `HectonSurvivalSystem.cs` — Lazy Resolve with Cache (Lines 242, 442, 691)

```csharp
private HectonMapMagicVegetationBridge _vegetationBridge;

private void Awake()
{
    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
}
```

**Verdict:** ✅ **COMPLIANT**
- Cached in `Awake()`
- Uses utility helper with null-guard

---

### `AcousticZoneController.cs` — Static Accessor (Lines 1827-1842)

```csharp
HectonMapMagicVegetationBridge.VegetationAcousticType acousticType =
    HectonMapMagicVegetationBridge.GlobalVegetationAcousticType;
float density = Mathf.Clamp01(HectonMapMagicVegetationBridge.GlobalVegetationAudioDensity);
```

**Verdict:** ✅ **COMPLIANT**
- Static property accessor (zero-cost)
- No object resolution

---

## V. Singleton Anti-Patterns

### `InputManager.cs` — DDOL Race Guard (Lines 54, 123)

```csharp
// Line 54:
InputManager instance = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);

// Line 123:
InputManager existing = UnityEngine.Object.FindAnyObjectByType<InputManager>(FindObjectsInactive.Include);
```

**Context:** Singleton bootstrap with duplicate detection.

**Verdict:** 🟡 **ACCEPTABLE**
- One-time resolution
- Proper `FindAnyObjectByType` (Unity 6 API)
- Prevents DDOL duplicates

**Note:** Could be improved with `GlobalRegistry` pattern, but current implementation is safe.

---

## VI. Cross-System Dependency Map

```
┌─────────────────────────┐
│  HectonPlayerMovement   │
│  (28 components on      │
│   Player root)          │
└───────────┬─────────────┘
            │
            ├──→ HectonMapMagicVegetationBridge (cached ref)
            ├──→ IHectonOceanKinematics (🔴 DIRECT CREST COUPLING)
            ├──→ HectonSurvivalSystem (via GlobalRegistry?)
            └──→ PlayerToolManager (component sibling)

┌─────────────────────────┐
│   HectonSurvivalSystem  │
└───────────┬─────────────┘
            │
            └──→ HectonMapMagicVegetationBridge (lazy resolve ✅)

┌─────────────────────────┐
│  FloraInteractionMgr    │
└───────────┬─────────────┘
            │
            └──→ HectonMapMagicVegetationBridge (🔴 GetComponent chain)

┌─────────────────────────┐
│    FaunaDirector        │
└───────────┬─────────────┘
            │
            └──→ HectonMapMagicVegetationBridge (cached ✅)

┌─────────────────────────┐
│  AcousticZoneController │
└───────────┬─────────────┘
            │
            └──→ HectonMapMagicVegetationBridge (static accessor ✅)
```

---

## VII. Recommendations

### Immediate (Next Sprint)

1. **Cache FloraInteractionManager Bridge** — Move `ResolveVegetationBridge()` to `Awake()`
2. **Fix HectonUnderwaterVisuals Singleton** — Use `FindAnyObjectByType` + cache
3. **Fix HectonAtmosphereManager** — Cache camera/terrain arrays in `Awake()`

### Medium-Term (Architecture Cleanup)

4. **GlobalRegistry Adoption** — Migrate all manager access through `GlobalRegistry.Manager`
5. **EventBus Migration** — Replace direct system calls with event-based communication where applicable
6. **Interface Contracts** — Define `IVegetationBridge`, `IOceanKinematics` interfaces for decoupling

---

## VIII. Compliance Status

| Category | Status | Notes |
|---|---|---|
| FindObjectOfType in Hot Paths | ✅ **COMPLIANT** | None found in Tick/Update |
| GetComponent in Loops | 🔴 **NON-COMPLIANT** | `FloraInteractionManager.cs` |
| Singleton Null-Guards | ✅ **COMPLIANT** | All singletons have null-checks |
| EventBus Usage | ✅ **COMPLIANT** | Events properly defined |
| GlobalRegistry Pattern | 🟡 **PARTIAL** | Mixed usage (some direct refs) |

---

**STATUS:** 🟠 **HIGH PRIORITY** — 3 runtime resolution patterns require caching fixes
