# Rationale - NARRATIVE_LORE_WEAVER

Status: PENDING VERIFICATION for Unity/runtime integration
Evidence class: STATIC_DOC / FILESYSTEM / PYTHON_BINARY_VALIDATION

## Initial Decisions

Problem: The batch needs narrative text while the runtime localization path requires deterministic hash lookup.
Solution: Author content as data-only localization records with `LocID`, layer metadata, and precomputed FNV-1a hashes matching `Hecton.Localization.LocHash.Compute` semantics: FNV-1a 32-bit over UTF-16 code units, low byte then high byte.
Rejected Alternatives: A flat `Dictionary<string,string>` only would hide hashes from the UI_LOCALIZATION_BABEL handoff. Editing `LocalizationKeys.cs` or generated C# would violate `ZERO C# TOUCH`.
Scalability potential: Low tier streams compact UTF-8 bytes through a header table; Middle adds broader narrative paging; High and Ultra can spend the same data on richer PDA/visor presentation, TTS, and stress-triggered hallucination swaps without changing lookup shape.
Hardware Impact: On i3/MX350, the binary blob avoids runtime JSON parsing in the hot path. Expected runtime saving is lookup architecture dependent and remains PENDING VERIFICATION without profiler proof.

Problem: The lore mentions pressure, O2, narcosis, flora motion, and leviathan influence.
Solution: Use project-defined fakes: Dalton scalar gas language, stress/hypoxia/nitrogen narcosis as data-driven symptoms, abyssal flow as VFX/audio/AI cue, and leviathan dread as scanner/PDA presentation instead of new simulation.
Rejected Alternatives: Real chemistry, per-bubble pressure truth, per-frond physical flora reaction, or live voice-event systems. Those belong to other domains and would violate the text/data boundary.
Scalability potential: Low uses text and static corrupted variants; Middle can add subtitle corrosion; High and Ultra can bind the same records to voice synthesis, richer diegetic animation, and visual overkill presentation.
Hardware Impact: Data-only content has no frame cost by itself. Binary table creation is offline tooling; runtime impact remains PENDING VERIFICATION.

## Tasks 1-5 Decisions

Problem: The Marauder faction needed moral separation from piracy without adding new game systems.
Solution: Defined Marauders as salvage engineers and evidence recoverers using procedure as identity: exits, air, gauges, records. Deep Reach remains the dead Corp whose liability language makes the colony feel hostile even after collapse.
Rejected Alternatives: A heroic resistance faction would soften the noir tone. A pure pirate faction would weaken the ethical line between player salvage and Corp exploitation.
Scalability potential: Low tier shows this through PDA text only. Middle can tie entries to scanner discoveries. High and Ultra can add diegetic terminal animation, voice synthesis, and stress-corrupted overlays.
Hardware Impact: 0 us runtime code cost in this pass. Runtime cost is limited to future localization lookup and UI rendering; profiler proof absent.

Problem: Audio logs needed fear and industrial exhaustion without creating runtime audio dependencies.
Solution: Wrote 15 short transcripts plus paired SSML records for future TTS. The SSML stays plain data; no AudioSource, event string, or DSP node was touched.
Rejected Alternatives: Creating audio event names or clip references would cross into audio ownership and violate the prompt's text/data boundary.
Scalability potential: Low uses subtitle text. Middle can use compressed voice clips later. High and Ultra can route SSML into richer spatial/diegetic playback after audio-domain ownership.
Hardware Impact: 0 us runtime code cost now; future voice playback budget remains PENDING VERIFICATION.

Problem: Scanner entries could imply expensive simulation if written as literal physics.
Solution: Biological uses are practical, while flora/current reactions are explicitly presentation-first: global shader sway, VFX particles, and AI-readable cues before physical truth.
Rejected Alternatives: Per-frond current simulation, per-fish oxygen physiology, or real chemical gas diffusion language. Those create false runtime promises.
Scalability potential: Low shows one clean scanner paragraph. Middle can add icons/resource extraction hints. High and Ultra can buy visual overkill with richer shader animation and denser near-field dressing.
Hardware Impact: 0 us runtime code cost in this pass. Avoided committing the project to per-entity simulation cost.

## Tasks 6-10 Decisions

