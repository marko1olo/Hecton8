# Final Decision Emotional Axis

Status: production-facing draft pending native localization.

Final choice asks what the player does with a crime scene, broken guardian, public truth, Atlas severance and no clean best ending.

## Packets

- `P191_FINAL_QUESTION_CRIME_SCENE_SALE` - Final Question Crime Scene Sale: Final Question Crime Scene Sale defines the material ending axis.
- `P192_FINAL_QUESTION_BROKEN_GUARDIAN` - Final Question Broken Guardian: Final Question Broken Guardian defines the preserve/quarantine ending axis.
- `P193_FINAL_QUESTION_PUBLIC_TRUTH_LOST_CONTROL` - Final Question Public Truth Lost Control: Final Question Public Truth Lost Control defines the public ledger ending pressure.
- `P194_FINAL_QUESTION_SEVERANCE_MERCY_THEFT` - Final Question Severance Mercy Theft: Final Question Severance Mercy Theft defines the shutdown ethical axis.
- `P195_BEST_ENDING_NO_CLEAN_HANDS` - Best Ending No Clean Hands: Best Ending No Clean Hands defines the moral standard for final outcomes.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
