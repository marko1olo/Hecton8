Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Subagent Runbook

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Pravila zapuska

- U kazhdogo subagenta svoy log-fayl v etoy papke.
- U kazhdogo subagenta svoy owner scope.
- Vyhod za owner scope zapreschen.
- Esli zadacha trebuet pravki chuzhogo owner-fayla, subagent dolzhen ostanovitsya i zapisat bloker v svoy log.
- Lyuboy rezultat bez live/log proof schitaetsya `PENDING VERIFICATION`.

## Format loga

Kazhdyy agent pishet tolko v svoy fayl:

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

## Minimalnoe soderzhimoe loga

- Scope
- Files touched
- Actions taken
- Blockers
- Verification status

## Zapret na interferentsiyu

- Nelzya dvum agentam pisat v odin i tot zhe source file.
- Nelzya odnovremenno pisat v odnu i tu zhe `.unity` stsenu bolee chem odnomu agentu.
- Nelzya odnovremenno trogat shell owner'ov i global option owner bez soblyudeniya tochek vhoda.
- Nelzya smeshivat content authoring i bootstrap wiring v odnom fayle.
