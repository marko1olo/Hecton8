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
            RenderTextureMemoryBudgetBytes = 320L * 1024L * 1024L,
            TotalVRAMBudgetBytes = 1800L * 1024L * 1024L,
            VisorRTBudgetBytes = 64L * 1024L * 1024L,
            CameraRTBudgetBytes = 160L * 1024L * 1024L,
            PostFXRTBudgetBytes = 64L * 1024L * 1024L,
            UIRTBudgetBytes = 32L * 1024L * 1024L
        };
    }
}
