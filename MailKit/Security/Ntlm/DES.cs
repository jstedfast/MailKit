//
// DES.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace MailKit.Security.Ntlm {
	enum CipherMode {
		CBC,
		ECB
	}

	sealed class SymmetricKeyEncryptor : IDisposable
	{
		readonly SymmetricKeyAlgorithmProvider algorithm;
		readonly CryptographicKey key;
		readonly IBuffer iv;

		public SymmetricKeyEncryptor (SymmetricKeyAlgorithmProvider algorithm, CryptographicKey key, IBuffer iv)
		{
			this.algorithm = algorithm;
			this.key = key;
			this.iv = iv;
		}

		public int TransformBlock (byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			if (inputBuffer == null)
				throw new ArgumentNullException ("inputBuffer");

			if (inputOffset < 0 || inputOffset > inputBuffer.Length)
				throw new ArgumentOutOfRangeException ("inputOffset");

			if (inputCount < 0 || inputOffset > inputBuffer.Length - inputCount)
				throw new ArgumentOutOfRangeException ("inputCount");

			if (outputBuffer == null)
				throw new ArgumentNullException ("outputBuffer");

			if (outputOffset < 0 || outputOffset > outputBuffer.Length - algorithm.BlockLength)
				throw new ArgumentOutOfRangeException ("outputOffset");

			IBuffer encrypted, data;
			byte[] input, output;

			input = new byte[inputCount];
			Buffer.BlockCopy (inputBuffer, inputOffset, input, 0, inputCount);
			data = CryptographicBuffer.CreateFromByteArray (input);

			encrypted = CryptographicEngine.Encrypt (key, data, iv);

			CryptographicBuffer.CopyToByteArray (encrypted, out output);

			Buffer.BlockCopy (output, 0, outputBuffer, outputOffset, output.Length);

			return output.Length;
		}

		public void Dispose ()
		{
		}
	}

	sealed class DES : IDisposable
	{
		bool disposed;

		public static DES Create ()
		{
			return new DES ();
		}

		~DES ()
		{
			Dispose (false);
		}

		public CipherMode Mode {
			get; set;
		}

		public byte[] Key {
			get; set;
		}

		public void Clear ()
		{
			if (disposed)
				throw ObjectDisposedException ("DES");

			if (Key != null) {
				Array.Clear (Key, 0, Key.Length);
				Key = null;
			}
		}

		public SymmetricKeyEncryptor CreateEncryptor ()
		{
			if (disposed)
				throw ObjectDisposedException ("DES");

			var buffer = CryptographicBuffer.CreateFromByteArray (Key);
			SymmetricKeyAlgorithmProvider algorithm;
			CryptographicKey key;
			string algorithmName;
			IBuffer iv = null;

			switch (Mode) {
			case CipherMode.CBC: algorithmName = SymmetricAlgorithmNames.DesCbc; break;
			case CipherMode.ECB: algorithmName = SymmetricAlgorithmNames.DesEcb; break;
			default: throw new IndexOutOfRangeException ("Mode");
			}

			algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm (algorithmName);
			key = algorithm.CreateSymmetricKey (buffer);

			if (Mode == CipherMode.CBC)
				iv = CryptographicBuffer.GenerateRandom (algorithm.BlockLength);

			return new SymmetricKeyEncryptor (algorithm, key, iv);
		}

		void Dispose (bool disposing)
		{
			Clear ();
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
			disposed = true;
		}
	}
}
