**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Workstream Index

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

Эта папка нужна не для красивого планирования, а для раздачи работы агентам без хаоса.

## Порядок запуска

Первая волна:

1. `01_SHELL_UI_WORKSTREAM.md`
2. `02_NARRATIVE_PROGRESSION_WORKSTREAM.md`
3. `03_WORLD_CONTENT_AND_RUNTIME_WORKSTREAM.md`

Вторая волна:

1. `04_BASE_LOOP_AND_SUPPORT_SYSTEMS_WORKSTREAM.md`
2. `05_PERF_QA_RELEASE_WORKSTREAM.md`

## Главный принцип

Нельзя пускать агентов в пересекающиеся owner-файлы.  
Нельзя одновременно трогать scene wiring, UI shell и narrative bootstrap без жёсткого разделения.  
Каждый workstream должен иметь:

- owner files;
- main tasks;
- do-not-touch scope;
- expected result;
- exit criteria.

## Что давать агентам в первую очередь

Если агентов мало:

1. Shell/UI.
2. Narrative/Progression.
3. World content/runtime cleanup.

Если агентов много:

1. Один агент на shell/menu.
2. Один агент на pause/rebind/options.
3. Один агент на quest/content data.
4. Один агент на audio logs.
5. Один агент на suit upgrades / progression.
6. Один агент на world cleanup / scene truth.
7. Один агент на caves/ruins/world density.
8. Один агент на perf/QA/build hardening.

## Обязательное правило для всех агентов

- Не трогать чужие owner-файлы.
- Не переименовывать public API без отдельного подтверждения.
- Не тащить новые системы, если текущий owner уже существует.
- Любой результат без реальной верификации считать `PENDING VERIFICATION`.
