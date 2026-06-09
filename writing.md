# HECTON-8 Writer-Screenwriter Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: in-world articles, encyclopedia pages, survivor diaries, terminal notes, black-box fragments, scanner/codex entries, technical explainers, mineral notes, ship/drive articles, public lore pages, and multilingual AppliedContent text packets.

## Prime Law

Write artifacts, not specifications.

Every text must feel like it was written by a person, institution, survivor, technician, insurer, Marauder, journalist, public archivist, or machine interface inside the world. It must not read like an AI-generated design note explaining what the packet is for.

The reader should believe the text existed before the player found it.

## Authority Route

Use this file with:

- `narrative.md` for missions, evidence order, quest state, logs, and black-box truth;
- `Docs/Lore/Lore_Bible.md` and `Docs/Lore/Canon_Locks.md` for canon;
- `Docs/Lore/Lore_Content_System.md`, `Docs/Lore/Lore_Localization_Model.md`, and `Docs/Lore/Lore_Multilingual_Content_Architecture.md` for Article ID, LocID, surface, source-voice, spoiler, and locale packet structure;
- `localization.md` for string IDs, fallback, RTL/CJK, subtitles, and runtime text behavior;
- `textes.md` only for real-world public marketing/community copy.

This file owns prose quality, genre voice, realism, anti-AI wording, and multilingual package shape. It does not own simulation truth, quest state, UI runtime, or public readiness claims.

## First-20 Route Hook

- First-20 moment: first scanner result, terminal note, black-box fragment, field note, or codex unlock that makes the opening route's resource, tool, hazard, or return-path decision more concrete.
- Route blocker removed: prevents first-route text from becoming generic lore, AI-sounding packet prose, or exposition detached from a physical evidence object.
- Proof class: STATIC_DOC hook only; acceptance still requires source brief, surface-specific artifact text, canon sources, locale status where production content is requested, and runtime/UI proof only when the text is implemented.

## Writer And Screenwriter Roles

The writer and the screenwriter may be the same person, but they are not the same mode of work.

Screenwriter mode owns the situation:

- what the player has just done;
- what the player can see, touch, hear, scan, or doubt;
- what evidence exists before text appears;
- what decision, route, danger, or emotional pressure the text changes;
- which speaker/source has access to this knowledge.

Writer mode owns the artifact:

- the exact voice of the person, institution, instrument, or archive;
- the visible prose;
- the omissions, bias, broken context, and physical marks;
- the English authority text;
- the 15-locale delivery shape.

Do not start in Writer mode. First define the scene, unlock, evidence object, and knowledge boundary. Then write the artifact. A beautiful paragraph attached to no object, no scene, and no player need is rejected.

If one agent does both roles, it must explicitly pass these gates:

1. Scene gate: where is the player and why does this text appear now?
2. Evidence gate: what physical or system artifact proves the text belongs here?
3. Knowledge gate: what can this source know, and what must remain unknown?
4. Voice gate: what exact human/institution/instrument wrote or recorded it?
5. Surface gate: scanner, terminal, codex, audio, website, wiki, note, dossier, or black box.
6. Localization gate: stable IDs, English authority text, and all 15 locale rows or explicit draft status.

## Required Writer Inputs

Before writing any article, diary, note, log, or encyclopedia entry, define:

- speaker or institution;
- audience;
- date or approximate era;
- surface: external site, in-game wiki, scanner, terminal, audio, black-box, survivor diary, corporate memo, field note;
- unlock context;
- what the writer knows;
- what the writer does not know;
- what the writer is hiding, if anything;
- physical object or evidence that carries the text;
- gameplay reason the player reads it now.

If those facts are missing, write a short source brief first. Do not invent omniscient prose.

## Source Brief Template

Every substantial content request should begin with a compact source brief. Keep it short, but do not skip fields.

```text
Packet ID:
Article ID:
Loc namespace:
Runtime layer:
Surface targets:
Spoiler level:
Canon sources:
Speaker/source:
Audience:
Date/era:
Location/depth/route:
Unlock context:
Evidence object:
What this source knows:
What this source does not know:
What this source hides or gets wrong:
Player use:
Forbidden facts:
Required proper nouns/units:
LocIDs:
Localization status:
```

No field may be filled with vibes. If the answer is unknown, write `UNKNOWN - DO NOT INVENT` and either stop or make discovery the task.

A source brief is an input or blocker, not the final artifact, unless the user explicitly asked only for a source brief.

## Production Writing Loop

Use this order for real work:

1. Read `PROJECT_BIBLES.md`, `narrative.md`, `localization.md`, this file, and exact canon sources in `Docs/Lore`.
2. Extract only the canon facts needed for the packet.
3. Choose the source brief and surface contract.
4. Draft the English authority text per surface.
5. Cut generic openings, explanations of theme, and authoring notes.
6. Add concrete object/route/tool/body/procedure detail only where the source could know it.
7. Build the 15-locale rows from the same meaning; mark non-native translations as draft.
8. Check length/shape risk for scanner, PDA, terminal, subtitles, and website.
9. Provide a proof packet: sources used, IDs, surfaces, locale roster, native-review status, forbidden facts avoided.

