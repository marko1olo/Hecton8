# Asset Owner 08 - Audio Direct Ref Unwiring Packet

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.
Hard boundary: no Unity run, no prefab edit, no import edit, no Addressables operation, no listening pass, no profiler, no GC proof, no runtime proof.

## Mandates Followed

- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `AGENTS.md` audio, Addressables, zero-GC, and GlobalQualityWeight rules

## Static Inputs

- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.md`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`
- `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.md`
- `Docs/Audio/audio_profile_usage_20260605.csv`

## Scope

Remove or route direct prefab `AudioClip` references in `Assets/_Project/Prefabs/Player.prefab` after Unity prefab readback. Current static evidence lists 24 remaining direct refs. Prior `Underwater Ambient.wav` and `dive_splash.wav` direct refs are source-cleared in the working tree and still require Unity prefab readback plus removal/replacement or playback route proof. Direct prefab serialization is not Addressables ownership, not release proof, not playback route proof, and not audio-thread proof.

First-20 route moment: player breath/audio continuity, water entry/exit contact, HUD/UI feedback audibility, warning priority, and shallow-route mix clarity.

## P0 Categories

| Category | Static rows | Current blocker | Required classification |
|---|---:|---|---|
| Underwater Ambient readback | 0 current direct rows | Prior `Underwater Ambient.wav` direct refs at `Player.prefab` lines 137 and 239 are source-cleared to `{fileID: 0}` in the working tree. Duration 193s, Streaming, Vorbis Q0.45 remain import/source facts only. | Unity-read null/source-cleared state and prove removal/replacement or owned long-bed route. |
| dive_splash readback | 0 current direct rows | Prior `dive_splash.wav` direct refs at `Player.prefab` lines 1066 and 1067 are source-cleared from the serialized component block. Duration 1.729s, CompressedInMemory, ADPCM remain import/source facts only. | Unity-read source-cleared state and prove player contact SFX replacement route or absence decision. |

## P1 Categories

| Category | Static rows | Current blocker | Required classification |
|---|---:|---|---|
| Footsteps direct refs | 20 | Default, metal, sand, rock, and wet step clips directly serialized in `Player.prefab`. | Short SFX route with surface/material owner, import readback, playback route, and no-allocation proof. |
| UI direct refs | 4 | `openSound`, `closeSound`, `tabSwitchSound`, and `lowBatterySound` directly serialized in `Player.prefab`. | UI feedback route. Short SFX exception allowed only if owned, bounded, preloaded/loaded through approved route, and proven audible without masking warnings. |

## Route Requirements

- Route playback through `SpatialAudioManager`/native DSP where spatial, player, ambience, contact, or loop behavior is involved.
- Use Addressables ownership where clips are not fixed startup/core exceptions. Every acquired handle needs owner, ref-count, release path, and proof artifact.
- UI short SFX may be a scoped exception only after Unity readback confirms import settings, preload/load lifetime, owner, mixer/DSP path, warning-priority behavior, and 0 B/frame playback.
- Do not introduce MasterAudio event names, string RPCs, or generic event-string routing.
- Do not use `AudioSource.PlayOneShot` in hot paths.
- Do not treat direct prefab refs as load/release ownership.
- Do not mutate import settings or prefab YAML from static tables alone.
- For retained player-loop exceptions, document owner, phase, lifetime, release/shutdown path, latency target, ducking/priority rule, and Unity/runtime proof target.

## Execution Order

1. Unity prefab readback: confirm each remaining static direct-ref row still exists, confirm prior P0 ambient/splash fields are source-cleared or intentionally replaced, and map each serialized field to its owning component.
2. Classify each row or cleared field: Addressables-routed clip, DSP/player-loop exception, UI short SFX exception, replacement, absence decision, or removal.
3. Replace direct refs only through Unity-safe prefab workflow. No raw YAML patching.
4. Fill owner ledger: cue id, owner, route, Addressables key or exception id, load phase, release phase, priority, ducking rule, and fallback.
5. Run static grep proof after edit.
6. Run Unity import readback, runtime/listening proof, and GC proof after process gate clears.

## Acceptance Gates

- Static grep proof: `Player.prefab` has no unclassified direct `AudioClip` refs for P0/P1 rows; retained exceptions are explicitly named and scoped.
- Import-setting readback: `PENDING UNITY`. Verify load type, compression, quality, sample rate, mono/stereo, preload state, and loop flags in Unity.
- Runtime/listening proof: `PENDING UNITY`. Verify ambient/splash/footstep/UI audibility, warning priority, ducking, route entry/exit, cooldown/cadence, and no masking of oxygen/pressure/system warnings.
- GC proof: `PENDING UNITY`. Required output is 0 B/frame during repeated footsteps, water entry/exit spam, UI open/close/tab spam, and ambient route transitions.
- Addressables proof: `PENDING UNITY`. Required handle ledger, ref-count, release path, and no orphaned handles for routed clips.

## Regression Model

- CPU: risk from extra routing, DSP voice allocation, or per-event lookup. Required proof: no hot-path `GlobalRegistry` polling, no `AudioSource.PlayOneShot`, no main-thread stall, no per-event string routing.
- GC: risk from event strings, clip lookup, managed callbacks, UI text/log spam, or dynamic collections. Required proof: 0 B/frame in playback stress cases.
- Memory: risk from retaining long ambience, duplicate footstep/UI clips, or unmanaged direct refs. Required proof: Addressables ownership or documented fixed exception with release/shutdown path.
- Cadence: risk from splash/footstep/UI spam, constant ambient bed, repeated stingers, or warning masking. Required proof: owned cooldown/priority/ducking and listening pass.
- Correctness: risk from removing clips that player feedback depends on or routing loops late. Required proof: water contact, movement, HUD/UI, warnings, and ambience still function in the first-20 route.

## Continuous GlobalQualityWeight Consequences

- Low (`0.0-0.33`): keep player breath/warnings/UI and water contact audible first; reduce noncritical ambience density, voice count, and update cadence smoothly. No flat silence where feedback is required.
- Middle (`0.34-0.66`): enable fuller footsteps/contact variation and one owned ambient context with proven ducking; keep memory and voice pressure bounded by continuous weights.
- High (`0.67-0.89`): spend headroom on richer transition layers, reverb sends, and footstep/contact variation after warning priority and cooldown proof.
- Ultra (`0.90-1.0`): extend ambience layering, spatial detail, reverb quality, and contact polish without changing cue ownership, Addressables identity, save truth, or critical warning priority.

Final status: `PENDING VERIFICATION`.
