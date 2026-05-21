# ÐŸÐ¾Ð»Ð½Ñ‹Ð¹ ÑÐ¿Ð¸ÑÐ¾Ðº Ð½ÐµÐ´Ð¾Ñ€Ð°Ð±Ð¾Ñ‚Ð¾Ðº Ð¸ Ð¿Ð»Ð°Ð½ Ñ€Ð°Ð·Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ¸ â€” Project Submerge (HECTON-8)

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

## 2026-05-18 R11 Legacy Evidence Boundary

`GOTOV` / ready, ES3/Easy Save, `Zero GC`, system-complete, and implementation-done claims below are historical backlog claims only. Current status is `PENDING VERIFICATION` until current source scan, Unity import/Console, Play Mode route, profiler/GCMonitor, save/load, and artifact links exist. ES3/Easy Save references are legacy contamination unless a current stable authority explicitly approves that path.

Verification: PENDING VERIFICATION

# ÐŸÐ¾Ð»Ð½Ñ‹Ð¹ ÑÐ¿Ð¸ÑÐ¾Ðº Ð½ÐµÐ´Ð¾Ñ€Ð°Ð±Ð¾Ñ‚Ð¾Ðº Ð¸ Ð¿Ð»Ð°Ð½ Ñ€Ð°Ð·Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ¸

## Project Submerge (HECTON-8) | Ð“Ð»ÑƒÐ±Ð¾ÐºÐ¸Ð¹ Ð°Ð½Ð°Ð»Ð¸Ð· ÐºÐ¾Ð´Ð° + README + Ð²ÑÐµ Ð¿Ð»Ð°Ð½Ñ‹

<user_quoted_section>Ð’ÐµÑ€ÑÐ¸Ñ 2.0 â€” Ð¿Ð¾ÑÐ»Ðµ Ð¸Ð·ÑƒÑ‡ÐµÐ½Ð¸Ñ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ ÐºÐ¾Ð´Ð° (HectonSurvivalSystem.cs, SaveManager.cs, HectonBaseAI.cs, FaunaDirector.cs, PowerGridManager.cs, SceneBootstrap.cs, HectonVoxelEngine.cs, CaveGraphGenerator.cs, Ð±ÐµÐºÐ»Ð¾Ð³.txt, ÑÐ¿ÐµÑ†Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ð¸.txt, Ð´Ð¸Ð°Ð»Ð¾Ð³ Ñ Claude Opus 4.6). ÐœÐ½Ð¾Ð³Ð¸Ðµ Â«Ð±Ð»Ð¾ÐºÐµÑ€Ñ‹Â» Ð¸Ð· v1.0 ÑƒÐ¶Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹ Ð½Ð° ÑƒÑ€Ð¾Ð²Ð½Ðµ ÐºÐ¾Ð´Ð° â€” Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¾.</user_quoted_section>

<user_quoted_section>Ð˜ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¸:  (2).md, , , , , , , , , , , , , , </user_quoted_section>

## ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð ÐÐ•Ð”ÐžÐ ÐÐ‘ÐžÐ¢ÐžÐš

```mermaid
graph TD
    A[HECTON-8 ÐÐµÐ´Ð¾Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ¸] --> B[ÐšÐ Ð˜Ð¢Ð˜Ð§Ð•Ð¡ÐšÐ˜Ð• Ð‘Ð›ÐžÐšÐ•Ð Ð«]
    A --> C[CORE GAMEPLAY]
    A --> D[WORLD & TERRAIN]
    A --> E[Ð’Ð˜Ð—Ð£ÐÐ› & ÐÐ¢ÐœÐžÐ¡Ð¤Ð•Ð Ð]
    A --> F[Ð¢Ð•Ð¥ÐÐ˜Ð§Ð•Ð¡ÐšÐ˜Ð™ Ð”ÐžÐ›Ð“]
    A --> G[ÐšÐžÐÐ¢Ð•ÐÐ¢ & ÐŸÐ ÐžÐ“Ð Ð•Ð¡Ð¡Ð˜Ð¯]

    B --> B1[ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ AI/Fauna]
    B --> B2[ÐÐµÑ‚ Save System]
    B --> B3[GPUI Rock Shader Ð½Ðµ Ð³Ð¾Ñ‚Ð¾Ð²]
    B --> B4[ÐÐµÑ‚ Additive Scene Loading]

    C --> C1[Survival System Ð½ÐµÐ¿Ð¾Ð»Ð½Ñ‹Ð¹]
    C --> C2[Inventory Ð±ÐµÐ· instance-level Ð´Ð°Ð½Ð½Ñ‹Ñ…]
    C --> C3[ÐÐµÑ‚ Ñ‚Ñ€Ð°Ð½ÑÐ¿Ð¾Ñ€Ñ‚Ð°]
    C --> C4[ÐÐµÑ‚ Voxel Cave System]

    D --> D1[MapMagic 5-biome graph Ð½Ðµ Ð³Ð¾Ñ‚Ð¾Ð²]
    D --> D2[ÐÐµÑ‚ ProximityColliderSystem live]
    D --> D3[ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ world fill]
    D --> D4[ÐÐµÑ‚ Floating Origin]

    E --> E1[Scene View sky Ð±Ð°Ð³]
    E --> E2[Bioluminescence Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð°]
    E --> E3[ÐÐµÑ‚ LOD dithering]

    F --> F1[83 runtime Ñ„Ð°Ð¹Ð»Ð° Ñ editor coupling]
    F --> F2[ÐÐµÑ‚ asmdef split]
    F --> F3[Reload 130+ ÑÐµÐº]

    G --> G1[44 placeholder biome]
    G --> G2[ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ð»ÑƒÑ‚Ð° Ð² Ð¼Ð¸Ñ€Ðµ]
    G --> G3[ÐÐµÑ‚ NPC/Barter]
```

## Ð ÐÐ—Ð”Ð•Ð› 1: Ð Ð•ÐÐ›Ð¬ÐÐ«Ð™ Ð¡Ð¢ÐÐ¢Ð£Ð¡ ÐšÐ›Ð®Ð§Ð•Ð’Ð«Ð¥ Ð¡Ð˜Ð¡Ð¢Ð•Ðœ (Ð¿Ð¾ÑÐ»Ðµ Ð¸Ð·ÑƒÑ‡ÐµÐ½Ð¸Ñ ÐºÐ¾Ð´Ð°)

<user_quoted_section>ÐœÐ½Ð¾Ð³Ð¾Ðµ Ð¸Ð· Ñ‚Ð¾Ð³Ð¾, Ñ‡Ñ‚Ð¾ ÐºÐ°Ð·Ð°Ð»Ð¾ÑÑŒ Â«Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¼Â», ÑƒÐ¶Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚ Ð² ÐºÐ¾Ð´Ðµ. ÐÐ¸Ð¶Ðµ â€” Ñ‡ÐµÑÑ‚Ð½Ð°Ñ Ð¾Ñ†ÐµÐ½ÐºÐ°.</user_quoted_section>

### âœ… Ð£Ð–Ð• Ð Ð•ÐÐ›Ð˜Ð—ÐžÐ’ÐÐÐž (Ð»ÑƒÑ‡ÑˆÐµ, Ñ‡ÐµÐ¼ ÐºÐ°Ð·Ð°Ð»Ð¾ÑÑŒ)

| Ð¡Ð¸ÑÑ‚ÐµÐ¼Ð° | Ð¤Ð°Ð¹Ð» | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ |
| --- | --- | --- |
| **Save System** | `SaveManager.cs` | ÐŸÐ¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ñ‹Ð¹ async save/load Ñ‡ÐµÑ€ÐµÐ· ES3, ISaveable registry, delta-snapshot, versioning (`SaveData.CurrentVersion`), async Awaitable API â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Survival System** | `HectonSurvivalSystem.cs` | O2/Energy/Integrity/Pressure/Depth â€” Ð²ÑÐµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹. `ApplyPressureDamage` Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚. `TakeDamage` API ÐµÑÑ‚ÑŒ. Zero-GC. ISaveable â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Scene Bootstrap** | `SceneBootstrap.cs` | ÐŸÐ¾Ð»Ð½Ñ‹Ð¹ async pipeline: singletons â†’ pool warmup â†’ world gen â†’ save/load â†’ world-ready â†’ player spawn â†’ OnGameReady â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **AI Base** | `HectonBaseAI.cs` | FSM (Idle/Wander/Escape/Aggressive), obstacle avoidance (7 rays), buoyancy, pooling, health, zero-GC â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Fauna Director** | `FaunaDirector.cs` | Biome-aware spawning, culling, horde spawn, predator pressure â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Power Grid** | `PowerGridManager.cs` | BFS connectivity, merge/split grids, zero-GC, ISlowTickable â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Voxel Engine** | `HectonVoxelEngine.cs` | SDF caves v4.0, Burst jobs, MC extraction, vertex colors, spawn points â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **Cave Graph** | `CaveGraphGenerator.cs` | Deterministic rooms/tunnels/entrances, 5 room types, 3 tunnel types â€” **Ð“ÐžÐ¢ÐžÐ’** |
| **ProximityCollider** | `ÑÐ¿ÐµÑ†Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ð¸.txt` | Burst job, hysteresis 40/45m, 0 GC, 10K points â€” **Ð“ÐžÐ¢ÐžÐ’ Ð² ÐºÐ¾Ð´Ðµ**, Ð½ÑƒÐ¶Ð½Ð° live ÑÑ†ÐµÐ½Ð° |
| **Inventory Stacks** | `Ð”Ð˜ÐÐ›ÐžÐ“ Ð¡ ÐšÐ›ÐžÐ” ÐžÐŸÐ£Ð¡ 4.6.txt` | Pass 1-3 Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹: ÑÑ‚ÐµÐºÐ¸, DROP, USE, SORT, durability bars, HUDNotification â€” **Ð“ÐžÐ¢ÐžÐ’** |

