using Hecton8.Global.Contracts;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Global.FutureSeams.Editor
{
    internal static class FutureSystemSeamStaticValidator
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

        [MenuItem("Hecton8/Architecture/Validate Future System Seams", priority = 920)]
        private static void ValidateFutureSeams()
        {
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
                Debug.Log("[H8 FutureSeams] PASS: dormant reservation records, binary writer, public API closure, survival envelope, and blackbox ring validated.");
                EditorUtility.DisplayDialog(
                    "Future System Seams",
                    "PASS: dormant future-seam contracts validated. No runtime systems were activated.",
                    "OK");
                return;
            }

            Debug.LogError("[H8 FutureSeams] FAIL: dormant future-seam contract validation rejected the current reservation set.");
            EditorUtility.DisplayDialog(
                "Future System Seams",
                "FAIL: dormant future-seam contracts are not valid. Check the editor console and source contract.",
                "OK");
        }
    }
}
