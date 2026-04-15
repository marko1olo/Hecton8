using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Recommendation for RenderTexture resolution optimization.
    /// </summary>
    public struct ResolutionOptimizationRecommendation
    {
        /// <summary>
        /// RenderTexture instance.
        /// </summary>
        public RenderTexture RenderTexture;
        
        /// <summary>
        /// Owner component.
        /// </summary>
        public Component Owner;
        
        /// <summary>
        /// Current width.
        /// </summary>
        public int CurrentWidth;
        
        /// <summary>
        /// Current height.
        /// </summary>
        public int CurrentHeight;
        
        /// <summary>
        /// Recommended width.
        /// </summary>
        public int RecommendedWidth;
        
        /// <summary>
        /// Recommended height.
        /// </summary>
        public int RecommendedHeight;
        
        /// <summary>
        /// Scale factor (0.25, 0.5, 0.75).
        /// </summary>
        public float Scale;
        
        /// <summary>
        /// RMSE (Root Mean Square Error) as percentage.
        /// </summary>
        public float RMSE;
        
        /// <summary>
        /// Memory savings in bytes.
        /// </summary>
        public long MemorySavingsBytes;
        
        /// <summary>
        /// Priority (higher = more important to optimize).
        /// </summary>
        public int Priority;
        
        /// <summary>
        /// Reason for recommendation.
        /// </summary>
        public string Reason;
    }
}
