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

#if NETFX_CORE
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
#else
using System.Security.Cryptography;
#endif

namespace MailKit.Security.Ntlm {
#if NETFX_CORE
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
			Array.Copy (inputBuffer, inputOffset, input, 0, inputCount);
			data = CryptographicBuffer.CreateFromByteArray (input);

			encrypted = CryptographicEngine.Encrypt (key, data, iv);

			CryptographicBuffer.CopyToByteArray (encrypted, out output);

			Array.Copy (output, 0, outputBuffer, outputOffset, output.Length);

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
				throw new ObjectDisposedException ("DES");

			if (Key != null) {
				Array.Clear (Key, 0, Key.Length);
				Key = null;
			}
		}

		public SymmetricKeyEncryptor CreateEncryptor ()
		{
			if (disposed)
				throw new ObjectDisposedException ("DES");

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
#else
	// Copyright (c) Microsoft Corporation.  All rights reserved.

	class DES : SymmetricAlgorithm
	{
		static KeySizes[] s_legalBlockSizes = {
			new KeySizes (64, 64, 0)
		};
		static KeySizes[] s_legalKeySizes = {
			new KeySizes (64, 64, 0)
		};

		//
		// protected constructors
		//

		DES ()
		{
			KeySizeValue = 64;
			BlockSizeValue = 64;
			FeedbackSizeValue = BlockSizeValue;
			LegalBlockSizesValue = s_legalBlockSizes;
			LegalKeySizesValue = s_legalKeySizes;
		}

		//
		// public properties
		//

		public override byte[] Key {
			get {
				if (KeyValue == null) {
					// Never hand back a weak or semi-weak key
					do {
						GenerateKey ();
					} while (IsWeakKey (KeyValue) || IsSemiWeakKey (KeyValue));
				}

				return (byte[]) KeyValue.Clone ();
			}
			set {
				if (value == null)
					throw new ArgumentNullException ("value");

				if (!ValidKeySize (value.Length * 8))
					throw new ArgumentException ("Invalid key size.");

				if (IsWeakKey (value))
					throw new CryptographicException ("Key is too weak.");

				if (IsSemiWeakKey (value))
					throw new CryptographicException ("Key is semi-weak.");

				KeyValue = (byte[]) value.Clone ();
				KeySizeValue = value.Length * 8;
			}
		}

		//
		// public methods
		//

		public static DES Create ()
		{
			return new DES ();
		}

		public static bool IsWeakKey (byte[] rgbKey)
		{
			if (!IsLegalKeySize (rgbKey))
				throw new CryptographicException ("Invalid key size.");

			byte[] rgbOddParityKey = FixupKeyParity (rgbKey);
			ulong key = QuadWordFromBigEndian (rgbOddParityKey);

			return ((key == 0x0101010101010101) ||
				(key == 0xfefefefefefefefe) ||
				(key == 0x1f1f1f1f0e0e0e0e) ||
				(key == 0xe0e0e0e0f1f1f1f1));
		}

		public static bool IsSemiWeakKey (byte[] rgbKey)
		{
			if (!IsLegalKeySize (rgbKey))
				throw new CryptographicException ("Invalid key size.");

			byte[] rgbOddParityKey = FixupKeyParity (rgbKey);
			ulong key = QuadWordFromBigEndian (rgbOddParityKey);

			return ((key == 0x01fe01fe01fe01fe) ||
				(key == 0xfe01fe01fe01fe01) ||
				(key == 0x1fe01fe00ef10ef1) ||
				(key == 0xe01fe01ff10ef10e) ||
				(key == 0x01e001e001f101f1) ||
				(key == 0xe001e001f101f101) ||
				(key == 0x1ffe1ffe0efe0efe) ||
				(key == 0xfe1ffe1ffe0efe0e) ||
				(key == 0x011f011f010e010e) ||
				(key == 0x1f011f010e010e01) ||
				(key == 0xe0fee0fef1fef1fe) ||
				(key == 0xfee0fee0fef1fef1));
		}

		//
		// private methods
		//

		static byte[] FixupKeyParity (byte[] key)
		{
			byte[] oddParityKey = new byte[key.Length];

			for (int index = 0; index < key.Length; index++) {
				// Get the bits we are interested in
				oddParityKey[index] = (byte) (key[index] & 0xfe);
				// Get the parity of the sum of the previous bits
				byte tmp1 = (byte) ((oddParityKey[index] & 0xF) ^ (oddParityKey[index] >> 4));
				byte tmp2 = (byte) ((tmp1 & 0x3) ^ (tmp1 >> 2));
				byte sumBitsMod2 = (byte) ((tmp2 & 0x1) ^ (tmp2 >> 1));
				// We need to set the last bit in oddParityKey[index] to the negation
				// of the last bit in sumBitsMod2
				if (sumBitsMod2 == 0)
					oddParityKey[index] |= 1;
			}

			return oddParityKey;
		}

		static bool IsLegalKeySize (byte[] rgbKey)
		{
			return rgbKey != null && rgbKey.Length == 8;
		}

		static ulong QuadWordFromBigEndian (byte[] block)
		{
			return (((ulong) block[0]) << 56) | (((ulong) block[1]) << 48) |
				(((ulong) block[2]) << 40) | (((ulong) block[3]) << 32) |
				(((ulong) block[4]) << 24) | (((ulong) block[5]) << 16) |
				(((ulong) block[6]) << 8) | ((ulong) block[7]));
		}
	}
#endif
}
