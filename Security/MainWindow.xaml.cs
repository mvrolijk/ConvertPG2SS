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
// <date>2015-04-22</date>
// <time>11:42</time>
//
// <summary>Security is a WPF utility to encrypt/decrypt strings used by the 
// CryptoEas class.</summary>
//----------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Windows;
using ConvertPG2SS.Common;
using ConvertPG2SS.Helpers;

namespace Security {
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
