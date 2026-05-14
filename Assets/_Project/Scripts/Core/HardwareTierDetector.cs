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
        private const int DefaultVramBudgetMegabytes = 1600;
        private const int GenericSharedMemoryVramBudgetMegabytes = 960;
        private const int LowVramGraphicsMemoryMegabytes = 2048;
        private const int LowSystemMemoryMegabytes = 8192;

        private static bool _initialized;
        private static bool _isLegacyDirect3D11;
        private static bool _isDirect3D12;
        private static bool _isVulkan;
        private static bool _isMetal;
        private static bool _isSteamDeckLike;
        private static bool _isQuest3Like;
        private static bool _isSharedMemoryArchitecture;
        private static bool _allowComputeCulling;
        private static int _recommendedVramBudgetMegabytes;
        private static GraphicsDeviceType _graphicsDeviceType;

        /// <summary>True when the active backend is Direct3D11 and must not use compute-first culling.</summary>
        public static bool IsLegacyDirect3D11
        {
            get
            {
                EnsureInitialized();
                return _isLegacyDirect3D11;
            }
        }

        /// <summary>True when the active backend is Direct3D12.</summary>
        public static bool IsDirect3D12
        {
            get
            {
                EnsureInitialized();
                return _isDirect3D12;
            }
        }

        /// <summary>True when the active backend is Vulkan.</summary>
        public static bool IsVulkan
        {
            get
            {
                EnsureInitialized();
                return _isVulkan;
            }
        }

        /// <summary>True when the active backend is Metal.</summary>
        public static bool IsMetal
        {
            get
            {
                EnsureInitialized();
                return _isMetal;
            }
        }

        /// <summary>True for Steam Deck or a Linux handheld signature close enough to use Deck limits.</summary>
        public static bool IsSteamDeckLike
        {
            get
            {
                EnsureInitialized();
                return _isSteamDeckLike;
            }
        }

        /// <summary>True for Meta Quest 3 signatures that should use the generated Quest 3 profile.</summary>
        public static bool IsQuest3Like
        {
            get
            {
                EnsureInitialized();
                return _isQuest3Like;
            }
        }

        /// <summary>True when RAM and VRAM must be treated as a shared pressure pool.</summary>
        public static bool SharedMemoryModeActive
        {
            get
            {
                EnsureInitialized();
                return _isSharedMemoryArchitecture;
            }
        }

        /// <summary>True when compute culling is allowed by backend and platform policy.</summary>
        public static bool AllowComputeCulling
        {
            get
            {
                EnsureInitialized();
                return _allowComputeCulling;
            }
        }

        /// <summary>Recommended runtime VRAM budget after shared-memory clamps.</summary>
        public static int RecommendedVramBudgetMegabytes
        {
            get
            {
                EnsureInitialized();
                return _recommendedVramBudgetMegabytes;
            }
        }

        /// <summary>Recommended runtime VRAM budget in bytes after shared-memory clamps.</summary>
        public static long RecommendedVramBudgetBytes => (long)RecommendedVramBudgetMegabytes << 20;

        /// <summary>Active graphics backend captured from Unity.</summary>
        public static GraphicsDeviceType ActiveGraphicsDeviceType
        {
            get
            {
                EnsureInitialized();
                return _graphicsDeviceType;
            }
        }

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
            _recommendedVramBudgetMegabytes = DefaultVramBudgetMegabytes;
            _graphicsDeviceType = GraphicsDeviceType.Null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
            if (_isLegacyDirect3D11 || _isSteamDeckLike || _isSharedMemoryArchitecture)
                GlobalRegistry.RegisterScalabilityTierOverride(ScalabilityTierProfiles.LowMx350);
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
                return HardwareProfileCatalog.ResolveSharedMemoryGraphicsBudgetMegabytes(
                    steamDeckLike,
                    quest3Like,
                    GenericSharedMemoryVramBudgetMegabytes);
            }

            int reportedGraphicsMemory = SystemInfo.graphicsMemorySize;
            if (reportedGraphicsMemory <= 0)
                return DefaultVramBudgetMegabytes;

            return Mathf.Min(DefaultVramBudgetMegabytes, Mathf.Max(512, reportedGraphicsMemory));
        }

        private static bool DetectSharedMemoryArchitecture(bool steamDeckLike, bool quest3Like)
        {
            if (steamDeckLike || quest3Like)
                return true;

            int graphicsMemory = SystemInfo.graphicsMemorySize;
            int systemMemory = SystemInfo.systemMemorySize;
            if (graphicsMemory > 0 &&
                graphicsMemory <= LowVramGraphicsMemoryMegabytes &&
                systemMemory > 0 &&
                systemMemory <= LowSystemMemoryMegabytes &&
                (_isVulkan || _isMetal))
            {
                return true;
            }

            return false;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
