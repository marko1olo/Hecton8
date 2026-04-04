// ============================================================================
// HECTON-8 - EditorInspectorRecoveryGuard.cs
// Repairs broken Inspector selection state after assembly/domain reload.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Clears stale editor selection targets that can leave the Inspector bound to null objects after reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorInspectorRecoveryGuard
    {
        private const int MaxRecoveryPasses = 8;

        private static int _remainingRecoveryPasses;

        static EditorInspectorRecoveryGuard()
        {
            RegisterCallbacks();
            RequestRecovery();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            EditorApplication.update -= TryRecoverInspectorState;
            AssemblyReloadEvents.afterAssemblyReload -= RequestRecovery;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            _remainingRecoveryPasses = 0;
            RegisterCallbacks();
            RequestRecovery();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                RequestRecovery();
        }

        private static void RegisterCallbacks()
        {
            AssemblyReloadEvents.afterAssemblyReload -= RequestRecovery;
            AssemblyReloadEvents.afterAssemblyReload += RequestRecovery;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void RequestRecovery()
        {
            EditorApplication.update -= TryRecoverInspectorState;

            if (InternalEditorUtility.inBatchMode)
                return;

            _remainingRecoveryPasses = MaxRecoveryPasses;
            EditorApplication.update += TryRecoverInspectorState;
        }

        private static void TryRecoverInspectorState()
        {
            if (_remainingRecoveryPasses <= 0)
            {
                EditorApplication.update -= TryRecoverInspectorState;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            _remainingRecoveryPasses--;

            bool hasNullSelection = HasNullSelectionObject();
            bool hasNullInspectorTarget = HasNullInspectorTarget();

            if (!hasNullSelection && !hasNullInspectorTarget)
            {
                EditorApplication.update -= TryRecoverInspectorState;
                return;
            }

            RepairSelectionState();

            if (!HasNullSelectionObject() && !HasNullInspectorTarget())
            {
                EditorApplication.update -= TryRecoverInspectorState;
            }
        }

        private static bool HasNullSelectionObject()
        {
            Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] == null)
                    return true;
            }

            return false;
        }

        private static bool HasNullInspectorTarget()
        {
            UnityEditor.Editor[] editors = ActiveEditorTracker.sharedTracker.activeEditors;
            for (int i = 0; i < editors.Length; i++)
            {
                UnityEditor.Editor editor = editors[i];
                if (editor == null)
                    return true;

                Object[] targets = editor.targets;
                for (int j = 0; j < targets.Length; j++)
                {
                    if (targets[j] == null)
                        return true;
                }
            }

            return false;
        }

        private static void RepairSelectionState()
        {
            Object[] selection = Selection.objects;
            int validCount = 0;

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null)
                    validCount++;
            }

            if (validCount != selection.Length)
            {
                if (validCount == 0)
                {
                    Selection.activeObject = null;
                }
                else
                {
                    Object[] filteredSelection = new Object[validCount];
                    int writeIndex = 0;
                    for (int i = 0; i < selection.Length; i++)
                    {
                        if (selection[i] == null)
                            continue;

                        filteredSelection[writeIndex] = selection[i];
                        writeIndex++;
                    }

                    Selection.objects = filteredSelection;
                }
            }

            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
#endif
