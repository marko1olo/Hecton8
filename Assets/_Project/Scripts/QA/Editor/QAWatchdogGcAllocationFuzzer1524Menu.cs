#if UNITY_EDITOR
using Hecton8.QA;
using UnityEditor;

namespace Hecton8.QA.Editor
{
    public static class QAWatchdogGcAllocationFuzzer1524Menu
    {
        [MenuItem("Hecton8/QA/1524/Arm GC Update Fuzzer", false, 15240)]
        private static void ArmUpdateFuzzer()
        {
            QAWatchdogGcAllocationFuzzer1524.ArmCold();
        }

        [MenuItem("Hecton8/QA/1524/Arm GC Update Fuzzer", true)]
        private static bool ValidateArmUpdateFuzzer()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Hecton8/QA/1524/Disarm GC Update Fuzzer", false, 15241)]
        private static void DisarmUpdateFuzzer()
        {
            QAWatchdogGcAllocationFuzzer1524.DisarmCold();
        }

        [MenuItem("Hecton8/QA/1524/Disarm GC Update Fuzzer", true)]
        private static bool ValidateDisarmUpdateFuzzer()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Hecton8/QA/1524/Inject Single GC Allocation", false, 15242)]
        private static void InjectSingleAllocation()
        {
            QAWatchdogGcAllocationFuzzer1524.InjectSingleAllocationCold();
        }

        [MenuItem("Hecton8/QA/1524/Inject Single GC Allocation", true)]
        private static bool ValidateInjectSingleAllocation()
        {
            return EditorApplication.isPlaying;
        }
    }
}
#endif
