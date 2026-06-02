# RS009 - Colony Layout / Worker Evidence

Status: production-facing AppliedContent release set.

Purpose: make the drowned colony usable as in-game wiki, scanner, terminal, VO-subtitle, site/wiki and image-card content. This set turns HECTON-8 from abstract catastrophe into a workplace with rooms, habits, delays, medical locks and names.

Packets:
- P041_WORKER_LOCKER_ROW: worker lockers, Barnard marks, food credits and first personal trace.
- P042_PRESSURE_BUNK_ROUTINE: daily life under pressure and the first proof that routine became a trap.
- P043_SHIFT_BOARD_ROUTE_HOLDS: shift logistics converting evacuation into route holds and priority reviews.
- P044_MEDICAL_LOCK_DELAY: medical supplies preserved while authorization logic blocks access.
- P045_BLACK_BOX_NAME_STACK: names and final pressure states as physical ending payload.

Runtime use:
- Bind to drowned colony POIs, scan fragments, terminal boards and late archive stacks through AppliedLore packet hashes.
- Use in the professional-to-personal motive arc before the final Atlas argument.
- Do not parse these markdown files at runtime; bake packet JSON through the AppliedLore importer.