Problem: Alpha Leviathan needed to feel terrifying without defining mechanics owned by AI/fauna agents.
Solution: Wrote it as a redacted convergence file: acoustic carrier, prey blackout, hull stress without contact, route editing, and unreliable survivor testimony under hypoxia/narcosis.
Rejected Alternatives: Exact size, attack table, boss phases, or creature AI claims. Those would create direct dependencies outside the writer domain.
Scalability potential: Low uses dossier text and scanner dread. Middle can add HUD corruption swaps. High and Ultra can spend visuals on route-light failure, flora dimming, acoustic pressure, and richer encounter presentation.
Hardware Impact: 0 us runtime code cost now. Future encounter cost remains PENDING VERIFICATION.

Problem: The binary handoff needed exact hash/offset/length math.
Solution: Implemented `Tools/LocToBinary.py` with a fixed little-endian `H8LB` header, 12-byte records, payload-relative offsets, duplicate LocID checks, FNV collision checks, and slice verification.
Rejected Alternatives: Manual binary assembly, a flat JSON handoff, or ASCII-only hashes. The existing C# `LocHash.Compute` uses UTF-16 code units, so ASCII-byte hashing would be a contract bug for any future non-ASCII key.
Scalability potential: Low streams compact UTF-8 and does not need runtime JSON. Middle can memory-map the payload. High and Ultra can prefetch visible narrative slices and bind them to richer PDA presentation.
Hardware Impact: Output is 24,729 bytes total with 23,725 bytes payload after briefing expansion. Runtime microsecond savings are not claimed without the UI consumer profiler path; file-level verification is complete.

## Tasks 11-14 Decisions

Problem: Stress-corrupted text could become an allocation-heavy runtime text generator if interpreted loosely.
Solution: Authored five fixed `madness_variant` localization records for `PlayerStress01 > 0.9` selection. The runtime can swap by LocID/hash without composing strings.
Rejected Alternatives: Procedural hallucination strings, random substring corruption, or UI-side text mutation. Those create GC risk and make localization QA unstable.
Scalability potential: Low uses rare static subtitle swaps. Middle can add controlled glyph dropout. High and Ultra can spend saved CPU on visor distortion, audio filtering, and richer panic presentation while keeping the text payload deterministic.
Hardware Impact: 0 us runtime code cost in this pass. Consumer selection cost is PENDING VERIFICATION.

Problem: SSML/emotion tags can accidentally become audio-system ownership.
Solution: Stored SSML as 15 paired localization records only. They are future voice direction metadata, not clip references or audio events.
Rejected Alternatives: AudioSource setup, MasterAudio event names, DSP node routing, or clip imports. Audio runtime ownership stays with the audio domain.
Scalability potential: Low displays text only. Middle can use pre-rendered compressed voice later. High and Ultra can route SSML to richer diegetic voice and spatial processing after profiler proof.
Hardware Impact: 0 us runtime code cost now; future playback cost unmeasured.

Problem: Lore consistency needed objective proof, not a claim in chat.
Solution: Added `LORE_CONSISTENCY_AUDIT` to the localization payload and mirrored the audit in `Docs/Design/Lore_Bible.md`. The audit binds Marauders, Deep Reach, Dalton gas language, pressure limits, current-field fakes, scanner categories, and Alpha Leviathan redaction into one reviewable record.
Rejected Alternatives: Chat-only consistency notes or mechanics promises owned by AI/physics/fauna agents.
Scalability potential: Low uses the audit as writer QA. Middle can feed it into localization review. High and Ultra can use the same constraints for voice, PDA animation, and environmental staging.
Hardware Impact: 0 us runtime code cost; audit is static data.

Problem: The prompt explicitly forbids C# edits.
Solution: Verified focused project C# diff is empty and kept the work to markdown, Python, JSON, and binary data.
Rejected Alternatives: Generated C# key constants, runtime localization loader edits, or GlobalRegistry changes. Those would violate task ownership.
Scalability potential: Low/Middle/High/Ultra all consume the same data contract through the future localization owner without new writer-domain code.
Hardware Impact: 0 us project C# delta. Runtime integration remains PENDING VERIFICATION. Broad diff currently reports an unrelated deleted `.codex_tmp/huv_20c82c86.cs`; it was not restored or modified by this pass.

## Task 15 Decision

Problem: The CTO-facing report cannot live only in chat.
Solution: Added `Docs/AgentLogs/LOG_NARRATIVE_LORE_WEAVER.md` with the required breakdown: wrong state, completed work, cinematic cheats, binary facts, verification, regression model, and failure modes.
Rejected Alternatives: Chat-only handoff, fake profiler metrics, or claiming runtime success from static data.
Scalability potential: Low-tier handoff remains compact data. Middle/High/Ultra teams can consume the same logged contract for UI, VO, and presentation upgrades without changing writer output.
Hardware Impact: 0 us runtime code cost. Exact measured runtime microseconds saved remain PENDING VERIFICATION because no Unity profiler path was executed.

