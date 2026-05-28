#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.QA.Editor
{
    /// <summary>
    /// Editor-only hostile fixture for agent 1424. It is never auto-armed and
    /// never owns an Update loop; the menu item injects one managed allocation
    /// on demand so the watchdog can be tested without a persistent hot-path
    /// garbage source.
    /// </summary>
    public static class QAWatchdogGcAllocationFuzzer1424
    {
        private static byte[] _lastAllocation;

        [MenuItem("Hecton8/QA/1424/Inject Single GC Allocation")]
        private static void InjectSingleAllocation()
        {
            _lastAllocation = new byte[1024];
        }
    }
}
#endif
