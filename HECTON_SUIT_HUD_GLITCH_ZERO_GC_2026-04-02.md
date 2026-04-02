# HECTON SUIT HUD GLITCH ZERO-GC — 2026-04-02

## Что было не так

В `Assets/_Project/Scripts/HectonSuitHUD.cs` glitch-эффект уже был переведён с coroutine на `ITickable`,
но внутри `TriggerGlitch()` всё ещё создавался `new int[3]`.

Это маленькая, но реальная лишняя аллокация во время визуального эффекта.

## Что сделано

- убран `new int[3]` из `TriggerGlitch()`
- выбор до трёх glitch-slot теперь хранится в обычных локальных `int`
- внешнее поведение эффекта не менялось

## Что это даёт

- glitch-эффект стал честно ближе к zero-GC контракту
- HUD не создаёт лишний мусор даже на редких визуальных всплесках

## Что проверено

- Unity compile после правки без first-party `Error`
- после короткого `play -> stop` smoke консоль осталась пустой
