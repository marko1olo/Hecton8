# Handoff 1771 - Protosite / Reader Agent

Evidence class: STATIC_DOC / STATIC_SOURCE

## Use Existing Pages

- Public home article: `Docs/Lore/AppliedContent/external_site/*/P456_SITE_HOME_LONGFORM_BRIEF.md`
- Public start cluster: `Docs/Lore/AppliedContent/external_site/*/P416_SITE_WIKI_START_HERE_CLUSTER.md`
- Surface index: `Docs/Lore/AppliedContent/Publication_Surface_Index.csv`
- Cluster index: `Docs/Lore/AppliedContent/Publication_Cluster_Index.csv`
- Inventory audit: `Docs/Lore/AppliedContent/production_audits/1771/external_site_inventory.csv`
- Translation units: `Docs/Lore/AppliedContent/production_audits/1771/public_translation_units.md`
- Editorial map: `Docs/Lore/AppliedContent/production_audits/1771/public_site_editorial_map.md`

## Desired Display Behavior

- Treat `P456_SITE_HOME_LONGFORM_BRIEF` as the public first-read page for the website home/start route.
- Show frontmatter localization status only to tooling, never as visible reader copy.
- For `ar_SA` and `he_IL`, set page direction from frontmatter `direction: rtl`; do not reverse source strings manually.
- Keep public pages spoiler-safe by default. Do not surface final receiver, Atlas basin, or ending payload pages unless the reader is in archive/spoiler mode.
- Preserve stable packet IDs and article IDs in links/data attributes.

## Do Not Depend On Future Work

- Do not wait for 1779.
- Do not require new generated pages.
- Do not assume native localization review; non-English P456 pages are draft/native-review pending.

