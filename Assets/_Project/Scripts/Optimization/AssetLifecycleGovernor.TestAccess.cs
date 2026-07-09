namespace Hecton8.Optimization
{
    public sealed partial class AssetLifecycleGovernor
    {
        public long Test_GetFrameSequence() => _frameSequence;

        public static System.Action OnEvaluateAddressableTtlAndQueueReleases;
        public static System.Action<long> OnReportColdTickBudgetIfNeeded;
    }
}
