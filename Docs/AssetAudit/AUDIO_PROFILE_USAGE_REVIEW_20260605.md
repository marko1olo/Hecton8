# Audio Profile Usage Review - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE` / `STATIC_DOC` only.
Scope: MusicDirector profiles/config assets, audio GUID-to-ledger mapping, and direct prefab AudioClip refs under `Assets/_Project`.
First-20 route moment: addresses static music-profile and cue-source ambiguity for main menu, prologue, first exit, photic shallows, shelf/mid-depth, abyss, base shelter, combat, and fallback routing.

## Evidence Boundary

- No Unity run, import, build, play mode, profiler, listening pass, mix pass, or audio import edit was performed.
- No files under `Assets` were edited.
- Static GUID refs prove serialized references only. They do not prove runtime loading, Addressables ownership, clip readiness, mixing, loudness, scene wiring, or 0 B/frame behavior.
- `Docs/Audio/audio_asset_ledger.csv` was used as read-only ledger evidence; it is not runtime residency proof.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit.txt`: static search downgraded to static evidence only.
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`: no runtime audio or managed callback readiness claims.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`: Addressables/residency remains pending because serialized refs are not handle/release proof.
- `audio.md`: music is judged as cue discipline evidence only; no constant-bed or mix acceptance claim.
- `streaming.md`: large/streamed audio bank ownership remains unresolved without Addressables proof.

## Static Scan Summary

| Item | Count | Evidence |
| --- | ---: | --- |
| Music profile assets scanned | 10 | `Assets/_Project/Data/Audio/Music/Profiles/*.asset` |
| MusicDirector config assets scanned | 1 | `Assets/_Project/Data/Audio/Music/Configs/*.asset` |
| Profile cue rows | 150 | serialized `_cueId` + `_clip` refs |
| Profile bleed rows | 36 | serialized `_profile` bleed refs |
| Config rows | 13 | profile routes, null mixers, runtime prefab |
| Direct prefab AudioClip refs | 28 | audio GUID refs inside `.prefab` YAML under `Assets/_Project` |
| Ledgered music tracks | 84 | `Docs/Audio/audio_asset_ledger.csv` class=`music` |
| Music tracks unused by any profile | 0 | GUID/path compare: profile clip refs vs ledger music paths |
| Repeated stinger GUID groups | 11 | same stinger AudioClip GUID serialized in more than one profile row |
| Long bed rows >=300s | 7 | profile long-track rows with ledger duration >=300s |
| Unmapped profile cue clip GUIDs | 0 | profile `_clip` refs not mapped to audio ledger path |
| Unmapped profile/config profile GUIDs | 0 | profile refs not mapped to profile asset path |

CSV output: `Docs/Audio/audio_profile_usage_20260605.csv`.

Known stale slice: this sidecar still records older direct `Player.prefab` rows for `Underwater Ambient.wav` and `dive_splash.wav`. Current `Player.prefab` static scan reports `0` direct refs for those two cues and `24` direct refs total, all footstep/UI. Use `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` and `Tools/ValidateAudioDirectRefDetail.py` as current Player direct-ref truth until this profile usage scan is regenerated.

## MusicDirector Config Map

| Config Field | Referenced Asset | Referenced Profile Id / Note |
| --- | --- | --- |
| _mainMenuProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_MainMenu.asset | music.profile.main_menu |
| _prologueProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Prologue.asset | music.profile.prologue |
| _shallowProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Shallow.asset | music.profile.shallow |
| _shelfProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Shelf.asset | music.profile.shelf |
| _abyssProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Abyss.asset | music.profile.abyss |
| _caveProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Cave.asset | music.profile.cave |
| _thermalProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Thermal.asset | music.profile.thermal |
| _baseProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Base.asset | music.profile.base |
| _combatProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Combat.asset | music.profile.combat |
| _fallbackProfile | Assets/_Project/Data/Audio/Music/Profiles/MusicProfile_Fallback.asset | music.profile.fallback |
| _musicMixerGroup |  | null mixer group ref in static config |
| _stingerMixerGroup |  | null mixer group ref in static config |
| _runtimeDirectorPrefab | Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab | runtime director prefab ref; not an audio clip |

Static defect: `_musicMixerGroup` and `_stingerMixerGroup` are null in `MusicDirectorConfig_Global.asset`. This is not a runtime failure claim; it is serialized config evidence that mixer routing is not authored in this asset.

## Unused Music Tracks

None found. All 84 ledgered `Music for Game` tracks have at least one serialized profile `_clip` reference.

## Repeated Stingers

Repeated here means the same stinger AudioClip GUID is serialized in more than one profile row. It may be intentional library reuse, but it reduces profile identity and needs listening/runtime cadence proof.

| Cue Id | Role | Count | Profiles | Ledger Path |
| --- | --- | ---: | --- | --- |
| music.stinger_being_saved_3_first_breath_above | RecoveryStinger | 8 | MusicProfile_Abyss; MusicProfile_Base; MusicProfile_Cave; MusicProfile_Combat; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_being_saved_3_First_Breath_Above.ogg |
| music.stinger_being_saved_1release_at_depth | RecoveryStinger | 8 | MusicProfile_Abyss; MusicProfile_Base; MusicProfile_Cave; MusicProfile_Combat; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_being_saved_1Release_At_Depth.ogg |
| music.stinger_being_saved_2_pressure_gives_way | RecoveryStinger | 8 | MusicProfile_Abyss; MusicProfile_Base; MusicProfile_Cave; MusicProfile_Combat; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_being_saved_2_Pressure_Gives_Way.ogg |
| music.stinger_dangerous_2heartbeat_under_floorboards | DangerStinger | 7 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Combat; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_dangerous_2Heartbeat_Under_Floorboards.ogg |
| music.stinger_dangerous_1_iron_teeth | DangerStinger | 7 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Combat; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_dangerous_1_Iron_Teeth.ogg |
| music.stinger_discovery_2_the_sunken_archive | DiscoveryStinger | 6 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_discovery_2_The_Sunken_Archive.ogg |
| music.stinger_discovery_4_the_sunken_observatory | DiscoveryStinger | 6 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_discovery_4_The_Sunken_Observatory.ogg |
| music.stinger_discovery_5_the_sunken_relic | DiscoveryStinger | 6 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_discovery_5_The_Sunken_Relic.ogg |
| music.stinger_discovery_1_the_sunken_gate | DiscoveryStinger | 6 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_discovery_1_The_Sunken_Gate.ogg |
| music.stinger_discovery_3_the_sunken_observatory | DiscoveryStinger | 6 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback; MusicProfile_Shallow; MusicProfile_Shelf; MusicProfile_Thermal | Assets/_Project/Audio/Music for Game/stinger_discovery_3_The_Sunken_Observatory.ogg |
| music.stinger_hallucination_1_beneath_the_tide | DiscoveryStinger | 3 | MusicProfile_Abyss; MusicProfile_Cave; MusicProfile_Fallback | Assets/_Project/Audio/Music for Game/stinger_hallucination_1_Beneath_the_Tide.ogg |

## Long Beds

Threshold used: serialized profile bed in `CalmLongTracks` or `TenseLongTracks` with ledger duration `>= 300` seconds.

| Profile | Section | Duration Sec | Weight | Tension | Ledger Path |
| --- | --- | ---: | ---: | ---: | --- |
| MusicProfile_Shelf | CalmLongTracks | 479.96 | 1 | 0.35 | Assets/_Project/Audio/Music for Game/shelf_1_Abandoned Depths.ogg |
| MusicProfile_Shelf | CalmLongTracks | 479.96 | 1 | 0.35 | Assets/_Project/Audio/Music for Game/shelf_2_Abandoned Depths (1).ogg |
| MusicProfile_Fallback | CalmLongTracks | 407.72 | 1 | 0.34 | Assets/_Project/Audio/Music for Game/ambient_dlinni_1_Sub-Bass Throb.ogg |
| MusicProfile_Fallback | CalmLongTracks | 379.08 | 1 | 0.34 | Assets/_Project/Audio/Music for Game/ambient_dlinni_2_Sub-Bass Hive.ogg |
| MusicProfile_Abyss | TenseLongTracks | 372.96 | 1 | 0.74 | Assets/_Project/Audio/Music for Game/abyss_3_Deep Trench Drone.ogg |
| MusicProfile_Abyss | CalmLongTracks | 317.28 | 1 | 0.46 | Assets/_Project/Audio/Music for Game/abyss_6_Deep Water Pressure Hum (1).ogg |
| MusicProfile_Cave | CalmLongTracks | 304.96 | 1 | 0.42 | Assets/_Project/Audio/Music for Game/cave_ambient_4_Sub-bass Pressure.ogg |

Risk: long beds are not automatically wrong. They are risk because `audio.md` rejects constant music beds and requires silence/decision discipline. Runtime MusicDirector pause/crossfade behavior is still `PENDING VERIFICATION`.

## Direct Prefab Audio Refs

All direct prefab AudioClip refs found in this pass are in `Assets/_Project/Prefabs/Player.prefab`. Static refs are listed because Addressables ownership/release and hot-path playback route are not proven by prefab serialization.

### Assets/_Project/Prefabs/Player.prefab

| Line | Ledger Path | Load Type | Compression | Duration Sec |
| ---: | --- | --- | --- | ---: |
| 137 | Assets/_Project/Audio/Underwater Ambient.wav | Streaming | Vorbis | 193 |
| 239 | Assets/_Project/Audio/Underwater Ambient.wav | Streaming | Vorbis | 193 |
| 816 | Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (1).ogg | DecompressOnLoad | ADPCM | 0.375 |
| 817 | Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (2).ogg | DecompressOnLoad | ADPCM | 0.375 |
| 818 | Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (3).ogg | DecompressOnLoad | ADPCM | 0.375 |
| 819 | Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (4).ogg | DecompressOnLoad | ADPCM | 0.375 |
| 825 | Assets/_Project/Audio/Footsteps/Metal/metal step (1).wav | DecompressOnLoad | ADPCM | 0.614 |
| 826 | Assets/_Project/Audio/Footsteps/Metal/metal step (2).wav | DecompressOnLoad | ADPCM | 0.914 |
| 827 | Assets/_Project/Audio/Footsteps/Metal/metal step (3).wav | CompressedInMemory | ADPCM | 0.706 |
| 828 | Assets/_Project/Audio/Footsteps/Metal/metal step (4).wav | CompressedInMemory | ADPCM | 0.73 |
| 834 | Assets/_Project/Audio/Footsteps/Sand/sand step  (1).wav | DecompressOnLoad | ADPCM | 0.44 |
| 835 | Assets/_Project/Audio/Footsteps/Sand/sand step  (2).wav | DecompressOnLoad | ADPCM | 0.517 |
| 836 | Assets/_Project/Audio/Footsteps/Sand/sand step  (3).wav | DecompressOnLoad | ADPCM | 0.726 |
| 837 | Assets/_Project/Audio/Footsteps/Sand/sand step  (4).wav | DecompressOnLoad | ADPCM | 0.569 |
| 843 | Assets/_Project/Audio/Footsteps/Rock/rock step (1).wav | DecompressOnLoad | ADPCM | 0.525 |
| 844 | Assets/_Project/Audio/Footsteps/Rock/rock step (2).wav | DecompressOnLoad | ADPCM | 0.476 |
| 845 | Assets/_Project/Audio/Footsteps/Rock/rock step (3).wav | DecompressOnLoad | ADPCM | 0.42 |
| 846 | Assets/_Project/Audio/Footsteps/Rock/rock step (4).wav | DecompressOnLoad | ADPCM | 0.49 |
| 852 | Assets/_Project/Audio/Footsteps/Wet/wet step (1).wav | DecompressOnLoad | ADPCM | 0.832 |
| 853 | Assets/_Project/Audio/Footsteps/Wet/wet step (2).wav | CompressedInMemory | ADPCM | 0.962 |
| 854 | Assets/_Project/Audio/Footsteps/Wet/wet step (3).wav | DecompressOnLoad | ADPCM | 0.771 |
| 855 | Assets/_Project/Audio/Footsteps/Wet/wet step (4).wav | DecompressOnLoad | ADPCM | 0.984 |
| 1066 | Assets/_Project/Audio/Movement/dive_splash.wav | CompressedInMemory | ADPCM | 1.729 |
| 1067 | Assets/_Project/Audio/Movement/dive_splash.wav | CompressedInMemory | ADPCM | 1.729 |
| 1612 | Assets/_Project/Audio/UI/blow ui sound (notif or option).mp3 | CompressedInMemory | ADPCM | 1.44 |
| 1613 | Assets/_Project/Audio/UI/electro (nope) sound.flac | DecompressOnLoad | ADPCM | 1.673 |
| 1614 | Assets/_Project/Audio/UI/click sound.wav | DecompressOnLoad | ADPCM | 0.172 |
| 1615 | Assets/_Project/Audio/UI/blow ui sound (notif or option).mp3 | CompressedInMemory | ADPCM | 1.44 |

## GUID Mapping Status

Profile cue clip GUIDs, profile bleed GUIDs, config profile GUIDs, and direct prefab audio GUIDs mapped to local asset paths for this static scope.

Unknown remains: Addressable group/key, owner, ref-count/release route, runtime pool ownership, mixer binding, listener routing, final loudness/mix, and runtime cue cadence. Ledger rows still carry `PENDING_OWNER` / `PENDING_ADDRESSABLES` in the source ledger.

## Blockers

- Runtime and mix readiness are unproven. No listening pass, Unity Console, Play Mode, Profiler, or DSP proof exists in this pass.
- Mixer group refs are null in the global MusicDirector config asset.
- Direct `Player.prefab` AudioClip refs need an owner/release route review before claiming Addressables or zero-GC audio lifecycle compliance.
- Long beds require runtime cadence/listening proof so music does not become a constant emotional blanket.
- Repeated stingers require profile-identity and cooldown proof. Static reuse alone does not prove bad behavior, but it is a tuning risk.

## Regression Model

- CPU: no runtime code changed; static scan only.
- GC: no runtime code changed; no 0 B/frame claim.
- Memory/residency: serialized refs and ledger paths only; Addressables handle ownership remains unproven.
- Cadence: no runtime cadence changed; repeated stinger and long-bed rows are static tuning risks only.
- Correctness: improved static asset-system traceability; no Unity acceptance claim.

Final status: `PENDING VERIFICATION`.
