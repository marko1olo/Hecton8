Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Shell / UI Workstream

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Chto zakryvaet etot front

- Main Menu
- Pause Menu
- Settings shell
- Input rebinding UI
- Option persistence
- Save/load UX

## Pochemu eto odin iz glavnyh frontov

Seychas shell suschestvuet, no vyglyadit kak production foundation, a ne kak zakonchennyy polzovatelskiy sloy.  
Otdelnogo owner'a dlya obschego persistence nastroek ne vidno. Est tolko fragmenty.

## Owner files

- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/PauseMenuHost.cs`
- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`
- `Assets/_Project/Scripts/Input/RebindingManager.cs`
- `Assets/_Project/Scripts/Input/InputManager.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`

## Osnovnye zadachi

### Front A. Main menu flow

- Dobit `MainMenuController`.
- Ubrat pustye ili tupikovye sostoyaniya.
- Dovesti load/new game flow do odnogo ponyatnogo stsenariya.
- Proverit vozvraty, cancel-paths, fokus i default selection.

### Front B. Pause shell

- Dovesti `PauseMenuController` i `PauseMenuHost`.
- Proverit sektsii `Main / Saves / Help / Settings`.
- Ispravit default focus i vozvrat iz sektsiy.
- Proverit path `pause -> save/load -> return`.

### Front C. Rebinding UX

- Dobit `PauseControlsPanel`.
- Dobit `PDAControlsRebindUI`.
- Proverit reset/save/apply/cancel.
- Proverit, chto stroki rebinding ne razvalivayutsya pri pustyh ili missing bindings.

### Front D. Options persistence

- Vynesti otdelnogo owner'a dlya nastroek, esli ego realno net.
- Sohranenie ne tolko input overrides, no i user options.
- Zafiksirovat contract: kakie nastroyki zhivut, gde hranyatsya, kto ih chitaet.

### Front E. Save/load user trust

- Proverit soobscheniya ob oshibkah.
- Proverit povedenie pri bitom seyve ili pustom slote.
- Proverit soglasovannost s `SaveManager`.

## Do-Not-Touch Scope

- Ne lezt v narrative systems.
- Ne lezt v world bootstrap.
- Ne pravit progression data.
- Ne menyat save backend contract bez otdelnogo analiza zavisimostey.

## Kak drobit po agentam

Agent 1:
- `MainMenuController.cs`
- `SaveSlotUI.cs`
- Zadacha: menu flow i save/load UX.

Agent 2:
- `PauseMenuController.cs`
- `PauseMenuHost.cs`
- Zadacha: pause shell i section flow.

Agent 3:
- `PauseControlsPanel.cs`
- Zadacha: rebinding UI v pause.

Agent 4:
- `PDAControlsRebindUI.cs`
- Zadacha: rebinding UI v PDA.

Agent 5:
- novyy owner pod option persistence
- minimalnye tochki vhoda v existing UI
- Zadacha: obschiy persistence sloy nastroek.

## Expected Result

- Main menu ne vedet v tupiki.
- Pause stabilen.
- Rebinding ne vyglyadit kak poluzaglushka.
- Nastroyki realno sohranyayutsya.
- Polzovatelskiy shell perestaet byt weak point.

## Exit Criteria

- Net pustyh panel states.
- Vse back/cancel paths zakryty.
- Input overrides sohranyayutsya i gruzyatsya.
- Est edinyy owner nastroek.
- Proveren bazovyy stsenariy: main menu -> world -> pause -> settings -> save/load -> return.
