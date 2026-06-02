# Rs014 Colony Return Windows

Status: production-facing AppliedContent release set; non-EN/RU localization is draft-filled and requires native pass.

Runtime contract:

- Do not parse this markdown or packet JSON at runtime.
- Bake through `Tools/AppliedLoreImporter.py` into DataMonolith source rows and hash constants.
- Export pages through `Tools/AppliedLorePageExporter.py` for in-game wiki and external site surfaces.
- Export route cards through `Tools/AppliedLoreRouteCardExporter.py`.

Purpose:

- Adds last clean comms proof, pressure forge escape fabrication, repair medicine body horror, procedure-complicity and present Deep Reach windows.
- Expands false/partial ending pressure with evidence bargains and ascent hardware.
- Makes Deep Reach active through relays, proxies and clauses, not FTL radio villainy.
- Provides article-ready colony character fragments for wiki/site publication.
- Keeps all content in AppliedLore baked path.

Packets:

- `P066_LIAN_TORRES_LAST_PACKET`: Lian Torres Last Packet.
- `P067_OSKAR_NEUMANN_PRESSURE_FORGE`: Oskar Neumann Pressure Forge.
- `P068_AYA_MORITA_REPAIR_MEDICINE`: Aya Morita Repair Medicine.
- `P069_PAVEL_SORN_PROCEDURE_HOLD`: Pavel Sorn Procedure Hold.
- `P070_DEEP_REACH_PRESENT_WINDOWS`: Deep Reach Present Windows.
