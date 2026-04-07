# 01_MAIN_MENU — Production Shell Finalization Checklist

**Target**: 01_MAIN_MENU is production-ready, all UI works, no broken references, proper architecture.

**Last Updated**: 2026-04-07

---

## Code Status

- ✅ **MainMenuController.cs** — Complete
  - Panel transitions (fade in/out with CanvasGroup)
  - New Game flow
  - Load Game flow with slot generation
  - Settings panel (stub for future extension)
  - Async scene loading with progress bar
  - Save slot click handling
  - GameStartContext integration (replaces legacy TargetSaveSlot)

- ✅ **MainMenuValidator.cs** (Editor tool)
  - Verify all required UI elements are assigned
  - Check for broken references
  - Usage: `Window > HECTON-8 > Validate Main Menu`

- ✅ **SceneGuard.cs** (Scene protection)
  - Detects if scene loaded without 00_BOOTSTRAP
  - Auto-reloads 00_BOOTSTRAP if violated
  - File: `Assets/_Project/Scripts/Bootstrap/SceneGuard.cs`

---

## Scene Configuration (Manual in Unity)

### 1. Canvas & UI Root
- [ ] Main Canvas exists (RenderMode: Screen Space - Overlay)
- [ ] Canvas Scale: UI Scale Mode = "Scale with Screen Size"
- [ ] Reference Resolution: 1920x1080 (or your target)

### 2. Panels (CanvasGroups)
These should be children of Canvas. Each is a panel that gets shown/hidden via alpha fade.

- [ ] **mainMenuGroup** (CanvasGroup)
  - Contains: New Game, Load Game, Settings, Quit buttons
  - Initial alpha: 1 (visible)
  
- [ ] **saveLoadGroup** (CanvasGroup)
  - Contains: Save slot buttons, Back button, slot descriptions
  - Initial alpha: 0 (hidden)
  
- [ ] **settingsGroup** (CanvasGroup)
  - Contains: Settings controls, Back button (stub for now)
  - Initial alpha: 0 (hidden)
  
- [ ] **loadingGroup** (CanvasGroup)
  - Contains: Progress bar, percent text, "Loading..." label
  - Initial alpha: 0 (hidden)

### 3. Main Menu Buttons
All need Button component + Image component.

- [ ] **btnNewGame** — Button "New Game"
  - Label: labelNewGame (TMP_Text child)
  - Position: Top-left or center
  
- [ ] **btnLoadGame** — Button "Load Game"
  - Label: labelLoadGame (TMP_Text child)
  
- [ ] **btnSettings** — Button "Settings"
  - Label: labelSettings (TMP_Text child)
  
- [ ] **btnQuit** — Button "Quit"
  - Label: labelQuit (TMP_Text child)

### 4. Sub-Menu Buttons
- [ ] **btnBackFromSaveLoad** — Button "Back" in saveLoadGroup
- [ ] **btnBackFromSettings** — Button "Back" in settingsGroup

### 5. Save Slots UI
- [ ] **slotsContainer** (Transform / GridLayoutGroup)
  - Parent for dynamically instantiated save slot buttons
  - Grid layout or vertical layout
  
- [ ] **slotPrefab** (GameObject)
  - Prefab for individual save slot item
  - Should have: Button, Text (slot name), Text (playtime), Text (preview)
  - File: suggest `Assets/_Project/Prefabs/UI/SaveSlotItem.prefab`

### 6. Loading Screen UI
- [ ] **loadingProgressBar** (Slider)
  - Range: 0-1
  - Handle: filled image (green or theme color)
  
- [ ] **loadingPercentText** (TMP_Text)
  - Shows: "0%", "50%", "100%"
  - Updated every frame during load

### 7. Camera
- [ ] **Main Camera** in scene
  - Tag: "MainCamera"
  - Position: (0, 0, -10) or adjust for menu composition
  - Clear Flags: Solid Color (dark theme)
  - Background: Dark color (e.g., #0B0E11)

### 8. EventSystem
- [ ] **EventSystem** exists in scene
  - Auto-created by Unity when first UI button added
  - Verify: Scene > GameObject > UI > Event System (if missing)
  
- [ ] **GraphicRaycaster** on Canvas
  - Auto-added to Canvas when first UI element added
  - Enables mouse/touch input on buttons

### 9. Scene Protection
- [ ] **SceneGuard** script attached to root GameObject (or separate root)
  - Enabled: true
  - _enforceBootstrap: true
  - Prevents scene from loading without 00_BOOTSTRAP

---

## Inspector Wiring Checklist

Use **MainMenuValidator** to auto-check. Steps:

1. Load scene: `Assets/_Project/Scenes/01_MAIN_MENU.unity`
2. Open menu: `Window > HECTON-8 > Validate Main Menu`
3. For each ✗ (missing), manually assign:
   - Select MainMenuController in Inspector
   - Drag the missing UI element into the corresponding field

Example:
- Field: `mainMenuGroup` → Click field → Drag Canvas/MainMenuGroup panel into it
- Field: `btnNewGame` → Click field → Drag Button_NewGame into it

---

## Functional Tests (In-Editor Play Mode)

1. **New Game Flow**
   - [ ] Click "New Game" button
   - [ ] See confirmation dialog (via ModalWindow)
   - [ ] Confirm → Game starts loading (loading screen shows)
   - [ ] Reach 02_HECTON_WORLD with new game state

2. **Load Game Flow**
   - [ ] Click "Load Game" button
   - [ ] See saveLoadGroup panel fade in
   - [ ] See 3 save slot buttons (slot_1, slot_2, slot_3)
   - [ ] Click one → confirmation dialog
   - [ ] Confirm → Game loads that save file
   - [ ] Check that player position/inventory matches save

3. **Settings Panel**
   - [ ] Click "Settings" button
   - [ ] See settingsGroup fade in
   - [ ] Click "Back" → fade back to main menu
   - [ ] (Settings content is stub for now, just navigation)

4. **Quit**
   - [ ] Click "Quit" button
   - [ ] See confirmation dialog
   - [ ] Confirm → Application.Quit() or Editor play mode stops

5. **Direct Load Protection**
   - [ ] In Build Settings, temporarily move 01_MAIN_MENU to index 0
   - [ ] Run Play mode
   - [ ] Scene should immediately reload 00_BOOTSTRAP (via SceneGuard)
   - [ ] Put Build Settings back: 00_BOOTSTRAP index 0, 01_MAIN_MENU index 1

---

## Build Settings Order

Verify in Edit > Project Settings > Scenes In Build:

- **0**: Assets/_Project/Scenes/00_BOOTSTRAP.unity
- **1**: Assets/_Project/Scenes/01_MAIN_MENU.unity
- **2**: Assets/_Project/Scenes/02_HECTON_WORLD.unity
- Others (XX_SANDBOX, etc.) can follow

---

## Known Limitations (Not Blocking)

- [ ] Settings panel is currently a stub (no actual settings implemented)
- [ ] Audio/music not wired to menu (placeholder silence is OK)
- [ ] Localization strings may need final review
- [ ] Button colors/styling can be tuned later (art pass)

---

## Status Marker

Mark this checklist as **COMPLETE** when:
- [ ] All code compiles without errors
- [ ] MainMenuValidator shows all ✓ checks
- [ ] All functional tests pass
- [ ] SceneGuard protection works (tried direct load, it redirected)
- [ ] Scene saved and committed

---

## Final Sign-Off

**Completion Date**: _____________  
**Verified By**: _____________  
**Notes**: _____________
