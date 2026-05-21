# HECTON-8 — Lore Systems Integration Guide

## Chto realizovano

### 1. AudioLog System (`Assets/_Project/Scripts/AudioLog/`)
**Audiodnevniki kolonii.**

- `AudioLogData` — ScriptableObject: dannye odnogo dnevnika (klip, subtitry, avtor, kategoriya)
- `AudioLogSystem` — singleton: vosproizvedenie, arhiv, ISaveable
- `AudioLogPickup` — IInteractable: obekt v mire, vosproizvodit log pri vzaimodeystvii
- `AudioLogEvents` — NativeQueue-backed event lane

**Kak ispolzovat:**
1. Create → Hecton8/Narrative/Audio Log Data → zapolnit polya
2. Dobavit `AudioLogPickup` na GameObject v stsene
3. Naznachit `AudioLogData` v inspektore
4. `AudioLogSystem` sozdaetsya cherez `HectonLoreSystemsRoot`

---

### 2. Quest System (`Assets/_Project/Scripts/Quest/`)
**Narrativnye kvesty cherez sobytiya mira.**

- `QuestData` — ScriptableObject: kvest s triggerom i usloviem zaversheniya
- `QuestManager` — singleton: slushaet sobytiya, ISaveable
- `QuestEvents` — NativeQueue-backed event lane

**Tipy triggerov:** OnItemCollected, OnDepthReached, OnBiomeEntered, OnDiscoveryMade, OnAudioLogFound, OnEclipseStart, OnSignalDetected, Manual

**Kak ispolzovat:**
1. Create → Hecton8/Quest/Quest Data → zapolnit triggerType, triggerId
2. Dobavit `QuestData` v massiv `QuestManager.allQuests`
3. Kvest aktiviruetsya avtomaticheski pri sobytii

---

### 3. Atlas Signal System (`Assets/_Project/Scripts/AtlasSignal/`)
**Puls signala Atlas-6 (ritm 11:23).**

- `AtlasSignalSystem` — singleton: puls kazhdye 683s, sila po rasstoyaniyu
- `AtlasSignalDecoder` — 4-faznaya rasshifrovka po sile signala
- `Atlas6DirectiveSystem` — status igroka s tochki zreniya Atlas-6
- `AtlasSignalEvents` — NativeQueue-backed event lane
- `Atlas6Events` — NativeQueue-backed event lane

**Shader globals:** `_AtlasSignalStrength`

**Kak ispolzovat:**
1. `AtlasSignalSystem` — naznachit `atlasCorePosWorld` (pozitsiya yadra na -5000m)
2. `AtlasSignalDecoder` — avtomaticheski rasshifrovyvaet pri priblizhenii
3. `Atlas6DirectiveSystem` — otslezhivaet status igroka

---

### 4. Suit Upgrade System (`Assets/_Project/Scripts/Gameplay/`)
**Apgreydy skafandra Tier 0-4.**

- `SuitUpgradeData` — ScriptableObject: delty parametrov, trebovaniya
- `SuitUpgradeManager` — singleton: primenyaet apgreydy, ISaveable
- `RuntimeSurvivalStats` — mutable wrapper nad SurvivalStats

**Tiry iz lora:**
- Tier 0: do -150m, O2 4 min (startovyy)
- Tier 1: do -500m, O2 8 min (pervyy kraft)
- Tier 2: do -1500m, O2 15 min
- Tier 3: do -3500m, O2 25 min
- Tier 4: do -5000m, O2 45 min

**Kak ispolzovat:**
1. Create → Hecton8/Gameplay/Suit Upgrade Data → zapolnit tier, deltaSafeDepth, deltaMaxOxygen
2. Dobavit v massiv `SuitUpgradeManager.allUpgrades`
3. `SuitUpgradeManager.InstallUpgrade(data)` — ustanovit apgreyd

---

### 5. Depth Zone System (`Assets/_Project/Scripts/World/`)
**Vertikalnaya stratifikatsiya mira.**

- `DepthZoneProfile` — ScriptableObject: zona s glubinoy, atmosferoy, trebovaniyami
- `DepthZoneDirector` — singleton: otslezhivaet zonu igroka, hull warnings
- `DepthZoneEvents` — NativeQueue-backed event lane

**Zony iz lora:**
- THE SPINE: 0-100m (startovaya)
- THE DROWNED FACTORIES: 100-1500m
- THE DROP: 1000-5000m
- Podzony: 0-150m, 150-500m, 500-1000m, 1000-1200m, 1200-2500m, 2500-4000m, 4000-5000m

**Kak ispolzovat:**
1. Create → Hecton8/World/Depth Zone Profile → zapolnit minDepth, maxDepth, requiredHullTier
2. Dobavit v massiv `DepthZoneDirector.zones`

---

### 6. Eclipse Gameplay System (`Assets/_Project/Scripts/Gameplay/`)
**Geympleynye posledstviya Velikogo Zatmeniya.**

- `EclipseGameplaySystem` — singleton: temperatura -8°C/min, nochnye hischniki cherez 60s
- `EclipseGameplayEvents` — NativeQueue-backed event lane

**Shader globals:** `_EclipseBiolumMultiplier`

**Avtomaticheski:** slushaet `HectonCelestialEngine.OnEclipseStart/End`

---

### 7. Spectrum System (`Assets/_Project/Scripts/Visor/`)
**Rezhimy vizora Hecton-OS.**

- `SpectrumSystem` — singleton: Normal/Thermal/Sonar/Echolocation
- `PDASpectrumTab` — UI vkladka v PDA (indeks 5)
- `SpectrumEvents` — NativeQueue-backed event lane

**Shader globals:** `_SpectrumMode`, `_SonarRadius`, `_SonarPulseTime`

