# Rationale: MARAUDER_RADIO_DIALOGUES

State: VERIFIED MASTER GRADE

## Initial Decision
Problem: Radio interception content needs deterministic localization identifiers without touching Unity runtime code.
Solution: Generate strict JSON data plus validator from a cold offline script, using FNV-1a hashes over canonical text payloads.
Rejected Alternatives: Unity ScriptableObject authoring was rejected because task explicitly requires JSON and forbids `.cs` edits.
Scalability potential: Low uses static subtitle data; Middle adds DSP emotion tags; High adds richer interception variety; Ultra can layer voice-filter modulation without changing the JSON contract.
Hardware Impact: Offline generation has 0 runtime frame cost on i3/MX350; baked JSON avoids runtime hash allocation and parsing work beyond normal localization load.

## Mandates Selected
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- QA_Evidence_Text_Filter_Audit.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- PROG_Quest_State_Graph_Logic.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt

## Decision: Character Dictionary
Problem: Radio lines need recognizable voice ownership without adding runtime speaker discovery.
Solution: Bake 10 speakers with deterministic FNV-1a `HashID` values in `marauder_radio_dictionary.json`.
Rejected Alternatives: Inline-only speaker names were rejected because they provide no audit surface for duplicate or orphaned voices.
Scalability potential: Low tier reads static subtitle metadata; Middle/High/Ultra can route speakers to richer DSP voice chains using the same speaker hashes.
Hardware Impact: Estimated low-end gain is tiny but real: no runtime speaker-string hashing or speaker dictionary construction on i3/MX350.

## Decision: Strict Array JSON
Problem: Prompt requires `[{"HashID": uint, "Speaker": string, "AudioDelay": float, "Text": string}]`, while project localization usually uses an object root.
Solution: Keep the radio interception output as a top-level array and place dictionary/validation in separate JSON files.
Rejected Alternatives: Reusing `Data/Localization/en_US.json` object schema was rejected because it would not match the prompt's required radio payload shape.
Scalability potential: Low tier can stream the raw array directly; Ultra can layer clean/dirty variants and richer DSP processing without touching runtime code.
Hardware Impact: Static estimate only: direct array read avoids one schema branch and keeps the auxiliary node narrow.

## Decision: Hash Surface
Problem: The batch says every text block needs FNV-1a hashes while project localization names LocHash as UTF-16LE.
Solution: `HashID` is decimal uint FNV-1a over the final `Text`; `RequiredGlobalStateHash` is decimal uint FNV-1a over the condition string.
Rejected Alternatives: Hashing only `LineID` was rejected because the prompt specifically says text block. Hashing at runtime was rejected by zero-GC localization rules.
Scalability potential: Low tier can resolve direct text hashes; Middle/High/Ultra can use `LineID` and source hashes to map clean/dirty voice banks without recomputing.
Hardware Impact: Static estimate only: offline hashing removes managed string traversal from load-time utility paths on low-end silicon.

## Decision: Clean Variant
Problem: Age-rating variant must exist without creating a second manually edited lore source.
Solution: Generator applies a bounded profanity replacement table, recomputes clean `HashID`, and stores `SourceHashID` for traceability.
Rejected Alternatives: Manual clean copy was rejected because it creates drift and uncaught hash mismatches.
Scalability potential: Low ships one selected variant; High/Ultra can carry both and switch by rating/profile without code changes.
Hardware Impact: No runtime filter cost; all replacement work is offline.

## Decision: Emotion Tags
Problem: DSP modulation needs a stable voice-filter hint without parsing prose.
Solution: Every line has `EmotionTag` constrained to `[STRESS]`, `[CALM]`, or `[PANIC]`.
Rejected Alternatives: Putting tags only inside subtitle text was rejected because UI may need clean text while DSP still needs modulation metadata.
Scalability potential: Low maps tags to one filter preset; Middle adds per-speaker pitch/EQ; High/Ultra can layer pressure distortion and radio breakup per tag.
Hardware Impact: Static estimate only: direct metadata avoids string scanning of subtitle text on low-end CPUs.

## Decision: No Unity Code
Problem: The prompt explicitly says no `.cs`, and the domain is data/lore.
Solution: Implemented only a Python generator and JSON outputs; no Unity runtime, editor script, prefab, scene, or asset YAML was touched.
Rejected Alternatives: C# importers or ScriptableObject assets were rejected because they increase compile/import risk and violate the batch task.
Scalability potential: Data remains portable across future runtime loaders.
Hardware Impact: No frame-time impact; no new assembly reload cost beyond normal file presence.

