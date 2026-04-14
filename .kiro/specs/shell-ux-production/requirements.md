# Requirements Document — Shell/UX Production System

## Introduction

HECTON-8 shell/UX system provides the player-facing interface layer for menu navigation, pause functionality, settings management, input rebinding, and save/load operations. The current implementation (~30-40% production-ready per audit) requires completion across five fronts: main menu flow, pause shell, rebinding UX, options persistence, and save/load user trust.

This system bridges bootstrap → main menu → world scene flow and provides in-game pause/settings access. It must handle zero-GC hot paths (CanvasGroup alpha transitions), unified settings ownership, graceful error states, and complete back/cancel navigation paths.

## Glossary

- **Shell_System**: The complete UI layer encompassing main menu, pause menu, settings panels, and modal dialogs
- **Main_Menu_Controller**: Scene controller for 01_MAIN_MENU managing panel transitions and scene loading
- **Pause_Menu_Controller**: In-game pause overlay with sections for saves, help, settings, and main menu exit
- **Settings_Owner**: Unified persistence manager for user options (graphics, audio, input overrides, language)
- **Rebinding_Manager**: Singleton service for runtime input action rebinding with PlayerPrefs persistence
- **Save_Manager**: Singleton service for slot-based save/load with CRC32 checksums and backup rotation
- **Panel_Transition**: CanvasGroup alpha fade between UI panels (zero-GC, no SetActive)
- **Default_Selection**: EventSystem-driven button focus for gamepad/keyboard navigation
- **Modal_Window**: Confirmation dialog for destructive actions (new game, quit, load, exit to menu)
- **Save_Slot**: One of three manual save locations (slot_1, slot_2, slot_3) with metadata and thumbnails
- **Input_Override**: User-customized key/button binding stored via RebindingManager
- **User_Options**: Persistent settings (volume, quality, language, input overrides) owned by Settings_Owner
- **Localization_Manager**: Singleton for multi-language text via LocalizationKeys
- **Game_Start_Context**: Inter-scene data holder for new game vs load game handoff
- **Bootstrap_Route**: Required scene flow (00_BOOTSTRAP → 01_MAIN_MENU → 02_HECTON_WORLD)

## Requirements

### Requirement 1: Main Menu Flow Completion

**User Story:** As a player, I want a complete main menu with working new game, load game, settings, and quit flows, so that I can start or resume my game without encountering dead-end states or broken navigation.

#### Acceptance Criteria

1. WHEN the player clicks "New Game", THE Main_Menu_Controller SHALL display a Modal_Window confirmation dialog
2. WHEN the player confirms new game, THE Main_Menu_Controller SHALL write GameStartContext.CreateNewGame() and load 02_HECTON_WORLD asynchronously
3. WHEN the player clicks "Load Game", THE Main_Menu_Controller SHALL fade to saveLoadGroup panel and display three Save_Slot buttons with metadata
4. WHEN a Save_Slot is empty, THE Main_Menu_Controller SHALL display "Empty Slot" text and disable the button
5. WHEN a Save_Slot contains data, THE Main_Menu_Controller SHALL display slot name, playtime, and last save timestamp
6. WHEN the player clicks a populated Save_Slot, THE Main_Menu_Controller SHALL display a Modal_Window confirmation dialog
7. WHEN the player confirms load, THE Main_Menu_Controller SHALL write GameStartContext.CreateLoadGame(slotName) and load 02_HECTON_WORLD asynchronously
8. WHEN the player clicks "Settings", THE Main_Menu_Controller SHALL fade to settingsGroup panel IF settings are available
9. WHEN settings are unavailable (stub state), THE Main_Menu_Controller SHALL disable the Settings button
10. WHEN the player clicks "Quit", THE Main_Menu_Controller SHALL display a Modal_Window confirmation dialog
11. WHEN the player confirms quit, THE Main_Menu_Controller SHALL call Application.Quit() (or EditorApplication.isPlaying = false in editor)
12. WHEN the player clicks "Back" in any sub-panel, THE Main_Menu_Controller SHALL fade back to mainMenuGroup panel
13. WHEN the player presses Escape in saveLoadGroup or settingsGroup, THE Main_Menu_Controller SHALL fade back to mainMenuGroup panel
14. WHEN the player presses Escape in mainMenuGroup, THE Main_Menu_Controller SHALL display quit confirmation dialog
15. WHEN scene loading begins, THE Main_Menu_Controller SHALL display loadingGroup panel with progress bar and percent text
16. WHEN scene loading reaches 90%, THE Main_Menu_Controller SHALL update percent text to 100% and activate the scene
17. WHEN any panel transition occurs, THE Main_Menu_Controller SHALL set interactable=false and blocksRaycasts=false on both panels during fade
18. WHEN a panel transition completes, THE Main_Menu_Controller SHALL set Default_Selection to the first interactable button in the new panel
19. WHEN the player uses gamepad or keyboard, THE Main_Menu_Controller SHALL maintain visible button focus via EventSystem
20. FOR ALL panel transitions, THE Main_Menu_Controller SHALL use CanvasGroup alpha fades (zero-GC, no SetActive calls)

