Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Plan zadach C/HADES/HECTON8 (rabochiy katalog `C:\hades\Hecton8`)

> Eto osnovnoy trek zadach dlya proekta Submerge (HECTON-8) na baze tekuschego repozitoriya. 
> Lyuboe izmenenie zadach dolzhno fiksirovatsya v issue/PR kak «done» posle proverki na rabotosposobnost.

## 1. Strategiya i tseli proekta

- Sformirovat zamysel klyuchevogo vizualnogo stilya: Deep Sea Noir + nauchno-tehnicheskaya tematika + atmosfera zatonuvshey bazy.
- Obespechit modulnuyu sborku: grafika + effekty + stsenariy + UI + optimizatsiya pamyati na urovne 2GB VRAM.
- Osnovnaya svyazka middleware:
  - Crest (voda),
  - MapMagic (generatsiya landshafta),
  - MicroSplat (orbazing),
  - VLB (volny/dym),
  - GPU Instancer,
  - Odin Inspector,
  - Easy Save.
- Udelit vnimanie stabilnosti: testy eskapirovaniya vysoty, logika slezheniya za transformami, kontroller kamery, korrektnoe otklyuchenie obektov pri striminge World (Storm-breaker).

## 2. Blizhayshie iteratsii (minimalnyy MVP)

- Faza 1: Vizualnyy set Deep Sea Noir.
  - Finalizirovat tsvetovoy nabor, osveschenie, vesennie profili postprotsessa, fog/volumetric.
  - Prototipirovat glavnyy mir: morskoe dno, ruiny, tochechnye istochniki sveta, skripty perehodov.

- Faza 2: Igrovaya mehanika, baza i interfeysy.
  - Realizovat sistemu upravleniya personazhem + PDA + inventar.
  - Dobavit interaktivnost obektov, sbor resursov, kraft remesla.

- Faza 3: Protsedurnaya generatsiya urovnya.
  - Zadat setku abyss/nodes, algoritmy zapolneniya, povtory.

- Faza 4: Proizvoditelnost.
  - Nizkopoligonnaya LOD-ferma, kastomnyy culling, uproschennaya geometriya dlya dalnego vida.

- Faza 5: Podgotovka reliza i trebovaniya.
  - Trebovaniya: 30 FPS na MX350, ne bolee 2GB VRAM, priemlemye nagruzhennye stseny.

## 3. Tehnicheskaya podderzhka i kachestvo

- Optimizatsiya VRAM, URP Volumetric, s uchetom low/medium/high.
- Avtomaticheskie proverki sostoyaniya assetov (Crest/MapMagic). Sdelat workflow asset health check cherez PR.
- Otslezhivat poryadok vyzovov Update/FixedUpdate/LateUpdate i problemy GC.

## 3.1. Chetvertyy stolp iz README (2)
- Utochnit tochnye target hardware constraints:
  - Texture max 2048x2048 dlya sten/landshafta, 1024x1024/512x512 dlya propsov.
  - Ne bolee 2 GB VRAM.
  - Nikakoy tesellation, tolko Normal/Parallax.
  - Zero-GC v Update/FixedUpdate/LateUpdate.
  - Bez LINQ/FindObjectOfType/GetComponent v goryachih tsiklah.

## 3.2. Core Pillars (iz README)
- Tehnologicheskiy uyut, megalofobiya, tyazhelyy inzhiniring, maroderstvo.
- Proverit, chto kazhdaya zadacha sootvetstvuet etim printsipam.

## 4. Upravlenie zadachami status-driven

- [ ] Razvivat yadro feature:
  - Plavnoe naplyvanie (Aegir phases)
  - Tidal lock drift
  - Ves resursov/energiya
  - Additivnyy striming mira
  - Sistema barternogo PDA
  - Protsedurnaya set abyss nodes

- [ ] Tehnicheskiy dolg:
  - Avtomaticheskiy generator TODO v `task.md`.
  - Proverka CurrentVolume transform i korrektnogo primeneniya.