## Ð ÐÐ—Ð”Ð•Ð› 2: ÐšÐ Ð˜Ð¢Ð˜Ð§Ð•Ð¡ÐšÐ˜Ð• Ð‘Ð›ÐžÐšÐ•Ð Ð« (Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ðµ, Ð¿Ð¾ÑÐ»Ðµ Ð°Ð½Ð°Ð»Ð¸Ð·Ð° ÐºÐ¾Ð´Ð°)

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -1: Voxel Cave System Ð½Ðµ Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡ÐµÐ½Ð° Ðº MapMagic

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `HectonVoxelEngine.cs` Ð¸ `CaveGraphGenerator.cs` â€” Ð¿Ð¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹ Ð¸ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÑŽÑ‚. ÐÐ¾ Ð¾Ð½Ð¸ Ð¸Ð·Ð¾Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ñ‹ Ð¾Ñ‚ MapMagic.

**Ð§Ñ‚Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- Ð˜Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ: MapMagic dents â†’ Ñ‚Ñ€Ð¸Ð³Ð³ÐµÑ€ Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ð¸ voxel cave
- Seam art masking (Ð¿ÐµÑ€ÐµÑ…Ð¾Ð´ terrain â†’ cave mesh)
- Spawn point â†’ gameplay content wiring (Ð»ÑƒÑ‚, Ð²Ñ€Ð°Ð³Ð¸ Ð²Ð½ÑƒÑ‚Ñ€Ð¸ Ð¿ÐµÑ‰ÐµÑ€)
- Cave biome assignment (ÐºÐ°ÐºÐ¾Ð¹ `CavePreset` Ð´Ð»Ñ ÐºÐ°ÐºÐ¾Ð³Ð¾ Ð±Ð¸Ð¾Ð¼Ð°)

**ÐŸÐ¾Ñ‡ÐµÐ¼Ñƒ Ð±Ð»Ð¾ÐºÐµÑ€:** ÐŸÐµÑ‰ÐµÑ€Ñ‹ â€” ÐºÐ»ÑŽÑ‡ÐµÐ²Ð¾Ð¹ gameplay space Ð´Ð»Ñ Ð´Ð¾Ð±Ñ‹Ñ‡Ð¸ Ñ€ÐµÑÑƒÑ€ÑÐ¾Ð², ÑƒÐºÑ€Ñ‹Ñ‚Ð¸Ð¹, Ð»Ð¾Ñ€Ð°.

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -2: GPUI Rock Shader Ð½Ðµ Ð³Ð¾Ñ‚Ð¾Ð² Ð´Ð»Ñ Ð¸Ð½ÑÑ‚Ð°Ð½ÑÐ¸Ð½Ð³Ð°

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð¯Ð²Ð½Ð¾ Ð·Ð°Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¾ Ð² file:MAPMAGIC_WORLD_STACK_PLAN.md ÐºÐ°Ðº "known, localized, honest blocker".

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Shader Graph `SG_Rock_Triplanar` â€” Ð´Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ `GPU Instancer Setup` node Ð²Ñ€ÑƒÑ‡Ð½ÑƒÑŽ Ð² Shader Graph
- ÐÐºÑ‚Ð¸Ð²Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ `Rock_Runtime` root Ð² ÑÑ†ÐµÐ½Ðµ
- Ð”Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ `HectonRockManager` Ð¸ `GPUInstancerPrefabManager` Ð² `[MANAGERS]`

**ÐŸÐ¾ÑÐ»ÐµÐ´ÑÑ‚Ð²Ð¸Ðµ:** Ð‘ÐµÐ· ÑÑ‚Ð¾Ð³Ð¾ Ð¼Ð¸Ñ€ Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ð¾ Ð¿ÑƒÑÑ‚Ð¾Ð¹ â€” Ð½ÐµÑ‚ ÑÐºÐ°Ð», Ð½ÐµÑ‚ mid/far field density.

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -3: ÐÐµÑ‚ Floating Origin

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** ÐÐµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½. ÐŸÑ€ÑÐ¼Ð¾ ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚ Ð² README (2).md Â§Stones ÐºÐ°Ðº ÐºÑ€Ð¸Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ñ€Ð¸ÑÐº.

**ÐŸÑ€Ð¾Ð±Ð»ÐµÐ¼Ð°:** ÐšÐ°Ñ€Ñ‚Ð° 15Ã—15 ÐºÐ¼ = floating-point precision errors Ð½Ð° Ñ€Ð°ÑÑÑ‚Ð¾ÑÐ½Ð¸Ð¸ >5 ÐºÐ¼ Ð¾Ñ‚ origin. Ð”Ñ€Ð¾Ð¶Ð°Ð½Ð¸Ðµ Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð², Ð½ÐµÑ‚Ð¾Ñ‡Ð½Ð°Ñ Ñ„Ð¸Ð·Ð¸ÐºÐ°, Ð°Ñ€Ñ‚ÐµÑ„Ð°ÐºÑ‚Ñ‹ Ñ€ÐµÐ½Ð´ÐµÑ€Ð¸Ð½Ð³Ð°.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- `FloatingOriginSystem` â€” Ð¿Ñ€Ð¸ ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ð¸ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð¾Ñ‚ (0,0,0) Ð½Ð° N Ð¼ÐµÑ‚Ñ€Ð¾Ð²: ÑÐ´Ð²Ð¸Ð³ Ð²ÑÐµÐ³Ð¾ Ð¼Ð¸Ñ€Ð° Ð¾Ð±Ñ€Ð°Ñ‚Ð½Ð¾ Ðº origin
- ÐšÐ¸Ð½ÐµÐ¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ°Ñ Ñ‚ÐµÐ»ÐµÐ¿Ð¾Ñ€Ñ‚Ð°Ñ†Ð¸Ñ (ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ð° Ð² README Â§Stones)
- Ð˜Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ MapMagic (terrain offset), Crest (ocean offset), Ð²ÑÐµÐ¼Ð¸ physics objects

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -4: ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ AI Ð¿Ð¾Ð²ÐµÐ´ÐµÐ½Ð¸Ñ (Ñ‚Ð¾Ð»ÑŒÐºÐ¾ FSM ÑÐºÐµÐ»ÐµÑ‚)

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `HectonBaseAI.cs` â€” Ð¾Ñ‚Ð»Ð¸Ñ‡Ð½Ñ‹Ð¹ FSM ÑÐºÐµÐ»ÐµÑ‚. `FaunaDirector.cs` â€” spawning Ð³Ð¾Ñ‚Ð¾Ð². ÐÐ¾:

**Ð§Ñ‚Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- Candice AI behavior trees Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ (Ð¿Ð°ÐºÐµÑ‚ ÐµÑÑ‚ÑŒ, Ð½Ðµ Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡Ñ‘Ð½)
- Ð ÐµÐ°ÐºÑ†Ð¸Ñ Ð½Ð° ÑˆÑƒÐ¼ (Ð½ÐµÑ‚ `NoiseSystem`)
- Ð ÐµÐ°ÐºÑ†Ð¸Ñ Ð½Ð° Ð¾ÑÐ²ÐµÑ‰ÐµÐ½Ð¸Ðµ (Ð½ÐµÑ‚ `LightDetectionSystem`)
- Leviathan types (Ð¼ÐµÐ³Ð°-ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¸Ð· README)
- NavMesh Ð´Ð»Ñ underwater navigation (A* Pathfinding ÐµÑÑ‚ÑŒ, Ð½Ðµ Ð½Ð°ÑÑ‚Ñ€Ð¾ÐµÐ½ Ð´Ð»Ñ underwater)
- GPU Boids Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ (`BoidSimulation.compute` + `BoidFishInstanced.shader` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‚ â€” Ð½ÑƒÐ¶Ð½Ð° ÑÑ†ÐµÐ½Ð°)
- Drone NPC Ð´Ð»Ñ barter

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -5: Additive Scene Loading â€” Ð°Ñ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð° ÐµÑÑ‚ÑŒ, ÑÑ†ÐµÐ½Ñ‹ Ð½ÐµÑ‚

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `SceneBootstrap.cs` Ð¿Ð¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ Ð³Ð¾Ñ‚Ð¾Ð². ÐÐ¾:

**Ð§Ñ‚Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- Ð¡Ñ†ÐµÐ½Ð° `00_BOOTSTRAP` (Ñ‚Ð¾Ð»ÑŒÐºÐ¾ `02_HECTON_WORLD` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚)
- Ð¡Ñ†ÐµÐ½Ð° `01_MAIN_MENU` (MainMenuController ÐµÑÑ‚ÑŒ, ÑÑ†ÐµÐ½Ð° Ð½Ðµ Ð½Ð°ÑÑ‚Ñ€Ð¾ÐµÐ½Ð°)
- `XX_SANDBOX` ÑÑ†ÐµÐ½Ð° Ð´Ð»Ñ Ð¸Ð·Ð¾Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð¾Ð³Ð¾ Ñ‚ÐµÑÑ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ
- Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ `GameManager` DontDestroyOnLoad singleton
- Ð“Ð»Ð¾Ð±Ð°Ð»ÑŒÐ½Ñ‹Ð¹ `AudioMixer` Ñ‡ÐµÑ€ÐµÐ· Bootstrap
- Checkpoint-based zone loading/unloading