### Requirement 2: Pause Shell Completion

**User Story:** As a player, I want a complete pause menu with working resume, save, help, settings, and exit flows, so that I can manage my game state and settings without encountering incomplete sections or broken navigation.

#### Acceptance Criteria

1. WHEN the player presses Escape or Start button in-game, THE Pause_Menu_Controller SHALL open the pause overlay and set Time.timeScale to 0
2. WHEN the pause menu opens, THE Pause_Menu_Controller SHALL switch InputManager to UI input mode and show cursor
3. WHEN the pause menu opens, THE Pause_Menu_Controller SHALL display the Main section with six buttons (Resume, Save Station, Field Guide, Settings, Exit to Main Menu, Quit)
4. WHEN the player clicks "Resume" or presses Escape in Main section, THE Pause_Menu_Controller SHALL close the pause menu, restore Time.timeScale, switch to Player input, and hide cursor
5. WHEN the player clicks "Save Station", THE Pause_Menu_Controller SHALL show the Saves section with three Save_Slot buttons
6. WHEN the player clicks a Save_Slot in pause menu, THE Pause_Menu_Controller SHALL call SaveManager.SaveGameAsync(slotName) and display status text
7. WHEN a save operation succeeds, THE Pause_Menu_Controller SHALL display "{SLOT_NAME} WRITTEN." status text
8. WHEN a save operation fails, THE Pause_Menu_Controller SHALL display "{SLOT_NAME} FAILED. {ERROR}" status text
9. WHEN the player clicks "Field Guide", THE Pause_Menu_Controller SHALL show the Help section with core inputs and mission rhythm text
10. WHEN the player clicks "Settings", THE Pause_Menu_Controller SHALL show the Settings section with language cycling and controls panel
11. WHEN the player clicks "Exit to Main Menu", THE Pause_Menu_Controller SHALL display a Modal_Window confirmation dialog
12. WHEN the player confirms exit to main menu, THE Pause_Menu_Controller SHALL restore Time.timeScale, load 01_MAIN_MENU asynchronously, and call Resources.UnloadUnusedAssets() after scene load
13. WHEN the player clicks "Quit Application", THE Pause_Menu_Controller SHALL display a Modal_Window confirmation dialog
14. WHEN the player confirms quit, THE Pause_Menu_Controller SHALL call Application.Quit() (or EditorApplication.isPlaying = false in editor)
15. WHEN the player clicks "Back" in any sub-section, THE Pause_Menu_Controller SHALL return to Main section
16. WHEN the player presses Escape in any sub-section, THE Pause_Menu_Controller SHALL return to Main section
17. WHEN any section transition occurs, THE Pause_Menu_Controller SHALL set Default_Selection to the first interactable button in the new section
18. WHEN the pause menu is open, THE Pause_Menu_Controller SHALL block PDA and Fabricator UI from opening
19. WHEN the pause menu closes, THE Pause_Menu_Controller SHALL restore player input mode and cursor lock state
20. FOR ALL section transitions, THE Pause_Menu_Controller SHALL use CanvasGroup alpha fades (zero-GC, no SetActive calls)