The loop must produce content, not a proposal about content. A task asking for a diary should end with a diary. A task asking for a technical article should end with a readable technical article, not notes about the future article.

If the same content problem repeats after one rewrite pass, stop polishing phrasing and change the source route: revise the scene/evidence/knowledge boundary, choose a different surface or speaker, or report the exact missing canon/source blocker.

Localization lock: AppliedContent and major in-world content should be prepared for all 15 supported locales immediately. Non-native or machine-assisted text must be labeled honestly through status/frontmatter/index fields, never as player-visible disclaimers and never as native-final without proof.

## Surface Truth Contract

One packet can export to many places, but every place must sound like its own artifact.

Use the same canon facts, then change the source and reader:

- Game scanner: short observed fact, confidence, hazard, action limit.
- PDA/codex: useful recovered knowledge after unlock, not omniscient encyclopedia.
- Terminal: in-world document with headers, routed action, stale authority, missing body cost.
- Audio log: spoken fragment under interruption, not a paragraph read aloud.
- Black box: telemetry, event marker, contradiction, damaged transcript.
- Survivor diary: next-hour need, object/person/route, wrong assumption, fatigue.
- Marauder note: practical correction, route pressure, contempt for bad data, no poetry.
- Deep Reach memo: clean liability language, defensible lie, no villain confession.
- Atlas fragment: classification/repair/routing language, not emotional villain dialogue.
- External site/wiki: public readable article with spoiler gate and no fake marketing claim.

Do not reuse the same paragraph across surfaces with only headings changed. That is spreadsheet prose.

## Anti-AI Prose Ban

Reject text that sounds like a packet summary, content brief, or AI article.

If a draft matches the patterns below, do not polish it. Reject the source route and rewrite from the source brief: scene, evidence object, speaker, surface, and knowledge boundary. The failure is structural, not cosmetic.

Hard-ban patterns unless quoted as bad corporate copy:

- "`X` defines/explains/shows why..."
- "This entry explores..."
- "serves as a reminder"
- "a testament to"
- "more than just"
- "at its core"
- "in a world where"
- "a delicate balance of"
- "a unique blend of"
- "both beautiful and terrifying"
- "the real horror is..."
- "not just X, but Y"
- "X turns Y into Z" when used as generic meta-description;
- field notes that tell the writer how to use the article;
- scanner text that summarizes authoring intent instead of sensor output;
- audio lines that sound like trailer taglines;
- encyclopedia pages that read like root design docs.

Hard-reject HECTON-8-specific AI smell:

- abstract merger thesis: "infrastructure stops separating from habitat", "the system becomes one body", "boundary labels are invalid", or any equivalent category-collapse statement without a literal machine, room, sample, or organism doing an observable action;
- organic metaphor spam: "one skin", "one tissue", "one body", "corridor as gut", "wall as organ", "cable blooms", "membrane shares..." unless the text is literal xenobiology or a named character's believable phrase and the scene proves it;
- proper-noun pileups that name Deep Reach, Atlas, blue debt, workers, ocean biology, membranes, and tools in one sentence without an owner action, timestamp, route, or physical consequence;
- repeated "X can be Y" rhythm used to fake discovery instead of describing what the player can inspect;
- fake metaphysical conclusion: "not mutation but function", "without human categories", "loss routed", "continuity protected", or similar abstract verdicts not spoken by a justified machine interface;
- pseudo-terminal all-caps with abstract labels instead of real fields, values, warnings, owners, timestamps, and failure codes;
- scanner lines that contain philosophy, theme, or taxonomy jokes instead of observed material, confidence, hazard, and limitation;
- audio stingers such as "it still works" or "it uses what fell into it" unless attached to an actual damaged recording with a speaker, place, interruption, and concrete fact;
- player-facing text that mentions what the player should learn, how the article should be used, or why the packet exists;
- legal/corporate mistranslation where a procedural claim, custody claim, insurance claim, or salvage claim becomes a courtroom plaintiff unless the canon source is literally a lawsuit.

Russian and Ukrainian rows must specifically reject "язык истцов", "мова позивачів", "конверсия истца", and similar legal mistranslations for claim/custody/insurance language. Use procedural equivalents such as "претензионный язык", "язык претензий", "мова претензій", or a more exact local phrase tied to the source context. If the English authority phrase cannot be localized without this failure, rewrite the English source first.

Strong HECTON-8 prose uses concrete nouns, dates, quantities, roles, failure states, custody marks, stains, gaps, and procedural pressure.

## LLM Style Suppression Law

Current AI-text detection research is not reliable enough to approve prose, especially after paraphrase, translation, or mixed human/AI editing. HECTON-8 therefore controls AI smell at authoring time, not by trusting a detector after the fact.

For every writer, translator, or scenario agent:

