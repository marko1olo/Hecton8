using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Hecton8.Core
{
    /// <summary>
    /// Cached Android/Vulkan/XR policy for Quest-class TBDR paths.
    /// </summary>
    public static class QuestVulkanRuntimePolicy
    {
        public const int QuestMemoryGateMegabytes = 8000;

        private const int QuestFamilyMemoryCeilingMegabytes = 9000;

        private static bool _initialized;
        private static bool _isAndroid;
        private static bool _isVulkan;
        private static bool _questMemoryGate;
        private static bool _questFamilyMemoryGate;
        private static bool _questDeviceSignature;
        private static int _systemMemoryMegabytes;

        public static int SystemMemoryMegabytes => _systemMemoryMegabytes;

        public static bool IsQuestMemoryGate => _questMemoryGate;

        public static bool IsQuestVulkanCandidate =>
            _initialized &&
            _isAndroid &&
            _isVulkan &&
            (_questMemoryGate || _questFamilyMemoryGate || _questDeviceSignature);

        public static bool IsQuestRuntimeActive =>
            IsQuestVulkanCandidate &&
            (HectonXRRuntimeState.IsXRActive || XRSettings.enabled || XRSettings.isDeviceActive);

        public static bool UseDepthlessTBDRPath => IsQuestRuntimeActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
            _isAndroid = false;
            _isVulkan = false;
            _questMemoryGate = false;
            _questFamilyMemoryGate = false;
            _questDeviceSignature = false;
            _systemMemoryMegabytes = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _systemMemoryMegabytes = Mathf.Max(0, SystemInfo.systemMemorySize);
            _isAndroid = Application.platform == RuntimePlatform.Android;
            _isVulkan = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;
            _questMemoryGate = _systemMemoryMegabytes > 0 && _systemMemoryMegabytes < QuestMemoryGateMegabytes;
            _questFamilyMemoryGate = _systemMemoryMegabytes > 0 && _systemMemoryMegabytes < QuestFamilyMemoryCeilingMegabytes;
            _questDeviceSignature =
                ContainsQuestToken(SystemInfo.deviceModel) ||
                ContainsQuestToken(SystemInfo.deviceName) ||
                ContainsQuestToken(XRSettings.loadedDeviceName);
            _initialized = true;
        }

        private static bool ContainsQuestToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Deprecated compatibility shim. Hardware foveation is owned by Graphics/VR/FoveatedRenderCommander.
    /// </summary>
    [Obsolete("Use Hecton8.Graphics.VR.FoveatedRenderCommander. This shim exists only to keep old serialized components from becoming missing scripts.")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9820)]
    [AddComponentMenu("")]
    public sealed class OculusFfrEnforcer : MonoBehaviour
    {
        private void OnEnable()
        {
            enabled = false;
        }
    }
}