### Requirement 3: Input Rebinding UX Completion

**User Story:** As a player, I want to rebind my input controls through the pause menu settings, so that I can customize my controls and see the changes reflected immediately in-game.

#### Acceptance Criteria

1. WHEN the player opens Settings in pause menu, THE Pause_Controls_Panel SHALL display all rebindable actions with current binding display strings
2. WHEN a binding display string is empty or missing, THE Pause_Controls_Panel SHALL display "--" instead of broken text
3. WHEN the player clicks a rebind button, THE Rebinding_Manager SHALL start interactive rebinding and display "Waiting for input..." text
4. WHEN the player presses a key or button during rebinding, THE Rebinding_Manager SHALL apply the new binding and update the display string
5. WHEN the player presses Escape during rebinding, THE Rebinding_Manager SHALL cancel the rebind and restore the previous binding
6. WHEN a rebind completes, THE Rebinding_Manager SHALL save Input_Override to PlayerPrefs via UserOptionsPersistence
7. WHEN the player clicks "Reset to Defaults", THE Pause_Controls_Panel SHALL call Rebinding_Manager.ClearOverrides() and refresh all display strings
8. WHEN the player clicks "Apply", THE Pause_Controls_Panel SHALL save all Input_Override changes via Rebinding_Manager.SaveOverrides()
9. WHEN the player clicks "Cancel", THE Pause_Controls_Panel SHALL reload Input_Override from PlayerPrefs and refresh all display strings
10. WHEN the player opens Settings, THE Pause_Controls_Panel SHALL call Rebinding_Manager.LoadOverrides() to ensure current state
11. WHEN a rebind conflicts with an existing binding, THE Rebinding_Manager SHALL display a warning and allow the player to confirm or cancel
12. WHEN the player rebinds a composite binding (e.g., WASD), THE Rebinding_Manager SHALL rebind each part individually
13. WHEN the player rebinds a gamepad button, THE Rebinding_Manager SHALL exclude mouse and keyboard inputs from detection
14. WHEN the player rebinds a keyboard key, THE Rebinding_Manager SHALL exclude gamepad inputs from detection
15. WHEN the player closes Settings, THE Pause_Controls_Panel SHALL ensure all Input_Override changes are saved
16. WHEN the player reopens Settings, THE Pause_Controls_Panel SHALL display the most recent Input_Override state
17. WHEN a rebind operation is in progress, THE Pause_Controls_Panel SHALL disable all other rebind buttons
18. WHEN a rebind operation completes or cancels, THE Pause_Controls_Panel SHALL re-enable all rebind buttons
19. WHEN the player rebinds an action, THE Pause_Controls_Panel SHALL immediately update the display string without requiring a panel refresh
20. FOR ALL rebind operations, THE Rebinding_Manager SHALL use zero-GC cached delegates and avoid LINQ or string allocations

### Requirement 4: Unified Settings Ownership

**User Story:** As a player, I want all my settings (graphics, audio, input, language) to persist across sessions, so that I don't have to reconfigure my preferences every time I play.

#### Acceptance Criteria

