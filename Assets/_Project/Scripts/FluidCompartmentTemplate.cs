using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Immutable authoring template for submarine fluid compartments and their drainage centroids.
    /// Runtime hydrodynamics must copy these values into native buffers before simulation.
    /// </summary>
    [CreateAssetMenu(fileName = "FluidCompartmentTemplate_", menuName = "Hecton8/Physics/Fluid Compartment Template", order = 19)]
    public sealed class FluidCompartmentTemplate : ScriptableObject
    {
        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct CompartmentRecord
        {
            [Tooltip("Stable authoring hash for the hull module or compartment owner.")]
            [SerializeField] private int hullModuleHash;

            [Tooltip("Maximum flood capacity for this compartment in cubic meters.")]
            [SerializeField, Min(0f)] private float capacityCubicMeters;

            [Tooltip("Maximum pump drainage rate for this compartment in cubic meters per second.")]
            [SerializeField, Min(0f)] private float pumpDrainageRateCubicMetersPerSecond;

            [Tooltip("Compartment centroid expressed in rigidbody-local space.")]
            [SerializeField] private Vector3 localCentroid;

            /// <summary>Stable authoring hash for the hull module or compartment owner.</summary>
            public int HullModuleHash => hullModuleHash;

            /// <summary>Maximum flood capacity for this compartment in cubic meters.</summary>
            public float CapacityCubicMeters => capacityCubicMeters;

            /// <summary>Maximum pump drainage rate for this compartment in cubic meters per second.</summary>
            public float PumpDrainageRateCubicMetersPerSecond => pumpDrainageRateCubicMetersPerSecond;

            /// <summary>Compartment centroid expressed in rigidbody-local space.</summary>
            public Vector3 LocalCentroid => localCentroid;

            internal void ClampAuthoring()
            {
                capacityCubicMeters = math.max(0f, capacityCubicMeters);
                pumpDrainageRateCubicMetersPerSecond = math.max(0f, pumpDrainageRateCubicMetersPerSecond);
            }
        }

        [Header("── Compartments ──────────────────")]
        [Tooltip("Immutable compartment records copied into native flood-state buffers at runtime.")]
        [SerializeField] private CompartmentRecord[] compartments = Array.Empty<CompartmentRecord>();

        /// <summary>Immutable compartment records copied into native flood-state buffers at runtime.</summary>
        public CompartmentRecord[] Compartments => compartments;

        private void OnValidate()
        {
            if (compartments == null)
                return;

            for (int i = 0; i < compartments.Length; i++)
            {
                CompartmentRecord record = compartments[i];
                record.ClampAuthoring();
                compartments[i] = record;
            }
        }
    }
}
