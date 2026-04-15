# Implementation Plan: Shell/UX Production System

## Overview

This plan implements the HECTON-8 shell/UX production system across five fronts: main menu flow completion, pause shell completion, input rebinding UX, unified settings ownership, and save/load user trust. Implementation follows zero-GC hot path rules, ITickable state machines, CanvasGroup alpha transitions, and strict AGENTS.md compliance.

All code references existing verified systems: MainMenuController, PauseMenuController, SettingsManager, SaveManager, RebindingManager, LocalizationManager, and SpatialAudioManager.

## Tasks

- [x] 1. Main Menu Flow - Core Panel Transitions
  - Implement CanvasGroup alpha fade state machine in MainMenuController
  - Add panel transition state enum (None, FadingOut, FadingIn)
  - Cache all CanvasGroup references in Awake (mainMenuGroup, saveLoadGroup, settingsGroup, loadingGroup)
  - Implement FadeToPanel(CanvasGroup from, CanvasGroup to, Action onComplete) using ITickable
  - Set interactable=false and blocksRaycasts=false during transitions
  - Use Time.unscaledTime for timing (works when Time.timeScale = 0)
  - _Requirements: 1.17, 1.18, 1.20, 8.1-8.13_

- [ ]* 1.1 Write unit tests for panel transition state machine
  - Test rapid Escape presses during transition
  - Test overlapping transition prevention
  - Test scene unload during transition
  - _Requirements: 1.17, 8.15, 8.16_

- [x] 2. Main Menu Flow - New Game Path
  - Implement OnNewGameClicked() with modal confirmation
  - Create ShowModal(string title, string message, Action onConfirm, Action onCancel)
  - On confirm: write GameStartContext.CreateNewGame() and load 02_HECTON_WORLD async
  - Display loadingGroup panel with progress bar and percent text
  - Use cached string template for percent text (zero-GC)
  - Update percent to 100% at 90% load, then activate scene
  - _Requirements: 1.1, 1.2, 1.15, 1.16, 8.17_

- [ ]* 2.1 Write unit tests for new game flow
  - Test modal cancel behavior
  - Test loading screen progress updates
  - Test scene activation timing
  - _Requirements: 1.1, 1.2, 1.15_

- [x] 3. Main Menu Flow - Load Game Path
  - Implement OnLoadGameClicked() to fade to saveLoadGroup
  - Query SaveManager for three slot metadata (slot_1, slot_2, slot_3)
  - Update SaveSlotUI components with metadata (name, playtime, timestamp, scene)
  - Display "Empty Slot" for missing saves and disable button
  - Implement OnSlotClicked(string slotName) with modal confirmation
  - On confirm: write GameStartContext.CreateLoadGame(slotName) and load 02_HECTON_WORLD
  - Handle corrupt save files with error modal and backup recovery messaging
  - _Requirements: 1.3, 1.4, 1.5, 1.6, 1.7, 5.9, 5.10, 5.16, 5.17_

- [ ]* 3.1 Write unit tests for load game flow
  - Test empty slot presentation
  - Test populated slot metadata display
  - Test corrupt save file handling
  - Test backup recovery path
  - _Requirements: 1.4, 1.5, 5.9, 5.10_

- [x] 4. Main Menu Flow - Settings and Quit Paths
  - Implement OnSettingsClicked() to fade to settingsGroup if available
  - Implement DetermineSettingsAvailability() checking SettingsPanel presence
  - Disable Settings button if unavailable (stub state)
  - Implement OnQuitClicked() with modal confirmation
  - On confirm: call Application.Quit() or EditorApplication.isPlaying = false in editor
  - _Requirements: 1.8, 1.9, 1.10, 1.11, 10.19_

- [x] 5. Main Menu Flow - Back Navigation and Focus Management
  - Implement OnBackClicked() for saveLoadGroup and settingsGroup
  - Handle Escape key in each panel group (return to mainMenuGroup or quit modal)
  - Implement SetDefaultSelection(GameObject button) calling EventSystem.SetSelectedGameObject
  - Set default focus after each panel transition (New Game, first slot, first setting, Back)
  - Clear previous selection before setting new default
  - Handle disabled buttons in navigation order
  - _Requirements: 1.12, 1.13, 1.14, 1.18, 1.19, 7.1-7.11, 7.18, 7.19_