- detection is triage only; manual redline is acceptance;
- stylometric suspicion is actionable: uniform sentence length, repeated balanced clauses, over-clean paragraph closure, vague abstract subjects, low punctuation variety, and identical rhythm across surfaces require rewrite;
- prevention beats repair: constrain the draft with speaker, object, route, surface, and forbidden facts before prose starts;
- examples beat adjectives: the prompt must carry bad examples to reject and good source-grounded examples to imitate;
- line editing is mandatory: final 5-10 percent of prose is hand-redline work, not another generation pass;
- old text is not grandfathered. If it fails the current firewall, rewrite it or mark it blocked.

Forbidden LLM sentence shapes:

- balanced thesis: "`X` is not merely `A`; it is `B`";
- fake escalation: "`X` becomes `Y`, `Z`, and finally `theme`";
- abstract subject: "the system", "the colony", "the ocean", "the process", "the factory" doing a moral or metaphysical action with no owner;
- generalized contrast: "danger is not random; it is..." unless a specific source can prove that distinction;
- aphoristic closer: final sentence that sounds quotable but carries no new evidence;
- noun stack without verb pressure: many proper nouns arranged as mood instead of action;
- metaphor chain: more than one metaphor in a paragraph, or any metaphor that hides missing mechanics.

Required HECTON-8 sentence shapes:

- actor + operation + constraint: "Shift K-12 locked Pump 4 open because the return gauge would not settle below 31 MPa.";
- object + trace + consequence: "The lower hinge has white salt under the paint; pry from the top or the seal tears first.";
- source + limit: "Atlas tagged the packet as cargo loss. The terminal never saw the two suit IDs still inside the bay.";
- human + procedure + omission: "Mara signed the return form because the loader was already flooding. She left the handwheel number in the margin.";

Beauty rule: beautiful language is allowed only when it is exact. Atmosphere comes from pressure, light, salt, worn tools, bad forms, missing people, delayed signals, and local sensory truth. It does not come from slogans, soft philosophy, or polished AI cadence.

## Creative Freedom Envelope

Strict anti-AI rules do not ban voice, rhythm, dread, humor, anger, grief, or beauty. They ban unsupported abstraction.

Creative language is allowed only when it is anchored to all four points:

- source: a named person, institution, instrument, document class, archive, or machine route;
- object: a physical thing, room, route, sample, form, sensor, body trace, tool, seal, gauge, locker, clamp, container, hull plate, pump, or signal;
- pressure: time, debt, oxygen, pressure rating, heat, mass, custody, liability, contamination, blackout, route loss, or maintenance cost;
- limit: what the source cannot know, cannot admit, cannot measure, or cannot safely say.

The writer may use metaphor only after the literal object is already clear. One exact metaphor can survive. A chain of metaphors is usually AI cover for missing evidence.

Good HECTON-8 beauty:

- a practical sentence that reveals a lie;
- a worn object that carries history without explaining it;
- a human omission inside a clean form;
- a machine field that is technically correct and morally useless;
- a local sensory detail that changes how the player reads a room.

Bad HECTON-8 beauty:

- a quotable closer;
- a universal statement about the ocean, colony, humanity, systems, or survival;
- a balanced analogy;
- a paragraph that would still work if every HECTON-8 noun were replaced by generic sci-fi nouns;
- any sentence whose main job is to sound deep.

Voice freedom by surface:

| Surface/source | Allowed flavor | Must still contain |
|---|---|---|
| Scanner | clipped sensor language, uncertainty, material caution | material, confidence/limit, hazard/action |
| Terminal | cold procedure, liability evasion, stale authority | owner, field names, timestamp or route, requested action |
| Survivor diary | fatigue, wrong assumption, practical fear, named care | person/object/route, next-hour need, knowledge limit |
| Marauder field note | sharp correction, contempt for bad data, salvage pressure | tool/route/debt/air/custody detail |
| Public/wiki article | readable history, context, restraint | source boundary, spoiler boundary, factual sectioning |
| Engineering article | precise explanation, dry tradeoff, hard cost | infrastructure, limit, mass/heat/time/maintenance cost |
| Corporate/legal | polished omission, procedural cleanliness | claim/custody/liability term, physical contradiction elsewhere |
| Audio | urgency, interruption, breath, damaged carrier | one concrete fact and a reason the line cuts off or continues |

## Risk Word And Rhythm Firewall

Some words are not banned as dictionary items, but they are high-risk AI perfume. They require a literal source object and a reason to exist.

High-risk English words and habits:

- "echo", "whisper", "haunt", "scar", "wound", "memory", "ghost", "song", "breath", "pulse", "hunger", "dream", "truth", "silence", "ritual", "liminal", "threshold", "synthesis", "convergence", "interplay", "tapestry";
- "quietly" used to make an abstract sentence feel serious;
- "beneath the surface" as a metaphor rather than a literal depth/surface relation;
- "what remains", "what was left behind", "the cost of", "the weight of" when no object or procedure carries the cost;
- paragraph openers that define a concept before naming a place, object, source, or action;
- final sentences written to be quotable rather than informative.

Use these words only when literal, sourced, and useful:

