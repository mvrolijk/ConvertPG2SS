using System.IO;
using System.Security.Cryptography;

namespace GenerateKeyFile {
	class Program {
		private const string FileName = "aes.key";

		private static void Main() {
			var aes = new AesManaged();

			using (var writer = new BinaryWriter(File.Open(FileName, FileMode.Create))) {
				writer.Write(aes.Key);
				writer.Write(aes.IV);
			}
		}
	}
}
