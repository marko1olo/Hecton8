# Table Value Handoff Contracts

Status: production-facing AppliedLore source.
Runtime rule: source/export only; runtime consumes baked static data and string-pool offsets.

Lock release-facing table handoff rows for resources, stacks, escape costs, contract pressure and ending payouts without hardcoding final balance values in lore.

## Packets

- `P301_RESOURCE_YIELD_ROW_CONTRACT` - Resource Yield Row Contract: Resource values are accepted only when pressure context, depletion behavior and custody grade are named.
- `P302_STACK_LIMIT_ROW_CONTRACT` - Stack Limit Row Contract: Stack limits are logistics facts tied to containment class, pressure certification and sample behavior.
- `P303_ESCAPE_RECIPE_COST_ROW_CONTRACT` - Escape Recipe Cost Row Contract: Ascent-qualified repairs require deeper pressure materials, relay proof and payload authority.
- `P304_CONTRACT_RISK_REWARD_ROW_CONTRACT` - Contract Risk Reward Row Contract: Contract pressure is a row with named risk axes, not an invisible difficulty mode.
- `P305_ENDING_PAYOUT_ROW_CONTRACT` - Ending Payout Row Contract: Ending payouts are inseparable from receiver authority, evidence custody and unresolved cost.
