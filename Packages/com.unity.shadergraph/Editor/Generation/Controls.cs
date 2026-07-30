using UnityEngine;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    // This whole file is regrettable.
    // However, right now we need an abstraction for MaterialSlot for use with BlockFieldDescriptors.
    // MaterialSlot is very leaky, so we cant make it public but we need BlockFieldDescriptor to be public.
    // All MaterialSlot types required by a BlockFieldDescriptor need a matching Control here.
    // We also need a corresponding case in BlockNode.AddSlot for each control.

    public interface IControl
    {
        ShaderGraphRequirements GetRequirements();
        MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor);
    }

    public class PositionControl : IControl
    {
        public CoordinateSpace space { get; private set; }

        public PositionControl(CoordinateSpace space)
        {
            this.space = space;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return new ShaderGraphRequirements() { requiresPosition = space.ToNeededCoordinateSpace() };
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new PositionMaterialSlot(0, descriptor.displayName, descriptor.name, space, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class NormalControl : IControl
    {
        public CoordinateSpace space { get; private set; }

        public NormalControl(CoordinateSpace space)
        {
            this.space = space;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return new ShaderGraphRequirements() { requiresNormal = space.ToNeededCoordinateSpace() };
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new NormalMaterialSlot(0, descriptor.displayName, descriptor.name, space, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class TangentControl : IControl
    {
        public CoordinateSpace space { get; private set; }

        public TangentControl(CoordinateSpace space)
        {
            this.space = space;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return new ShaderGraphRequirements() { requiresTangent = space.ToNeededCoordinateSpace() };
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new TangentMaterialSlot(0, descriptor.displayName, descriptor.name, space, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class ColorControl : IControl
    {
        public Color value { get; private set; }
        public bool hdr { get; private set; }

        public ColorControl(Color value, bool hdr)
        {
            this.value = value;
            this.hdr = hdr;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            var colorMode = hdr ? ColorMode.HDR : ColorMode.Default;
            return new ColorRGBMaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, colorMode, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class ColorRGBAControl : IControl
    {
        public Color value { get; private set; }

        public ColorRGBAControl(Color value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new ColorRGBAMaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class FloatControl : IControl
    {
        public float value { get; private set; }

        public FloatControl(float value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new Vector1MaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class Vector2Control : IControl
    {
        public Vector2 value { get; private set; }

        public Vector2Control(Vector2 value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new Vector2MaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class Vector3Control : IControl
    {
        public Vector3 value { get; private set; }

        public Vector3Control(Vector3 value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new Vector3MaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class Vector4Control : IControl
    {
        public Vector4 value { get; private set; }

        public Vector4Control(Vector4 value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return ShaderGraphRequirements.none;
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new Vector4MaterialSlot(0, descriptor.displayName, descriptor.name, SlotType.Input, value, descriptor.shaderStage.GetShaderStageCapability());
        }
    }

    public class VertexColorControl : IControl
    {
        public Color value { get; private set; }

        public VertexColorControl(Color value)
        {
            this.value = value;
        }

        public ShaderGraphRequirements GetRequirements()
        {
            return new ShaderGraphRequirements() { requiresVertexColor = true };
        }

        public MaterialSlot InstantiateSlot(BlockFieldDescriptor descriptor)
        {
            return new VertexColorMaterialSlot(0, descriptor.displayName, descriptor.name, descriptor.shaderStage.GetShaderStageCapability());
        }
    }
}
