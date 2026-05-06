Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Pause Menu Plan

## Goal

Move controls/settings out of PDA and into a standard `Esc` pause menu flow.

## Target Structure

- `Esc` opens pause menu
- `Esc` while inside a sub-panel returns to pause main panel
- `Esc` from the main pause panel closes the menu and resumes gameplay

## Main Sections

1. `Resume Expedition`
2. `Save Station`
3. `Field Guide`
4. `Settings`
5. `Exit To Main Menu`
6. `Quit Application`

## Settings Scope

- Controls / rebinding
- Future:
  - audio
  - graphics
  - gameplay

## PDA Scope After Migration

PDA is now gameplay-facing, not settings-facing.

Active PDA tabs:
- `Inventory`
- `Loadout`
- `Data Log`

Removed from active PDA flow:
- `Controls`

## Current Implementation State

- `PauseMenuHost.cs`
  - creates runtime pause root under `Suit_HUD_Canvas`
- `PauseMenuController.cs`
  - owns pause shell, section switching, save/help/settings actions
  - pauses via `Time.timeScale = 0`
  - switches input maps through `InputManager`
- `PauseControlsPanel.cs`
  - runtime rebinding UI for settings
- `PlayerPDA.cs`
  - no longer uses Controls as an active PDA tab

## Verified

- Compile clean after pause migration
- `PauseMenu_Root` is created at runtime
- TMP font warnings from settings rebinding panel were removed
- `SpatialAudioManager` null-spam was removed

## Known Open Items

- Manual user verification of real `Esc` open/close flow is still needed
- Manual user verification of `Tab` closing PDA is still needed
- Save panel UX is functional first-pass, not polished
- Old `Tab_Controls` object still exists in scene as inactive legacy content and can be removed later if no fallback is needed

## Design Rules

- Pause menu should read as a ship/suit operations layer, not a casual game menu
- Keep layout calmer than PDA
- High readability over decorative clutter
- Use the same NASA-punk visual language as PDA, but with stronger clarity and less density
