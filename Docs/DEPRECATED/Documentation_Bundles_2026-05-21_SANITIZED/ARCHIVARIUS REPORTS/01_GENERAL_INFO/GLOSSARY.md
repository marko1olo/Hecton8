# HECTON-8 â€” GLOSSARY OF TERMS
Batch007 warning: [DEPRECATED] for runtime authority. Old snippets in this glossary that show `Update()` or direct `GlobalRegistry` access inside `Update()` are rejected. Use `.agents-skills/ARCH_Execution_Phases.txt`, `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, and `.agents-skills/ARCH_Signal_Lane_Segregation.txt`.

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


**Ð’ÐµÑ€ÑÐ¸Ñ:** 1.0.0 | **Ð”Ð°Ñ‚Ð°:** 2026-04-28 | **ÐÐ²Ñ‚Ð¾Ñ€:** Supreme Compliance Auditor

---

## ðŸ“‹ TABLE OF CONTENTS

1. [ÐÑ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð½Ñ‹Ðµ Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ñ‹](#1-Ð°Ñ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð½Ñ‹Ðµ-Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ñ‹)
2. [ÐœÐ°Ñ‚ÐµÐ¼Ð°Ñ‚Ð¸ÐºÐ° Ð¸ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹](#2-Ð¼Ð°Ñ‚ÐµÐ¼Ð°Ñ‚Ð¸ÐºÐ°-Ð¸-ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹)
3. [ÐžÐ¿Ñ‚Ð¸Ð¼Ð¸Ð·Ð°Ñ†Ð¸Ñ Ð¸ Ð¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ÑÑ‚ÑŒ](#3-Ð¾Ð¿Ñ‚Ð¸Ð¼Ð¸Ð·Ð°Ñ†Ð¸Ñ-Ð¸-Ð¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ÑÑ‚ÑŒ)
4. [Ð¡Ð¸ÑÑ‚ÐµÐ¼Ñ‹ Ð¸ ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ñ‹](#4-ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹-Ð¸-ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ñ‹)
5. [Ð¢Ñ€ÐµÑ‚ÑŒÑ ÑÑ‚Ð¾Ñ€Ð¾Ð½Ð° (Third-Party)](#5-Ñ‚Ñ€ÐµÑ‚ÑŒÑ-ÑÑ‚Ð¾Ñ€Ð¾Ð½Ð°-third-party)
6. [ÐŸÑ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ð°Ñ Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ñ](#6-Ð¿Ñ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ð°Ñ-Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ñ)
7. [ÐÑƒÐ´Ð¸Ð¾ Ð¸ DSP](#7-Ð°ÑƒÐ´Ð¸Ð¾-Ð¸-dsp)
8. [Ð ÐµÐ½Ð´ÐµÑ€Ð¸Ð½Ð³ Ð¸ Ð³Ñ€Ð°Ñ„Ð¸ÐºÐ°](#8-Ñ€ÐµÐ½Ð´ÐµÑ€Ð¸Ð½Ð³-Ð¸-Ð³Ñ€Ð°Ñ„Ð¸ÐºÐ°)

---

## 1. ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð ÐÐ«Ð• Ð¢Ð•Ð ÐœÐ˜ÐÐ«

### AUP (Absolute Universe Position)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¡Ð¸ÑÑ‚ÐµÐ¼Ð° ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚ Ñ Ð¿Ð»Ð°Ð²Ð°ÑŽÑ‰Ð¸Ð¼ Ð½Ð°Ñ‡Ð°Ð»Ð¾Ð¼ (Floating Origin), Ð³Ð´Ðµ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ Ñ…Ñ€Ð°Ð½Ð¸Ñ‚ÑÑ ÐºÐ°Ðº `int64x3 grid_sector + float3 local_offset`.

**ÐÐ°Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ:** Ð˜Ð·Ð±ÐµÐ³Ð°Ð½Ð¸Ðµ Ð¿Ñ€Ð¾Ð±Ð»ÐµÐ¼ Ñ Ñ‚Ð¾Ñ‡Ð½Ð¾ÑÑ‚ÑŒÑŽ float Ð½Ð° Ð±Ð¾Ð»ÑŒÑˆÐ¸Ñ… Ñ€Ð°ÑÑÑ‚Ð¾ÑÐ½Ð¸ÑÑ… (>10 ÐºÐ¼ Ð¾Ñ‚ Ð½Ð°Ñ‡Ð°Ð»Ð° ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚).

**ÐŸÑ€Ð¸Ð¼ÐµÑ€ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ:**
```csharp
struct AUPosition {
    long3 gridSector;    // 64-bit integer grid cell
    float3 localOffset;  // Local position within cell (0-1024 units)
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `HectonFloatingOrigin.cs`

---

### SOA (Structure of Arrays)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐŸÐ°Ñ‚Ñ‚ÐµÑ€Ð½ Ð¾Ñ€Ð³Ð°Ð½Ð¸Ð·Ð°Ñ†Ð¸Ð¸ Ð´Ð°Ð½Ð½Ñ‹Ñ…, Ð³Ð´Ðµ Ð²Ð¼ÐµÑÑ‚Ð¾ Ð¼Ð°ÑÑÐ¸Ð²Ð° ÑÑ‚Ñ€ÑƒÐºÑ‚ÑƒÑ€ (AoS) Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ ÑÑ‚Ñ€ÑƒÐºÑ‚ÑƒÑ€Ð° Ð¼Ð°ÑÑÐ¸Ð²Ð¾Ð².

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// âŒ AoS (Array of Structures) â€” Ð¿Ð»Ð¾Ñ…Ð¾ Ð´Ð»Ñ ÐºÑÑˆ-Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ð¾ÑÑ‚Ð¸
class Item { float weight; int id; string name; }
Item[] items = new Item[1000];

// âœ… SOA (Structure of Arrays) â€” Ñ…Ð¾Ñ€Ð¾ÑˆÐ¾ Ð´Ð»Ñ ÐºÑÑˆ-Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ð¾ÑÑ‚Ð¸
struct ItemData {
    NativeArray<float> weights;  // [1000]
    NativeArray<int> ids;        // [1000]
    NativeArray<int> nameHashes; // [1000]
}
```

**ÐŸÑ€ÐµÐ¸Ð¼ÑƒÑ‰ÐµÑÑ‚Ð²Ð°:**
- Ð›ÑƒÑ‡ÑˆÐ°Ñ ÐºÑÑˆ-Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ð¾ÑÑ‚ÑŒ Ð¿Ñ€Ð¸ Ð¸Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ð¸ Ð¿Ð¾ Ð¾Ð´Ð½Ð¾Ð¼Ñƒ Ð¿Ð¾Ð»ÑŽ
- Ð¡Ð¾Ð²Ð¼ÐµÑÑ‚Ð¸Ð¼Ð¾ÑÑ‚ÑŒ Ñ Unity Job System Ð¸ Burst
- Zero-GC Ð¿Ñ€Ð¸ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ð¸ NativeArray

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `DATA_Inventory_Resources_Items_SOA_Layout.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### DOD (Data-Oriented Design)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐÑ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð½Ñ‹Ð¹ Ð¿Ð¾Ð´Ñ…Ð¾Ð´, Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ð½Ð° Ð´Ð°Ð½Ð½Ñ‹Ðµ, Ð° Ð½Ðµ Ð½Ð° Ð¾Ð±ÑŠÐµÐºÑ‚Ñ‹. ÐŸÑ€Ð¾Ñ‚Ð¸Ð²Ð¾Ð¿Ð¾Ð»Ð¾Ð¶Ð½Ð¾ÑÑ‚ÑŒ ÐžÐžÐŸ.

**ÐŸÑ€Ð¸Ð½Ñ†Ð¸Ð¿Ñ‹:**
1. **Cache-line alignment:** Ð”Ð°Ð½Ð½Ñ‹Ðµ Ñ€Ð°ÑÐ¿Ð¾Ð»Ð°Ð³Ð°ÑŽÑ‚ÑÑ Ð² Ð¿Ð°Ð¼ÑÑ‚Ð¸ Ð¿Ð¾ÑÐ»ÐµÐ´Ð¾Ð²Ð°Ñ‚ÐµÐ»ÑŒÐ½Ð¾
2. **Separation of data and behavior:** Ð”Ð°Ð½Ð½Ñ‹Ðµ Ð¾Ñ‚Ð´ÐµÐ»ÐµÐ½Ñ‹ Ð¾Ñ‚ Ð»Ð¾Ð³Ð¸ÐºÐ¸
3. **Batch processing:** ÐžÐ±Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ° Ð´Ð°Ð½Ð½Ñ‹Ñ… Ð¿Ð°ÐºÐµÑ‚Ð°Ð¼Ð¸ (Job System)
4. **Minimal indirection:** Ð˜Ð·Ð±ÐµÐ³Ð°Ð½Ð¸Ðµ ÑƒÐºÐ°Ð·Ð°Ñ‚ÐµÐ»ÐµÐ¹ Ð¸ ÑÑÑ‹Ð»Ð¾Ñ‡Ð½Ñ‹Ñ… Ñ‚Ð¸Ð¿Ð¾Ð²

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// âŒ ÐžÐžÐŸ Ð¿Ð¾Ð´Ñ…Ð¾Ð´ (Ð¿Ð»Ð¾Ñ…Ð¾)
class Creature : MonoBehaviour {
    void Update() { /* AI logic */ }
}

// âœ… DOD Ð¿Ð¾Ð´Ñ…Ð¾Ð´ (Ñ…Ð¾Ñ€Ð¾ÑˆÐ¾)
struct CreatureData {
    public float3 position;
    public float health;
    public int stateFlags;
}

class CreatureSystem : ISystem {
    NativeArray<CreatureData> _creatures;
    public void Update(float dt) {
        // Process all creatures in a single Job
    }
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Service Locator Pattern (GlobalRegistry)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐŸÐ°Ñ‚Ñ‚ÐµÑ€Ð½ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð° Ðº ÑÐµÑ€Ð²Ð¸ÑÐ°Ð¼ Ñ‡ÐµÑ€ÐµÐ· Ñ†ÐµÐ½Ñ‚Ñ€Ð°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ñ€ÐµÐµÑÑ‚Ñ€ Ð²Ð¼ÐµÑÑ‚Ð¾ Ð¿Ñ€ÑÐ¼Ñ‹Ñ… ÑÑÑ‹Ð»Ð¾Ðº.

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// âŒ ÐŸÑ€ÑÐ¼Ð°Ñ Ð·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾ÑÑ‚ÑŒ (Ð¿Ð»Ð¾Ñ…Ð¾)
public class Player : MonoBehaviour {
    private AudioManager _audio;
    void Awake() => _audio = FindObjectOfType<AudioManager>();
}

// âœ… Service Locator (Ñ…Ð¾Ñ€Ð¾ÑˆÐ¾)
public class Player : MonoBehaviour {
    void Update() => GlobalRegistry.Audio.PlaySFX(sfxId);
}
```

**ÐŸÑ€ÐµÐ¸Ð¼ÑƒÑ‰ÐµÑÑ‚Ð²Ð°:**
- Loose coupling Ð¼ÐµÐ¶Ð´Ñƒ ÑÐ¸ÑÑ‚ÐµÐ¼Ð°Ð¼Ð¸
- Easy testing (Ð¼Ð¾Ð¶Ð½Ð¾ Ð¿Ð¾Ð´Ð¼ÐµÐ½Ð¸Ñ‚ÑŒ ÑÐµÑ€Ð²Ð¸Ñ Ð½Ð° mock)
- ÐÐµÑ‚ FindObjectOfType Ð² runtime

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

---

### Bridge Pattern (Anti-Corruption Layer)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐŸÐ°Ñ‚Ñ‚ÐµÑ€Ð½ Ð¸Ð·Ð¾Ð»ÑÑ†Ð¸Ð¸ third-party ÐºÐ¾Ð´Ð° Ð¾Ñ‚ Ð¿ÐµÑ€Ð²Ð¾Ð¹ ÑÑ‚Ð¾Ñ€Ð¾Ð½Ñ‹ Ñ‡ÐµÑ€ÐµÐ· Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ-Ð¿Ñ€Ð¾ÑÐ»Ð¾Ð¹ÐºÑƒ.

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// âœ… Anti-Corruption Layer Ð´Ð»Ñ Crest
public interface IHectonOceanKinematics {
    float GetWaveHeight(float3 position);
    float3 GetWaterVelocity(float3 position);
}

public class HectonCrestOceanKinematics : IHectonOceanKinematics {
    // using Crest; â€” Ð¢ÐžÐ›Ð¬ÐšÐž Ð—Ð”Ð•Ð¡Ð¬
    public float GetWaveHeight(float3 pos) => OceanRenderer.Instance.SampleHeight(pos);
}

// âœ… Gameplay-ÐºÐ¾Ð´ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ
public class PlayerMovement : MonoBehaviour {
    private IHectonOceanKinematics _ocean;
    void Awake() => _ocean = GlobalRegistry.OceanKinematics.ActiveProvider;
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `THIRD_PARTY_POISON.md`

---

## 2. ÐœÐÐ¢Ð•ÐœÐÐ¢Ð˜ÐšÐ Ð˜ ÐšÐžÐžÐ Ð”Ð˜ÐÐÐ¢Ð«

### Bishop Frame
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐŸÐ¾Ð´Ð²Ð¸Ð¶Ð½Ñ‹Ð¹ Ñ€ÐµÐ¿ÐµÑ€ Ð² Ð´Ð¸Ñ„Ñ„ÐµÑ€ÐµÐ½Ñ†Ð¸Ð°Ð»ÑŒÐ½Ð¾Ð¹ Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸Ð¸, Ð¾Ð¿Ð¸ÑÑ‹Ð²Ð°ÑŽÑ‰Ð¸Ð¹ Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð°Ñ†Ð¸ÑŽ ÐºÑ€Ð¸Ð²Ð¾Ð¹ Ð±ÐµÐ· Ð½ÐµÐ¾Ð´Ð½Ð¾Ð·Ð½Ð°Ñ‡Ð½Ð¾ÑÑ‚Ð¸ Ð²ÐµÐºÑ‚Ð¾Ñ€Ð° Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸ Ð¤Ñ€ÐµÐ½Ðµ Ð² Ñ‚Ð¾Ñ‡ÐºÐ°Ñ… Ð¿ÐµÑ€ÐµÐ³Ð¸Ð±Ð°.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Ð¤Ð¸Ð·Ð¸ÐºÐ° Ñ‚Ñ€Ð¾ÑÐ¾Ð²/ÐºÐ°Ð±ÐµÐ»ÐµÐ¹ (Verlet constraints) â€” twist-free Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ñ
- ÐŸÑ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ð°Ñ Ñ„Ð»Ð¾Ñ€Ð° (kelp, sargassum) â€” Ð¿Ð»Ð°Ð²Ð½Ð¾Ðµ Ñ€Ð°ÑÐ¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÐµÐ½Ð¸Ðµ Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ð¸ Ð¿Ð¾ Ð´Ð»Ð¸Ð½Ðµ ÑÑ‚ÐµÐ±Ð»Ñ

**ÐŸÑ€ÐµÐ¸Ð¼ÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¿ÐµÑ€ÐµÐ´ Frenet frame:**
- ÐÐµÑ‚ Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð¾Ñ‚Ð° Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸ Ð¿Ñ€Ð¸ Ð½ÑƒÐ»ÐµÐ²Ð¾Ð¹ ÐºÑ€Ð¸Ð²Ð¸Ð·Ð½Ðµ
- Ð¡Ñ‚Ð°Ð±Ð¸Ð»ÑŒÐ½Ð°Ñ Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ñ Ð´Ð»Ñ ÐºÐ¾Ð½ÑÑ‚Ñ€ÐµÐ¹Ð½Ñ‚Ð¾Ð²
- Ð¡Ð¾Ð²Ð¼ÐµÑÑ‚Ð¸Ð¼ Ñ Burst-Ð²ÐµÐºÑ‚Ð¾Ñ€Ð¸Ð·Ð°Ñ†Ð¸ÐµÐ¹

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `PHYS_Tether_Cable_Acceleration_Constraints.txt`

---

### Burst
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Unity Burst Compiler â€” Ð²Ñ‹ÑÐ¾ÐºÐ¾Ð¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¹ ÐºÐ¾Ð¼Ð¿Ð¸Ð»ÑÑ‚Ð¾Ñ€ Ð´Ð»Ñ C# Job System.

**Ð’Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾ÑÑ‚Ð¸:**
- ÐšÐ¾Ð¼Ð¿Ð¸Ð»ÑÑ†Ð¸Ñ Ð² Ð¾Ð¿Ñ‚Ð¸Ð¼Ð¸Ð·Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ð¼Ð°ÑˆÐ¸Ð½Ð½Ñ‹Ð¹ ÐºÐ¾Ð´ (SIMD)
- ÐŸÐ¾Ð´Ð´ÐµÑ€Ð¶ÐºÐ° float precision control (Fast/Standard/Precise)
- Ð¡Ñ‚Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð°Ð½Ð°Ð»Ð¸Ð· Ð½Ð° Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ÑÑ‚ÑŒ (no managed refs)

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
struct FluidJob : IJobParallelFor {
    public void Execute(int index) {
        // Burst-ÐºÐ¾Ð¼Ð¿Ð¸Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ ÐºÐ¾Ð´
        float result = math.sqrt(input[index]);
    }
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### NativeArray / NativeList / NativeHashMap
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐÐ¸Ð·ÐºÐ¾ÑƒÑ€Ð¾Ð²Ð½ÐµÐ²Ñ‹Ðµ ÐºÐ¾Ð»Ð»ÐµÐºÑ†Ð¸Ð¸ Unity Ð´Ð»Ñ Zero-GC Ñ€Ð°Ð±Ð¾Ñ‚Ñ‹ Ñ Ð¿Ð°Ð¼ÑÑ‚ÑŒÑŽ.

**Ð¢Ð¸Ð¿Ñ‹:**
- `NativeArray<T>` â€” Ñ„Ð¸ÐºÑÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ð¼Ð°ÑÑÐ¸Ð² (Ð±Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹ Ð´Ð¾ÑÑ‚ÑƒÐ¿ Ð¿Ð¾ Ð¸Ð½Ð´ÐµÐºÑÑƒ)
- `NativeList<T>` â€” Ð´Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ ÑÐ¿Ð¸ÑÐ¾Ðº (Ð°Ð½Ð°Ð»Ð¾Ð³ List<T> Ð±ÐµÐ· GC)
- `NativeHashMap<K,V>` â€” Ñ…ÑÑˆ-Ñ‚Ð°Ð±Ð»Ð¸Ñ†Ð° (Ð°Ð½Ð°Ð»Ð¾Ð³ Dictionary<K,V> Ð±ÐµÐ· GC)

**ÐÐ»Ð¾ÐºÐ°Ñ‚Ð¾Ñ€Ñ‹:**
- `Allocator.Temp` â€” Ð¾Ð´Ð¸Ð½ Ð¼ÐµÑ‚Ð¾Ð´, Ð°Ð²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´Ð°ÐµÑ‚ÑÑ
- `Allocator.TempJob` â€” Ð¾Ð´Ð¸Ð½ job cycle, Ñ‚Ñ€ÐµÐ±ÑƒÐµÑ‚ Dispose
- `Allocator.Persistent` â€” Ð¿ÐµÑ€ÑÐ¸ÑÑ‚ÐµÐ½Ñ‚Ð½Ñ‹Ð¹, Ñ‚Ñ€ÐµÐ±ÑƒÐµÑ‚ ÑÐ²Ð½Ð¾Ð³Ð¾ Dispose

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// âœ… ÐŸÑ€Ð°Ð²Ð¸Ð»ÑŒÐ½Ð¾Ðµ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ
NativeArray<float> _buffer = new NativeArray<float>(1024, Allocator.Persistent);
void OnDestroy() {
    _buffer.Dispose();
    _buffer = default;
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### Job System (IJob, IJobParallelFor)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¡Ð¸ÑÑ‚ÐµÐ¼Ð° Ð¼Ð½Ð¾Ð³Ð¾Ð¿Ð¾Ñ‚Ð¾Ñ‡Ð½Ð¾Ð¹ Ð¾Ð±Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ¸ Ð´Ð°Ð½Ð½Ñ‹Ñ… Unity.

**Ð¢Ð¸Ð¿Ñ‹ jobs:**
- `IJob` â€” Ð¾Ð´Ð½Ð¾Ð¿Ð¾Ñ‚Ð¾Ñ‡Ð½Ñ‹Ð¹ job
- `IJobParallelFor` â€” Ð¼Ð½Ð¾Ð³Ð¾Ð¿Ð¾Ñ‚Ð¾Ñ‡Ð½Ñ‹Ð¹ job (parallel for loop)
- `IJobChunk` â€” DOTS ECS job (Ð´Ð»Ñ Entity queries)

**ÐŸÑ€Ð°Ð²Ð¸Ð»Ð°:**
- Schedule() Ð² Ð½Ð°Ñ‡Ð°Ð»Ðµ ÐºÐ°Ð´Ñ€Ð°
- Complete() Ð² ÐºÐ¾Ð½Ñ†Ðµ ÐºÐ°Ð´Ñ€Ð° (Ð¸Ð»Ð¸ ÑÐ»ÐµÐ´ÑƒÑŽÑ‰ÐµÐ¼)
- âŒ Ð—ÐÐŸÐ Ð•Ð©Ð•ÐÐž: Schedule() + Complete() Ð² Ð¾Ð´Ð½Ð¾Ð¼ hot path

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### XXHash3
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð‘Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹ non-cryptographic Ñ…ÑÑˆ-Ð°Ð»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼ (SIMD-ÑƒÑÐºÐ¾Ñ€ÐµÐ½Ð½Ñ‹Ð¹).

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Checksum Ð´Ð»Ñ save files
- Hash Ð´Ð»Ñ procedural generation seeds
- Quick integrity checks

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// Native XXHash3 Ñ‡ÐµÑ€ÐµÐ· P/Invoke
uint hash = XXHash3.ComputeHash(data, length);
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

---

### Lotka-Volterra
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐœÐ°Ñ‚ÐµÐ¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ°Ñ Ð¼Ð¾Ð´ÐµÐ»ÑŒ Ñ…Ð¸Ñ‰Ð½Ð¸Ðº-Ð¶ÐµÑ€Ñ‚Ð²Ð° Ð´Ð»Ñ ÑÐ¸Ð¼ÑƒÐ»ÑÑ†Ð¸Ð¸ ÑÐºÐ¾ÑÐ¸ÑÑ‚ÐµÐ¼.

**Ð£Ñ€Ð°Ð²Ð½ÐµÐ½Ð¸Ñ:**
```
dx/dt = Î±x - Î²xy  (Ð¶ÐµÑ€Ñ‚Ð²Ñ‹)
dy/dt = Î´xy - Î³y  (Ñ…Ð¸Ñ‰Ð½Ð¸ÐºÐ¸)

Ð³Ð´Ðµ:
x = population prey
y = population predator
Î± = prey growth rate
Î² = predation rate
Î´ = predator growth from prey
Î³ = predator death rate
```

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Ð‘Ð°Ð»Ð°Ð½Ñ Ñ„Ð°ÑƒÐ½Ñ‹ Ð² Ð±Ð¸Ð¾Ð¼Ð°Ñ…
- Ð”Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ°Ñ Ñ€ÐµÐ³ÑƒÐ»ÑÑ†Ð¸Ñ Ð¿Ð¾Ð¿ÑƒÐ»ÑÑ†Ð¸Ð¹
- Emergent ecosystem behavior

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `AI_Creature_Cognition_States.txt`

---

## 3. ÐžÐŸÐ¢Ð˜ÐœÐ˜Ð—ÐÐ¦Ð˜Ð¯ Ð˜ ÐŸÐ ÐžÐ˜Ð—Ð’ÐžÐ”Ð˜Ð¢Ð•Ð›Ð¬ÐÐžÐ¡Ð¢Ð¬

### Zero-GC
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐžÑ‚ÑÑƒÑ‚ÑÑ‚Ð²Ð¸Ðµ allocations garbage collector Ð² hot paths (Tick, Update, FixedUpdate).

**Ð—Ð°Ð¿Ñ€ÐµÑ‰ÐµÐ½Ð¾ Ð² hot path:**
- `new class/List/Dict/array`
- LINQ (.Where, .Select, .Any, .FirstOrDefault, .ToList)
- string interpolation / concatenation
- boxing value types
- delegates / lambdas (capturing)
- StartCoroutine
- GetComponent<T>() uncached

**Ð Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ð¾:**
- NativeArray<T>, NativeList<T>
- struct allocations (Vector3, Color, Quaternion)
- cached delegates
- ITickable state machines

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Hot Path
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐšÐ¾Ð´, Ð²Ñ‹Ð¿Ð¾Ð»Ð½ÑÐµÐ¼Ñ‹Ð¹ ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€ (Tick, Update, LateUpdate, FixedUpdate).

**Ð‘ÑŽÐ´Ð¶ÐµÑ‚Ñ‹ (MX350 target):**
- Main thread: â‰¤12 ms
- GC: 0 B/frame
- SetPass calls: â‰¤600
- Batches: â‰¤1800
- Memory: â‰¤4096 MB total

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

---

### Cold Alloc
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐžÐ´Ð½Ð¾ÐºÑ€Ð°Ñ‚Ð½Ð°Ñ Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ñ Ð² Init (Awake/Start), Ð½Ðµ Ð² hot path.

**ÐšÐ°Ð½Ð¾Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ñ„Ð¾Ñ€Ð¼Ð°Ñ‚ ÐºÐ¾Ð¼Ð¼ÐµÐ½Ñ‚Ð°Ñ€Ð¸Ñ:**
```csharp
// COLD ALLOC: Type[capacity] â€” reason â€” owner: ClassName
private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
// COLD ALLOC: MaterialPropertyBlock[1] â€” per-renderer props â€” owner: self
```

**ÐŸÑ€Ð°Ð²Ð¸Ð»Ð°:**
- Ð¢Ð¾Ð»ÑŒÐºÐ¾ Ð² Awake/Start/OnEnable
- Ð¡ ÑÐ²Ð½Ñ‹Ð¼ ÑƒÐºÐ°Ð·Ð°Ð½Ð¸ÐµÐ¼ capacity Ð´Ð»Ñ ÐºÐ¾Ð»Ð»ÐµÐºÑ†Ð¸Ð¹
- Ð¡ ÐºÐ¾Ð¼Ð¼ÐµÐ½Ñ‚Ð°Ñ€Ð¸ÐµÐ¼ Ð¾ Ð¿Ñ€Ð¸Ñ‡Ð¸Ð½Ðµ Ð¸ Ð²Ð»Ð°Ð´ÐµÐ»ÑŒÑ†Ðµ

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Kahn's Topological Sort
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐÐ»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼ Ñ‚Ð¾Ð¿Ð¾Ð»Ð¾Ð³Ð¸Ñ‡ÐµÑÐºÐ¾Ð¹ ÑÐ¾Ñ€Ñ‚Ð¸Ñ€Ð¾Ð²ÐºÐ¸ Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð¾Ð³Ð¾ Ð°Ñ†Ð¸ÐºÐ»Ð¸Ñ‡ÐµÑÐºÐ¾Ð³Ð¾ Ð³Ñ€Ð°Ñ„Ð° (DAG) Ñ‡ÐµÑ€ÐµÐ· ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ Ð²ÐµÑ€ÑˆÐ¸Ð½ Ñ Ð½ÑƒÐ»ÐµÐ²Ð¾Ð¹ Ð²Ñ…Ð¾Ð´ÑÑ‰ÐµÐ¹ ÑÑ‚ÐµÐ¿ÐµÐ½ÑŒÑŽ.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- `SaveManager` â€” Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ Ð¿Ð¾Ñ€ÑÐ´ÐºÐ° Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ¸ `ISaveable` Ð¿Ð¾ `LoadPriority` Ñ Ñ€Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ð¸ÐµÐ¼ Ñ†Ð¸ÐºÐ»Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… Ð·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾ÑÑ‚ÐµÐ¹.
- `CraftingSystem` â€” ÑƒÐ¿Ð¾Ñ€ÑÐ´Ð¾Ñ‡Ð¸Ð²Ð°Ð½Ð¸Ðµ Ñ€ÐµÑ†ÐµÐ¿Ñ‚Ð¾Ð² ÐºÑ€Ð°Ñ„Ñ‚Ð° Ð¿Ð¾ Ð·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾ÑÑ‚ÑÐ¼ Ð¸Ð½Ð³Ñ€ÐµÐ´Ð¸ÐµÐ½Ñ‚Ð¾Ð².
- `PowerGrid` â€” Ð²Ð°Ð»Ð¸Ð´Ð°Ñ†Ð¸Ñ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²Ð¸Ñ Ñ†Ð¸ÐºÐ»Ð¾Ð² Ð² ÑÐ½ÐµÑ€Ð³Ð¾ÑÐµÑ‚Ð¸ Ð¿ÐµÑ€ÐµÐ´ Ñ€Ð°ÑÑ‡Ñ‘Ñ‚Ð¾Ð¼ Ð¿Ð¾Ñ‚Ð¾ÐºÐ°.

**Ð¡Ð»Ð¾Ð¶Ð½Ð¾ÑÑ‚ÑŒ:** O(V + E) â€” Ð»Ð¸Ð½ÐµÐ¹Ð½Ð°Ñ Ð¾Ñ‚ Ñ‡Ð¸ÑÐ»Ð° Ð²ÐµÑ€ÑˆÐ¸Ð½ Ð¸ Ñ€Ñ‘Ð±ÐµÑ€.

**ÐŸÑÐµÐ²Ð´Ð¾ÐºÐ¾Ð´:**
```csharp
Queue<int> zeroInDegree = new Queue<int>();
foreach (var node in graph)
    if (node.InDegree == 0) zeroInDegree.Enqueue(node.Id);

while (zeroInDegree.Count > 0)
{
    int current = zeroInDegree.Dequeue();
    sorted.Add(current);
    foreach (int neighbor in graph.Adjacent(current))
    {
        neighbor.InDegree--;
        if (neighbor.InDegree == 0)
            zeroInDegree.Enqueue(neighbor.Id);
    }
}
// Ð•ÑÐ»Ð¸ sorted.Count < graph.Count â†’ Ñ†Ð¸ÐºÐ» detected â†’ LogError + disable.
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `SaveManager.cs`, `CraftingSystem.cs`, `PowerGrid.cs`

---

### Torricelli Damping
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¤Ð¸Ð·Ð¸Ñ‡ÐµÑÐºÐ°Ñ Ð¼Ð¾Ð´ÐµÐ»ÑŒ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ñ, Ð¾ÑÐ½Ð¾Ð²Ð°Ð½Ð½Ð°Ñ Ð½Ð° Ð·Ð°ÐºÐ¾Ð½Ðµ Ð¢Ð¾Ñ€Ñ€Ð¸Ñ‡ÐµÐ»Ð»Ð¸ Ð´Ð»Ñ Ð¸ÑÑ‚ÐµÑ‡ÐµÐ½Ð¸Ñ Ð¶Ð¸Ð´ÐºÐ¾ÑÑ‚Ð¸ Ñ‡ÐµÑ€ÐµÐ· Ð¾Ñ‚Ð²ÐµÑ€ÑÑ‚Ð¸Ðµ, Ð°Ð´Ð°Ð¿Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð°Ñ Ð´Ð»Ñ Ð´ÐµÐ¼Ð¿Ñ„Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ ÑÐºÐ¾Ñ€Ð¾ÑÑ‚Ð¸ Ð² Ð²Ð¾Ð´Ð½Ð¾Ð¹ ÑÑ€ÐµÐ´Ðµ.

**Ð¤Ð¾Ñ€Ð¼ÑƒÐ»Ð°:**
```
v_new = v_old * (1 - k * sqrt(|v_old|) * dt)

gÐ´Ðµ:
k = ÐºÐ¾ÑÑ„Ñ„Ð¸Ñ†Ð¸ÐµÐ½Ñ‚ Ð´ÐµÐ¼Ð¿Ñ„Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ ÑÑ€ÐµÐ´Ñ‹ (Ð²Ð¾Ð´Ñ‹ / Ð²ÑÐ·ÐºÐ¾ÑÑ‚Ð¸)
|v_old| = Ð¼Ð¾Ð´ÑƒÐ»ÑŒ ÑÐºÐ¾Ñ€Ð¾ÑÑ‚Ð¸
```

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- `PlayerMovement` â€” Ð¿Ð»Ð°Ð²Ð½Ð¾Ðµ Ñ‚Ð¾Ñ€Ð¼Ð¾Ð¶ÐµÐ½Ð¸Ðµ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð² Ð²Ð¾Ð´Ðµ Ð±ÐµÐ· Ñ€ÐµÐ·ÐºÐ¸Ñ… Ñ€Ñ‹Ð²ÐºÐ¾Ð² (Ð°Ð»ÑŒÑ‚ÐµÑ€Ð½Ð°Ñ‚Ð¸Ð²Ð° Ð»Ð¸Ð½ÐµÐ¹Ð½Ð¾Ð¼Ñƒ drag).
- `FaunaBrain` â€” Ð´ÐµÐ¼Ð¿Ñ„Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ ÑÐºÐ¾Ñ€Ð¾ÑÑ‚Ð¸ Ð¼Ð¾Ñ€ÑÐºÐ¸Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð¿Ñ€Ð¸ Ð¼Ð°Ð½ÐµÐ²Ñ€Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ð¸.
- `PhysicsApplySystem` â€” Ð¿Ñ€Ð¸Ð¼ÐµÐ½ÐµÐ½Ð¸Ðµ ÑÐ¸Ð»Ñ‹ ÑÐ¾Ð¿Ñ€Ð¾Ñ‚Ð¸Ð²Ð»ÐµÐ½Ð¸Ñ ÑÑ€ÐµÐ´Ñ‹ Ðº `ForcePacket` Ð² Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ð¾Ð¹ Ñ„Ð¸Ð·Ð¸ÐºÐµ.

**ÐŸÑ€ÐµÐ¸Ð¼ÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¿ÐµÑ€ÐµÐ´ Ð»Ð¸Ð½ÐµÐ¹Ð½Ñ‹Ð¼ drag:**
- Ð‘Ð¾Ð»ÐµÐµ Ñ€ÐµÐ°Ð»Ð¸ÑÑ‚Ð¸Ñ‡Ð½Ð¾Ðµ Ð¿Ð¾Ð²ÐµÐ´ÐµÐ½Ð¸Ðµ Ð¿Ñ€Ð¸ Ð²Ñ‹ÑÐ¾ÐºÐ¸Ñ… ÑÐºÐ¾Ñ€Ð¾ÑÑ‚ÑÑ… (ÐºÐ²Ð°Ð´Ñ€Ð°Ñ‚Ð¸Ñ‡Ð½Ð¾Ðµ ÑÐ¾Ð¿Ñ€Ð¾Ñ‚Ð¸Ð²Ð»ÐµÐ½Ð¸Ðµ).
- Ð¡Ñ‚Ð°Ð±Ð¸Ð»ÑŒÐ½Ð°Ñ ÑÑ…Ð¾Ð´Ð¸Ð¼Ð¾ÑÑ‚ÑŒ Ðº Ð½ÑƒÐ»ÑŽ Ð±ÐµÐ· Ð¼Ð¸ÐºÑ€Ð¾-ÐºÐ¾Ð»ÐµÐ±Ð°Ð½Ð¸Ð¹.
- Ð¡Ð¾Ð²Ð¼ÐµÑÑ‚Ð¸Ð¼ Ñ `FixedTick` Ð¸ Burst-Ð²ÐµÐºÑ‚Ð¾Ñ€Ð¸Ð·Ð°Ñ†Ð¸ÐµÐ¹.

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `PHYS_Fluid_Incursion_Interior.txt`, `HectonPlayerMovement.cs`, `FaunaBrain.cs`

---

### Double Buffer
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐŸÐ°Ñ‚Ñ‚ÐµÑ€Ð½ Ñ Ð´Ð²ÑƒÐ¼Ñ Ð±ÑƒÑ„ÐµÑ€Ð°Ð¼Ð¸ Ð´Ð»Ñ Ñ‡Ñ‚ÐµÐ½Ð¸Ñ/Ð·Ð°Ð¿Ð¸ÑÐ¸ Ð±ÐµÐ· Ð±Ð»Ð¾ÐºÐ¸Ñ€Ð¾Ð²Ð¾Ðº.

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
NativeArray<CreatureData> _bufferA; // read frame N
NativeArray<CreatureData> _bufferB; // write frame N â†’ read frame N+1

void Swap() {
    var temp = _bufferA;
    _bufferA = _bufferB;
    _bufferB = temp;
}
```

**ÐŸÑ€ÐµÐ¸Ð¼ÑƒÑ‰ÐµÑÑ‚Ð²Ð°:**
- No race conditions
- No locks
- Cache-friendly sequential access

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `AI_Creature_Cognition_States.txt`, `PHYS_Fluid_Incursion_Interior.txt`

---

### SPSC (Single Producer Single Consumer)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Lock-free Ð¾Ñ‡ÐµÑ€ÐµÐ´ÑŒ Ð´Ð»Ñ Ð¾Ð´Ð½Ð¾ÑÑ‚Ð¾Ñ€Ð¾Ð½Ð½ÐµÐ¹ ÐºÐ¾Ð¼Ð¼ÑƒÐ½Ð¸ÐºÐ°Ñ†Ð¸Ð¸ Ð¼ÐµÐ¶Ð´Ñƒ Ð¿Ð¾Ñ‚Ð¾ÐºÐ°Ð¼Ð¸.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Audio DSP thread â†’ Main thread
- Job System â†’ Main thread
- Physics gather â†’ Physics apply

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// Native SPSC queue for audio param sync
NativeQueue<AudioParam> _paramQueue;

// Producer (DSP thread)
_paramQueue.Enqueue(new AudioParam { ... });

// Consumer (Main thread, LateUpdate)
while (_paramQueue.TryDequeue(out var param)) {
    ApplyParam(param);
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

---

## 4. Ð¡Ð˜Ð¡Ð¢Ð•ÐœÐ« Ð˜ ÐšÐžÐœÐŸÐžÐÐ•ÐÐ¢Ð«

### ITickable / IFixedTickable / ISlowTickable
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð˜Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹ÑÑ‹ Ð´Ð»Ñ ÑÐ¸ÑÑ‚ÐµÐ¼, Ð¾Ð±Ð½Ð¾Ð²Ð»ÑÐµÐ¼Ñ‹Ñ… Ñ‡ÐµÑ€ÐµÐ· GameTickManager.

```csharp
public interface ITickable {
    void Tick(float dt);  // Per-frame update
}

public interface IFixedTickable {
    void FixedTick(float fdt);  // Physics update (FixedDeltaT)
}

public interface ISlowTickable {
    void SlowTick();  // ~0.5s update (AI, ambient systems)
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `GameTickManager.cs`

---

### IPoolable
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð˜Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ Ð´Ð»Ñ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² Ð² object pool.

```csharp
public interface IPoolable {
    void OnSpawn();    // Ð¡Ð±Ñ€Ð¾Ñ Ð’Ð¡Ð•Ð“Ðž ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ñ
    void OnDespawn();  // ÐžÑ‚Ð¿Ð¸ÑÐºÐ° Ð¾Ñ‚ Ð’Ð¡Ð•Ð¥ ÑÐ¾Ð±Ñ‹Ñ‚Ð¸Ð¹, unregister Ð¸Ð· tick
}
```

**ÐšÑ€Ð¸Ñ‚Ð¸Ñ‡Ð½Ð¾:**
- OnSpawn Ð”ÐžÐ›Ð–Ð•Ð ÑÐ±Ñ€Ð°ÑÑ‹Ð²Ð°Ñ‚ÑŒ Ð’Ð¡Ð ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ
- OnDespawn Ð”ÐžÐ›Ð–Ð•Ð Ð¾Ñ‚Ð¿Ð¸ÑÑ‹Ð²Ð°Ñ‚ÑŒÑÑ Ð¾Ñ‚ Ð’Ð¡Ð•Ð¥ ÑÐ¾Ð±Ñ‹Ñ‚Ð¸Ð¹
- âŒ Ð—ÐÐŸÐ Ð•Ð©Ð•ÐÐž: async/await Ñ destroyCancellationToken Ð½Ð° pooled objects

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `ObjectPoolManager.cs`

---

### IInteractable
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð˜Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ Ð´Ð»Ñ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð², Ñ ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ð¼Ð¸ Ð¼Ð¾Ð¶Ð½Ð¾ Ð²Ð·Ð°Ð¸Ð¼Ð¾Ð´ÐµÐ¹ÑÑ‚Ð²Ð¾Ð²Ð°Ñ‚ÑŒ.

```csharp
public interface IInteractable {
    void Interact(InteractionPacket p);
    bool CanInteract(uint toolID);
    byte QueryState();  // 0-255 state value
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `Interaction/` scripts

---

### ISaveable
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð˜Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ Ð´Ð»Ñ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð², Ð¿Ð¾Ð´Ð´ÐµÑ€Ð¶Ð¸Ð²Ð°ÑŽÑ‰Ð¸Ñ… ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ñ.

```csharp
public interface ISaveable {
    int SavePriority { get; }    // 0-10 Core, 11-50 World, 51-100 Player, 101+ UI
    void PopulateSaveData(NativeByteStream stream);
    void LoadFromSaveData(NativeByteReader reader);
}
```

**LoadPriority:**
- 0-10: Core systems (GameTickManager, GlobalRegistry)
- 11-50: World (terrain, caves, props)
- 51-100: Player (position, inventory, tools)
- 101-200: Inventory (items, resources)
- 201+: UI (open tabs, cursor position)

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `SaveManager.cs`

---

### IPowerComponent
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð˜Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ Ð´Ð»Ñ ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ð¾Ð² ÑÐ½ÐµÑ€Ð³Ð¾ÑÐµÑ‚Ð¸.

```csharp
public interface IPowerComponent {
    float PowerRating { get; }      // kW consumption/production
    int PowerPriority { get; }      // 0 = critical, 255 = optional
    bool HasPower { get; }
    event Action<bool> OnPowerStatusChanged;
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `PowerGrid.cs`, `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

---

## 5. Ð¢Ð Ð•Ð¢Ð¬Ð¯ Ð¡Ð¢ÐžÐ ÐžÐÐ (THIRD-PARTY)

### Crest
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ocean simulation system Ð´Ð»Ñ Unity (URP/HDRP).

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Ocean surface simulation
- Wave kinematics
- Underwater rendering

**ÐžÐ³Ñ€Ð°Ð½Ð¸Ñ‡ÐµÐ½Ð¸Ñ:**
- `using Crest;` Ð¢ÐžÐ›Ð¬ÐšÐž Ð² `HectonCrestOcean*` ÐºÐ»Ð°ÑÑÐ°Ñ…
- gameplay-ÐºÐ¾Ð´ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ `IHectonOceanKinematics` Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹Ñ

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `THIRD_PARTY_POISON.md`, `HectonCrestOceanDepthCacheRuntimeBridge.cs`

---

### MapMagic
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Procedural terrain generation system.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Terrain heightmap generation
- Biome placement
- Scatter objects (rocks, trees)

**ÐžÐ³Ñ€Ð°Ð½Ð¸Ñ‡ÐµÐ½Ð¸Ñ:**
- `using MapMagic;` Ð¢ÐžÐ›Ð¬ÐšÐž Ð² `MapMagicBridge` ÐºÐ»Ð°ÑÑÐµ
- runtime access Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ñ‡ÐµÑ€ÐµÐ· `MapMagicBridge.Instance`

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

---

### MMFeedbacks (Feel)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Juice/feedback system Ð´Ð»Ñ Unity.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Camera shake
- Screen vibrations
- Audio feedback
- Particle bursts

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** âœ… Ð ÐÐ—Ð Ð•Ð¨ÐÐ Ð´Ð»Ñ runtime

---

### Odin Inspector
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Extended Unity Inspector Ñ Ð°Ñ‚Ñ€Ð¸Ð±ÑƒÑ‚Ð°Ð¼Ð¸.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Editor-only attributes ([OdinSerialize], [ShowInInspector])
- Custom inspectors

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** âœ… Editor Ñ‚Ð¾Ð»ÑŒÐºÐ¾, Ð½Ðµ Ð²Ñ…Ð¾Ð´Ð¸Ñ‚ Ð² Ð±Ð¸Ð»Ð´

---

## 6. ÐŸÐ ÐžÐ¦Ð•Ð”Ð£Ð ÐÐÐ¯ Ð“Ð•ÐÐ•Ð ÐÐ¦Ð˜Ð¯

### ProceduralFamily_*
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ScriptableObject Ñ Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€Ð°Ð¼Ð¸ Ð´Ð»Ñ Ð¿Ñ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ð¾Ð¹ Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ð¸ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð².

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
[CreateAssetMenu(fileName = "ProceduralFamily_Coral", ...)]
public class ProceduralFamily_Coral : ScriptableObject {
    public Mesh[] baseMeshes;
    public Material[] materials;
    public float sizeVariance = 0.3f;
    public float rotationVariance = 180f;
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `PROCEDURAL_ASSET_PIPELINE.md`

---

### ProceduralRule_*
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ScriptableObject Ñ Ð¿Ñ€Ð°Ð²Ð¸Ð»Ð°Ð¼Ð¸ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ Ð¿Ñ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ñ‹Ñ… Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð².

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
[CreateAssetMenu(fileName = "ProceduralRule_Scatter", ...)]
public class ProceduralRule_Scatter : ScriptableObject {
    public float minDensity = 0.5f;
    public float maxDensity = 2.0f;
    public float slopeThreshold = 30f;
    public LayerMask validLayers;
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `SCATTER_REFACTOR_EXECUTION_PLAN.md`

---

### SDF (Signed Distance Field)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐœÐ°Ñ‚ÐµÐ¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¾Ðµ Ð¿Ñ€ÐµÐ´ÑÑ‚Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ ÐºÐ°Ðº Ñ€Ð°ÑÑÑ‚Ð¾ÑÐ½Ð¸Ñ Ð´Ð¾ Ð±Ð»Ð¸Ð¶Ð°Ð¹ÑˆÐµÐ¹ Ñ‚Ð¾Ñ‡ÐºÐ¸.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Voxel terrain carving
- Cave generation
- Smooth mesh extraction (marching cubes)

**Ð¤Ð¾Ñ€Ð¼ÑƒÐ»Ð°:**
```
SDF(point) > 0  â†’ Ñ‚Ð¾Ñ‡ÐºÐ° Ð²Ð½Ðµ Ð¾Ð±ÑŠÐµÐºÑ‚Ð°
SDF(point) = 0  â†’ Ñ‚Ð¾Ñ‡ÐºÐ° Ð½Ð° Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸
SDF(point) < 0  â†’ Ñ‚Ð¾Ñ‡ÐºÐ° Ð²Ð½ÑƒÑ‚Ñ€Ð¸ Ð¾Ð±ÑŠÐµÐºÑ‚Ð°
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`

---

### Marching Cubes
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** ÐÐ»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼ Ð¸Ð·Ð²Ð»ÐµÑ‡ÐµÐ½Ð¸Ñ Ð¿Ð¾Ð»Ð¸Ð³Ð¾Ð½Ð°Ð»ÑŒÐ½Ð¾Ð¹ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð¸Ð· SDF/Ð²Ð¾ÐºÑÐµÐ»ÐµÐ¹.

**ÐŸÑ€Ð¸Ð½Ñ†Ð¸Ð¿:**
1. Ð Ð°Ð·Ð±Ð¸Ñ‚ÑŒ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²Ð¾ Ð½Ð° ÐºÑƒÐ±Ñ‹ (voxel grid)
2. Ð”Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ ÐºÑƒÐ±Ð° Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»Ð¸Ñ‚ÑŒ Ñ‚Ð¸Ð¿ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ (8 Ð²ÐµÑ€ÑˆÐ¸Ð½ â†’ 256 ÐºÐ¾Ð½Ñ„Ð¸Ð³ÑƒÑ€Ð°Ñ†Ð¸Ð¹)
3. Ð¡Ð³ÐµÐ½ÐµÑ€Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ Ñ‚Ñ€ÐµÑƒÐ³Ð¾Ð»ÑŒÐ½Ð¸ÐºÐ¸ Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð¹ ÐºÐ¾Ð½Ñ„Ð¸Ð³ÑƒÑ€Ð°Ñ†Ð¸Ð¸

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`

---

## 7. ÐÐ£Ð”Ð˜Ðž Ð˜ DSP

### DSPGraph
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Unity audio DSP graph system Ð´Ð»Ñ procedural audio.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Procedural sound synthesis
- Real-time audio processing
- Spatial audio (HRTF)

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
// DSP node graph
var graph = new DSPGraph();
var oscillator = graph.CreateNode<OscillatorNode>();
var filter = graph.CreateNode<FilterNode>();
var output = graph.CreateNode<OutputNode>();

oscillator.Connect(filter);
filter.Connect(output);
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`, `SpatialAudioManager.cs`

---

### HRTF (Head-Related Transfer Function)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¤ÑƒÐ½ÐºÑ†Ð¸Ñ, Ð¼Ð¾Ð´ÐµÐ»Ð¸Ñ€ÑƒÑŽÑ‰Ð°Ñ Ð²Ð¾ÑÐ¿Ñ€Ð¸ÑÑ‚Ð¸Ðµ Ð·Ð²ÑƒÐºÐ° Ñ‡ÐµÐ»Ð¾Ð²ÐµÐºÐ¾Ð¼ Ð² 3D Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²Ðµ.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Binaural audio Ð´Ð»Ñ Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ°
- Spatial occlusion (Ñ‡ÐµÑ€ÐµÐ· Ð²Ð¾Ð´Ñƒ, Ñ‡ÐµÑ€ÐµÐ· ÐºÐ¾Ñ€Ð¿ÑƒÑ)

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `AUDIO_Hrtf_Binaural_Spatialization.txt`, `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`

---

### IAudioOutputJob
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Native DSP synthesis interface Ð´Ð»Ñ Zero-GC audio.

**ÐŸÑ€Ð¸Ð¼ÐµÑ€:**
```csharp
public interface IAudioOutputJob {
    void Process(float[] left, float[] right, int sampleCount);
}
```

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `SpatialAudioManager.cs`

---

## 8. Ð Ð•ÐÐ”Ð•Ð Ð˜ÐÐ“ Ð˜ Ð“Ð ÐÐ¤Ð˜ÐšÐ

### URP (Universal Render Pipeline)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Scriptable Render Pipeline Ð¾Ñ‚ Unity Ð´Ð»Ñ ÐºÑ€Ð¾ÑÑ-Ð¿Ð»Ð°Ñ‚Ñ„Ð¾Ñ€Ð¼ÐµÐ½Ð½Ð¾Ð³Ð¾ Ñ€ÐµÐ½Ð´ÐµÑ€Ð¸Ð½Ð³Ð°.

**ÐšÐ¾Ð½Ñ„Ð¸Ð³ÑƒÑ€Ð°Ñ†Ð¸Ñ HECTON-8:**
- Surface (Medium): HDR, MSAA=OFF, FXAA, scale 1.0
- Low: HDR, MSAA=OFF, FXAA, scale 0.65

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `Assets/_Project/Data/URP_Medium.asset`

---

### SRP Batcher
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Unity batching system Ð´Ð»Ñ Scriptable Render Pipelines.

**Ð¢Ñ€ÐµÐ±Ð¾Ð²Ð°Ð½Ð¸Ñ:**
- ÐžÐ´Ð¸Ð½ Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð» = Ð¾Ð´Ð¸Ð½ shader variant
- CBUFFER_START(UnityPerMaterial) Ð´Ð»Ñ per-material data
- âŒ Ð—ÐÐŸÐ Ð•Ð©Ð•ÐÐž: MaterialPropertyBlock Ð½Ð° ÑÑ‚Ð°Ð½Ð´Ð°Ñ€Ñ‚Ð½Ð¾Ð¹ Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸Ð¸

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

---

### GPU Instancing
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð ÐµÐ½Ð´ÐµÑ€Ð¸Ð½Ð³ Ð¼Ð½Ð¾Ð¶ÐµÑÑ‚Ð²Ð° Ð¾Ð´Ð¸Ð½Ð°ÐºÐ¾Ð²Ñ‹Ñ… Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² Ð² Ð¾Ð´Ð½Ð¾Ð¼ draw call.

**Ð¢Ñ€ÐµÐ±Ð¾Ð²Ð°Ð½Ð¸Ñ:**
- Enable Ð½Ð° Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ðµ
- âŒ Ð—ÐÐŸÐ Ð•Ð©Ð•ÐÐž: ÐºÐ¾Ð¼Ð±Ð¸Ð½Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ ÑÐ¾ Static Batching
- Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÑŒ Ð´Ð»Ñ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€ÑÑŽÑ‰Ð¸Ñ…ÑÑ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² (rocks, trees, props)

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `REND_Instanced_Flora_Physics.txt`

---

### LOD (Level of Detail)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¡Ð¸ÑÑ‚ÐµÐ¼Ð° ÑƒÐ¼ÐµÐ½ÑŒÑˆÐµÐ½Ð¸Ñ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² Ð½Ð° Ñ€Ð°ÑÑÑ‚Ð¾ÑÐ½Ð¸Ð¸.

**Ð‘ÑŽÐ´Ð¶ÐµÑ‚Ñ‹ HECTON-8:**
- Props > 0.5m: LOD0 + LOD1 + Cull
- Hero: LOD0 + LOD1 + LOD2 + Cull
- LOD1 â‰¤ 50% LOD0 poly
- LOD2 â‰¤ 25% LOD0 poly

**ÐŸÐµÑ€ÐµÑ…Ð¾Ð´Ñ‹:** Crossfade/dithered near-field, discrete distant

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `REND_Foveated_Simulation_LOD.txt`, `LOD_SYSTEM_README.md`

---

### VAT (Vertex Animation Textures)
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð—Ð°Ð¿ÐµÑ‡Ñ‘Ð½Ð½Ð°Ñ Ð°Ð½Ð¸Ð¼Ð°Ñ†Ð¸Ñ Ð²ÐµÑ€ÑˆÐ¸Ð½ Ð² Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ñ‹ Ð´Ð»Ñ GPU-driven animation.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Flora animation (kelp, coral)
- Destructible objects

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `REND_GPU_Driven_Animation_VAT.txt`

---

### Impostors
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** 2D billboard Ñ Ð·Ð°Ð¿ÐµÑ‡Ñ‘Ð½Ð½Ð¾Ð¹ 3D Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸ÐµÐ¹ Ð´Ð»Ñ Ð´Ð°Ð»ÑŒÐ½ÐµÐ³Ð¾ LOD.

**Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð² HECTON-8:**
- Very distant objects (>100m)
- Complex geometry simplification

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `ImpostorSystem.cs`, `AmplifyImpostors/`

---

### Diegetic Editor Preview
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Wireframe / gizmo-based visualization of a diegetic UI element in the Unity Scene view during Edit Mode, without entering Play Mode.

**ÐÐ°Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ:** Allows the Lead Architect and environment artists to see the spatial layout, scale, and FOV alignment of projected HUD canvases (e.g., `SuitHUDV4CanvasOverlay` in `ProjectionSource` mode) without relying on runtime camera pose updates.

**Ð¢Ñ€ÐµÐ±Ð¾Ð²Ð°Ð½Ð¸Ñ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸:**
- `#if UNITY_EDITOR` only â€” stripped from builds.
- No `GetComponent`, `FindObjectOfType`, or physics queries inside gizmo path.
- Must read only cached serialized fields and `SceneView.lastActiveSceneView.camera`.
- Color-coded wireframes: orange = projection frustum, cyan = element bounds, white = text labels.

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `HUD_EDITOR_SPEC.md`, `SuitHUDV4CanvasOverlay.cs`

---

### BC7 / BC5
**ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ:** Ð¤Ð¾Ñ€Ð¼Ð°Ñ‚Ñ‹ ÑÐ¶Ð°Ñ‚Ð¸Ñ Ñ‚ÐµÐºÑÑ‚ÑƒÑ€ DirectX.

**BC7:**
- 8 bits/pixel
- Ð”Ð»Ñ albedo/roughness/AO
- 2048Ã—2048 â‰ˆ 5.3 MB

**BC5:**
- 8 bits/pixel (RG ÐºÐ°Ð½Ð°Ð»Ñ‹)
- Ð”Ð»Ñ normal maps (DXT5nm)
- 2048Ã—2048 â‰ˆ 5.3 MB

**Ð¡Ð¼. Ñ‚Ð°ÐºÐ¶Ðµ:** `VRAM_BUDGET_AUDIT.md`

---

## ðŸ“ ÐŸÐ Ð˜ÐœÐ•Ð§ÐÐÐ˜Ð¯ ÐŸÐž Ð˜Ð¡ÐŸÐžÐ›Ð¬Ð—ÐžÐ’ÐÐÐ˜Ð®

### Ð”Ð»Ñ AI-Ð°Ð³ÐµÐ½Ñ‚Ð¾Ð²:
1. **Ð’ÑÐµÐ³Ð´Ð° Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÑŒ Ñ‚Ð¾Ñ‡Ð½Ñ‹Ðµ Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ñ‹** Ð¸Ð· ÑÑ‚Ð¾Ð³Ð¾ Ð³Ð»Ð¾ÑÑÐ°Ñ€Ð¸Ñ
2. **ÐÐµ Ð¸Ð·Ð¾Ð±Ñ€ÐµÑ‚Ð°Ñ‚ÑŒ Ð½Ð¾Ð²Ñ‹Ðµ Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ñ‹** Ð±ÐµÐ· ÑÐ²Ð½Ð¾Ð¹ Ð½ÐµÐ¾Ð±Ñ…Ð¾Ð´Ð¸Ð¼Ð¾ÑÑ‚Ð¸
3. **Ð¡ÑÑ‹Ð»Ð°Ñ‚ÑŒÑÑ Ð½Ð° Ð¼Ð°Ð½Ð´Ð°Ñ‚Ñ‹** Ð¿Ñ€Ð¸ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ð¸ ÑÐ¿ÐµÑ†Ð¸Ð°Ð»Ð¸Ð·Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ñ… Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ð¾Ð²

### Ð”Ð»Ñ Ñ€Ð°Ð·Ñ€Ð°Ð±Ð¾Ñ‚Ñ‡Ð¸ÐºÐ¾Ð²:
1. **Ð”Ð¾Ð±Ð°Ð²Ð»ÑÑ‚ÑŒ Ð½Ð¾Ð²Ñ‹Ðµ Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ñ‹** Ð¿Ñ€Ð¸ Ð²Ð²Ð¾Ð´Ðµ Ð½Ð¾Ð²Ñ‹Ñ… ÑÐ¸ÑÑ‚ÐµÐ¼
2. **ÐžÐ±Ð½Ð¾Ð²Ð»ÑÑ‚ÑŒ Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ñ** Ð¿Ñ€Ð¸ Ð¸Ð·Ð¼ÐµÐ½ÐµÐ½Ð¸Ð¸ Ð°Ñ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ñ‹
3. **Ð¡ÑÑ‹Ð»Ð°Ñ‚ÑŒÑÑ Ð½Ð° Ð³Ð»Ð¾ÑÑÐ°Ñ€Ð¸Ð¹** Ð² Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ð¸

---

**STATUS:** âœ… GLOSSARY.md ÑÐ¾Ð·Ð´Ð°Ð½
**LAST UPDATED:** 2026-04-28
**NEXT REVIEW:** ÐŸÑ€Ð¸ Ð´Ð¾Ð±Ð°Ð²Ð»ÐµÐ½Ð¸Ð¸ Ð½Ð¾Ð²Ñ‹Ñ… ÑÐ¸ÑÑ‚ÐµÐ¼ Ð¸Ð»Ð¸ Ñ‚ÐµÑ€Ð¼Ð¸Ð½Ð¾Ð²
