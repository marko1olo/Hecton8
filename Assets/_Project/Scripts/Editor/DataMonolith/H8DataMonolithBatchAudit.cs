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

            if (!valid)
                Debug.LogError("[H8DataMonolithBatchAudit] bake/validate failed: " + validationError + " last=" + H8DataMonolithCompiler.LastError);

            if (!fuzzed)
                Debug.LogError("[H8DataMonolithBatchAudit] corruption fuzzer failed.");

            Debug.Log("[H8DataMonolithBatchAudit] parserFindings=" + parserFindings);

            if (Application.isBatchMode)
                EditorApplication.Exit(valid && fuzzed ? 0 : 1);
        }
    }
}
#endif
