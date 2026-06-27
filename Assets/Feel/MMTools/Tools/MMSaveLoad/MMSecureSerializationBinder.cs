using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace MoreMountains.Tools
{
    /// <summary>
    /// A strict SerializationBinder for BinaryFormatter that mitigates insecure deserialization vulnerabilities
    /// by only allowing the explicitly requested type, alongside common safe primitive/collection types.
    /// </summary>
    public sealed class MMSecureSerializationBinder : SerializationBinder
    {
        private readonly Type _allowedType;

        public MMSecureSerializationBinder(Type allowedType)
        {
            _allowedType = allowedType;
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            Type typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
            if (typeToDeserialize == null)
            {
                typeToDeserialize = Type.GetType(typeName);
            }

            if (typeToDeserialize == null)
            {
                throw new SerializationException("Type " + typeName + " could not be resolved.");
            }

            // Exactly the allowed type
            if (typeToDeserialize == _allowedType)
            {
                return typeToDeserialize;
            }

            // Primitive types and string are safe
            if (typeToDeserialize.IsPrimitive || typeToDeserialize == typeof(string) || typeToDeserialize == typeof(decimal))
            {
                return typeToDeserialize;
            }

            // Allow generic lists of the allowed type
            if (typeToDeserialize.IsGenericType && typeToDeserialize.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type[] typeArguments = typeToDeserialize.GetGenericArguments();
                if (typeArguments.Length == 1 && typeArguments[0] == _allowedType)
                {
                    return typeToDeserialize;
                }
            }

            // Allow arrays of the allowed type
            if (typeToDeserialize.IsArray && typeToDeserialize.GetElementType() == _allowedType)
            {
                return typeToDeserialize;
            }

            // Safe basic Unity types that might be serialized
            if (typeToDeserialize.Namespace == "UnityEngine" &&
                (typeToDeserialize.Name == "Vector2" || typeToDeserialize.Name == "Vector3" ||
                 typeToDeserialize.Name == "Vector4" || typeToDeserialize.Name == "Quaternion" ||
                 typeToDeserialize.Name == "Color"   || typeToDeserialize.Name == "Color32"))
            {
                return typeToDeserialize;
            }

            throw new SerializationException("Security Exception: Deserialization of type " + typeName + " is not allowed.");
        }
    }
}
