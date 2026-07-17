# SA010 Photic Bloom Pressure Anomaly And Lost Dive Log

Article ID: `expedition.photic_bloom_pressure_anomaly`
Loc namespace: `LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY`
Runtime layer: `Narrative`
Evidence class: `STATIC_DOC / CONTENT_SOURCE`
Canon status: source-authority draft for future site/wiki/game packet admission
Canon owners: `Docs/Lore/Canon_Locks.md`, `Docs/Lore/Lore_Bible.md`, `Docs/Lore/SourceArticles/SA008_BLUE_DEBT_PRESSURE_HISTORY_AND_CUSTODY.md`, `Docs/Lore/SourceArticles/SA003_PHOTIC_SHELF_PRESSURE_ECOLOGY.md`, `Docs/Lore/SourceArticles/SA007_ATLAS6_DAMAGED_REPAIR_LOGIC.md`
Spoiler level: 1 Early Game
Surface targets: website/wiki, in-game codex, scanner, terminal fragment, audio transcript, Marauder field note
Localization status: `en_US` source_authority; non-English rows are `draft_machine_or_llm` pending native review and layout proof
Runtime/publication status: not runtime-bound, not baked, not native-reviewed, not publication-ready
Locale roster: `en_US`, `ar_SA`, `de_DE`, `es_ES`, `fr_FR`, `he_IL`, `id_ID`, `ja_JP`, `ko_KR`, `nl_NL`, `pl_PL`, `pt_BR`, `ru_RU`, `uk_UA`, `zh_CN`

## Source Brief

Speaker/source: recovered dive-log spool from a two-person Marauder salvage pair (call-signs KESTREL-9 and its dropped partner), read back through a flooded acoustic relay; secondary corporate annotation from a Recovery Compliance Office custody stamp.

Audience: public lore readers, early player codex readers, salvage contractors, pressure-ecology writers.

Date/era: 2190, within the first descent seasons after the player's own bathy-drop loss.

Location/depth/route: HECTON-8 photic shelf lip into the first pressure step, roughly the -180 m to -420 m band above the Blue Debt custody boundary; Aegir low in the transfer sky during the log.

Unlock context: found on a silted spool clipped to a dead flotation ring, or streamed from a half-drowned relay buoy whose timing core still holds charge; scanner surfaces the transcript, terminal surfaces the custody stamp.

Evidence object: corroded dive-log spool, cracked wrist manometer frozen at a bad reading, a heat-shield ring plate stripped for ascent charge, one unspent emergency buoy.

What this source knows: that a bioluminescent bloom on the photic shelf tracked a real, measurable pressure anomaly — the water column pushed a false shallow reading while the true crush depth kept climbing; that the pair spent ascent charge chasing light they mistook for a surface glow; that the relay heard them and logged them, and that logging is not the same as recovery.

What this source does not know: the physical cause of the anomaly, whether Atlas-6 salvage traffic disturbed the column, the final Atlas basin truth, the payload receiver, or any ending outcome.

What this source hides or gets wrong: the pair reads the bloom as a rescue marker and the false-shallow manometer as good news; the corporate stamp reclassifies their loss as "instrument-attributable diver error," shifting custody cost off Keelmark Mutual.

Player use: teaches that light and gauge readings on the photic shelf can lie under a pressure anomaly, that ascent charge is a finite budget not a reflex, and that a heard signal (relay ping, buoy, receipt) is a custody event, not a promise of extraction — the player must instead repair the manometer trust chain, hold true-depth discipline, and ration ascent energy.

Forbidden facts: miracle-drive travel, instant rescue, bioluminescence as sentient guidance, the bloom as a friendly beacon, Black Keel as a rescue ship or crowded orbital city, Deep Reach as empire-scale absolute power, cartoon villain confession, guaranteed clean ending.

Required proper nouns/units: HECTON-8, Aegir, Black Keel, Keelmark Mutual, Recovery Compliance Office, Marauder, KESTREL-9, photic shelf, Blue Debt boundary, bathy-drop, acoustic relay, wrist manometer, true-depth, ascent charge, false-shallow, quarantine handshake, -420 m.

## Transcript Draft (en_US source authority)

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.LINE_01` — "Log, KESTREL-9. We crossed the shelf lip an hour back. The whole column lit up under us — blue, then colder blue, like a ceiling of it. Manometer says we are shallow. My spine says we are not."

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.LINE_02` — "Partner read the bloom as surface scatter and burned charge to climb toward it. We did not climb. The light was under us the whole time. The gauge was reading the anomaly, not the depth."

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.LINE_03` — "True-depth by the crack in my faceplate is past four hundred. False-shallow held the needle for eleven minutes. Eleven minutes of ascent charge spent chasing a floor that thought it was a sky."

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.LINE_04` — "Relay heard us. It said received. It logged the timestamp. It did not say rising. Keelmark hears everyone. That is the whole business — they hear you, and the hearing is billable."

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.LINE_05` — "If you find this on the ring: do not trust the light, do not trust a shallow gauge over a full crush, and do not spend your charge on a glow. Hold true-depth. The bloom is not looking for you. Nothing down here is."

## Custody Stamp (terminal fragment)

`LORE_EXPEDITION_PHOTIC_BLOOM_ANOMALY.STAMP_01` — "Recovery Compliance Office — HECTON-8 descent corridor. Signal from unit KESTREL-9 received and time-logged at the -420 m custody band. Loss reclassified: instrument-attributable diver error. Ascent-charge expenditure noncertifiable for reimbursement. Keelmark Mutual liability: none. Quarantine handshake: not initiated."

## Author Notes (non-runtime)

- Reuses the established pressure-custody spine: a signal being *heard and logged* is a billing/custody event, never an extraction promise (consistent with SA001, SA008). Do not soften this into rescue.
- Bioluminescence stays non-sentient environmental ecology (consistent with SA003). The "bloom follows the anomaly" reading is the *divers'* fatal misinterpretation, not a canon fact.
- Ties the "false-shallow" instrument failure to a physical pressure anomaly so the article can later drive a gameplay lesson (manometer trust, true-depth discipline, finite ascent charge) without asserting any unproven mechanism.
- When this is baked into an `AudioLogData` asset via the lore pipeline, each `LINE_##` / `STAMP_##` key becomes a `tableKey` in the Babel dictionary across the 15-locale roster; en_US is source authority, the rest enter as `draft_machine_or_llm` pending native review.
