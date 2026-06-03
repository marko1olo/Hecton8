# Numeric Authoring Bridge Surfaces

Status: production-facing draft pending native localization.

Turn unresolved economy numbers into table-owned authoring surfaces instead of prose promises.

## Packets

- `P261_RESOURCE_YIELD_AUTHORING_ROWS` - Resource Yield Authoring Rows: Resource Yield Authoring Rows explain that HECTON-8's resources are hard-sci-fi categories first and table-owned balance data second.
- `P262_INVENTORY_STACK_AUTHORING_ROWS` - Inventory Stack Authoring Rows: Inventory Stack Authoring Rows define why HECTON-8 inventory pressure is a logistics rule rather than a generic loot stack.
- `P263_ESCAPE_RECIPE_AUTHORING_ROWS` - Escape Recipe Authoring Rows: Escape Recipe Authoring Rows explain why HECTON-8 extraction is engineering, not a simple repair checklist.
- `P264_CONTRACT_RISK_REWARD_AUTHORING_ROWS` - Contract Risk Reward Authoring Rows: Contract Risk Reward Authoring Rows describe HECTON-8's salvage contracts as physical, legal and orbital pressure.
- `P265_ENDING_PAYOUT_AUTHORING_ROWS` - Ending Payout Authoring Rows: Ending Payout Authoring Rows explain how HECTON-8 endings remember payload authority without creating power progression.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