1. THE Settings_Owner SHALL exist as a singleton DontDestroyOnLoad MonoBehaviour
2. THE Settings_Owner SHALL load all User_Options from PlayerPrefs on Awake
3. THE Settings_Owner SHALL save all User_Options to PlayerPrefs when any setting changes
4. THE Settings_Owner SHALL expose public properties for graphics quality, audio volume, language, and input overrides
5. WHEN the player changes graphics quality, THE Settings_Owner SHALL apply the new quality level and save to PlayerPrefs
6. WHEN the player changes audio volume, THE Settings_Owner SHALL apply the new volume to AudioMixer and save to PlayerPrefs
7. WHEN the player changes language, THE Settings_Owner SHALL call LocalizationManager.SetLanguage() and save to PlayerPrefs
8. WHEN the player rebinds input, THE Settings_Owner SHALL delegate to Rebinding_Manager and ensure Input_Override is saved
9. WHEN the player resets settings to defaults, THE Settings_Owner SHALL clear all User_Options from PlayerPrefs and reload defaults
10. WHEN the game starts, THE Settings_Owner SHALL apply all User_Options before the main menu appears
11. WHEN the player opens Settings in main menu or pause menu, THE Settings_Owner SHALL provide current User_Options to the UI
12. WHEN the player closes Settings without saving, THE Settings_Owner SHALL revert to the last saved User_Options state
13. WHEN the player closes Settings with saving, THE Settings_Owner SHALL persist all User_Options to PlayerPrefs
14. WHEN a setting change fails to apply, THE Settings_Owner SHALL log an error and revert to the previous value
15. WHEN the player changes multiple settings, THE Settings_Owner SHALL batch save to PlayerPrefs once at the end
16. WHEN the player opens Settings, THE Settings_Owner SHALL validate all User_Options and repair any corrupted values
17. WHEN a User_Options key is missing from PlayerPrefs, THE Settings_Owner SHALL use the default value
18. WHEN a User_Options value is out of range, THE Settings_Owner SHALL clamp to valid range and save the corrected value
19. WHEN the player changes settings in main menu, THE Settings_Owner SHALL ensure changes persist into the game world
20. FOR ALL settings operations, THE Settings_Owner SHALL use zero-GC cached strings and avoid LINQ or allocations

### Requirement 5: Save/Load User Trust

**User Story:** As a player, I want clear feedback when saving or loading fails, so that I understand what went wrong and can take corrective action without losing progress.

#### Acceptance Criteria

1. WHEN a save operation starts, THE Save_Manager SHALL raise SaveEvents.OnSaveStarted and set IsBusy to true
2. WHEN a save operation succeeds, THE Save_Manager SHALL raise SaveEvents.OnSaveCompleted, set LastOperationSucceeded to true, and set IsBusy to false
3. WHEN a save operation fails, THE Save_Manager SHALL raise SaveEvents.OnSaveFailed with error message, set LastOperationSucceeded to false, and set IsBusy to false
4. WHEN a save operation fails, THE Shell_System SHALL display a Modal_Window with error message and "Retry" or "Cancel" options
5. WHEN a load operation starts, THE Save_Manager SHALL raise SaveEvents.OnLoadStarted and set IsBusy to true
6. WHEN a load operation succeeds, THE Save_Manager SHALL raise SaveEvents.OnLoadCompleted, set LastOperationSucceeded to true, and set IsBusy to false
7. WHEN a load operation fails, THE Save_Manager SHALL raise SaveEvents.OnLoadFailed with error message, set LastOperationSucceeded to false, and set IsBusy to false
8. WHEN a load operation fails, THE Shell_System SHALL display a Modal_Window with error message and "Retry" or "Return to Menu" options
9. WHEN a save file is corrupt, THE Save_Manager SHALL attempt to load from backup (.bak) and set LastLoadUsedBackup to true
10. WHEN all save file candidates (primary + backups) are corrupt, THE Save_Manager SHALL return error "No valid save data found for slot"
11. WHEN a save file checksum mismatch occurs, THE Save_Manager SHALL log a warning and attempt backup load
12. WHEN a save file is missing, THE Save_Manager SHALL return error "Save file does not exist for slot"
13. WHEN a save operation is already in progress, THE Save_Manager SHALL reject new save requests with error "Save already in progress"
14. WHEN a load operation is already in progress, THE Save_Manager SHALL reject new load requests with error "Load already in progress"
15. WHEN the player attempts to save during scene transition, THE Save_Manager SHALL block the save and return error "Cannot save during scene transition"
16. WHEN the player views save slots in main menu, THE Main_Menu_Controller SHALL display "Empty Slot" for missing saves
17. WHEN the player views save slots in main menu, THE Main_Menu_Controller SHALL display playtime, timestamp, and scene name for existing saves
18. WHEN the player views save slots in pause menu, THE Pause_Menu_Controller SHALL display "Awaiting save command" before any save operation
19. WHEN the player views save slots in pause menu, THE Pause_Menu_Controller SHALL display "{SLOT_NAME} WRITTEN" after successful save
20. WHEN the player views save slots in pause menu, THE Pause_Menu_Controller SHALL display "{SLOT_NAME} FAILED. {ERROR}" after failed save

