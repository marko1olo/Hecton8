using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.ProceduralGen
{
    /// <summary>
    /// Editor window for offline Bio-Forge L-system and SDF asset baking.
    /// </summary>
    public sealed class BioForgeWindow : EditorWindow
    {
        private BioRuleData _rule;
        private int _seed = 12648430;
        private string _nameOverride = string.Empty;

        [MenuItem("HECTON-8/Bio-Forge", false, 170)]
        private static void Open()
        {
            var window = GetWindow<BioForgeWindow>("Bio-Forge");
            window.minSize = new Vector2(390f, 220f);
            window.TryAdoptSelection();
        }

        [MenuItem("HECTON-8/Bio-Forge/Create Default Rule", false, 171)]
        private static void CreateDefaultRule()
        {
            BioForgeGenerator.CreateDefaultRuleAsset();
        }

        private void OnSelectionChange()
        {
            TryAdoptSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline L-Systems & SDF Meshing", EditorStyles.boldLabel);
            _rule = (BioRuleData)EditorGUILayout.ObjectField("Bio Rule Data", _rule, typeof(BioRuleData), false);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _nameOverride = EditorGUILayout.TextField("Name Override", _nameOverride);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_rule == null))
            {
                if (GUILayout.Button("Generate Selected Flora"))
                    BioForgeGenerator.GenerateFlora(_rule, _seed, _nameOverride);

                if (GUILayout.Button("Generate Rock Variant"))
                    BioForgeGenerator.GenerateRock(_rule, _seed, _nameOverride);

                if (GUILayout.Button("Generate 100 Flora Variations"))
                    BioForgeGenerator.GenerateFloraBatch(_rule, _seed, _nameOverride);

                if (GUILayout.Button("Generate 100 Rock Variations"))
                    BioForgeGenerator.GenerateRockBatch(_rule, _seed, _nameOverride);
            }

            if (_rule == null)
            {
                EditorGUILayout.HelpBox("Assign or create a BioRuleData asset. Generation is editor-only and writes mesh assets plus an LODGroup prefab.", MessageType.Info);
                if (GUILayout.Button("Create Default Bio Rule"))
                    _rule = BioForgeGenerator.CreateDefaultRuleAsset();
            }
        }

        private void TryAdoptSelection()
        {
            if (Selection.activeObject is BioRuleData selectedRule)
                _rule = selectedRule;
        }
    }
}
