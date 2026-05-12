# RECON_AUDIO_SONAR_PROPAGATION

Status: PENDING VERIFICATION

Scope: `Assets/_Project/Scripts/Audio/`

## Findings

- `PlayerCriticalProceduralAudioRenderer.cs`: player-owned critical audio renderer has serialized `AudioSource` references for authored continuous loops: `hullGroanLoopSource`, `boilingWaterLoopSource`, and `boilingWaterPoolSources`. These are loop/pool sources, not active sonar echo playback.
- `AtmosphericAudioRuntimeInstaller.cs`: probes legacy thruster `AudioSource` only to disable/migrate legacy playback.
- `HectonMusicDirector.cs` and `MusicVoicePool.cs`: music/stinger voice sources, not player-attached critical sonar emitters.
- `AudioSource.PlayOneShot`: no matches in `Assets/_Project/Scripts/Audio/`.

## Risk

Player-attached multiple `AudioSource` components are possible through serialized fields on `PlayerCriticalProceduralAudioRenderer`, but the active sonar echo path now remains pure math through `_sonarEchoDelay` and `NativeArray<SonarEchoTap>`.

## Command Evidence

`rg -n "PlayOneShot|AudioSource" Assets/_Project/Scripts/Audio -g '*.cs'`

Hardware Impact: removing active sonar echo dependency on `AudioSource` avoids managed clip scheduling and mixer-source fanout; estimated 80-250 us saved per active ping on low-end i3/MX350. PENDING VERIFICATION.
