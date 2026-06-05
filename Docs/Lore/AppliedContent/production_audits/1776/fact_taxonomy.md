# Fact Taxonomy - 1776

Evidence class: STATIC_DOC. This taxonomy is authoring/audit guidance, not runtime implementation.

## Prime Rule

Facts are separate from voice. A packet surface can lie, omit, panic, corrupt, or misunderstand. The fact registry records the claim state and owner so downstream website/wiki/codex/player-note work does not turn voice into truth.

## Fact States

| State | Meaning | Accepted owner/proof route | Player-facing behavior |
|---|---|---|---|
| `observed` | A sensor, room, object, page, or player action records something directly visible, scanned, heard, or recovered. | Packet, scan surface, terminal, black-box, scene prop, or index row. | Can appear after discovery. Must not infer final cause alone. |
| `claimed` | A source asserts a fact without sufficient independent proof. | Source-labeled terminal, survivor note, corporate memo, Marauder annotation, public article. | Keep source voice visible. Do not present as neutral truth. |
| `contradicted` | Two or more owned sources disagree, or canon explicitly rejects the claim. | Contradiction must list both paths and the higher authority. | Player note may say what conflicts, not the final answer unless unlocked. |
| `inferred` | The fact is a defensible conclusion from multiple observed/claimed items, but not directly confirmed. | Needs at least two evidence paths and a clear unlock boundary. | Use cautious wording. No omniscient phrasing. |
| `confirmed` | Canon lock or approved packet owner establishes truth. | `Canon_Locks.md`, `Lore_Bible.md`, source-owned packet, or approved generated index. | May be neutral once the unlock route allows it. |
| `redacted` | Text indicates hidden/withheld content. The missing part is a fact about custody, not a license to invent. | Redaction marker, damaged transcript, official excerpt, black-box gap. | Player note tracks the gap and possible evidence route. |
| `damaged` | Source exists but is corrupted, mojibake, incomplete, clipped, or physically degraded. | Damaged row/file/audio/terminal fragment path. | Treat as clue, not full truth. |
| `player-note` | A memory aid derived from discovered evidence. | Existing packet/article/unlock ID plus note template. | Must be partial, useful, and unlock-bound. |
| `public-safe` | Fact may appear before/around release without late-game spoilers. | Spoiler tier 0/1 or public-site lock. | No final payload receiver, Atlas-basin outcome, or ending consequence leak. |
| `spoiler-locked` | Fact requires mid/deep/ending unlock or gated publication. | Spoiler tier, unlock route, or canon lock. | Hide from early notes/public pages; route only after evidence. |

## Contradiction State Values

Use these exact values in audit CSVs until runtime schema exists:

- `confirmed`
- `observed`
- `claimed`
- `inferred`
- `contradicted`
- `redacted`
- `damaged`
- `player-note`
- `public-safe`
- `spoiler-locked`
- `schema-gap`

## Quality Scaling

`GlobalQualityWeight` may scale note density and optional surrounding evidence:

- Low: shortest note, one route/action clue, no optional archive fragments.
- Middle: note plus one crosslink or unresolved question.
- High: note plus source voice, contradiction marker, and next evidence lead.
- Ultra: note plus secondary dossier context and archive crosslink.

Quality never changes fact ID, article ID, unlock gate, spoiler state, canon owner, or truth.

