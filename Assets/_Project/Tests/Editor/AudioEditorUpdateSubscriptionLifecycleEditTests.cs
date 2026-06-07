using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AudioEditorUpdateSubscriptionLifecycleEditTests
    {
        [TestCase("Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs", "OnEditorUpdate")]
        [TestCase("Assets/_Project/Scripts/Audio/Editor/AbyssalAcousticsTunerWindow.cs", "OnEditorUpdate")]
        [TestCase("Assets/_Project/Scripts/Audio/Editor/VocalWarningQueueTunerWindow.cs", "OnEditorHeartbeat")]
        [TestCase("Assets/_Project/Scripts/Audio/Editor/SabineReverbDspTunerWindow.cs", "OnEditorUpdate")]
        [TestCase("Assets/_Project/Scripts/Audio/Editor/GranularSynthTunerWindow.cs", "OnEditorUpdate")]
        [TestCase("Assets/_Project/Scripts/Audio/Synthesis/Editor/AbyssalSynthTunerWindow.cs", "OnEditorUpdate")]
        public void AudioEditorWindows_DefensivelyDeduplicateEditorUpdateSubscription(string relativePath, string callback)
        {
            string source = ReadProjectFile(relativePath);
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string remove = "EditorApplication.update -= " + callback + ";";
            string add = "EditorApplication.update += " + callback + ";";

            StringAssert.Contains(remove, onEnable);
            StringAssert.Contains(add, onEnable);
            AssertOrder(onEnable, remove, add);
        }

        [TestCase("Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs")]
        [TestCase("Assets/_Project/Scripts/Audio/Editor/SabineReverbDspTunerWindow.cs")]
        public void AudioEditorSceneGuiWindows_DefensivelyDeduplicateSceneGuiSubscription(string relativePath)
        {
            string source = ReadProjectFile(relativePath);
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");

            StringAssert.Contains("SceneView.duringSceneGui -= OnSceneGui;", onEnable);
            StringAssert.Contains("SceneView.duringSceneGui += OnSceneGui;", onEnable);
            AssertOrder(onEnable, "SceneView.duringSceneGui -= OnSceneGui;", "SceneView.duringSceneGui += OnSceneGui;");
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertOrder(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, "Missing first token: " + first);
            Assert.GreaterOrEqual(secondIndex, 0, "Missing second token: " + second);
            Assert.Less(firstIndex, secondIndex, first + " must appear before " + second);
        }
    }
}
