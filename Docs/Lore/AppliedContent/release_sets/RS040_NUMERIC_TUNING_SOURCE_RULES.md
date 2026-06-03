# Numeric Tuning Source Rules

Status: production-facing draft pending native localization.

Contracts for future resource, recipe, risk, inventory and localization tables without freezing fake numbers in prose.

## Packets

- `P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT` - Resource Table Placeholder Contract: Resource Table Placeholder Contract separates canon resource identity from future numeric tuning.
- `P197_ESCAPE_RECIPE_BALANCE_BANDS` - Escape Recipe Balance Bands: Escape Recipe Balance Bands define future numeric tuning for extraction components.
- `P198_RISK_REWARD_TABLE_BANDS` - Risk Reward Table Bands: Risk Reward Table Bands define future contract tuning axes.
- `P199_INVENTORY_STACK_TUNING_RULE` - Inventory Stack Tuning Rule: Inventory Stack Tuning Rule defines future stack-size logic for HECTON-8 resources.
- `P200_NATIVE_LOCALIZATION_PASS_CONTRACT` - Native Localization Pass Contract: Native Localization Pass Contract defines the publication-ready localization gate for HECTON-8 AppliedLore.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
