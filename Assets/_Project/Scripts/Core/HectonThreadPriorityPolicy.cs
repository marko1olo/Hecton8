using System.Threading;

namespace Hecton8.Core
{
    public enum HectonThreadRole : byte
    {
        BackgroundIo = 0,
        Heartbeat = 1,
        AudioProducer = 2
    }

    public static class HectonThreadPriorityPolicy
    {
        public static ThreadPriority Resolve(HectonThreadRole role)
        {
#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS
            return ThreadPriority.Normal;
#else
            switch (role)
            {
                case HectonThreadRole.AudioProducer:
                    return ThreadPriority.AboveNormal;
                case HectonThreadRole.Heartbeat:
                    return ThreadPriority.BelowNormal;
                case HectonThreadRole.BackgroundIo:
                    return ThreadPriority.Lowest;
                default:
                    return ThreadPriority.Normal;
            }
#endif
        }
    }
}
