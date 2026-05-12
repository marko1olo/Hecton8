using UnityEngine;
using UnityEngine.XR;

namespace Hecton8.Core
{
    /// <summary>
    /// XR platform policy surface for eye texture descriptors and mobile VR compile gates.
    /// </summary>
    public static class HectonXRManager
    {
        public const int BaselineEyeTextureSize = 2048;
        public const int BaselineDepthBits = 24;

        private static RenderTextureDescriptor _cachedEyeDescriptor;
        private static bool _hasCachedDescriptor;

        /// <summary>
        /// Returns the currently resolved eye render texture descriptor. Baseline is 2048x2048 per eye.
        /// </summary>
        public static RenderTextureDescriptor EyeRenderTextureDescriptor
        {
            get
            {
                EnsureEyeDescriptor();
                return _cachedEyeDescriptor;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedEyeDescriptor = default;
            _hasCachedDescriptor = false;
        }

        /// <summary>
        /// Rebuilds the cached XR eye descriptor from Unity XR when available.
        /// </summary>
        public static RenderTextureDescriptor RefreshEyeDescriptor()
        {
            RenderTextureDescriptor descriptor = ResolveUnityEyeDescriptor();
            descriptor.width = Mathf.Max(BaselineEyeTextureSize, descriptor.width);
            descriptor.height = Mathf.Max(BaselineEyeTextureSize, descriptor.height);
            descriptor.depthBufferBits = Mathf.Max(BaselineDepthBits, descriptor.depthBufferBits);
            descriptor.msaaSamples = Mathf.Max(1, descriptor.msaaSamples);
            descriptor.vrUsage = VRTextureUsage.TwoEyes;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            _cachedEyeDescriptor = descriptor;
            _hasCachedDescriptor = true;
            return _cachedEyeDescriptor;
        }

        private static void EnsureEyeDescriptor()
        {
            if (!_hasCachedDescriptor)
                RefreshEyeDescriptor();
        }

        private static RenderTextureDescriptor ResolveUnityEyeDescriptor()
        {
            if (XRSettings.enabled)
            {
                RenderTextureDescriptor descriptor = XRSettings.eyeTextureDesc;
                if (descriptor.width > 0 && descriptor.height > 0)
                    return descriptor;
            }

            return new RenderTextureDescriptor(
                BaselineEyeTextureSize,
                BaselineEyeTextureSize,
                RenderTextureFormat.ARGB32,
                BaselineDepthBits);
        }
    }
}
