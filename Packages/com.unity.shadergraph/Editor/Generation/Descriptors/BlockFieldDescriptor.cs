using System;
namespace UnityEditor.ShaderGraph
{
    public class BlockFieldDescriptor : FieldDescriptor
    {
        public string displayName { get; }
        public IControl control { get; }
        public ShaderStage shaderStage { get; }
        public bool isHidden { get; }
        public bool isUnknown { get; }
        public bool isCustom { get; }

        internal string path { get; set; }

        public BlockFieldDescriptor(string tag, string referenceName, string define, IControl control, ShaderStage shaderStage, bool isHidden = false, bool isUnknown = false, bool isCustom = false, string interpolation = "")
            : base(tag, referenceName, define, interpolation)
        {
            this.displayName = referenceName;
            this.control = control;
            this.shaderStage = shaderStage;
            this.isHidden = isHidden;
            this.isUnknown = isUnknown;
            this.isCustom = isCustom;
        }

        public BlockFieldDescriptor(string tag, string referenceName, string displayName, string define, IControl control, ShaderStage shaderStage, bool isHidden = false, bool isUnknown = false, bool isCustom = false)
            : base(tag, referenceName, define)
        {
            this.displayName = displayName;
            this.control = control;
            this.shaderStage = shaderStage;
            this.isHidden = isHidden;
            this.isUnknown = isUnknown;
            this.isCustom = isCustom;
        }
    }
}
