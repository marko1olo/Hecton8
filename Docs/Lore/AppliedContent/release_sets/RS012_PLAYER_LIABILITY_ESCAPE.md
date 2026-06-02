# Rs012 Player Liability Escape

Status: production-facing AppliedContent release set; non-EN/RU localization is draft-filled and requires native pass.

Runtime contract:

- Do not parse this markdown or packet JSON at runtime.
- Bake through `Tools/AppliedLoreImporter.py` into DataMonolith source rows and hash constants.
- Export pages through `Tools/AppliedLorePageExporter.py` for in-game wiki and external site surfaces.
- Export route cards through `Tools/AppliedLoreRouteCardExporter.py`.

Purpose:

- Locks protagonist as ex-Deep-Reach without family melodrama.
- Separates real Great Tide physics from Deep Reach liability.
- Defines Black Keel as claim-pool carrier with hidden Deep Reach hooks.
- Turns escape into six-piece engineering/legal/evidence chain.
- Defines the first hour as playable survival, beauty, lie and repair scar.

Packets:

- `P056_EX_DEEP_REACH_MARAUDER`: Ex-Deep-Reach Marauder.
- `P057_GREAT_TIDE_LIABILITY_CHAIN`: Great Tide Liability Chain.
- `P058_BLACK_KEEL_CLAIM_HOOKS`: Black Keel Claim Hooks.
- `P059_ESCAPE_CHAIN_ASSEMBLY`: Escape Chain Assembly.
- `P060_FIRST_HOUR_SPINE`: First Hour Spine.