### Requirement 6: Complete Navigation Paths

**User Story:** As a player, I want all back and cancel buttons to work correctly, so that I can navigate the menu system without getting stuck in dead-end states.

#### Acceptance Criteria

1. WHEN the player clicks "Back" in any sub-panel, THE Shell_System SHALL return to the previous panel
2. WHEN the player presses Escape in any sub-panel, THE Shell_System SHALL return to the previous panel
3. WHEN the player is in mainMenuGroup and presses Escape, THE Main_Menu_Controller SHALL display quit confirmation
4. WHEN the player is in Main section of pause menu and presses Escape, THE Pause_Menu_Controller SHALL close the pause menu
5. WHEN the player is in any sub-section of pause menu and presses Escape, THE Pause_Menu_Controller SHALL return to Main section
6. WHEN the player cancels a Modal_Window, THE Shell_System SHALL return to the previous state without executing the action
7. WHEN the player confirms a Modal_Window, THE Shell_System SHALL execute the action and transition to the appropriate state
8. WHEN the player is rebinding input and presses Escape, THE Rebinding_Manager SHALL cancel the rebind and restore the previous binding
9. WHEN the player is in Settings and clicks "Cancel", THE Shell_System SHALL revert all unsaved changes and return to the previous panel
10. WHEN the player is in Settings and clicks "Apply", THE Shell_System SHALL save all changes and return to the previous panel
11. WHEN the player is in saveLoadGroup and clicks "Back", THE Main_Menu_Controller SHALL return to mainMenuGroup
12. WHEN the player is in settingsGroup and clicks "Back", THE Main_Menu_Controller SHALL return to mainMenuGroup
13. WHEN the player is in Saves section of pause menu and clicks "Back", THE Pause_Menu_Controller SHALL return to Main section
14. WHEN the player is in Help section of pause menu and clicks "Back", THE Pause_Menu_Controller SHALL return to Main section
15. WHEN the player is in Settings section of pause menu and clicks "Back", THE Pause_Menu_Controller SHALL return to Main section
16. WHEN the player is in any panel and the scene unloads, THE Shell_System SHALL gracefully handle cleanup without errors
17. WHEN the player is in any panel and the game pauses, THE Shell_System SHALL maintain correct state without double-pausing
18. WHEN the player is in any panel and the game unpauses, THE Shell_System SHALL restore correct input mode and cursor state
19. WHEN the player navigates between panels, THE Shell_System SHALL maintain correct Default_Selection for gamepad/keyboard
20. FOR ALL navigation paths, THE Shell_System SHALL ensure no panel is left in an invalid state (alpha=0 but interactable=true)

### Requirement 7: Default Selection and Focus Management

**User Story:** As a player using gamepad or keyboard, I want the menu system to automatically select the correct button when I navigate between panels, so that I can use the menu without needing a mouse.

#### Acceptance Criteria