- "scar" is allowed for a hull gouge, tissue sample, route mark, or repaired fracture;
- "ghost" is allowed for a sonar return, UI artifact, stale route ID, or local slang from a named speaker;
- "memory" is allowed for a recorder buffer, custody log, witness statement, or character voice;
- "pulse" is allowed for pressure, signal, power, sonar, pump cadence, or biological rhythm with observed evidence.

If a risky word can be removed without losing fact, remove it. If removing it breaks only the mood, the line was probably filler.

Rhythm rejection:

- three similar-length sentences in a row that all resolve cleanly;
- a paragraph that starts broad, narrows, then ends with a lesson;
- repeated "The [noun]..." openings in encyclopedia tone;
- a scanner, terminal, field note, and public article sharing the same sentence cadence;
- every paragraph ending with a short dramatic sentence.

## AI Phrase Family Quarantine

Do not treat AI smell as a fixed dictionary. Treat it as families of moves that make text sound generated. A banned phrase rewritten with synonyms is still banned if it performs the same move.

Quarantine these phrase families in every language:

- essay-framing: "this article explores", "this entry examines", "this section shows", or any equivalent that tells the reader what the text is doing;
- thesis contrast: "not merely X but Y", "more than X", "at once X and Y", or any equivalent balanced reveal;
- prestige abstraction: "essence", "core", "truth", "legacy", "continuity", "humanity", "meaning", "memory" used without a source object;
- museum-label prose: "stands as", "serves as", "testament", "witness", "reminder", "symbol", "reflection";
- generated transition glue: "however", "moreover", "ultimately", "in this way", "together these", when used to smooth a weak paragraph rather than connect evidence;
- fake sensory fog: "whispers", "breathes", "echoes", "hums", "sings", "hungers", when no actual sound, pressure, power, signal, or organism supports it;
- universal moral closure: final lines about what the ocean, colony, machine, debt, or survival "really" is;
- concept-as-actor: "the system", "the ocean", "the colony", "the process", "the factory", "the debt" acting without a specific owner, mechanism, document, or route;
- category soup: infrastructure, biology, labor, debt, ocean, machine, memory, and loss merged into one abstract sentence.

Replacement rule:

- do not replace a quarantined phrase with a prettier synonym;
- replace it with actor + action + object + constraint;
- if that cannot be done from canon, mark `BLOCKED_SOURCE`;
- if the line was only mood, cut it.

Examples:

```text
AI: The colony's silence stands as a testament to the cost of survival.
Rewrite: Pump Room K-12 has three suit hooks and two tags in the drain tray. Deep Reach filed the room as cleared.
```

```text
AI: The ocean does not merely reclaim the base; it rewrites it.
Rewrite: The north intake has fresh carbonate over yesterday's weld bead. The patch is holding, but the panel number is gone.
```

```text
AI: Together, these fragments reveal a delicate balance between progress and loss.
Rewrite: The invoice lists two replacement seals. The manifest lists one. The missing seal is on the flooded side of the lock.
```

## Living Prose Floor

Anti-AI editing must not flatten the text into sterile documentation. A line can be alive without becoming vague.

A living HECTON-8 line usually has at least one of:

- a specific verb that changes state: locked, bled, buckled, vented, billed, sealed, jammed, tagged, misrouted, flashed, pitted, stripped, fouled;
- a handled object: gauge, form, clamp, boot, tag, locker, cassette, seal, flange, sample bag, valve wheel, cable jacket;
- a small human pressure: hurry, shame, debt, cold hands, bad handwriting, fear of losing the route, a person choosing the cheaper lie;
- a procedural contradiction: the field says safe while the room proves otherwise;
- a sensory fact that a source could know: salt under paint, warm hinge, sour insulation, grit in a glove ring, delayed sonar return.

Do not remove anger, fatigue, humor, grief, or dread. Move them into the source voice:

- survivor: clipped, tired, wrong about one thing;
- Marauder: practical, suspicious, debt-aware;
- corporate: clean language hiding an ugly omission;
- scanner: narrow, material, uncertain;
- public article: clear and restrained, with spoiler boundary.

If every sentence becomes perfectly neutral, the rewrite failed in the opposite direction. The target is specific and alive, not generic and safe.

## Zero-Shot Writer Contract

When asking any writer, translator, or scenario agent for lore, the prompt must not say only "write beautifully" or "make it atmospheric." It must supply constraints that force source-grounded prose.

Minimum zero-shot contract:

```text
Write a HECTON-8 in-world artifact, not a summary.
Surface:
Speaker/source:
Audience:
Location/depth/route:
Evidence object:
Unlock moment:
What the source knows:
What the source cannot know:
What the source hides or mislabels:
Required facts/numbers/units:
Forbidden facts:
Forbidden style:
- no aphorisms
- no "not just X but Y"
- no "at its core"
- no category-collapse thesis
- no organic metaphor unless literal
- no fake terminal/scanner poetry
Acceptance:
- every paragraph attaches to a physical object or document
- every surface has its own voice
- every locale row is manually redlined
```

If the prompt lacks these fields, the output is a draft-risk item even if the prose sounds good.

