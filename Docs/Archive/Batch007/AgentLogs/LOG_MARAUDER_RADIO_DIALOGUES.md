# LOG: MARAUDER_RADIO_DIALOGUES

## 2026-05-16 - Radio Interception Bake

What was wrong:
- The batch had no baked Marauder radio auxiliary JSON under `Data/Localization/Radio/`.
- No local status/rationale/log files existed for this agent at session start.
- The project had localization hash precedent, but this task needed a strict top-level radio array and a clean-rating variant.

What was done:
- Created `Data/Localization/Radio/generate_marauder_radio.py`.
- Generated `marauder_radio_interceptions.json` with 15 lines: 5 reactor tutorial orders and 10 Leviathan/Black Keel ambient interceptions.
- Generated `marauder_radio_interceptions_clean.json` through the script-based clean filter.
- Generated `marauder_radio_dictionary.json` with 10 characters and 5 slang terms.
- Generated `marauder_radio_validation.json` with final `Status: VERIFIED MASTER GRADE`, `CoreStatus: DIALOGUES BAKED`, and `EvidenceClass: STATIC_JSON_CLI`.
- Maintained `Docs/Tasks/Status_MARAUDER_RADIO_DIALOGUES.md` and `Docs/AgentLogs/Rationale_MARAUDER_RADIO_DIALOGUES.md`.

Cinematic Cheats used:
- The Leviathan hunt is carried by radio, sonar confusion, lighting behavior, and pressure-language instead of physical simulation.
- Rival crew panic sells monster scale without spawning or simulating the monster.
- DSP emotion tags allow cheap filter modulation before any expensive voice processing.

Exact microseconds saved:
- Static estimates only; no profiler artifact exists for this data-only bake.
- Pre-baked text hashes: estimated 90 us saved per 15-line load pass.
- Pre-baked state hashes: estimated 45 us saved by avoiding runtime condition-string hash.
- Direct strict array payload: estimated 35 us saved by avoiding schema branch/object unwrap.
- Clean variant generated offline: runtime profanity-filter cost avoided; estimated 0 frame-time cost.

Verification:
- `python -m py_compile Data\Localization\Radio\generate_marauder_radio.py`: PASS.
- `python Data\Localization\Radio\generate_marauder_radio.py`: PASS, emitted `VERIFIED MASTER GRADE: core=DIALOGUES BAKED raw=15 clean=15 characters=10 slang=5`.
- Independent JSON audit: PASS, raw=15, clean=15, duplicate_hashes=0, tutorial=5, ambient=10.
- JSON parse audit: PASS for all 4 generated JSON files.
- `.cs` touch check under `Data/Localization/Radio`: PASS, none present.

Residual risk:
- Unity import, runtime subtitle loading, DSP consumption, GCMonitor, profiler, and PlayMode proof were not run. Evidence class remains `STATIC_JSON_CLI`.

## 2026-05-16 - OSHINO Data Truth Inquisition Pass

What was wrong:
- The previous pass was JSON-only and did not give SHINOBU a binary cache.
- Pacing metadata used constants without enough provenance.
- Ultra/RTX data existed only as JSON metadata, not binary-ingestible payload slices.
- Recipe-loop proof had not been run from this agent's verifier.

What was done:
- Added `marauder_radio_interceptions.h8bin` with `H8RD` magic, Little-endian `<4sHH14I` header, Little-endian `<20I` records, 64-byte header, 80-byte record stride, sorted hash records, 16-byte aligned payload offset, and 16-byte aligned text/low/clean/ultra payload slices.
- Added `marauder_radio_interceptions.layout.json`.
- Added `VerifyMarauderRadio.py`.
- Extended JSON entries with `LineIDHash`, `SpeakerHash`, `CategoryHash`, `LowTierText`, `LowTierHashID`, `EmotionCode`, and `Scalability` data.
- Added Ultra payload data: harmonic weights, harmonic noise octaves, spectral gradient RGBA stops, subtitle glitch mask, and radio breakup.
- Added validation report sections for math audit, scalability, Project Atlas fit, H-Phi/static data sovereignty, hash surface, and binary audit.
- Ran the 1,000,000-step economy Monte Carlo against `Data/Economy/Recipes.json`.

Cinematic Cheats used:
- Leviathan presence remains a deterministic radio/audio/lighting implication, not a simulated monster.
- Ultra radio breakup is hash-derived harmonic/spectral data, not runtime physical acoustics.
- Toaster path uses stripped `LowTierText` and narrowband static DSP metadata.

Exact microseconds saved:
- Still static estimates only; no profiler was run.
- Binary hash-record ingest avoids a runtime JSON traversal for this radio node.
- LowTierText reduces subtitle payload and voice/DSP decision surface on i3/MX350.
- Ultra data spends saved CPU budget on richer radio/noir presentation, not simulation truth.

Verification:
- `python -m py_compile Data\Localization\Radio\generate_marauder_radio.py Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- `python Data\Localization\Radio\generate_marauder_radio.py`: PASS.
- `python Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- Verify result: `json_collisions=0`, `binary_errors=0`, `economy_steps=1000000`, `economy_errors=0`.
- `.h8bin`: length `11104`, `mod16=0`, magic `H8RD`.
- Current `Docs/PROJECT_ATLAS.md`: 83 first-party asmdefs; radio data adds 0 runtime dependencies and 0 Core refs.

