//----------------------------------------------------------------------------------------
// <copyright company="">
//     Copyright Miguel Vrolijk (c) 2015. All rights reserved.
// </copyright>
// <author>Miguel Vrolijk</author> 
// <date>2015-04-22</date>
// <time>11:42</time>
//
// <summary>The BAISecurity is a utility to encrypt/decrypt strings used by the 
// BIA.ini file.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Windows;
using ConvertPG2SS.Common;
using ConvertPG2SS.Helpers;

namespace BIASecurity {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		readonly CryptoAes _crypto;
		public MainWindow() {
			InitializeComponent();

			// Retrieve the AES key & vector
			var key = new byte[Constants.KeySize];
			var vector = new byte[Constants.VectorSize];

			using (var writer = new BinaryReader(File.Open(Constants.AesKeyFile, FileMode.Open))) {
				writer.Read(key, 0, Constants.KeySize);
				writer.Read(vector, 0, Constants.VectorSize);
			}

			_crypto = new CryptoAes(key, vector);
		}

		private void EncryptButtonClick(object sender, RoutedEventArgs e) {
			if (FromTextBox.Text.Length == 0) return;
			ResultTextBox.Text = _crypto.EncryptToString(
				FromTextBox.Text, 
				RadioHex.IsChecked != null && (bool) RadioHex.IsChecked);
		}

		private void DecryptButtonClick(object sender, RoutedEventArgs e) {
			if (FromTextBox.Text.Length == 0) return;
			try {
				ResultTextBox.Text = _crypto.DecryptString(
					FromTextBox.Text, 
					RadioHex.IsChecked != null && (bool)RadioHex.IsChecked);
			}
			catch (Exception ex) {
				ResultTextBox.Text = "";
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK);
				Close();
			}
		}

		private void CloseButtonClick(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}
