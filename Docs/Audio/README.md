# Audio Support Corpus

Status: STATIC AUDIO AUTHORING DATA / ACTIVE SUPPORT INPUTS
Evidence class: STATIC_DOC / CSV_SUPPORT

This folder stores dialogue, stem, synth, ledger, and remediation CSV support data. Some files are active support inputs for editor/runtime tools; others are stale scan snapshots retained for traceability. This folder is not audio DSP readiness, mixer readiness, import readiness, Addressables readiness, native audio proof, Play Mode proof, profiler proof, or runtime integration proof.

Local support files:

- `audio_asset_ledger.csv` - static ledger/remediation support data.
- `audio_profile_usage_20260605.csv` - static source-scan evidence snapshot; currently stale for prior `Underwater Ambient.wav` and `dive_splash.wav` Player-prefab direct rows after source-clearing. Use `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` plus `Tools/ValidateAudioDirectRefDetail.py` for current direct-ref truth until regenerated.
- `audio_remediation_matrix_20260605.csv` - static remediation queue.
- `audio_stem_rules.csv` - active stem-rule support input read by `AdaptiveStemAudioMixer` editor/runtime support paths.
- `dialogue_script.csv` - active dialogue metadata support input read by `VocalBankPlaybackRuntime` editor metadata paths.
- `synth_presets.csv` - synth preset authoring support data.

Stable authority routes:

- `audio.md`
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`
- `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`

Garbage-collection rule:

- Do not move this folder wholesale into `Docs/DEPRECATED`. `audio_stem_rules.csv`, `dialogue_script.csv`, and the direct-reference audit path above are live support routes. Individual stale snapshots may only be moved after checking active script references with `rg` and updating the consuming docs/tools.

Do not cite these CSVs as proof of DSPGraph output, underrun-free playback, imported assets, Addressables groups, stem playback, synthesis routing, or runtime audio health. Those claims require current audio/runtime proof artifacts outside this README.