## 5. Protsess v rabote

1. Kazhdyy task oformlyaetsya v issue i razbivaetsya na podzadachi, zadachi vypolnyayutsya po ocheredi.
2. Komanda koderov soblyudaet standarty, vedet kommentarii i kody sostoyaniya.
3. Zakrytie zadachi po kriteriyu: na MX350 30 FPS pri 2GB VRAM, otsutstvie bagov, dokumentirovanie.

---

## 6. Sravnenie s README (2).md i nedostatki

- README soderzhit detalnyy manifest, tseli po CPU/GPU (MX350), arhitekturnye standarty i strukturu direktoriy, kotorye v osnovnom NE otrazheny v starom fayle zadach. Nado dobavit:
  - strozhayshie gaydlayny po papkam `Assets/Plugins`, `Assets/_Project`, `Assets/Scenes`, `Assets/Scripts`.
  - pravila raboty s Additive Scene Loading (00_BOOTSTRAP, 01_MAIN_MENU, 02_HECTON_WORLD, XX_SANDBOX).
  - Prefab-centric workflow (zapret na pravku stseny, ispolzovanie Prefab Mode/Variants).
  - data-driven nastroyki (SO vmesto hardkoda) i Git protokoly (LFS, .gitignore, konsol oshibok).
- README daet strukturu Asset Stack (Crest, MapMagic, MicroSplat, VLB, Odin, Easy Save, Candice, Feel). Nuzhno v taskah otdelno proverit eti integratsii:
  - ochistka demo-kontenta
  - fiksy (ForceIncludeInstancing, Sirenix PDB)
  - otklyuchenie tyazhelyh moduley (Tessellation/Parallax v MicroSplat, lishnie VLB ustarevshie metody).

> Eto fiks: `HADES_HECTON8_tasks.md` teper sinhronizirovan s klyuchevymi punktami iz `README (2).md`. Proverit i dopolnit ostalnye temy na sleduyuschem sprinte.

## 7. Kontsept geympleya (polnyy nabor idey)

### 7.1. Core Gameplay Loop
- [ ] Issledovanie: zona 15x15 km, sektsii: The Spine, Drowned Factories, Abyssal Face, The Wound.
- [ ] Sbor resursov: lom, ruda, organika, bioluministsentsiya.
- [ ] Obsluzhivanie oborudovaniya: tyuning batiskafa/sistem; szhiganie energii (PDA, fonari, nasosy).
- [ ] Vyzhivanie: upravlenie kislorodom, davleniem, temperaturoy, radiatsiey.
- [ ] Kraft i apgreyd: instrumenty, bronya, moduli bazy.
- [ ] Polnaya sistema resursov i krafta:
  - polnyy spisok syrya, biomaterialov, himii i promezhutochnyh komponentov
  - data-driven `ItemData` dlya vseh klyuchevyh resursov
  - polnotsennye retsepty: syre -> komponent -> instrument/apgreyd/modul
  - realnye world-sources: lom, rudnye uzly, biosbor, sealed caches
  - otkaz ot prostogo copper-only economy
  - opora na [RESOURCE_CRAFTING_FOUNDATION.md](C:/hades/Hecton8/RESOURCE_CRAFTING_FOUNDATION.md)
- [ ] Progress: sbora dannyh, vosstanovleniya II, zahvata novyh zon.
- [ ] Risk: hischniki, razgermetizatsiya, MCU (vzryvy), kollapsy.

### 7.2. Fizika i upravlenie (iz README)
- [ ] Realizovat plavuchuyu mehaniku dlya batiskafa + buoyancy (Crest + sobstvennyy kod). 
- [ ] Obrabotka vhoda: WASD, pryzhki, akseleratsiya; s uchetom inertsii vody.
- [ ] Reaktsiya na davlenie: parametry DepthExposure (0..1), modifikatory urona/shansov polomki.

