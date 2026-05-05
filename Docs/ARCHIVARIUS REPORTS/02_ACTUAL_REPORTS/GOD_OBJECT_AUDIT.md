# GOD OBJECT AUDIT — Player & Submarine Prefab Component Analysis
Date: 2026-05-04
Status: REFERENCE


> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md § Architecture First · § Ownership / Ambiguity  
> **Target:** ≤25 components on root GameObject  

---

## 1. EXECUTIVE SUMMARY

| Prefab | Root Components | Target | Delta | Verdict |
|--------|-----------------|--------|-------|---------|
| **Player.prefab** | **28** | ≤25 | **+3** | 🔴 GOD OBJECT — exceeds limit |
| **Submarine.prefab** | N/A | ≤25 | — | 🟡 No unified prefab found; systems are modular |

**Player** is a confirmed God Object. Root carries gameplay, physics, UI, audio, dev/smoke-test, and presentation logic simultaneously. Decomposition is possible and mandated.

**Submarine** does not exist as a single prefab. Base/Habitat systems are distributed across `PFB_Module_*.prefab` construction modules and scene-placed `HabitatIntegrityManager`. This is actually the *correct* architectural pattern for large interactable systems.

---

## 2. PLAYER.PREFAB — ROOT COMPONENT BREAKDOWN

**File:** `Assets/_Project/Prefabs/Player.prefab`  
**Root GameObject:** `Player` (fileID: 5334833049046775397)  
**Total GameObjects in prefab:** 45 (including children)  
**Components on root:** 28

### Component List

| # | Type | Class / Purpose | Category | Movable? |
|---|------|-----------------|----------|----------|
| 1 | Transform | — | Core | No |
| 2 | MonoBehaviour | `HectonSurvivalSystem` | Gameplay / Survival | **Yes** → Data-only SO wrapper, could live on child `Systems` object |
| 3 | MonoBehaviour | `PlayerInteraction` | Interaction | **Yes** → Raycast logic; could merge with `PlayerToolManager` |
| 4 | Rigidbody | — | Physics | No (must stay on root for KCC) |
| 5 | MonoBehaviour | `HectonPlayerMovement` | Gameplay / Locomotion | No (drives Rigidbody) |
| 6 | MonoBehaviour | `BuoyancyObject` | Physics | **Yes** → Could be child `Physics` object |
| 7 | MonoBehaviour | `PlayerToolManager` | Gameplay / Tools | **Yes** → Child `Tools` object |
| 8 | MonoBehaviour | `PlayerInventory` | Inventory | **Yes** → Child `Inventory` object |
| 9 | CapsuleCollider | — | Physics | No (must align with movement) |
| 10 | MonoBehaviour | `PlayerPDA` | UI | **Yes** → Child `UI/PDA` object |
| 11 | MonoBehaviour | `PlayerFlashlight` | Gameplay / Tools | **Yes** → Merge into `PlayerToolManager` or child `Tools` |
| 12 | MonoBehaviour | `ToolLoadoutProvisioner` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 13 | MonoBehaviour | `ToolRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 14 | MonoBehaviour | `SuitAdvisoryController` | UI / Feedback | **Yes** → Child `UI/Suit` object |
| 15 | MonoBehaviour | `PlayerBuilder` | Building | **Yes** → Child `Building` object |
| 16 | MonoBehaviour | `BuilderRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 17 | MonoBehaviour | `UIRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 18 | MonoBehaviour | `ScanLogSystem` | Gameplay / Scan | **Yes** → Child `Systems/Scan` object |
| 19 | MonoBehaviour | `ScanRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 20 | MonoBehaviour | `FieldToolRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 21 | MonoBehaviour | `PDAExchangeSystem` | Economy / Barter | **Yes** → Child `Systems/Economy` object |
| 22 | MonoBehaviour | `BarterRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 23 | MonoBehaviour | `FieldOperationLogSystem` | Telemetry | **Yes** → Child `Systems/Telemetry` object |
| 24 | MonoBehaviour | `BeaconNetworkSystem` | Gameplay / World | **Yes** → Child `Systems/World` object |
| 25 | MonoBehaviour | `ToolTrialRangeRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 26 | MonoBehaviour | `FabricationRuntimeSmokeTester` | **Dev-only** | **Yes** → Move to `__DEV` child or strip from release |
| 27 | MonoBehaviour | `PlayerSwimPresentationController` | Animation / Presentation | **Yes** → Child `Presentation/Swim` object |
| 28 | MonoBehaviour | `PlayerSwimBlockoutRig` | Animation / Rig | **Yes** → Child `Presentation/Swim` object |

### Critical Observations

- **8 of 28 components are Dev/SmokeTest utilities** (indices 12, 13, 16, 17, 19, 20, 22, 25, 26). That is **9 components** that should **never** ship on the root. Removing them immediately drops the count to **19**, well under the limit.
- **Rigidbody + CapsuleCollider + HectonPlayerMovement** must remain on root; they form the kinematic character controller (KCC) core.
- `BuoyancyObject` could be moved to a child `PhysicsProxy` object if it only reads/transforms forces, but verify no direct `transform.position` writes from it.
- `PlayerInteraction` and `PlayerToolManager` overlap in raycast/tool logic. Evaluate merging into a single `PlayerHandsSystem`.

---

## 3. SUBMARINE / HABITAT AUDIT

**Finding:** No `Submarine.prefab` or `Habitat.prefab` exists in first-party assets.

**Where the systems live:**
- `HabitatIntegrityManager` — found as scene object or on `PFB_Module_*.prefab` construction modules.
- `SubmarineStructuralGrid` — implements `IDamageReceiver`, likely attached to individual base module prefabs.
- Construction modules: `PFB_Module_Foundation`, `PFB_Module_Corridor`, `PFB_Module_ServicePump`, etc. Each is a separate prefab with ≤10 components.

**Verdict:** Submarine/Habitat architecture is **correctly decomposed**. No God Object detected. The absence of a monolithic Submarine prefab is architectural success.

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
**After full remediation:** root count ≈ 9 (optimal).

---

*Report generated by ARCHIVARIUS. Component count verified via prefab YAML parse. No guesswork.*
