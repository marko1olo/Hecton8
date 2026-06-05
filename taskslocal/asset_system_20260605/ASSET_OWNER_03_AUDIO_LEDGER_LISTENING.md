# Asset Owner 03 - Audio Ledger And Listening

Mission: convert static audio inventory into a production cue ledger and listening-risk report for first-exit/shallow route audio.

Read first:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`
- `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/Audio/audio_profile_usage_20260605.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `audio.md`
- `streaming.md`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Required work:

- P0 first: resolve/report null `_musicMixerGroup` and `_stingerMixerGroup` refs in `MusicDirectorConfig_Global.asset`.
- P0 first: classify and route direct `Player.prefab` AudioClip refs; do not claim Addressables/release readiness from prefab serialization.
- Follow `AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md` for mixer, prefab refs, long-bed cadence, and import-policy conflict handling.
- Use `audio_remediation_matrix_20260605.csv` for exact P0/P1/P2 row order.
- Continue from `Docs/Audio/audio_asset_ledger.csv`; do not erase pending owner/Addressables fields.
- Preserve the 3213 class split: `music`, `ambient`, `sfx`, `player_loop`, `ui`, `voice`. Do not reintroduce `sfx_or_player_loop`.
- Columns minimum: path, cue_id, class, duration_sec, load_type, compression, quality, owner, Addressable group/key, placeholder flag, route use, notes.
- Resolve current ambiguity as report, not silent import edits: AGENTS ambient/music compressed-in-memory vs streaming mandate for clips above 10 seconds.
- Reclassify long breath/swim loops away from generic SFX if they are player/ambient loop banks.
- Mark VO stubs as placeholder and reconcile any duration override risk.
- Listening pass: first-exit bed, shallow shelf, storm/tension, silence windows, stingers, UI warnings.

Waveform review constraints:

- `shelf_1_Abandoned Depths`, `abyss_3_Deep Trench Drone`, and `stinger_dangerous_1_Iron_Teeth` are loud/dense enough to require MusicDirector gating.
- `breathing breath in and out 1` is too hot for generic SFX classification.
- `VOStub_Chen_Log01_EN` is placeholder and not final loudness proof.

Profile usage constraints:

- All 84 ledgered music tracks are serialized in at least one MusicDirector profile.
- `_musicMixerGroup` and `_stingerMixerGroup` are null in `MusicDirectorConfig_Global.asset` static evidence.
- `Player.prefab` has direct AudioClip refs; Addressables owner/release proof remains absent.

Proof output:

- Static ledger plus listening notes.
- No runtime mix acceptance without MusicDirector proof.
- No claim of 0 GC/audio thread safety without runtime telemetry.
