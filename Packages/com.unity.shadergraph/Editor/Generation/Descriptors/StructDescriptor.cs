namespace UnityEditor.ShaderGraph
{
    [GenerationAPI]
    public struct StructDescriptor
    {
        public string name;
        public bool packFields;
        public bool populateWithCustomInterpolators;
        public FieldDescriptor[] fields;
    }
}