### ðŸ”´ Ð‘Ð›ÐžÐšÐ•Ð -6: Save System â€” ÐºÐ¾Ð´ Ð³Ð¾Ñ‚Ð¾Ð², Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ð½ÐµÐ¿Ð¾Ð»Ð½Ð°Ñ

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `SaveManager.cs` â€” production-grade. `HectonSurvivalSystem` â€” ISaveable. ÐÐ¾:

**Ð§Ñ‚Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- `ResourceNode` depletion state â€” Ð½Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÑ‚ÑÑ (Ð½ÐµÑ‚ ISaveable Ð½Ð° ResourceNode)
- `WorldStateManager` â€” ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, Ð½Ð¾ `ClearAll()` Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿Ñ€Ð¸ New Game
- `ConstructionManager` â€” ISaveable Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½ (Ñ‚Ð¾Ð»ÑŒÐºÐ¾ `ClearAllModules()`)
- `BeaconNetworkSystem` â€” save/load ÐµÑÑ‚ÑŒ, Ð½Ð¾ Ð½Ðµ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐµÐ½Ð¾ Ð² Ð¿Ð¾Ð»Ð½Ð¾Ð¼ Ñ†Ð¸ÐºÐ»Ðµ
- Save Station UI â†’ `SaveManager.SaveGameAsync()` â€” Ð½Ðµ Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¾
- Ð’ÐµÑ€ÑÐ¸Ð¾Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ ÑÑ…ÐµÐ¼Ñ‹ (`SaveData.CurrentVersion`) â€” ÐµÑÑ‚ÑŒ, Ð½Ð¾ migration logic Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚
- Delta saves â€” Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹ (ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÑ‚ÑÑ Ð²ÐµÑÑŒ `SaveData` Ñ†ÐµÐ»Ð¸ÐºÐ¾Ð¼)

## Ð ÐÐ—Ð”Ð•Ð› 3: CORE GAMEPLAY â€” Ð”Ð•Ð¢ÐÐ›Ð¬ÐÐ«Ð™ ÐÐÐÐ›Ð˜Ð— ÐÐ•Ð”ÐžÐ ÐÐ‘ÐžÐ¢ÐžÐš

### ðŸŸ  CORE-1: Survival System â€” Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ ÐµÑÑ‚ÑŒ, Ñ‚ÐµÐ¼Ð¿ÐµÑ€Ð°Ñ‚ÑƒÑ€Ð°/Ñ€Ð°Ð´Ð¸Ð°Ñ†Ð¸Ñ Ð½ÐµÑ‚

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ ÐºÐ¾Ð´Ð°:** `HectonSurvivalSystem.cs` â€” `ApplyPressureDamage()` Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚. `pressure = 1f + depth * 0.1f`. `SafeDepth` Ð¸Ð· `SurvivalStats`.

**Ð§Ñ‚Ð¾ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- **Ð¢ÐµÐ¼Ð¿ÐµÑ€Ð°Ñ‚ÑƒÑ€Ð°:** Ð½ÐµÑ‚ Ð¿Ð¾Ð»Ñ `temperature` Ð² ÑÐ¸ÑÑ‚ÐµÐ¼Ðµ, Ð½ÐµÑ‚ drain/damage Ð¾Ñ‚ Ñ‚ÐµÐ¼Ð¿ÐµÑ€Ð°Ñ‚ÑƒÑ€Ñ‹
- **Ð Ð°Ð´Ð¸Ð°Ñ†Ð¸Ñ:** Ð½ÐµÑ‚ Ð¿Ð¾Ð»Ñ `radiation`, Ð½ÐµÑ‚ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð² Ñ€Ð°Ð´Ð¸Ð°Ñ†Ð¸Ð¸ Ð² Ð¼Ð¸Ñ€Ðµ
- **Suit upgrade mechanics:** Ñ€ÐµÑ†ÐµÐ¿Ñ‚Ñ‹ `Emergency O2 Canister` ÐµÑÑ‚ÑŒ, Ð½Ð¾ `MaxOxygen` Ð½ÐµÐ»ÑŒÐ·Ñ Ð°Ð¿Ð³Ñ€ÐµÐ¹Ð´Ð¸Ñ‚ÑŒ Ñ‡ÐµÑ€ÐµÐ· `OverrideStats()` â€” Ð½ÐµÑ‚ UI/flow Ð´Ð»Ñ ÑÑ‚Ð¾Ð³Ð¾
- **Crush Depth perimeter:** Ð¾Ð¿Ð¸ÑÐ°Ð½ Ð² README Â§Crush Depth ÐºÐ°Ðº Ð·Ð¾Ð½Ð° -7000m Ñ Ð¼Ð³Ð½Ð¾Ð²ÐµÐ½Ð½Ð¾Ð¹ ÑÐ¼ÐµÑ€Ñ‚ÑŒÑŽ â€” Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð°
- **Ð Ð°Ð·Ð³ÐµÑ€Ð¼ÐµÑ‚Ð¸Ð·Ð°Ñ†Ð¸Ñ:** Ð½ÐµÑ‚ `BreachEvent` Ð¿Ñ€Ð¸ ÑÑ‚Ð¾Ð»ÐºÐ½Ð¾Ð²ÐµÐ½Ð¸Ð¸ Ñ Ð¾ÑÑ‚Ñ€Ñ‹Ð¼Ð¸ Ð¾Ð±ÑŠÐµÐºÑ‚Ð°Ð¼Ð¸
- **Ð’Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ñ‹Ðµ ÑÑ„Ñ„ÐµÐºÑ‚Ñ‹:** Ð½ÐµÑ‚ screen effects Ð¿Ñ€Ð¸ ÐºÑ€Ð¸Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¾Ð¼ O2/Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ð¸

### ðŸŸ  CORE-2: Inventory â€” ÑÑ‚ÐµÐºÐ¸ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹, instance-level Ð½ÐµÑ‚

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ:** Ð”Ð¸Ð°Ð»Ð¾Ð³ Ñ Claude Opus 4.6 Ð¿Ð¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÑ‚ Pass 1-3 Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹: ÑÑ‚ÐµÐºÐ¸ (`_stackCounts[]`), DROP, USE (consumables), SORT, durability bars.

**Ð§Ñ‚Ð¾ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- `InventoryItemInstance` â€” per-item durability tracking (Ð½Ðµ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² ToolMetadata)
- Drag & drop Ð¿ÐµÑ€ÐµÑÑ‚Ð°Ð½Ð¾Ð²ÐºÐ° Ð² ÑÐµÑ‚ÐºÐµ (Pass A Ð¸Ð· Ð´Ð¸Ð°Ð»Ð¾Ð³Ð° â€” Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½)
- Category filter tabs (Pass B â€” Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½)
- Per-instance upgrades (Ð½ÐµÐ»ÑŒÐ·Ñ Ð°Ð¿Ð³Ñ€ÐµÐ¹Ð´Ð¸Ñ‚ÑŒ ÐºÐ¾Ð½ÐºÑ€ÐµÑ‚Ð½Ñ‹Ð¹ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚)
- Stack splitting (Ð½ÐµÐ»ÑŒÐ·Ñ Ñ€Ð°Ð·Ð´ÐµÐ»Ð¸Ñ‚ÑŒ ÑÑ‚Ð°Ðº Ð½Ð° Ð´Ð²Ð°)

### ðŸŸ  CORE-3: Construction â€” Power Grid Ð³Ð¾Ñ‚Ð¾Ð², habitat gameplay Ð½ÐµÑ‚

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ:** `PowerGridManager.cs` â€” production-grade BFS, merge/split. `PowerNode`, `PowerGrid` â€” Ð³Ð¾Ñ‚Ð¾Ð²Ñ‹.

**Ð§Ñ‚Ð¾ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- Ð§Ñ‚Ð¾ Ð´ÐµÐ»Ð°ÑŽÑ‚ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸? ÐÐµÑ‚ `HabitatModule` Ñ Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¼Ð¸ ÑÑ„Ñ„ÐµÐºÑ‚Ð°Ð¼Ð¸ (O2 refill, pressure seal, storage)
- Deconstruct-return path: `LaserCutter` Ð¼Ð¾Ð¶ÐµÑ‚ Ð´ÐµÐºÐ¾Ð½ÑÑ‚Ñ€ÑƒÐ¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ, Ð½Ð¾ Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‚ Ñ€ÐµÑÑƒÑ€ÑÐ¾Ð² Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½
- Pressure sealing: Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ð°Ñ Ð±Ð°Ð·Ð° Ð½Ðµ ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð·Ð¾Ð½Ñƒ Ñ Ð½Ð¾Ñ€Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¼ Ð´Ð°Ð²Ð»ÐµÐ½Ð¸ÐµÐ¼
- Snap system: `ModuleSocket` ÐµÑÑ‚ÑŒ, Ð½Ð¾ snap-to-grid gameplay Ð½Ðµ Ð¾Ñ‚Ð¿Ð¾Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½
- Power consumption: `PowerNode` ÐµÑÑ‚ÑŒ, Ð½Ð¾ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ñ‹/ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹ Ð½Ðµ Ð¿Ð¾Ñ‚Ñ€ÐµÐ±Ð»ÑÑŽÑ‚ Ð¸Ð· grid

### ðŸŸ  CORE-4: Transport â€” Ð½Ðµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- `GarpunScooter` â€” Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ñ‹Ð¹ ÑÐºÑƒÑ‚ÐµÑ€: Rigidbody + HectonFluidEngine + PlayerController override
- `CrabWalker` â€” IK walker: A* Pathfinding + Unity IK (Ð¿Ð°ÐºÐµÑ‚Ñ‹ ÐµÑÑ‚ÑŒ)
- Vehicle enter/exit interaction Ñ‡ÐµÑ€ÐµÐ· `IInteractable`
- Vehicle buoyancy Ñ‡ÐµÑ€ÐµÐ· `BuoyancyObject`
- Vehicle save state (Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ, Ñ‚Ð¾Ð¿Ð»Ð¸Ð²Ð¾)

