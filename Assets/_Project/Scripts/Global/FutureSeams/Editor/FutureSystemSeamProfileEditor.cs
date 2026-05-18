using System;
using System.IO;
using Hecton8.Global.Contracts;
using Hecton8.Global.FutureSeams.Authoring;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Global.FutureSeams.Editor
{
    /// <summary>
    /// Editor-only facade for validating and exporting dormant future seam reservations.
    /// </summary>
    [CustomEditor(typeof(FutureSystemSeamProfile))]
    public sealed class FutureSystemSeamProfileEditor : UnityEditor.Editor
    {
        // COLD ALLOC: FutureSystemSeamRecord64[64] - editor export scratch - owner: FutureSystemSeamProfileEditor
        private static readonly FutureSystemSeamRecord64[] _records =
            new FutureSystemSeamRecord64[FutureSystemSeamPacking.MaxAuthoringReservationRows];

        // COLD ALLOC: byte[4160] - editor binary export scratch - owner: FutureSystemSeamProfileEditor
        private static readonly byte[] _binaryScratch =
            new byte[FutureSystemSeamPacking.HeaderSizeBytes +
                     (FutureSystemSeamContracts.RecordSizeBytes * FutureSystemSeamPacking.MaxAuthoringReservationRows)];

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            FutureSystemSeamProfile profile = (FutureSystemSeamProfile)target;
            FutureSeamValidationError errors = profile.ValidateProfile();
            int recordCount = profile.BuildRecords(_records);
            int binaryBytes = FutureSystemSeamPacking.ComputeBinarySize(recordCount);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Contract State", errors == FutureSeamValidationError.None ? "PASS" : "PENDING FIX");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Record Count", recordCount);
                EditorGUILayout.IntField("Binary Bytes", binaryBytes);
            }

            if (errors != FutureSeamValidationError.None)
            {
                EditorGUILayout.HelpBox("Validation errors are present. The enum mask below is the exact contract state.", MessageType.Warning);
                EditorGUILayout.EnumFlagsField("Validation Errors", errors);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed Defaults"))
                {
                    Undo.RecordObject(profile, "Seed Future Seam Defaults");
                    profile.SeedDefaultSurfaces();
                    EditorUtility.SetDirty(profile);
                }

                using (new EditorGUI.DisabledScope(errors != FutureSeamValidationError.None || recordCount <= 0))
                {
                    if (GUILayout.Button("Export .h8bin"))
                        ExportBinary(profile, recordCount);
                }
            }
        }

        private static void ExportBinary(FutureSystemSeamProfile profile, int recordCount)
        {
            if (!FutureSystemSeamPacking.TryWriteBinary(
                    new ReadOnlySpan<FutureSystemSeamRecord64>(_records, 0, recordCount),
                    _binaryScratch,
                    out int bytesWritten,
                    out FutureSeamValidationError errors))
            {
                EditorUtility.DisplayDialog("Future Seam Export Failed", "Validation errors are present. Check the inspector error mask.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export Future Seam Reservations",
                "Assets/StreamingAssets/Hecton8/FutureSeams",
                profile.name + ".h8bin",
                "h8bin");

            if (string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                stream.Write(_binaryScratch, 0, bytesWritten);

            AssetDatabase.Refresh();
        }
    }
}
