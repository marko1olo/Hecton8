using System;

namespace Hecton8.Core
{
    /// <summary>
    /// Declares a cold-path startup dependency for registry-owned systems.
    /// Bootstrap validation uses pre-baked edges; this attribute is the canonical source annotation for new systems.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    public sealed class DependencyAttribute : Attribute
    {
        /// <summary>
        /// Required service type that must be registered before the attributed system initializes.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Creates one dependency declaration.
        /// </summary>
        /// <param name="serviceType">Required registry service type.</param>
        public DependencyAttribute(Type serviceType)
        {
            ServiceType = serviceType;
        }
    }
}