## Cultural Background: Marauders
Problem: Marauder radio needed a culture, not generic sci-fi barks.
Solution: Authored them as salvage workers, ex-riggers, blacklisted mechanics, and contract-breakers who treat procedure as survival law. Their slang comes from air debt, bad seals, pressure shadows, and corporate abandonment. The rival Black Keel crew exists to make the Leviathan hunt audible before it becomes visible.
Rejected Alternatives: Heroic military chatter was rejected because HECTON-8 is not a clean command fantasy. Random pirate dialect was rejected because it would break the NASA-punk industrial tone.
Scalability potential: Low uses static subtitles and radio filters; Middle adds speaker-specific DSP; High layers more channel breakup and panic EQ; Ultra can stack pressure distortion, occlusion, and Leviathan proximity modulation while using the same JSON contract.
Hardware Impact: Low-end i3/MX350 pays only for loading static text. High-end machines can spend saved runtime CPU on richer audio presentation and voice-filter overkill.

## Decision: Omega Polish Status
Problem: Core task 15 required `DIALOGUES BAKED`, then Omega polish required final status `VERIFIED MASTER GRADE`.
Solution: Preserve `CoreStatus: DIALOGUES BAKED` and set final validation/status to `VERIFIED MASTER GRADE` with evidence class `STATIC_JSON_CLI`.
Rejected Alternatives: Claiming Unity/runtime verification was rejected because no Unity Console, PlayMode, profiler, or GCMonitor was run for this data-only task.
Scalability potential: Status metadata remains honest for downstream ingestion gates.
Hardware Impact: No hardware impact; documentation and validation metadata only.

## Decision: SHINOBU Binary Cache
Problem: JSON-only radio data forces downstream import or runtime parsing work before stateless lookup.
Solution: Added `marauder_radio_interceptions.h8bin` with Little-endian `<4sHH14I` header and fixed `<32I` records. Header is 64 bytes, records are 128 bytes, payload offset is 16-byte aligned, every text/low/clean payload slice is 16-byte aligned, file length is 16-byte aligned, and Ultra data is packed into in-record Q8/RGBA32 fields.
Rejected Alternatives: Keeping Ultra data only in JSON was rejected because the user explicitly required SHINOBU zero-cost ingest. Variable-width records and Ultra JSON slices were rejected because record stride must stay cache-predictable and not force downstream parsing.
Scalability potential: Low tier reads stripped `LowTierText` slices; Middle/High read full text and emotion state; Ultra reads packed harmonic weights, octave counts, spectral gradient RGBA32 stops, glitch mask, radio breakup, and signal hash from fixed record fields.
Hardware Impact: i3/MX350 avoids a runtime JSON walk for this radio node. High-end machines can use the same binary to drive richer radio/noir distortion without extra authoring files.

## Decision: Fixed-Width Ultra Payload
Problem: The first binary hardening pass still stored Ultra metadata as aligned UTF-8 JSON payload slices. That was aligned, but it was not cold enough for a zero-cost cluster ingest target.
Solution: Repacked Ultra metadata into the `<32I>` record as integer fields: noise seed, Q8 harmonic weights, Q8 octave counts, four RGBA32 spectral gradient stops, Q8 subtitle glitch mask, Q8 radio breakup, Ultra signal hash, and High-tier radio noise seed. Current verifier report: 15 records, 128-byte record size, 7872-byte file, 5888-byte payload, zero binary errors.
Rejected Alternatives: Leaving the Ultra blob as payload text was rejected because downstream systems would still need a variable-length lookup and parse step. Adding a second Ultra table was rejected because it creates another pointer-chasing surface for 15 lines.
Scalability potential: Low ignores the Ultra fields and uses capped low text. Middle/High use hashes and voice-state metadata. Ultra consumes fixed fields directly for richer channel breakup, harmonic noise, and gradient-driven subtitle distortion.
Hardware Impact: Low-end silicon keeps a predictable 128-byte stride and avoids JSON parse work. High-end hardware gets richer packed visual/audio control without changing the lookup contract.

