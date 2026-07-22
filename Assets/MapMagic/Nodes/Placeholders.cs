using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using UnityEngine.Profiling;

using Den.Tools;

using MapMagic.Core;
using MapMagic.Products;

namespace MapMagic.Nodes
{
	public static class Placeholders
	{
		[Serializable]
		public class SerObject
		/// nearly copy of Serializer.Object, but has some fields missed intentionally to allow serialization
		{
			public string type = null;
			public string[] fields = null;
			public Serializer.Value[] values = null;

			public static explicit operator Serializer.Object (SerObject src)
			{
				Serializer.Object dst = new Serializer.Object();
				dst.type = src.type;
				dst.fields = src.fields;
				dst.values = src.values;
				return dst;
			}

			public static explicit operator SerObject (Serializer.Object src)
			{
				SerObject dst = new SerObject();
				dst.type = src.type;
				dst.fields = src.fields;
				dst.values = src.values;
				return dst;
			}
		}

		public abstract class GenericPlaceholder : Generator,  Serializer.ICustomSerialization, ISerializationCallbackReceiver
		{
			public string origType;

			[NonSerialized] public string[] origFields = new string[0];
			[NonSerialized] public Serializer.Value[] origValues = new Serializer.Value[0];
			[NonSerialized] public UnityEngine.Object[] origUnityObjects = new UnityEngine.Object[0];
			[NonSerialized] public object[] origObjects = new object[0];

			[NonSerialized] private Serializer.Object[] tempAllSerialized;
			[NonSerialized] private object[] tempAllDeserialized;
			[NonSerialized] private Serializer.Object tempSerObj;

			public override void Generate (TileData data, StopToken stop) { }

			public void OnBeforeSerialize () { }

			public void OnAfterDeserialize ()
			{
				if (tempSerObj != null && tempAllSerialized != null && tempAllDeserialized != null)
				{
					for (int v=0; v<tempSerObj.values.Length; v++)
					{
						if (tempSerObj.values[v].t == 255) // reference
						{
							int refId = tempSerObj.values[v];
							if (refId >= 0 && refId < tempAllSerialized.Length)
							{
								Serializer.Object fieldObj = tempAllSerialized[refId];
								if (fieldObj != null && fieldObj.uniObj == null) // C# object
								{
									object val = Serializer.DeserializeObject(refId, tempAllSerialized, tempAllDeserialized);
									origObjects[v] = val;
								}
							}
						}
					}

					tempSerObj = null;
					tempAllSerialized = null;
					tempAllDeserialized = null;
				}
			}

			public void PreprocessBeforeDeserialize (Serializer.Object serObj, Serializer.Object[] allSerialized, object[] allDeserialized) 
			/// Loading placeholder
			/// Reading serObj and converting it to placeholder values
			{
				origType = serObj.type;

				List<string> fieldsList = new List<string>();
				List<Serializer.Value> valuesList = new List<Serializer.Value>();
				List<UnityEngine.Object> unityObjectsList = new List<UnityEngine.Object>();
				List<object> objectsList = new List<object>();

				for (int v=0; v<serObj.values.Length; v++)
				{
					fieldsList.Add(serObj.fields[v]);
					valuesList.Add(serObj.values[v]);

					if (serObj.values[v].t != 255) //if not reference
					{
						unityObjectsList.Add(null);
						objectsList.Add(null);
					}
					else if (serObj.values[v] >= 0 && serObj.values[v] < allSerialized.Length) //if not null
					{
						Serializer.Object fieldObj = allSerialized[serObj.values[v]];
						if (fieldObj != null && fieldObj.uniObj != null)
						{
							unityObjectsList.Add(fieldObj.uniObj);
							objectsList.Add(null); // It's a Unity object, not a C# object to be handled by origObjects
						}
						else
						{
							unityObjectsList.Add(null);
							objectsList.Add(null); // C# object, will be populated in OnAfterDeserialize
						}
					}
					else
					{
						unityObjectsList.Add(null);
						objectsList.Add(null);
					}
				}

				origFields = fieldsList.ToArray();
				origValues = valuesList.ToArray();
				origUnityObjects = unityObjectsList.ToArray();
				origObjects = objectsList.ToArray();

				this.tempSerObj = serObj;
				this.tempAllSerialized = allSerialized;
				this.tempAllDeserialized = allDeserialized;
			}


