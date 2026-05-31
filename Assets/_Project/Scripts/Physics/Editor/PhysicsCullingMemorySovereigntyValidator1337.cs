#if UNITY_EDITOR
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    [InitializeOnLoad]
    public static class PhysicsCullingMemorySovereigntyValidator1337
    {
        static PhysicsCullingMemorySovereigntyValidator1337()
        {
            PhysicsCullingLayout1337.ValidateForEditor();
        }

        [MenuItem("HECTON-8/Physics/Run Physics Culling Memory Sovereignty Validator 1337")]
        public static void RunMenu()
        {
            if (!PhysicsCullingLayout1337.ValidateForEditor())
                return;

            H8Debug.Log("[1337] Physics culling memory sovereignty validator passed.");
        }

        [MenuItem("HECTON-8/Physics/Run Physics Culling Mock Spam Fuzzer 1337")]
        public static void RunMockSpamFuzzerMenu()
        {
            if (!PhysicsCullingLayout1337.ValidateForEditor())
                return;

            GlobalPhysicsStateManager manager = FindRuntimeManager();
            if (manager == null)
            {
                H8Debug.LogError("[1337] Physics culling mock fuzzer requires a live GlobalPhysicsStateManager.");
                return;
            }

            int generated = manager.GenerateMockPhysicsBodies(1000);
            manager.FireMockSeismicShockwave(1337u);
            if (generated <= 0)
            {
                H8Debug.LogError("[1337] Physics culling mock fuzzer generated zero mock bodies.");
                return;
            }

            H8Debug.Log("[1337] Physics culling mock spam fuzzer generated " + generated + " bodies and queued seismic wake.");
        }

        private static GlobalPhysicsStateManager FindRuntimeManager()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<GlobalPhysicsStateManager>();
#else
            return Object.FindObjectOfType<GlobalPhysicsStateManager>();
#endif
        }
    }
}
#endif
