# RECON_AI_ENCOUNTER_DIRECTOR

Scope: `rg -n "Instantiate\s*\(" Assets -g "*AI*.cs" -g "*Ai*.cs" -g "*Spawner*.cs" -g "*Spawn*.cs" -g "*Fauna*.cs" -g "*Encounter*.cs"`

Findings:
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:7756` instantiates a scatter prefab. This is a world scatter director path, not the headless encounter director path.
- `Assets/Candice AI for Games/Scripts/EnemySpawners.cs:44` instantiates `enemyPrefab` under `parentLayer`.
- `Assets/Candice AI for Games/Scripts/EnemySpawners.cs:45` instantiates `SpawnFx`.
- `Assets/Candice AI for Games/Scripts/EnemySpawners.cs:50` instantiates `enemyPrefab` without explicit parent.
- `Assets/Candice AI for Games/Scripts/EnemySpawners.cs:51` instantiates `SpawnFx`.
- `Assets/Feel/MMTools/Tools/MMInstantiation/MMSpawnAroundTester.cs:53` instantiates `ObjectToInstantiate`.
- `Assets/Plugins/DarkTonic/MasterAudio/ExampleScenes/Scripts/MA_EnemySpawner.cs:37` instantiates `Enemy`.

Assessment:
- No `Instantiate(` call was found in `Assets/_Project/Scripts/EncounterDirector.cs` or `Assets/_Project/Scripts/HectonDirectorAI.cs`.
- Offenders are world scatter or third-party/example spawner surfaces, not the headless encounter director path.
