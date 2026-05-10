Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Narrative / Progression Workstream

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Chto zakryvaet etot front

- Quest content
- Audio logs
- Suit upgrades
- Narrative discovery
- First-hour progression
- Live lore system integration

## Pochemu eto kritichno

Seychas zdes glavnyy razryv mezhdu "mnogo koda" i "est igra".  
Code owners suschestvuyut. Production content po klyuchevym data roots pochti pustoy.

## Owner files

- `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`
- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/NarrativeEvents.cs`
- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestData.cs`
- `Assets/_Project/Scripts/Quest/QuestEvents.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogData.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs`
- `Assets/_Project/Scripts/Gameplay/SuitHUDProfile.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs`
- `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Gameplay/EndingSystem.cs`

## Data roots

- `Assets/_Project/Data/Lore/Registries`
- `Assets/_Project/Data/Lore/DepthZones`
- `Assets/_Project/Data/Lore/Quests`
- `Assets/_Project/Data/Lore/AudioLogs`
- `Assets/_Project/Data/Lore/SuitUpgrades`

Fakt:

- `Quests` pusto.
- `AudioLogs` pusto.
- `SuitUpgrades` pusto.

## Osnovnye zadachi

### Front A. Narrative data authoring

- Zapolnit discovery IDs i narrative links.
- Privyazat registries k realnym depth beats.
- Zafiksirovat story spine pervogo chasa.

### Front B. Quest system fill-in

- Sozdat realnye quest assets.
- Opredelit trigger sources.
- Proverit aktivatsiyu kvestov ot suschestvuyuschih world/narrative events.

### Front C. Audio log fill-in

- Sozdat audio log assets.
- Privyazat pickup flow.
- Proverit discovery i PDA display.

### Front D. Suit progression

- Sozdat assets uluchsheniy.
- Privyazat unlock conditions.
- Proverit vizualnuyu podachu cherez HUD.

### Front E. Scene/bootstrap integration

- Proverit zhivoe nalichie `LoreSystems` v `02_HECTON_WORLD`.
- Garantirovat, chto kornevoy lore owner realno podnimaetsya v production path.
- Proverit, chto sistemy ne suschestvuyut tolko na bumage.

## Do-Not-Touch Scope

- Ne lezt v menu/pause UI.
- Ne trogat save/load shell.
- Ne perepisyvat world streaming/scatter.
- Ne smeshivat content authoring s performance work.

## Kak drobit po agentam

Agent 1:
- `HectonNarrativeDirector.cs`
- `NarrativeDiscovery.cs`
- `NarrativeEvents.cs`
- `Registries`, `DepthZones`
- Zadacha: narrative spine i discovery layer.

Agent 2:
- `QuestManager.cs`
- `QuestData.cs`
- `QuestEvents.cs`
- `Data/Lore/Quests`
- Zadacha: quest content i activation.

Agent 3:
- `AudioLogSystem.cs`
- `AudioLogData.cs`
- `AudioLogPickup.cs`
- `PDADataLogTab.cs`
- `Data/Lore/AudioLogs`
- Zadacha: audio logs i PDA flow.

Agent 4:
- `SuitUpgradeManager.cs`
- `SuitUpgradeData.cs`
- `SuitHUDProfile.cs`
- `SuitHUDPresentationController.cs`
- `Data/Lore/SuitUpgrades`
- Zadacha: suit progression.

Agent 5:
- `HectonLoreSystemsRoot.cs`
- scene wiring / validation tooling
- Zadacha: live bootstrap integration.

## Expected Result

- Narrative/progression perestaet byt pustym karkasom.
- Pervyy chas igry poluchaet realnyy content spine.
- Quest/log/upgrade bloki suschestvuyut ne tolko v kode.

## Exit Criteria

- Data roots bolshe ne pustye.
- V production world path realno zhivut lore systems.
- Igrok mozhet proyti hotya by odin svyaznyy narrative/progression marshrut.