- [ ]* 5.1 Write unit tests for navigation and focus
  - Test Back button from all sub-panels
  - Test Escape key handling in each panel
  - Test default selection after transitions
  - Test disabled button navigation skip
  - _Requirements: 6.1-6.5, 7.1-7.11_

- [x] 6. Checkpoint - Main Menu Flow Verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Pause Shell - Core Structure and State Management
  - Implement OpenPause() in PauseMenuController with Time.timeScale = 0
  - Switch InputManager to UI mode and show cursor
  - Block opening when PDA or Fabricator menus are open
  - Implement ClosePause() restoring Time.timeScale, Player input mode, and cursor lock
  - Build section state machine (Main, Saves, Help, Settings)
  - Implement CanvasGroup alpha transitions between sections using ITickable
  - Cache all section CanvasGroup references in Awake
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.18, 2.19, 2.20, 8.1-8.13_

- [ ]* 7.1 Write unit tests for pause state management
  - Test Time.timeScale restoration
  - Test input mode switching
  - Test cursor state restoration
  - Test PDA/Fabricator blocking
  - _Requirements: 2.1, 2.2, 2.4, 2.18, 2.19_

- [x] 8. Pause Shell - Main Section and Resume
  - Build Main section UI with six buttons (Resume, Save Station, Field Guide, Settings, Exit to Main Menu, Quit)
  - Implement OnResumeClicked() calling ClosePause()
  - Handle Escape key in Main section to close pause
  - Set default selection to Resume button when pause opens
  - _Requirements: 2.3, 2.4, 2.10, 7.6, 7.10_

- [x] 9. Pause Shell - Saves Section
  - Implement OnSaveStationClicked() to show Saves section
  - Display three SaveSlotUI buttons with current metadata
  - Implement OnSaveSlotClicked(string slotName) calling SaveManager.SaveGameAsync
  - Subscribe to SaveEvents.OnSaveStarted, OnSaveCompleted, OnSaveFailed
  - Display status text: "Awaiting save command" → "{SLOT_NAME} WRITTEN." or "{SLOT_NAME} FAILED. {ERROR}"
  - Set default selection to first Save_Slot button
  - Harden async void SaveSlot() to async Task with proper exception handling
  - _Requirements: 2.5, 2.6, 2.7, 2.8, 5.1, 5.2, 5.3, 5.18, 5.19, 7.7_

- [ ]* 9.1 Write unit tests for pause save flow
  - Test successful save operation
  - Test save failure with error message
  - Test save-in-progress rejection
  - Test status text updates
  - _Requirements: 2.6, 2.7, 2.8, 5.1-5.4, 5.13_

- [x] 10. Pause Shell - Help and Settings Sections
  - Implement OnFieldGuideClicked() to show Help section with core inputs and mission rhythm text
  - Implement OnSettingsClicked() to show Settings section
  - Display language cycling controls and PauseControlsPanel
  - Set default selection to Back button (Help) or first rebind button (Settings)
  - _Requirements: 2.9, 2.10, 7.8, 7.9_

- [x] 11. Pause Shell - Exit to Main Menu and Quit
  - Implement OnExitToMainMenuClicked() with modal confirmation
  - On confirm: restore Time.timeScale, load 01_MAIN_MENU async, call Resources.UnloadUnusedAssets()
  - Implement OnQuitApplicationClicked() with modal confirmation
  - On confirm: call Application.Quit() or EditorApplication.isPlaying = false in editor
  - _Requirements: 2.11, 2.12, 2.13, 2.14, 10.17, 10.18, 10.19_

- [x] 12. Pause Shell - Back Navigation and Section Transitions
  - Implement OnBackClicked() for all sub-sections returning to Main
  - Handle Escape key in sub-sections to return to Main
  - Set default selection after each section transition
  - Ensure CanvasGroup transitions use zero-GC alpha fades
  - _Requirements: 2.15, 2.16, 2.17, 2.20, 6.5, 7.10_

