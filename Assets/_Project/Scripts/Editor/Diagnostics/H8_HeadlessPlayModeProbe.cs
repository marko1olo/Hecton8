using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hecton8.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Enters Play Mode from batchmode, lets the game boot, reports what the runtime actually
    /// contains, and exits.
    ///
    /// This exists because nothing in the project could prove runtime behaviour. Every static
    /// probe here answers "is it wired", never "does it run". The PlayMode test assembly is
    /// disabled by a NEVER_COMPILE_TESTS define constraint, so no test can execute without
    /// changing project-wide settings, and a headless machine has no other way in. The result was
    /// a steady stream of changes verified by "0 CS errors" and nothing else.
    ///
    /// Usage - note there must be NO -quit, or the editor closes before Play Mode ever starts:
    ///   Unity.exe -batchmode -nographics -projectPath . -logFile Logs/playprobe.log \
    ///             -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \
    ///             -h8Scene Assets/_Project/Scenes/02_HECTON_WORLD.unity -h8WarmupFrames 240
    ///
    /// The probe calls EditorApplication.Exit itself: 0 if every check passed, 1 otherwise.
    ///
    /// Relies on the project having Enter Play Mode Options set to DisableDomainReload
    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), which is why a plain
    /// static state machine survives the transition. Should that ever change, the statics reset
    /// mid-run and the probe stalls rather than lying - always run it under an external timeout.
    /// </summary>
    public static class H8_HeadlessPlayModeProbe
    {
        private const string Marker = "[H8_PLAYPROBE]";
        private const string DefaultScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const int DefaultWarmupFrames = 240;
        private const double HardTimeoutSeconds = 240.0;

        private enum Phase
        {
            Idle,
            WaitingForPlayMode,
            WarmingUp,
            Reporting,
            LeavingPlayMode,
        }

        private static Phase _phase = Phase.Idle;
        private static double _startedAt;
        private static int _frames;
        private static int _warmupFrames = DefaultWarmupFrames;
        private static int _failures;

        public static void Run()
        {
            string scenePath = ReadStringArg("-h8Scene", DefaultScene);
            _warmupFrames = Math.Max(1, ReadIntArg("-h8WarmupFrames", DefaultWarmupFrames));

            Debug.Log($"{Marker} START scene={scenePath} warmupFrames={_warmupFrames} batchmode={Application.isBatchMode}");

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} FAILED to open {scenePath}: {ex.GetType().Name}: {ex.Message}");
                Finish(1);
                return;
            }

            _phase = Phase.WaitingForPlayMode;
            _startedAt = EditorApplication.timeSinceStartup;
            _frames = 0;
            _failures = 0;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _startedAt > HardTimeoutSeconds)
            {
                Debug.Log($"{Marker} TIMEOUT after {HardTimeoutSeconds}s in phase={_phase} frames={_frames}");
                Finish(1);
                return;
            }

            switch (_phase)
            {
                case Phase.WaitingForPlayMode:
                    if (EditorApplication.isPlaying)
                    {
                        Debug.Log($"{Marker} PLAYING (entered after {EditorApplication.timeSinceStartup - _startedAt:F1}s)");
                        _phase = Phase.WarmingUp;
                    }
                    break;

                case Phase.WarmingUp:
                    // Leaving play mode unexpectedly means the game aborted its own boot. That is a
                    // result, not a probe failure to hide.
                    if (!EditorApplication.isPlaying)
                    {
                        Debug.Log($"{Marker} LEFT PLAY MODE early at frame {_frames} - boot aborted or a script called Exit");
                        Finish(1);
                        return;
                    }

                    if (++_frames >= _warmupFrames)
                        _phase = Phase.Reporting;
                    break;

                case Phase.Reporting:
                    RunChecks();
                    _phase = Phase.LeavingPlayMode;
                    EditorApplication.ExitPlaymode();
                    break;

                case Phase.LeavingPlayMode:
                    if (!EditorApplication.isPlaying)
                        Finish(_failures == 0 ? 0 : 1);
                    break;
            }
        }

        private static void RunChecks()
        {
            Debug.Log($"{Marker} --- checks at frame {_frames} ---");

            try
            {
                CheckWorldSeed();
                ReportRegistryPresence();
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} CHECK THREW {ex.GetType().Name}: {ex.Message}");
                _failures++;
            }

            Debug.Log($"{Marker} RESULT failures={_failures}");
        }

        /// <summary>
        /// The world seed reached every procedural generator as 0 because nothing implemented
        /// IWorldSeedProvider. This is the check that says whether that is still true.
        /// </summary>
        private static void CheckWorldSeed()
        {
            IWorldSeedProvider provider = GlobalRegistry.WorldSeedProvider;
            if (provider == null)
            {
                Debug.Log($"{Marker} WORLDSEED provider=NULL - every procedural generator is running on seed 0");
                _failures++;
                return;
            }

            Debug.Log(
                $"{Marker} WORLDSEED provider={provider.GetType().Name} initialized={provider.IsInitialized} " +
                $"seed={provider.RuntimeWorldSeed} versionId={provider.RuntimeWorldGenerationVersionId}");

            if (!provider.IsInitialized)
            {
                Debug.Log($"{Marker} WORLDSEED provider present but NOT initialized - consumers still take the 0 path");
                _failures++;
                return;
            }

            // The seven MapMagic / procedural consumers go through this static, not the registry.
            bool resolved = global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int staticSeed);
            Debug.Log($"{Marker} WORLDSEED static TryGetActiveRuntimeWorldSeed -> {resolved} seed={staticSeed}");

            if (!resolved || staticSeed == 0)
            {
                Debug.Log($"{Marker} WORLDSEED static path still returns nothing - the MapMagic nodes remain on 0");
                _failures++;
                return;
            }

            if (staticSeed != provider.RuntimeWorldSeed)
            {
                Debug.Log($"{Marker} WORLDSEED MISMATCH static={staticSeed} registry={provider.RuntimeWorldSeed}");
                _failures++;
            }
        }

        /// <summary>
        /// Reports presence only, and fails nothing. A missing service here is information about
        /// what this scene boots, not a defect on its own.
        /// </summary>
        private static void ReportRegistryPresence()
        {
            var slots = new List<KeyValuePair<string, object>>
            {
                new("Terrain", GlobalRegistry.Terrain),
                new("FaunaGenetics", GlobalRegistry.FaunaGenetics),
                new("Player", GlobalRegistry.Player),
                new("Save", GlobalRegistry.Save),
                new("TickDispatcher", GlobalRegistry.TickDispatcher),
                new("VoxelEngine", GlobalRegistry.VoxelEngine),
            };

            var builder = new StringBuilder();
            foreach (KeyValuePair<string, object> slot in slots)
            {
                if (builder.Length > 0)
                    builder.Append("  ");

                builder.Append(slot.Key).Append('=');
                builder.Append(IsAlive(slot.Value) ? slot.Value.GetType().Name : "null");
            }

            Debug.Log($"{Marker} REGISTRY {builder}");
        }

        private static bool IsAlive(object service)
        {
            if (service is UnityEngine.Object unityObject)
                return unityObject != null;

            return service != null;
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            _phase = Phase.Idle;

            Debug.Log($"{Marker} DONE exitCode={exitCode}");

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static string ReadStringArg(string flag, string fallback)
        {
            // Fully qualified: the project has its own Hecton8.Environment namespace, which wins
            // over System here and resolves to a type that has no GetCommandLineArgs.
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int ReadIntArg(string flag, int fallback)
        {
            string raw = ReadStringArg(flag, null);
            return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }
}