### ðŸŸ  CORE-5: Barter System â€” BarterRuntimeSmokeTester ÐµÑÑ‚ÑŒ, Ð»Ð¾Ð³Ð¸ÐºÐ¸ Ð½ÐµÑ‚

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ:** `BarterRuntimeSmokeTester.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚ â€” Ð·Ð½Ð°Ñ‡Ð¸Ñ‚ ÑÐ¸ÑÑ‚ÐµÐ¼Ð° Ð¿Ð»Ð°Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð»Ð°ÑÑŒ. ÐÐ¾ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ `BarterSystem` Ð½ÐµÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- `BarterSystem` singleton Ñ drone inventory
- `DroneNPC` prefab Ñ `IInteractable`
- Exchange tab Ð² PDA (Hecton-OS)
- Pricing model (resource value table)
- Drone spawn/despawn Ñ‡ÐµÑ€ÐµÐ· `FaunaDirector`

### ðŸŸ  CORE-6: GPU Boids â€” compute shader ÐµÑÑ‚ÑŒ, ÑÑ†ÐµÐ½Ñ‹ Ð½ÐµÑ‚

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ:** `BoidSimulation.compute` + `BoidFishInstanced.shader` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‚ Ð² `_Project/Scripts`.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- `HectonBoidController.cs` â€” ÑƒÐ¶Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚! ÐÑƒÐ¶Ð½Ð° live ÑÑ†ÐµÐ½Ð°
- Boid prefabs Ñ `BoidFishInstanced` shader
- Ð˜Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ `FaunaDirector` (Ð±Ð¸Ð¾Ð¼-aware spawning ÐºÐ¾ÑÑÐºÐ¾Ð²)
- Collision avoidance Ñ terrain (BiomeSamplerCache)

### ðŸŸ  CORE-7: Cable Physics â€” Ð½Ðµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Verlet integration Ð´Ð»Ñ ÐºÐ°Ð±ÐµÐ»ÐµÐ¹ (ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ð¾ Ð² README Â§Stones)
- `CableComponent` Ñ N ÑÐµÐ³Ð¼ÐµÐ½Ñ‚Ð°Ð¼Ð¸
- Ð˜Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ `PowerNode` (Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ñ‹Ðµ ÐºÐ°Ð±ÐµÐ»Ð¸ Ð¼ÐµÐ¶Ð´Ñƒ Ð¼Ð¾Ð´ÑƒÐ»ÑÐ¼Ð¸)
- Collision Ñ terrain

## Ð ÐÐ—Ð”Ð•Ð› 4: WORLD & TERRAIN (Ð¿Ñ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ð°Ñ Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ñ)

### ðŸŸ¡ WORLD-1: MapMagic 5-biome enterprise graph Ð½Ðµ Ð³Ð¾Ñ‚Ð¾Ð²

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** README (2).md ÑÐ¾Ð´ÐµÑ€Ð¶Ð¸Ñ‚ Ð´ÐµÑ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ‚Ð°ÑÐº Ð½Ð° Ñ€ÐµÑ„Ð°ÐºÑ‚Ð¾Ñ€Ð¸Ð½Ð³ MM2 Ð³Ñ€Ð°Ñ„Ð° (Ñ€Ð°Ð·Ð´ÐµÐ» "MAPMAGIC TASK"), Ð½Ð¾ Ð¾Ð½ Ð½Ðµ Ð²Ñ‹Ð¿Ð¾Ð»Ð½ÐµÐ½.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Distorted gradient Ð´Ð»Ñ 5 Ð±Ð¸Ð¾Ð¼Ð¾Ð²
- Spline rifts
- Biome selectors
- ÐšÐ¾Ð½ÐºÑ€ÐµÑ‚Ð½Ñ‹Ðµ node deletions/additions/values Ð¸Ð· README

**Ð¢ÐµÐºÑƒÑ‰ÐµÐµ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ:** Ð Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ "small runtime palette" Ð¸Ð· 6-8 Ð±Ð¸Ð¾Ð¼Ð¾Ð², 108-biome matrix ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐºÐ°Ðº data layer.

### ðŸŸ¡ WORLD-2: ProximityColliderSystem Ð½Ðµ live

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð—Ð°Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¾ Ð² file:MAPMAGIC_WORLD_STACK_PLAN.md ÐºÐ°Ðº "Honest Current Tail".

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Live `ProximityColliderSystem` Ð² ÑÑ†ÐµÐ½Ðµ
- Collider-budget control Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡Ñ‘Ð½ Ðº Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð¹ physics budget ÑÐ¸ÑÑ‚ÐµÐ¼Ðµ
- Near-field physics split (Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð±Ð»Ð¸Ð·ÐºÐ¸Ðµ Ð¾Ð±ÑŠÐµÐºÑ‚Ñ‹ Ð¿Ð¾Ð»ÑƒÑ‡Ð°ÑŽÑ‚ full colliders)

### ðŸŸ¡ WORLD-3: ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ world fill

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** World zones/content sockets/population rules â€” data layer Ð³Ð¾Ñ‚Ð¾Ð². Ð ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ ÐºÐ¾Ð½Ñ‚ÐµÐ½Ñ‚Ð° Ð½ÐµÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Salvage pockets Ñ Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¼Ð¸ prefabs
- Service scars
- Power routes
- Relay canyons
- Biolum pockets
- Abyss edges
- Colony ruins (hybrid scatter/manual)

### ðŸŸ¡ WORLD-4: Terrain â€” 44 placeholder biome Ð¸Ð· 108

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð—Ð°Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¾ Ð² file:BIOME_MATRIX_108_PLAN.md.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Ð”ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ 44 placeholder Ð±Ð¸Ð¾Ð¼Ð¾Ð²
- Ð¡Ð²ÑÐ·ÑŒ matrix biomes Ñ zone plans
- MapMagic masks Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð¹ family
- Atmosphere overrides per biome
- Encounter rhythm per biome

### ðŸŸ¡ WORLD-5: ÐÐµÑ‚ Chunk Streaming (Ð¿Ð¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ð¾Ð³Ð¾)

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `WorldSliceDirector` Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ Ð´Ð»Ñ authored zones. Ð ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ tile streaming Ð½ÐµÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- ÐÐºÑ‚Ð¸Ð²Ð½Ð°Ñ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ°/Ð²Ñ‹Ð³Ñ€ÑƒÐ·ÐºÐ° Ð·Ð¾Ð½ Ð¿Ð¾ Ñ‡ÐµÐºÐ¿Ð¾Ð¸Ð½Ñ‚Ð°Ð¼
- Scene streaming Ñ LOD
- ÐžÑ‚ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ðµ ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ð¾Ð² Ð²Ð½Ðµ Ð·Ð¾Ð½Ñ‹ Ð²Ð¸Ð´Ð¸Ð¼Ð¾ÑÑ‚Ð¸
- Tile size 1000m (Ð¾Ð¿Ð¸ÑÐ°Ð½ Ð² README)

## Ð ÐÐ—Ð”Ð•Ð› 5: Ð’Ð˜Ð—Ð£ÐÐ› & ÐÐ¢ÐœÐžÐ¡Ð¤Ð•Ð Ð

### ðŸŸ¡ VIS-1: Scene View sky Ð±Ð°Ð³ Ð½Ðµ ÑƒÑÑ‚Ñ€Ð°Ð½Ñ‘Ð½

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð—Ð°Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¾ Ð² file:SCENE_SKY_NOTES.md. Ð§Ð°ÑÑ‚Ð¸Ñ‡Ð½Ð¾ Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¾, Ð¾ÑÑ‚Ð°Ñ‚Ð¾Ñ‡Ð½Ñ‹Ð¹ Ð´ÐµÑ„ÐµÐºÑ‚.

**Ð§Ñ‚Ð¾ Ð¾ÑÑ‚Ð°Ð»Ð¾ÑÑŒ:**

- ÐžÐ±Ð»Ð°ÐºÐ°/ÐºÐ°ÑÑ‚Ð¾Ð¼Ð½Ñ‹Ð¹ sky Ð² Scene View Ð½Ðµ ÑÐ¾Ð²Ð¿Ð°Ð´Ð°ÑŽÑ‚ Ñ Game view
- ÐÑƒÐ¶Ð½Ð° Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ° live-Ð¿Ð¾Ð»ÐµÐ¹ `HectonUnderwaterVisuals` Ð¸ `HectonAtmosphereManager`

### ðŸŸ¡ VIS-2: Bioluminescence Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð°

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** ÐŸÐ¾Ð»Ð½Ð°Ñ ÑÐ¿ÐµÑ†Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ñ Ð² README (2).md (Ñ€Ð°Ð·Ð´ÐµÐ» "Bioluminescence Rules").

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Shader-based glow (HDR emission + Bloom)
- Async pulsing (Time + ObjectPos sine)
- GPU particles
- 1% real lights
- Fog culling integration
- `Biolum Paste` ÐºÐ°Ðº Ñ€ÐµÑÑƒÑ€Ñ ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ, Ð½Ð¾ Ð²Ð¸Ð·ÑƒÐ°Ð» Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½

### ðŸŸ¡ VIS-3: LOD dithering Ð½Ðµ Ð½Ð°ÑÑ‚Ñ€Ð¾ÐµÐ½

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð£Ð¿Ð¾Ð¼ÑÐ½ÑƒÑ‚ Ð² README ÐºÐ°Ðº Ð¾Ñ‚Ð´ÐµÐ»ÑŒÐ½Ñ‹Ð¹ Ñ€Ð¸ÑÐº ("LOD dithering").

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° LOD cross-fade dithering Ð´Ð»Ñ Ð²ÑÐµÑ… major asset Ð³Ñ€ÑƒÐ¿Ð¿
- Amplify Impostors Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ð´Ð»Ñ Ð´Ð°Ð»ÑŒÐ½Ð¸Ñ… Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð²

### ðŸŸ¡ VIS-4: Celestial mechanics Ð½Ðµ Ð·Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ñ‹

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `HectonCelestialEngine.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ñ‹ "Aegir phases" Ð¸ "Tidal lock drift".

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- ÐŸÐ»Ð°Ð²Ð½Ð¾Ðµ Ð½Ð°Ð¿Ð»Ñ‹Ð²Ð°Ð½Ð¸Ðµ Aegir (Ð³Ð°Ð·Ð¾Ð²Ñ‹Ð¹ Ð³Ð¸Ð³Ð°Ð½Ñ‚ Ð½Ð° Ð½ÐµÐ±Ðµ)
- Tidal lock drift
- Eclipse mechanics (ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ñ‹ Ð² sky shader Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€Ð°Ñ…)

