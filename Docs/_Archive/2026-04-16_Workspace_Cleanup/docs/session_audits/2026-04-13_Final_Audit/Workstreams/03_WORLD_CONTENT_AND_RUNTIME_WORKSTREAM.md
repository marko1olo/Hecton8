Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — World Content / Runtime Workstream

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Chto zakryvaet etot front

- Production world scene truth
- Cleanup vremennyh suschnostey
- World density
- Caves / ruins / ecology
- Runtime world ownership

## Pochemu eto kritichno

Seychas production-stsena neset sledy aktivnoy masterskoy: temp, trial, staging, smoke.  
Poka eto ne zachischeno, lyubaya otsenka gotovnosti mira zagryaznena.

## Live facts from current world scene

- Est `Fabrication_Trial`.
- Est `Tool_Staging`.
- Est `__TEMP_DENSE_KELP_PREVIEW`.
- Est `__PROCEDURAL_PROXY_WORLD`.
- Est `__PROCEDURAL_SCATTER_WORLD`.
- Na Player vidny smoke-test komponenty.

## Owner files and systems

- `Assets/_Project/Scripts/SceneBootstrap.cs`
- `Assets/_Project/Scripts/World/WorldStreamingDirector.cs`
- `Assets/_Project/Scripts/World/WorldSliceDirector.cs`
- `Assets/_Project/Scripts/World/WorldInterestDirector.cs`
- `Assets/_Project/Scripts/World/WorldZoneDirector.cs`
- `Assets/_Project/Scripts/World/WorldContentDirector.cs`
- `Assets/_Project/Scripts/World/WorldPopulationDirector.cs`
- `Assets/_Project/Scripts/World/BiomeMatrixDirector.cs`
- `Assets/_Project/Scripts/World/WorldProceduralFillDirector.cs`
- `Assets/_Project/Scripts/World/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/World/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/World/WorldCaveDirector.cs`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

## Osnovnye zadachi

### Front A. Production scene cleanup

- Otdelit debug/trial/staging ot shipping path.
- Ubrat musor iz live scene ili sdelat ego debug-only.
- Zafiksirovat truth hierarchy.

### Front B. World truth matrix

- Otmetit dlya kazhdogo krupnogo subsystem:
  - code exists
  - scene-wired
  - content-backed
  - runtime-verified
- Ne putat nalichie manager'a s gotovnostyu mira.

### Front C. World density

- Surface ecology.
- Mid-depth identity.
- Deep-zone identity.
- Ruins / colony remnants / industrial remains.
- Small set pieces mezhdu hero-tochkami.

### Front D. Caves / geology gameplay

- Ne tolko generatsiya, no i marshrut.
- Reward placement.
- Landmark readability.
- Shortcut logic.
- Visibility / pressure / fear curve.

### Front E. Procedural pipeline sanity

- Proverit, gde procedural stack pomogaet miru, a gde prosto naraschivaet massu.
- Zafiksirovat semeystva, gde nuzhny authored finals, a gde dostatochno runtime variation.

## Do-Not-Touch Scope

- Ne trogat shell/menu/pause.
- Ne trogat quest/audio log data.
- Ne pravit save/load backend.
- Ne ustraivat bolshoy arhitekturnyy refaktor world stack bez otdelnogo resheniya.

## Kak drobit po agentam

Agent 1:
- `02_HECTON_WORLD.unity`
- `SceneBootstrap.cs`
- world bootstrap owners
- Zadacha: cleanup production path i truth hierarchy.

Agent 2:
- `WorldContentDirector.cs`
- `WorldPopulationDirector.cs`
- `BiomeMatrixDirector.cs`
- Zadacha: world density i biomnoe napolnenie.

Agent 3:
- `WorldCaveDirector.cs`
- geology integration owners
- Zadacha: caves/geology payoff.

Agent 4:
- procedural fill/scatter owners
- Zadacha: sanity-check procedural contribution i content ownership.

## Expected Result

- Production world perestaet vyglyadet kak masterskaya.
- Mir stanovitsya chische i plotnee.
- Poyavlyaetsya realnoe razdelenie mezhdu debug path i shipping path.

## Exit Criteria

- Net temp/trial/staging musora v live route.
- Po krupnym world-sistemam est truth matrix.
- Est podtverzhdennye marshruty caves/ruins/ecology, a ne tolko sloy generatsii.
