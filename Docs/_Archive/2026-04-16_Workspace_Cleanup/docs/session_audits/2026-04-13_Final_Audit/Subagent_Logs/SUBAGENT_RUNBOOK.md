Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Subagent Runbook

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Правила запуска

- У каждого субагента свой лог-файл в этой папке.
- У каждого субагента свой owner scope.
- Выход за owner scope запрещён.
- Если задача требует правки чужого owner-файла, субагент должен остановиться и записать блокер в свой лог.
- Любой результат без live/log proof считается `PENDING VERIFICATION`.

## Формат лога

Каждый агент пишет только в свой файл:

- `agent_01_main_menu_log.md`
- `agent_02_pause_shell_log.md`
- `agent_03_pause_rebind_log.md`
- `agent_04_pda_rebind_log.md`
- `agent_05_options_persistence_log.md`
- `agent_06_narrative_spine_log.md`
- `agent_07_quest_content_log.md`
- `agent_08_audio_logs_log.md`
- `agent_09_suit_progression_log.md`
- `agent_10_lore_bootstrap_log.md`
- `agent_11_world_cleanup_log.md`
- `agent_12_world_density_log.md`
- `agent_13_caves_geology_log.md`
- `agent_14_base_loop_log.md`
- `agent_15_perf_memory_log.md`
- `agent_16_tests_builds_log.md`

## Минимальное содержимое лога

- Scope
- Files touched
- Actions taken
- Blockers
- Verification status

## Запрет на интерференцию

- Нельзя двум агентам писать в один и тот же source file.
- Нельзя одновременно писать в одну и ту же `.unity` сцену более чем одному агенту.
- Нельзя одновременно трогать shell owner'ов и global option owner без соблюдения точек входа.
- Нельзя смешивать content authoring и bootstrap wiring в одном файле.