			public void PostprocessAfterSerialize (Serializer.Object serObj, Dictionary<object,Serializer.Object> allSerialized) 
			/// Storing original instead of placeholder
			/// Writing serObj 
			{
				serObj.type = origType;

				ArrayTools.Append(ref serObj.fields, origFields);
				ArrayTools.Append(ref serObj.values, origValues); 

				int length = serObj.values.Length;
				for (int i=0; i<origValues.Length; i++)
				{
					if (origUnityObjects[i] != null)
					{
						if (!allSerialized.TryGetValue(origUnityObjects[i], out Serializer.Object fieldSerObj))
						{
							fieldSerObj = new Serializer.Object() { refId = allSerialized.Count, type = origUnityObjects[i].GetType().AssemblyQualifiedName, uniObj = origUnityObjects[i] };
							allSerialized.Add(origUnityObjects[i], fieldSerObj);
						}
						serObj.values[length - origValues.Length + i] = fieldSerObj.refId;
					}
					else if (origObjects[i] != null)
					{
						Serializer.Object fieldSerObj = Serializer.SerializeObject(origObjects[i], allSerialized);
						serObj.values[length - origValues.Length + i] = fieldSerObj.refId;
					}
				}
			}
		}

		public static bool IsInletType (Type type) => 
			type.GetInterfaces().Find(i => i.GetGenericTypeDefinition() == typeof(IInlet<>))  >=  0;

		[GeneratorMenu (name = "Unknown", iconName="GeneratorIcons/Generator")]
		public class InletOutletPlaceholder : GenericPlaceholder, IInlet<object>, IOutlet<object> { }

		[GeneratorMenu (name = "Unknown", iconName="GeneratorIcons/Generator")]
		public class InletPlaceholder : GenericPlaceholder, IInlet<object> { }

		[GeneratorMenu (name = "Unknown", iconName="GeneratorIcons/Generator")]
		public class OutletPlaceholder : GenericPlaceholder, IOutlet<object> { }

		[GeneratorMenu (name = "Unknown", iconName="GeneratorIcons/Generator")]
		public class Placeholder : GenericPlaceholder { }


		[GeneratorMenu (name = "Unknown", iconName="GeneratorIcons/Generator")]
		public class MultiInletOutletPlaceholder : GenericPlaceholder, IMultiInlet, IMultiOutlet, IMultiLayer
		{ 
			public IEnumerable<IInlet<object>> Inlets ()
			{
				for (int i=0; i<origObjects.Length; i++)
				{
					if (origObjects[i] is IInlet<object> inlet) yield return inlet;
					else if (origObjects[i] is IEnumerable enumerable)
					{
						foreach (object obj in enumerable)
							if (obj is IInlet<object> subInlet) yield return subInlet;
					}
				}
			}

			public IEnumerable<IOutlet<object>> Outlets ()
			{
				for (int i=0; i<origObjects.Length; i++)
				{
					if (origObjects[i] is IOutlet<object> outlet) yield return outlet;
					else if (origObjects[i] is IEnumerable enumerable)
					{
						foreach (object obj in enumerable)
							if (obj is IOutlet<object> subOutlet) yield return subOutlet;
					}
				}
			}

			public IList<IUnit> Layers
			{
				get
				{
					List<IUnit> layerList = new List<IUnit>();
					for (int i=0; i<origObjects.Length; i++)
					{
						if (origObjects[i] is IUnit unit) layerList.Add(unit);
						else if (origObjects[i] is IEnumerable enumerable)
						{
							foreach (object obj in enumerable)
								if (obj is IUnit subUnit) layerList.Add(subUnit);
						}
					}
					return layerList;
				}
				set { }
			}

			public bool Inversed => false;
			public bool HideFirst => false;
		}
	}
}