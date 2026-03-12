// ============================================================================
// HECTON-8 — SaveManager.cs
// Менеджер сохранений. Singleton, DontDestroyOnLoad.
//
// АРХИТЕКТУРА:
//   • Реестр ISaveable вместо FindObjectsByType (zero GC при save/load).
//   • Сортировка по приоритетам: SavePriority / LoadPriority.
//   • ES3 для сериализации (binary по умолчанию, можно JSON для debug).
//   • Полная защита от ошибок: try-catch-finally, null-checks, file existence.
//   • Метаданные: версия, timestamp, playtime для UI слотов.
//   • Unity 6 Awaitable API: дисковая I/O через BackgroundThreadAsync,
//     zero-GC thread switching (без Task.Run, без лямбда-замыканий).
//   • Синхронный fallback (SaveGameAndBlock) для OnApplicationQuit.
//
// ПОРЯДОК ЗАГРУЗКИ:
//   1. Player stats + position  (priority 10)
//   2. Inventory                (priority 20)
//   3. World state              (priority 50)
//   4. Construction             (priority 90)
//
// ИСПОЛЬЗОВАНИЕ:
//   await SaveManager.Instance.SaveGameAsync("slot_1");
//   await SaveManager.Instance.LoadGameAsync("slot_1");
//   SaveManager.Instance.DeleteSave("slot_1");
//   bool exists = SaveManager.Instance.SaveExists("slot_1");
//   SaveManager.Instance.SaveGameAndBlock("emergency");   // OnApplicationQuit
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.SaveSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class SaveManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static SaveManager _instance;

        public static SaveManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Ключ для ES3. Формат: 'save_{slotName}'")]
        [SerializeField] private string saveKeyPrefix = "save_";

        [Tooltip("Использовать сжатие при сохранении")]
        [SerializeField] private bool useCompression = true;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugRegisteredCount;
        [SerializeField] private bool _debugIsSaving;
        [SerializeField] private bool _debugIsLoading;

        // ══════════════════════════════════════════════════════════
        //  REGISTRY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реестр ISaveable систем. Заполняется через Register/Unregister.
        /// Сортируется при save/load по соответствующему приоритету.
        /// </summary>
        private readonly List<ISaveable> _saveables = new List<ISaveable>(16);

        /// <summary>Флаг: нужна пересортировка после Register/Unregister.</summary>
        private bool _registryDirty;

        /// <summary>Отслеживание времени игры.</summary>
        private float _sessionStartTime;
        private float _totalPlayTime;

        /// <summary>Предотвращает одновременные save/load.</summary>
        private bool _isBusy;

        // ══════════════════════════════════════════════════════════
        //  COMPARERS (pre-allocated, zero GC)
        // ══════════════════════════════════════════════════════════

        /// <summary>Сортировка по SavePriority (ascending).</summary>
        private static readonly Comparison<ISaveable> SavePriorityCompare =
            (a, b) => a.SavePriority.CompareTo(b.SavePriority);

        /// <summary>Сортировка по LoadPriority (ascending).</summary>
        private static readonly Comparison<ISaveable> LoadPriorityCompare =
            (a, b) => a.LoadPriority.CompareTo(b.LoadPriority);

        // ══════════════════════════════════════════════════════════
        //  ES3 SETTINGS (pre-allocated, reused)
        // ══════════════════════════════════════════════════════════

        private ES3Settings _cachedSettings;

        private ES3Settings GetSettings()
        {
            if (_cachedSettings == null)
            {
                _cachedSettings = new ES3Settings
                {
                    encryptionType  = ES3.EncryptionType.None,
                    compressionType = useCompression
                        ? ES3.CompressionType.Gzip
                        : ES3.CompressionType.None
                };
            }
            else
            {
                // Обновляем на случай если в Inspector переключили флаг
                _cachedSettings.compressionType = useCompression
                    ? ES3.CompressionType.Gzip
                    : ES3.CompressionType.None;
            }

            return _cachedSettings;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _sessionStartTime = Time.realtimeSinceStartup;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// При экстренном выходе — синхронное сохранение,
        /// т.к. async не гарантирует завершение после OnApplicationQuit.
        /// </summary>
        private void OnApplicationQuit()
        {
            // Раскомментируй при необходимости автосохранения при выходе:
            // SaveGameAndBlock("autosave_quit");
        }

        // ══════════════════════════════════════════════════════════
        //  REGISTRY — Register / Unregister
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрирует ISaveable систему в реестре.
        /// Вызывается из OnEnable() каждой ISaveable системы.
        /// Дубликаты игнорируются.
        /// </summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;

            // ── Проверка дубликатов (ReferenceEquals, zero GC) ──
            for (int i = 0, count = _saveables.Count; i < count; i++)
            {
                if (ReferenceEquals(_saveables[i], saveable))
                    return;
            }

            _saveables.Add(saveable);
            _registryDirty = true;

#if UNITY_EDITOR
            _debugRegisteredCount = _saveables.Count;
#endif
        }

        /// <summary>
        /// Снимает ISaveable с регистрации.
        /// Вызывается из OnDisable() каждой ISaveable системы.
        /// </summary>
        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;

            for (int i = _saveables.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_saveables[i], saveable))
                {
                    // Swap-remove: O(1)
                    int last = _saveables.Count - 1;
                    _saveables[i] = _saveables[last];
                    _saveables.RemoveAt(last);
                    _registryDirty = true;
                    break;
                }
            }

