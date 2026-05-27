#if UNITY_EDITOR
using Hecton8.Core;
using UnityEditor;

namespace Hecton8.Physics.Editor
{
    [InitializeOnLoad]
    public static class FluidMemorySovereigntyValidator1322
    {
        static FluidMemorySovereigntyValidator1322()
        {
            ValidateOrThrow();
        }

        [MenuItem("HECTON-8/Physics/Run Fluid Memory Sovereignty Validator 1322")]
        public static void RunMenu()
        {
            ValidateOrThrow();
            H8Debug.Log("[1322] Fluid memory sovereignty validator passed.");
        }

        public static void ValidateOrThrow()
        {
            if (!HectonFluidEngine.ValidateFluidMemorySovereigntyLayout1322(out int failureMask))
                throw new FatalArchitectureException("1322 fluid memory DTO layout violation mask=" + failureMask);
        }
    }
}
#endif
