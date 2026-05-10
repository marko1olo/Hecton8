Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Agent Assignment Matrix

Data: 2026-04-13  
Status: PENDING VERIFICATION

Eto ne obschiy plan. Eto pryamoy list razdachi zadach agentam.

## Volna 1

### Agent 1 — Main Menu / Save-Load Flow

Owner files:

- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`

Zadacha:

- Dobit main menu flow.
- Ubrat pustye i tupikovye panel states.
- Privesti `new game / load game / back / cancel` k odnomu ponyatnomu stsenariyu.
- Proverit default selection i vozvraty.

Ne trogat:

- `PauseMenuController.cs`
- input rebinding
- narrative systems
- save backend contract v `SaveManager`

Rezultat:

- Main menu perestaet byt poluzaglushkoy.
- Save/load path vyglyadit kak production shell.

Kriteriy gotovnosti:

- Net tupikovyh sostoyaniy.
- Vse back-paths zakryty.
- Polzovatel mozhet stabilno proyti menu -> load/new game -> world.

### Agent 2 — Pause Shell

Owner files:

- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/PauseMenuHost.cs`

Zadacha:

- Dovesti pause menu.
- Proverit sektsii `Main / Saves / Help / Settings`.
- Ispravit selection defaults i vozvraty.
- Proverit pause resume path i perehod nazad v main menu.

Ne trogat:

- `MainMenuController.cs`
- quest / lore systems
- world bootstrap

Rezultat:

- Pause perestaet byt hrupkim shell-sloem.

Kriteriy gotovnosti:

- Net razvalennyh perehodov.
- Net pustyh section routes.
- Proveren stsenariy pause -> settings/save -> resume.

### Agent 3 — Pause Rebinding UI

Owner files:

- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`

Zadacha:

- Dovesti rebinding UI v pause.
- Proverit reset/apply/save/cancel.
- Proverit povedenie pri missing binding rows.
- Privesti statusy i tekst k vnyatnomu vidu.

Ne trogat:

- `PDAControlsRebindUI.cs`
- `MainMenuController.cs`
- general options persistence owner

Rezultat:

- Rebinding v pause rabotaet kak otdelnyy zakonchennyy sloy.

Kriteriy gotovnosti:

- Rows korrektno stroyatsya.
- Overrides sohranyayutsya.
- Oshibki i pustye bindings ne lomayut UI.

### Agent 4 — PDA Rebinding UI

Owner files:

- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`

Zadacha:

- Dovesti rebinding UI v PDA.
- Proverit tab switching, row resolution, reset/save flow.
- Proverit consistency s `RebindingManager`.

Ne trogat:

- `PauseControlsPanel.cs`
- `MainMenuController.cs`
- lore / quest files

Rezultat:

- PDA controls panel ne vyglyadit nedodelannym dublikatom.

Kriteriy gotovnosti:

- PDA rebinding path stabilen.
- Overrides chitayutsya i sohranyayutsya bez rassinhrona.

### Agent 5 — Options Persistence Owner

Owner files:

- novyy owner pod user options
- minimalnye tochki vhoda v menu/pause UI
- `Assets/_Project/Scripts/Input/RebindingManager.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`

Zadacha:

- Sozdat edinyy persistence sloy dlya ne-input nastroek.
- Zafiksirovat contract hraneniya optsiy.
- Podklyuchit menu/pause k etomu owner'u bez raspolzaniya logiki.

Ne trogat:

- main menu layout
- pause shell layout
- world systems

Rezultat:

- V proekte poyavlyaetsya edinyy vladelets polzovatelskih nastroek.

Kriteriy gotovnosti:

- Nastroyki sohranyayutsya mezhdu sessiyami.
- Est yavnyy owner vmesto razroznennyh `PlayerPrefs` ostrovkov.

## Volna 2

### Agent 6 — Narrative Spine

Owner files:

- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/NarrativeEvents.cs`
- `Assets/_Project/Data/Lore/Registries`
- `Assets/_Project/Data/Lore/DepthZones`

Zadacha:

- Sobrat narrative spine pervogo chasa.
- Zapolnit discovery layer.
- Privyazat depth beats k registries i sobytiyam.

Ne trogat:

- quest assets
- audio logs
- suit upgrades
- menu/pause

Rezultat:

- Poyavlyaetsya osmyslennyy narrative backbone vmesto abstraktnogo lora.

Kriteriy gotovnosti:

- Est minimum odin svyaznyy narrative route.
- Discovery IDs i progression links ne pustye i ne visyat v vozduhe.

### Agent 7 — Quest Content

Owner files:

- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestData.cs`
- `Assets/_Project/Scripts/Quest/QuestEvents.cs`
- `Assets/_Project/Data/Lore/Quests`

Zadacha:

- Sozdat realnye quest assets.
- Opredelit trigger points.
- Proverit aktivatsiyu ot suschestvuyuschih sobytiy.

Ne trogat:

- audio logs
- suit upgrades
- world cleanup

Rezultat:

- Quest system vyhodit iz sostoyaniya pustoy infrastruktury.

Kriteriy gotovnosti:

- `Data/Lore/Quests` bolshe ne pustoy.
- Est hotya by odin rabochiy kvestovyy marshrut.

### Agent 8 — Audio Logs

Owner files:

- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogData.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
- `Assets/_Project/Data/Lore/AudioLogs`

Zadacha:

- Sozdat audio log assets.
- Privyazat pickup flow.
- Proverit discovery i PDA presentation.

Ne trogat:

- quest logic
- suit upgrades
- menu/pause

Rezultat:

- Audio log system nachinaet suschestvovat kak kontent, a ne tolko kak kod.

Kriteriy gotovnosti:

- `Data/Lore/AudioLogs` ne pustoy.
- Igrok mozhet podobrat log i uvidet/proigrat ego cherez PDA.

### Agent 9 — Suit Progression

Owner files:

- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs`
- `Assets/_Project/Scripts/Gameplay/SuitHUDProfile.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Data/Lore/SuitUpgrades`

Zadacha:

- Sozdat data-driven suit upgrades.
- Privyazat unlock conditions.
- Proverit otrazhenie sostoyaniya v HUD.

Ne trogat:

- quests
- audio logs
- pause/menu

Rezultat:

- U progressii poyavlyaetsya osyazaemyy sloy uluchsheniy.

Kriteriy gotovnosti:

- `Data/Lore/SuitUpgrades` ne pustoy.
- Upgrade path realno vliyaet na sostoyanie igroka i HUD.

### Agent 10 — Lore Bootstrap Integration

Owner files:

- `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`
- `Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs`
- `Assets/_Project/Scripts/Editor/HectonLoreSystemsRootEditor.cs`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Zadacha:

- Garantirovat nalichie live `LoreSystems` root v production world path.
- Proverit, chto lore systems realno podnimayutsya v stsene.
- Ne dopustit sostoyaniya "kod est, v live-mire ne zhivet".

Ne trogat:

- content authoring
- shell/menu
- world density

Rezultat:

- Narrative stack perestaet byt prizrakom v kode.

Kriteriy gotovnosti:

- V `02_HECTON_WORLD` podtverzhden live root.
- Sistemy realno instantsiruyutsya v production path.

## Volna 3

### Agent 11 — Production World Cleanup

Owner files:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Scripts/SceneBootstrap.cs`
- world bootstrap owners

Zadacha:

- Zachistit production path ot `temp / trial / staging / smoke`.
- Otdelit debug-only route ot shipping route.
- Zafiksirovat truth hierarchy.

Ne trogat:

- quests/audio logs
- main menu/pause
- save backend

Rezultat:

- Production world perestaet byt aktivnoy masterskoy.

Kriteriy gotovnosti:

- V live route net vremennogo musora.
- Debug path i shipping path otdeleny.

### Agent 12 — World Density / Biomes

Owner files:

- `Assets/_Project/Scripts/World/WorldContentDirector.cs`
- `Assets/_Project/Scripts/World/WorldPopulationDirector.cs`
- `Assets/_Project/Scripts/World/BiomeMatrixDirector.cs`

Zadacha:

- Usilit world density.
- Dobit biomnuyu differentsiatsiyu.
- Dobavit smysl mezhdu hero-tochkami.

Ne trogat:

- shell/UI
- lore bootstrap
- save backend

Rezultat:

- Mir perestaet derzhatsya tolko na backbone i procedural mass.

Kriteriy gotovnosti:

- Est chitaemye razlichiya po biomam i sloyam mira.
- Mezhdu krupnymi tochkami poyavilis meaningful fillers.

### Agent 13 — Caves / Geology Gameplay

Owner files:

- `Assets/_Project/Scripts/World/WorldCaveDirector.cs`
- geology integration owners

Zadacha:

- Dovesti caves do urovnya marshrutov, a ne tolko generatsii.
- Proverit rewards, landmarks, shortcuts, fear/visibility curve.

Ne trogat:

- menu/pause
- quests
- general perf pass

Rezultat:

- Peschery stanovyatsya igrovym kontentom, a ne prosto geometriey.

Kriteriy gotovnosti:

- Est hotya by odin polnotsennyy cave route s payoff.

### Agent 14 — Base Loop / Return Value

Owner files:

- support/crafting/building/power/inventory owners
- survival path owners

Zadacha:

- Zafiksirovat, zachem igrok vozvraschaetsya.
- Skleit crafting, storage, power, oxygen, upgrade loop.
- Proverit continuity posle save/load.

Ne trogat:

- shell/UI
- narrative content
- world cleanup

Rezultat:

- Baza i support systems stanovyatsya oporoy tsikla, a ne dekoratsiey.

Kriteriy gotovnosti:

- Est rabochaya petlya `explore -> gather -> return -> recover/craft/upgrade -> go deeper`.

## Volna 4

### Agent 15 — Perf / Memory Truth

Owner files:

- perf-sensitive world owners
- profiling routines
- relevant docs/ledgers

Zadacha:

- Sobrat baseline po CPU, GC, VRAM, RT, batches, SetPass.
- Proverit streaming hitch i scatter cost.

Ne trogat:

- feature scope
- narrative authoring

Rezultat:

- U komandy poyavlyayutsya realnye tsifry, a ne oschuscheniya.

Kriteriy gotovnosti:

- Est baseline measurements i spisok red zones.

### Agent 16 — Critical Flow Tests / Build Discipline

Owner files:

- `Assets/_Project/Tests`
- critical path owners dlya shell/save/pause/core loop
- build issue docs

Zadacha:

- Podnyat minimalnyy smoke/test sloy po critical path.
- Zafiksirovat build cadence i issue discipline.

Ne trogat:

- world content authoring
- narrative content production

Rezultat:

- Regressii nachinayut lovitsya ranshe.

Kriteriy gotovnosti:

- Est smoke checklist.
- Est coverage na main menu, pause, save/load i odin core progression path.

## Zhestkie pravila vydachi

- Ne davat dvum agentam odin i tot zhe owner file.
- Ne sovmeschat scene integration i content authoring v odnom agente, esli mozhno razdelit.
- Ne puskat agentov odnovremenno v `02_HECTON_WORLD.unity`, esli zadachi ne razdeleny po ownership.
- Snachala zakryvat pustoty i integration gaps, potom polishing.
- Lyubuyu zadachu bez live proof schitat `PENDING VERIFICATION`.