- [ ]* 12.1 Write unit tests for pause navigation
  - Test Back button from all sub-sections
  - Test Escape key handling in sub-sections
  - Test section transition state machine
  - _Requirements: 2.15, 2.16, 6.5_

- [x] 13. Checkpoint - Pause Shell Verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Input Rebinding - PauseControlsPanel Integration
  - Implement InitializeRebindUI() in PauseControlsPanel
  - Call RebindingManager.LoadOverrides() on panel open
  - Display all rebindable actions with current binding display strings
  - Display "--" for empty or missing binding strings
  - Cache all rebind button references in Awake
  - _Requirements: 3.1, 3.2, 3.10, 3.19_

- [x] 15. Input Rebinding - Interactive Rebinding Flow
  - Implement OnRebindButtonClicked(string actionName) calling RebindingManager.StartRebind
  - Display "Waiting for input..." text during rebinding
  - Disable all other rebind buttons during operation
  - Handle player input and apply new binding via RebindingManager
  - Update display string immediately after rebind completes
  - Handle Escape key to cancel rebind and restore previous binding
  - Re-enable all rebind buttons after completion or cancel
  - _Requirements: 3.3, 3.4, 3.5, 3.17, 3.18, 3.19, 3.20_

- [x] 16. Input Rebinding - Conflict Handling and Persistence
  - Implement conflict detection when rebinding
  - Display warning modal with confirm/cancel options
  - Handle composite bindings (WASD) by rebinding each part individually
  - Exclude mouse/keyboard inputs when rebinding gamepad buttons
  - Exclude gamepad inputs when rebinding keyboard keys
  - Save Input_Override to PlayerPrefs via RebindingManager on rebind complete
  - _Requirements: 3.6, 3.11, 3.12, 3.13, 3.14, 3.20_

- [x] 17. Input Rebinding - Apply, Cancel, and Reset
  - Implement OnApplyClicked() calling RebindingManager.SaveOverrides()
  - Implement OnCancelClicked() calling RebindingManager.LoadOverrides() and refreshing display strings
  - Implement OnResetToDefaultsClicked() calling RebindingManager.ClearOverrides() and refreshing display strings
  - Ensure all Input_Override changes are saved when closing Settings
  - Ensure most recent state is displayed when reopening Settings
  - _Requirements: 3.7, 3.8, 3.9, 3.15, 3.16_

- [ ]* 17.1 Write unit tests for rebinding flow
  - Test interactive rebinding with key press
  - Test Escape cancel during rebinding
  - Test conflict detection and warning
  - Test Apply/Cancel/Reset behavior
  - Test persistence across panel open/close
  - _Requirements: 3.3-3.9, 3.15, 3.16_

- [x] 18. Checkpoint - Input Rebinding Verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 19. Settings Ownership - SettingsManager Singleton Setup
  - Verify SettingsManager exists as DontDestroyOnLoad singleton
  - Implement LoadSettings() in Awake loading all User_Options from PlayerPrefs
  - Implement SaveSettings() persisting all User_Options to PlayerPrefs
  - Expose public properties for graphics quality, audio volume, language, input overrides
  - Implement lazy access pattern with null checks for all manager dependencies
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.20, 10.1, 10.2, 10.3_

- [x] 20. Settings Ownership - Graphics and Audio Application
  - Implement SetGraphicsQuality(int level) applying quality level and saving to PlayerPrefs
  - Implement SetAudioVolume(float volume) applying to AudioMixer and saving to PlayerPrefs
  - Implement SetLanguage(string language) calling LocalizationManager.SetLanguage() and saving to PlayerPrefs
  - Cache mainCamera and urpVolume references in Awake
  - Apply FOV through mainCamera.fieldOfView
  - Apply Bloom and Motion Blur through urpVolume
  - Verify Ambient Occlusion persistence and application parity
  - _Requirements: 4.5, 4.6, 4.7, 4.10_

- [x] 21. Settings Ownership - Settings Panel Staging and Preview
  - Verify SettingsPanel reads current values from SettingsManager
  - Implement staged value caching in SettingsPanel
  - Implement OnApplyClicked() committing staged state to SettingsManager
  - Implement OnCancelClicked() reverting preview/staged state to last persisted state
  - Implement OnResetToDefaultsClicked() clearing User_Options and reloading defaults
  - Integrate SettingsLivePreview for safe preview changes (FOV, Bloom, Motion Blur)
  - Ensure preview is reversible and does not persist until Apply
  - _Requirements: 4.9, 4.11, 4.12, 4.13, 4.14_

