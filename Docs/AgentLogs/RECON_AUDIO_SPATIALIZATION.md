# AUDIO_SPATIALIZATION Recon

Command: `rg -n "AudioReverbZone|AudioChorusFilter" Assets Docs ProjectSettings`
Date: 2026-05-12

Findings:
- No first-party `AudioReverbZone` usage found in `Assets/_Project`, `Docs`, or `ProjectSettings`.
- `AudioChorusFilter` exists only in third-party DarkTonic MasterAudio plugin/editor code:
  - `Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioInspector.cs`
  - `Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupCreatorInspector.cs`
  - `Assets/Plugins/DarkTonic/MasterAudio/Scripts/Settings/SoundGroupVariation.cs`
  - `Assets/Plugins/DarkTonic/MasterAudio/Scripts/Settings/DynamicGroupVariation.cs`
- AUDIO_SPATIALIZATION implementation did not add `AudioReverbZone` or `AudioChorusFilter`.
