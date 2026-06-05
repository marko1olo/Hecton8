# Gemini Browser Image Asset Workflow

Практический протокол для Codex/оператора, который вручную генерирует картинки и текстуры через Gemini в браузере.

## Что Нужно

- Открытый браузер Edge/Chrome.
- Сайт `https://gemini.google.com/app`.
- Пользователь уже залогинен в свои Google accounts.
- Разрешение пользователя переключать аккаунты, если Gemini упирается в лимит.

## Важное

- Prompts для генерации изображений писать на английском.
- Для текстур всегда явно писать `seamless`, `tileable`, `square`.
- Сначала скачивать в не-runtime папку проекта, например `Docs/GeneratedAssets/Gemini/`.
- Не скачивать сразу в `Assets`, если это Unity/Unreal проект: это может вызвать импорт/ребилд.
- Дубликаты удалять.
- Не заявлять, что ассет готов в игре, пока он не импортирован, не назначен и не проверен в редакторе.

## UI Шаги

1. Открой `https://gemini.google.com/app`.
2. Если нужно включить генерацию изображений:
   - нажми `+` рядом с полем ввода;
   - выбери создание изображений.
3. Вставь английский production prompt.
4. Отправь prompt.
5. Дождись изображения.
6. Скачай:
   - через download button на изображении;
   - или right click -> `Save image as`.
7. Сохрани в intake папку:
   - `Docs/GeneratedAssets/Gemini/`
8. Переименуй по смыслу:
   - `TX_Project_WetBasaltShoreline_Albedo_001.png`
   - `TX_Project_ReefSand_Albedo_001.png`
   - `TX_Project_FoamNoise_Mask_001.png`
9. Проверь tileability:
   - через `https://iliad.ai/seamless-texture-checker`;
   - или локальным 2x2 preview.
10. Только accepted файлы переноси в рабочую asset папку проекта.

## Переключение Аккаунтов

Если Gemini пишет про лимиты:

1. Нажми нижний левый avatar/account button.
2. Выбери другой доступный аккаунт.
3. Дождись reload.
4. Закрой информационный popup, если появился.
5. Продолжай generation.

Не надо писать email/names аккаунтов в отчет.

## Что Просить У Codex

```text
Use the already opened Gemini browser session manually.
Generate production candidate image assets using English prompts.
If image generation mode is not active, click the plus button and choose image creation.
If Gemini hits a limit, switch to another available account from the lower-left account menu.
Download images to Docs/GeneratedAssets/Gemini first, not to runtime/import folders.
Rename files clearly, delete duplicates, and run a tileability/visual QA pass before importing.
Report exact paths and whether the asset is only a source candidate or already integrated.
```