### ðŸŸ¡ VIS-5: Underwater visuals Ð½ÐµÐ¿Ð¾Ð»Ð½Ñ‹Ðµ

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `HectonUnderwaterVisuals.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, Ð½Ð¾:

- Volumetric fog underwater Ð½Ðµ Ð½Ð°ÑÑ‚Ñ€Ð¾ÐµÐ½ Ð´Ð»Ñ Ð²ÑÐµÑ… Ð³Ð»ÑƒÐ±Ð¸Ð½
- Caustics Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÑŽÑ‚
- Particle systems Ð´Ð»Ñ marine snow Ð½Ðµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹

## Ð ÐÐ—Ð”Ð•Ð› 6: Ð¢Ð•Ð¥ÐÐ˜Ð§Ð•Ð¡ÐšÐ˜Ð™ Ð”ÐžÐ›Ð“ â€” Ð”Ð•Ð¢ÐÐ›Ð¬ÐÐ«Ð™ ÐÐÐÐ›Ð˜Ð—

### ðŸ”µ TECH-0: Reload 130+ ÑÐµÐºÑƒÐ½Ð´ â€” Ð³Ð»Ð°Ð²Ð½Ð°Ñ Ð±Ð¾Ð»ÑŒ Ñ€Ð°Ð·Ñ€Ð°Ð±Ð¾Ñ‚ÐºÐ¸

**Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ:** `UNITY_RELOAD_FINDINGS.md` â€” `FinalizeReload` avg 125671ms. Wave 1 cleanup Ð¿Ñ€Ð¸Ð¼ÐµÐ½Ñ‘Ð½. ÐÐ¾:

- 83 runtime Ñ„Ð°Ð¹Ð»Ð° Ñ editor coupling (Ð¸Ð· 207 runtime Ñ„Ð°Ð¹Ð»Ð¾Ð² = 40%!)
- Ð¢Ð¾Ð¿ Ð½Ð°Ñ€ÑƒÑˆÐ¸Ñ‚ÐµÐ»Ð¸: `HectonUnderwaterVisuals` (23), `HectonVoxelEngine` (15), `SkySystemFollowCamera` (13)
- ÐÐµÑ‚ `_Project` asmdef split â€” Ð²ÐµÑÑŒ ÐºÐ¾Ð´ ÐºÐ¾Ð¼Ð¿Ð¸Ð»Ð¸Ñ€ÑƒÐµÑ‚ÑÑ Ð² Ð¾Ð´Ð¸Ð½ assembly
- Vendor hotspots: Bakery, GPU Instancer, A* Pathfinding, MapMagic, Amplify Impostors

**Ð¦ÐµÐ»ÑŒ:** <30 ÑÐµÐº reload Ñ‡ÐµÑ€ÐµÐ· asmdef split + Ð¿Ñ€Ð¾Ð´Ð¾Ð»Ð¶ÐµÐ½Ð¸Ðµ vendor cleanup

### ðŸ”µ TECH-1: 83 runtime Ñ„Ð°Ð¹Ð»Ð° Ñ editor coupling â€” Ð½ÐµÑ‚ asmdef split

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð—Ð°Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¾ Ð² file:UNITY_PROJECT_SPLIT_REPORT.md.

**Ð¢Ð¾Ð¿ Ð½Ð°Ñ€ÑƒÑˆÐ¸Ñ‚ÐµÐ»ÐµÐ¹:**

- `HectonUnderwaterVisuals.cs` â€” 23 coupling signals
- `HectonVoxelEngine.cs` â€” 15
- `SkySystemFollowCamera.cs` â€” 13
- `BaseModule.cs` â€” 10
- `ProximityColliderSystem.cs` â€” 10

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Ð¡Ð½Ð°Ñ‡Ð°Ð»Ð° Ð¾Ñ‡Ð¸ÑÑ‚Ð¸Ñ‚ÑŒ editor coupling Ð² Ñ‚Ð¾Ð¿-Ð½Ð°Ñ€ÑƒÑˆÐ¸Ñ‚ÐµÐ»ÑÑ…
- Ð—Ð°Ñ‚ÐµÐ¼ ÑÐ¾Ð·Ð´Ð°Ñ‚ÑŒ `_Project` asmdef split (runtime + editor assemblies)
- Ð¦ÐµÐ»ÑŒ: ÑƒÑÐºÐ¾Ñ€Ð¸Ñ‚ÑŒ reload Ñ 130+ ÑÐµÐº Ð´Ð¾ <30 ÑÐµÐº

### ðŸ”µ TECH-2: ÐÐµÑ‚ XML Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ð¸

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð¯Ð²Ð½Ð¾ ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ð¾ Ð² file:task.md ÐºÐ°Ðº Ð½ÐµÐ·Ð°ÐºÑ€Ñ‹Ñ‚Ñ‹Ð¹ Ð¿ÑƒÐ½ÐºÑ‚: "Add comprehensive XML documentation".

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- XML doc Ð´Ð»Ñ Ð²ÑÐµÑ… public API Ð² `_Project/Scripts`
- ÐžÑÐ¾Ð±ÐµÐ½Ð½Ð¾: InputManager, PlayerInventory, PlayerToolManager, HectonSurvivalSystem

### ðŸ”µ TECH-3: ÐÐµÑ‚ CI pipeline

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `.github/` Ð´Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€Ð¸Ñ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, Ð½Ð¾ ÑÐ¾Ð´ÐµÑ€Ð¶Ð¸Ð¼Ð¾Ðµ Ð½ÐµÐ¸Ð·Ð²ÐµÑÑ‚Ð½Ð¾.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾ ÑÐ¾Ð³Ð»Ð°ÑÐ½Ð¾ README:**

- CI Ð½Ð° ÐºÐ¾Ð¼Ð¿Ð¸Ð»ÑÑ†Ð¸ÑŽ
- Asset health check workflow
- ÐÐ²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð³ÐµÐ½ÐµÑ€Ð°Ñ‚Ð¾Ñ€ TODO Ð² `task.md`

### ðŸ”µ TECH-4: ÐÐµÑ‚ Ð¿Ñ€Ð¾Ñ„Ð°Ð¹Ð»Ð¸Ð½Ð³-Ð¼ÐµÑ‚Ñ€Ð¸Ðº ÐºÐ°Ðº Ð´Ð¾ÑÑ‚Ð¸Ð¶Ð¸Ð¼Ð¾Ð¹ Ñ†ÐµÐ»Ð¸

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð£Ð¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ð¾ Ð² file:HADES_HECTON8_tasks.md ÐºÐ°Ðº Ð½ÐµÐ·Ð°ÐºÑ€Ñ‹Ñ‚Ñ‹Ð¹ Ð¿ÑƒÐ½ÐºÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Profiling benchmarks: FPS, memory, GC, draw calls
- Ð¦ÐµÐ»ÐµÐ²Ñ‹Ðµ Ð¼ÐµÑ‚Ñ€Ð¸ÐºÐ¸: 30 FPS Ð½Ð° MX350, â‰¤2GB VRAM
- ÐÐ²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ñ‚ÐµÑÑ‚Ñ‹ Ð¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ÑÑ‚Ð¸

### ðŸ”µ TECH-5: MCP console observability Ð½ÐµÐ½Ð°Ð´Ñ‘Ð¶Ð½Ð°

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð£Ð¿Ð¾Ð¼ÑÐ½ÑƒÑ‚Ð¾ Ð² Ð½ÐµÑÐºÐ¾Ð»ÑŒÐºÐ¸Ñ… Ñ„Ð°Ð¹Ð»Ð°Ñ… ÐºÐ°Ðº "tooling-observability tail".

**ÐŸÑ€Ð¾Ð±Ð»ÐµÐ¼Ð°:** MCP Ð½Ðµ Ð²ÑÐµÐ³Ð´Ð° Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ live component snapshots Ð² play mode, Ñ‡Ñ‚Ð¾ Ð·Ð°Ñ‚Ñ€ÑƒÐ´Ð½ÑÐµÑ‚ Ð²ÐµÑ€Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸ÑŽ.

### ðŸ”µ TECH-6: ÐÐµÑ‚ Localization

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `LocalizationManager.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚ (ÑƒÐ¿Ð¾Ð¼ÑÐ½ÑƒÑ‚ Ð² split report), Ð½Ð¾ ÑÐ¸ÑÑ‚ÐµÐ¼Ð° Ð½Ðµ Ñ€Ð°Ð·Ð²Ñ‘Ñ€Ð½ÑƒÑ‚Ð°.

