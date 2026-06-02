using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.ColliderOptimization1609
{
    public sealed class ColliderOptimizationWindow1609 : EditorWindow
    {
        private DefaultAsset folder;
        private ColliderOptimizationStrategy1609 strategy;
        private ColliderOptimizationReport1609 lastReport;
        private string lastFolderPath = ColliderOptimizationEngine1609.PrefabRoot;
        private float globalQualityWeight = ColliderOptimizationEngine1609.DefaultGlobalQualityWeight;
        private Vector2 scroll;

        [MenuItem("HECTON-8/Physics/Collider Optimization Engine 1609", false, 180)]
        public static void Open()
        {
            GetWindow<ColliderOptimizationWindow1609>("Collider Optimization Engine");
        }

        [MenuItem("HECTON-8/Physics/1609 Audit Prefab MeshColliders", false, 181)]
        public static void AuditDefaultPrefabs()
        {
            ColliderOptimizationReport1609 report = ColliderOptimizationEngine1609.AuditPrefabs(ColliderOptimizationEngine1609.PrefabRoot, true);
            Debug.Log("[ColliderOptimization1609] Audit complete. High-poly MeshColliders=" + report.HighPolyMeshColliders + ", meshCollidersFound=" + report.MeshCollidersFound + ", executionMs=" + report.ExecutionMilliseconds);
        }

        [MenuItem("HECTON-8/Physics/1609 Purge Flora Colliders", false, 182)]
        public static void PurgeFlora()
        {
            ColliderOptimizationReport1609 report = ColliderOptimizationEngine1609.PurgeFloraColliders();
            Debug.Log("[ColliderOptimization1609] Flora purge complete. Deleted=" + report.FloraCollidersDeleted + ", modifiedPrefabs=" + report.PrefabsModified + ", executionMs=" + report.ExecutionMilliseconds);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Collider Optimization Engine", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            folder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab Folder", folder, typeof(DefaultAsset), false);
            strategy = (ColliderOptimizationStrategy1609)EditorGUILayout.EnumPopup("Strategy", strategy);
            globalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", globalQualityWeight, 0f, 1f);

            string folderPath = ResolveFolderPath();
            EditorGUILayout.LabelField("Resolved Folder", folderPath);
            if (strategy == ColliderOptimizationStrategy1609.PurgeAll)
                EditorGUILayout.HelpBox("PurgeAll is flora-filtered. Use the Purge Flora button for the full safe scenery pass.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Audit"))
                {
                    lastFolderPath = folderPath;
                    lastReport = ColliderOptimizationEngine1609.AuditPrefabs(folderPath, true);
                }

                if (GUILayout.Button("Optimize"))
                {
                    if (strategy == ColliderOptimizationStrategy1609.PurgeAll)
                    {
                        lastFolderPath = ColliderOptimizationEngine1609.FloraPurgeScopeLabel;
                        lastReport = ColliderOptimizationEngine1609.PurgeFloraColliders();
                    }
                    else
                    {
                        lastFolderPath = folderPath;
                        ColliderOptimizationSettings1609 settings = ColliderOptimizationSettings1609.FromGlobalQualityWeight(globalQualityWeight);
                        lastReport = ColliderOptimizationEngine1609.OptimizeFolder(folderPath, strategy, settings);
                    }
                }

                if (GUILayout.Button("Purge Flora"))
                {
                    lastFolderPath = ColliderOptimizationEngine1609.FloraPurgeScopeLabel;
                    lastReport = ColliderOptimizationEngine1609.PurgeFloraColliders();
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Last Folder", lastFolderPath);
            EditorGUILayout.LabelField("Prefabs Visited", lastReport.PrefabsVisited.ToString());
            EditorGUILayout.LabelField("Prefabs Modified", lastReport.PrefabsModified.ToString());
            EditorGUILayout.LabelField("Prefabs Failed", lastReport.PrefabsFailed.ToString());
            EditorGUILayout.LabelField("MeshColliders Found", lastReport.MeshCollidersFound.ToString());
            EditorGUILayout.LabelField("High-Poly MeshColliders", lastReport.HighPolyMeshColliders.ToString());
            EditorGUILayout.LabelField("MeshColliders Deleted", lastReport.MeshCollidersDeleted.ToString());
            EditorGUILayout.LabelField("Primitive Colliders Generated", lastReport.PrimitiveCollidersGenerated.ToString());
            EditorGUILayout.LabelField("Proxy Meshes Generated", lastReport.ProxyMeshesGenerated.ToString());
            EditorGUILayout.LabelField("Proxy Meshes Deleted", lastReport.ProxyMeshesDeleted.ToString());
            EditorGUILayout.LabelField("Flora Colliders Deleted", lastReport.FloraCollidersDeleted.ToString());
            EditorGUILayout.LabelField("Rigidbodies Tuned", lastReport.RigidbodiesTuned.ToString());
            EditorGUILayout.LabelField("CCD Stripped", lastReport.CcdStripped.ToString());
            EditorGUILayout.LabelField("Physics Triangles Removed", lastReport.VisualTrianglesRemovedFromPhysics.ToString());
            EditorGUILayout.LabelField("Global Quality Weight", lastReport.GlobalQualityWeight.ToString("0.000"));
            EditorGUILayout.LabelField("Technie Available", lastReport.TechnieAvailable ? "yes" : "no");
            EditorGUILayout.LabelField("Execution ms", lastReport.ExecutionMilliseconds.ToString());
            EditorGUILayout.EndScrollView();
        }

        private string ResolveFolderPath()
        {
            if (folder == null)
                return ColliderOptimizationEngine1609.PrefabRoot;

            string path = AssetDatabase.GetAssetPath(folder);
            return AssetDatabase.IsValidFolder(path) ? path : ColliderOptimizationEngine1609.PrefabRoot;
        }
    }
}
