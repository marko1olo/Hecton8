using UnityEngine;

namespace Hecton8.Optimization
{
    public enum RenderTextureOwnerCategory : byte
    {
        Other = 0,
        Visor = 1,
        Camera = 2,
        PostFX = 3,
        UI = 4
    }

    /// <summary>
    /// Record of a RenderTexture allocation for lifecycle tracking.
    /// </summary>
    public struct RenderTextureAllocationRecord
    {
        /// <summary>
        /// RenderTexture instance.
        /// </summary>
        public RenderTexture RenderTexture;
        
        /// <summary>
        /// Owner component (MonoBehaviour).
        /// </summary>
        public Component Owner;

        /// <summary>
        /// Cached owner category. Resolved once at registration to keep SlowTick scans free of type-name work.
        /// </summary>
        public RenderTextureOwnerCategory OwnerCategory;
        
        /// <summary>
        /// RT width in pixels.
        /// </summary>
        public int Width;
        
        /// <summary>
        /// RT height in pixels.
        /// </summary>
        public int Height;
        
        /// <summary>
        /// RT format (R8, RG16, ARGB64, RGBA32).
        /// </summary>
        public RenderTextureFormat Format;
        
        /// <summary>
        /// Allocation timestamp (Time.time).
        /// </summary>
        public float AllocationTime;
        
        /// <summary>
        /// Optional stack trace for leak debugging.
        /// </summary>
        public string AllocationStackTrace;
        
        /// <summary>
        /// Whether RT has been disposed.
        /// </summary>
        public bool IsDisposed;
        
        /// <summary>
        /// Calculates memory consumption in bytes.
        /// </summary>
        public long MemoryBytes => CalculateMemoryBytes(Width, Height, Format);
        
        private static long CalculateMemoryBytes(int width, int height, RenderTextureFormat format)
        {
            int bpp = format switch
            {
                RenderTextureFormat.R8 => 8,
                RenderTextureFormat.RG16 => 16,
                RenderTextureFormat.ARGB64 => 64,
                RenderTextureFormat.ARGB32 => 32,
                RenderTextureFormat.DefaultHDR => 64,
                _ => 32
            };
            return (long)width * height * bpp / 8;
        }
    }
}
