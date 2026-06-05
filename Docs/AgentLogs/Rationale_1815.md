# Agent 1815 Rationale

## Decisions

- Static-only boundary retained. Task explicitly forbids Unity/runtime/profiler claims, so all findings use source/data/doc evidence classes and runtime acceptance remains pending.
- No gameplay/data source edit was applied. Static evidence shows the first-hour route is missing product authority, not a one-line typo: copper is authored as `HarvestToolClass.Drill`, the visible starter provisioner is dev-only/disabled, and `FirstCraft` lacks an approved route craft/use-state contract. Editing copper's tool class, enabling the dev helper, granting raw copper, or hardcoding a speculative whitelist would lower first-20 truth.
- Preferred next implementation route is product-owned starter authority plus route-specific craft/use-state gate. Tool inventory ownership must be granted before slot assignment, and craft completion should filter typed `CraftingCompletedSignal` recipe/result hashes or preserve recipe identity in `CraftingEvents`.
