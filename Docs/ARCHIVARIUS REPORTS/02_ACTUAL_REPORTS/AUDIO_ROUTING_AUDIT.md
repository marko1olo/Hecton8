# HECTON-8 — AUDIO MIXER ROUTING AUDIT
Date: 2026-05-04
Status: REFERENCE

**Status:** ETA SURGERY_PREPPED  
**Mandate:** AGENTS.md — "Every sound must route through the Master Mixer (SFX, Music, UI, Ambient) for sidechaining."  
**Date:** 2026-04-28

---

## EXECUTIVE SUMMARY

| Category | Count | Action Required |
|---|---|---|
| **First-Party Violators** | **6** | **ASSIGN MIXER GROUPS** |
| Third-Party Violators | 4 | Documented only; do not modify without vendor mandate |
| Runtime-Routed (Code-Correct) | 5+ | No action — mixer assigned in `Awake`/`Play` |
| Total AudioSource Scanned | 10+ prefabs / scenes | — |

---

## FIRST-PARTY VIOLATORS

> **Rule:** Any `AudioSource` with `OutputAudioMixerGroup: {fileID: 0}` in a first-party prefab is a **sidechain-breaking violation**. Master Grade requires explicit routing.

### 1. `Player.prefab` — 3× Un-routed AudioSource
| Component Context | fileID | Mixer Assigned? | Risk |
|---|---|---|---|
| AudioSource (root) | `3265406370731010970` | ❌ `fileID: 0` | Thruster / suit SFX will bypass ducking |
| AudioSource (child) | `140955468146317891` | ❌ `fileID: 0` | Footstep / ambient emitters un-routed |
| AudioSource (child) | `8362680367133501314` | ❌ `fileID: 0` | Secondary voice / warning tones un-routed |

**Code Evidence:**
- `PlayerThrusterAudio.cs` acquires `GetComponent<AudioSource>()` but **never assigns** `outputAudioMixerGroup`.
- `MantaScooter.cs` acquires `_motorAudioSource` via `TryGetComponent` but **never assigns** `outputAudioMixerGroup`.
- `AcousticZoneController.cs` has fallback logic to assign `AmbientGroup` at runtime, but this is **lazy and conditional** — if `SpatialAudioManager` is not initialized, the source remains un-routed.

**Fix:** Assign `SpatialAudioManager.Instance.SfxGroup` or `AmbientGroup` in `PlayerThrusterAudio.Awake()` and `MantaScooter.Awake()` explicitly. Serialize mixer group references on Player prefab AudioSources.

---

### 2. `PFB_HectonMusicDirectorRoot.prefab` — 3× Un-routed AudioSource
| Component Context | fileID | Mixer Assigned? | Risk |
|---|---|---|---|
| Music Voice A | `5264868714818055336` | ❌ `fileID: 0` | Music bed bypasses master music ducking |
| Music Voice B | `6037039787081037273` | ❌ `fileID: 0` | Music bed bypasses master music ducking |
| Stinger Voice | `8395572843726022766` | ❌ `fileID: 0` | Stingers bypass stinger ducking |

**Code Evidence:**
- `HectonMusicDirector.cs` assigns `outputAudioMixerGroup = musicGroup` in `ConfigureVoice()` (line 808) and `ApplyLayerMixerState()`.
- **BUT:** If `MusicVoicePool` fails to resolve voices, or if prefab is instantiated before `HectonMusicDirector.Start()` runs, the sources remain on `fileID: 0` until runtime assignment.

**Fix:** Hard-assign mixer groups in prefab YAML for all three AudioSources. Runtime re-assignment is acceptable, but prefab default must NOT be `fileID: 0`.

---

## THIRD-PARTY VIOLATORS (Informational Only)

Per AGENTS.md 3RD-PARTY ASSET INTEGRITY rule: do not modify third-party prefabs without explicit vendor mandate.

