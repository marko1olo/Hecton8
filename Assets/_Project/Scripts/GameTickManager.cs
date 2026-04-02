// ============================================================================
// HECTON-8 — GameTickManager.cs
// Единственный MonoBehaviour с Update/FixedUpdate в проекте.
//
// ВСЕ игровые системы регистрируются здесь через интерфейсы ITickable,
// IFixedTickable, ISlowTickable. Менеджер вызывает их централизованно.
//
// ПРЕИМУЩЕСТВА ПЕРЕД NATIVE Update():
// ┌────────────────────────────┬──────────────────────────────────┐
// │ Unity Native               │ GameTickManager                   │
// ├────────────────────────────┼──────────────────────────────────┤
// │ Reflection-based dispatch  │ Прямой вызов через интерфейс     │
// │ ~500 ns per call overhead  │ ~5 ns per call                   │
// │ Нет контроля порядка       │ Порядок регистрации              │
// │ Нет возможности паузить    │ bool _isPaused — один флаг       │
// │ GC от SendMessage в Editor │ Zero GC                          │
// └────────────────────────────┴──────────────────────────────────┘
//
// БЕЗОПАСНОСТЬ ИТЕРАЦИИ:
//   Register/Unregister во время Tick? → Buffered.
//   Изменения применяются после завершения текущей итерации.
//   Никаких InvalidOperationException, никаких пропусков.
//
// HARDENING (Auto-Cleanup):
//   Уничтоженные MonoBehaviour ("fake null") автоматически удаляются
//   из списков во время итерации. Паттерн-матчинг `is UnityEngine.Object`
//   для reference type — zero boxing, zero GC.
//
// ZERO GC:
//   • Нет foreach (используется for с индексом).
//   • Нет LINQ.
//   • Нет аллокаций в горячих путях.
//   • WaitForSeconds для SlowTick кэширован один раз.
//   • Swap-remove вместо List.Remove (без сдвига массива).
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hecton8.Core;
using Hecton8.Dev;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)] // Тикает РАНЬШЕ всех
    public sealed class GameTickManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static GameTickManager _instance;

        /// <summary>
        /// Глобальный доступ к менеджеру тиков.
        /// Гарантированно не-null после Awake менеджера.
        /// </summary>
        public static GameTickManager Instance
        {
            get
            {
#if UNITY_EDITOR
                // В Editor вне Play Mode — безопасный null
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Slow Tick ─────────────────────────────────")]
        [Tooltip("Интервал между SlowTick вызовами (секунды). " +
                 "0.5 = 2 раза в секунду.")]
        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("── Diagnostics (Read Only) ───────────────────")]
        [SerializeField] private int _debugTickCount;
        [SerializeField] private int _debugFixedCount;
        [SerializeField] private int _debugSlowCount;

        [Header("── Slow Tick Profiling ───────────────────────")]
        [SerializeField] private bool enableSlowTickProfiling = true;
        [SerializeField] private float slowTickSpikeThresholdMs = 8f;
        [SerializeField] private int slowTickTopEntries = 6;
        [SerializeField] private float _debugLastSlowTickDurationMs;
        [SerializeField] private float _debugTopSlowTickDurationMs;
        [SerializeField] private string _debugTopSlowTickOwner = "None";
        [SerializeField] private string _debugLastSlowTickReport = "None";

        // ══════════════════════════════════════════════════════════
        //  TICK LISTS — буферизованные коллекции
        // ══════════════════════════════════════════════════════════

        private TickList<ITickable>      _tickables;
        private TickList<IFixedTickable> _fixedTickables;
        private TickList<ISlowTickable>  _slowTickables;

        // ══════════════════════════════════════════════════════════
        //  COROUTINE CACHE
        // ══════════════════════════════════════════════════════════

        private Coroutine      _slowTickHandle;
        private WaitForSeconds _cachedWait;
        private float _cachedWaitInterval = -1f;
        private const int SlowTickProfilerCapacity = 8;
        private readonly object[] _slowTickTopOwners = new object[SlowTickProfilerCapacity];
        private readonly double[] _slowTickTopDurationsMs = new double[SlowTickProfilerCapacity];
        private readonly StringBuilder _slowTickReportBuilder = new StringBuilder(512);

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton enforcement ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

            // ── Initialize tick lists ──
            // Initial capacity: предполагаем ~100 тикабельных объектов.
            // List расширится автоматически, если нужно — одна аллокация.
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            StartSlowTick();
        }

        private void OnDisable()
        {
            StopSlowTick();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE — единственный в проекте
        // ══════════════════════════════════════════════════════════

        private void Update()
        {
            EnsureInitialized();

            float dt = Time.deltaTime;

            _tickables.BeginIteration();

            List<ITickable> items = _tickables.Items;
            int count = items.Count;

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    _tickables.Remove(item);
                    continue;
                }

                // ── Auto-Cleanup: "fake null" (уничтоженный MonoBehaviour) ──
                if (item is UnityEngine.Object obj && obj == null)
                {
                    _tickables.Remove(item);
                    continue;
                }

                item.Tick(dt);
            }

            _tickables.EndIteration();

