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

The director routes from scene + base interior + zone text tokens + biome matrix + soundscape tier + AI tension. Beds crossfade. Stingers duck the bed instead of replacing it.

## Current integration contract

Status: `STATIC_SOURCE VERIFIED`, `PENDING UNITY LISTENING PASS`

Player-facing intent:

- the ocean bed is always allowed to breathe
- exploration music enters as phrases, then yields back to the world instead of becoming a permanent loop
- deeper soundscape tiers make rests longer and phrases tighter, so abyssal water feels less safe and less musical
- if the biome matrix has no explicit music profile yet, the soundscape tier still routes the fallback profile family: shallow, shelf, abyss, or thermal
- combat, tense exploration, base shelter, menu, and prologue can force music open immediately
- emergency breath, oxygen panic, and critical `PlayerStressSignal` foreground audio win over music; reactive synth punches and stingers are suppressed while player-critical audio dominates
- before the hard emergency cutoff, player stress still raises rhythm, bass, and danger-layer pressure so music tightens instead of switching binary on/off
- active vocal warnings own the speech foreground: music activity is ducked and stingers/reactive synth impulses are suppressed while the warning is speaking
- when music owns the emotional foreground, the underwater ambient loop ducks subtly instead of fighting the score

Runtime signals:

- `HectonMusicDirector.CurrentMusicActivity01` exposes how much the music is currently in the foreground
- `HectonMusicDirector.CurrentMusicActivityReason` exposes why: `Silent`, `Rest`, `Exploration`, `Base`, `Tense`, `Combat`, `Menu`, `Prologue`, `Override`, `Emergency`
- `HectonMusicDirector.CurrentRhythmLayer01`, `CurrentBassLayer01`, `CurrentAtmosphereLayer01`, and `CurrentDangerLayer01` expose the current layer model for tuning and HUD/debug surfaces
- `HectonMusicDirector.CurrentLayerMixerRouteAvailable` exposes whether at least one optional mixer-layer route is currently bound
- `DynamicMusicScalarSignal.MusicActivity01` mirrors that activity into the granular synth scalar lane
- `DynamicMusicScalarSignal.FlagSuppressReactiveImpulses` tells the granular synth to clear damage/stinger impulses during emergency or no-director suppression
- `PlayerStressSignal.Stress01` is consumed only by `HectonMusicDirector` on the main thread; the granular synth receives the director's sanitized activity/suppression policy, not a direct stress read
- `IVocalWarningSystem.IsWarningActive` is consumed only through the cached `GlobalRegistry.VocalWarnings` read-model; vocal warnings do not push music signals directly
- `StopMusic` / director disable publishes an immediate zero-activity scalar with reactive suppression so stale synth foreground does not linger
- `SoundscapeSystem` mirrors current `SoundscapeTier` and depth into `HectonMusicDirector.SetSoundscapeTierContext`
- `AcousticZoneController` reads the cached music director on the game thread and sidechains only the underwater ambient loop volume
- `AdaptiveStemAudioMixer` is a context-only compatibility bridge for tension/depth/quality; it publishes zero music activity and suppresses reactive impulses so it cannot fight the director
- in the granular synth drain, `SourceMusicDirectorHash` outranks fallback scalar sources for foreground activity and context within the same frame

Mixer routing:

- `MusicDirectorConfig_Global._musicMixerGroup` is bound to `MasterMixer/Music`
- `MusicDirectorConfig_Global._stingerMixerGroup` is bound to `MasterMixer/Music`
- fallback routing still exists in code, but authored runtime should use the dedicated Music bus so the settings `MusicVolume` slider controls beds and stingers together
- the dynamic granular synth follows `HectonMusicDirector.DedicatedMusicMixerGroup` before falling back to the Settings volume / Ambient route
- runtime layer routing computes rhythm, bass, atmosphere, and danger intensity from tension, predators, oxygen, player stress, storm pressure, depth, and soundscape tier
- if a mixer exposes `MusicLayer_Rhythm_dB`, `MusicLayer_Bass_dB`, `MusicLayer_Atmosphere_dB`, or `MusicLayer_Danger_dB`, the director writes cached logarithmic dB values to those parameters
- missing layer parameters are treated as an optional authoring route, not a runtime error; the director marks the layer mixer route unavailable and avoids retrying the same missing parameter every tick

Music activity policy:

- `Emergency`: music activity publishes `0`, reactive impulses are suppressed, stingers do not fire, and forced override starts publish suppression instead of a synth punch; oxygen danger and critical player stress both enter this gate
- `Vocal warning active`: music keeps its current reason but target activity is sidechained down; critical warning IDs like crush depth, hull breach, and oxygen low duck harder than routine radiation / power warnings
- `Rest` / `Silent`: no ambient duck, world sound owns the foreground
- `Exploration`: low to medium activity, phrase/rest cadence depends on soundscape tier, ambient duck is intentionally subtle
- `Base`: small stable bed, longer pauses, ambient duck remains mild
- `Tense`: higher activity and stronger ambient duck
- `Combat` / `Override`: strongest foreground ownership and full authored music duck weight
- `Menu` / `Prologue`: forced authored foreground, but still routed through the same activity scalar

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
- no biome/depth match -> current soundscape tier maps to `Shallow`, `Shelf`, `Abyss`, or `Thermal`
- no soundscape match -> `MusicProfile_Fallback`

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