## Decision: Math Audit Boundary
Problem: The reset demanded Beer-Lambert/Dalton/Sabine provenance for LUTs/matrices, but this task owns radio dialogue, not optical, gas, or acoustic-RT60 tables.
Solution: Validation report explicitly marks `LutMatrixSurface: NONE_IN_SCOPE` and derives the only numeric model here: subtitle pacing from `171 / 60` words/sec, `650 / 1000` radio lead-out seconds, and `2400 / 1000` subtitle floor seconds. The report now carries provenance text for each pacing constant.
Rejected Alternatives: Inventing fake physics provenance for dialogue timing was rejected as false evidence.
Scalability potential: Pacing stays deterministic for all tiers; low tier uses shorter text while keeping full text available.
Hardware Impact: No runtime math burden; values are pre-baked.

## Decision: Source Hygiene Verifier
Problem: Binary safety claims were still partly implicit because the verifier checked output layout but not the generator's struct declaration surface or header hashes.
Solution: Extended `VerifyMarauderRadio.py` to audit `struct.Struct` format strings, reject direct `struct.pack`, verify all radio `.h8bin/.bin` sizes are 16-byte aligned, compare binary header canonical hashes against raw/clean/dictionary/layout JSON, decode payload slices, and compare packed Ultra fields against JSON source.
Rejected Alternatives: Trusting the layout JSON alone was rejected because layout docs can drift from pack code. Runtime importer proof was not claimed because no Unity loader was executed.
Scalability potential: Low keeps a narrow predictable binary path; Ultra can consume rich packed fields without parser work.
Hardware Impact: i3/MX350 avoids endian/layout guesswork at ingest. High-end presentation gets deterministic packed data without extra metadata files or variable payload parsing.

## Decision: Lore Tone Verifier
Problem: Slang and NASA-punk noir tone were previously validated mostly by prose inspection.
Solution: Added `LoreToneAudit` to the verifier: all five slang terms must appear, sterile/off-tone tokens are rejected, and emotion distribution is reported.
Rejected Alternatives: Manual tone policing only was rejected because future edits could silently clean the language back into generic sci-fi.
Scalability potential: The same authoring node now guards low-tier subtitle cuts and high-tier DSP metadata against tone drift.
Hardware Impact: Offline-only audit; 0 runtime hardware cost.

## Decision: Economy Monte Carlo Gate
Problem: The reset requested proof that recipes do not create an infinite resource loop.
Solution: `VerifyMarauderRadio.py` audits `Data/Economy/Recipes.json` for 1,000,000 seeded craft/deconstruct steps. Latest result: steps=1,000,000, crafts=57,916, deconstructs=41,304, errors=0, primitive value growth=0.0, normal reclaim ratio=0.8.
Rejected Alternatives: Static reclaim-ratio reasoning alone was rejected because the user explicitly demanded Monte Carlo evidence.
Scalability potential: The verifier remains offline and can be rerun without changing runtime systems.
Hardware Impact: No runtime hardware impact; this is an offline data gate.

## Decision: Monte Carlo Verifier Efficiency
Problem: A stricter verifier version rebuilt the craftable recipe list every step and timed out after 604 seconds. That is unacceptable verifier cost even for an offline gate.
Solution: Replaced per-step full craftable-list rebuild with bounded random recipe probing. The final 1,000,000-step pass completed and produced 57,916 crafts plus 41,304 deconstructs with zero primitive-value growth.
Rejected Alternatives: Keeping the timed-out verifier was rejected because it creates process debt and tempts future agents to skip the gate.
Scalability potential: The verifier can remain a real million-step gate without becoming a batch blocker.
Hardware Impact: No runtime hardware impact; CI/offline verification cost is reduced while preserving the evidence target.

## Decision: Atlas / H-Phi Fit
Problem: The reset referenced an 85-domain map, but current disk `Docs/PROJECT_ATLAS.md` states `83` first-party asmdef files.
Solution: Validation and verify reports record the current disk count as 83 and state the correction. Radio data adds 0 runtime dependencies, 0 Core refs, and no private runtime state requirement. Data sovereignty impact is positive because the node is stateless hash records plus aligned payload slices.
Rejected Alternatives: Repeating the user-provided 85 count without disk evidence was rejected.
Scalability potential: The node fits UI/Narrative localization data and does not pull simulation domains into Core.
Hardware Impact: Stateless lookup path is cache-friendly on low-end hardware and does not reduce high-end visual/audio headroom.
