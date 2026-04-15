using System;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// VRAM budget thresholds for target hardware (NVIDIA MX350 2GB).
    /// </summary>
    [Serializable]
    public struct VRAMBudgetThresholds
    {
        /// <summary>
        /// Texture memory budget in bytes (default 900 MB).
        /// </summary>
        [Tooltip("Texture memory budget in bytes (default 900 MB).")]
        public long TextureMemoryBudgetBytes;
        
        /// <summary>
        /// RenderTexture memory budget in bytes (default 500 MB).
        /// </summary>
        [Tooltip("RenderTexture memory budget in bytes (default 500 MB).")]
        public long RenderTextureMemoryBudgetBytes;
        
        /// <summary>
        /// Total VRAM budget in bytes (default 1.2 GB).
        /// </summary>
        [Tooltip("Total VRAM budget in bytes (default 1.2 GB).")]
        public long TotalVRAMBudgetBytes;
        
        /// <summary>
        /// Visor subsystem RT budget in bytes (default 64 MB).
        /// </summary>
        [Tooltip("Visor subsystem RT budget in bytes (default 64 MB).")]
        public long VisorRTBudgetBytes;
        
        /// <summary>
        /// Camera subsystem RT budget in bytes (default 256 MB).
        /// </summary>
        [Tooltip("Camera subsystem RT budget in bytes (default 256 MB).")]
        public long CameraRTBudgetBytes;
        
        /// <summary>
        /// PostFX subsystem RT budget in bytes (default 128 MB).
        /// </summary>
        [Tooltip("PostFX subsystem RT budget in bytes (default 128 MB).")]
        public long PostFXRTBudgetBytes;
        
        /// <summary>
        /// UI subsystem RT budget in bytes (default 64 MB).
        /// </summary>
        [Tooltip("UI subsystem RT budget in bytes (default 64 MB).")]
        public long UIRTBudgetBytes;
        
        public static VRAMBudgetThresholds Default => new VRAMBudgetThresholds
        {
            TextureMemoryBudgetBytes = 900L * 1024L * 1024L,
            RenderTextureMemoryBudgetBytes = 500L * 1024L * 1024L,
            TotalVRAMBudgetBytes = 1200L * 1024L * 1024L,
            VisorRTBudgetBytes = 64L * 1024L * 1024L,
            CameraRTBudgetBytes = 256L * 1024L * 1024L,
            PostFXRTBudgetBytes = 128L * 1024L * 1024L,
            UIRTBudgetBytes = 64L * 1024L * 1024L
        };
    }
}
