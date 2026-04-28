# HECTON-8 CODEX — MASTER INDEX

**Версия:** 2026-04-28 | **Статус:** ETA CODEX VERIFIED

---

## 📁 ИЕРАРХИЧЕСКАЯ СТРУКТУРА ДОКУМЕНТАЦИИ

### [ARCH] — Architecture & Global Systems

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENTS.md` | Root AI Agent mandates | ✅ ACTIVE |
| `PROJECT_ATLAS.md` | Master directory проекта | ✅ COMPLETE |
| `SUPREME_AUDITOR_CONTINUOUS_REPORT.md` | Сводный аудит | ✅ COMPLETE |
| `DEEP_FORENSIC_AUDIT_REPORT.md` | Глубокий криминальный аудит | ✅ COMPLETE |
| `GLOBAL_ARCHITECTURE_MAP.md` | Архитектурная карта | ✅ COMPLETE |
| `ETA2_CIRCULAR_DEPS.md` | AsmDef dependency audit | ✅ COMPLETE |

### [CORE] — Physics, AUP, Locomotion

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_03_PHYSICS_LOG.md` | Physics agent worklog | ✅ COMPLETE |
| `AUP_DRIFT_WARNINGS.md` | AUP drift alerts | ✅ COMPLETE |
| `TRANSFORM_ACCESS_CRIMES.md` | Transform access violations | ⚠️ ARCHIVE |
| `MATH_API_WARNINGS.md` | Mathf violations | ⚠️ ARCHIVE |
| `ETA2_HOT_PATH_VIOLATIONS.md` | Zero-GC hot-path scan | ✅ COMPLETE |

### [REND] — Shaders, BRG, URP

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_01_GRAPHICS_LOG.md` | Graphics agent worklog | ✅ COMPLETE |
| `VRAM_BUDGET_AUDIT.md` | VRAM budget analysis | ✅ COMPLETE |
| `SHADER_VARIANT_BLOAT.md` | Shader optimization | ✅ COMPLETE |
| `ETA2_VRAM_EXECUTION_LIST.md` | Top 20 VRAM offenders | ✅ COMPLETE |
| `ETA2_CYRILLIC_SWEEP.md` | Cyrillic in shaders/code | ✅ COMPLETE |
| `RENDERGRAPH_AUDIT.md` | URP RenderFeature/Pass leak audit | ✅ COMPLETE |
| `COMPUTE_BUFFER_AUDIT.md` | GraphicsBuffer/ComputeBuffer lifecycle | ✅ COMPLETE |

### [LOGI] — Logistics, Atmosphere, Power

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_04_CORE_LOG.md` | Core systems worklog | ✅ COMPLETE |
| `EVENT_FLOW_MAP.md` | Event bus architecture | ✅ COMPLETE |
| `DOUBLE_BUFFER_COMPLIANCE.md` | Double-buffer patterns | ✅ COMPLETE |
| `ETA2_EVENT_LEAK_REPORT.md` | EventBus unsubscription leaks | ✅ COMPLETE |

### [AI] — Boids, Directors, Genetics

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_02_AI_LOG.md` | AI agent worklog | ✅ COMPLETE |
| `SYSTEM_COUPLING_WARNINGS.md` | AI system coupling | ✅ COMPLETE |

### [UI] — User Interface

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_05_UI_AUDIO_LOG.md` | UI/Audio agent worklog | ✅ COMPLETE |

### [TECH] — Technical Art & Performance

| Файл | Назначение | Статус |
|------|-----------|--------|
| `AGENT_06_TECHART_LOG.md` | TechArt agent worklog | ✅ COMPLETE |
| `PERFORMANCE_WARNINGS.md` | Performance alerts | ✅ COMPLETE |
| `MEMORY_LEAK_WARNINGS.md` | Memory leak warnings | ✅ COMPLETE |
| `SCENE_OBJECT_HYGIENE.md` | Scene optimization | ✅ COMPLETE |

### [DATA] — Data Dictionaries & Contracts

| Файл | Назначение | Статус |
|------|-----------|--------|
| `DATA_DICTIONARY.md` | Struct layouts & memory alignment | ✅ COMPLETE |
| `DEPENDENCY_GRAPH.md` | Global init order & service registration | ✅ COMPLETE |
| `INTERFACE_CONTRACT_TABLE.md` | Interface → Implementation mapping | ✅ COMPLETE |
| `PROFILING_PREPAREDNESS_AUDIT.md` | ProfilerMarker coverage | ✅ COMPLETE |
| `ASSET_DEPENDENCY_MAP.md` | Prefab/SO hardcoded refs | ✅ COMPLETE |
| `AUP_SURGERY_MAP.md` | Byte-level AUP layout migration map | ✅ COMPLETE |
| `BUILD_DEPENDENCY_GRAPH.md` | Bootstrapper bloat & forced RAM audit | ✅ COMPLETE |
| `ETA2_LIAR_DETECTION.md` | Agent mandate compliance audit | ✅ COMPLETE |

### [NARRATIVE] — Frame Flow & Architecture

| Файл | Назначение | Статус |
|------|-----------|--------|
| `STRUCTURAL_NARRATIVE.md` | Одна рамка от Input до Audio | ✅ COMPLETE |
| `DEAD_ASSET_SWEEP_REPORT.md` | Неиспользуемые ассеты | ✅ COMPLETE |

### [INVENTORY] — Inventory & Persistence Audits

