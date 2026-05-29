using Hecton8.Core.Contracts.Signals;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using H8DesignDataFacade = global::Hecton8.Core.Bridge.H8DesignDataFacade;
using H8InputMappingFacade = global::Hecton8.Core.Bridge.H8InputMappingFacade;

namespace Hecton8.Core.Bridge.EditorTools
{
    [CustomEditor(typeof(H8DesignDataFacade))]
    public sealed class H8DesignDataFacadeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            H8DesignDataFacade facade = (H8DesignDataFacade)target;
            EditorGUILayout.Space(6f);
            long bytes = facade.EstimateVramBytes();
            EditorGUILayout.LabelField("VRAM Cost Meter", (bytes >> 20) + " MB");
            EditorGUILayout.LabelField("Validation", facade.HasValidationErrors ? "ERRORS" : "OK");
            EditorGUILayout.LabelField("Runtime Bindings", facade.ValidationRuntimeBindingCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Disabled Bindings", facade.ValidationDisabledBindingCount.ToString(CultureInfo.InvariantCulture));
            if (facade.HasValidationErrors)
            {
                EditorGUILayout.LabelField("Null Binding Rows", facade.ValidationNullBindingCount + " first " + facade.ValidationFirstNullBindingIndex);
                EditorGUILayout.LabelField("Duplicate Field Hash Rows", facade.ValidationDuplicateFieldHashCount + " first " + facade.ValidationFirstDuplicateFieldHashIndex);
            }

            Rect meterRect = GUILayoutUtility.GetRect(0f, 14f, GUILayout.ExpandWidth(true));
            float normalized = Mathf.Clamp01(bytes / (1024f * 1024f * 1024f));
            EditorGUI.ProgressBar(meterRect, normalized, bytes + " bytes");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync DataVault"))
                    TrySyncDesignFacade(facade);

                if (GUILayout.Button("Generate Contracts"))
                    H8BridgeContractGenerator.GenerateAllContracts();
            }
        }

        private static void TrySyncDesignFacade(H8DesignDataFacade facade)
        {
            if (facade == null)
                return;

            bool synced = facade.SyncToVault(
                Hecton8.Core.GlobalRegistry.DataVault,
                Hecton8.Core.GlobalRegistry.MacroDatabase);
            if (!synced)
                Debug.LogError("[H8Bridge] Design DataVault sync failed. Fix duplicate field hashes or wait for DataVault allocation fences to clear.");
        }
    }

    [CustomEditor(typeof(H8InputMappingFacade))]
    public sealed class H8InputMappingFacadeEditor : UnityEditor.Editor
    {
        private static readonly string[] CommandNames =
        {
            "None",
            "ToggleInventory",
            "TogglePda",
            "Cancel",
            "TabNext",
            "TabPrevious",
            "Interact",
            "PrimaryAction",
            "SecondaryAction",
            "ToolSlot1",
            "ToolSlot2",
            "ToolSlot3",
            "ToolSlot4",
            "Flashlight"
        };

        private static readonly byte[] CommandValues =
        {
            0,
            PlayerInputSignalCommands.ToggleInventory,
            PlayerInputSignalCommands.TogglePda,
            PlayerInputSignalCommands.Cancel,
            PlayerInputSignalCommands.TabNext,
            PlayerInputSignalCommands.TabPrevious,
            PlayerInputSignalCommands.Interact,
            PlayerInputSignalCommands.PrimaryAction,
            PlayerInputSignalCommands.SecondaryAction,
            PlayerInputSignalCommands.ToolSlot1,
            PlayerInputSignalCommands.ToolSlot2,
            PlayerInputSignalCommands.ToolSlot3,
            PlayerInputSignalCommands.ToolSlot4,
            PlayerInputSignalCommands.Flashlight
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty bindings = serializedObject.FindProperty("bindings");
            SerializedProperty pushOnValidate = serializedObject.FindProperty("pushOnValidateInPlayMode");
            EditorGUILayout.PropertyField(pushOnValidate);

            if (bindings != null)
            {
                int nextSize = Mathf.Max(0, EditorGUILayout.IntField("Size", bindings.arraySize));
                if (nextSize != bindings.arraySize)
                    bindings.arraySize = nextSize;

                for (int i = 0; i < bindings.arraySize; i++)
                {
                    SerializedProperty item = bindings.GetArrayElementAtIndex(i);
                    SerializedProperty buttonName = item.FindPropertyRelative("buttonName");
                    SerializedProperty actionMask = item.FindPropertyRelative("actionMask");
                    SerializedProperty playerCommand = item.FindPropertyRelative("playerCommand");
                    SerializedProperty actionHash = item.FindPropertyRelative("actionNameHash");

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.PropertyField(buttonName);
                        EditorGUILayout.PropertyField(actionMask);
                        int commandIndex = FindCommandIndex((byte)playerCommand.intValue);
                        commandIndex = EditorGUILayout.Popup("Player Command", commandIndex, CommandNames);
                        playerCommand.intValue = CommandValues[commandIndex];
                        EditorGUILayout.LabelField("Hash", actionHash.uintValue.ToString());
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            H8InputMappingFacade facade = (H8InputMappingFacade)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Validation", facade.HasValidationErrors ? "ERRORS" : "OK");
            EditorGUILayout.LabelField("Runtime Bindings", facade.ValidationRuntimeBindingCount.ToString(CultureInfo.InvariantCulture));
            if (facade.HasValidationErrors)
            {
                EditorGUILayout.LabelField("Null Binding Rows", facade.ValidationNullBindingCount + " first " + facade.ValidationFirstNullBindingIndex);
                EditorGUILayout.LabelField("Duplicate Action Hash Rows", facade.ValidationDuplicateActionHashCount + " first " + facade.ValidationFirstDuplicateActionHashIndex);
            }

            if (GUILayout.Button("Sync Input Map"))
                TrySyncInputFacade(facade);
        }

        private static void TrySyncInputFacade(H8InputMappingFacade facade)
        {
            if (facade == null)
                return;

            bool synced = facade.SyncToVault(Hecton8.Core.GlobalRegistry.DataVault);
            if (!synced)
                Debug.LogError("[H8Bridge] Input map sync failed. Fix duplicate action hashes or wait for DataVault allocation fences to clear.");
        }

        private static int FindCommandIndex(byte command)
        {
            for (int i = 0; i < CommandValues.Length; i++)
            {
                if (CommandValues[i] == command)
                    return i;
            }

            return 0;
        }
    }
}
