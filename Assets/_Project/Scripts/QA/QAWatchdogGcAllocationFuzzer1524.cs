#if UNITY_EDITOR || DEVELOPMENT_BUILD
// ============================================================================
// HECTON-8 - QAWatchdogGcAllocationFuzzer1524.cs
// Manually armed hostile fixture. It is never active during normal watchdog runs.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.QA
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/QA/QA Watchdog GC Allocation Fuzzer 1524")]
    public sealed class QAWatchdogGcAllocationFuzzer1524 : MonoBehaviour, IFastTickable, IGlobalRegistryHotSwapListener
    {
        private const string ObjectName = "__QA_WATCHDOG_GC_FUZZER_1524";
        private const int RootScratchCapacity = 32;

        private static readonly List<GameObject> s_rootScratch = new List<GameObject>(RootScratchCapacity); // COLD ALLOC: scene root scan scratch - owner: QAWatchdogGcAllocationFuzzer1524
        private static QAWatchdogGcAllocationFuzzer1524 s_instance;
        private static byte[] s_lastAllocation;
        private static bool s_armed;

        private bool _tickRegistered;
        private bool _hotSwapRegistered;

        public static bool Armed => s_armed;

        public static void ArmCold()
        {
            s_armed = true;
            EnsureInstanceCold();
            if (s_instance != null)
                s_instance.TryRegisterTickLaneCold();
        }

        public static void DisarmCold()
        {
            s_armed = false;
            s_lastAllocation = null;
            if (s_instance == null)
                return;

            s_instance.TryUnregisterTickLaneCold();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(s_instance.gameObject);
        }

        public static void InjectSingleAllocationCold()
        {
            s_lastAllocation = new byte[1024]; // INTENTIONAL TEST ALLOC: one-shot GC tripwire validation - owner: QAWatchdogGcAllocationFuzzer1524
        }

        private static void EnsureInstanceCold()
        {
            if (!Application.isPlaying || s_instance != null)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                int rootCount = activeScene.rootCount;
                if (s_rootScratch.Capacity < rootCount)
                    s_rootScratch.Capacity = rootCount;

                s_rootScratch.Clear();
                activeScene.GetRootGameObjects(s_rootScratch);
                for (int i = 0; i < s_rootScratch.Count; i++)
                {
                    GameObject root = s_rootScratch[i];
                    if (root == null || !string.Equals(root.name, ObjectName, StringComparison.Ordinal))
                        continue;

                    if (root.TryGetComponent(out QAWatchdogGcAllocationFuzzer1524 existing))
                    {
                        s_instance = existing;
                        s_rootScratch.Clear();
                        return;
                    }
                }

                s_rootScratch.Clear();
            }

            GameObject fixtureObject = new GameObject(ObjectName); // COLD ALLOC: editor-only hostile fixture root - owner: QAWatchdogGcAllocationFuzzer1524
            DontDestroyOnLoad(fixtureObject);
            s_instance = fixtureObject.AddComponent<QAWatchdogGcAllocationFuzzer1524>();
        }

        private void Awake()
        {
            s_instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            s_instance = this;
            TryRegisterHotSwapListenerCold();
            TryRegisterTickLaneCold();
        }

        private void OnDisable()
        {
            TryUnregisterTickLaneCold();
            TryUnregisterHotSwapListenerCold();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!s_armed)
                return;

            s_lastAllocation = new byte[1024]; // INTENTIONAL TEST ALLOC: per-frame GC alarm fixture - owner: QAWatchdogGcAllocationFuzzer1524
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (currentService != null)
                TryRegisterTickLaneCold();
            else
                TryUnregisterTickLaneCold();
        }

        private void TryRegisterTickLaneCold()
        {
            if (_tickRegistered || !Application.isPlaying || !s_armed || GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTickLaneCold()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
            _tickRegistered = false;
        }

        private void TryRegisterHotSwapListenerCold()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListenerCold()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
#endif
