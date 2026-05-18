# GOD OBJECT AUDIT â€” Player & Submarine Prefab Component Analysis
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



> **Status:** ETA SANITIZED
> **Mandates Followed:** AGENTS.md Â§ Architecture First Â· Â§ Ownership / Ambiguity
> **Target:** â‰¤25 components on root GameObject

---

## 1. EXECUTIVE SUMMARY

| Prefab | Root Components | Target | Delta | Verdict |
|--------|-----------------|--------|-------|---------|
| **Player.prefab** | **28** | â‰¤25 | **+3** | ðŸ”´ GOD OBJECT â€” exceeds limit |
| **Submarine.prefab** | N/A | â‰¤25 | â€” | ðŸŸ¡ No unified prefab found; systems are modular |

**Player** is a confirmed God Object. Root carries gameplay, physics, UI, audio, dev/smoke-test, and presentation logic simultaneously. Decomposition is possible and mandated.

**Submarine** does not exist as a single prefab. Base/Habitat systems are distributed across `PFB_Module_*.prefab` construction modules and scene-placed `HabitatIntegrityManager`. This is actually the *correct* architectural pattern for large interactable systems.

---

## 2. PLAYER.PREFAB â€” ROOT COMPONENT BREAKDOWN

**File:** `Assets/_Project/Prefabs/Player.prefab`
**Root GameObject:** `Player` (fileID: 5334833049046775397)
**Total GameObjects in prefab:** 45 (including children)
**Components on root:** 28

### Component List

| # | Type | Class / Purpose | Category | Movable? |
|---|------|-----------------|----------|----------|
| 1 | Transform | â€” | Core | No |
| 2 | MonoBehaviour | `HectonSurvivalSystem` | Gameplay / Survival | **Yes** â†’ Data-only SO wrapper, could live on child `Systems` object |
| 3 | MonoBehaviour | `PlayerInteraction` | Interaction | **Yes** â†’ Raycast logic; could merge with `PlayerToolManager` |
| 4 | Rigidbody | â€” | Physics | No (must stay on root for KCC) |
| 5 | MonoBehaviour | `HectonPlayerMovement` | Gameplay / Locomotion | No (drives Rigidbody) |
| 6 | MonoBehaviour | `BuoyancyObject` | Physics | **Yes** â†’ Could be child `Physics` object |
| 7 | MonoBehaviour | `PlayerToolManager` | Gameplay / Tools | **Yes** â†’ Child `Tools` object |
| 8 | MonoBehaviour | `PlayerInventory` | Inventory | **Yes** â†’ Child `Inventory` object |
| 9 | CapsuleCollider | â€” | Physics | No (must align with movement) |
| 10 | MonoBehaviour | `PlayerPDA` | UI | **Yes** â†’ Child `UI/PDA` object |
| 11 | MonoBehaviour | `PlayerFlashlight` | Gameplay / Tools | **Yes** â†’ Merge into `PlayerToolManager` or child `Tools` |
| 12 | MonoBehaviour | `ToolLoadoutProvisioner` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 13 | MonoBehaviour | `ToolRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 14 | MonoBehaviour | `SuitAdvisoryController` | UI / Feedback | **Yes** â†’ Child `UI/Suit` object |
| 15 | MonoBehaviour | `PlayerBuilder` | Building | **Yes** â†’ Child `Building` object |
| 16 | MonoBehaviour | `BuilderRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 17 | MonoBehaviour | `UIRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 18 | MonoBehaviour | `ScanLogSystem` | Gameplay / Scan | **Yes** â†’ Child `Systems/Scan` object |
| 19 | MonoBehaviour | `ScanRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 20 | MonoBehaviour | `FieldToolRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 21 | MonoBehaviour | `PDAExchangeSystem` | Economy / Barter | **Yes** â†’ Child `Systems/Economy` object |
| 22 | MonoBehaviour | `BarterRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 23 | MonoBehaviour | `FieldOperationLogSystem` | Telemetry | **Yes** â†’ Child `Systems/Telemetry` object |
| 24 | MonoBehaviour | `BeaconNetworkSystem` | Gameplay / World | **Yes** â†’ Child `Systems/World` object |
| 25 | MonoBehaviour | `ToolTrialRangeRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 26 | MonoBehaviour | `FabricationRuntimeSmokeTester` | **Dev-only** | **Yes** â†’ Move to `__DEV` child or strip from release |
| 27 | MonoBehaviour | `PlayerSwimPresentationController` | Animation / Presentation | **Yes** â†’ Child `Presentation/Swim` object |
| 28 | MonoBehaviour | `PlayerSwimBlockoutRig` | Animation / Rig | **Yes** â†’ Child `Presentation/Swim` object |

### Critical Observations

- **8 of 28 components are Dev/SmokeTest utilities** (indices 12, 13, 16, 17, 19, 20, 22, 25, 26). That is **9 components** that should **never** ship on the root. Removing them immediately drops the count to **19**, well under the limit.
- **Rigidbody + CapsuleCollider + HectonPlayerMovement** must remain on root; they form the kinematic character controller (KCC) core.
- `BuoyancyObject` could be moved to a child `PhysicsProxy` object if it only reads/transforms forces, but verify no direct `transform.position` writes from it.
- `PlayerInteraction` and `PlayerToolManager` overlap in raycast/tool logic. Evaluate merging into a single `PlayerHandsSystem`.

---

## 3. SUBMARINE / HABITAT AUDIT

**Finding:** No `Submarine.prefab` or `Habitat.prefab` exists in first-party assets.

**Where the systems live:**
- `HabitatIntegrityManager` â€” found as scene object or on `PFB_Module_*.prefab` construction modules.
- `SubmarineStructuralGrid` â€” implements `IDamageReceiver`, likely attached to individual base module prefabs.
- Construction modules: `PFB_Module_Foundation`, `PFB_Module_Corridor`, `PFB_Module_ServicePump`, etc. Each is a separate prefab with â‰¤10 components.

**Verdict:** Prefab YAML scan did not find a monolithic Submarine root in that pass. Runtime architecture proof is absent.

---

## 4. RECOMMENDED REMEDIATION (Player)

| Priority | Action | Resulting Root Component Count |
|----------|--------|-------------------------------|
| P0 | **Remove all 9 Dev/SmokeTest MonoBehaviours** from root. Move to a `__DEV_SmokeTest` child object that is stripped by build script, or delete them from prefab entirely. | 19 |
| P1 | Move `PlayerInventory` to child `Data/Inventory` object. Cache reference in `PlayerToolManager` via serialized field. | 18 |
| P1 | Move `PlayerPDA`, `SuitAdvisoryController` to child `UI` object. | 16 |
| P2 | Move `ScanLogSystem`, `BeaconNetworkSystem`, `FieldOperationLogSystem`, `PDAExchangeSystem` to child `Systems` object. | 12 |
| P2 | Move `PlayerSwimPresentationController` + `PlayerSwimBlockoutRig` to child `Presentation` object. | 10 |
| P3 | Merge `PlayerInteraction` raycast logic into `PlayerToolManager` (single hands system). | 9 |

**After P0 alone:** root count = 19 (compliant).
**After full remediation:** root count â‰ˆ 9 (optimal).

---

*Report generated by ARCHIVARIUS. Component count was parsed from prefab YAML in that pass; this is not current runtime proof.*