**Kak ispolzovat:**
- `SpectrumSystem.Instance.SetMode(SpectrumMode.Thermal)` — pereklyuchit rezhim
- `SpectrumSystem.Instance.CycleMode()` — tsiklicheskoe pereklyuchenie

---

### 8. Biolum Controller (`Assets/_Project/Scripts/World/`)
**Globalnaya biolyuminestsentsiya.**

- `HectonBiolumController` — singleton: reagiruet na glubinu, zatmenie, signal Atlas-6

**Shader globals:** none. Runtime biolum shader globals are owned by `BiolumPulseSyncRuntime`.

---

### 9. Narrative Systems (`Assets/_Project/Scripts/Narrative/`)
**Lornye dannye.**

- `ColonistLoreRegistry` — SO: vse lornye obekty kolonii (Chen_M, kapitan, biolog...)
- `FaunaLoreRegistry` — SO: vse suschestva (11 tipov iz lora)
- `DeepReachCorporationData` — SO: korporatsiya, fraktsii, izotopy, prikazy
- `CorporateOrderSystem` — singleton: protivorechivye prikazy s zaderzhkoy 8-12ch

**Kak ispolzovat:**
1. Create → Hecton8/Narrative/Colonist Lore Registry → uzhe predzapolnen
2. Create → Hecton8/Narrative/Fauna Lore Registry → uzhe predzapolnen
3. Create → Hecton8/Narrative/Deep Reach Corporation Data → uzhe predzapolnen
4. `CorporateOrderSystem` — naznachit `corporationData` v inspektore

---

### 10. Random Event System (`Assets/_Project/Scripts/Gameplay/`)
**Sluchaynye sobytiya mira.**

- `RandomEventSystem` — singleton: 5 tipov sobytiy s usloviyami po glubine

**Sobytiya:** BiolumStorm (>1000m), ThermalEruption (>3000m), FaunaMigration (lyubaya), HectonOSGlitch (>500m), CaveCollapse (>200m)

**Shader globals:** `_BiolumStormActive`, `_HUDGlitchActive`

---

### 11. First Hour Director (`Assets/_Project/Scripts/Gameplay/`)
**Rezhissura pervogo chasa.**

- `FirstHourDirector` — singleton: 6 milestone, ISaveable

**Milestone:** Orientation (5min), FirstAnxiety (15min), FirstCraft (25-40min), TheShadow (40min), FirstModule (70min), HumCloser (90min)

---

### 12. Soundscape System (`Assets/_Project/Scripts/World/`)
**Zvukovye tiry po glubine.**

- `SoundscapeSystem` — singleton: 7 tirov (Surface→Thermal)
- `SoundscapeEvents` — NativeQueue-backed event lane

**Shader globals:** `_SoundscapeDepthTier`

**Kak ispolzovat:** Podpisatsya na `SoundscapeEvents.OnTierChanged` v AudioManager

---

### 13. Ending System (`Assets/_Project/Scripts/Gameplay/`)
**Tri kontsovki igry.**

- `EndingSystem` — singleton: usloviya aktivatsii, vybor kontsovki, ISaveable
- `EndingTerminalInteractable` — IInteractable: terminal u yadra Atlas-6
- `EndingEvents` — NativeQueue-backed event lane

**Kontsovki:** ShutDown (vyklyuchit), Leave (ostavit), Amplify (usilit signal)

**Kak ispolzovat:**
1. Razmestit `EndingTerminalInteractable` u yadra Atlas-6 na -5000m
2. `EndingSystem.Instance.ChooseEnding(EndingChoice.Amplify)` — iz UI

---

### 14. PDA Data Log Tab (`Assets/_Project/Scripts/UI/`)
**Arhiv audiodnevnikov v PDA.**

- `PDADataLogTab` — vkladka 4 v PDA
- Avtomaticheski dobavlyaetsya cherez `PlayerPDA.AutoResolveTabs`

**Kak ispolzovat:**
1. Naznachit `AudioLogData[]` v `PDADataLogTab.allLogs`
2. Vkladka otobrazhaet obnaruzhennye zapisi, pozvolyaet pereslushat

---

## Kak dobavit v stsenu

### Shag 1: Sozdat LoreSystems GameObject
```
Hierarchy → Create Empty → nazvat "LoreSystems"
Dobavit komponent: HectonLoreSystemsRoot
Nazhat [Setup All Systems] v inspektore
```

### Shag 2: Sozdat ScriptableObject assety
```
Assets/_Project/Data/Lore/ → sozdat papku
Sozdat: ColonistLoreRegistry, FaunaLoreRegistry, DeepReachCorporationData
Sozdat: DepthZoneProfile × 7 (po zonam iz lora)
Sozdat: SuitUpgradeData × 5 (Tier 0-4)
Sozdat: QuestData × N (kvesty iz lora)
Sozdat: AudioLogData × N (dnevniki kolonii)
```

### Shag 3: Naznachit ssylki
```
AtlasSignalSystem → atlasCorePosWorld = (0, -5000, 0)
DepthZoneDirector → zones[] = vse DepthZoneProfile
SuitUpgradeManager → baseStats, allUpgrades[]
QuestManager → allQuests[]
CorporateOrderSystem → corporationData
PDADataLogTab → allLogs[]
```

### Shag 4: Razmestit obekty v mire
```
AudioLogPickup × N → v modulyah kolonii
EndingTerminalInteractable → u yadra Atlas-6 (-5000m)
NarrativeDiscovery × N → lornye obekty (KPK, shemy, skafandry)
```

---

## SaveData versiya: 16
Vse sistemy sohranyayut sostoyanie cherez ISaveable.
