using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Recommendation for RenderTexture format optimization.
    /// </summary>
    public struct FormatOptimizationRecommendation
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
        /// Current format.
        /// </summary>
        public RenderTextureFormat CurrentFormat;
        
        /// <summary>
        /// Recommended format.
        /// </summary>
        public RenderTextureFormat RecommendedFormat;
        
        /// <summary>
        /// Memory savings in bytes.
        /// </summary>
        public long MemorySavingsBytes;
        
        /// <summary>
        /// Reason for recommendation.
        /// </summary>
        public string Reason;
    }
}