## Ð ÐÐ—Ð”Ð•Ð› 7: ÐšÐžÐÐ¢Ð•ÐÐ¢ & ÐŸÐ ÐžÐ“Ð Ð•Ð¡Ð¡Ð˜Ð¯

### ðŸŸ¢ CONTENT-1: Ð˜Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ñ‹ â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ scaffold, Ð½ÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ñ… visuals

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð’ÑÐµ 12 Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð¾Ð² Ð¸Ð¼ÐµÑŽÑ‚ placeholder cube visuals.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- 3D Ð¼Ð¾Ð´ÐµÐ»Ð¸ Ð´Ð»Ñ Ð²ÑÐµÑ… 12 held prefabs
- ÐÐ½Ð¸Ð¼Ð°Ñ†Ð¸Ð¸ (equip/use/holster)
- Audio refs (Ð·Ð²ÑƒÐºÐ¸ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ)
- VFX (Ð»Ð°Ð·ÐµÑ€, Ð³Ð°Ñ€Ð¿ÑƒÐ½, Ð¿Ñ€Ð¾Ð¿ÑƒÐ»ÑŒÑÐ¸Ñ)

### ðŸŸ¢ CONTENT-2: ÐÐµÑ‚ Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ð»ÑƒÑ‚Ð° Ð² Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¾Ð¼ Ð¼Ð¸Ñ€Ðµ

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `Resource_FieldSources` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚ ÐºÐ°Ðº authored placeholder. Ð ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ scatter Ð½ÐµÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ðµ prefabs Ð´Ð»Ñ Ð²ÑÐµÑ… 20 raw resources Ð² Ð¼Ð¸Ñ€Ðµ
- Sealed caches (Ð¾Ñ‚ÐºÑ€Ñ‹Ð²Ð°ÑŽÑ‚ÑÑ LaserCutter)
- Biological harvest nodes (Ð´Ð»Ñ Knife)
- Rare deep deposits

### ðŸŸ¢ CONTENT-3: ÐÐµÑ‚ NPC / Drone barter

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** Ð£Ð¿Ð¾Ð¼ÑÐ½ÑƒÑ‚ Ð² README ÐºÐ°Ðº "drone barter" Ð¸ "Exchange/barter system".

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Drone NPC prefabs
- Barter logic
- Exchange tab Ð² PDA

### ðŸŸ¢ CONTENT-4: ÐÐµÑ‚ Ð·Ð²ÑƒÐºÐ¾Ð²Ð¾Ð³Ð¾ Ð´Ð¸Ð·Ð°Ð¹Ð½Ð°

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `SpatialAudioManager.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, `MasterAudio` Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð¸Ñ€Ð¾Ð²Ð°Ð½. Ð ÐµÐ°Ð»ÑŒÐ½Ð¾Ð³Ð¾ ÐºÐ¾Ð½Ñ‚ÐµÐ½Ñ‚Ð° Ð½ÐµÑ‚.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- Ambient underwater soundscape
- Tool sounds
- Survival alerts (low O2, pressure warning)
- Footstep audio (PlayerFootstepAudio.cs ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚)
- Bioluminescence pulse sounds

### ðŸŸ¢ CONTENT-5: ÐÐµÑ‚ Main Menu Ð¿Ð¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ð¾Ð³Ð¾

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** `MainMenuController.cs` ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚, Ð½Ð¾ ÑÑ†ÐµÐ½Ð° `01_MAIN_MENU` Ð½Ðµ Ð¾Ð¿Ð¸ÑÐ°Ð½Ð° ÐºÐ°Ðº Ð³Ð¾Ñ‚Ð¾Ð²Ð°Ñ.

**Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾:**

- ÐŸÐ¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ñ‹Ð¹ Main Menu Ñ Continue/New Game/Settings/Quit
- Loading screen
- Ð˜Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ Save System

### ðŸŸ¢ CONTENT-6: ÐÐµÑ‚ Hecton-OS AR tabs Ð¿Ð¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ñ‹Ñ…

**Ð¡Ñ‚Ð°Ñ‚ÑƒÑ:** README Ð¾Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚ 6 Ð²ÐºÐ»Ð°Ð´Ð¾Ðº: Cargo, Blueprints, Diagnostics, Network, Spectrum, Exchange. Ð ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ñ‹: Inventory, Loadout, DataLog, Controls.

**Ð§Ñ‚Ð¾ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²ÑƒÐµÑ‚:**

- **Network tab** â€” beacon network visualization
- **Spectrum tab** â€” environmental scanner data
- **Exchange tab** â€” barter interface
- **Diagnostics** â€” Ñ‡Ð°ÑÑ‚Ð¸Ñ‡Ð½Ð¾ ÐµÑÑ‚ÑŒ Ð² DataLog

## Ð ÐÐ—Ð”Ð•Ð› 8: ÐŸÐžÐ›ÐÐ«Ð™ ÐŸÐ›ÐÐ Ð ÐÐ—Ð ÐÐ‘ÐžÐ¢ÐšÐ˜ (ÐžÐ‘ÐÐžÐ’Ð›ÐÐÐÐ«Ð™)

### ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚Ð¸Ð·Ð°Ñ†Ð¸Ñ Ð¿Ð¾ Ñ„Ð°Ð·Ð°Ð¼

```mermaid
graph TD
    P0[Ð¤ÐÐ—Ð 0: Ð¡Ñ‚Ð°Ð±Ð¸Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ] --> P1[Ð¤ÐÐ—Ð 1: Core Loop MVP]
    P1 --> P2[Ð¤ÐÐ—Ð 2: World & Content]
    P2 --> P3[Ð¤ÐÐ—Ð 3: Polish & AI]
    P3 --> P4[Ð¤ÐÐ—Ð 4: Performance & Release]
