# Audio Source Folder Route Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `AUDIO_WAVEFORM_QA`.
Scope: folder-level source map derived from `Docs/Audio/audio_asset_ledger.csv`, `Docs/Audio/audio_profile_usage_20260605.csv`, and `Docs/Audio/audio_remediation_matrix_20260605.csv`.

No Unity run, import edit, prefab edit, build, play mode, profiler, listening pass, or `Assets` mutation was performed. This matrix is routing evidence only.

CSV companion: `Docs/AssetAudit/AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`.

## Static Summary

- Folders mapped: `15`.
- Ledger rows covered: `138`.
- Long rows over 10 seconds: `98`.
- Low-quality rows below Q70: `35`.
- Placeholder rows: `3`.
- Direct `Player.prefab` AudioClip refs by folder: `28`.
- Folder highest-priority distribution: `NONE_STATIC`=4, `P0`=2, `P1`=8, `P2`=1.

## High-Risk Folders

- `Assets/_Project/Audio`: rows `11`, classes `ambient:11`, highest `P0`, direct refs `2`, low-Q `7`. Direct Player.prefab AudioClip refs need lifecycle classification and proof. Next: `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md / ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Movement`: rows `3`, classes `player_loop:2;sfx:1`, highest `P0`, direct refs `2`, low-Q `2`. Direct dive_splash refs and movement loops need player/contact route classification. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/UI`: rows `5`, classes `ui:5`, highest `P1`, direct refs `4`, low-Q `0`. Direct Player.prefab UI refs need UI feedback owner, warning-priority, import readback, and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Footsteps/Default`: rows `4`, classes `sfx:4`, highest `P1`, direct refs `4`, low-Q `0`. Direct Player.prefab footstep refs need owner/load/release/playback and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Footsteps/Metal`: rows `4`, classes `sfx:4`, highest `P1`, direct refs `4`, low-Q `4`. Direct Player.prefab footstep refs need owner/load/release/playback and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Footsteps/Rock`: rows `4`, classes `sfx:4`, highest `P1`, direct refs `4`, low-Q `0`. Direct Player.prefab footstep refs need owner/load/release/playback and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Footsteps/Sand`: rows `4`, classes `sfx:4`, highest `P1`, direct refs `4`, low-Q `4`. Direct Player.prefab footstep refs need owner/load/release/playback and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Footsteps/Wet`: rows `4`, classes `sfx:4`, highest `P1`, direct refs `4`, low-Q `4`. Direct Player.prefab footstep refs need owner/load/release/playback and no-allocation proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Music for Game`: rows `84`, classes `music:84`, highest `P1`, direct refs `0`, low-Q `0`. MusicDirector profile beds/stingers need mixer route, silence-window, cooldown, Addressables, and listening proof. Next: `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`.
- `Assets/_Project/Audio/VO/Stubs`: rows `2`, classes `voice:2`, highest `P1`, direct refs `0`, low-Q `2`. Placeholder VO/source rows cannot define final voice, subtitle, localization, or loudness policy. Next: `AUDIO_ASSET_TAXONOMY_20260605.md / localization-writing owner`.
- `Assets/_Project/Audio/Ambient`: rows `1`, classes `ambient:1`, highest `P2`, direct refs `0`, low-Q `1`. Low Vorbis quality rows conflict with Q70 target or require scoped exception and listening proof. Next: `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`.
- `Assets/_Project/Audio/SFX`: rows `5`, classes `sfx:5`, highest `NONE_STATIC`, direct refs `0`, low-Q `5`. Low Vorbis quality rows conflict with Q70 target or require scoped exception and listening proof. Next: `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md / future SFX owner`.
- `Assets/_Project/Audio/Breathing`: rows `3`, classes `player_loop:3`, highest `NONE_STATIC`, direct refs `0`, low-Q `2`. Player-loop breath/suit rows need low-latency owner route, placeholder disposition, import exception, warning ducking, and listening proof. Next: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- `Assets/_Project/Audio/Impact`: rows `2`, classes `sfx:2`, highest `NONE_STATIC`, direct refs `0`, low-Q `2`. Low Vorbis quality rows conflict with Q70 target or require scoped exception and listening proof. Next: `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md / future SFX owner`.
- `Assets/_Project/Audio/Thruster`: rows `2`, classes `sfx:2`, highest `NONE_STATIC`, direct refs `0`, low-Q `2`. Low Vorbis quality rows conflict with Q70 target or require scoped exception and listening proof. Next: `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md / future SFX owner`.

## Rejections

- Do not classify folder presence as runtime playback proof.
- Do not fix direct prefab refs by raw YAML patching.
- Do not mass-edit import settings from this matrix alone.
- Do not treat low-Q rows, placeholder VO, long beds, or direct refs as accepted because they are indexed.
- Do not claim `0 B/frame`, Addressables release, mixer output, warning ducking, or listening quality from this matrix.

## Regression Model

- CPU: static source map only; no runtime CPU change.
- GC: no runtime code changed; no no-allocation proof.
- Memory: folder duration/load risk only; no resident memory proof.
- Cadence: long-bed/direct-ref risks are identified; runtime cadence remains unproven.
- Correctness: folder owners are clearer; Unity/readback/listening proof remains required.

Final status: `PENDING_VERIFICATION`.
