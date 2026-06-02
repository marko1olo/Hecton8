# Rs013 Colony Atlas Maintenance

Status: production-facing AppliedContent release set; non-EN/RU localization is draft-filled and requires native pass.

Runtime contract:

- Do not parse this markdown or packet JSON at runtime.
- Bake through `Tools/AppliedLoreImporter.py` into DataMonolith source rows and hash constants.
- Export pages through `Tools/AppliedLorePageExporter.py` for in-game wiki and external site surfaces.
- Export route cards through `Tools/AppliedLoreRouteCardExporter.py`.

Purpose:

- Defines Atlas maintenance ecology as non-mystical biology-as-infrastructure.
- Adds named colony witnesses for tide modeling, evacuation, pump repair and Atlas safety.
- Turns human names into playable evidence hooks instead of generic lore exposition.
- Preserves Atlas as damaged restoration logic, not evil conquest AI.
- Keeps ocean strange without making it a talking god.

Packets:

- `P061_MAINTENANCE_ECOLOGY`: Maintenance Ecology.
- `P062_MARA_VENN_TIDE_MODEL`: Mara Venn Tide Model.
- `P063_JUNO_KADE_EVACUATION_HOLD`: Juno Kade Evacuation Hold.
- `P064_REN_OKOYE_PUMP_63`: Ren Okoye Pump 63.
- `P065_SAHANA_IQBAL_ATLAS_SAFETY`: Sahana Iqbal Atlas Safety.