```

### ðŸ“‹ Ð¤ÐÐ—Ð 0: Ð¡Ñ‚Ð°Ð±Ð¸Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ (Ñ‚ÐµÑ…Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ñ„ÑƒÐ½Ð´Ð°Ð¼ÐµÐ½Ñ‚)

| # | Ð—Ð°Ð´Ð°Ñ‡Ð° | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ | ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚ |
| --- | --- | --- | --- |
| 0.1 | Floating Origin implementation | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸ”´ ÐšÐ Ð˜Ð¢Ð˜Ð§ÐÐž |
| 0.2 | GPUI Rock Shader â€” Ð´Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ GPU Instancer Setup node | Shader ÐµÑÑ‚ÑŒ, node Ð½ÐµÑ‚ | ðŸ”´ ÐšÐ Ð˜Ð¢Ð˜Ð§ÐÐž |
| 0.3 | Ð¡Ñ†ÐµÐ½Ñ‹ 00_BOOTSTRAP, 01_MAIN_MENU, XX_SANDBOX | SceneBootstrap.cs Ð³Ð¾Ñ‚Ð¾Ð² | ðŸ”´ ÐšÐ Ð˜Ð¢Ð˜Ð§ÐÐž |
| 0.4 | ResourceNode ISaveable + WorldStateManager Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ | ÐšÐ¾Ð´ ÐµÑÑ‚ÑŒ, ISaveable Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 0.5 | ConstructionManager ISaveable | ÐšÐ¾Ð´ ÐµÑÑ‚ÑŒ, ISaveable Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 0.6 | ProximityColliderSystem live Ð² ÑÑ†ÐµÐ½Ðµ | ÐšÐ¾Ð´ Ð³Ð¾Ñ‚Ð¾Ð², ÑÑ†ÐµÐ½Ð° Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 0.7 | Save Station UI â†’ SaveManager.SaveGameAsync() Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ðµ | UI ÐµÑÑ‚ÑŒ, wiring Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 0.8 | asmdef split (Ð¾Ñ‡Ð¸ÑÑ‚ÐºÐ° Ñ‚Ð¾Ð¿-10 editor coupling) | Wave 1 done, Ð½ÑƒÐ¶ÐµÐ½ split | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |

### ðŸ“‹ Ð¤ÐÐ—Ð 1: Core Loop MVP

| # | Ð—Ð°Ð´Ð°Ñ‡Ð° | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ | ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚ |
| --- | --- | --- | --- |
| 1.1 | Survival: Temperature + Radiation Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€Ñ‹ | Pressure ÐµÑÑ‚ÑŒ, temp/rad Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 1.2 | Crush Depth perimeter (-7000m instant death zone) | ÐÐµ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð¾ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 1.3 | Inventory drag & drop + category filter tabs | Ð¡Ñ‚ÐµÐºÐ¸ ÐµÑÑ‚ÑŒ, drag Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 1.4 | Voxel Cave â†’ MapMagic Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ (dents + seams) | ÐžÐ±Ð° Ð³Ð¾Ñ‚Ð¾Ð²Ñ‹, Ð½Ðµ ÑÐ²ÑÐ·Ð°Ð½Ñ‹ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 1.5 | HabitatModule gameplay (O2 refill, pressure seal, storage) | PowerGrid Ð³Ð¾Ñ‚Ð¾Ð², ÑÑ„Ñ„ÐµÐºÑ‚Ð¾Ð² Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 1.6 | Construction deconstruct-return path | LaserCutter ÐµÑÑ‚ÑŒ, return Ð½ÐµÑ‚ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 1.7 | Sealed caches prefabs (LaserCutter interaction) | Ð˜Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚ Ð³Ð¾Ñ‚Ð¾Ð², Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² Ð½ÐµÑ‚ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 1.8 | Biological harvest nodes (Knife interaction) | Ð˜Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚ Ð³Ð¾Ñ‚Ð¾Ð², Ð¾Ð±ÑŠÐµÐºÑ‚Ð¾Ð² Ð½ÐµÑ‚ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 1.9 | BarterSystem + DroneNPC + Exchange PDA tab | BarterSmokeTester ÐµÑÑ‚ÑŒ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 1.10 | GPU Boids live ÑÑ†ÐµÐ½Ð° (HectonBoidController) | Compute shader + controller ÐµÑÑ‚ÑŒ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |

### ðŸ“‹ Ð¤ÐÐ—Ð 2: World & Content

| # | Ð—Ð°Ð´Ð°Ñ‡Ð° | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ | ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚ |
| --- | --- | --- | --- |
| 2.1 | GPUI Rock instancing live (Ð¿Ð¾ÑÐ»Ðµ shader fix) | Shader fix â†’ activate | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 2.2 | MapMagic 5-biome enterprise graph refactor | Ð¢ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð³Ñ€Ð°Ñ„ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 2.3 | Hybrid density: near/mid/far world fill Ñ Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¼Ð¸ prefabs | WorldSliceDirector Ð³Ð¾Ñ‚Ð¾Ð² | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 2.4 | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ðµ prefabs Ð´Ð»Ñ 20 raw resources Ð² Ð¼Ð¸Ñ€Ðµ | Data ÐµÑÑ‚ÑŒ, prefabs Ð½ÐµÑ‚ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 2.5 | `HectonBiolumMaster.shader` â€” Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ Ðº Ð¾Ð±ÑŠÐµÐºÑ‚Ð°Ð¼ | Shader ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚! | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.6 | 44 placeholder biome Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ | 64 Ð¸Ð· 108 Ð³Ð¾Ñ‚Ð¾Ð²Ñ‹ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.7 | Colony ruins (hybrid scatter/manual) | WorldContentSocket Ð³Ð¾Ñ‚Ð¾Ð² | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.8 | Marine snow particle system | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.9 | Caustics underwater shader | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.10 | Chunk streaming (tile-based, 1000m tiles) | WorldSliceDirector Ñ‡Ð°ÑÑ‚Ð¸Ñ‡Ð½Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 2.11 | Celestial mechanics: Aegir phases + tidal lock | HectonCelestialEngine ÐµÑÑ‚ÑŒ | ðŸŸ¢ ÐÐ˜Ð—ÐšÐ˜Ð™ |
| 2.12 | Cable physics (Verlet) Ð´Ð»Ñ power grid Ð²Ð¸Ð·ÑƒÐ°Ð»Ð° | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¢ ÐÐ˜Ð—ÐšÐ˜Ð™ |

### ðŸ“‹ Ð¤ÐÐ—Ð 3: AI & Polish

| # | Ð—Ð°Ð´Ð°Ñ‡Ð° | Ð ÐµÐ°Ð»ÑŒÐ½Ñ‹Ð¹ ÑÑ‚Ð°Ñ‚ÑƒÑ | ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚ |
| --- | --- | --- | --- |
| 3.1 | Candice AI behavior trees Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ | HectonBaseAI FSM Ð³Ð¾Ñ‚Ð¾Ð² | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 3.2 | NoiseSystem + LightDetectionSystem Ð´Ð»Ñ AI Ñ€ÐµÐ°ÐºÑ†Ð¸Ð¹ | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 3.3 | Leviathan types (Ð¼ÐµÐ³Ð°-ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°) | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.4 | Garpun scooter Ñ‚Ñ€Ð°Ð½ÑÐ¿Ð¾Ñ€Ñ‚ | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.5 | Crab walker Ñ IK | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¢ ÐÐ˜Ð—ÐšÐ˜Ð™ |
| 3.6 | Tool VFX (Ð»Ð°Ð·ÐµÑ€, Ð³Ð°Ñ€Ð¿ÑƒÐ½, Ð¿Ñ€Ð¾Ð¿ÑƒÐ»ÑŒÑÐ¸Ñ) | Placeholder visuals | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.7 | Tool animations (equip/use/holster) | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.8 | Ambient underwater soundscape | SpatialAudioManager Ð³Ð¾Ñ‚Ð¾Ð² | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.9 | Tool sounds | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.10 | Survival alerts audio (low O2, pressure) | ÐÐµ Ð½Ð°Ñ‡Ð°Ñ‚Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.11 | LOD dithering + Amplify Impostors Ð½Ð°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° | ÐÐµ Ð½Ð°ÑÑ‚Ñ€Ð¾ÐµÐ½Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.12 | Scene View sky Ð±Ð°Ð³ Ñ„Ð¸Ð½Ð°Ð»ÑŒÐ½Ð¾Ðµ Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ | Ð§Ð°ÑÑ‚Ð¸Ñ‡Ð½Ð¾ Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¾ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.13 | Main Menu Ð¿Ð¾Ð»Ð½Ð¾Ñ†ÐµÐ½Ð½Ñ‹Ð¹ (01_MAIN_MENU ÑÑ†ÐµÐ½Ð°) | MainMenuController ÐµÑÑ‚ÑŒ | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 3.14 | Localization Ñ€Ð°Ð·Ð²Ñ‘Ñ€Ñ‚Ñ‹Ð²Ð°Ð½Ð¸Ðµ (English.json + Russian.json) | Ð¤Ð°Ð¹Ð»Ñ‹ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‚! | ðŸŸ¢ ÐÐ˜Ð—ÐšÐ˜Ð™ |

### ðŸ“‹ Ð¤ÐÐ—Ð 4: Performance & Release Prep

| # | Ð—Ð°Ð´Ð°Ñ‡Ð° | Ð¤Ð°Ð¹Ð»-Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸Ðº | ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚ |
| --- | --- | --- | --- |
| 4.1 | asmdef split (_Project runtime + editor) | UNITY_PROJECT_SPLIT_REPORT.md | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 4.2 | Profiling benchmarks (FPS/GC/drawcalls) | HADES_HECTON8_tasks.md | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 4.3 | VRAM budget checks + texture atlas | README (2).md Â§Performance | ðŸŸ  Ð’Ð«Ð¡ÐžÐšÐ˜Ð™ |
| 4.4 | Job System + Burst Ð´Ð»Ñ AI/noise/physics grids | README (2).md Â§Performance | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 4.5 | CI pipeline (ÐºÐ¾Ð¼Ð¿Ð¸Ð»ÑÑ†Ð¸Ñ + asset health) | HADES_HECTON8_tasks.md | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 4.6 | XML Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ð°Ñ†Ð¸Ñ Ð²ÑÐµÐ³Ð¾ public API | task.md | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 4.7 | Localization system Ñ€Ð°Ð·Ð²Ñ‘Ñ€Ñ‚Ñ‹Ð²Ð°Ð½Ð¸Ðµ | UNITY_PROJECT_SPLIT_REPORT.md | ðŸŸ¢ ÐÐ˜Ð—ÐšÐ˜Ð™ |
| 4.8 | Quality profiles (Abyss/Surface/Orbit) | README (2).md Â§Technical | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 4.9 | Texture compression audit (max 2048Ã—2048) | README (2).md Â§Technical | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |
| 4.10 | Overdraw limits audit | README (2).md Â§Stones | ðŸŸ¡ Ð¡Ð Ð•Ð”ÐÐ˜Ð™ |

## Ð¡Ð’ÐžÐ”ÐÐÐ¯ Ð¢ÐÐ‘Ð›Ð˜Ð¦Ð ÐÐ•Ð”ÐžÐ ÐÐ‘ÐžÐ¢ÐžÐš (v2.0 â€” Ð¿Ð¾ÑÐ»Ðµ Ð°Ð½Ð°Ð»Ð¸Ð·Ð° ÐºÐ¾Ð´Ð°)

| ÐšÐ°Ñ‚ÐµÐ³Ð¾Ñ€Ð¸Ñ | ÐšÑ€Ð¸Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… | Ð’Ñ‹ÑÐ¾ÐºÐ¸Ñ… | Ð¡Ñ€ÐµÐ´Ð½Ð¸Ñ… | ÐÐ¸Ð·ÐºÐ¸Ñ… | Ð˜Ñ‚Ð¾Ð³Ð¾ |
| --- | --- | --- | --- | --- | --- |
| Ð‘Ð»Ð¾ÐºÐµÑ€Ñ‹ | 6 | â€” | â€” | â€” | **6** |
| Core Gameplay | â€” | 5 | 5 | â€” | **10** |
| World & Terrain | â€” | 4 | 8 | 2 | **14** |
| Ð’Ð¸Ð·ÑƒÐ°Ð» | â€” | 1 | 4 | 1 | **6** |
| Ð¢ÐµÑ…Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ð¹ Ð´Ð¾Ð»Ð³ | â€” | 3 | 3 | 1 | **7** |
| ÐšÐ¾Ð½Ñ‚ÐµÐ½Ñ‚ | â€” | â€” | 5 | 3 | **8** |
| **Ð˜Ð¢ÐžÐ“Ðž** | **6** | **13** | **25** | **7** | **51** |

<user_quoted_section>Ð’Ð°Ð¶Ð½Ð¾: ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ð·Ð°Ð´Ð°Ñ‡ Ð²Ñ‹Ñ€Ð¾ÑÐ»Ð¾ Ð½Ðµ Ð¿Ð¾Ñ‚Ð¾Ð¼Ñƒ Ñ‡Ñ‚Ð¾ ÑÑ‚Ð°Ð»Ð¾ Ñ…ÑƒÐ¶Ðµ â€” Ð° Ð¿Ð¾Ñ‚Ð¾Ð¼Ñƒ Ñ‡Ñ‚Ð¾ Ð°Ð½Ð°Ð»Ð¸Ð· ÐºÐ¾Ð´Ð° Ð²Ñ‹ÑÐ²Ð¸Ð» ÑÐºÑ€Ñ‹Ñ‚Ñ‹Ðµ Ð²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾ÑÑ‚Ð¸ (biolum shader ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ, boid compute ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ, localization Ñ„Ð°Ð¹Ð»Ñ‹ ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ) Ð¸ ÑƒÑ‚Ð¾Ñ‡Ð½Ð¸Ð» Ñ€ÐµÐ°Ð»ÑŒÐ½Ñ‹Ðµ gaps.</user_quoted_section>

## ÐšÐ›Ð®Ð§Ð•Ð’Ð«Ð• ÐŸÐ Ð˜ÐÐ¦Ð˜ÐŸÐ« (Ð¸Ð· README (2).md â€” Ð½ÐµÐ»ÑŒÐ·Ñ Ð½Ð°Ñ€ÑƒÑˆÐ°Ñ‚ÑŒ)

<user_quoted_section>Ð­Ñ‚Ð¸ Ð¿Ñ€Ð°Ð²Ð¸Ð»Ð° Ð´Ð¾Ð»Ð¶Ð½Ñ‹ ÑÐ¾Ð±Ð»ÑŽÐ´Ð°Ñ‚ÑŒÑÑ Ð¿Ñ€Ð¸ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ ÐšÐÐ–Ð”ÐžÐ™ Ð·Ð°Ð´Ð°Ñ‡Ð¸:</user_quoted_section>

1. **Zero GC** Ð² Update/FixedUpdate/LateUpdate â€” Ð½Ð¸ÐºÐ°ÐºÐ¸Ñ… LINQ, FindObjectOfType, GetComponent Ð² Ð³Ð¾Ñ€ÑÑ‡Ð¸Ñ… Ñ†Ð¸ÐºÐ»Ð°Ñ…
2. **VRAM â‰¤ 2GB** â€” Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ñ‹ max 2048Ã—2048 Ð´Ð»Ñ terrain/walls, 1024/512 Ð´Ð»Ñ props
3. **30+ FPS Ð½Ð° MX350** â€” Ñ†ÐµÐ»ÐµÐ²Ð°Ñ Ð¿Ð»Ð°Ñ‚Ñ„Ð¾Ñ€Ð¼Ð° Ð´Ð»Ñ Ð²ÑÐµÑ… Ð·Ð°Ð´Ð°Ñ‡
4. **Prefab-centric workflow** â€” Ð½Ð¸ÐºÐ°ÐºÐ¾Ð¹ Ð¿Ñ€Ð°Ð²ÐºÐ¸ ÑÑ†ÐµÐ½Ñ‹ Ð½Ð°Ð¿Ñ€ÑÐ¼ÑƒÑŽ, Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Prefab Mode/Variants
5. **Data-driven** â€” ScriptableObjects Ð²Ð¼ÐµÑÑ‚Ð¾ Ñ…Ð°Ñ€Ð´ÐºÐ¾Ð´Ð°
6. **No tessellation** â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Normal/Parallax Ð² MicroSplat
7. **Additive Scene Loading** â€” Ð½Ð¸ÐºÐ°ÐºÐ¸Ñ… Ð¼Ð¾Ð½Ð¾Ð»Ð¸Ñ‚Ð½Ñ‹Ñ… ÑÑ†ÐµÐ½
8. **Git LFS** â€” Ð²ÑÐµ Ð±Ð¸Ð½Ð°Ñ€Ð½Ñ‹Ðµ Ð°ÑÑÐµÑ‚Ñ‹ Ñ‡ÐµÑ€ÐµÐ· LFS

## Ð¢Ð•ÐšÐ£Ð©Ð•Ð• Ð¡ÐžÐ¡Ð¢ÐžÐ¯ÐÐ˜Ð• Ð¡Ð˜Ð¡Ð¢Ð•Ðœ (Ñ‡ÐµÑÑ‚Ð½Ð°Ñ Ð¾Ñ†ÐµÐ½ÐºÐ° v2.0)

```mermaid
graph TD
    subgraph Ð“ÐžÐ¢ÐžÐ’Ðž_ÐšÐžÐ”
        A1[Input System âœ…]
        A2[HUD V4 âœ…]
        A3[PDA Shell + Tabs âœ…]
        A4[12 Tools scaffold âœ…]
        A5[Fabricator + Recipes âœ…]
        A6[Resource Economy data âœ…]
        A7[World Stack architecture âœ…]
        A8[108 Biome Matrix data âœ…]
        A9[Buoyancy/Current âœ…]
        A10[Beacon Network âœ…]
        A11[SaveManager async âœ…]
        A12[HectonBaseAI FSM âœ…]
        A13[FaunaDirector âœ…]
        A14[PowerGridManager âœ…]
        A15[VoxelEngine + CaveGraph âœ…]
        A16[SceneBootstrap async âœ…]
        A17[Inventory Stacks/USE/DROP âœ…]
        A18[BoidSimulation.compute âœ…]
        A19[HectonBiolumMaster.shader âœ…]
        A20[Localization EN+RU âœ…]
    end

    subgraph ÐÐ£Ð–ÐÐ_Ð¡Ð¦Ð•ÐÐ_Ð˜Ð›Ð˜_WIRING
        B1[ProximityColliderSystem ðŸ”¶]
        B2[GPU Boids live ðŸ”¶]
        B3[Biolum shader Ð½Ð° Ð¾Ð±ÑŠÐµÐºÑ‚Ð°Ñ… ðŸ”¶]
        B4[Save Station wiring ðŸ”¶]
        B5[ResourceNode ISaveable ðŸ”¶]
        B6[GPUI Rock shader fix ðŸ”¶]
    end

    subgraph ÐÐ•_ÐÐÐ§ÐÐ¢Ðž
        C1[Floating Origin âŒ]
        C2[Transport âŒ]
        C3[Barter/DroneNPC âŒ]
        C4[Temperature/Radiation âŒ]
        C5[Real World Fill prefabs âŒ]
        C6[Caveâ†’MapMagic seams âŒ]
        C7[Sound Design âŒ]
        C8[HabitatModule effects âŒ]
    end
