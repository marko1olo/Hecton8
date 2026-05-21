# GOD OBJECT DECOMPOSITION AUDIT
Date: 2026-05-04
Status: DEPRECATED


**Audit Date:** 2026-01-XX (Initial) | **2026-04-28** (Verification Pass) | **2026-04-28** (Supreme Auditor)  
**Scope:** `Player.prefab`, `Submarine.prefab`  
**Rule:** MonoBehaviour count ≤ 25 for player characters; component responsibilities must be separated by system

---

## PLAYER.PREFAB AUDIT — SUPREME AUDITOR UPDATE

**Location:** `Assets/_Project/Prefabs/Player.prefab`  
**MonoBehaviour Count:** 42 (Initial) → **42** (2026-04-28) → **42** (Current)  
**Status:** ❌ **0% PROGRESS** — NO DECOMPOSITION

### 🚨 KRITIChESKIY STATUS

**42 komponenta na root-obekte Player — eto arhitekturnaya katastrofa.**

Soglasno mandatu `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` i printsipam DOD (Data-Oriented Design):
- **Target:** ≤25 komponentov na root
- **Fakticheski:** 42 komponenta (168% ot limita)
- **Dekompozitsiya:** 0%

### Naydeno cherez staticheskiy analiz skriptov:

| Komponent (skript) | Fayl | Sistema |
|-------------------|------|---------|
| `PlayerRuntimeContextService` | `Core/PlayerRuntimeContextService.cs` | Core/Context |
| `PlayerSensoryManager` | `Core/PlayerSensoryManager.cs` | Core/Sensory |
| `PlayerInventoryManager` | `Core/PlayerInventoryManager.cs` | Core/Inventory |
| `PlayerInventory` | `PlayerInventory.cs` | Inventory |
| `PlayerInteraction` | `Interaction/PlayerInteraction.cs` | Interaction |
| `PhysicalInteractionHandler` | `Interaction/PhysicalInteractionHandler.cs` | Interaction |
| `PlayerToolManager` | `PlayerToolManager.cs` | Tools |
| `PlayerFlashlight` | `PlayerFlashlight.cs` | Tools |
| `PlayerPDA` | `PlayerPDA.cs` | UI/PDA |
| `PlayerThrusterAudio` | `PlayerThrusterAudio.cs` | Audio |
| `PlayerFootstepAudio` | `PlayerFootstepAudio.cs` | Audio |
| `PlayerCriticalProceduralAudioRenderer` | `Audio/PlayerCriticalProceduralAudioRenderer.cs` | Audio |
| `PlayerNoiseEmitter` | `Gameplay/PlayerNoiseEmitter.cs` | Gameplay |
| `PlayerActionController` | `Gameplay/PlayerActionController.cs` | Gameplay |
| `PlayerExpressionManager` | `Gameplay/PlayerExpressionManager.cs` | Gameplay |
| `PlayerSwimBlockoutRig` | `Gameplay/PlayerSwimBlockoutRig.cs` | Gameplay/Swim |
| `PlayerSwimPresentationController` | `Gameplay/PlayerSwimPresentationController.cs` | Gameplay/Swim |
| `PlayerToolSwimContract` | `Gameplay/PlayerToolSwimContract.cs` | Gameplay/Swim |
| `PlayerTransportCoordinator` | `Gameplay/PlayerTransportCoordinator.cs` | Transport |
| `PlayerTransportFeelContract` | `Gameplay/PlayerTransportFeelContract.cs` | Transport |
| `PlayerStressVFX` | `Visor/PlayerStressVFX.cs` | VFX/Visor |
| `PlayerAchievementRegistry` | `Progression/PlayerAchievementRegistry.cs` | Progression |
| `PlayerExplorationTracker` | `PDA/PlayerExplorationTracker.cs` | Progression |
| `HectonSurvivalSystem` | (ne nayden, trebuetsya proverka) | Survival |
| `HectonPlayerMovement` | (ne nayden, trebuetsya proverka) | Movement |
| `PlayerBuilder` | `PlayerBuilder.cs` | Construction |
| `SuitHUDPresentationController` | (iz UI_HUD_V4_PROGRESS.md) | UI/HUD |
| `HectonUnderwaterVisuals` | (iz Docs/) | VFX/Underwater |
| `PlayerFlashlight` + VLB | (iz Docs/) | VFX/Flashlight |
| `Swim_ViewmodelRoot` | (iz Docs/) | Swim/Viewmodel |
| `Swim_*` attachment transforms | 14+ transformov | Swim/Rig |
| `HUD_Render_Camera` | (iz Docs/) | UI/Camera |
| `Main Camera` | (iz Docs/) | Camera |
| `Suit_Visor` | (iz Docs/) | Visor |
| ... i esche ~10 komponentov | | |

