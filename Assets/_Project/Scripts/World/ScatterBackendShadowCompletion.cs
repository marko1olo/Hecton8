namespace Hecton8.World
{
    /// <summary>
    /// Typed shadow-pass completion payload from the scatter backend host.
    /// Keeps parity bookkeeping out of the director partial.
    /// </summary>
    internal readonly struct ScatterBackendShadowCompletion
    {
        public ScatterBackendShadowCompletion(
            int candidateCount,
            int classicQueuedCandidateCount,
            bool isJobActive)
        {
            CandidateCount = candidateCount;
            ClassicQueuedCandidateCount = classicQueuedCandidateCount;
            CandidateDelta = candidateCount - classicQueuedCandidateCount;
            IsJobActive = isJobActive;
        }

        public int CandidateCount { get; }
        public int ClassicQueuedCandidateCount { get; }
        public int CandidateDelta { get; }
        public bool IsJobActive { get; }
    }
}
