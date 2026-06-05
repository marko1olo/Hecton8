# Audio Mix Priority Decision Queue - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_AUDIO_QA_ONLY`.
Evidence class: `STATIC_SOURCE + AUDIO_WAVEFORM_QA`.
Runtime mix proof: absent.
Listening pass: absent.
Unity import/readback: absent.
Asset mutation: none.

CSV companion: `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.csv`.

## Scope

This queue defines the future mix-priority order for first-exit, shallow, and medium-depth audio proof. It uses static source metadata, waveform preview stats, `AUDIO_LISTENING_PASS_QUEUE_20260605.csv`, and `AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.csv`.

This is not a mix result. It exists to prevent future owners from letting long beds, stingers, or suit loops mask player-critical information.

## Priority Rule

Player decision cues outrank atmosphere:

1. oxygen, pressure, damage, threat, tool, sonar, and UI warning cues;
2. breath, suit-body, swim, dive, and first-person motion continuity;
3. route ambience and environmental beds;
4. MusicDirector beds;
5. stingers and decorative tension events;
6. VO stubs and non-final placeholder speech.

Music taste cannot be accepted while mixer routes, direct refs, ducking, silence windows, active-bank ownership, and runtime `0 B/frame` proof are absent.

## Static Risk Notes

- `breathing breath in and out 1.mp3`: preview peak `-0.16 dBFS`, RMS `-14.03 dBFS`; too hot to classify as generic background.
- `shelf_1_Abandoned Depths.ogg`: RMS `-11.05 dBFS`; loud long bed risk.
- `abyss_3_Deep Trench Drone.ogg`: peak `-0.51 dBFS`; high-peak long drone risk.
- `stinger_dangerous_1_Iron_Teeth.ogg`: RMS `-14.48 dBFS`; cooldown and event ownership required.
- `UI/click sound.wav`: RMS `-38.58 dBFS`; likely inaudible under unowned beds unless UI route is proved.
- `VOStub_Chen_Log01_EN.wav`: RMS `-63.21 dBFS`; placeholder only, not final VO policy.

## Low / Middle / High / Ultra Consequences

- Low/compact: only critical player, warning, UI, and one owned route bed may be active unless proof allows more.
- Middle: add controlled ambience/music breadth after ducking and active-bank proof.
- High: add smoother transitions, profile stingers, and richer ambience only after warning priority survives.
- Ultra: dense beds, wider stinger palette, reverb, and prefetch breadth are allowed only after critical cues remain readable and owner/release routes stay stable.

## Regression Model

- CPU: no runtime route changed; future risk is decode, crossfade, stinger scheduling, and DSP contention.
- GC: no runtime route changed; future proof must reject string cue lookup, allocations, callbacks, and coroutine mix paths.
- Memory: no source imported or resident; future risk is long-bed prefetch and duplicate direct prefab refs.
- Cadence: no runtime cadence changed; future risk is constant beds, repeated stingers, and missing silence windows.
- Correctness: queue fixes proof order only. Runtime audio remains unproven.

Final status: `PENDING_VERIFICATION`.