- [x] 22. Settings Ownership - Validation and Error Handling
  - Implement ValidateSettings() checking all User_Options on load
  - Repair corrupted values by clamping to valid range and saving corrected value
  - Use default value for missing PlayerPrefs keys
  - Implement batch save to PlayerPrefs when multiple settings change
  - Log error and revert to previous value when setting change fails to apply
  - _Requirements: 4.15, 4.16, 4.17, 4.18, 4.19, 10.10_

- [ ]* 22.1 Write unit tests for settings ownership
  - Test settings load from PlayerPrefs
  - Test settings save to PlayerPrefs
  - Test validation and repair of corrupted values
  - Test Apply/Cancel/Reset behavior
  - Test persistence across scene transitions
  - _Requirements: 4.2, 4.3, 4.9, 4.12, 4.13, 4.16-4.18_

- [x] 23. Checkpoint - Settings Ownership Verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 24. Save/Load Trust - SaveManager Event Integration
  - Subscribe to SaveEvents.OnSaveStarted, OnSaveCompleted, OnSaveFailed in MainMenuController
  - Subscribe to SaveEvents.OnLoadStarted, OnLoadCompleted, OnLoadFailed in MainMenuController
  - Subscribe to same events in PauseMenuController
  - Implement OnSaveStarted() setting IsBusy flag and disabling save buttons
  - Implement OnSaveCompleted() setting LastOperationSucceeded=true, IsBusy=false, displaying success message
  - Implement OnSaveFailed(string error) setting LastOperationSucceeded=false, IsBusy=false, displaying error modal
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.19, 5.20_

- [x] 25. Save/Load Trust - Load Operation Event Handling
  - Implement OnLoadStarted() setting IsBusy flag and disabling load buttons
  - Implement OnLoadCompleted() setting LastOperationSucceeded=true, IsBusy=false
  - Implement OnLoadFailed(string error) setting LastOperationSucceeded=false, IsBusy=false, displaying error modal
  - Display "Retry" or "Return to Menu" options in load failure modal
  - Handle corrupt save file with backup recovery messaging
  - Display "No valid save data found for slot" when all candidates are corrupt
  - Set LastLoadUsedBackup flag when backup is used
  - _Requirements: 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 5.12_

- [x] 26. Save/Load Trust - Operation Rejection and Edge Cases
  - Implement save/load rejection when IsBusy=true with error "Save/Load already in progress"
  - Block save during scene transition with error "Cannot save during scene transition"
  - Display clear error messages for missing save files
  - Display checksum mismatch warnings and backup load attempts
  - Ensure all error states log detailed messages to console for debugging
  - _Requirements: 5.13, 5.14, 5.15, 10.4-10.8, 10.20_

- [ ]* 26.1 Write unit tests for save/load trust
  - Test save success and failure paths
  - Test load success and failure paths
  - Test corrupt save file handling
  - Test backup recovery
  - Test operation rejection when busy
  - Test scene transition blocking
  - _Requirements: 5.1-5.15_

- [x] 27. Checkpoint - Save/Load Trust Verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 28. Localization Integration - Language Loading and Refresh
  - Implement LoadLanguage() in MainMenuController and PauseMenuController
  - Load current language from SettingsManager on Awake
  - Call LocalizationManager.SetLanguage() with saved language
  - Implement RefreshAllText() calling LocalizationManager.Get() for all visible text labels
  - Cache LocalizationKeys constants for all menu text (zero-GC)
  - Display key name as fallback when localization key is missing
  - _Requirements: 9.1, 9.2, 9.5, 9.19, 9.20_

- [x] 29. Localization Integration - Language Change Handling
  - Implement OnLanguageChanged() in SettingsPanel
  - Call LocalizationManager.SetLanguage() with new language
  - Save new language to SettingsManager
  - Call RefreshAllText() to update all visible text immediately
  - Maintain correct Default_Selection after language change
  - Display current language name in Settings
  - _Requirements: 9.3, 9.4, 9.16, 9.17, 9.18_

