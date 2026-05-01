using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor entrypoint for wreck primitive-collider fitting.
    /// </summary>
    public static class WreckColliderFitter
    {
        /// <summary>
        /// Fits primitive compound colliders for a single selected or generated wreck root.
        /// </summary>
        /// <param name="root">Root containing render meshes to approximate with primitive colliders.</param>
        /// <returns>Number of primitive collider children generated.</returns>
        public static int FitRoot(GameObject root)
        {
            return HectonCompoundColliderAutoFitter.BakeSelectionRoot(root);
        }

        [MenuItem("Hecton/Physics/Fit Wreck Colliders From Selection", priority = 216)]
        private static void FitSelectedWreckColliders()
        {
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
                FitRoot(selected[i]);
        }
    }
}
