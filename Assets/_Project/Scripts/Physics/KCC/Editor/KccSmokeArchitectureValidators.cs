#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.KCC.Editor
{
    [InitializeOnLoad]
    internal static class KccSmokeLayoutGuard
    {
        static KccSmokeLayoutGuard()
        {
            HeadlessKccLayoutAssertions.AssertAll();
            int stateSize = UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>();
            int stateAlign = UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>();
            int aupOffset = Marshal.OffsetOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(
                nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.TestPlayerAUP)).ToInt32();
            int frameOffset = Marshal.OffsetOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(
                nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.CurrentFrameCount)).ToInt32();
            int flagOffset = Marshal.OffsetOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(
                nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.MismatchFlags)).ToInt32();

            if (stateSize != 32 || stateAlign != 8 || aupOffset != 0 || frameOffset != 24 || flagOffset != 28)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_355 KccSmokeTestStateDTO layout violation. Expected Size=32 Align=8 Offsets=0/24/28.");
            }
        }
    }

    public static class OOP_Test_Scanner
    {
        [MenuItem("HECTON-8/Kinematics/Run OOP Test Scanner")]
        public static void RunMenu()
        {
            ScanAndWriteReport();
        }

        public static void ScanAndWriteReport()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string testsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Tests");
            int scannedFiles = 0;
            int physicsSimulateHits = 0;
            int gameObjectCreateHits = 0;
            int instantiateHits = 0;
            int listVector3Hits = 0;
            int editorWindowHits = 0;
            int sceneViewHits = 0;
            int uiElementsHits = 0;
            int skippedNonKccFiles = 0;

            if (Directory.Exists(testsRoot))
            {
                string[] files = Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                    ScanFile(files[i], ref scannedFiles, ref skippedNonKccFiles, ref physicsSimulateHits, ref gameObjectCreateHits, ref instantiateHits, ref listVector3Hits, ref editorWindowHits, ref sceneViewHits, ref uiElementsHits);
            }

            string reportsDir = Path.Combine(projectRoot, "Docs", "Reports");
            Directory.CreateDirectory(reportsDir);
            string path = Path.Combine(reportsDir, "QA_OPTIMIZATION_OOP_REPORT.json");
            StringBuilder builder = new StringBuilder(256);
            builder.Append("{\"oopTestsEradicated\":");
            builder.Append((physicsSimulateHits | instantiateHits | gameObjectCreateHits | listVector3Hits | editorWindowHits | sceneViewHits | uiElementsHits) == 0 ? "true" : "false");
            builder.Append(",\"scanner\":\"OOP_Test_Scanner\"");
            builder.Append(",\"scope\":\"KCC_KINEMATIC_TEST_FILES_ONLY\"");
            builder.Append(",\"scanned_roots\":[\"Assets/_Project/Tests scoped by Kcc/Kinematic source tokens\"]");
            builder.Append(",\"scanned_files\":");
            builder.Append(scannedFiles);
            builder.Append(",\"skipped_non_kcc_kinematic_files\":");
            builder.Append(skippedNonKccFiles);
            builder.Append(",\"physics_simulate_hits\":");
            builder.Append(physicsSimulateHits);
            builder.Append(",\"gameobject_create_hits\":");
            builder.Append(gameObjectCreateHits);
            builder.Append(",\"instantiate_hits\":");
            builder.Append(instantiateHits);
            builder.Append(",\"list_vector3_hits\":");
            builder.Append(listVector3Hits);
            builder.Append(",\"editor_window_hits\":");
            builder.Append(editorWindowHits);
            builder.Append(",\"scene_view_hits\":");
            builder.Append(sceneViewHits);
            builder.Append(",\"ui_elements_hits\":");
            builder.Append(uiElementsHits);
            builder.Append("}\n");
            File.WriteAllText(path, builder.ToString());
        }

        private static void ScanFile(
            string path,
            ref int scannedFiles,
            ref int skippedNonKccFiles,
            ref int physicsSimulateHits,
            ref int gameObjectCreateHits,
            ref int instantiateHits,
            ref int listVector3Hits,
            ref int editorWindowHits,
            ref int sceneViewHits,
            ref int uiElementsHits)
        {
            string source = File.ReadAllText(path);
            if (source.IndexOf("Kcc", StringComparison.OrdinalIgnoreCase) < 0 &&
                source.IndexOf("Kinematic", StringComparison.OrdinalIgnoreCase) < 0)
            {
                skippedNonKccFiles++;
                return;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            SyntaxNode root = tree.GetRoot();
            scannedFiles++;

            System.Collections.Generic.IEnumerator<SyntaxNode> enumerator = root.DescendantNodes().GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    SyntaxNode node = enumerator.Current;
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        string expression = invocation.Expression.ToString();
                        string physicsSimulate = "Physics" + ".Simulate";
                        if (expression == physicsSimulate || expression.EndsWith("." + physicsSimulate, StringComparison.Ordinal))
                            physicsSimulateHits++;
                        string instantiate = "Instantiate";
                        string gameObjectInstantiate = "GameObject" + "." + instantiate;
                        if (expression == instantiate || expression == gameObjectInstantiate || expression.EndsWith("." + instantiate, StringComparison.Ordinal))
                            instantiateHits++;
                    }
                    else if (node is ObjectCreationExpressionSyntax creation)
                    {
                        string typeName = creation.Type.ToString();
                        if (typeName == "GameObject" || typeName.EndsWith(".GameObject", StringComparison.Ordinal))
                            gameObjectCreateHits++;
                    }
                    else if (node is GenericNameSyntax generic)
                    {
                        string identifier = generic.Identifier.ValueText;
                        string typeArguments = generic.TypeArgumentList.ToString();
                        if (identifier == "List" && typeArguments.IndexOf("Vector3", StringComparison.Ordinal) >= 0)
                            listVector3Hits++;
                    }
                    else if (node is IdentifierNameSyntax identifierName)
                    {
                        string identifier = identifierName.Identifier.ValueText;
                        if (identifier == "EditorWindow")
                            editorWindowHits++;
                        else if (identifier == "SceneView")
                            sceneViewHits++;
                        else if (identifier == "UIElements" || identifier == "VisualElement" || identifier == "Button" || identifier == "ProgressBar")
                            uiElementsHits++;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }
    }
}
#endif
