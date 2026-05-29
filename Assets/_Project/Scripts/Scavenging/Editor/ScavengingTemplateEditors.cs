#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.Scavenging.Editor
{
    [CustomEditor(typeof(ResourceNodeTemplate))]
    internal sealed class ResourceNodeTemplateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ResourceNodeTemplate template = (ResourceNodeTemplate)target;
            DrawDefaultInspector();
            template.RefreshValidationState();

            EditorGUILayout.Space(6f);
            DrawResourceValidation(template);

            if (GUILayout.Button("Refresh Validation"))
                template.RefreshValidationState();
        }

        private static void DrawResourceValidation(ResourceNodeTemplate template)
        {
            MessageType messageType = template.HasValidationErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"Runtime yield rows: {template.ValidationRuntimeYieldEntryCount} | Invalid yield rows: {template.ValidationInvalidYieldEntryCount} | Duplicate yield item rows: {template.ValidationDuplicateYieldItemHashCount}\n" +
                $"Runtime rarity rows: {template.ValidationRuntimeRarityDropCount} | Invalid rarity rows: {template.ValidationInvalidRarityDropCount} | Duplicate rarity key rows: {template.ValidationDuplicateRarityDropKeyCount}",
                messageType);

            if (template.ValidationFirstInvalidYieldEntryIndex >= 0)
                EditorGUILayout.LabelField("First invalid yield row", template.ValidationFirstInvalidYieldEntryIndex.ToString());

            if (template.ValidationFirstDuplicateYieldItemHashIndex >= 0)
                EditorGUILayout.LabelField("First duplicate yield row", template.ValidationFirstDuplicateYieldItemHashIndex.ToString());

            if (template.ValidationFirstInvalidRarityDropIndex >= 0)
                EditorGUILayout.LabelField("First invalid rarity row", template.ValidationFirstInvalidRarityDropIndex.ToString());

            if (template.ValidationFirstDuplicateRarityDropKeyIndex >= 0)
                EditorGUILayout.LabelField("First duplicate rarity key row", template.ValidationFirstDuplicateRarityDropKeyIndex.ToString());
        }
    }

    [CustomEditor(typeof(HarvestableTemplate))]
    internal sealed class HarvestableTemplateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            HarvestableTemplate template = (HarvestableTemplate)target;
            DrawDefaultInspector();
            template.RefreshValidationState();

            EditorGUILayout.Space(6f);
            DrawHarvestableValidation(template);

            if (GUILayout.Button("Refresh Validation"))
                template.RefreshValidationState();
        }

        private static void DrawHarvestableValidation(HarvestableTemplate template)
        {
            MessageType messageType = template.HasValidationErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"Runtime loot rows: {template.ValidationRuntimeLootEntryCount} | Invalid loot rows: {template.ValidationInvalidLootEntryCount} | Duplicate loot item rows: {template.ValidationDuplicateLootItemHashCount}",
                messageType);

            if (template.ValidationFirstInvalidLootEntryIndex >= 0)
                EditorGUILayout.LabelField("First invalid loot row", template.ValidationFirstInvalidLootEntryIndex.ToString());

            if (template.ValidationFirstDuplicateLootItemHashIndex >= 0)
                EditorGUILayout.LabelField("First duplicate loot row", template.ValidationFirstDuplicateLootItemHashIndex.ToString());
        }
    }
}
#endif
