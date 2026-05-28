using System;
using System.IO;
using System.Text.RegularExpressions;
using Hecton8.Core;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DispatcherPhaseAlignment1410EditTests
    {
        [Test]
        public void MockDispatcherOrder_TickWritesBeforeLateFrameReads_UnmanagedBuffer()
        {
            NativeArray<int> buffer = new NativeArray<int>(1, Allocator.Temp);
            try
            {
                MockSimulationSystem simulation = new MockSimulationSystem(buffer);
                MockLateFrameSystem presentation = new MockLateFrameSystem(buffer);

                simulation.Tick(1f / 60f);
                presentation.LateFrameTick();

                Assert.AreEqual(1410, presentation.ObservedValue);
            }
            finally
            {
                if (buffer.IsCreated)
                    buffer.Dispose();
            }
        }

        [Test]
        public void RuntimeScripts_DoNotDeclareRogueUnityMagicMethods()
        {
            string[] files = Directory.GetFiles(RuntimeScriptsRoot(), "*.cs", SearchOption.AllDirectories);
            Regex magic = new Regex(@"^\s*(?:public|private|protected|internal)?\s*(?:async\s+)?void\s+(?:Update|LateUpdate|FixedUpdate)\s*\(", RegexOptions.Multiline);
            int violations = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string path = NormalizePath(files[i]);
                if (IsEditorPath(path))
                    continue;

                string text = File.ReadAllText(files[i]);
                if (magic.IsMatch(text))
                    violations++;
            }

            Assert.AreEqual(0, violations);
        }

        [Test]
        public void HectonVoxelStreamingBridge_HotPresentationPaths_AreZeroGcByTokenScan()
        {
            string text = File.ReadAllText(VoxelBridgePath());
            string[] methods =
            {
                "RegisterChunkFade",
                "LateFrameTick",
                "TickChunkFade",
                "ResolveChunkFadeQualityWeight01"
            };

            for (int i = 0; i < methods.Length; i++)
            {
                string body = ExtractMethodBody(text, methods[i]);
                Assert.AreEqual(0, Count(body, @"\bnew\s+(?:List|Queue|Action|Dictionary|HashSet|StringBuilder|object|string)\b"), methods[i] + " reference allocations");
                Assert.AreEqual(0, Count(body, @"\bstring\.Format\s*\("), methods[i] + " string.Format");
                Assert.AreEqual(0, Count(body, @"\.ToString\s*\("), methods[i] + " ToString");
                Assert.AreEqual(0, Count(body, @"\bforeach\s*\("), methods[i] + " foreach");
                Assert.AreEqual(0, Count(body, @"\bSystem\.Linq\b|\bEnumerable\."), methods[i] + " LINQ");
            }
        }

        [Test]
        public void HectonVoxelStreamingBridge_RegisterChunkFade_DoesNotFlushOverflowInline()
        {
            string text = File.ReadAllText(VoxelBridgePath());
            string body = ExtractMethodBody(text, "RegisterChunkFade");

            Assert.That(body, Does.Contain("ChunkFadePendingQueueFullWarningHash"));
            Assert.That(text, Does.Not.Contain("_pendingChunkFadeVolumes"));
            Assert.That(text, Does.Not.Contain("long[] _pendingChunkFadeKeys"));
            Assert.That(text, Does.Contain("FixedList512Bytes<long> _pendingChunkFadeKeys"));
            Assert.That(body, Does.Not.Contain("FlushPendingChunkFadeRegistrations"));
        }

        [Test]
        public void HomeostasisBrain_PreSimulationShaderGlobals_AreVisualSyncStaged()
        {
            string brain = File.ReadAllText(HomeostasisBrainPath());
            string scalability = File.ReadAllText(HomeostasisScalabilityPath());

            AssertNoShaderGlobalWrite(brain, "PreSimulationTick");
            AssertNoShaderGlobalWrite(brain, "ApplyPressurePolicy");
            AssertNoShaderGlobalWrite(scalability, "ApplyDictatorPressurePolicy");
            AssertNoShaderGlobalWrite(scalability, "WriteDictatorState");
            AssertNoShaderGlobalWrite(scalability, "RefreshMathLodLowScalar");
            AssertNoShaderGlobalWrite(scalability, "UpdateCullingMultiplier");
            AssertNoShaderGlobalWrite(scalability, "PublishQualityShaderGlobals");

            string flush = ExtractMethodBody(scalability, "FlushVisualSyncShaderState");
            Assert.Greater(Count(flush, @"\bShader\.SetGlobal"), 0);
            Assert.That(flush, Does.Contain("_pendingScalabilityShaderDirtyFlags = 0u"));
        }

        [Test]
        public void GlobalRegistry_MathPrecisionTransition_QueuesShaderStateUntilVisualSync()
        {
            string registry = File.ReadAllText(GlobalRegistryPath());

            AssertNoShaderPresentationWrite(registry, "TickMathPrecisionTransition");
            AssertNoShaderPresentationWrite(registry, "ApplyMathPrecisionShaderState");
            AssertNoShaderPresentationWrite(registry, "QueueMathPrecisionShaderState");

            string flush = ExtractMethodBody(registry, "FlushMathPrecisionShaderState");
            Assert.Greater(Count(flush, @"\bShader\.SetGlobal"), 0);
            Assert.Greater(Count(flush, @"\bShader\.(?:EnableKeyword|DisableKeyword)"), 0);
            Assert.That(flush, Does.Contain("_mathPrecisionShaderDirty"));
        }

        private static string RuntimeScriptsRoot()
        {
            return Path.Combine(ProjectRoot(), "Assets", "_Project", "Scripts");
        }

        private static string VoxelBridgePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "HectonVoxelStreamingBridge.cs");
        }

        private static string HomeostasisBrainPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "HomeostasisBrain.cs");
        }

        private static string HomeostasisScalabilityPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "HomeostasisBrain.ScalabilityDictator.cs");
        }

        private static string GlobalRegistryPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "GlobalRegistry.cs");
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static bool IsEditorPath(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static int Count(string text, string pattern)
        {
            return Regex.Matches(text, pattern).Count;
        }

        private static void AssertNoShaderGlobalWrite(string text, string methodName)
        {
            string body = ExtractMethodBody(text, methodName);
            Assert.AreEqual(0, Count(body, @"\bShader\.SetGlobal"), methodName + " shader global write");
        }

        private static void AssertNoShaderPresentationWrite(string text, string methodName)
        {
            string body = ExtractMethodBody(text, methodName);
            Assert.AreEqual(0, Count(body, @"\bShader\.(?:SetGlobal|EnableKeyword|DisableKeyword)"), methodName + " shader presentation write");
        }

        private static string ExtractMethodBody(string text, string methodName)
        {
            Regex declaration = new Regex(
                @"(?m)^\s*(?:(?:public|private|protected|internal|static|readonly|unsafe|async|virtual|override|sealed|partial|extern|new)\s+)*(?:[\w<>\[\],\.\?]+\s+)+" +
                Regex.Escape(methodName) +
                @"\s*\(",
                RegexOptions.CultureInvariant);
            Match match = declaration.Match(text);
            Assert.IsTrue(match.Success, "Missing method " + methodName);

            int open = text.IndexOf('{', match.Index);
            Assert.GreaterOrEqual(open, 0, "Missing method body " + methodName);

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return text.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Unclosed method body " + methodName);
            return string.Empty;
        }

        private sealed class MockSimulationSystem : ITickable
        {
            private NativeArray<int> _buffer;

            public MockSimulationSystem(NativeArray<int> buffer)
            {
                _buffer = buffer;
            }

            public void Tick(float deltaTime)
            {
                _buffer[0] = 1410;
            }
        }

        private sealed class MockLateFrameSystem : ILateFrameTickable
        {
            private NativeArray<int> _buffer;

            public int ObservedValue { get; private set; }

            public MockLateFrameSystem(NativeArray<int> buffer)
            {
                _buffer = buffer;
            }

            public void LateFrameTick()
            {
                ObservedValue = _buffer[0];
            }
        }
    }
}
