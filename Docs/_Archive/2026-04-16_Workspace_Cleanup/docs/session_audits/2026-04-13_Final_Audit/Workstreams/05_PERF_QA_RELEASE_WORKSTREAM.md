**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Perf / QA / Release Workstream

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Что закрывает этот фронт

- Performance truth
- Memory truth
- Test coverage
- Build cadence
- Release hardening

## Почему это нельзя откладывать

На текущем объёме проекта ручная память команды уже не держит всю систему.  
13 тестов для такого проекта означают слабую страховку от регрессий.

## Основные задачи

### Front A. Perf truth on target hardware

- CPU frame time.
- GC/frame.
- VRAM.
- RenderTexture memory.
- Batches / SetPass.
- Streaming hitch profile.

### Front B. Regression discipline

- Зафиксировать обязательные before/after замеры.
- Нельзя принимать perf-fix без чисел.
- Нельзя считать исправление закрытым без повтора сценария.

### Front C. Critical flow test coverage

- Main menu path.
- Save/load path.
- Core survival path.
- Pause/settings path.
- One narrative/progression path.

### Front D. Build validation

- Регулярные production builds.
- Прогон smoke checklist.
- Логирование нерешённых build blockers.

### Front E. Memory / render triage

- Texture memory.
- RT memory.
- Lighting/post cost.
- Scatter CPU cost.

## Candidate owners

- `Assets/_Project/Tests`
- performance-sensitive world owners
- build issue docs
- save/menu/pause critical path owners

## Do-Not-Touch Scope

- Не расширять gameplay scope.
- Не превращать perf work в новый feature work.
- Не переписывать системы без измерения.

## Как дробить по агентам

Агент 1:
- perf numbers / profiling routines
- Задача: собрать truth baseline.

Агент 2:
- critical flow tests
- Задача: поднять минимальную страховку от регрессий.

Агент 3:
- build smoke and issue ledger
- Задача: превратить сборки в регулярный контроль, а не случайное событие.

## Expected Result

- Появляются реальные цифры.
- Регрессии начинают ловиться раньше.
- Финальная доводка перестаёт идти вслепую.

## Exit Criteria

- Есть baseline по perf/memory.
- Есть smoke suite по critical path.
- Build blockers фиксируются регулярно, а не от случая к случаю.