| Файл | Назначение | Статус |
|------|-----------|--------|
| `INVENTORY_AUDIT/STATIC_AUDIT_MASTER_SUMMARY.md` | Master static audit | ✅ COMPLETE |
| `INVENTORY_AUDIT/TECH_DEBT_REGISTRY.md` | Technical debt | ✅ COMPLETE |
| `INVENTORY_AUDIT/THIRD_PARTY_POISON.md` | Third-party ACL | ✅ COMPLETE |
| `INVENTORY_AUDIT/NAMING_VIOLATIONS.md` | Naming compliance | ✅ COMPLETE |
| `INVENTORY_AUDIT/STRING_LITERAL_CRIMES.md` | String allocations | ✅ COMPLETE |

---

## 📋 БЫСТРЫЕ ССЫЛКИ

### Для нового агента — С чего начать:

1. **Прочитай `PROJECT_ATLAS.md`** — полная карта проекта
2. **Прочитай `GLOSSARY.md`** — терминология проекта
3. **Проверь `TECH_DEBT_REGISTRY.md`** — что нужно исправить
4. **Проверь свой домен в `AGENT_XX_LOG.md`**

### Критические аудиты:

- **Crest ACL:** `THIRD_PARTY_POISON.md`
- **ItemData Zero-GC:** `DEEP_FORENSIC_AUDIT_REPORT.md`
- **VRAM Budget:** `VRAM_BUDGET_AUDIT.md`
- **Player.prefab:** `GOD_OBJECT_AUDIT.md`

---

## 📊 СТАТИСТИКА ДОКУМЕНТАЦИИ

| Категория | Файлов |
|-----------|--------|
| Total .md | 42 |
| Agent Logs | 6 |
| Inventory Audits | 10 |
| Root Docs | 8 |
| CODEX Documents | 8 |
| ETA-2 Deep Audits | 6 |
| Leak Tracking Audits | 3 |

## 📋 TOP 5 DOD STRUCTS

| Struct | Size | Alignment | File |
|--------|------|-----------|------|
| `AbsoluteUniversePositionBlit128` | 48 bytes | 16-byte ✅ | PersistentWorldRegistry.cs |
| `CognitionCore` | 64 bytes | 64-byte ✅ | PredatorCognitionDomain.cs |
| `BoidData` | 32 bytes | 4-byte ✅ | SargassumMicroFaunaBoids.cs |
| `HectonVegetationInstanceData` | ~104 bytes | Default | HectonIndirectVegetationContracts.cs |
| `ForcePacket` | ~36 bytes | Default | PhysicsApplySystem.cs |

## 👻 GHOST INTERFACES

| Interface | Status | Action |
|-----------|--------|--------|
| `IRenderable` | ⚠️ PARTIAL | Remove or implement |
| `IUIService` | ⚠️ PARTIAL | Register UI systems |
| `IDamageReceiver` | ⚠️ PARTIAL | Unify damage system |

## 🎯 PROFILING BLIND SPOTS

| System | Priority | Status |
|--------|----------|--------|
| `PhysicsApplySystem` | 🔴 CRITICAL | No markers |
| `HectonFluidEngine` | 🔴 CRITICAL | No markers |
| `HectonPlayerMovement` | 🟠 HIGH | No markers |

---

## 🔗 ВНЕШНИЕ РЕСУРСЫ

| Ресурс | Путь |
|--------|------|
| Lore docs | `Lore/` |
| Internal specs | `internal-specs/` |
| Tools | `Tools/` |
| Diff patches | `*.diff` files |

---

**STATUS:** ✅ ETA CODEX VERIFIED

**Следующий обзор:** 2026-05-05

## 📋 КРИТИЧЕСКИЕ НАРУШЕНИЯ (Требуют действий)

| ID | Issue | Файл | Статус |
|----|-------|------|--------|
| ACL-01 | Crest ACL violation | HectonSurfaceWeatherDirector.cs | ❌ |
| GC-01 | ItemData managed refs | PlayerInventory.cs | ❌ |
| LM-01 | LayerMask.NameToLayer | 4 файла | ❌ |
| GOD-01 | 42 компонентов | Player.prefab | ❌ |
| PROF-01 | Нет ProfilerMarker | PhysicsApplySystem.cs | ❌ |
| PROF-02 | Нет ProfilerMarker | HectonFluidEngine.cs | ❌ |
| EVT-01 | EventBus leak (no Dispose) | GlobalProfileManager.cs | ❌ |
| EVT-02 | EventBus leak (no Dispose) | RunModifierController.cs | ❌ |
| CYR-01 | Russian folder name | Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР | ⚠️ |
| HP-01 | NameToLayer in hot path | PlayerSwimBlockoutRig.cs | ❌ |
| HP-02 | Trailing space string | HectonCrestOceanDepthCacheBootstrap.cs | ❌ |

**Все нарушения задокументированы в:** `SUPREME_AUDITOR_CONTINUOUS_REPORT.md`, `ETA2_EVENT_LEAK_REPORT.md`, `ETA2_HOT_PATH_VIOLATIONS.md`

**Новые аудиты (Mission 2 — Leak Tracking):**
- `RENDERGRAPH_AUDIT.md` — URP RenderFeature/Pass lifecycle (0 first-party leaks)
- `COMPUTE_BUFFER_AUDIT.md` — GraphicsBuffer/ComputeBuffer disposal (0 first-party leaks)
- `BUILD_DEPENDENCY_GRAPH.md` — Bootstrapper forced-RAM analysis (0 heavy assets at boot)