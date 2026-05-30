using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    /// <summary>
    /// Cached platform and graphics-backend policy for compatibility gates.
    /// </summary>
    public static class HardwareTierDetector
    {
        private const int DefaultVramBudgetMegabytes = 1800;
        private const int GenericSharedMemoryVramBudgetMegabytes = 960;
        private const int GenericSharedMemoryMidBudgetMegabytes = 1536;
        private const int GenericSharedMemoryHighBudgetMegabytes = 2048;
        private const int CompactDiscreteVramBudgetMegabytes = 1800;
        private const int MidDiscreteVramBudgetMegabytes = 3072;
        private const int HighDiscreteVramBudgetMegabytes = 4096;
        private const int UltraDiscreteVramBudgetMegabytes = 6144;
        private const int LowVramGraphicsMemoryMegabytes = 2048;
        private const int MidVramGraphicsMemoryMegabytes = 4096;
        private const int HighVramGraphicsMemoryMegabytes = 8192;
        private const int LowSystemMemoryMegabytes = 8192;
        private const int MidSystemMemoryMegabytes = 16384;

        private static bool _initialized;
        private static bool _isLegacyDirect3D11;
        private static bool _isDirect3D12;
        private static bool _isVulkan;
        private static bool _isMetal;
        private static bool _isSteamDeckLike;
        private static bool _isQuest3Like;
        private static bool _isSharedMemoryArchitecture;
        private static bool _allowComputeCulling;
        private static bool _allowHighResourceComputeShaders;
        private static int _recommendedVramBudgetMegabytes = DefaultVramBudgetMegabytes;
        private static GraphicsDeviceType _graphicsDeviceType;

        /// <summary>True when the active backend is Direct3D11 and must not use compute-first culling.</summary>
        public static bool IsLegacyDirect3D11 => _isLegacyDirect3D11;

        /// <summary>True when the active backend is Direct3D12.</summary>
        public static bool IsDirect3D12 => _isDirect3D12;

        /// <summary>True when the active backend is Vulkan.</summary>
        public static bool IsVulkan => _isVulkan;

        /// <summary>True when the active backend is Metal.</summary>
        public static bool IsMetal => _isMetal;

        /// <summary>True for the known SteamOS handheld signature lane.</summary>
        public static bool IsSteamDeckLike => _isSteamDeckLike;

        /// <summary>True for Meta Quest 3 signatures that should use the generated Quest 3 profile.</summary>
        public static bool IsQuest3Like => _isQuest3Like;

        /// <summary>True when RAM and VRAM must be treated as a shared pressure pool.</summary>
        public static bool SharedMemoryModeActive => _isSharedMemoryArchitecture;

        /// <summary>True when compute culling is allowed by backend and platform policy.</summary>
        public static bool AllowComputeCulling => _allowComputeCulling;

        /// <summary>True when compute is supported on a desktop/proven backend above mobile resource-risk lanes.</summary>
        public static bool AllowHighResourceComputeShaders => _allowHighResourceComputeShaders;

        /// <summary>Recommended runtime VRAM budget after shared-memory clamps.</summary>
        public static int RecommendedVramBudgetMegabytes => _recommendedVramBudgetMegabytes;

        /// <summary>Recommended runtime VRAM budget in bytes after shared-memory clamps.</summary>
        public static long RecommendedVramBudgetBytes => (long)_recommendedVramBudgetMegabytes << 20;

        /// <summary>Active graphics backend captured from Unity.</summary>
        public static GraphicsDeviceType ActiveGraphicsDeviceType => _graphicsDeviceType;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
            _isLegacyDirect3D11 = false;
            _isDirect3D12 = false;
            _isVulkan = false;
            _isMetal = false;
            _isSteamDeckLike = false;
            _isQuest3Like = false;
            _isSharedMemoryArchitecture = false;
            _allowComputeCulling = false;
            _allowHighResourceComputeShaders = false;
            _recommendedVramBudgetMegabytes = DefaultVramBudgetMegabytes;
            _graphicsDeviceType = GraphicsDeviceType.Null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Forces first-use initialization for bootstrap owners that need policy before scene load.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _graphicsDeviceType = SystemInfo.graphicsDeviceType;
            _isLegacyDirect3D11 = _graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            _isDirect3D12 = _graphicsDeviceType == GraphicsDeviceType.Direct3D12;
            _isVulkan = _graphicsDeviceType == GraphicsDeviceType.Vulkan;
            _isMetal = _graphicsDeviceType == GraphicsDeviceType.Metal;
            _isSteamDeckLike = DetectSteamDeckLike();
            _isQuest3Like = DetectQuest3Like();
            _isSharedMemoryArchitecture = DetectSharedMemoryArchitecture(_isSteamDeckLike, _isQuest3Like);
            _allowComputeCulling =
                SystemInfo.supportsComputeShaders &&
                !_isLegacyDirect3D11;
            _allowHighResourceComputeShaders =
                SystemInfo.supportsComputeShaders &&
                !_isSharedMemoryArchitecture &&
                !(Application.isMobilePlatform && (_isVulkan || _isMetal)) &&
                !(_isVulkan && (_isQuest3Like || _isSteamDeckLike)) &&
                (_isLegacyDirect3D11 || _isDirect3D12 || _isVulkan || _isMetal);
            _recommendedVramBudgetMegabytes = ResolveRecommendedVramBudgetMegabytes(
                _isSharedMemoryArchitecture,
                _isSteamDeckLike,
                _isQuest3Like);
            _initialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveRecommendedVramBudgetMegabytes(bool sharedMemory, bool steamDeckLike, bool quest3Like)
        {
            if (sharedMemory)
            {
                int genericBudget = ResolveGenericSharedMemoryBudgetMegabytes();
                return HardwareProfileCatalog.ResolveSharedMemoryGraphicsBudgetMegabytes(
                    steamDeckLike,
                    quest3Like,
                    genericBudget);
            }

            int reportedGraphicsMemory = SystemInfo.graphicsMemorySize;
            if (reportedGraphicsMemory <= 0)
                return DefaultVramBudgetMegabytes;

            int targetBudget = reportedGraphicsMemory <= LowVramGraphicsMemoryMegabytes
                ? CompactDiscreteVramBudgetMegabytes
                : reportedGraphicsMemory <= MidVramGraphicsMemoryMegabytes
                    ? MidDiscreteVramBudgetMegabytes
                    : reportedGraphicsMemory <= HighVramGraphicsMemoryMegabytes
                        ? HighDiscreteVramBudgetMegabytes
                        : UltraDiscreteVramBudgetMegabytes;

            return Mathf.Clamp(targetBudget, 512, reportedGraphicsMemory);
        }

        private static bool DetectSharedMemoryArchitecture(bool steamDeckLike, bool quest3Like)
        {
            if (steamDeckLike || quest3Like)
                return true;

            if (Application.isMobilePlatform)
                return true;

            if (DetectIntegratedGraphicsSignature())
                return true;

            if (_isMetal && ContainsIgnoreCase(SystemInfo.processorType, "apple"))
                return true;

            int graphicsMemory = SystemInfo.graphicsMemorySize;
            int systemMemory = SystemInfo.systemMemorySize;
            if (graphicsMemory > 0 &&
                graphicsMemory <= LowVramGraphicsMemoryMegabytes &&
                systemMemory > 0 &&
                systemMemory <= MidSystemMemoryMegabytes &&
                (_isDirect3D12 || _isVulkan || _isMetal))
            {
                return true;
            }

            return false;
        }

        private static int ResolveGenericSharedMemoryBudgetMegabytes()
        {
            int systemMemory = SystemInfo.systemMemorySize;
            int budget = systemMemory > MidSystemMemoryMegabytes
                ? GenericSharedMemoryHighBudgetMegabytes
                : systemMemory > LowSystemMemoryMegabytes
                    ? GenericSharedMemoryMidBudgetMegabytes
                    : GenericSharedMemoryVramBudgetMegabytes;

            int reportedGraphicsMemory = SystemInfo.graphicsMemorySize;
            if (reportedGraphicsMemory > 0)
                budget = Mathf.Min(budget, Mathf.Max(512, reportedGraphicsMemory));

            return budget;
        }

        private static bool DetectSteamDeckLike()
        {
            return ContainsIgnoreCase(SystemInfo.operatingSystem, "steam") ||
                   ContainsIgnoreCase(SystemInfo.deviceModel, "steam") ||
                   ContainsIgnoreCase(SystemInfo.deviceName, "steam") ||
                   ContainsIgnoreCase(SystemInfo.processorType, "custom amd aerith") ||
                   ContainsIgnoreCase(SystemInfo.processorType, "van gogh");
        }

        private static bool DetectQuest3Like()
        {
            return ContainsIgnoreCase(SystemInfo.deviceModel, "quest 3") ||
                   ContainsIgnoreCase(SystemInfo.deviceModel, "quest3") ||
                   ContainsIgnoreCase(SystemInfo.deviceName, "quest 3") ||
                   ContainsIgnoreCase(SystemInfo.deviceName, "quest3") ||
                   ContainsIgnoreCase(SystemInfo.operatingSystem, "quest 3") ||
                   ContainsIgnoreCase(SystemInfo.processorType, "xr2 gen 2");
        }

        private static bool DetectIntegratedGraphicsSignature()
        {
            string deviceName = SystemInfo.graphicsDeviceName;
            string vendor = SystemInfo.graphicsDeviceVendor;
            string processor = SystemInfo.processorType;
            return ContainsIgnoreCase(deviceName, "intel uhd") ||
                   ContainsIgnoreCase(deviceName, "intel iris") ||
                   ContainsIgnoreCase(deviceName, "intel arc graphics") ||
                   ContainsIgnoreCase(deviceName, "radeon graphics") ||
                   ContainsIgnoreCase(deviceName, "amd radeon(tm) graphics") ||
                   ContainsIgnoreCase(deviceName, "adreno") ||
                   ContainsIgnoreCase(deviceName, "mali") ||
                   ContainsIgnoreCase(deviceName, "apple") ||
                   ContainsIgnoreCase(vendor, "qualcomm") ||
                   ContainsIgnoreCase(vendor, "arm") ||
                   ContainsIgnoreCase(processor, "apple");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
