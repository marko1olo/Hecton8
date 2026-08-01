using Hecton8.Global.Contracts;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Global.FutureSeams.Editor
{
    public static class FutureSystemSeamStaticValidator
    {
        // COLD ALLOC: FutureSystemSeamRecord64[64] - editor-only validation scratch - owner: FutureSystemSeamStaticValidator
        private static readonly FutureSystemSeamRecord64[] _records =
            new FutureSystemSeamRecord64[FutureSystemSeamPacking.MaxAuthoringReservationRows];

        // COLD ALLOC: byte[4160] - editor-only binary validation scratch - owner: FutureSystemSeamStaticValidator
        private static readonly byte[] _binaryScratch =
            new byte[FutureSystemSeamPacking.HeaderSizeBytes +
                     (FutureSystemSeamContracts.RecordSizeBytes * FutureSystemSeamPacking.MaxAuthoringReservationRows)];

        // COLD ALLOC: FutureKernelBlackboxEntry64[300] - editor-only blackbox probe scratch - owner: FutureSystemSeamStaticValidator
        private static readonly FutureKernelBlackboxEntry64[] _blackboxScratch =
            new FutureKernelBlackboxEntry64[FutureSystemSeamContracts.RequiredBlackboxFrames];

        // Public for -executeMethod / CI batchmode (never DisplayDialog under batch).
        [MenuItem("Hecton8/Architecture/Validate Future System Seams", priority = 920)]
        public static void ValidateFutureSeams()
        {
            bool batch = Application.isBatchMode;

            int count = FutureSystemSeamSelfAudit.BuildDefaultReservations(
                _records,
                out FutureSeamValidationError errors);

            FutureSystemSeamAuditReport64 report = default;
            if (count > 0)
            {
                bool auditPassed = FutureSystemSeamSelfAudit.Run(
                    new System.ReadOnlySpan<FutureSystemSeamRecord64>(_records, 0, count),
                    _binaryScratch,
                    _blackboxScratch,
                    out report);

                if (!auditPassed)
                    errors |= unchecked((FutureSeamValidationError)report.ErrorMask);
            }

            if (errors == FutureSeamValidationError.None)
            {
                Debug.Log("[H8 FutureSeams] RESULT: PASS — dormant reservation records, binary writer, public API closure, survival envelope, and blackbox ring validated.");
                if (!batch)
                {
                    EditorUtility.DisplayDialog(
                        "Future System Seams",
                        "PASS: dormant future-seam contracts validated. No runtime systems were activated.",
                        "OK");
                }
                return;
            }

            // Soft FAIL under -quit: LogError + exit 0 (no DisplayDialog hang).
            Debug.LogError("[H8 FutureSeams] RESULT: FAIL — dormant future-seam contract validation rejected the current reservation set. errors=" + errors);
            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Future System Seams",
                    "FAIL: dormant future-seam contracts are not valid. Check the editor console and source contract.",
                    "OK");
            }
        }
    }
}
