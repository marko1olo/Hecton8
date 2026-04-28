# SCENE OBJECT HYGIENE — HECTON-8 Static Audit

**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**DOD Principle:** Data-Oriented Design — high component density = architectural smell

---

## I. Component Density Audit (Prefab YAML Scan)

### Player.prefab

| GameObject | Component Count | Severity | Notes |
|---|---|---|---|
| **Player** (root) | **28** | 🔴 **CRITICAL** | Far exceeds 10-component DOD threshold. Monolithic root object. |
| Main Camera | 8 | 🟡 **HIGH** | Camera + post-FX + audio listeners |
| Suit_Visor | 5 | ✅ OK | — |
| DiveLamp_Light | 5 | ✅ OK | — |
| HUD_Render_Camera | 5 | ✅ OK | — |
| Underwater_ShallowSunBeam | 5 | ✅ OK | — |
| All other GameObjects | 1–3 | ✅ OK | Transform + optional components |

**Player root object (28 components) is the primary DOD violation.** This monolithic design concentrates gameplay, survival, inventory, tools, movement, interaction, and more on a single GameObject.

### Ocean_Crest.prefab

| GameObject | Component Count | Severity | Notes |
|---|---|---|---|
| Ocean_Crest (root) | 9 | ✅ OK | Under 10 threshold |
| SargassumOilFilmInput | 4 | ✅ OK | — |
| SargassumWaveDampingInput | 4 | ✅ OK | — |
| SargassumFoamDampingInput | 4 | ✅ OK | — |
| OceanDepthCache | 2 | ✅ OK | — |

**Ocean_Crest is clean.** No DOD violations.

### Submarine.prefab

**File not found.** No prefab named `Submarine*.prefab` exists in `Assets/_Project/Prefabs/`. The submarine is likely assembled at runtime from `BaseModule` prefabs or spawned procedurally.

---

## II. Player.prefab — Component Breakdown (Root Object)

The 28 components on the Player root represent a **God Object** anti-pattern:

**Expected component categories on Player root:**
- Rigidbody (physics)
- HectonPlayerMovement (movement controller)
- HectonSurvivalSystem (health/O2/pressure)
- PlayerInventory (inventory management)
- PlayerToolManager (tool switching)
- PlayerInteraction (raycast interaction)
- PlayerFlashlight (flashlight control)
- PlayerPDA (PDA management)
- PlayerBuilder (construction)
- BiomeSamplerCache (biome sampling)
- Multiple audio sources
- Multiple colliders
- Additional subsystems (expression, transport, etc.)

### DOD Decomposition Plan

**Phase 1: Logical Grouping (Child GameObjects)**

| Child GameObject | Components | Owner Class |
|---|---|---|
| `Player/Movement` | Rigidbody, CharacterController, HectonPlayerMovement, PlayerSwimBlockoutRig | `MovementSystem` |
| `Player/Survival` | HectonSurvivalSystem, PlayerHealth, OxygenSystem, PressureSystem | `SurvivalSystem` |
| `Player/Inventory` | PlayerInventory, PlayerToolManager, PlayerFlashlight | `InventorySystem` |
| `Player/Interaction` | PlayerInteraction, PlayerBuilder, ClimbableLadder (ref) | `InteractionSystem` |
| `Player/UI` | PlayerPDA, PDAShellChrome, VisorUIController | `UISystem` |
| `Player/Audio` | AudioListener, PlayerCriticalProceduralAudioRenderer, Ambient sources | `AudioSystem` |
| `Player/Collision` | CapsuleCollider, SphereCollider(s), ContactFilter | `PhysicsSystem` |

**Phase 2: Data-Oriented Refactor (NativeArray + Jobs)**

| System | Data Structure | Storage |
|---|---|---|
| Movement | `NativeArray<PlayerMovementState>` | SoA (position/velocity/input) |
| Survival | `NativeArray<PlayerSurvivalState>` | SoA (health/O2/pressure/temperature) |
| Inventory | `NativeList<InventoryItem>` | Managed (low-frequency) |
| Interaction | `NativeQueue<InteractionPacket>` | Event-based |

**Phase 3: GlobalRegistry Access**

Replace `GetComponent<T>()` calls with `GlobalRegistry.PlayerMovement`, `GlobalRegistry.PlayerSurvival`, etc.

---

**Recommendation:** Decompose Player root into subsystems using child GameObjects or separate manager classes accessed via `GlobalRegistry`. The current 28-component density makes the Player prefab fragile, hard to test, and violates single-responsibility.

---

## III. Summary

| Prefab | Max Components (single GO) | DOD Violation? | Action Required |
|---|---|---|---|
| `Player.prefab` | **28** (root) | 🔴 **CRITICAL** | Decompose into 6 subsystem child GameObjects |
| `Ocean_Crest.prefab` | 9 | ✅ NO | None |
| `Submarine.prefab` | N/A (not found) | — | Audit BaseModule prefabs instead |

---

## IV. Action Plan

| Priority | Task | Owner | Sprint |
|---|---|---|---|
| 🔴 P0 | Create child GameObjects under Player root (Movement/Survival/Inventory/Interaction/UI/Audio) | Senior Dev | Next |
| 🟠 P1 | Migrate components to child GameObjects | Senior Dev | Next+1 |
| 🟡 P2 | Implement `GlobalRegistry.Player*` accessors | Architect | Next+1 |
| 🟢 P3 | SoA refactoring (NativeArray for movement/survival state) | Senior Dev | Future |

---

**STATUS:** 🔴 **CRITICAL** — Player.prefab DOD decomposition required before next milestone.
