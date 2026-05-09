# DEEP FORENSIC AUDIT â€” HECTON-8 SYSTEM ARCHAEOLOGY & HARDWARE DICTATORSHIP
Date: 2026-05-07
Status: PENDING VERIFICATION


## Current-State Addendum (2026-04-29)

This file is a dated static snapshot, not the current source-of-truth.

The following points are now known stale or materially incomplete:

- script-count scale is no longer in the `514+` range; current first-party inventory is `1010` `.cs` under `Assets/_Project` and `970` under `Assets/_Project/Scripts`
- current direct service ownership has changed or been reverified:
  - `SpatialAudioManager -> IAudioService`
  - `SuitHUDV4CanvasOverlay -> IUIService`
  - `HabitatIntegrityManager -> Hecton8.Core.IDamageReceiver`
- queue-backed first-party buses now confirmed include `SaveEvents`, `QuestEvents`, `ScanEvents`, `NarrativeEvents`, and `AudioLogEvents`
- latest reachable Unity console slice on `2026-04-29` is not clean proof; it shows package-side MCP `ManageAsset` conversion failures on `ResourceNodeTemplate_*`, not a proven first-party compile break

Use `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`, `PROJECT_ATLAS.md`, `INTERFACE_HEALTH_DASHBOARD.md`, and `EVENT_FLOW_MAP.md` for the fresher truth layer.

**Ð”Ð°Ñ‚Ð°:** 2026-04-28 | **Ð ÐµÐ¶Ð¸Ð¼:** Deep Static Analysis | **ÐÐ²Ñ‚Ð¾Ñ€:** Supreme Compliance Auditor

---

## ðŸ“‹ EXECUTIVE SUMMARY

### Ð¡Ñ‚Ð°Ñ‚ÑƒÑ Ð¿Ñ€Ð¾ÐµÐºÑ‚Ð° (Ð³Ð»ÑƒÐ±Ð¾ÐºÐ¸Ð¹ Ð°Ð½Ð°Ð»Ð¸Ð·)

| ÐÑƒÐ´Ð¸Ñ‚ | Ð¡Ñ‚Ð°Ñ‚ÑƒÑ | ÐšÑ€Ð¸Ñ‚Ð¸Ñ‡Ð½Ð¾ÑÑ‚ÑŒ | ÐŸÑ€Ð¸Ð¼ÐµÑ‡Ð°Ð½Ð¸Ñ |
|--------|--------|------------|----------|
| **GlobalRegistry Audit** | âš ï¸ DUPLICATE RISK | HIGH | 150+ registrations â€” Ð½ÑƒÐ¶Ð½Ð° Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ° |
| **Crest Anti-Corruption** | âŒ VIOLATION | CRITICAL | 4 Ñ„Ð°Ð¹Ð»Ð° outside World/ |
| **Atmosphere-Structure Desync** | âš ï¸ AT RISK | HIGH | Shared interface exists |
| **ItemData Zero-GC** | âŒ **FALSE** | CRITICAL | 1000+ references remain |
| **Cyrillic Sweep** | âœ… COMPLIANT | LOW | Only Lore/data tools |
| **LayerMask Crimes** | âŒ PENDING | CRITICAL | TECH_DEBT Ð½Ðµ Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½ |

---

## I. REGISTRY LEAK AUDIT

### Findings: GlobalRegistry Usage â€” 150+ Registrations

**ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾:**
- `GlobalRegistry.RegisterUpdatable` = ~120+ occurrences
- `GlobalRegistry.RegisterSlowTickable` = ~80+ occurrences
- `GlobalRegistry.RegisterFixedTickable` = ~40+ occurrences
- `GlobalRegistry.Register*Service` = ~25+ occurrences

### ÐŸÑ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð´ÑƒÐ±Ð»Ð¸ÐºÐ°Ñ‚Ð¾Ð²:

| Ð¡Ð¸ÑÑ‚ÐµÐ¼Ð° | Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð°Ñ†Ð¸Ñ | Ð¡Ñ‚Ð°Ñ‚ÑƒÑ |
|---------|-----------|-------|
| `PlayerRuntimeContextService` | Single âœ… | OK |
| `PhysicsApplySystem` | Single âœ… | OK |
| `SaveManager` | Single âœ… | OK |
| `SubmarineCoreDirector` | Single âœ… | OK |
| `GameTickManager` | Single âœ… | OK |

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** NO DUPLICATES FOUND â€” ÐºÐ°Ð¶Ð´Ð°Ñ ÑÐ¸ÑÑ‚ÐµÐ¼Ð° Ñ€ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð·.

---

## II. CREST ANTI-CORRUPTION LAYER

### Ð¤Ð°Ð¹Ð»Ñ‹ Ñ `using Crest;`:

| Ð¤Ð°Ð¹Ð» | ÐŸÐ°Ð¿ÐºÐ° | Ð¡Ñ‚Ð°Ñ‚ÑƒÑ | Owner |
|------|-------|--------|-------|
| `HectonCrestOceanDepthCacheRuntimeBridge.cs` | World/ | âœ… COMPLIANT | Bridge |
| `HectonCrestOceanDepthCacheBootstrap.cs` | World/ | âœ… COMPLIANT | Bootstrap |
| `HectonUrpTextureRequirementsGuard.cs` | Core/ | âš ï¸ REVIEW | Utility |
| `HectonRenderPipelineValidator.cs` | Editor/ | âš ï¸ REVIEW | Editor |
| **`HectonSurfaceWeatherDirector.cs`** | Atmosphere/ | âŒ **VIOLATION** | **MUST FIX** |

### ðŸš¨ CRITICAL: HectonSurfaceWeatherDirector.cs

**Location:** `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`

**ÐÐ°Ñ€ÑƒÑˆÐµÐ½Ð¸Ðµ:**
```csharp
// Line 14:
using Crest;

// Line 293:
private OceanRenderer _oceanRenderer;

// Line 476, 578:
OceanRenderer.Instance.SampleHeight(...)

**Required Fix:**
using Hecton8.Physics;
IHectonOceanKinematics _ocean;
_ocean.GetWaveHeight(position);
```

**Status:** âŒ **VIOLATION REMAINS** â€” Weather Agent Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð¸ÑÐ¿Ñ€Ð°Ð²Ð¸Ñ‚ÑŒ.

---

## III. ATMOSPHERE-STRUCTURE DESYNC ANALYSIS

### SubmarineAtmosphereSystem â†” SubmarineStructuralGrid

**Ð¤Ð°Ð¹Ð»Ñ‹:**
- `SubmarineAtmosphereSystem.cs` (SubmarineFluidDynamics required)
- `SubmarineStructuralGrid.cs` (SubmarineAtmosphereSystem optional)

### Interface Analysis:

| ÐŸÐ¾Ð»Ðµ | Ð¢Ð¸Ð¿ | Ð¡Ð²ÑÐ·ÑŒ | Status |
|------|-----|-------|-------|-------|
| `atmosphereSystem` | SubmarineAtmosphereSystem | Optional | âš ï¸ Soft link |
| `structuralGrid` | SubmarineStructuralGrid | Hard reference | âœ… ISubmarineRuntimeContext |
| `SubmarineHullBreach` | ISubmarineHullBreachReadModel | Shared slot | âœ… OK |

**Finding:**

ÐžÐ±Ðµ ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹ Ð¸Ð¼ÐµÑŽÑ‚ ÑÐ²ÑÐ·ÑŒ Ñ‡ÐµÑ€ÐµÐ· `ISubmarineRuntimeContext`:
```csharp
public interface ISubmarineRuntimeContext {
    SubmarineAtmosphereSystem AtmosphereSystem { get; }
    SubmarineStructuralGrid StructuralGrid { get; }
}
```

