using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class HectonBuildDaemon
    {
        private const double BeeLockTimeoutSeconds = 30.0;
        private const double BeePollIntervalSeconds = 2.0;
        private const double KillCooldownSeconds = 10.0;
        private const double BeeCpuProgressEpsilonSeconds = 0.05;

        private static bool _reloadOrCompileObserved;
        private static bool _killAttemptedThisCycle;
        private static double _observationStartTime;
        private static double _nextPollTime;
        private static double _lastKillTime = -KillCooldownSeconds;
        private static double _lastBeeCpuSeconds = -1.0;
        private static double _lastBeeProgressTime;

        static HectonBuildDaemon()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= HandleAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
            CompilationPipeline.compilationStarted -= HandleCompilationStarted;
            CompilationPipeline.compilationStarted += HandleCompilationStarted;
            CompilationPipeline.compilationFinished -= HandleCompilationFinished;
            CompilationPipeline.compilationFinished += HandleCompilationFinished;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void HandleBeforeAssemblyReload()
        {
            BeginObservation();
        }

        private static void HandleAfterAssemblyReload()
        {
            EndObservation();
        }

        private static void HandleCompilationStarted(object context)
        {
            BeginObservation();
        }

        private static void HandleCompilationFinished(object context)
        {
            EndObservation();
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPollTime)
                return;

            _nextPollTime = now + BeePollIntervalSeconds;
            bool editorBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;
            bool beePresent = TryGetBeeBackendCpuSeconds(out double beeCpuSeconds);
            if (!editorBusy && !beePresent && !_reloadOrCompileObserved)
                return;

            if (!_reloadOrCompileObserved)
                BeginObservation();

            if (editorBusy)
            {
                if (beePresent)
                    UpdateBeeProgress(now, beeCpuSeconds);

                return;
            }

            if (beePresent)
            {
                UpdateBeeProgress(now, beeCpuSeconds);

                if (_killAttemptedThisCycle ||
                    now - _lastBeeProgressTime < BeeLockTimeoutSeconds ||
                    now - _lastKillTime < KillCooldownSeconds)
                    return;

                int killed = KillBeeBackends();
                _killAttemptedThisCycle = true;
                _lastKillTime = now;
                if (killed > 0)
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                return;
            }

            EndObservation();
        }

        private static void BeginObservation()
        {
            if (_reloadOrCompileObserved)
                return;

            _reloadOrCompileObserved = true;
            _killAttemptedThisCycle = false;
            _observationStartTime = EditorApplication.timeSinceStartup;
            _lastBeeCpuSeconds = -1.0;
            _lastBeeProgressTime = _observationStartTime;
        }

        private static void EndObservation()
        {
            _reloadOrCompileObserved = false;
            _killAttemptedThisCycle = false;
            _observationStartTime = 0d;
            _lastBeeCpuSeconds = -1.0;
            _lastBeeProgressTime = 0d;
        }

        private static void UpdateBeeProgress(double now, double beeCpuSeconds)
        {
            if (_lastBeeCpuSeconds < 0.0 ||
                beeCpuSeconds - _lastBeeCpuSeconds > BeeCpuProgressEpsilonSeconds)
            {
                _lastBeeCpuSeconds = beeCpuSeconds;
                _lastBeeProgressTime = now;
            }
        }

        private static bool TryGetBeeBackendCpuSeconds(out double cpuSeconds)
        {
            cpuSeconds = 0.0;
            try
            {
                Process[] processes = Process.GetProcessesByName("bee_backend");
                bool found = false;
                for (int i = 0; i < processes.Length; i++)
                {
                    using (Process process = processes[i])
                    {
                        if (process.HasExited)
                            continue;

                        found = true;
                        cpuSeconds += process.TotalProcessorTime.TotalSeconds;
                    }
                }

                return found;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[HectonBuildDaemon] bee_backend probe failed: " + exception.Message);
                return false;
            }
        }

        private static int KillBeeBackends()
        {
            int killed = 0;
            try
            {
                Process[] processes = Process.GetProcessesByName("bee_backend");
                for (int i = 0; i < processes.Length; i++)
                {
                    using (Process process = processes[i])
                    {
                        if (process.HasExited)
                            continue;

                        process.Kill();
                        process.WaitForExit(2000);
                        killed++;
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[HectonBuildDaemon] bee_backend kill failed: " + exception.Message);
            }

            return killed;
        }
    }
}
