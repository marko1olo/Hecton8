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

        [Test]
        public void DistanceMath_PushShaderMathLod_QueuesShaderStateUntilVisualSync()
        {
            string distanceMath = File.ReadAllText(DistanceMathPath());
            string dispatcher = File.ReadAllText(SystemDispatcherPath());

            string flush = ExtractMethodBody(distanceMath, "FlushVisualSyncShaderState");
            string nonFlushSource = distanceMath.Replace(flush, string.Empty);

            Assert.AreEqual(0, Count(nonFlushSource, @"\bShader\.(?:SetGlobal|EnableKeyword|DisableKeyword)"), "DistanceMath shader write outside visual sync flush");
            Assert.Greater(Count(flush, @"\bShader\.SetGlobal"), 0);
            Assert.Greater(Count(flush, @"\bShader\.(?:EnableKeyword|DisableKeyword)"), 0);
            Assert.That(dispatcher, Does.Contain("DistanceMath.FlushVisualSyncShaderState();"));
        }

        [Test]
        public void ConnectionSplineBatchRenderer_LogisticsHighlight_QueuesShaderStateUntilVisualSync()
        {
            string renderer = File.ReadAllText(ConnectionSplineBatchRendererPath());
            string dispatcher = File.ReadAllText(SystemDispatcherPath());

            AssertNoShaderPresentationWrite(renderer, "SetLogisticsPathHighlightActive");

            string flush = ExtractMethodBody(renderer, "FlushVisualSyncShaderState");
            Assert.Greater(Count(flush, @"\bShader\.SetGlobal"), 0);
            Assert.That(dispatcher, Does.Contain("ConnectionSplineBatchRenderer.FlushVisualSyncShaderState();"));
        }

        [Test]
        public void SpectrumSystem_PublicSonarPresentation_QueuesShaderStateUntilLateFrame()
        {
            string spectrum = File.ReadAllText(SpectrumSystemPath());

            AssertNoShaderPresentationWrite(spectrum, "ApplyShaderMode");
            AssertNoShaderPresentationWrite(spectrum, "EmitSonarPulse");
            AssertNoShaderPresentationWrite(spectrum, "PublishSonarReveal");
            AssertNoShaderPresentationWrite(spectrum, "PublishScreenSpaceSonarPulse");
            AssertNoShaderPresentationWrite(spectrum, "HandleAcousticEchoReturned");
            AssertNoShaderPresentationWrite(spectrum, "HandlePingReturnSignal");
            AssertNoShaderPresentationWrite(spectrum, "ClearSonarSnapshot");
            AssertNoShaderPresentationWrite(spectrum, "PublishPassiveRadarShaderState");
            AssertNoAudioPresentationWrite(spectrum, "TryPlayAbyssalAnchorReturn");

            string lateFrame = ExtractMethodBody(spectrum, "LateFrameTick");
            string flush = ExtractMethodBody(spectrum, "FlushQueuedSpectrumShaderGlobals");
            Assert.That(lateFrame, Does.Contain("FlushQueuedSpectrumShaderGlobals();"));
            Assert.That(lateFrame, Does.Contain("FlushQueuedSpectrumAudio();"));
            Assert.Greater(Count(flush, @"\bShader\.SetGlobal"), 0);
        }

        [Test]
        public void FeedbackPresentationRoutes_QueueUntilLateFrame()
        {
            string interaction = File.ReadAllText(PlayerInteractionPath());
            AssertNoFeedbackPresentationWrite(interaction, "SetHover");
            AssertNoFeedbackPresentationWrite(interaction, "ExecuteInteraction");
            AssertNoFeedbackPresentationWrite(interaction, "QueueStaticAudio");
            Assert.That(ExtractMethodBody(interaction, "LateFrameTick"), Does.Contain("FlushQueuedStaticAudio();"));

            string terminal = File.ReadAllText(MessageTerminalPath());
            AssertNoFeedbackPresentationWrite(terminal, "Interact");
            AssertNoFeedbackPresentationWrite(terminal, "AddMessage");
            AssertNoFeedbackPresentationWrite(terminal, "QueueStaticAudio");
            Assert.That(ExtractMethodBody(terminal, "LateFrameTick"), Does.Contain("FlushQueuedStaticAudio();"));

            string health = File.ReadAllText(HectonPlayerHealthPath());
            AssertNoFeedbackPresentationWrite(health, "PlaySurvivalGraceHeartbeatPulse");
            AssertNoFeedbackPresentationWrite(health, "TryIssueLeviathanTraumaAdvisory");
            Assert.That(ExtractMethodBody(health, "LateFrameTick"), Does.Contain("FlushQueuedPresentationFeedback();"));

            string kinematics = File.ReadAllText(PlayerKinematicsRuntimePath());
            AssertNoFeedbackPresentationWrite(kinematics, "PublishMovementAcoustics");
            AssertNoFeedbackPresentationWrite(kinematics, "TryPublishSdfSqueezeFeedback");
            AssertNoFeedbackPresentationWrite(kinematics, "EmitBraceHaptic");
            AssertNoFeedbackPresentationWrite(kinematics, "TryEmitGloveScrape");
            Assert.That(ExtractMethodBody(kinematics, "LateFrameTick"), Does.Contain("FlushQueuedFeedbackSignals();"));

            string trauma = File.ReadAllText(TraumaDispatcherPath());
            AssertNoFeedbackPresentationWrite(trauma, "UpdateActiveParasiteAudioState");
            AssertNoFeedbackPresentationWrite(trauma, "PublishParasiteAudioLoad");
            Assert.That(ExtractMethodBody(trauma, "LateFrameTick"), Does.Contain("FlushParasiteAudioLoad();"));

            string thermal = File.ReadAllText(AbyssalThermalManagerPath());
            AssertNoFeedbackPresentationWrite(thermal, "EmitThermalShock");
            AssertNoFeedbackPresentationWrite(thermal, "TryQueueThermalRoar");
            Assert.That(ExtractMethodBody(thermal, "LateFrameTick"), Does.Contain("FlushThermalFeedbackSignals();"));
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

        private static string DistanceMathPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "DistanceMath.cs");
        }

        private static string SystemDispatcherPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "SystemDispatcher.cs");
        }

        private static string ConnectionSplineBatchRendererPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "ConnectionSplineBatchRenderer.cs");
        }

        private static string SpectrumSystemPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Visor", "SpectrumSystem.cs");
        }

        private static string PlayerInteractionPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Interaction", "PlayerInteraction.cs");
        }

        private static string MessageTerminalPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "MessageTerminal.cs");
        }

        private static string HectonPlayerHealthPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "HectonPlayerHealth.cs");
        }

        private static string PlayerKinematicsRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "PlayerKinematicsRuntime.cs");
        }

        private static string TraumaDispatcherPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "TraumaDispatcher.cs");
        }

        private static string AbyssalThermalManagerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "AbyssalThermalManager.cs");
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

        private static void AssertNoAudioPresentationWrite(string text, string methodName)
        {
            string body = ExtractMethodBody(text, methodName);
            Assert.AreEqual(0, Count(body, @"\bPlayStatic2D\s*\(|\bAudioSource\.Play\s*\(|\.PlayOneShot\s*\("), methodName + " audio presentation write");
        }

        private static void AssertNoFeedbackPresentationWrite(string text, string methodName)
        {
            string body = ExtractMethodBody(text, methodName);
            Assert.AreEqual(
                0,
                Count(body, @"\bPlayStatic2D\s*\(|\bTryRaiseAudioPingTriggered\s*\(|\bSetParasiteRoomAcousticLoad\s*\(|\bSignalBus<(?:MovementAcousticSignal|HapticRequest|AcousticPingSignal)>\.TryPushTracked"),
                methodName + " feedback presentation write");
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
