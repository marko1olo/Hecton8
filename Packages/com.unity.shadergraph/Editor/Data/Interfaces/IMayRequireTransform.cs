using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    public struct NeededTransform
    {
        static Dictionary<UnityMatrixType, NeededTransform> s_TransformMap = new Dictionary<UnityMatrixType, NeededTransform>
        {
            {UnityMatrixType.Model, ObjectToWorld},
            {UnityMatrixType.InverseModel, WorldToObject},
            {UnityMatrixType.View, WorldToView},
            {UnityMatrixType.InverseView, ViewToWorld},
            {UnityMatrixType.Projection, ViewToScreen},
            {UnityMatrixType.InverseProjection, ScreenToView},
            {UnityMatrixType.ViewProjection, WorldToScreen},
            {UnityMatrixType.InverseViewProjection, ScreenToWorld},
        };

        public static NeededTransform None => new NeededTransform(NeededCoordinateSpace.None, NeededCoordinateSpace.None);
        public static NeededTransform ObjectToWorld => new NeededTransform(NeededCoordinateSpace.Object, NeededCoordinateSpace.World);
        public static NeededTransform WorldToObject => new NeededTransform(NeededCoordinateSpace.World, NeededCoordinateSpace.Object);
        public static NeededTransform WorldToView => new NeededTransform(NeededCoordinateSpace.World, NeededCoordinateSpace.View);
        public static NeededTransform ViewToWorld => new NeededTransform(NeededCoordinateSpace.View, NeededCoordinateSpace.World);
        public static NeededTransform ViewToScreen => new NeededTransform(NeededCoordinateSpace.View, NeededCoordinateSpace.Screen);
        public static NeededTransform ScreenToView => new NeededTransform(NeededCoordinateSpace.Screen, NeededCoordinateSpace.View);
        public static NeededTransform WorldToScreen => new NeededTransform(NeededCoordinateSpace.World, NeededCoordinateSpace.Screen);
        public static NeededTransform ScreenToWorld => new NeededTransform(NeededCoordinateSpace.Screen, NeededCoordinateSpace.World);

        public NeededTransform(NeededCoordinateSpace from, NeededCoordinateSpace to)
        {
            this.from = from;
            this.to = to;
        }

        // Secondary constructor for certain nodes like TransformationMatrix.
        internal NeededTransform(UnityMatrixType matrix)
        {
            if (s_TransformMap.TryGetValue(matrix, out var transform))
            {
                from = transform.from;
                to = transform.to;
            }
            else
            {
                from = NeededCoordinateSpace.None;
                to = NeededCoordinateSpace.None;
            }
        }

        public NeededCoordinateSpace from;
        public NeededCoordinateSpace to;
    }

    interface IMayRequireTransform
    {
        NeededTransform[] RequiresTransform(ShaderStageCapability stageCapability = ShaderStageCapability.All);
    }
}