## Few-Shot Rewrite Pattern Bank

Use examples to train the writer away from AI prose. A few-shot example must show the failure and the repair. Do not include only good prose; the agent must see what is forbidden.

Bad AI lore:

```text
The Bottom Factory is where infrastructure and habitat become one body. Its corridors breathe with the logic of pressure, turning loss into continuity.
```

Why it fails:

- abstract category collapse;
- organ metaphor chain;
- no room, route, timestamp, source, surface, or player-inspectable object;
- "loss into continuity" is a fake thesis, not evidence.

Acceptable terminal fragment:

```text
DEEP REACH MAINTENANCE PACKET 6-14
Bay: Silt Return / East lower service throat
Action: keep Pump 4 open until brine density reads below 1.23.
Exception: two suit tags remain inside the return cage.
Claim language: cargo delay, not personnel delay.
```

Acceptable field note:

```text
East lower return cage: do not cut the blue hose first. It is carrying the pressure readout for the lock above it. Cut the clamp, wait for the gauge to stop hunting, then pull the tag drawer. There were two IDs in mine.
```

Bad AI legal/corporate lore:

```text
The language of plaintiffs proves how the colony weaponized words.
```

Why it fails:

- mistranslates procedural claim language into courtroom plaintiff language;
- tells the theme instead of showing the form;
- no document field or affected worker.

Acceptable corporate/public record:

```text
Return Form KR-77 lists the failed pickup as "partial cargo fitness." The loader manifest beside it lists four crates and one suit locker. Deep Reach only billed the crates.
```

Bad scanner:

```text
The wall is no longer wall or flesh. Boundary tags are invalid.
```

Acceptable scanner:

```text
Scan: pressure-grown carbonate over service panel C-12.
Confidence: 62 percent.
Hazard: panel edge is still conductive.
Limit: cannot classify the pale fiber until a sample is sealed.
```

These examples are pattern law. New examples may be added only when they name the rejected mechanism and the concrete repair.

## Fine-Tune And Example Dataset Policy

If HECTON-8 later uses fine-tuning, retrieval examples, style cards, or a writer memory pack, the data must come from manually redlined pairs, not raw generated output.

Allowed training/example items:

- bad source text marked with the exact failure class;
- rewritten authority text;
- source brief fields used for the rewrite;
- surface type;
- locale status;
- notes on why the repair works.

Forbidden training/example items:

- generated prose accepted because it "sounds better";
- detector-scored text without human redline;
- multilingual rows with copied English or unreviewed idiom;
- examples that only say "make it more human";
- examples with no object, source, pressure, and limit.

The target model behavior is not "more literary." The target behavior is: refuse unsupported abstraction, ask for source facts when missing, and rewrite from physical evidence.

## Paragraph Evidence Firewall

Before localization or packet export, every paragraph must survive this check:

1. What exact object, room, route, organism, interface, document, or person does this paragraph attach to?
2. What action happens, failed, or is being hidden?
3. Who can know this, and why can this source know it?
4. What can the player later see, scan, repair, steal, contradict, or route around?
5. Which words would fail if the proper nouns were removed?

If two or more answers are absent, the paragraph is rejected as AI-style filler. Rewrite from physical evidence instead of adding adjectives.

## Manual Redline Protocol

Every new or old lore fragment that is being accepted, repaired, localized, exported, or published must be read line by line by the writer. Automated checks can help find suspects, but they cannot accept prose.

For every paragraph, surface line, terminal field, scanner line, audio line, field note, title, and locale row, mark it mentally or in the edit pass as one of:

- `KEEP`: concrete source, concrete object, believable voice, useful player or reader value;
- `REWRITE`: valid canon fact, bad wording, weak source voice, bad localization, vague sentence, wrong surface;
- `CUT`: AI-style filler, theme explanation, unsupported metaphor, generic aphorism, repeated summary, no evidence object;
- `BLOCKED_SOURCE`: canon fact missing, source cannot know it, or the English authority row cannot be localized without distortion.

The writer must not carry a sentence forward because it sounds dramatic. A dramatic line survives only if a named source could actually write or say it under the current pressure, route, document, or playback condition.

Manual redline questions:

1. Would this line still mean anything if `HECTON-8`, `Deep Reach`, `Atlas`, and `blue debt` were removed? If yes, treat it as generic unless a concrete source object proves otherwise.
2. Could the same line be said by a survivor, scanner, corporate counsel, Marauder, and public archivist? If yes, the voice is dead.
3. Is the sentence mainly an aphorism, metaphor, analogy, or thesis? If yes, cut it unless the speaker and evidence object justify that exact phrasing.
4. Does the line contain a physical noun the player can find, repair, scan, steal, fear, or contradict? If no, rewrite.
5. Does a translation row preserve the same source voice, or did it become generic literary prose? If generic, rewrite the source or the locale row.
6. Does any non-English row preserve English words only because the agent was lazy? If the terms are not stable IDs, route labels, or intentional in-world labels, rewrite the row.

