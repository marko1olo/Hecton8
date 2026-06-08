# Final Decision Emotional Axis

Status: production-facing localized source, pending native review.

Final choice asks what the player does with a crime scene, a broken guardian, public truth, Atlas severance and the refusal of a clean best ending. This set is player-facing: short in-game surfaces stay sharp, while external-site articles explain why each ending can be materially correct and morally costly at the same time.

## Packets

- `P191_FINAL_QUESTION_CRIME_SCENE_SALE` - Final Question: Crime Scene Sale. Paid exit, cleared lien, sold evidence.
- `P192_FINAL_QUESTION_BROKEN_GUARDIAN` - Final Question: Broken Guardian. Preserve/quarantine Atlas without pretending it is innocent.
- `P193_FINAL_QUESTION_PUBLIC_TRUTH_LOST_CONTROL` - Final Question: Public Truth, Lost Control. Exposure breaks erasure and releases consequence.
- `P194_FINAL_QUESTION_SEVERANCE_MERCY_THEFT` - Final Question: Severance, Mercy, Theft. Shutdown as surgery, rescue, killing, sabotage or theft.
- `P195_BEST_ENDING_NO_CLEAN_HANDS` - Best Ending: No Clean Hands. The best route saves something real and leaves residue.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
