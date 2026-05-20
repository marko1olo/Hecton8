using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    [DisallowMultipleComponent]
    public sealed class OfflineWreckagePreviewGizmo : MonoBehaviour
    {
        [SerializeField, Tooltip("Draws the current Wreckage Forge preview mesh as an editor-only wireframe.")]
        private bool drawPreview = true;

        private void OnDrawGizmos()
        {
            if (!drawPreview || !OfflineWreckagePreviewStore.HasPreview || OfflineWreckagePreviewStore.Mesh == null)
                return;

            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.75f);
            Gizmos.DrawWireMesh(OfflineWreckagePreviewStore.Mesh, transform.position, transform.rotation, transform.lossyScale);
        }
    }

    public static class OfflineWreckagePreviewStore
    {
        public static Mesh Mesh;
        public static bool HasPreview;

        public static void SetMesh(Mesh mesh)
        {
            Dispose();
            Mesh = mesh;
            if (Mesh != null)
                Mesh.hideFlags = HideFlags.HideAndDontSave;
            HasPreview = Mesh != null;
        }

        public static void Dispose()
        {
            if (Mesh != null)
            {
                Object.DestroyImmediate(Mesh);
                Mesh = null;
            }

            HasPreview = false;
        }
    }

    [InitializeOnLoad]
    internal static class OfflineWreckagePreviewLifecycle
    {
        static OfflineWreckagePreviewLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposePreview;
            AssemblyReloadEvents.beforeAssemblyReload += DisposePreview;
            EditorApplication.quitting -= DisposePreview;
            EditorApplication.quitting += DisposePreview;
        }

        private static void DisposePreview()
        {
            OfflineWreckagePreviewStore.Dispose();
            OfflineWreckageBlackBox.Dispose();
        }
    }
}