### 7.3. Interfeys i PDA (Hecton-OS)
- [ ] HUD/AR stil: monoshirinnyy shrift, zhestkie ramki, staticheskie glitchi.
- [ ] Batareya/O2/Gidravlika/Temperatura -> vizualnye klastery.
- [ ] Exchange/barter system vnutri PDA.

### 7.4. Bazy i striming mira
- [ ] Additive Scene Loading: polnotsennyy Bootstrapping.
- [ ] Striming chankov: lody, culling, otklyuchenie komponentov vne zony vidimosti.
- [ ] Sistema ploschadok dlya postroyki: resursy, vragi, zaschita.

### 7.5. Neyroagaenty i AI
- [ ] Integratsiya Candice AI, povedenie dronov i mutantov.
- [ ] Logika schastya/agressii: reaktsiya na shum, osveschenie.
- [ ] Vozmozhnost terraformirovaniya v zone oshibok.

### 7.6. Kriticheski vazhnye nesdelannye elementy
- [ ] Vseh iz README 0.1-0.3 poka nado formalizovat v zadachah, razbit na subtask.
- [ ] Ne propisany check-listy po Asset-steku, vklyuchaya urezku demo i konfig v staryh plaginah.
- [ ] Net otdelnogo modulya dlya stseny Sandbox, on nuzhen dlya izolyatsii i testov.
- [ ] Tekuschiy fayl poka ne imeet svyazki s .kiro/specs/hecton8-enterprise-roadmap/tasks.md na detalnom urovne (trebuyutsya ssylki/perenos).
- [ ] Profayl-metriki (FPS, GC, drawcalls) ne zavedeny kak dostizhimaya tsel v taskah.

### 7.7. Polnyy roadmap realizatsii (chto realizovat)
- [ ] Initsializatsionnyy stek
  - [ ] Bootstrap + globalnye menedzhery GameManager, SaveSystem, AudioMixer, InputManager.
  - [ ] Additive Scene Loading: 00_BOOTSTRAP, 01_MAIN_MENU, 02_HECTON_WORLD, XX_SANDBOX.
  - [ ] Scene streaming: aktivnaya zagruzka/vygruzka zon, chekpointy.

- [ ] Core engine
  - [ ] Input System (Player + UI) + InputManager singleton + zero-GC callbacks.
  - [ ] Movement System: Rigidbody / CharacterController + voda/inertsiya.
  - [ ] PDA/UI System: bag-report, inventory, barter, sistema zadach.
  - [ ] Survival System: O2, temperatura, davlenie, radiatsiya, statusy.

- [ ] World generation
  - [ ] MapMagic integration + chunk streaming, biome masks.
  - [ ] Crest ocean/underwater renderer + buoyancy/wave intersection.
  - [ ] MicroSplat terrain shader + tris, LOD, bez tessellation.

- [ ] AI & entities
  - [ ] Candice behavior trees dlya mutantov, dronov, NPC.
  - [ ] State machine dlya ohrany, krazhi, begstva.
  - [ ] Pooling enemy/spawner system + navigation mesh updates.

- [ ] Interaction & loot
  - [ ] Sbor luta: udar, rezka, fizika dobychi.
  - [ ] Kraft i apgreydy: retsepty, materialy, UI.
  - [ ] Weight/encumbrance i svyaz s energozatratami.

- [ ] Performance & tech
  - [ ] VRAM budget checks, texture atlas + compression.
  - [ ] URP renderer features: VLB, volumetric fog, shadow quality toggles.
  - [ ] Profiling benchmarks: FPS, memory, GC, draw calls.
  - [ ] Job System + Burst dlya slozhnyh raschetov (shum, AI, fizicheskie setki).

- [ ] Tools & workflow
  - [ ] Prefab workflow (variants, nested prefabs, no scene edits).
  - [ ] Git/LFS rules, CI pipeline na kompilyatsiyu + utility.
  - [ ] Dokumentatsiya: architecture, coding standards, profiling reports.

> Po kazhdomu punktu sozdaem otdelnyy issue, veshaem na sprint, zakryvaem po proverke na MX350.