```

## Ð¡ÐšÐ Ð«Ð¢Ð«Ð• Ð’ÐžÐ—ÐœÐžÐ–ÐÐžÐ¡Ð¢Ð˜ (Ð½Ð°Ð¹Ð´ÐµÐ½Ñ‹ Ð² ÐºÐ¾Ð´Ðµ)

<user_quoted_section>Ð­Ñ‚Ð¸ Ð²ÐµÑ‰Ð¸ ÑƒÐ¶Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‚ Ð² Ð¿Ñ€Ð¾ÐµÐºÑ‚Ðµ, Ð½Ð¾ Ð½Ðµ Ð°ÐºÑ‚Ð¸Ð²Ð¸Ñ€Ð¾Ð²Ð°Ð½Ñ‹ Ð¸Ð»Ð¸ Ð½Ðµ Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡ÐµÐ½Ñ‹:</user_quoted_section>

| Ð¤Ð°Ð¹Ð» | Ð§Ñ‚Ð¾ ÐµÑÑ‚ÑŒ | Ð§Ñ‚Ð¾ Ð½ÑƒÐ¶Ð½Ð¾ ÑÐ´ÐµÐ»Ð°Ñ‚ÑŒ |
| --- | --- | --- |
| `HectonBiolumMaster.shader` | Ð“Ð¾Ñ‚Ð¾Ð²Ñ‹Ð¹ biolum shader | ÐŸÑ€Ð¸Ð¼ÐµÐ½Ð¸Ñ‚ÑŒ Ðº Ð¾Ð±ÑŠÐµÐºÑ‚Ð°Ð¼, Ð½Ð°ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÑŒ HDR emission + Bloom |
| `BoidSimulation.compute` + `BoidFishInstanced.shader` | GPU boids pipeline | Ð¡Ð¾Ð·Ð´Ð°Ñ‚ÑŒ prefabs, Ð´Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ Ð² ÑÑ†ÐµÐ½Ñƒ Ñ‡ÐµÑ€ÐµÐ· HectonBoidController |
| `HectonBoidController.cs` | Boid manager | Ð”Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ Ð² [MANAGERS], Ð½Ð°ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÑŒ Ð±Ð¸Ð¾Ð¼-aware spawning |
| `English.json` + `Russian.json` | Ð›Ð¾ÐºÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ | Ð—Ð°Ð¿Ð¾Ð»Ð½Ð¸Ñ‚ÑŒ ÐºÐ»ÑŽÑ‡Ð¸, Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ LocalizationManager Ðº UI |
| `BarterRuntimeSmokeTester.cs` | Barter test harness | Ð ÐµÐ°Ð»Ð¸Ð·Ð¾Ð²Ð°Ñ‚ÑŒ BarterSystem, Ð¿Ð¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ |
| `AcousticZoneController.cs` | Acoustic zones | Ð Ð°Ð·Ð¼ÐµÑÑ‚Ð¸Ñ‚ÑŒ Ð² ÑÑ†ÐµÐ½Ðµ, Ð½Ð°ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÑŒ underwater reverb |
| `LandingImpactVFX.cs` | Landing VFX | ÐŸÐ¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ Ðº PlayerMovement |
| `CameraJuiceProcessor.cs` | Camera juice (bobbing, shake, FOV) | ÐŸÐ¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ Ðº PlayerMovement (Ð¸Ð· Ð±ÐµÐºÐ»Ð¾Ð³Ð°) |
| `PlayerThrusterAudio.cs` | Thruster audio | ÐŸÐ¾Ð´ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ Ðº Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸ÑŽ |
| `PlayerFootstepAudio.cs` | Footstep audio | ÐÐ°ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÑŒ surface detection |