| Prefab / Scene | AudioSource Count | fileID Context |
|---|---|---|
| `Assets/ScifiFacility/Prefabs/structural/walls/wall_01_4x3_door_02.prefab` | 1 | `82283425256389758` — door sound, disabled (`m_Enabled: 0`) |
| `Assets/Plugins/DarkTonic/MasterAudio/Sources/Prefabs/Internal/DynamicGroupVariation.prefab` | 1 | Template prefab; MasterAudio assigns mixer at runtime via `SoundGroupVariation.UpdateAudioSource()` |
| `Assets/Plugins/DarkTonic/MasterAudio/ExampleScenes/_StandaloneScene.unity` | 2 | Example scene — not in build |
| `Assets/Feel/NiceVibrations/Demo/DemoAssets/WobbleDemo/Prefabs/WobbleButton.prefab` | 1 | Demo prefab — not in build |

---

## CODE-LEVEL VIOLATORS (Serialized Fields Without Mixer Assignment)

The following first-party scripts acquire `AudioSource` references via `[SerializeField]` or `GetComponent` but do **not** assign `outputAudioMixerGroup` in code. If the prefab instance also lacks a mixer assignment, the source is un-routed.

| Script | AudioSource Field | Runtime Mixer Assignment? |
|---|---|---|
| `BaseModule.cs` | `[SerializeField] private AudioSource audioSource;` | ❌ None — leak loop, flood clip play raw |
| `LaserCutter.cs` | `[SerializeField] private AudioSource cutAudio;` | ❌ None — cutting loop un-routed |
| `RepairTool.cs` | `[SerializeField] private AudioSource repairLoopAudio;` | ❌ None — weld loop un-routed |
| `MantaScooter.cs` | `private AudioSource _motorAudioSource;` | ❌ None — motor loop un-routed |
| `PlayerThrusterAudio.cs` | `private AudioSource _audioSource;` | ❌ None — thruster loop un-routed |

**Note:** `AcousticZoneController.cs` has `playerUnderwaterAmbientSource` which is conditionally routed in `EnsureAmbientSourceMixerRouting()`. This is the only script with explicit fallback logic.

---

## COMPLIANT SYSTEMS (Runtime Mixer Assignment Verified)

| System | Assignment Location | Mixer Group |
|---|---|---|
| `SpatialAudioManager` | `PlayAtPoint()` / `PlayStatic2D()` | `_sfxGroup`, `_interfaceGroup`, `_ambientGroup` |
| `HectonMusicDirector` | `ConfigureVoice()` / `ApplyLayerMixerState()` | `musicGroup`, `stingerGroup` |
| `MusicVoicePool` | `ConfigureVoiceSource()` | `mixerGroup` (passed from director) |
| `AcousticZoneController` | `EnsureAmbientSourceMixerRouting()` | `playerUnderwaterAmbientMixerGroup` → `AmbientGroup` fallback |

---

## RECOMMENDED FIX ORDER

1. **Player.prefab** — Add `AudioMixerGroup` references to all 3 AudioSource components (SFX group for thruster, Ambient group for underwater, UI group for warnings).
2. **PFB_HectonMusicDirectorRoot.prefab** — Add `AudioMixerGroup` references to all 3 AudioSource components (Music group, Stinger group).
3. **BaseModule.cs** — In `Awake()` or `OnEnable()`, assign `audioSource.outputAudioMixerGroup = SpatialAudioManager.Instance?.SfxGroup`.
4. **LaserCutter.cs** — Assign `cutAudio.outputAudioMixerGroup` in `Awake()`.
5. **RepairTool.cs** — Assign `repairLoopAudio.outputAudioMixerGroup` in `Awake()`.
6. **MantaScooter.cs** — Assign `_motorAudioSource.outputAudioMixerGroup` after `TryGetComponent`.
7. **PlayerThrusterAudio.cs** — Assign `_audioSource.outputAudioMixerGroup` in `Awake()`.

---

## DEBT TALLY

- **Un-routed First-Party AudioSources in Prefabs:** `6`
- **Un-routed First-Party AudioSources in Code (no fallback):** `5`
- **Total First-Party Violations:** `11` (6 prefab + 5 code-path)

**STATUS:** ETA SURGERY_PREPPED
