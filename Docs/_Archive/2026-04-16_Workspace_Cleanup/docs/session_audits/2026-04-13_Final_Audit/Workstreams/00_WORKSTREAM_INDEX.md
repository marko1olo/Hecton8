Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Workstream Index

Data: 2026-04-13  
Status: PENDING VERIFICATION

Eta papka nuzhna ne dlya krasivogo planirovaniya, a dlya razdachi raboty agentam bez haosa.

## Poryadok zapuska

Pervaya volna:

1. `01_SHELL_UI_WORKSTREAM.md`
2. `02_NARRATIVE_PROGRESSION_WORKSTREAM.md`
3. `03_WORLD_CONTENT_AND_RUNTIME_WORKSTREAM.md`

Vtoraya volna:

1. `04_BASE_LOOP_AND_SUPPORT_SYSTEMS_WORKSTREAM.md`
2. `05_PERF_QA_RELEASE_WORKSTREAM.md`

## Glavnyy printsip

Nelzya puskat agentov v peresekayuschiesya owner-fayly.  
Nelzya odnovremenno trogat scene wiring, UI shell i narrative bootstrap bez zhestkogo razdeleniya.  
Kazhdyy workstream dolzhen imet:

- owner files;
- main tasks;
- do-not-touch scope;
- expected result;
- exit criteria.

## Chto davat agentam v pervuyu ochered

Esli agentov malo:

1. Shell/UI.
2. Narrative/Progression.
3. World content/runtime cleanup.

Esli agentov mnogo:

1. Odin agent na shell/menu.
2. Odin agent na pause/rebind/options.
3. Odin agent na quest/content data.
4. Odin agent na audio logs.
5. Odin agent na suit upgrades / progression.
6. Odin agent na world cleanup / scene truth.
7. Odin agent na caves/ruins/world density.
8. Odin agent na perf/QA/build hardening.

## Obyazatelnoe pravilo dlya vseh agentov

- Ne trogat chuzhie owner-fayly.
- Ne pereimenovyvat public API bez otdelnogo podtverzhdeniya.
- Ne taschit novye sistemy, esli tekuschiy owner uzhe suschestvuet.
- Lyuboy rezultat bez realnoy verifikatsii schitat `PENDING VERIFICATION`.
