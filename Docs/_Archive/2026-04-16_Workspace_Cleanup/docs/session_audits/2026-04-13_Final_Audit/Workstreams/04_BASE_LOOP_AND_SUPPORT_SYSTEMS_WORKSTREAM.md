**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Base Loop / Support Systems Workstream

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Что закрывает этот фронт

- Return loop
- Base value
- Crafting / storage / power / oxygen support
- Support systems that make survival loop matter

## Почему это важно

Если база, фабрикация и support systems существуют только как набор отдельных механик, игра не склеивается в устойчивый цикл.

## Основные задачи

### Front A. Return value

- Зафиксировать, зачем игрок возвращается на базу.
- Сделать базу местом recovery, planning, crafting и progression.

### Front B. Oxygen / refill / safety loop

- Проверить oxygen refill path.
- Проверить safe recovery route.
- Проверить failure feedback.

### Front C. Crafting / storage / power cohesion

- Связать крафт, storage, repair, power и upgrades в один понятный цикл.
- Убрать состояния, где системы формально есть, но player value не дают.

### Front D. Save / world state continuity

- Проверить, что support loop переживает save/load.
- Проверить reload после mid-loop progress.

## Candidate owners

- `Assets/_Project/Scripts/SaveManager.cs`
- Player survival / inventory / builder / fabrication owners в `Assets/_Project/Scripts/Gameplay`
- base/support owners в `Assets/_Project/Scripts/Building`, `Crafting`, `Power`, `Inventory`

## Do-Not-Touch Scope

- Не лезть в shell/UI кроме точек вызова.
- Не авторить narrative content здесь.
- Не смешивать с heavy perf work.

## Как дробить по агентам

Агент 1:
- oxygen / survival / refill path
- Задача: survival support loop.

Агент 2:
- crafting / storage / inventory owners
- Задача: return value и support cohesion.

Агент 3:
- save continuity по support systems
- Задача: проверка сохранения и восстановления цикла.

## Expected Result

- У игрока появляется ясная причина возвращаться.
- База перестаёт быть декоративной системой.
- Support loop склеивается с progression.

## Exit Criteria

- Есть рабочий цикл: explore -> gather -> return -> recover/craft/upgrade -> go deeper.
- Нет критических разрывов после save/load.
