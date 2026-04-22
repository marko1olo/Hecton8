using UnityEditor;

namespace Hecton8.Editor
{
    /// <summary>
    /// Batch entry points for Crest migration tooling.
    /// </summary>
    public static class CrestMigrationBatch
    {
        /// <summary>
        /// Executes the Crest 4 dump menu item in batch mode.
        /// </summary>
        public static void Dump()
        {
            if (!EditorApplication.ExecuteMenuItem("Tools/Hecton8/Crest/Dump Crest 4 Settings"))
                throw new System.InvalidOperationException("Failed to execute Crest 4 dump menu item.");
        }

        /// <summary>
        /// Executes the Crest 5 parallel scene build menu item in batch mode.
        /// </summary>
        public static void BuildParallelScene()
        {
            if (!EditorApplication.ExecuteMenuItem("Tools/Hecton8/Crest/Build Crest 5 Parallel Scene"))
                throw new System.InvalidOperationException("Failed to execute Crest 5 parallel scene menu item.");
        }
    }
}