1. WHEN the main menu opens, THE Main_Menu_Controller SHALL set Default_Selection to "New Game" button
2. WHEN the player opens saveLoadGroup, THE Main_Menu_Controller SHALL set Default_Selection to the first interactable Save_Slot button
3. WHEN all Save_Slot buttons are disabled, THE Main_Menu_Controller SHALL set Default_Selection to "Back" button
4. WHEN the player opens settingsGroup, THE Main_Menu_Controller SHALL set Default_Selection to the first settings control
5. WHEN the player returns to mainMenuGroup, THE Main_Menu_Controller SHALL set Default_Selection to "New Game" button
6. WHEN the pause menu opens, THE Pause_Menu_Controller SHALL set Default_Selection to "Resume" button
7. WHEN the player opens Saves section, THE Pause_Menu_Controller SHALL set Default_Selection to the first Save_Slot button
8. WHEN the player opens Help section, THE Pause_Menu_Controller SHALL set Default_Selection to "Back" button
9. WHEN the player opens Settings section, THE Pause_Menu_Controller SHALL set Default_Selection to the first rebind button
10. WHEN the player returns to Main section, THE Pause_Menu_Controller SHALL set Default_Selection to "Resume" button
11. WHEN a panel transition completes, THE Shell_System SHALL call EventSystem.SetSelectedGameObject() with the Default_Selection button
12. WHEN the player uses mouse to click a button, THE Shell_System SHALL update EventSystem selection to match the clicked button
13. WHEN the player uses gamepad to navigate, THE Shell_System SHALL maintain visible button highlight via EventSystem
14. WHEN the player uses keyboard to navigate, THE Shell_System SHALL maintain visible button highlight via EventSystem
15. WHEN a button is disabled, THE Shell_System SHALL skip it in navigation order
16. WHEN a button is re-enabled, THE Shell_System SHALL include it in navigation order
17. WHEN the player navigates to a button, THE Shell_System SHALL ensure the button is visible (not scrolled off-screen)
18. WHEN the player navigates between panels, THE Shell_System SHALL clear previous selection before setting new Default_Selection
19. WHEN the player opens a Modal_Window, THE Shell_System SHALL set Default_Selection to the confirm button
20. FOR ALL panel transitions, THE Shell_System SHALL ensure Default_Selection is set before the panel becomes interactable

### Requirement 8: Zero-GC Panel Transitions

**User Story:** As a developer, I want all panel transitions to use CanvasGroup alpha fades without SetActive calls, so that the menu system maintains zero-GC performance in hot paths.

#### Acceptance Criteria

1. THE Shell_System SHALL use CanvasGroup.alpha for all panel show/hide operations
2. THE Shell_System SHALL set CanvasGroup.interactable to false during panel transitions
3. THE Shell_System SHALL set CanvasGroup.blocksRaycasts to false during panel transitions
4. THE Shell_System SHALL set CanvasGroup.interactable to true when a panel transition completes
5. THE Shell_System SHALL set CanvasGroup.blocksRaycasts to true when a panel transition completes
6. THE Shell_System SHALL use ITickable for panel transition updates (no Update() or coroutines)
7. THE Shell_System SHALL cache all CanvasGroup references in Awake (no GetComponent in hot paths)
8. THE Shell_System SHALL use Time.unscaledTime for panel transition timing (works when Time.timeScale = 0)
9. THE Shell_System SHALL use Mathf.Lerp for alpha interpolation (no DOTween or animation curves in hot paths)
10. THE Shell_System SHALL complete panel transitions in configurable fade duration (default 0.2s)
11. THE Shell_System SHALL prevent double-clicks by disabling interactable immediately on button press
12. THE Shell_System SHALL prevent overlapping transitions by checking _isTransitioning flag
13. THE Shell_System SHALL use enum state machine for panel transition phases (None, FadingOut, FadingIn)
14. THE Shell_System SHALL reset transition state when a transition completes
15. THE Shell_System SHALL handle rapid Escape presses without breaking transition state
16. THE Shell_System SHALL handle scene unload during transition without errors
17. THE Shell_System SHALL use cached string templates for loading percent text (no string interpolation in hot paths)
18. THE Shell_System SHALL use dirty flag for text updates (only update when value changes)
19. THE Shell_System SHALL use MaterialPropertyBlock for any renderer property changes (not renderer.material)
20. FOR ALL panel transitions, THE Shell_System SHALL produce zero GC allocations per frame

### Requirement 9: Localization Integration

**User Story:** As a player, I want all menu text to respect my language setting, so that I can play the game in my preferred language.

#### Acceptance Criteria

