#if UNITY_EDITOR
using UnityEditor;

namespace Hecton8.Editor
{
    /// <summary>
    /// Batch entry points for graphics-domain validation and Crest depth-cache auditing.
    /// </summary>
    public static class HectonGraphicsBatchAudit
    {
        /// <summary>
        /// Runs the renderer validator against the currently active scene.
        /// </summary>
        public static void ValidateActiveScene()
        {
            HectonRenderPipelineValidator.RunBatchValidation();
        }

        /// <summary>
        /// Opens the production world scene, restores URP renderer features, and logs depth-cache coverage.
        /// </summary>
        public static void ValidateWorldScene()
        {
            HectonRenderPipelineValidator.RunBatchWorldValidation();
        }
    }
}
#endif