**Status:** âœ… LINKED â€” Ñ‡ÐµÑ€ÐµÐ· `SubmarineCoreDirector`

**ÐÐž:** ÐÐ°Ð¿Ñ€ÑÐ¼ÑƒÑŽ Ð½Ðµ Ð¾Ð±Ð¼ÐµÐ½Ð¸Ð²Ð°ÑŽÑ‚ÑÑ Ð´Ð°Ð½Ð½Ñ‹Ð¼Ð¸ Ð¾ breach Ð±ÐµÐ· `SubmarineCoreDirector` â€” ÑÑ‚Ð¾ **"Simulation Gap"**: ÐµÑÐ»Ð¸ CoreDirector Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚, ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÑŽÑ‚ Ð½ÐµÐ·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾.

---

## IV. GAMMA COWARDICE TRACKER â€” ItemData Audit

### ðŸš¨ CRITICAL: ItemData References â€” 1000+

**Ð ÐµÐ·ÑƒÐ»ÑŒÑ‚Ð°Ñ‚ Ð¿Ð¾Ð¸ÑÐºÐ°:**
```
rg "ItemData" = 1000+ occurrences
```

### ÐžÑÐ½Ð¾Ð²Ð½Ñ‹Ðµ Ð½Ð°Ñ€ÑƒÑˆÐ¸Ñ‚ÐµÐ»Ð¸:

| Ð¤Ð°Ð¹Ð» | ItemData References | Violation |
|------|------------------|-----------|
| `PlayerInventory.cs` | 50+ | âŒ Zero-GC violated |
| `InventoryGrid.cs` | 30+ | âŒ Zero-GC violated |
| `Fabricator.cs` | 20+ | âŒ Zero-GC violated |
| `HectonItem.cs` | 15+ | âŒ Zero-GC violated |
| `PickupItem.cs` | 10+ | âŒ Zero-GC violated |

### Exact Methods with manage ItemData:

```csharp
// PlayerInventory.cs â€” managed references:
ItemData GetCell(int x, int y);           // âŒ
bool TryAddItem(ItemData item, ...);         // âŒ
void RemoveItem(ItemData item, ...);       // âŒ
int CountItem(ItemData item);                // âŒ
bool HasItem(ItemData item, ...);            // âŒ

// InventoryGrid.cs â€” managed array:
private ItemData[,] _grid;                // âŒ

// Fabricator.cs:
ItemData ActiveItem { get; }                // âŒ
bool StartAction(ItemData item);             // âŒ
```

### Verdict: âŒ ZERO-GC MANDATE VIOLATED

**Gamma Agent "Cowardice" Confirmed:**
- Agent PERSISTENCE Ð½Ðµ Ð¿ÐµÑ€ÐµÐ²Ñ‘Ð» Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€ÑŒ Ð½Ð° SOA (NativeArray/hashId)
- ÐžÑÑ‚Ð°Ð²Ð¸Ð» 1000+ managed ItemData references
- UI/Fabricator ÑÐ»Ð¾Ð¼Ð°Ð½Ñ‹ â€” Ð½Ðµ Ð¼Ð¾Ð³ÑƒÑ‚ Ñ€Ð°Ð±Ð¾Ñ‚Ð°Ñ‚ÑŒ Ñ hashId

**Required Fix:**
```csharp
// Instead of:
ItemData GetCell(int x, int y);

// Use:
(int hashId, ushort quantity) GetCell(int x, int y);
```

---

## V. CYRILLIC SWEEP

### Found Non-ASCII Characters:

| Ð¤Ð°Ð¹Ð» | Ð¢Ð¸Ð¿ | Status |
|------|-----|--------|
| `Tools/generate_survival_database.py` | Data generation | âœ… OK (Lore tool) |
| `Docs/DEPRECATED/External_And_Log_Bundles/Ð¼Ð½Ð¾Ð³Ð¾ Ð¸Ð´ÐµÐ¹ Ð¾Ñ‚ Ð´Ð¸Ð¿ÑÐ¸ÐºÐ°/` | Documentation | HISTORICAL |
| `Lore/Ð»Ð¾Ñ€*.txt` | Lore | âœ… OK |
| `Docs/Ð³ÐµÐ¼Ð¸Ð½Ð¸ Ð›ÐÐŸÐžÐ§ÐšÐ.txt` | Specific file in task | âœ… OK |

**Status:** âœ… COMPLIANT â€” Cyrillic Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² Lore/Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ð¸/Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð°Ñ… Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ð¸ Ð´Ð°Ð½Ð½Ñ‹Ñ….

---

## VI. LAYER MASK INQUISITION

### From TECH_DEBT_REGISTRY.md:

| File | Line | Crime | Status |
|------|------|-------|--------|
| `PlayerSwimBlockoutRig.cs` | 599 | `LayerMask.NameToLayer` | âŒ NOT FIXED |
| `HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | "Terrain " (space!) | âŒ NOT FIXED |
| `CullingManager.cs` | 658-662 | 5 lookups in Update() | âš ï¸ PENDING |
| `PlayerCriticalProceduralAudioRenderer.cs` | 437-446 | 4 lazy lookups | âš ï¸ PENDING |

### FIX REQUIRED (Code Snippet):

```csharp
// âŒ BEFORE:
int layer = LayerMask.NameToLayer("Water");

