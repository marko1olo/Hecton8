using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace MoreMountains.Tools
{
	/// <summary>
	/// This save load method saves and loads files as binary files
	/// </summary>
	public class MMSaveLoadManagerMethodBinary : IMMSaveLoadManagerMethod
	{
		/// <summary>
		/// Saves the specified object to disk at the specified location after serializing it
		/// </summary>
		/// <param name="objectToSave"></param>
		/// <param name="saveFile"></param>
		public void Save(object objectToSave, FileStream saveFile)
		{
			string json = JsonUtility.ToJson(objectToSave);
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			saveFile.Write(bytes, 0, bytes.Length);
			saveFile.Close();
		}

		/// <summary>
		/// Loads the specified file from disk and deserializes it
		/// </summary>
		/// <param name="objectType"></param>
		/// <param name="saveFile"></param>
		/// <returns></returns>
		public object Load(System.Type objectType, FileStream saveFile)
		{
			object savedObject;
			byte[] bytes = new byte[saveFile.Length];
			saveFile.Read(bytes, 0, (int)saveFile.Length);
			string json = Encoding.UTF8.GetString(bytes);
			savedObject = JsonUtility.FromJson(json, objectType);
			saveFile.Close();
			return savedObject;
		}
	}
}