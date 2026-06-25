using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public static class UnityMathematicsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToFloat3(this Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToFloat3(this double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }
    }
}
