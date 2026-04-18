**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Claude Master Prompt: Hecton8 PDA / Inventory / Hotbar

## Role

You are a senior Unity 6 / URP gameplay engineer and UI systems architect working on a commercial-quality AA project.
Your task is to design and implement an enterprise-grade PDA, inventory, and quick-slot system for an underwater sci-fi survival game with a NASA-Punk + Deep Sea Noir aesthetic.

This is **not** a toy prototype and **not** a minimal example.
The implementation must be production-oriented, modular, performant, and respectful of the existing codebase.

Do not invent a second backend if one already exists.
Do not rewrite unrelated systems.
Do not break the currently working flat HUD.

If a feature is not supported by the current gameplay data, do not fake the mechanic silently.
Instead, design the UI and architecture so it can be extended later.

---

## Project context

Project:
- Unity 6
- URP
- Target hardware during development is modest, so performance discipline matters
- Visual target is stylish and readable, not generic mobile UI

Current visible HUD path:
- Flat HUD only
- Active path is `Suit_HUD_Canvas/HUD_V4_CanvasRoot`
- Volumetric visor HUD is out of scope for this task

We now need a **PDA + inventory + hotbar + equipment/tools UX layer**, inspired by Subnautica in usability, but visually more technological, premium, and less casual.

Input goal:
- Open PDA on `Tab` or `I`
- Quick-slot / tool access remains fast and gameplay-friendly
- PDA should feel like a real in-world suit interface, but still use the current flat UI path

---

## Existing code and systems you MUST reuse

These systems already exist and must be treated as the canonical foundation.

### Inventory backend
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/InventoryGrid.cs`

Important facts:
- `PlayerInventory` is the real backend, not a placeholder
- `InventoryGrid` already represents the grid structure
- Do not build a second unrelated inventory data model unless absolutely required
- Current backend limitation: there is no rich item-instance/stack system beyond repeated `ItemData` anchors

Already-added useful API in `PlayerInventory.cs`:
- `public event Action InventoryChanged;`
- `public bool ContainsItem(ItemData item)`
- `public int CountAnchors(ItemData item)`

The UI layer must subscribe to `InventoryChanged` instead of polling.

### PDA shell
- `Assets/_Project/Scripts/PlayerPDA.cs`

Already-added useful API:
- `public GameObject PanelRoot => pdaPanel;`
- `public void ConfigureUI(GameObject panelRoot, CanvasGroup panelCanvasGroup, GameObject[] configuredTabs)`

Behavior already intended:
- `OnInventory` opens PDA directly to inventory tab if closed
- If already open, `OnInventory` switches to the inventory tab

### Tool / quick-slot backbone
- `Assets/_Project/Scripts/PlayerToolManager.cs`
- `Assets/_Project/Scripts/PlayerTool.cs`

Important facts:
- `PlayerToolManager` is the existing 4-slot backbone
- Do not replace it with a different hotbar backend
- UI must reflect real slot availability and active tool state

Already-added useful API in `PlayerToolManager.cs`:
- `public event Action<int> ActiveSlotChanged;`
- `public int SlotCount => toolPrefabs != null ? toolPrefabs.Length : 0;`
- `public GameObject GetAssignedToolPrefab(int slotIndex)`
- `public bool IsToolAvailableInSlot(int slotIndex)`

### Input
- `Assets/_Project/Scripts/Input/InputManager.cs`
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`

Important facts:
- Existing input layer should remain authoritative
- Rebind UI already exists and has been lightly hardened
- Avoid hardcoding new input systems if the current one can be extended cleanly

### Current HUD canvas
- `Suit_HUD_Canvas`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

Important facts:
- This is the current working visible UI path
- PDA should integrate visually with this ecosystem
- Do not break or replace `HUD_V4_CanvasRoot`

---

## Current local modifications already present

These were already applied in the repo and should be assumed to exist:

### `PlayerInventory.cs`
- has `InventoryChanged` event
- now notifies on collect/remove/load
- has helper methods `ContainsItem` and `CountAnchors`

### `PlayerPDA.cs`
- exposes `PanelRoot`
- subscribes to `OnInventory`
- can be configured through `ConfigureUI(...)`

### `PlayerToolManager.cs`
- exposes `ActiveSlotChanged`
- can report slot count / assigned prefab / availability
- responds to inventory changes by holstering disappearing tools

### `PDAControlsRebindUI.cs`
- has null-safe subscribe/unsubscribe
- has a `Configure(...)` helper

These modifications must be respected and used, not reimplemented differently.

---

## What to build

Build a production-ready first pass of the following:

### 1. PDA root runtime
Create a robust PDA runtime coordinator that:
- binds together `PlayerPDA`, `PlayerInventory`, `PlayerToolManager`, and input
- owns references to the PDA panel UI
- updates tabs through events, not through per-frame string rebuilding
- gracefully handles missing references and editor/runtime differences

This can be one main coordinator plus small focused UI presenters.
Do not create one giant god-script.

### 2. Inventory tab
Create an Inventory tab that includes:
- grid view driven by `PlayerInventory` / `InventoryGrid`
- item slots
- item icon display
- count display when count > 1
- item selection / highlight
- item info panel