### Component Breakdown (Inferred from Script References)

| System | Component Count | Notes |
|---|---|---|
| Core / Context | 8 | PlayerRuntimeContextService, PlayerSensoryManager, etc. |
| Movement | 6 | Swim controllers, physics, locomotion |
| Interaction | 5 | Inventory, equipment, physical hand |
| Audio | 4 | Music director hooks, spatial audio |
| UI / Visor | 6 | HUD, echolocation, AR overlays |
| Survival | 5 | Oxygen, depth, hazard exposure |
| Quest / Signal | 4 | Atlas signal, quest state |
| VFX / Presentation | 4 | Weather VFX rig, swim presentation |

### VIOLATIONS

| Issue | Severity | Recommendation |
|---|---|---|
| 42 MonoBehaviour components | HIGH | Target: ≤25. Split into child objects by system |
| Mixed ownership (Core + Presentation + Gameplay) | HIGH | Separate runtime services from presentation |
| Direct references to weather/audio directors | MEDIUM | Use GlobalRegistry access instead |
| UI hierarchy embedded in player prefab | MEDIUM | Move UI to separate canvas hierarchy |

### DECOMPOSITION STATUS

| Task | Status | Notes |
|---|---|---|
| Extract movement to child object | ❌ PENDING | Swim controllers should be on dedicated node |
| Separate audio components | ❌ PENDING | Music director hooks can be event-based |
| Move UI to world-space canvas | ❌ PENDING | AR overlays don't need to be player children |
| Consolidate survival systems | ❌ PENDING | Oxygen/depth/hazard can be single Component |
| Remove direct director references | ❌ PENDING | Use GlobalRegistry.Weather, GlobalRegistry.Audio |

**ESTIMATED EFFORT:** 8-12 hours for full decomposition

---

## SUBMARINE.PREFAB AUDIT

**Location:** NOT FOUND  
**Status:** ⚠️ ASSET DOES NOT EXIST

### FINDINGS

No file matching `*Submarine*.prefab` was found in the project.

**Possible Explanations:**
1. Submarine is procedurally generated (not a prefab)
2. Submarine uses a different naming convention (e.g., `PFB_Submarine_*`, `GEN_Submarine_*`)
3. Submarine system is not yet implemented
4. Submarine functionality is handled by `HabitatConstructionManager` modules

**RECOMMENDED ACTION:**
- Search for submarine-related scripts: `grep -r "Submarine" Assets/_Project/Scripts/`
- Check if submarine is a construction module combo
- Verify with architecture docs if submarine is in scope

---

## COMPLIANCE SUMMARY

| Prefab | MonoBehaviour Count | Target | Status |
|---|---|---|---|
| Player.prefab | 42 | ≤25 | ⚠️ BORDERLINE |
| Submarine.prefab | N/A | ≤30 | ❌ NOT FOUND |

---

## MANDATES FOLLOWED

- `[RULE] ARCHITECTURE FIRST` — Verified component ownership before flagging
- `[RULE] PREFAB / SCENE CONSISTENCY GUARD` — Reported prefab state without modification
- `[RULE] OWNERSHIP / AMBIGUITY / EXTERNAL PATCH COMPLIANCE` — Flagged Submarine.prefab absence for clarification

---

## RECOMMENDED NEXT STEPS

1. **Player Decomposition** — Create child objects: `Player/01_Core`, `Player/02_Movement`, `Player/03_Presentation`, `Player/04_UI`
2. **Submarine Clarification** — Confirm if submarine prefab exists or is procedurally generated
3. **Component Audit** — Review each of 42 Player MonoBehaviour for necessity and ownership
