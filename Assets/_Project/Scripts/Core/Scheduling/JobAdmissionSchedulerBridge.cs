using Hecton8.Core.Contracts;

namespace Hecton8.Core.Scheduling
{
    /// <summary>
    /// Static bridge used by ScheduleAdmitted wrappers without depending on the Core registry assembly.
    /// </summary>
    public static class JobAdmissionSchedulerBridge
    {
        private static IJobAdmissionService _service;

        /// <summary>Current registered admission service, or null before bootstrap.</summary>
        public static IJobAdmissionService Service => _service;

        /// <summary>Binds the bootstrap-owned admission service.</summary>
        /// <param name="service">Service instance.</param>
        public static void SetService(IJobAdmissionService service)
        {
            _service = service;
        }

        /// <summary>Clears the bridge when the owning service shuts down.</summary>
        /// <param name="service">Service instance requesting unbind.</param>
        public static void ClearService(IJobAdmissionService service)
        {
            if (ReferenceEquals(_service, service))
                _service = null;
        }
    }
}
