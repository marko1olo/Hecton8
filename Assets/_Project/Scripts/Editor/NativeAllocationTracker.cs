using System;
using Hecton8.Core;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Escalates Unity's built-in TempJob lifetime warnings into hard editor errors with stack-trace leak detection enabled.
    /// </summary>
    [InitializeOnLoad]
    internal static class NativeAllocationTracker
    {
        private const string ErrorPrefix = "[NativeAllocationTracker]";
        private const string TempJobAllocationToken = "JobTempAlloc";
        private const string FourFrameLifetimeToken = "more than 4 frames old";

        private static bool _pendingEscalation;
        private static string _pendingCondition;
        private static string _pendingStackTrace;

        static NativeAllocationTracker()
        {
            if (Application.isBatchMode)
                return;

            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
            Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
            EditorApplication.update -= FlushPendingEscalation;
            EditorApplication.update += FlushPendingEscalation;
        }

        private static void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            if (string.IsNullOrEmpty(condition) ||
                condition.Contains(ErrorPrefix, StringComparison.Ordinal) ||
                !condition.Contains(TempJobAllocationToken, StringComparison.Ordinal) ||
                !condition.Contains(FourFrameLifetimeToken, StringComparison.Ordinal))
            {
                return;
            }

            _pendingCondition = condition;
            _pendingStackTrace = stackTrace;
            _pendingEscalation = true;
        }

        private static void FlushPendingEscalation()
        {
            if (!_pendingEscalation)
                return;

            _pendingEscalation = false;
            string condition = _pendingCondition;
            string stackTrace = _pendingStackTrace;
            _pendingCondition = null;
            _pendingStackTrace = null;

            NativeAllocationTrackerRuntimeBridge.ReportLeak($"{ErrorPrefix} TempJob leak detected. Agent shame active.\n{condition}");
            H8Debug.LogError($"{ErrorPrefix} TempJob allocation exceeded 4-frame lifetime.\n{condition}\n{stackTrace}");
            EditorApplication.isPaused = true;
        }
    }
}