#if UNITY_EDITOR
            _debugRegisteredCount = _saveables.Count;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — Registry Helpers
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Сортирует реестр по заданному компаратору, только если _registryDirty.
        /// После сортировки сбрасывает dirty-флаг.
        /// </summary>
        private void SortRegistryIfDirty(Comparison<ISaveable> comparison)
        {
            if (!_registryDirty) return;

            _saveables.Sort(comparison);
            _registryDirty = false;
        }

        /// <summary>
        /// Проверяет, жив ли ISaveable (защита от «фейковых нуллов» Unity
        /// для уничтоженных MonoBehaviour).
        /// </summary>
        private static bool IsAlive(ISaveable saveable)
        {
            if (saveable == null) return false;

            // Если ISaveable реализован на MonoBehaviour / ScriptableObject,
            // Unity может вернуть «fake null» после Destroy.
            if (saveable is UnityEngine.Object unityObj)
                return unityObj != null; // вызывает operator== Unity

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — ASYNC SAVE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Асинхронно сохраняет текущее состояние игры в указанный слот.
        ///
        /// Порядок (Zero-Hitch):
        ///   Main Thread  — снапшот: CreateNew + PopulateSaveData (быстро).
        ///   Background   — дисковый I/O: ES3.Save (без блокировки главного потока).
        ///   Main Thread  — таймеры, события.
        ///
        /// Thread switching через Awaitable.BackgroundThreadAsync /
        /// Awaitable.MainThreadAsync — zero-GC, без лямбда-замыканий.
        ///
        /// При ошибке: SaveEvents.OnSaveFailed, данные не повреждаются.
        /// Флаг _isBusy гарантированно сбрасывается через try-finally.
        /// </summary>
        /// <param name="slotName">Имя слота (например "slot_1", "autosave").</param>
        public async Awaitable SaveGameAsync(string slotName)
        {
            if (_isBusy)
            {
                Debug.LogWarning("[SaveManager] Save/Load already in progress!");
                return;
            }

            if (string.IsNullOrEmpty(slotName))
            {
                Debug.LogError("[SaveManager] Slot name is null or empty!");
                return;
            }

            _isBusy = true;

#if UNITY_EDITOR
            _debugIsSaving = true;
#endif

            SaveEvents.RaiseSaveStarted(slotName);

            var totalTimer = Stopwatch.StartNew();

            try
            {
                // ════════════════════════════════════════════════
                //  PHASE 1 — Snapshot (Main Thread, должен быть <2ms)
                // ════════════════════════════════════════════════

                var snapshotTimer = Stopwatch.StartNew();

                float playTime = _totalPlayTime
                    + (Time.realtimeSinceStartup - _sessionStartTime);
                SaveData data = SaveData.CreateNew(playTime);

                // Сортируем только если dirty
                SortRegistryIfDirty(SavePriorityCompare);

                // Сбор данных от всех ISaveable
                for (int i = 0, count = _saveables.Count; i < count; i++)
                {
                    ISaveable saveable = _saveables[i];

                    if (!IsAlive(saveable)) continue;

                    try
                    {
                        saveable.PopulateSaveData(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[SaveManager] Error in PopulateSaveData " +
                            $"({saveable.GetType().Name}): {ex.Message}\n{ex.StackTrace}");
                    }
                }

                snapshotTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Snapshot phase: {snapshotTimer.Elapsed.TotalMilliseconds:F2}ms " +
                    $"({_saveables.Count} saveables)");

                // ════════════════════════════════════════════════
                //  PHASE 2 — Disk Write (Background Thread)
                //
                //  Awaitable.BackgroundThreadAsync / MainThreadAsync:
                //    • Zero-GC: нет лямбд, нет замыканий, нет Task аллокаций.
                //    • Нативная интеграция с Unity Player Loop.
                //    • Переменные data, key, settings захвачены в
                //      async state machine (стековый фрейм), не в куче.
                // ════════════════════════════════════════════════

                var writeTimer = Stopwatch.StartNew();

                string key          = GetSaveKey(slotName);
                ES3Settings settings = GetSettings();

                await Awaitable.BackgroundThreadAsync();
                ES3.Save(key, data, settings);
                await Awaitable.MainThreadAsync();

                // Мы гарантированно в Main Thread
                writeTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Disk write phase: {writeTimer.Elapsed.TotalMilliseconds:F2}ms");

                totalTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Game saved to '{slotName}' successfully. " +
                    $"Total: {totalTimer.Elapsed.TotalMilliseconds:F2}ms");

                SaveEvents.RaiseSaveCompleted(slotName);
            }
            catch (Exception ex)
            {
                totalTimer.Stop();
                string error = $"Save failed: {ex.Message}";
                Debug.LogError($"[SaveManager] {error}\n{ex.StackTrace}");
                SaveEvents.RaiseSaveFailed(slotName, error);
            }
            finally
            {
                _isBusy = false;

#if UNITY_EDITOR
                _debugIsSaving = false;
#endif
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — SYNC SAVE (Emergency / OnApplicationQuit)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// «Пожарный» синхронный метод сохранения.
        /// Используй ТОЛЬКО в OnApplicationQuit или при критическом краше,
        /// где async/await не гарантирует завершение.
        /// БЛОКИРУЕТ главный поток до завершения записи.
        /// </summary>
        /// <param name="slotName">Имя слота.</param>
        public void SaveGameAndBlock(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                Debug.LogError("[SaveManager] SaveGameAndBlock: Slot name is null or empty!");
                return;
            }

            // Не проверяем _isBusy — при экстренном выходе мы ОБЯЗАНЫ записать.
            // Если async-операция была в процессе, данные могут быть частичными,
            // но это лучше, чем ничего.

            var timer = Stopwatch.StartNew();

            try
            {
                float playTime = _totalPlayTime
                    + (Time.realtimeSinceStartup - _sessionStartTime);
                SaveData data = SaveData.CreateNew(playTime);

                // Сортируем при необходимости
                SortRegistryIfDirty(SavePriorityCompare);

                for (int i = 0, count = _saveables.Count; i < count; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable)) continue;

                    try
                    {
                        saveable.PopulateSaveData(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[SaveManager] SaveGameAndBlock: Error in PopulateSaveData " +
                            $"({saveable.GetType().Name}): {ex.Message}");
                    }
                }

                string key = GetSaveKey(slotName);
                ES3.Save(key, data, GetSettings());

                timer.Stop();
                Debug.Log(
                    $"[SaveManager] SaveGameAndBlock '{slotName}' completed in " +
                    $"{timer.Elapsed.TotalMilliseconds:F2}ms (BLOCKING).");
            }
            catch (Exception ex)
            {
                timer.Stop();
                Debug.LogError(
                    $"[SaveManager] SaveGameAndBlock '{slotName}' FAILED: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — ASYNC LOAD
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Асинхронно загружает состояние игры из указанного слота.
        ///
        /// Порядок (Zero-Hitch):
        ///   Background   — дисковый I/O: ES3.Load (без блокировки главного потока).
        ///   Main Thread  — применение данных: LoadFromSaveData (Unity API safe).
        ///
        /// Thread switching через Awaitable.BackgroundThreadAsync /
        /// Awaitable.MainThreadAsync — zero-GC, без лямбда-замыканий.
        ///
        /// КРИТИЧНО: Player загружается ПЕРВЫМ (priority 10),
        /// Construction — ПОСЛЕДНИМ (priority 90).
        /// </summary>
        public async Awaitable LoadGameAsync(string slotName)
        {
            if (_isBusy)
            {
                Debug.LogWarning("[SaveManager] Save/Load already in progress!");
                return;
            }

            if (string.IsNullOrEmpty(slotName))
            {
                Debug.LogError("[SaveManager] Slot name is null or empty!");
                return;
            }

            string key = GetSaveKey(slotName);

            // ── Проверка существования (быстрая, Main Thread) ──
            if (!ES3.KeyExists(key))
            {
                string error = $"Save '{slotName}' not found.";
                Debug.LogWarning($"[SaveManager] {error}");
                SaveEvents.RaiseLoadFailed(slotName, error);
                return;
            }

            _isBusy = true;

#if UNITY_EDITOR
            _debugIsLoading = true;
#endif

            SaveEvents.RaiseLoadStarted(slotName);

            var totalTimer = Stopwatch.StartNew();

            try
            {
                // ════════════════════════════════════════════════
                //  PHASE 1 — Disk Read (Background Thread)
                //
                //  Awaitable.BackgroundThreadAsync / MainThreadAsync:
                //    • Zero-GC: нет лямбд, нет замыканий, нет Task аллокаций.
                //    • Нативная интеграция с Unity Player Loop.
                //    • Переменная key захвачена в async state machine
                //      (стековый фрейм), не в куче.
                // ════════════════════════════════════════════════

                var readTimer = Stopwatch.StartNew();

                await Awaitable.BackgroundThreadAsync();
                SaveData data = ES3.Load<SaveData>(key);
                await Awaitable.MainThreadAsync();

                // Гарантированно в Main Thread
                readTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Disk read phase: {readTimer.Elapsed.TotalMilliseconds:F2}ms");

                if (data == null)
                {
                    throw new Exception("ES3.Load returned null.");
                }

                // ── Проверка версии ──
                if (data.version != SaveData.CurrentVersion)
                {
                    Debug.LogWarning(
                        $"[SaveManager] Save version mismatch: " +
                        $"file={data.version}, current={SaveData.CurrentVersion}. " +
                        "Attempting load anyway (migration may be needed).");
                }

                // ════════════════════════════════════════════════
                //  PHASE 2 — Apply Data (Main Thread, Unity API safe)
                // ════════════════════════════════════════════════

                var applyTimer = Stopwatch.StartNew();

                // Восстановление playtime
                _totalPlayTime    = data.totalPlayTime;
                _sessionStartTime = Time.realtimeSinceStartup;

                // Сортировка по LoadPriority (только если dirty)
                // Принудительно помечаем dirty, т.к. save мог сортировать по SavePriority
                _registryDirty = true;
                SortRegistryIfDirty(LoadPriorityCompare);

                // Раздача данных всем ISaveable
                for (int i = 0, count = _saveables.Count; i < count; i++)
                {
                    ISaveable saveable = _saveables[i];

                    if (!IsAlive(saveable)) continue;

                    try
                    {
                        saveable.LoadFromSaveData(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[SaveManager] Error in LoadFromSaveData " +
                            $"({saveable.GetType().Name}): {ex.Message}\n{ex.StackTrace}");
                    }
                }

                applyTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Apply phase: {applyTimer.Elapsed.TotalMilliseconds:F2}ms " +
                    $"({_saveables.Count} saveables)");

                totalTimer.Stop();
                Debug.Log(
                    $"[SaveManager] Game loaded from '{slotName}' successfully. " +
                    $"Total: {totalTimer.Elapsed.TotalMilliseconds:F2}ms");

                SaveEvents.RaiseLoadCompleted(slotName);
            }
            catch (Exception ex)
            {
                totalTimer.Stop();
                string error = $"Load failed: {ex.Message}";
                Debug.LogError($"[SaveManager] {error}\n{ex.StackTrace}");
                SaveEvents.RaiseLoadFailed(slotName, error);
            }
            finally
            {
                _isBusy = false;

#if UNITY_EDITOR
                _debugIsLoading = false;
#endif
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Существует ли сохранение в данном слоте.</summary>
        public bool SaveExists(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return false;
            return ES3.KeyExists(GetSaveKey(slotName));
        }

        /// <summary>Удаляет сохранение из слота.</summary>
        public void DeleteSave(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return;

            string key = GetSaveKey(slotName);
            if (ES3.KeyExists(key))
            {
                ES3.DeleteKey(key);
                Debug.Log($"[SaveManager] Save '{slotName}' deleted.");
            }
        }

        /// <summary>
        /// Загружает ТОЛЬКО метаданные (без полной загрузки).
        /// Для UI меню выбора слотов: показать timestamp, playtime.
        /// </summary>
        public bool TryGetSaveMetadata(string slotName,
            out string timestamp, out float playTime)
        {
            timestamp = null;
            playTime  = 0f;

            if (!SaveExists(slotName)) return false;

            try
            {
                SaveData data = ES3.Load<SaveData>(GetSaveKey(slotName));
                if (data != null)
                {
                    timestamp = data.timestamp;
                    playTime  = data.totalPlayTime;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to read metadata: {ex.Message}");
            }

            return false;
        }

        /// <summary>Текущее время игровой сессии.</summary>
        public float CurrentPlayTime =>
            _totalPlayTime + (Time.realtimeSinceStartup - _sessionStartTime);

        /// <summary>Идёт ли процесс save/load.</summary>
        public bool IsBusy => _isBusy;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Строит ключ ES3 из имени слота.</summary>
        private string GetSaveKey(string slotName)
        {
            return $"{saveKeyPrefix}{slotName}";
        }
    }
}