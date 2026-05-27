using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Bounded owner-local component capture for Construction runtime paths.
    /// </summary>
    internal static class ConstructionParentLookup
    {
        private const int DefaultMaxParentDepth = 32;

        public static bool TryCaptureSelfOrParent<T>(Component source, out T component, int maxDepth = DefaultMaxParentDepth)
            where T : class
        {
            component = null;
            if (source == null || maxDepth <= 0)
                return false;

            Transform current = source.transform;
            for (int depth = 0; current != null && depth < maxDepth; depth++)
            {
                if (current.TryGetComponent(out component) && component != null)
                    return true;

                current = current.parent;
            }

            return false;
        }
    }
}
