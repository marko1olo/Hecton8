# Hecton Music Director

Status: `PENDING VERIFICATION`

## Runtime shape

Core files:

- `Assets/_Project/Scripts/Audio/HectonMusicClip.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicBiomeProfile.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirectorAnchor.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`

Generated assets:

- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_MainMenu.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Prologue.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Shallow.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Shelf.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Abyss.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Cave.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Thermal.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Base.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Combat.asset`
- `Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Fallback.asset`
- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`

Scene wiring already exists:

- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

State machine:

1. `Waiting`
2. `Playing`
3. `Override`

The director routes from scene + base interior + zone text tokens + biome matrix + AI tension. Beds crossfade. Stingers duck the bed instead of replacing it.

## Current authored behavior

Exploration routing:

- `Main Menu` -> `MusicProfile_MainMenu`
- `Prologue` -> `MusicProfile_Prologue`
- `Base Interior` -> `MusicProfile_Base`
- `Combat latch` -> `MusicProfile_Combat`
- `Thermal` token hit -> `MusicProfile_Thermal`
- `Cave` token hit -> `MusicProfile_Cave`
- depth tier `<= 3` -> `MusicProfile_Shallow`
- depth tier `<= 9` -> `MusicProfile_Shelf`
- deeper -> `MusicProfile_Abyss`
- no match -> `MusicProfile_Fallback`

Recent improvements now authored into the assets:

- per-clip loudness attenuation via `_volume`
- per-clip selection bias via `_weight`
- per-profile repeat horizon via `_longRepeatHorizon` / `_shortRepeatHorizon`
- cross-tension borrowing on `Shallow`, `Shelf`, `Abyss`
- stronger local ownership for `Cave` and especially `Thermal`
- calmer `Base` profile with longer pauses, no short cues, no bleed
- stinger cooldown support in runtime
- combat exit support: recovery stinger + one forced calm follow-up bed
- dynamic depth-edge blending between `Shallow <-> Shelf <-> Abyss`
- optional telemetry from the director via `_enableTelemetry`

## Authoring notes

`Shallow`, `Shelf`, `Abyss`:

- calm and tense are not treated as strict separate worlds
- runtime may borrow from the opposite tension pool
- this was intentional because the library tone overlaps heavily
- exploration cadence was shifted toward calmer play: longer pauses, fewer short cues, longer short-cue cooldowns
- calm exploration pools are now weighted toward quieter nouns like `silence`, `reverbfall`, `glow`, `hum`, `vinyl`
- harsher nouns like `alarm`, `danger`, `pressure`, `hiss`, `clatter` are de-emphasized in calm exploration pools when possible

`Cave`:

- local pool remains primary
- limited bleed from `Abyss` / `Fallback`
- should keep a distinct enclosed tone instead of dissolving into open-water music

`Thermal`:

- local pool weight increased hard
- abyss and cave bleed were reduced
- goal is to stop thermal from collapsing into generic abyss too often

`Base`:

- long pauses
- no short cues
- no calm bleed
- should read as shelter, not as another exploration biome

## Loudness pass

The generator now runs an offline attenuation pass from `ffmpeg volumedetect` and writes `_volume` into cue entries.

Important limitation:

- current pass only attenuates louder tracks
- it does not boost quieter tracks
- this is deliberate because runtime cue volume is currently used as a safe linear multiplier

Practical result:

- obvious loud outliers are pulled down
- truly quiet tracks still need ear-based manual review if they feel buried

Generator:

- `Temp/GenerateMusicDirectorAssets.ps1`

## Telemetry

`HectonMusicDirector` has `_enableTelemetry`.

When enabled in `UNITY_EDITOR` or `DEVELOPMENT_BUILD`, it logs:

- context changes
- cue selections
- stinger plays
- wait transitions
- override start/clear

This is for playtest tuning only. It is not proof that the mix is good.

## Verification status

Verified by asset readback:

- `_volume` values are now non-uniform across the generated profiles
- `_weight` values are now non-uniform inside exploration pools, so selection is no longer flat-random
- `Base`, `Cave`, `Thermal` shaping values are written into assets
- `Shallow`, `Shelf`, `Abyss` repeat horizons and cross-tension settings are written into assets

Not verified:

- 20-30 minute real listening pass across all gameplay transitions
- final loudness balancing by ear
- final cave / thermal feel in a clean runtime session

System status remains `PENDING VERIFICATION`.
