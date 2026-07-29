#if UNITY_EDITOR
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Generators.World
{
    internal sealed class AbyssalScatterPolisherWindow : EditorWindow
    {
        private string _mapMagicOutputFolder = AbyssalScatterPolisherPipeline.DefaultMapMagicOutputFolder;
        private string _cullingDatasetFolder = AbyssalScatterPolisherPipeline.DefaultCullingDatasetFolder;
        private string _outputName = AbyssalScatterPolisherPipeline.DefaultAssetName;
        private int _instanceCount = 100000;
        private int _boundsCount = 500;
        private float _globalQualityWeight = 1f;
        private AbyssalScatterBakeResult _lastResult;
        private bool _hasScan;
        private bool _hasResult;

        [MenuItem("Hecton8/World Scatter/Abyssal Scatter Polisher", priority = 1614)]
        public static void Open()
        {
            AbyssalScatterPolisherWindow window = GetWindow<AbyssalScatterPolisherWindow>();
            window.titleContent = new GUIContent("Abyssal Scatter Polisher");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline Scatter Sources", EditorStyles.boldLabel);
            _mapMagicOutputFolder = EditorGUILayout.TextField("MapMagic Output Folder", _mapMagicOutputFolder);
            _cullingDatasetFolder = EditorGUILayout.TextField("Culling Dataset Folder", _cullingDatasetFolder);
            _outputName = EditorGUILayout.TextField("Output BRG Data", _outputName);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Bake Capacity", EditorStyles.boldLabel);
            _instanceCount = EditorGUILayout.IntSlider("Instances", _instanceCount, 1, AbyssalScatterPolisherConstants.MaxGraphicsBufferElements);
            _boundsCount = EditorGUILayout.IntSlider("Bounds", _boundsCount, 1, AbyssalScatterPolisherPipeline.MaxCullingBounds);
            _globalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", _globalQualityWeight, 0f, 1f);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Sources", GUILayout.Height(30f)))
                    ScanSources();

                if (GUILayout.Button("Polish and Bake", GUILayout.Height(30f)))
                    PolishAndBake();
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Source Scan (inventory only)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These counts are an inventory readout and are NOT bake input. Polish and Bake generates " +
                "synthetic instances (GenerateMockScatterInputJob / GenerateMockTerrainNormalsJob) around " +
                "the fixed sector origin in AbyssalScatterPolisherPipeline.DefaultConfig; it reads none of " +
                "the scanned rules, prefabs or MapMagic assets, and it does not sample real terrain. A " +
                "successful bake here is an ABI and throughput proof, not world content - do not report it " +
                "as placed scatter.",
                MessageType.Warning);
            if (!_hasScan)
            {
                EditorGUILayout.HelpBox("No 1614 source scan executed in this window session.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("MapMagic Folder", _lastResult.MapMagicSourceFolder);
                EditorGUILayout.LabelField("Culling Folder", _lastResult.CullingDatasetFolder);
                EditorGUILayout.LabelField("Rules", _lastResult.SourceRuleCount.ToString());
                EditorGUILayout.LabelField("Prefabs", _lastResult.SourcePrefabCount.ToString());
                EditorGUILayout.LabelField("MapMagic Assets", _lastResult.MapMagicAssetCount.ToString());
                if (!_lastResult.SourceFoldersValid)
                    EditorGUILayout.HelpBox("One or more source folders were invalid. The polisher used the default asset folders for this scan.", MessageType.Warning);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Last Bake", EditorStyles.boldLabel);
            if (!_hasResult)
            {
                EditorGUILayout.HelpBox("No 1614 bake executed in this window session.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("BRG Data", _lastResult.OutputPath);
            EditorGUILayout.LabelField("Metadata", _lastResult.MetadataPath);
            EditorGUILayout.LabelField("Prefab", _lastResult.PrefabPath);
            EditorGUILayout.LabelField("Instances", _lastResult.InstanceCount.ToString());
            EditorGUILayout.LabelField("Culling Bounds", _lastResult.CullingBoundsCount.ToString());
            EditorGUILayout.LabelField("Imported Bounds", _lastResult.ImportedCullingBoundsCount.ToString());
            EditorGUILayout.LabelField("Mock Bounds", _lastResult.MockCullingBoundsCount.ToString());
            if (_lastResult.ImportedCullingBoundsTruncated)
                EditorGUILayout.HelpBox("Imported culling bounds were truncated by the current Bounds cap.", MessageType.Warning);
            EditorGUILayout.LabelField("Culled", _lastResult.CulledCount.ToString());
            EditorGUILayout.LabelField("Non-finite Corrections", _lastResult.NonFiniteCount.ToString());
            EditorGUILayout.LabelField("Bytes", _lastResult.FileBytes.ToString());
            EditorGUILayout.LabelField("Align ms", _lastResult.AlignmentMilliseconds.ToString("0.###"));
            EditorGUILayout.LabelField("Cull ms", _lastResult.CullingMilliseconds.ToString("0.###"));
            EditorGUILayout.LabelField("Write ms", _lastResult.SerializationMilliseconds.ToString("0.###"));
            EditorGUILayout.LabelField("Total ms", _lastResult.TotalMilliseconds.ToString("0.###"));
            EditorGUILayout.LabelField("Renderer Controller", _lastResult.PrefabHasRendererController ? "GpuScatterLodManager" : "MISSING");
        }

        private void ScanSources()
        {
            AbyssalScatterPolisherPipeline.ScanScatterSourcesForFolders(_mapMagicOutputFolder, _cullingDatasetFolder, out _lastResult);
            _hasScan = true;
            _hasResult = false;
            Repaint();
        }

        private void PolishAndBake()
        {
            int count = math.clamp(_instanceCount, 1, AbyssalScatterPolisherConstants.MaxGraphicsBufferElements);
            int boundCount = math.max(1, _boundsCount);
            bool ok = AbyssalScatterPolisherPipeline.BakeMockScatterChunk(
                count,
                boundCount,
                math.saturate(_globalQualityWeight),
                _outputName,
                _mapMagicOutputFolder,
                _cullingDatasetFolder,
                out _lastResult);
            _hasScan = true;
            _hasResult = ok;
            if (ok)
            {
                AssetDatabase.Refresh();
                Repaint();
            }
        }
    }
}
#endif