Residual risk:
- Unity import, runtime loader integration, DSP consumption, PlayMode, GCMonitor, profiler, and player-build proof remain absent. Evidence class remains `STATIC_JSON_CLI`.

## 2026-05-16 - Monte Carlo Gate Tightening

What was wrong:
- A stricter Monte Carlo version rebuilt craftable recipe lists every step and timed out after 604 seconds. That was verifier inefficiency, not a data pass.

What was done:
- Replaced per-step craftable-list rebuild with bounded random recipe probing in `VerifyMarauderRadio.py`.
- Reran the verifier after regenerating radio data.

Cinematic Cheats used:
- None. This was an offline economy-proof correction.

Exact microseconds saved:
- Runtime: 0 us. Offline verifier cost was reduced from a timed-out 604 s attempt to a completed pass.

Verification:
- `python -m py_compile Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- `python Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- Result: `steps=1000000`, `crafts=57916`, `deconstructs=41304`, `primitive_value_growth=0.0`, `json_collisions=0`, `binary_errors=0`, `economy_errors=0`.

Residual risk:
- This is still offline data evidence, not PlayMode or runtime economy proof.

## 2026-05-16 - Fixed-Width Ultra Binary Record Pass

What was wrong:
- The previous binary cache was aligned, but Ultra metadata still lived as a variable UTF-8 payload slice.
- That would force downstream parsing or a second-stage metadata unpack before God-Mode radio visuals could use it.

What was done:
- Repacked Ultra metadata into fixed `<32I>` records in `marauder_radio_interceptions.h8bin`.
- Updated `marauder_radio_interceptions.layout.json` to record 128-byte stride and fixed Ultra fields.
- Preserved 16-byte alignment: 64-byte header, 15 records, 5888-byte payload, 7872-byte file length, `mod16=0`.
- Kept endian contract Little-only through `<4sHH14I` header and `<32I` records.

Cinematic Cheats used:
- Ultra radio intensity remains packed harmonic/spectral metadata, not physical acoustic simulation.
- Leviathan scale continues to be sold through radio breakup and subtitle distortion instead of simulated creature truth.

Exact microseconds saved:
- Runtime profiler evidence absent. Static data-path estimate only: fixed record fields remove a downstream Ultra JSON parse and keep record lookup one 128-byte stride per line.

Verification:
- `python Data\Localization\Radio\generate_marauder_radio.py`: PASS.
- `python Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- Verify result: `json_collisions=0`, `binary_errors=0`, `economy_steps=1000000`, `economy_errors=0`.
- Binary report: `RecordSizeBytes=128`, `FileLengthBytes=7872`, `PayloadLengthBytes=5888`, `Endian=Little`.

Residual risk:
- Unity import, runtime loader integration, DSP consumption, PlayMode, GCMonitor, profiler, and player-build proof remain absent. Evidence class remains `STATIC_JSON_CLI`.

## 2026-05-16 - Provenance And Source Hygiene Pass

What was wrong:
- Pacing constants were derived in code but not documented strongly enough in generated validation data.
- Binary hygiene claims checked the output blob, but not the generator's explicit struct-format surface or stored header hashes.
- Lore tone had generator checks for slang and non-corporate sterile terms, but the standalone verifier did not expose a separate tone audit.

What was done:
- Added pacing provenance and derived-constant strings to `marauder_radio_validation.json`.
- Hardened `VerifyMarauderRadio.py` to verify canonical raw/clean/dictionary/layout hashes stored in the binary header.
- Hardened verifier to decode binary text slices and compare packed Ultra fields against source JSON.
- Added source hygiene checks: `struct.Struct` formats must start with `<`, direct `struct.pack` calls are rejected, and every radio `.h8bin/.bin` file must be 16-byte aligned.
- Added `LoreToneAudit` for slang presence, sterile/off-tone token rejection, and emotion distribution.

Cinematic Cheats used:
- The Alpha Leviathan remains represented by radio breakup, subtitle distortion, and pressure-language rather than simulation truth.
- Ultra still spends data bandwidth on fixed harmonic/spectral presentation fields, not physical acoustics.

Exact microseconds saved:
- Runtime profiler evidence absent. Static data-path estimate only: header hash validation and fixed fields reduce importer ambiguity; no runtime parser work is introduced by this pass.

Verification:
- `python -m py_compile Data\Localization\Radio\generate_marauder_radio.py Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- `python Data\Localization\Radio\generate_marauder_radio.py`: PASS.
- `python Data\Localization\Radio\VerifyMarauderRadio.py`: PASS.
- Verify result: `json_collisions=0`, `binary_errors=0`, `economy_steps=1000000`, `economy_errors=0`.
- Source hygiene: `StructFormats=["<4sHH14I","<32I"]`, direct `struct.pack` calls=false, binary alignment remainder=0.
- Lore tone: all 5 slang terms present, sterile token hits=0.

Residual risk:
- Unity import, runtime loader integration, DSP consumption, PlayMode, GCMonitor, profiler, and player-build proof remain absent. Evidence class remains `STATIC_JSON_CLI`.
