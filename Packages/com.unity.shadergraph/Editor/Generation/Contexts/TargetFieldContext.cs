using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    [GenerationAPI]
    public class TargetFieldContext
    {
        internal List<ConditionalField> conditionalFields { get; private set; }
        internal PassDescriptor pass { get; private set; }
        internal List<(BlockFieldDescriptor descriptor, bool isDefaultValue)> blocks { get; private set; }
        internal List<BlockFieldDescriptor> connectedBlocks { get; private set; }
        public bool hasDotsProperties { get; private set; }

        // NOTE: active blocks (and connectedBlocks) do not include temporarily added default blocks
        internal TargetFieldContext(PassDescriptor pass, List<(BlockFieldDescriptor descriptor, bool isDefaultValue)> activeBlocks, List<BlockFieldDescriptor> connectedBlocks, bool hasDotsProperties)
        {
            conditionalFields = new List<ConditionalField>();
            this.pass = pass;
            this.blocks = activeBlocks;
            this.connectedBlocks = connectedBlocks;
            this.hasDotsProperties = hasDotsProperties;
        }

        internal void AddField(FieldDescriptor field, bool conditional = true)
        {
            conditionalFields.Add(new ConditionalField(field, conditional));
        }
    }
}
