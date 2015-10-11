//----------------------------------------------------------------------------------------
// <copyright company="">
//    Copyright 2015 Miguel Vrolijk.
//
//    This file is part of ConvertPG2SS.
//
//    ConvertPG2SS is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    ConvertPG2SS is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy (COPYING.txt) of the GNU General Public
//    License along with ConvertPG2SS.  If not, see
//    <http://www.gnu.org/licenses/>.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-21</date>
// <time>19:37</time>
//
// <summary>Based on:
// http://stackoverflow.com/questions/165808/simple-2-way-encryption-for-c-sharp
// This class is used to handle encryptions and decriptions</summary>
//----------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConvertPG2SS.Common;

namespace ConvertPG2SS.Helpers {
	/// <summary>
	///     The CryptoAes class. Used to encrypt and decrypt sensitive data.
	/// </summary>
	class CryptoAes {
		private readonly ICryptoTransform _decryptorTransform;
		private readonly ICryptoTransform _encryptorTransform;
		private readonly UTF8Encoding _utfEncoder;

		/// <summary>
		///     Constructor.
		/// </summary>
		internal CryptoAes(byte[] key, byte[] vector) {
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (vector == null) throw new ArgumentNullException(nameof(vector));

			if (key.Length != Constants.KeySize) {
				throw new ArgumentOutOfRangeException(
					$"Key length must be {Constants.KeySize}; {key.Length} was provided.");
			}

			if (vector.Length != Constants.VectorSize) {
				throw new ArgumentOutOfRangeException(
					$"Vector length must be {Constants.VectorSize}; {vector.Length} was provided.");
			}

			// This is our encryption method.
			var rm = new RijndaelManaged();

			// Create an encryptor and a decryptor using our encryption method,
			// key, and vector.
			_encryptorTransform = rm.CreateEncryptor(key, vector);
			_decryptorTransform = rm.CreateDecryptor(key, vector);

			// Used to translate bytes to text and vice versa
			_utfEncoder = new UTF8Encoding();
		}

		/// <summary>
		///     Generates an encryption key.
		/// </summary>
		/// <returns>Encryption key</returns>
		internal byte[] GenerateEncryptionKey() {
			// Generate a Key.
			var rm = new RijndaelManaged();
			rm.GenerateKey();
			return rm.Key;
		}

		/// <summary>
		///     Generates a unique encryption vector.
		/// </summary>
		/// <returns>Encryption vector</returns>
		internal byte[] GenerateEncryptionVector() {
			// Generate a Vector.
			var rm = new RijndaelManaged();
			rm.GenerateIV();
			return rm.IV;
		}

		/// <summary>
		///     Encrypt some text and return a string suitable for passing in a URL.
		/// </summary>
		/// <param name="textValue">The text to be encrypted</param>
		/// <param name="hex"></param>
		/// <returns>
		///     A string representing the encrypted text, which is suitable
		///     for passing in a URL.
		/// </returns>
		internal string EncryptToString(string textValue, bool hex = false) {
			return ByteArrToString(Encrypt(textValue), hex);
		}

		/// <summary>
		///     Encrypt some text and return an encrypted byte array.
		/// </summary>
		/// <param name="textValue">Text to be encrypted</param>
		/// <returns>Byte array representing the encrypted text</returns>
		internal byte[] Encrypt(string textValue) {
			// Translates our text value into a byte array.
			var bytes = _utfEncoder.GetBytes(textValue);

			// Used to stream the data in and out of the CryptoStream.
			var memoryStream = new MemoryStream();

			// We will have to write the unencrypted bytes to the stream,
			// then read the encrypted result back from the stream.
			var cs = new CryptoStream(memoryStream, _encryptorTransform, CryptoStreamMode.Write);
			cs.Write(bytes, 0, bytes.Length);
			cs.FlushFinalBlock();

			memoryStream.Position = 0;
			var encrypted = new byte[memoryStream.Length];
			memoryStream.Read(encrypted, 0, encrypted.Length);

			// Clean up.
			cs.Dispose();
			memoryStream.Dispose();

			return encrypted;
		}

		/// <summary>
		///     Decrypts a string.
		/// </summary>
		/// <param name="encryptedString">The encrypted string</param>
		/// <param name="hex"></param>
		/// <returns>The decrypted string</returns>
		internal string DecryptString(string encryptedString, bool hex = false) {
			return Decrypt(StrToByteArray(encryptedString, hex));
		}

		/// <summary>
		///     Decrypt a byte array.
		/// </summary>
		/// <param name="encryptedValue">The encrypted bye array</param>
		/// <returns>The decrypted string</returns>
		internal string Decrypt(byte[] encryptedValue) {
			var encryptedStream = new MemoryStream();
			var decryptStream = new CryptoStream(encryptedStream,
				_decryptorTransform, CryptoStreamMode.Write);
			decryptStream.Write(encryptedValue, 0, encryptedValue.Length);
			decryptStream.FlushFinalBlock();

			encryptedStream.Position = 0;
			var decryptedBytes = new byte[encryptedStream.Length];
			encryptedStream.Read(decryptedBytes, 0, decryptedBytes.Length);
			encryptedStream.Close();

			return _utfEncoder.GetString(decryptedBytes);
		}

		/// <summary>
		///     Convert a string to a byte array. NOTE: Normally we'd create a Byte
		///     Array from a string using an ASCII encoding (like so):
		///     System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
		///     return encoding.GetBytes(str);
		///     However, this results in character values that cannot be passed in a
		///     URL. So, instead, I just lay out all of the byte values in a long
		///     string of numbers (three per - must pad numbers less than 100).
		/// </summary>
		/// <param name="str">The string to be converted</param>
		/// <param name="hex"></param>
		/// <returns>A byte array representing 'str'</returns>
		private static byte[] StrToByteArray(string str, bool hex) {
			if (str.Length == 0) {
				throw new ArgumentException("Invalid string value in StrToByteArray");
			}

			var ln = hex ? 2 : 3;
			var byteArr = new byte[str.Length / ln];
			var i = 0;
			var j = 0;
			do {
				var val = hex 
					? byte.Parse(
						str.Substring(i, ln), 
						NumberStyles.AllowHexSpecifier, 
						CultureInfo.InvariantCulture) 
					: byte.Parse(str.Substring(i, ln), CultureInfo.InvariantCulture);
				byteArr[j++] = val;
				i += ln;
			} while (i < str.Length);
			return byteArr;
		}

		/// <summary>
		///     Same comment as above. Normally the conversion would use an ASCII
		///     encoding in the other direction:
		///     System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
		///     return enc.GetString(byteArr);
		/// </summary>
		/// <param name="byteArr">Byte array to be converted</param>
		/// <param name="hex"></param>
		/// <returns>Converted string</returns>
		private static string ByteArrToString(ICollection<byte> byteArr, bool hex) {
			StringBuilder tempStr;
			switch (hex) {
				case false:
					tempStr = new StringBuilder(byteArr.Count * 3);
					foreach (var b in byteArr) {
						if (b < 10) tempStr.Append("00");
						else if (b < 100) tempStr.Append("0");
						tempStr.Append(b.ToString(CultureInfo.InvariantCulture));
					}
					break;

				default:
					tempStr = new StringBuilder(byteArr.Count * 2);
					foreach (var b in byteArr) tempStr.AppendFormat("{0:x2}", b);
					break;
			}
			return tempStr.ToString();
		}
	}
}