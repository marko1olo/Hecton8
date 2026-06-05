# Audio Critical Cue Coverage Matrix - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_AUDIO_QA_ONLY`.
Evidence class: `STATIC_SOURCE + STATIC_DOC + AUDIO_WAVEFORM_QA`.
Runtime mix proof: absent.
Listening pass: absent.
Unity import/readback: absent.
Asset mutation: none.

CSV companion: `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv`.

## Scope

This matrix checks whether first-exit, shallow, and medium-depth player-critical cue families have source coverage before audio runtime owners touch import, mixer, Addressables, or DSP routes.

It uses:

- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.csv`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv`
- `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md`
- English route warning copy under `Docs/Lore/AppliedContent/in_game_wiki/en_US/`

## Static Findings

- `VocalWarningSystem` has priority doctrine for hull, crush, oxygen, radiation, and power, but static evidence does not map those hashes to final clips or banks.
- Breath, suit, swim, water-entry, UI, ambient, MusicDirector, and stinger candidates exist.
- Dedicated sonar/scanner and tool-feedback audio sources were not found by the current ledger keyword scan.
- Music beds, pressure hums, and stingers are not warning-cue coverage. They require lower priority than player decision cues.
- `CLICK_SOUND` and VO stubs are too weak or placeholder-bound to prove UI/warning readability.

## Low / Middle / High / Ultra Consequences

- Low/compact: keep critical warnings, UI, breath, and one owned route bed readable; missing cue families must fail closed, not be masked by music.
- Middle: admit route ambience and motion detail after warning and UI ducking proof.
- High: add richer transitions, stingers, and tool/UI texture only after cue-family coverage is proved.
- Ultra: wider audio density is allowed only after final warnings stay readable and owner/release routes remain stable.

## Regression Model

- CPU: no runtime route changed; future risk is decode, voice scheduling, stinger spam, and unmanaged callback work.
- GC: no runtime route changed; future proof must reject string cue lookup, managed clip dispatch, and coroutine audio routes.
- Memory: no source imported or resident; future risk is final warning-bank prefetch, long-bed residency, and duplicate direct prefab refs.
- Cadence: no runtime cadence changed; future risk is missing silence windows and repeated stingers.
- Correctness: this file identifies missing or blocked cue-family coverage only. It is not mix acceptance.

Final status: `PENDING_VERIFICATION`.
