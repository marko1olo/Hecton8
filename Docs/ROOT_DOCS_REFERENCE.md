# Root Docs Reference

Date: `2026-04-17`
Status: `PENDING VERIFICATION`

Purpose: explain what still remains in repo root after cleanup and where former root references were relocated.

Root is now reduced to the smallest practical active set.

## Kept In Root

- `MASTER_RELEASE_WORK_PLAN.md` - broad production roadmap anchor.
- `BUILD_PLAYTEST_ISSUES.md` - live validation ledger for build/runtime observations.

## Moved Out Of Root

- `AI_CREATURE_ROSTER_ENTERPRISE.md` -> `Docs/AI_Fauna/AI_CREATURE_ROSTER_ENTERPRISE.md`
- `AI_FAUNA_WORLD_INTEGRATION_REPORT.md` -> `Docs/AI_Fauna/AI_FAUNA_WORLD_INTEGRATION_REPORT.md`
- `TERRAIN_108_BIOMES_VISION.md` -> `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md`
- `terrain_description.txt` -> `Docs/Legacy_World_Reference/terrain_description.txt`
- `беклог.txt` -> `Docs/Legacy_Backlog/беклог.txt`
- `спецификации.txt` -> `Docs/Legacy_Backlog/спецификации.txt`
- `Полный_список_недоработок_и_план_разработки_—_Project_Submerge_(HECTON-8) - ЧИТАТЬ, рядом создать документ - что и как испарвляем, дианмично обновлять.md` -> `Docs/Legacy_Backlog/Полный_список_недоработок_и_план_разработки_—_Project_Submerge_(HECTON-8) - ЧИТАТЬ, рядом создать документ - что и как испарвляем, дианмично обновлять.md`
- `Что_и_как_исправляем_—_живой_план.md` -> `Docs/Legacy_Backlog/Что_и_как_исправляем_—_живой_план.md`

## Why They Were Moved

- They are still useful references, but not active runtime authority.
- Keeping them in root created noise and hid the real active anchors.
- Their link surface was small enough to absorb relocation safely.

## Future Cleanup Candidate

If you want an even stricter root later:

1. decide whether `MASTER_RELEASE_WORK_PLAN.md` should stay in root or move into `Docs`
2. decide whether `BUILD_PLAYTEST_ISSUES.md` should stay in root or move into `Docs`
3. replace oversized legacy backlog docs with cleaner maintained summaries if they are still consulted