Manual rewrite sequence for old corpus:

1. Read the current source line.
2. Mark `KEEP`, `REWRITE`, `CUT`, or `BLOCKED_SOURCE`.
3. If `REWRITE`, write the replacement from object/source/pressure/limit.
4. If the English authority row changes, rewrite every locale row from the new meaning.
5. If the old line is dramatic but sourceless, cut it. Do not preserve it as "flavor".
6. Do not batch-accept adjacent paragraphs because one paragraph was fixed.

## Legacy Corpus Rewrite Law

Existing lore, generated pages, production packets, field notes, terminal fragments, scanner text, audio stubs, website articles, wiki pages, and localization rows do not get a pass because they already exist.

When an agent touches an old file or release set, it must:

- inspect the current file, not a stale report;
- read every title, heading, paragraph, surface block, and locale row in scope;
- cut or rewrite AI-style filler instead of adding a new note that says it is bad;
- preserve canon facts, stable IDs, unlocks, source voices, and locale status honestly;
- mark missing canon as `BLOCKED_SOURCE`, not invent connective prose;
- rewrite the English authority row before repairing translated rows when the failure starts in English;
- rewrite a locale row directly when the English source is good but the row contains calque, wrong legal register, copied English, or local AI boilerplate.

A "reviewed" old file with unchanged AI-style prose is not reviewed. It is still dirty.

## Anti-Machine Edit

Run this pass before accepting any draft.

Delete or rewrite:

- generic opening sentence that explains what the article is about;
- summary phrases that could fit any sci-fi setting;
- analogies that are not something this exact speaker would use;
- metaphors that hide missing physical facts;
- emotional labels without behavior or evidence;
- "lore voice" that knows everything and touches nothing;
- repeated sentence rhythm across different sources;
- explanation of what the player should feel;
- fake moral clarity where the source should be partial, tired, evasive, or wrong.

Replace with:

- a job, object, place, route, number, custody mark, tool, failure mode, or physical trace;
- one concrete human mistake or omission;
- one fact the source cannot know;
- one detail the player can later see, scan, repair, steal, or contradict.

Read the draft aloud as the source. If a pump technician, exhausted survivor, scanner, corporate counsel, Marauder, public archivist, and Atlas fragment could all say the same line, the line is dead.

## Human Specificity

Every human-facing text needs at least two grounded details:

- a job title;
- a place name or route label;
- a tool, gauge, seal, pump, locker, plate, clamp, container, wound, sample, badge, or packet;
- a timestamp, shift, pressure rating, mass allowance, temperature, depth band, signal delay, or custody number;
- a sensory fact: condensation line, stale scrubber smell, salt bloom, cracked enamel, dirty visor, wet paper, warped hatch;
- a contradiction between official wording and physical evidence.

Do not explain the entire setting. Make one piece of the setting feel real.

## Genre Contracts

### External Encyclopedia Article

Voice: public, edited, readable, non-spoiler unless marked.

Use:

- short paragraphs;
- source uncertainty where public knowledge is incomplete;
- concrete route, legal, technical, or historical consequence;
- plain definitions before specialized terms.

Avoid:

- terminal all-caps;
- `Scanner/Terminal/Audio/Field Note` sections unless the article is explicitly a multi-surface packet manifest;
- authoring language;
- lore dump with no reader use.

### In-Game Wiki / Codex

Voice: recovered operational knowledge.

Use:

- what the player can do with the information;
- why it matters to route, salvage, survival, evidence, or risk;
- source marker: scan, recovered log, physical sample, black-box decode, Marauder annotation.

Avoid:

- encyclopedia before discovery;
- meta explanation of game design;
- full truth before the player has evidence.

### Scanner Result

Voice: instrument output.

Use:

- observed material;
- confidence;
- hazard;
- immediate action or limitation.

Good scanner text is not poetic. It is useful and incomplete.

### Terminal / Corporate Memo

Voice: procedural, evasive, legally defensible.

Use:

- document ID;
- department;
- requested action;
- liability-safe wording;
- missing human cost.

Corporate text must not confess like a villain. The room or later evidence proves the crime.

### Survivor Diary

Voice: partial, tired, practical, personal without melodrama.

Use:

- what the survivor is trying to do in the next hour;
- a named person, object, or route they care about;
- a wrong assumption they believe at the time;
- sensory detail the camera can later support.

Avoid:

- prophecy;
- complete lore understanding;
- perfect last words;
- speeches about themes.

### Engineering Note

Voice: technician, shipyard, field manual, or maintenance annotation.

Use:

- component names;
- tolerances or pressure classes when useful;
- failure mode;
- workaround;
- cost of the workaround.

Do not write fake technobabble. If the mechanism is not known, write the observable behavior and the maintenance decision.

### Mineral / Resource Note

Voice: field geology, lab intake, salvage valuation, or Marauder handling note.

Use:

- sample source;
- containment requirement;
- contamination risk;
- pressure/temperature history;
- value condition;
- reason it can kill or bankrupt someone.

Never make resources feel magical. Blue debt is frightening because it obeys pressure, custody, contamination, and Atlas classification.

