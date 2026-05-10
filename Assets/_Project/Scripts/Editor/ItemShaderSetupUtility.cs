// ============================================================================
// HECTON-8 — ItemShaderSetupUtility.cs
// Editor-utilita dlya massovoy nastroyki sheydera predmetov.
//
// OTVETSTVENNOSTI:
//   1. Nahodit vse prefaby s ItemData.
//   2. Primenyaet Hecton_Item_Highlight sheyder k materialam.
//   3. Dobavlyaet ItemHighlight.cs komponent.
//
// ISPOLZOVANIE:
//   Tools → Hecton → Setup Item Shaders
// ============================================================================

#if UNITY_EDITOR

using Hecton8.Gameplay;
using Hecton8.Items;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class ItemShaderSetupUtility
    {
        private const string ShaderPath = "Assets/_Project/Shaders/Hecton_Item_Highlight.shader";
        private const string HighlightShaderName = "Hecton/Item_Highlight";

        // ══════════════════════════════════════════════════════════
        //  MENU ITEMS
        // ══════════════════════════════════════════════════════════

        [MenuItem("Tools/Hecton/Setup Item Shaders", false, 100)]
        public static void SetupAllItemShaders()
        {
            Shader highlightShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (highlightShader == null)
            {
                // Probuem nayti po imeni
                highlightShader = Shader.Find(HighlightShaderName);
            }

            if (highlightShader == null)
            {
                Debug.LogError("[ItemShaderSetupUtility] Shader not found: " + ShaderPath);
                EditorUtility.DisplayDialog(
                    "Shader Not Found",
                    "Could not find Hecton_Item_Highlight shader.\n" +
                    "Make sure the shader exists at: " + ShaderPath,
                    "OK");
                return;
            }

            // Nahodim vse prefaby s ItemData
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            int processedCount = 0;
            int modifiedCount = 0;
            List<string> modifiedFiles = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // Proveryaem nalichie ItemData (na prefabe ili v dochernih obektah)
                ItemData itemData = prefab.GetComponent<ItemData>();
                if (itemData == null)
                    itemData = prefab.GetComponentInChildren<ItemData>();

                if (itemData == null) continue;

                processedCount++;

                // Proveryaem, nuzhno li modifitsirovat
                bool wasModified = false;

                // ── 1. Primenyaem sheyder k materialam ──
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null) continue;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null) continue;

                    for (int m = 0; m < materials.Length; m++)
                    {
                        Material mat = materials[m];
                        if (mat == null) continue;

                        // Propuskaem uzhe nastroennye materialy
                        if (mat.shader == highlightShader) continue;

                        // Primenyaem sheyder
                        mat.shader = highlightShader;
                        wasModified = true;
                    }
                }

                // ── 2. Dobavlyaem ItemHighlight komponent ──
                ItemHighlight highlight = prefab.GetComponent<ItemHighlight>();
                if (highlight == null)
                {
                    highlight = prefab.AddComponent<ItemHighlight>();
                    wasModified = true;
                }

                if (wasModified)
                {
                    modifiedCount++;
                    modifiedFiles.Add(path);
                    EditorUtility.SetDirty(prefab);
                }
            }

            // Sohranyaem izmeneniya
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Logiruem rezultat
            Debug.Log($"[ItemShaderSetupUtility] Processed: {processedCount}, Modified: {modifiedCount}");

            if (modifiedCount > 0)
            {
                for (int i = 0; i < modifiedFiles.Count; i++)
                {
                    Debug.Log($"  Modified: {modifiedFiles[i]}");
                }
            }

            EditorUtility.DisplayDialog(
                "Setup Complete",
                $"Processed: {processedCount} item prefabs\n" +
                $"Modified: {modifiedCount} prefabs\n\n" +
                "See Console for details.",
                "OK");
        }

        [MenuItem("Tools/Hecton/Setup Item Shaders (Selected)", false, 101)]
        public static void SetupSelectedItems()
        {
            Shader highlightShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (highlightShader == null)
                highlightShader = Shader.Find(HighlightShaderName);

            if (highlightShader == null)
            {
                Debug.LogError("[ItemShaderSetupUtility] Shader not found: " + ShaderPath);
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[ItemShaderSetupUtility] No objects selected.");
                return;
            }

            int modifiedCount = 0;

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject obj = selectedObjects[i];
                string path = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(path)) continue;

                bool wasModified = false;

                // ── 1. Primenyaem sheyder k materialam ──
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null) continue;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null) continue;

                    for (int m = 0; m < materials.Length; m++)
                    {
                        Material mat = materials[m];
                        if (mat == null) continue;

                        if (mat.shader == highlightShader) continue;

                        mat.shader = highlightShader;
                        wasModified = true;
                    }
                }

                // ── 2. Dobavlyaem ItemHighlight komponent ──
                ItemHighlight highlight = obj.GetComponent<ItemHighlight>();
                if (highlight == null)
                {
                    highlight = obj.AddComponent<ItemHighlight>();
                    wasModified = true;
                }

                if (wasModified)
                {
                    modifiedCount++;
                    EditorUtility.SetDirty(obj);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemShaderSetupUtility] Modified {modifiedCount} selected prefabs.");
        }

        [MenuItem("Tools/Hecton/Validate Item Shaders", false, 102)]
        public static void ValidateItemShaders()
        {
            Shader highlightShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (highlightShader == null)
                highlightShader = Shader.Find(HighlightShaderName);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            int itemCount = 0;
            int validCount = 0;
            int missingShaderCount = 0;
            int missingComponentCount = 0;
            List<string> issues = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                ItemData itemData = prefab.GetComponent<ItemData>();
                if (itemData == null)
                    itemData = prefab.GetComponentInChildren<ItemData>();

                if (itemData == null) continue;

                itemCount++;

                bool hasCorrectShader = true;
                bool hasHighlightComponent = prefab.GetComponent<ItemHighlight>() != null;

                // Proveryaem materialy
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length && hasCorrectShader; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null) continue;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null) continue;

                    for (int m = 0; m < materials.Length; m++)
                    {
                        Material mat = materials[m];
                        if (mat == null) continue;

                        if (mat.shader != highlightShader)
                        {
                            hasCorrectShader = false;
                            break;
                        }
                    }
                }

                if (hasCorrectShader && hasHighlightComponent)
                {
                    validCount++;
                }
                else
                {
                    if (!hasCorrectShader)
                    {
                        missingShaderCount++;
                        issues.Add($"[Missing Shader] {path}");
                    }
                    if (!hasHighlightComponent)
                    {
                        missingComponentCount++;
                        issues.Add($"[Missing Component] {path}");
                    }
                }
            }

            Debug.Log($"[ItemShaderSetupUtility] Validation Results:\n" +
                      $"  Total Items: {itemCount}\n" +
                      $"  Valid: {validCount}\n" +
                      $"  Missing Shader: {missingShaderCount}\n" +
                      $"  Missing Component: {missingComponentCount}");

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.Log($"  {issues[i]}");
            }

            EditorUtility.DisplayDialog(
                "Validation Complete",
                $"Total Items: {itemCount}\n" +
                $"Valid: {validCount}\n" +
                $"Missing Shader: {missingShaderCount}\n" +
                $"Missing Component: {missingComponentCount}\n\n" +
                "See Console for details.",
                "OK");
        }
    }
}

#endif
