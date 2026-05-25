using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Narrow runtime route for consumers that only need pooled RenderTexture rent/return.
    /// </summary>
    public interface IRenderTexturePoolService
    {
        float PoolHitRate { get; }

        int TotalPooledCount { get; }

        RenderTexture Rent(int width, int height, RenderTextureFormat format, Component owner);

        RenderTexture Rent(int width, int height, RenderTextureFormat format, Component owner, int depthBits);

        void Return(RenderTexture rt);

        void ClearAllPools();

        void ReclaimPdaRenderTextures();
    }
}
