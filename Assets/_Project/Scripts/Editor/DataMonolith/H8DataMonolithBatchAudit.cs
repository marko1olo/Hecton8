#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    public static class H8DataMonolithBatchAudit
    {
        public static void RunFromCommandLine()
        {
            H8DataMonolithLayoutGuard.ValidateOrThrow();

            bool baked = H8DataMonolithCompiler.BakeAll(logSummary: true);
            string validationError = string.Empty;
            bool valid = baked && H8DataMonolithCompiler.TryValidateOutputBlob(out validationError);
            bool fuzzed = valid && H8DataMonolithCorruptionFuzzer.Run();
            int parserFindings = OOP_StaticData_Scanner.Run();
            bool parserClean = parserFindings == 0;
            H8DataMonolithReleaseParserScanner.ScanResult releaseGate = H8DataMonolithReleaseParserScanner.Scan(
                writeReport: true,
                blockOnFindings: false,
                developmentBuild: false,
                target: EditorUserBuildSettings.activeBuildTarget);
            bool releaseGateClean = releaseGate.BlockingFindingCount == 0;

            if (!valid)
                Debug.LogError("[H8DataMonolithBatchAudit] bake/validate failed: " + validationError + " last=" + H8DataMonolithCompiler.LastError);

            if (!fuzzed)
                Debug.LogError("[H8DataMonolithBatchAudit] corruption fuzzer failed.");

            if (!parserClean)
                Debug.LogError("[H8DataMonolithBatchAudit] parser scanner found production residue: " + parserFindings);

            if (!releaseGateClean)
                Debug.LogError("[H8DataMonolithBatchAudit] release gate found production blockers: " + releaseGate.BlockingFindingCount);

            Debug.Log("[H8DataMonolithBatchAudit] parserFindings=" + parserFindings);
            Debug.Log("[H8DataMonolithBatchAudit] releaseGateFindings=" + releaseGate.BlockingFindingCount);

            if (Application.isBatchMode)
                EditorApplication.Exit(valid && fuzzed && parserClean && releaseGateClean ? 0 : 1);
        }
    }
}
#endif