### Technical Article: Ship, Drive, Relay, Engine

Voice: industrial history, engineering note, public explainer, or route manual.

Use:

- what problem the machine solves;
- what infrastructure it requires;
- what it cannot do;
- what it costs in time, mass, shielding, maintenance, or custody.

No miracle engines. No heroic prose. A heavy ship is logistics with heat, debt, and braking windows.

### Audio Log

Voice: spoken under constraint.

Use:

- interruption;
- breath, suit noise, alarm, or transmission artifact when needed;
- one urgent fact;
- human omission.

Audio is not a paragraph read aloud. It is a damaged moment.

### Black-Box Fragment

Voice: telemetry plus human contradiction.

Use:

- state values;
- event marker;
- short transcript or annotation;
- mismatch between system status and reality.

The machine records facts. The horror is in what the facts exclude.

## Content Deliverable Shape

For production content, output a packet that can move between game, site, wiki, notes, audio, and localization without being rewritten from zero.

Minimum packet:

```text
PACKET
Packet ID:
Article ID:
Loc namespace:
Runtime layer:
Canonical title:
Spoiler level:
Canon sources:
Source brief:

SURFACE TEXTS
External site / wiki:
In-game codex:
Scanner short:
Terminal / document:
Audio / transcript:
Field note / Marauder annotation:
Black-box or telemetry fragment:

LOCALIZATION
LocID:
Locale rows with status and text:
en_US [source_authority]:
ar_SA [draft_machine_or_llm]:
de_DE [draft_machine_or_llm]:
es_ES [draft_machine_or_llm]:
fr_FR [draft_machine_or_llm]:
he_IL [draft_machine_or_llm]:
id_ID [draft_machine_or_llm]:
ja_JP [draft_machine_or_llm]:
ko_KR [draft_machine_or_llm]:
nl_NL [draft_machine_or_llm]:
pl_PL [draft_machine_or_llm]:
pt_BR [draft_machine_or_llm]:
ru_RU [draft_machine_or_llm]:
uk_UA [draft_machine_or_llm]:
zh_CN [draft_machine_or_llm]:

QA
Forbidden facts avoided:
Length risks:
Native-review status:
Runtime/site/wiki placement notes:
```

Only include surfaces the task actually needs, but never collapse different surfaces into one generic text. If the task says "article", write the article. If the task says "survivor diary", write the diary. If it says "can go to game/site/wiki/notes", produce separated surface texts or explicitly label which surfaces are not appropriate.

Long articles should have section headings for website/wiki, but in-game codex should stay readable after unlock. Scanner and subtitle text must be separate short forms, not truncated article bodies.

## AppliedContent Packet Shape

Generated packets may contain multiple surfaces, but each surface must be written as its own real artifact.

Bad packet style:

```text
Title
X explains why HECTON-8 is important.

Scanner
One slogan.

Terminal
ALL CAPS SUMMARY.

Audio
Trailer line.

Field Note
Use for art constraints.
```

Required packet style:

- external article reads like an article;
- scanner reads like sensor output;
- terminal reads like an actual terminal or memo;
- audio reads like speech or carrier playback;
- field note reads like a Marauder/technician annotation, not writer instructions.

If writer instructions are needed, put them in source comments or authoring notes, not player-facing text.

## AppliedContent Production Handoff

For production AppliedContent, prose is not done until it is materialized into the content pipeline:

- packet/source JSON or production packet file exists with stable packet ID, Article ID, unlock ID, surface text, and locale status;
- all 15 locale rows contain actual draft text or `BLOCKED_TRANSLATION_DRAFT`;
- target surface files and indexes are updated for `in_game_wiki` and/or `external_site` when those surfaces are in scope;
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` and generated hash constants are refreshed when the packet targets runtime;
- route cards or binding maps exist when the packet is meant to unlock from a POI, scanner, terminal, quest, or scene object;
- exporter/importer/audit output is reported with its real proof class.

A loose markdown article, source brief, future integration note, or English-only packet is not production AppliedContent unless the task explicitly requested draft-only work.

## Multilingual Requirement

Unless the task explicitly requests English source only, AppliedContent writing must plan all 15 project locales:

- `en_US`
- `ar_SA`
- `de_DE`
- `es_ES`
- `fr_FR`
- `he_IL`
- `id_ID`
- `ja_JP`
- `ko_KR`
- `nl_NL`
- `pl_PL`
- `pt_BR`
- `ru_RU`
- `uk_UA`
- `zh_CN`

English source is the authority row. Localized rows must preserve:

- packet ID;
- article ID;
- unlock ID;
- speaker/source;
- facts, numbers, units, dates, names, and custody labels;
- spoiler level;
- gameplay instruction;
- tone class.

Localize meaning, not word order. Do not invent local idioms, jokes, metaphors, or extra lore. Personal names remain identity strings; transliterate only when the locale policy requires it. Units must remain gameplay-readable. RTL locales require layout/fallback awareness. CJK and German expansion require UI length proof where text appears in-game.

Machine translation is draft only. Mark native-review status honestly.

For production content, locale rows must contain actual draft text, not only a plan, placeholder, or "same as English" note. If the agent cannot produce a usable draft for a locale, it must mark that locale `BLOCKED_TRANSLATION_DRAFT` and state the blocker. Do not silently omit a locale.

Production locale rows must carry one of these statuses:

- `source_authority`: English authority text.
- `draft_machine_or_llm`: draft translation, not native-approved.
- `BLOCKED_TRANSLATION_DRAFT`: required row exists, but usable draft text could not be produced; blocker must be stated.
- `fluent_reviewed`: reviewed by a fluent reviewer, still not final native QA if UI/audio timing is untested.
- `native_reviewed`: native speaker reviewed meaning, voice, and idiom.
- `runtime_ready`: native-reviewed and tested in the assigned UI/audio/site surface.

When producing all 15 locales in one writing pass, the honest default is `draft_machine_or_llm` for non-English rows unless the task provides native-reviewed text. Do not hide this status in comments; put it in the packet.

Translations must preserve:

- who is speaking;
- what the source knows and does not know;
- exact numbers, dates, units, route names, custody marks, and IDs;
- the same spoiler boundary;
- the same gameplay instruction;
- the same lie or omission if the source is biased.

Translations must not add jokes, idioms, folklore, poetic intensifiers, moral interpretation, or extra exposition to "sound natural". Natural means believable for the source and locale, not bigger.

## GlobalQualityWeight Presentation Density

`GlobalQualityWeight` may scale how much optional lore presentation appears around the same truth:

- Compact: shorter codex bodies, scanner short forms, fewer optional archive fragments, stronger objective clarity.
- Middle: fuller codex entries, more field notes, terminal fragments, and evidence crosslinks.
- High: richer audio fragments, more document variants, stronger environmental contradiction chains, and website/wiki supporting sections.
- Ultra: dense archive material, secondary contradictions, optional dossier commentary, and extended public/wiki versions.

It must not change Article ID, LocID, canon fact, speaker/source, spoiler level, unlock truth, gameplay instruction, save identity, ending eligibility, or public claim state. Quality scaling changes presentation density only. It never makes a different story true.

## Language-Specific Caution

- `ar_SA` and `he_IL`: RTL punctuation, numerals, unit order, and embedded Latin IDs must be checked.
- `ja_JP`, `ko_KR`, `zh_CN`: avoid English idiom carryover; preserve technical IDs and names consistently.
- `de_DE`, `nl_NL`, `pl_PL`, `uk_UA`, `ru_RU`: expect expansion and heavier compound nouns; test UI width.
- `es_ES`, `fr_FR`, `pt_BR`, `id_ID`: avoid over-formal filler; keep operational directness.

If a phrase depends on English wordplay, replace it in source before localization.

## Realism Rules

Good HECTON-8 text usually has restraint:

- one concrete image;
- one procedural fact;
- one consequence;
- one gap.

The gap matters. A survivor does not know the whole conspiracy. A technician does not explain the theme. A public encyclopedia does not have classified truth. A corporate memo does not admit murder. A scanner does not understand guilt.

## Rewriting Bad Text

Bad:

```text
Pellet-Fusion Freight defines the industrial ship history behind HECTON-8.
```

Better external article:

```text
Pellet-fusion freight made Aegir cheap enough to claim and too expensive to abandon. The first cargo did not carry families. It carried reactors, sealant, seed machines, spare hull plates, and enough legal payload to make the route billable before anyone called it habitable.
```

Bad:

```text
Field Note: Use for system overview and skybox/art constraints.
```

Better field note:

```text
Field note, Black Keel optical bay: the shelf has daylight. If a contractor says HECTON-8 is black from orbit, they are selling fear or hiding a bad sensor.
```

Bad:

```text
The vent does not care whether you call it power or weather.
```

Better audio:

```text
Do not stand over the white water. Gauge says heat, visor says oxygen, and both are lying by enough to cook the seal.
```

## Rejection Gates

Reject writing if:

- it sounds like a design specification;
- it uses banned AI/marketing phrases;
- it explains theme instead of showing evidence;
- all surfaces in a packet share the same voice;
- a survivor knows more than they could know;
- a corporation confesses too cleanly;
- a scanner becomes poetic;
- a technical article has no engineering cost;
- a mineral note has no handling condition;
- localization is English-only when the task asked for production content;
- translated variants change facts, names, IDs, or gameplay meaning.

## Production Packet

For any substantial writing task, provide:

- canon sources used;
- surface list;
- speaker/source list;
- unlock context;
- English source text;
- localization plan for all 15 locales or explicit English-only scope;
- forbidden facts avoided;
- evidence object or UI route;
- review status: source draft, localized draft, native-reviewed, or runtime proof attached.

## Acceptance Sentence

Writing is accepted only when it reads like a believable artifact from HECTON-8, preserves canon, avoids AI/specification prose, has a clear speaker and context, and can become a 15-locale packet without changing facts or sounding machine-written.