- [x] 30. Localization Integration - Localized UI Elements
  - Implement localized text for all modal dialogs (title, message, confirm, cancel)
  - Implement localized status text for save/load operations
  - Implement localized "Empty Slot" text for save slots
  - Implement localized "Loading..." text for loading screen
  - Implement localized section titles and button labels for pause menu
  - Implement localized help text for Field Guide section
  - Implement localized setting labels and action names for rebinding UI
  - Implement localized "Waiting for input..." text for rebinding
  - _Requirements: 9.6, 9.7, 9.8, 9.9, 9.10, 9.11, 9.12, 9.13, 9.14, 9.15_

- [ ]* 30.1 Write unit tests for localization
  - Test language load on startup
  - Test language change and text refresh
  - Test fallback text for missing keys
  - Test localized modal dialogs
  - Test localized status messages
  - _Requirements: 9.1-9.5, 9.19_

- [x] 31. Error Handling - Singleton Null Checks
  - Add null checks for SaveManager.Instance in all save/load operations
  - Display "Save system unavailable" and disable save/load buttons when null
  - Add null checks for InputManager.Instance in all input operations
  - Display "Input system unavailable" and disable rebind buttons when null
  - Add null checks for LocalizationManager.Instance in all text operations
  - Use fallback English text when LocalizationManager is null
  - Add null checks for all singleton accesses in OnDisable/OnDestroy
  - _Requirements: 10.1, 10.2, 10.3, 10.16_

- [x] 32. Error Handling - Input Spam and Transition Protection
  - Implement _isTransitioning flag to prevent overlapping transitions
  - Ignore Escape key input during panel transitions
  - Ignore button clicks during panel transitions
  - Set interactable=false immediately on button press to prevent double-clicks
  - Handle rapid Escape presses without breaking transition state
  - Handle scene unload during transition without errors
  - _Requirements: 8.11, 8.12, 8.15, 8.16, 10.11, 10.12_

- [x] 33. Error Handling - UI State Conflicts and Cleanup
  - Close PDA before opening pause menu if PDA is open
  - Close Fabricator before opening pause menu if Fabricator is open
  - Restore correct input mode (Player, not UI) when closing pause menu
  - Restore correct cursor state (locked and hidden) when closing pause menu
  - Call Resources.UnloadUnusedAssets() after exiting to main menu
  - Ensure all settings are saved before Application.Quit()
  - _Requirements: 10.13, 10.14, 10.15, 10.16, 10.17, 10.18_

- [ ]* 33.1 Write unit tests for error handling
  - Test singleton null checks
  - Test input spam during transitions
  - Test PDA/Fabricator conflict resolution
  - Test input mode and cursor restoration
  - Test scene teardown safety
  - _Requirements: 10.1-10.3, 10.11-10.16_

- [x] 34. Final Integration and Wiring
  - Wire all MainMenuController panel references in 01_MAIN_MENU scene
  - Wire all button onClick events to controller methods
  - Wire all PauseMenuController section references at runtime
  - Verify all CanvasGroup references are cached in Awake
  - Verify all EventSystem default selections are set after transitions
  - Verify all modal dialogs use localized text
  - Verify all error states display clear messages
  - Verify all save/load operations subscribe to SaveEvents
  - Verify all settings changes go through SettingsManager
  - Verify all input rebinding goes through RebindingManager
  - _Requirements: All requirements_

- [ ]* 34.1 Write integration tests for complete flows
  - Test main menu → new game → world load
  - Test main menu → load game → world load
  - Test main menu → settings → apply/cancel
  - Test pause → saves → save operation
  - Test pause → settings → rebinding
  - Test pause → exit to main menu
  - _Requirements: All requirements_

- [x] 35. Final Checkpoint - Complete System Verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- All code must follow AGENTS.md zero-GC rules and ITickable patterns
- All panel transitions use CanvasGroup alpha fades (no SetActive)
- All singleton accesses include null checks
- All error states display clear user-facing messages
- Async void SaveSlot() in PauseMenuController is a known risk and must be hardened to async Task