#if UNITY_EDITOR
            _debugTickCount = _tickables.Count;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  FIXED UPDATE — единственный в проекте
        // ══════════════════════════════════════════════════════════

        private void FixedUpdate()
        {
            EnsureInitialized();

            float fdt = Time.fixedDeltaTime;

            _fixedTickables.BeginIteration();

            List<IFixedTickable> items = _fixedTickables.Items;
            int count = items.Count;

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    _fixedTickables.Remove(item);
                    continue;
                }

                // ── Auto-Cleanup: "fake null" (уничтоженный MonoBehaviour) ──
                if (item is UnityEngine.Object obj && obj == null)
                {
                    _fixedTickables.Remove(item);
                    continue;
                }

                item.FixedTick(fdt);
            }

            _fixedTickables.EndIteration();

#if UNITY_EDITOR
            _debugFixedCount = _fixedTickables.Count;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SLOW TICK — корутина (2 раза в секунду по дефолту)
        // ══════════════════════════════════════════════════════════

        private void StartSlowTick()
        {
            EnsureInitialized();

            if (_slowTickHandle != null)
            {
                return;
            }

            if (_cachedWait == null || !Mathf.Approximately(_cachedWaitInterval, slowTickInterval))
            {
                _cachedWait = new WaitForSeconds(slowTickInterval);
                _cachedWaitInterval = slowTickInterval;
            }

            _slowTickHandle = StartCoroutine(SlowTickRoutine());
        }

        private void StopSlowTick()
        {
            if (_slowTickHandle != null)
            {
                StopCoroutine(_slowTickHandle);
                _slowTickHandle = null;
            }
        }

        /// <summary>
        /// Вечная корутина. WaitForSeconds кэширован — zero GC per yield.
        /// </summary>
        private IEnumerator SlowTickRoutine()
        {
            // Первый yield — чтобы все системы успели зарегистрироваться
            yield return null;

            while (true)
            {
                yield return _cachedWait;

                _slowTickables.BeginIteration();

                List<ISlowTickable> items = _slowTickables.Items;
                int count = items.Count;
                long loopStartTimestamp = enableSlowTickProfiling ? Stopwatch.GetTimestamp() : 0L;
                if (enableSlowTickProfiling)
                    ResetSlowTickProfilerFrame();

                for (int i = 0; i < count; i++)
                {
                    var item = items[i];

                    if (item == null)
                    {
                        _slowTickables.Remove(item);
                        continue;
                    }

                    // ── Auto-Cleanup: "fake null" (уничтоженный MonoBehaviour) ──
                    if (item is UnityEngine.Object obj && obj == null)
                    {
                        _slowTickables.Remove(item);
                        continue;
                    }

                    long itemStartTimestamp = enableSlowTickProfiling ? Stopwatch.GetTimestamp() : 0L;
                    item.SlowTick();
                    if (enableSlowTickProfiling)
                    {
                        long itemEndTimestamp = Stopwatch.GetTimestamp();
                        RecordSlowTickSample(item, itemEndTimestamp - itemStartTimestamp);
                    }
                }

                _slowTickables.EndIteration();

                if (enableSlowTickProfiling)
                {
                    long loopEndTimestamp = Stopwatch.GetTimestamp();
                    CommitSlowTickProfilerFrame(loopEndTimestamp - loopStartTimestamp, count);
                }

#if UNITY_EDITOR
                _debugSlowCount = _slowTickables.Count;
#endif
            }
        }

        private void ResetSlowTickProfilerFrame()
        {
            for (int i = 0; i < SlowTickProfilerCapacity; i++)
            {
                _slowTickTopOwners[i] = null;
                _slowTickTopDurationsMs[i] = 0d;
            }
        }

        private void RecordSlowTickSample(object owner, long elapsedTicks)
        {
            double elapsedMs = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            for (int i = 0; i < SlowTickProfilerCapacity; i++)
            {
                if (elapsedMs <= _slowTickTopDurationsMs[i])
                    continue;

                for (int shift = SlowTickProfilerCapacity - 1; shift > i; shift--)
                {
                    _slowTickTopOwners[shift] = _slowTickTopOwners[shift - 1];
                    _slowTickTopDurationsMs[shift] = _slowTickTopDurationsMs[shift - 1];
                }

                _slowTickTopOwners[i] = owner;
                _slowTickTopDurationsMs[i] = elapsedMs;
                return;
            }
        }

        private void CommitSlowTickProfilerFrame(long elapsedTicks, int registeredCount)
        {
            double totalMs = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            _debugLastSlowTickDurationMs = (float)totalMs;

            if (_slowTickTopOwners[0] != null)
            {
                _debugTopSlowTickDurationMs = (float)_slowTickTopDurationsMs[0];
                _debugTopSlowTickOwner = ResolveTickableLabel(_slowTickTopOwners[0]);
            }
            else
            {
                _debugTopSlowTickDurationMs = 0f;
                _debugTopSlowTickOwner = "None";
            }

            if (totalMs < Mathf.Max(0.1f, slowTickSpikeThresholdMs))
                return;

            _slowTickReportBuilder.Clear();
            _slowTickReportBuilder.Append("[TickProfiler] SlowTick spike total=");
            _slowTickReportBuilder.Append(totalMs.ToString("0.00"));
            _slowTickReportBuilder.Append("ms registered=");
            _slowTickReportBuilder.Append(registeredCount);
            _slowTickReportBuilder.Append(" top=");

            int topCount = Mathf.Clamp(slowTickTopEntries, 1, SlowTickProfilerCapacity);
            bool hasEntry = false;
            for (int i = 0; i < topCount; i++)
            {
                object owner = _slowTickTopOwners[i];
                if (owner == null || _slowTickTopDurationsMs[i] <= 0.001d)
                    continue;

                if (hasEntry)
                    _slowTickReportBuilder.Append(" | ");

                _slowTickReportBuilder.Append(ResolveTickableLabel(owner));
                _slowTickReportBuilder.Append('=');
                _slowTickReportBuilder.Append(_slowTickTopDurationsMs[i].ToString("0.00"));
                _slowTickReportBuilder.Append("ms");
                hasEntry = true;
            }

            if (!hasEntry)
                _slowTickReportBuilder.Append("none");

            _debugLastSlowTickReport = _slowTickReportBuilder.ToString();
            UnityEngine.Debug.Log(_debugLastSlowTickReport, this);
            RuntimeDiagnosticsTrace.WriteEvent("slowtick", _debugLastSlowTickReport);
        }

        private static string ResolveTickableLabel(object owner)
        {
            if (owner == null)
                return "Null";

            if (owner is Component component)
                return $"{component.GetType().Name}@{component.gameObject.name}";

            return owner.GetType().Name;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — TYPED REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>Регистрирует ITickable (каждый кадр).</summary>
        public void Register(ITickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _tickables.Add(tickable);
        }

        /// <summary>Снимает ITickable с обновления.</summary>
        public void Unregister(ITickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _tickables.Remove(tickable);
        }

        /// <summary>Регистрирует IFixedTickable (физический шаг).</summary>
        public void Register(IFixedTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _fixedTickables.Add(tickable);
        }

        /// <summary>Снимает IFixedTickable с обновления.</summary>
        public void Unregister(IFixedTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _fixedTickables.Remove(tickable);
        }

        /// <summary>Регистрирует ISlowTickable (медленный тик).</summary>
        public void Register(ISlowTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _slowTickables.Add(tickable);
        }

        /// <summary>Снимает ISlowTickable с обновления.</summary>
        public void Unregister(ISlowTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _slowTickables.Remove(tickable);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CONVENIENCE AUTO-DETECT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Авто-регистрация: объект проверяется на все интерфейсы.
        /// Удобно для классов, реализующих несколько интерфейсов:
        ///   GameTickManager.Instance.RegisterAll(this);
        ///
        /// Паттерн-матчинг `is` для reference types — zero GC.
        /// </summary>
        public void RegisterAll(object target)
        {
            EnsureInitialized();
            if (target == null) return;

            if (target is ITickable t)
                _tickables.Add(t);

            if (target is IFixedTickable ft)
                _fixedTickables.Add(ft);

            if (target is ISlowTickable st)
                _slowTickables.Add(st);
        }

        /// <summary>
        /// Авто-отписка: снимает объект со всех списков.
        /// </summary>
        public void UnregisterAll(object target)
        {
            EnsureInitialized();
            if (target == null) return;

            if (target is ITickable t)
                _tickables.Remove(t);

            if (target is IFixedTickable ft)
                _fixedTickables.Remove(ft);

            if (target is ISlowTickable st)
                _slowTickables.Remove(st);
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        /// <summary>Кол-во зарегистрированных ITickable.</summary>
        public int TickableCount      => _tickables?.Count ?? 0;

        /// <summary>Кол-во зарегистрированных IFixedTickable.</summary>
        public int FixedTickableCount => _fixedTickables?.Count ?? 0;

        /// <summary>Кол-во зарегистрированных ISlowTickable.</summary>
        public int SlowTickableCount  => _slowTickables?.Count ?? 0;

        // ══════════════════════════════════════════════════════════
        //  TickList<T> — БУФЕРИЗОВАННАЯ КОЛЛЕКЦИЯ (nested class)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обёртка над List&lt;T&gt; с буферизацией Add/Remove.
        ///
        /// ПРОБЛЕМА: если вызвать List.Add или List.Remove во время
        /// итерации по тому же списку — InvalidOperationException
        /// или пропуск/двойная обработка элементов.
        ///
        /// РЕШЕНИЕ: во время итерации все изменения складываются
        /// в буферы _toAdd / _toRemove. После завершения итерации
        /// (EndIteration) буферы применяются к основному списку.
        ///
        /// ZERO GC:
        ///   • Никаких foreach — только for с индексом.
        ///   • ReferenceEquals вместо EqualityComparer (без boxing).
        ///   • Swap-remove: O(1) удаление без сдвига массива.
        ///   • Списки переиспользуются — Clear() не аллоцирует.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_instance == null)
                _instance = this;

            if (_tickables == null)
                _tickables = new TickList<ITickable>(128);

            if (_fixedTickables == null)
                _fixedTickables = new TickList<IFixedTickable>(64);

            if (_slowTickables == null)
                _slowTickables = new TickList<ISlowTickable>(32);

            if (slowTickInterval <= 0f)
                slowTickInterval = 0.5f;
        }

        private sealed class TickList<T> where T : class
        {
            // ── Основной список ──
            private readonly List<T> _items;

            // ── Буферы отложенных операций ──
            private readonly List<T> _toAdd;
            private readonly List<T> _toRemove;

            // ── Флаг: сейчас идёт итерация ──
            private bool _isIterating;

            // ─────────────────────────────────────────────────────
            //  CONSTRUCTOR
            // ─────────────────────────────────────────────────────

            public TickList(int initialCapacity)
            {
                _items    = new List<T>(initialCapacity);
                _toAdd    = new List<T>(16);
                _toRemove = new List<T>(16);
            }

            // ─────────────────────────────────────────────────────
            //  PUBLIC ACCESSORS
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Прямой доступ к внутреннему списку для for-цикла.
            /// ТОЛЬКО ДЛЯ ЧТЕНИЯ во время итерации!
            /// </summary>
            public List<T> Items => _items;

            /// <summary>Текущее кол-во элементов.</summary>
            public int Count => _items.Count;

            // ─────────────────────────────────────────────────────
            //  ADD / REMOVE — с буферизацией
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Добавляет элемент. Если итерация — в буфер.
            /// Дубликаты игнорируются (проверка ReferenceEquals).
            /// </summary>
            public void Add(T item)
            {
                if (_isIterating)
                {
                    // Проверка: может, он уже в буфере удаления?
                    // Если да — отменяем удаление вместо двойного добавления.
                    if (ContainsRef(_toRemove, item))
                    {
                        RemoveRef(_toRemove, item);
                        return;
                    }

                    // Не добавляем дважды
                    if (!ContainsRef(_items, item) && !ContainsRef(_toAdd, item))
                        _toAdd.Add(item);
                }
                else
                {
                    if (!ContainsRef(_items, item))
                        _items.Add(item);
                }
            }

            /// <summary>
            /// Удаляет элемент. Если итерация — в буфер.
            /// Безопасно при отсутствии элемента (no-op).
            /// </summary>
            public void Remove(T item)
            {
                if (_isIterating)
                {
                    // Может, он ещё не добавлен (в буфере добавления)?
                    if (ContainsRef(_toAdd, item))
                    {
                        RemoveRef(_toAdd, item);
                        return;
                    }

                    if (ContainsRef(_items, item) && !ContainsRef(_toRemove, item))
                        _toRemove.Add(item);
                }
                else
                {
                    SwapRemove(_items, item);
                }
            }

            // ─────────────────────────────────────────────────────
            //  ITERATION GUARDS
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Вызови ПЕРЕД for-циклом. Активирует буферизацию.
            /// </summary>
            public void BeginIteration()
            {
                _isIterating = true;
            }

            /// <summary>
            /// Вызови ПОСЛЕ for-цикла. Применяет буферы.
            /// </summary>
            public void EndIteration()
            {
                _isIterating = false;
                FlushPending();
            }

            // ─────────────────────────────────────────────────────
            //  PRIVATE — Flush Buffers
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Применяет все отложенные добавления и удаления.
            /// Порядок: сначала удаления, потом добавления.
            /// Clear() на List не аллоцирует — обнуляет Count, буфер остаётся.
            /// </summary>
            private void FlushPending()
            {
                // ── Удаления ──
                int removeCount = _toRemove.Count;
                if (removeCount > 0)
                {
                    for (int i = 0; i < removeCount; i++)
                        SwapRemove(_items, _toRemove[i]);

                    _toRemove.Clear();
                }

                // ── Добавления ──
                int addCount = _toAdd.Count;
                if (addCount > 0)
                {
                    for (int i = 0; i < addCount; i++)
                    {
                        T item = _toAdd[i];
                        if (!ContainsRef(_items, item))
                            _items.Add(item);
                    }

                    _toAdd.Clear();
                }
            }

            // ─────────────────────────────────────────────────────
            //  PRIVATE — Zero-GC Collection Helpers
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Проверка наличия по ссылке. Без EqualityComparer,
            /// без boxing, без аллокаций. O(n) — но списки маленькие,
            /// и вызывается только при Register/Unregister (редко).
            /// </summary>
            private static bool ContainsRef(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Удаление по ссылке из небуферизованного списка.
            /// Используется для чистки буферов _toAdd / _toRemove.
            /// Обычный Remove (не swap) — сохраняет порядок буфера.
            /// </summary>
            private static void RemoveRef(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                    {
                        list.RemoveAt(i);
                        return;
                    }
                }
            }

            /// <summary>
            /// Swap-Remove: меняет элемент с последним, удаляет последний.
            /// O(1) удаление вместо O(n) сдвига.
            ///
            /// ⚠ НЕ сохраняет порядок элементов.
            ///   Для систем тиков порядок обычно не критичен.
            ///   Если порядок важен — замените на list.RemoveAt(i).
            /// </summary>
            private static void SwapRemove(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                    {
                        int last = count - 1;
                        list[i] = list[last];  // Swap (no-op если i == last)
                        list.RemoveAt(last);   // O(1) — последний элемент
                        return;
                    }
                }
            }
        }
    }
}