1. WHEN the game starts, THE Shell_System SHALL load current language from Settings_Owner
2. WHEN the game starts, THE Shell_System SHALL call LocalizationManager.SetLanguage() with the saved language
3. WHEN the player changes language in Settings, THE Shell_System SHALL call LocalizationManager.SetLanguage() and refresh all visible text
4. WHEN the player changes language, THE Shell_System SHALL save the new language to Settings_Owner
5. WHEN a panel opens, THE Shell_System SHALL refresh all text labels via LocalizationManager.Get()
6. WHEN a Modal_Window opens, THE Shell_System SHALL use localized title and message text
7. WHEN a save operation displays status, THE Shell_System SHALL use localized status text
8. WHEN a load operation displays error, THE Shell_System SHALL use localized error text
9. WHEN the player views save slots, THE Shell_System SHALL use localized "Empty Slot" text
10. WHEN the player views loading screen, THE Shell_System SHALL use localized "Loading..." text
11. WHEN the player views pause menu, THE Shell_System SHALL use localized section titles and button labels
12. WHEN the player views help section, THE Shell_System SHALL use localized help text
13. WHEN the player views settings section, THE Shell_System SHALL use localized setting labels
14. WHEN the player views rebind UI, THE Shell_System SHALL use localized action names
15. WHEN the player views rebind UI, THE Shell_System SHALL use localized "Waiting for input..." text
16. WHEN the player cycles language, THE Shell_System SHALL display current language name in Settings
17. WHEN the player cycles language, THE Shell_System SHALL update all visible text immediately
18. WHEN the player cycles language, THE Shell_System SHALL maintain correct Default_Selection
19. WHEN a localization key is missing, THE Shell_System SHALL display the key name as fallback text
20. FOR ALL localized text, THE Shell_System SHALL use cached LocalizationKeys constants (no string literals)

### Requirement 10: Error Handling and Edge Cases

**User Story:** As a player, I want the menu system to handle errors gracefully, so that I don't encounter crashes or broken states when something goes wrong.

#### Acceptance Criteria

1. WHEN SaveManager.Instance is null, THE Shell_System SHALL disable save/load buttons and display "Save system unavailable"
2. WHEN InputManager.Instance is null, THE Shell_System SHALL disable rebind buttons and display "Input system unavailable"
3. WHEN LocalizationManager.Instance is null, THE Shell_System SHALL use fallback English text
4. WHEN a save file is corrupt, THE Shell_System SHALL display "Save file corrupt, attempting backup load"
5. WHEN all save file candidates are corrupt, THE Shell_System SHALL display "No valid save data found"
6. WHEN a save operation times out, THE Shell_System SHALL display "Save operation timed out, please retry"
7. WHEN a load operation times out, THE Shell_System SHALL display "Load operation timed out, please retry"
8. WHEN a scene load fails, THE Shell_System SHALL display "Failed to load scene, returning to menu"
9. WHEN a rebind operation fails, THE Shell_System SHALL display "Rebind failed, please try again"
10. WHEN a settings change fails, THE Shell_System SHALL display "Failed to apply setting, reverting to previous value"
11. WHEN the player spams Escape during transition, THE Shell_System SHALL ignore input until transition completes
12. WHEN the player spams button clicks during transition, THE Shell_System SHALL ignore input until transition completes
13. WHEN the player opens pause menu while PDA is open, THE Shell_System SHALL close PDA first
14. WHEN the player opens pause menu while Fabricator is open, THE Shell_System SHALL close Fabricator first
15. WHEN the player closes pause menu, THE Shell_System SHALL restore correct input mode (Player, not UI)
16. WHEN the player closes pause menu, THE Shell_System SHALL restore correct cursor state (locked and hidden)
17. WHEN the player exits to main menu, THE Shell_System SHALL call Resources.UnloadUnusedAssets() to free memory
18. WHEN the player quits the game, THE Shell_System SHALL ensure all settings are saved before Application.Quit()
19. WHEN the game is in editor, THE Shell_System SHALL use EditorApplication.isPlaying = false instead of Application.Quit()
20. FOR ALL error states, THE Shell_System SHALL log detailed error messages to console for debugging

