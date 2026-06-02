# RS001 - First Descent Release Set

Status: production-facing draft.
Purpose: first bundled content set for in-game wiki, scanner, terminal/audio lines, external site/wiki, art briefs, and localization.

## Included Content

- P001 Crash Shelf.
- P002 Black Keel Contact.
- P003 Barnard Mark.
- P004 Blue Debt.
- P005 Repair Scar.

## Player-Facing Arc

1. Player survives a damaged bathy-drop on a bright alien shelf.
2. Black Keel answers, but behaves like a contract machine before a rescue platform.
3. Drowned colony rooms reveal ordinary human work, then a Barnard mark makes it personal.
4. Blue debt makes HECTON-8 valuable, dangerous, and tempting.
5. Atlas repair scars prove the horror is purposeful maintenance, not random mutation.

## Deliverables In This Set

- Structured packet JSON with localized runtime/publishing strings.
- In-game wiki articles.
- External site articles.
- Image/art briefs.
- Scanner/terminal/audio lines.

## Runtime Boundary

This release set is an authoring/export layer. Runtime should consume a baked version:

- packet ID hashes;
- LocID hashes;
- surface enums;
- unlock flags;
- seed placement tags;
- localized string pool offsets.

No runtime markdown parsing. No runtime translation generation.