Even if the backend is still simple, the UI architecture must be ready for future extension:
- item description
- category
- stack amount
- equipment usage
- future drag/drop or transfer

### 3. Equipment / tools view
Create an Equipment/Tools tab or section showing:
- 4 quick slots from `PlayerToolManager`
- current active slot
- whether each slot is actually available
- assigned tool name/icon if known

This must visually connect to gameplay and avoid fake states.

### 4. Controls / rebinding tab
Integrate the existing `PDAControlsRebindUI`
- use the existing script instead of rebuilding rebinding from scratch
- make it feel part of the same PDA system

### 5. Quick bar / hotbar HUD
Create a compact quick-access bar that is visible in normal gameplay when PDA is closed:
- linked to the real tool slots
- shows active slot
- uses the same visual language as the PDA
- low visual clutter
- readable on water/sky/terrain backgrounds

---

## Visual direction

This must NOT look like default Unity UI.
It must feel premium and consistent with the current HUD.

Visual goals:
- cleaner than Subnautica, more technological and more “mission equipment”
- subtle cyan / aqua primary accents
- restrained amber only for warnings / low resources / critical states
- avoid purple, generic sci-fi neon overload, or chunky mobile-game panels
- typography should separate labels and numbers
- shapes should be precise, slightly beveled, technical, and purposeful
- use negative space and framing, not noisy decoration

UI structure guidance:
- keep the PDA panel modular
- use a strong information hierarchy
- avoid cluttering the center of the screen
- make quick bar minimal, not a chunky RPG belt

Font guidance:
- isolate fonts used by this PDA system
- do not globally replace fonts used elsewhere unless explicitly required
- support separate label font vs numeric font where useful

Icons:
- prefer existing project sprites under `Assets/_Project/Art/Sprites/`
- if icon mapping is incomplete, structure the code so icons can be assigned later without code rewrite

---

## Performance rules

This is mandatory:
- zero avoidable GC allocations in `Update`
- no `FindObjectOfType` in hot paths
- no rebuilding whole hierarchies every frame
- no string concatenation in per-frame hot paths if avoidable
- prefer event-driven refresh
- cache references
- keep UI changes incremental

If any UI must update per frame:
- use `TMP.SetText`
- update only the widgets whose state changed

Do not overengineer with jobs/burst for plain UI, but do keep gameplay-side logic lean.

---

## Integration constraints

Do not:
- break the current flat HUD
- re-enable the volumetric visor HUD path
- create a second inventory backend
- rewrite unrelated survival systems
- invent fake food/water data if the backend does not have it yet

Do:
- design so food/water/equipment can be added later cleanly
- expose inspector hooks for tuning where useful
- keep the system scene-safe and prefab-safe
- preserve current project architecture where reasonable

---

## Code architecture expectation

Preferred structure:
- one PDA runtime coordinator
- one inventory presenter
- one quickbar presenter
- optional small reusable slot view component(s)
- optional item-info presenter

Good decomposition example:
- `PdaRuntimeController`
- `PdaInventoryPresenter`
- `PdaQuickSlotPresenter`
- `PdaSlotView`
- `PdaItemDetailsPresenter`

These are example names only.
Choose names that fit the codebase.

---

## Deliverables

Produce:
1. A concrete architecture summary
2. The full C# implementation
3. Any prefab/runtime wiring assumptions
4. Notes about what is real now vs what is placeholder-ready-for-extension
5. A short verification checklist for Unity

If runtime-generated UI is used:
- keep it structured and inspector-friendly
- do not generate unreadable spaghetti hierarchy logic

If prefab-driven UI is used:
- explain exactly which prefab or scene objects must exist

---

## Verification checklist

Your implementation must be verifiable with the following:

1. Press `Tab` or `I`
- PDA opens to inventory tab

2. Press `Tab` or `I` again while open
- inventory tab stays authoritative or re-focuses correctly

3. Inventory changes in gameplay
- UI updates through events, no manual refresh required

4. Tool availability changes
- quick slots reflect it

5. Active tool changes
- quick bar highlight updates correctly

6. Controls tab opens
- rebinding UI is functional and does not null-ref

7. No red compile errors

8. No obvious GC-heavy hot loop introduced by the PDA UI

---

## Important implementation note

You are allowed to write a fairly large amount of code.
Favor a strong first-pass architecture over fake minimalism.
If you need to introduce multiple scripts, do so cleanly and coherently.

But:
- keep each script focused
- do not produce a 1500-line monolith unless absolutely unavoidable

If you can reuse existing scripts, do it.
If you must extend them, do so carefully and explicitly.

---

## Extra note about current console noise

Some current console warnings/errors come from MCP-for-Unity serialization of obsolete AudioSource properties and editor-only handle serialization:
- `minVolume is not supported anymore`
- `maxVolume is not supported anymore`
- `rolloffFactor is not supported anymore`
- `TransformHandle object is null`

These are tooling/inspection issues, not the core gameplay feature you are implementing.
Do not redesign the PDA system around them.

