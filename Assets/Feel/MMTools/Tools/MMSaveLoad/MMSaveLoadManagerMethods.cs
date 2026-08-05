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
			// Magic header for AES-GCM encrypted files
			byte[] magicHeader = Encoding.ASCII.GetBytes("MMGCM");
			outputStream.Write(magicHeader, 0, magicHeader.Length);

			using (var keyDerivation = new Rfc2898DeriveBytes(sKey, Encoding.ASCII.GetBytes(_saltText), 1000, HashAlgorithmName.SHA1))
			{
				byte[] keyBytes = keyDerivation.GetBytes(32);
				byte[] nonce = new byte[12];
				using (var rng = RandomNumberGenerator.Create())
				{
					rng.GetBytes(nonce);
				}

				byte[] inputBytes;
				using (var ms = new MemoryStream())
				{
					inputStream.CopyTo(ms);
					inputBytes = ms.ToArray();
				}

				using (AesGcm aesGcm = new AesGcm(keyBytes))
				{
					byte[] ciphertext = new byte[inputBytes.Length];
					byte[] tag = new byte[16];

					aesGcm.Encrypt(nonce, inputBytes, ciphertext, tag);

					outputStream.Write(nonce, 0, nonce.Length);
					outputStream.Write(tag, 0, tag.Length);
					outputStream.Write(ciphertext, 0, ciphertext.Length);
				}
			}
		}

		private bool ReadFully(Stream stream, byte[] buffer)
		{
			int totalRead = 0;
			while (totalRead < buffer.Length)
			{
				int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
				if (read == 0) return false;
				totalRead += read;
			}
			return true;
		}

		/// <summary>
		/// Decrypts the input stream into the output stream using the key passed in parameters
		/// </summary>
		/// <param name="inputStream"></param>
		/// <param name="outputStream"></param>
		/// <param name="sKey"></param>
		protected virtual void Decrypt(Stream inputStream, Stream outputStream, string sKey)
		{
			long originalPosition = 0;
			if (inputStream.CanSeek)
			{
				originalPosition = inputStream.Position;
			}

			byte[] magicHeader = new byte[5];
			if (!ReadFully(inputStream, magicHeader))
			{
				// Not long enough to be GCM or even legacy, fallback to legacy
				DecryptLegacy(inputStream, outputStream, sKey, originalPosition);
				return;
			}

			string headerString = Encoding.ASCII.GetString(magicHeader);
			if (headerString == "MMGCM")
			{
				// AES-GCM
				byte[] readNonce = new byte[12];
				if (!ReadFully(inputStream, readNonce)) throw new CryptographicException("Failed to read nonce");

				byte[] readTag = new byte[16];
				if (!ReadFully(inputStream, readTag)) throw new CryptographicException("Failed to read tag");

				using (var ms = new MemoryStream())
				{
					inputStream.CopyTo(ms);
					byte[] readCiphertext = ms.ToArray();

					using (var dKeyDerivation = new Rfc2898DeriveBytes(sKey, Encoding.ASCII.GetBytes(_saltText), 1000, HashAlgorithmName.SHA1))
					{
						byte[] dKeyBytes = dKeyDerivation.GetBytes(32);

						using (AesGcm dAesGcm = new AesGcm(dKeyBytes))
						{
							byte[] plaintext = new byte[readCiphertext.Length];
							dAesGcm.Decrypt(readNonce, readCiphertext, readTag, plaintext);

							outputStream.Write(plaintext, 0, plaintext.Length);
						}
					}
				}
			}
			else
			{
				// Legacy AES-CBC fallback
				DecryptLegacy(inputStream, outputStream, sKey, originalPosition);
			}
		}

		protected virtual void DecryptLegacy(Stream inputStream, Stream outputStream, string sKey, long originalPosition)
		{
			if (inputStream.CanSeek)
			{
				inputStream.Position = originalPosition;
			}
			else
			{
				throw new System.Exception("Cannot fallback to legacy decryption on a stream that does not support seeking.");
			}

			Aes algorithm = Aes.Create();
			Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sKey, Encoding.ASCII.GetBytes(_saltText));

			algorithm.Key = key.GetBytes(algorithm.KeySize / 8);
			algorithm.IV = key.GetBytes(algorithm.BlockSize / 8);

			CryptoStream cryptostream = new CryptoStream(inputStream, algorithm.CreateDecryptor(), CryptoStreamMode.Read);
			cryptostream.CopyTo(outputStream);
		}
	}
}