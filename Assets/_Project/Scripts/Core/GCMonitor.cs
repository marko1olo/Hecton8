using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Post-fixed Gen0 GC detector for runtime allocation enforcement.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9485)]
    public sealed class GCMonitor : MonoBehaviour, IPostFixedTickable
    {
        private static GCMonitor _instance;

        private bool _registeredPostFixed;
        private int _lastGen0CollectionCount;
        private int _lastReportedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static GCMonitor EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[GCMonitor]"); // COLD ALLOC: GameObject[1] - bootstrap-owned GC sentinel root - owner: GCMonitor
            return runtimeRoot.AddComponent<GCMonitor>();
        }

        public void InitializeService()
        {
            _lastGen0CollectionCount = GC.CollectionCount(0);
            TryRegisterPostFixed();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _lastGen0CollectionCount = GC.CollectionCount(0);
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            TryRegisterPostFixed();
        }

        private void Start()
        {
            TryRegisterPostFixed();
        }

        private void OnDisable()
        {
            if (!_registeredPostFixed)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixed = false;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_instance == this)
                _instance = null;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            int currentGen0CollectionCount = GC.CollectionCount(0);
            if (currentGen0CollectionCount == _lastGen0CollectionCount)
                return;

            int frame = Time.frameCount;
            int delta = currentGen0CollectionCount - _lastGen0CollectionCount;
            _lastGen0CollectionCount = currentGen0CollectionCount;
            if (_lastReportedFrame == frame)
                return;

            _lastReportedFrame = frame;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogAssertion(
                "[GCMonitor] Gen0 GC collection detected at frame " +
                frame +
                " delta=" +
                delta +
                " fixedDeltaTime=" +
                fixedDeltaTime.ToString("0.000000"));
#endif
        }

        private void TryRegisterPostFixed()
        {
            if (_registeredPostFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixed = SystemDispatcher
                .GetPostFixedLane(PriorityLayer.Core)
                .Contains(this);
        }
    }
}
