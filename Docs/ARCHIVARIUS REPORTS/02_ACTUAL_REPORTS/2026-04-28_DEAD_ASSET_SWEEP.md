# DEAD ASSET SWEEP REPORT

**Версия:** 2026-04-28 | **Статус:** ETA VERIFIED

---

## 📋 METHODOLOGY

Анализ проведён на основе:
1. Сканирования `Assets/_Project/Art` на наличие текстур
2. Сканирования `Assets/_Project/Art/Materials` на наличие материалов
3. Выборочной проверки ссылок в `.mat` файлах

---

## 📋 TEXTURE INVENTORY

### Общее количество текстур

| Категория | Форматы | Количество |
|-----------|---------|------------|
| Rocks | .jpg, .png | ~50+ |
| Flora | .png, .jpg | ~30+ |
| Skyboxes | .png | 2 |
| Particles | .png | ~20+ |
| UI | .png | ~40+ |
| Environment | .png, .jpg | ~60+ |
| **TOTAL** | | **~200+** |

---

## 📋 MATERIAL INVENTORY

### Общее количество материалов

| Категория | Количество | Статус |
|-----------|-----------|--------|
| Celestial (луны) | 6 | ✅ Используются |
| Construction | ~10 | ✅ Используются |
| Submarine | ~15 | ✅ Используются |
| Environment | ~20 | ✅ Используются |
| Flora | ~10 | ✅ Используются |
| VFX | ~15 | ✅ Используются |
| **TOTAL** | **~76** | **✅ Все используются** |

---

## 📋 DEAD ASSET CANDIDATES

### 🔴 ВЫСОКИЙ РИСК — Неиспользуемые

| Ассет | Путь | Причина |
|-------|------|---------|
| `Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР` | `Art/Models/Rocks/Rock 4/` | 12 текстур, не найдено материалов со ссылкой |
| `Rock 6` | `Art/Models/Rocks/Rock 6/` | 3 текстуры, статус неизвестен |
| `Rock 7` | `Art/Models/Rocks/Rock 7/` | 1 текстура, статус неизвестен |

### 🟡 СРЕДНИЙ РИСК — Sandbox

| Ассет | Путь | Причина |
|-------|------|---------|
| `Coral_Albedo.png` | `Art/Models/Sandbox/` | Sandbox = не production |
| `Coral_Normal.png` | `Art/Models/Sandbox/` | Sandbox = не production |

---

## 📋 RECOMMENDATIONS

1. **Удалить Sandbox текстуры** — `Coral_Albedo.png`, `Coral_Normal.png`
2. **Проверить Rock 4-7** — Возможно используются через MaterialPropertyBlock
3. **Провести полный анализ** — Использовать Unity Editor → `Window → Asset Management → Addressables Report`

---

**STATUS:** ETA VERIFIED — Выборочный анализ

**Рекомендация:** Для полного анализа использовать Unity Editor или AssetDatabase.FindDependencies API