using System;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// VRAM budget thresholds for target hardware (NVIDIA MX350 2GB).
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VRAMBudgetThresholds
    {
        private const long BytesPerMegabyte = 1024L * 1024L;
        private const int DefaultTextureBudgetMegabytes = 900;
        private const int DefaultRenderTextureBudgetMegabytes = 320;
        private const int DefaultTotalBudgetMegabytes = 1800;
        private const int DefaultVisorRTBudgetMegabytes = 64;
        private const int DefaultCameraRTBudgetMegabytes = 160;
        private const int DefaultPostFXRTBudgetMegabytes = 64;
        private const int DefaultUIRTBudgetMegabytes = 32;

        /// <summary>
        /// Texture memory budget in bytes (default 900 MB).
        /// </summary>
        [Tooltip("Texture memory budget in bytes (default 900 MB).")]
        public long TextureMemoryBudgetBytes;
        
        /// <summary>
        /// RenderTexture memory budget in bytes (default 320 MB).
        /// </summary>
        [Tooltip("RenderTexture memory budget in bytes (default 320 MB).")]
        public long RenderTextureMemoryBudgetBytes;
        
        /// <summary>
        /// Total VRAM budget in bytes (default 1.8 GB hard ceiling for MX350).
        /// </summary>
        [Tooltip("Total VRAM budget in bytes (default 1.8 GB hard ceiling for MX350).")]
        public long TotalVRAMBudgetBytes;
        
        /// <summary>
        /// Visor subsystem RT budget in bytes (default 64 MB).
        /// </summary>
        [Tooltip("Visor subsystem RT budget in bytes (default 64 MB).")]
        public long VisorRTBudgetBytes;
        
        /// <summary>
        /// Camera subsystem RT budget in bytes (default 160 MB).
        /// </summary>
        [Tooltip("Camera subsystem RT budget in bytes (default 160 MB).")]
        public long CameraRTBudgetBytes;
        
        /// <summary>
        /// PostFX subsystem RT budget in bytes (default 64 MB).
        /// </summary>
        [Tooltip("PostFX subsystem RT budget in bytes (default 64 MB).")]
        public long PostFXRTBudgetBytes;
        
        /// <summary>
        /// UI subsystem RT budget in bytes (default 32 MB).
        /// </summary>
        [Tooltip("UI subsystem RT budget in bytes (default 32 MB).")]
        public long UIRTBudgetBytes;
        
        /// <summary>
        /// Returns the MX350 baseline budget thresholds.
        /// </summary>
        public static VRAMBudgetThresholds Default => new VRAMBudgetThresholds
        {
            TextureMemoryBudgetBytes = MegabytesToBytes(DefaultTextureBudgetMegabytes),
            RenderTextureMemoryBudgetBytes = MegabytesToBytes(DefaultRenderTextureBudgetMegabytes),
            TotalVRAMBudgetBytes = MegabytesToBytes(DefaultTotalBudgetMegabytes),
            VisorRTBudgetBytes = MegabytesToBytes(DefaultVisorRTBudgetMegabytes),
            CameraRTBudgetBytes = MegabytesToBytes(DefaultCameraRTBudgetMegabytes),
            PostFXRTBudgetBytes = MegabytesToBytes(DefaultPostFXRTBudgetMegabytes),
            UIRTBudgetBytes = MegabytesToBytes(DefaultUIRTBudgetMegabytes)
        };

        /// <summary>
        /// Returns profile-aware runtime thresholds for known hardware, otherwise the MX350 baseline.
        /// </summary>
        /// <remarks>Cold-path only; callers should cache the returned value.</remarks>
        public static VRAMBudgetThresholds RuntimeDefault
        {
            get
            {
                HardwareTierDetector.EnsureInitialized();
                if (HardwareTierDetector.IsSteamDeckLike)
                {
                    return CreateProfileThresholds(
                        HardwareProfileCatalog.SteamDeckLcdGraphicsBudgetMegabytes,
                        HardwareProfileCatalog.SteamDeckLcdTextureBudgetMegabytes,
                        HardwareProfileCatalog.SteamDeckLcdRenderTargetBudgetMegabytes);
                }

                if (HardwareTierDetector.IsQuest3Like)
                {
                    return CreateProfileThresholds(
                        HardwareProfileCatalog.Quest3GraphicsBudgetMegabytes,
                        HardwareProfileCatalog.Quest3TextureBudgetMegabytes,
                        HardwareProfileCatalog.Quest3RenderTargetBudgetMegabytes);
                }

                return Default;
            }
        }

        /// <summary>
        /// Replaces only untouched MX350 default thresholds with profile-aware runtime thresholds.
        /// </summary>
        /// <param name="current">Current serialized threshold values.</param>
        /// <returns>Profile-aware thresholds when the input is the untouched default; otherwise the input value.</returns>
        public static VRAMBudgetThresholds ResolveRuntimeBudget(VRAMBudgetThresholds current)
        {
            return IsUnsetBudget(current) || IsDefaultBudget(current) ? RuntimeDefault : current;
        }

        private static VRAMBudgetThresholds CreateProfileThresholds(
            int totalBudgetMegabytes,
            int textureBudgetMegabytes,
            int renderTargetBudgetMegabytes)
        {
            VRAMBudgetThresholds thresholds = Default;
            thresholds.TotalVRAMBudgetBytes = MegabytesToBytes(totalBudgetMegabytes);
            thresholds.TextureMemoryBudgetBytes = MegabytesToBytes(textureBudgetMegabytes);
            thresholds.RenderTextureMemoryBudgetBytes = MegabytesToBytes(renderTargetBudgetMegabytes);
            thresholds.VisorRTBudgetBytes = ScaleRenderTargetBudget(DefaultVisorRTBudgetMegabytes, renderTargetBudgetMegabytes);
            thresholds.CameraRTBudgetBytes = ScaleRenderTargetBudget(DefaultCameraRTBudgetMegabytes, renderTargetBudgetMegabytes);
            thresholds.PostFXRTBudgetBytes = ScaleRenderTargetBudget(DefaultPostFXRTBudgetMegabytes, renderTargetBudgetMegabytes);
            thresholds.UIRTBudgetBytes = ScaleRenderTargetBudget(DefaultUIRTBudgetMegabytes, renderTargetBudgetMegabytes);
            return thresholds;
        }

        private static bool IsDefaultBudget(VRAMBudgetThresholds current)
        {
            VRAMBudgetThresholds defaults = Default;
            return current.TextureMemoryBudgetBytes == defaults.TextureMemoryBudgetBytes &&
                   current.RenderTextureMemoryBudgetBytes == defaults.RenderTextureMemoryBudgetBytes &&
                   current.TotalVRAMBudgetBytes == defaults.TotalVRAMBudgetBytes &&
                   current.VisorRTBudgetBytes == defaults.VisorRTBudgetBytes &&
                   current.CameraRTBudgetBytes == defaults.CameraRTBudgetBytes &&
                   current.PostFXRTBudgetBytes == defaults.PostFXRTBudgetBytes &&
                   current.UIRTBudgetBytes == defaults.UIRTBudgetBytes;
        }

        private static bool IsUnsetBudget(VRAMBudgetThresholds current)
        {
            return current.TextureMemoryBudgetBytes <= 0L &&
                   current.RenderTextureMemoryBudgetBytes <= 0L &&
                   current.TotalVRAMBudgetBytes <= 0L &&
                   current.VisorRTBudgetBytes <= 0L &&
                   current.CameraRTBudgetBytes <= 0L &&
                   current.PostFXRTBudgetBytes <= 0L &&
                   current.UIRTBudgetBytes <= 0L;
        }

        private static long ScaleRenderTargetBudget(int defaultMegabytes, int renderTargetBudgetMegabytes)
        {
            return MegabytesToBytes((defaultMegabytes * renderTargetBudgetMegabytes) / DefaultRenderTextureBudgetMegabytes);
        }

        private static long MegabytesToBytes(int megabytes)
        {
            return (long)megabytes * BytesPerMegabyte;
        }
    }
}
