# Audio Listening Pass Queue - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `AUDIO_WAVEFORM_QA`.
Scope: listening-pass order only. No Unity import settings, mixer groups, Addressables data, prefabs, or audio files were changed.

## Input Evidence

- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`
- `Docs/Audio/audio_profile_usage_20260605.csv`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv`

Queue file:

- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv`

## Pass Order

1. Resolve MusicDirector mixer route evidence before judging music taste.
2. Prove or reroute direct `Player.prefab` long/loop refs before broader ambience acceptance.
3. Listen to player breath, suit, and swim loops as player-loop banks, not generic SFX.
4. Listen to long ambience and MusicDirector beds for masking, cadence, silence windows, and tension ownership.
5. Check stingers for cooldown and warning priority.
6. Check UI click and VO stub last; UI needs audibility proof, VO is placeholder-blocked.

## Required Proof

- Unity config/prefab/import readback.
- Runtime MusicDirector capture for pause windows, profile transitions, stinger cooldown, and no constant bed.
- Listening notes for first exit, shallow shelf, storm/tension, warning/UI overlap, and silence windows.
- Mixer or native/DSP route proof.
- Runtime 0 B/frame proof before any hot-path playback claim.
- Memory/Addressables proof for long beds and active banks.

## Scalability Consequences

- Low/compact: one active long ambience/music bank unless memory proof allows more; player loops, warnings, and UI feedback outrank decorative beds.
- Middle: profile variety can expand only after warning/UI/player-loop audibility remains intact.
- High: broader prefetch and richer stinger variety require MusicDirector cadence and memory proof.
- Ultra: dense secondary beds and richer spatial/reverb layers are allowed only when breath, warnings, route cues, and UI remain readable.

## Regression Model

- CPU: no runtime code changed.
- GC: no runtime code changed; future playback must prove 0 B/frame.
- Memory: no import or residency changed; long-bed residency remains blocked by policy and proof.
- Cadence: no runtime cadence changed; future stingers and beds need runtime capture.
- Correctness: reduces listening-order ambiguity only; no audio acceptance.

Final status: `PENDING_VERIFICATION`.