## Polish Pass Decision

Problem: The Omega polish pass could only be parsed after all core tasks were checked.
Solution: Parsed `CURRENT_BATCH.md` after the checklist reached 15/15. No `<POLISH_MANDATE>` tag exists, so the final pass was a self-inquisition: JSON parse, placeholder scan, category count validation, independent binary parser, C# diff scan, and compile-tool availability check.
Rejected Alternatives: Reading polish instructions early, skipping the final pass, or treating Python/file validation as Unity runtime proof.
Scalability potential: The data contract remains compact for Low tier and unchanged for richer High/Ultra presentation work.
Hardware Impact: 0 us runtime code cost. `dotnet build` could not run because `dotnet` is not available in this shell; compile status remains PENDING VERIFICATION. Focused project C# diff remains empty.

## Hard Review Decision

Problem: Review against `LocRegistry.ClassifyLayer` found 43 authored entries with prefixes that would not land in the Narrative pool if converted into the current runtime table path: `DATAPAD_*` and `SCANNER_*`.
Solution: Renamed those LocIDs to runtime-recognized narrative prefixes: `PDA_DATAPAD_*` for data pads and `LORE_SCANNER_*` for scanner lore. Regenerated hashes and `en_US.bin`.
Rejected Alternatives: Leaving metadata-only `Layer: Narrative` and assuming a future consumer would respect it. Current runtime classification is prefix-based, so that would be a concrete integration risk.
Scalability potential: Low-tier runtime pool sizing stays aligned with intended narrative residency. Middle/High/Ultra presentation can still use the same category metadata for UI grouping.
Hardware Impact: No runtime code changed. The fix prevents future narrative records from being misclassified into Core if the localization owner flattens this payload.

Problem: Identifier maintenance needs hash regeneration without weakening normal verification.
Solution: Changed `LocToBinary.py` so `--normalize` permits intentional hash rewrite after LocID edits, while normal `--verify-only` still rejects any JSON hash mismatch.
Rejected Alternatives: Manual hash edits or allowing mismatches during normal verification.
Scalability potential: Offline maintenance remains deterministic and cheaper to audit.
Hardware Impact: Offline tool only; 0 us runtime code cost.

Problem: The requested 2-page faction briefing was underweight at 562 words.
Solution: Expanded the Marauder/Deep Reach briefing to 911 words, focusing on crew ledgers, field hierarchy, corporate-language translation, and first-hours player guidance without adding mechanics outside writer ownership.
Rejected Alternatives: Leaving the briefing thin or padding with generic prose. The expansion adds usable faction doctrine and UI/lore direction.
Scalability potential: Low tier still displays static text; Middle/High/Ultra can spend richer PDA animation or VO on the same stronger briefing.
Hardware Impact: Blob grew to 24,729 bytes total / 23,725 bytes payload. Runtime resident impact is still negligible by asset scale but remains PENDING VERIFICATION in Unity.

Problem: Original prompt re-extraction failed during the second review.
Solution: Verified `CURRENT_BATCH.md` no longer contains `NARRATIVE_LORE_WEAVER`; ignored the unrelated new batch and relied on saved status/rationale/log plus produced files.
Rejected Alternatives: Letting the new batch influence this agent's work or pretending re-extraction succeeded.
Scalability potential: Disk logs remain the durable memory source for this completed assignment.
Hardware Impact: None.

Problem: The five hallucinated stress entries did not explicitly carry the `PlayerStress01 > 0.9` trigger in the data itself.
Solution: Added `Trigger: PlayerStress01 > 0.9` to all five `madness_variant` records. Existing runtime already has `MADNESS_WHISPERS_01..15`, so these entries remain narrative handoff variants unless a localization/UI owner wires them.
Rejected Alternatives: Renaming them to `MADNESS_WHISPERS_*` would collide conceptually with existing runtime-owned keys. Adding C# resolver logic would violate the zero-C# boundary.
Scalability potential: Low tier can consume these as direct stress swaps. Middle/High/Ultra can bind the same variants to richer PDA/visor corruption presentation.
Hardware Impact: JSON metadata only; binary payload and runtime code cost unchanged. Unity integration remains PENDING VERIFICATION.
