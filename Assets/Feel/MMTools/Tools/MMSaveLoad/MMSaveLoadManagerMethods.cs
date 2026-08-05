using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace MoreMountains.Tools
{
	/// <summary>
	/// An interface to implement save and load using different methods (binary, json, etc)
	/// </summary>
	public interface IMMSaveLoadManagerMethod
	{
		void Save(object objectToSave, FileStream saveFile);
		object Load(System.Type objectType, FileStream saveFile);
	}

	/// <summary>
	/// The possible methods to save and load files to and from disk available in the MMSaveLoadManager
	/// </summary>
	public enum MMSaveLoadManagerMethods { Json, JsonEncrypted, Binary, BinaryEncrypted };

	/// <summary>
	/// This class implements methods to encrypt and decrypt streams
	/// </summary>
	public abstract class MMSaveLoadManagerEncrypter
	{
		/// <summary>
		/// The Key to use to save and load the file
		/// </summary>
		public virtual string Key { get; set; }

		protected string _saltText;

		public MMSaveLoadManagerEncrypter(string key, string saltText)
		{
			Key = key;
			_saltText = saltText;
		}

		/// <summary>
		/// Encrypts the specified input stream into the specified output stream using the key passed in parameters
		/// </summary>
		/// <param name="inputStream"></param>
		/// <param name="outputStream"></param>
		/// <param name="sKey"></param>
		protected virtual void Encrypt(Stream inputStream, Stream outputStream, string sKey)
		{
			byte[] saltBytes = Encoding.ASCII.GetBytes(_saltText);
			using (Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(sKey, saltBytes, 1000))
			{
				byte[] key = keyDerivation.GetBytes(32);
				byte[] nonce = new byte[12];
				using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
				{
					rng.GetBytes(nonce);
				}

				byte[] plaintext;
				using (MemoryStream ms = new MemoryStream())
				{
					inputStream.CopyTo(ms);
					plaintext = ms.ToArray();
				}

				byte[] ciphertext = new byte[plaintext.Length];
				byte[] tag = new byte[16];

				using (AesGcm aesGcm = new AesGcm(key))
				{
					aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
				}

				outputStream.Write(nonce, 0, nonce.Length);
				outputStream.Write(tag, 0, tag.Length);
				outputStream.Write(ciphertext, 0, ciphertext.Length);
			}
		}

		/// <summary>
		/// Decrypts the input stream into the output stream using the key passed in parameters
		/// </summary>
		/// <param name="inputStream"></param>
		/// <param name="outputStream"></param>
		/// <param name="sKey"></param>
		protected virtual void Decrypt(Stream inputStream, Stream outputStream, string sKey)
		{
			byte[] nonce = new byte[12];
			if (inputStream.Read(nonce, 0, nonce.Length) != nonce.Length)
			{
				throw new CryptographicException("Invalid stream length for nonce.");
			}

			byte[] tag = new byte[16];
			if (inputStream.Read(tag, 0, tag.Length) != tag.Length)
			{
				throw new CryptographicException("Invalid stream length for tag.");
			}

			byte[] ciphertext;
			using (MemoryStream ms = new MemoryStream())
			{
				inputStream.CopyTo(ms);
				ciphertext = ms.ToArray();
			}

			byte[] plaintext = new byte[ciphertext.Length];
			byte[] saltBytes = Encoding.ASCII.GetBytes(_saltText);

			using (Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(sKey, saltBytes, 1000))
			{
				byte[] key = keyDerivation.GetBytes(32);
				using (AesGcm aesGcm = new AesGcm(key))
				{
					aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
				}
			}

			outputStream.Write(plaintext, 0, plaintext.Length);
		}
	}
}