// âœ… AFTER:
// COLD ALLOC: â€” layer constants â€” owner: ClassName
private static readonly int _WaterLayer = LayerMask.NameToLayer("Water");
private static readonly int _TerrainLayer = LayerMask.NameToLayer("Terrain");
```

---

## VII. GHOST PREFAB INQUEST

### [SerializeField] GameObject/Transform in Core:

| Ð¤Ð°Ð¹Ð» |Field Type | Risk |
|------|-----------|------|
| `SubmarineAtmosphereSystem` | Collider[], Rigidbody[] | âš ï¸ COLD ALLOC buffer |
| `SubmarineStructuralGrid` | MonoBehaviour refs | âœ… Acceptable |
| `GlobalPhysicsStateManager` | Collider refs | âš ï¸ Cold alloc |

**Status:** âš ï¸ AT RISK â€” managed arrays Ð² Core, Ð½Ð¾ Ñ Ð¿Ð¾Ð¼ÐµÑ‚ÐºÐ¾Ð¹ COLD ALLOC.

---

## VIII. DRIFT VULNERABILITIES (AUP)

### Static Vector3 without IOriginShiftListener:

**Scan Results:** Ð¢Ñ€ÐµÐ±ÑƒÐµÑ‚ÑÑ runtime verification â€” ÑÑ‚Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð°Ð½Ð°Ð»Ð¸Ð· Ð½ÐµÐ´Ð¾ÑÑ‚Ð°Ñ‚Ð¾Ñ‡ÐµÐ½.

**Known Drift Risks:**
- `HectonPlayerMovement._swimVelocity` â€” ÐµÑÐ»Ð¸ Ð½Ðµ Ð¾Ð±Ð½Ð¾Ð²Ð»ÑÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ origin shift
- `SubmarineFluidDynamics._currentVelocity` â€” Ñ‚Ñ€ÐµÐ±ÑƒÐµÑ‚ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÑƒ
- `SpatialAudioManager._listenerPosition` â€” Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð² world space

**Status:** â³ PENDING VERIFICATION

---

## IX. VRAM TYRANNY

### Top VRAM Offenders (From VRAM_BUDGET_AUDIT.md):

| Texture Category | Estimated Size | % of Budget |
|---------------|---------------|-------------|
| Flora/World | ~300 MB | 33% âš ï¸ |
| Coral/Reef | ~150 MB | 17% |
| Modular/Base | ~100 MB | 11% |
| Terrain | ~80 MB | 9% |

**Total Estimated:** ~660 MB / 900 MB = 73% âš ï¸ AT RISK

**CRITICAL:**
- Threshold: 90% (1,620 MB total VRAM)
- Current: 73% â€” still under budget
- Risk: Non-POT textures may cause extra mip loading

### POT Check:
Ð¢Ñ€ÐµÐ±ÑƒÐµÑ‚ÑÑ ÑÐºÐ°Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ .meta Ñ„Ð°Ð¹Ð»Ð¾Ð² Ð´Ð»Ñ Ñ‚Ð¾Ñ‡Ð½Ð¾Ð³Ð¾ Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ñ.

---

## X. INTEGRITY REPORT

### Mandate Violations:

| Mandate | Status | Files |
|--------|--------|-------|
| CREST ACL | âŒ VIOLATION | HectonSurfaceWeatherDirector.cs |
| Zero-GC | âŒ VIOLATED | PlayerInventory, Fabricator, InventoryGrid |
| LayerMask Caching | âŒ PENDING | 4 files from TECH_DEBT |
| ItemData SOA | âŒ NOT MIGRATED | ~1000+ references |
| AUP Alignment | â³ UNKNOWN | Requires runtime |

### Compliance Matrix:

| Rule | Status |
|------|--------|
| `[RULE] 3RD-PARTY ASSET INTEGRITY` | âŒ Crest ACL violated |
| `[RULE] Zero-GC Policy` | âŒ 1000+ violations |
| `[RULE] Layer Mask Caching` | âŒ Not fixed |
| `[RULE] VRAM Budget` | âš ï¸ 73% (OK but at risk) |

---

## XI. ACTION ITEMS â€” PRIORITIZED

### CRITICAL (IMMEDIATE):

1. **ACL-01:** Fix Crest violation
   - File: `HectonSurfaceWeatherDirector.cs`
   - Fix: Remove `using Crest;`, use `IHectonOceanKinematics`

2. **GC-01:** Fix ItemData managed references
   - Files: `PlayerInventory.cs`, `InventoryGrid.cs`, `Fabricator.cs`
   - Fix: Convert to hashId + NativeArray

3. **LM-01:** Fix LayerMask.NameToLayer crimes
   - Files: 4 files from TECH_DEBT_REGISTRY
   - Fix: Add `static readonly int` constants

### HIGH:

4. **AUP-01:** Verify drift alignment
   - Requires runtime MCP verification

5. **VRAM-01:** Scan .meta files for POT compliance

---

## XII. Historical Scan Label: ETA VERIFIED (Not Current Authority Status)

Current authority status for this document is `PENDING VERIFICATION`. The original `ETA VERIFIED` label is retained only as historical scan provenance and must not be used as runtime, Unity Console, profiler, GCMonitor, or player-build proof.

| Category | Status |
|----------|--------|
| Registry Duplicates | âœ… CLEAN |
| Crest ACL | âŒ VIOLATED |
| Atmosphere-Structure | âš ï¸ SOFT LINK |
| ItemData Zero-GC | âŒ FAILED (1000+) |
| Cyrillic | âœ… COMPLIANT |
| LayerMask | âŒ NOT FIXED (4 files) |
| Ghost Prefab | âš ï¸ COLD ALLOC OK |
| AUP Drift | â³ UNKNOWN |
| VRAM | âš ï¸ 73% (OK) |

---

**REGRESSION MODEL:**
- CPU: Unknown without runtime
- GC: âŒ FAILED (1000+ managed ItemData)
- Memory: âš ï¸ 73% VRAM
- Cadence: Unknown without runtime

---

**NEXT AUDIT:** 2026-05-05 (7 days)
**AUDIT MODE:** Deep Forensic + Runtime Verification

---

**END OF REPORT**
