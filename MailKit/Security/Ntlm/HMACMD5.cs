//
// HMACMD5.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2017 Xamarin Inc. (www.xamarin.com)
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
using System.IO;

#if NETFX_CORE
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
#else
using System.Security.Cryptography;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
#endif

namespace MailKit.Security.Ntlm
{
#if NETFX_CORE
	class HMACMD5 : IDisposable
	{
		MacAlgorithmProvider algorithm;
		CryptographicHash hash;
		byte[] hashValue, key;
		bool disposed;

		public HMACMD5 (byte[] key)
		{
			Key = key;
		}

		~HMACMD5 ()
		{
			Dispose (false);
		}

		public byte[] Hash {
			get {
				if (hashValue == null)
					throw new InvalidOperationException ("No hash value computed.");

				return hashValue;
			}
		}

		public byte[] Key {
			get { return key; }
			set {
				if (value == null)
					throw new ArgumentNullException (nameof (value));

				if (key != null)
					Array.Clear (key, 0, key.Length);

				key = value;
				Initialize ();
			}
		}

		void HashCore (byte[] block, int offset, int size)
		{
			var array = new byte[size];

			Buffer.BlockCopy (block, offset, array, 0, size);

			var buffer = CryptographicBuffer.CreateFromByteArray (array);
			hash.Append (buffer);
		}

		byte[] HashFinal ()
		{
			var buffer = hash.GetValueAndReset ();
			byte[] value;

			CryptographicBuffer.CopyToByteArray (buffer, out value);

			return value;
		}

		public void Initialize ()
		{
			var buffer = CryptographicBuffer.CreateFromByteArray (Key);

			algorithm = MacAlgorithmProvider.OpenAlgorithm (MacAlgorithmNames.HmacMd5);
			hash = algorithm.CreateHash (buffer);
		}

		public void Clear ()
		{
			Dispose (false);
		}

		public byte[] ComputeHash (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0 || offset > buffer.Length - count)
				throw new ArgumentOutOfRangeException (nameof (count));

			if (disposed)
				throw new ObjectDisposedException ("HashAlgorithm");

			HashCore (buffer, offset, count);
			hashValue = HashFinal ();

			return hashValue;
		}

		public byte[] ComputeHash (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			return ComputeHash (buffer, 0, buffer.Length);
		}

		public byte[] ComputeHash (Stream inputStream)
		{
			// don't read stream unless object is ready to use
			if (disposed)
				throw new ObjectDisposedException ("HashAlgorithm");

			var buffer = new byte[4096];
			int nread;

			do {
				if ((nread = inputStream.Read (buffer, 0, buffer.Length)) > 0)
					HashCore (buffer, 0, nread);
			} while (nread > 0);

			hashValue = HashFinal ();

			return hashValue;
		}

		public int TransformBlock (byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			if (inputBuffer == null)
				throw new ArgumentNullException (nameof (inputBuffer));

			if (inputOffset < 0 || inputOffset > inputBuffer.Length)
				throw new ArgumentOutOfRangeException (nameof (inputOffset));

			if (inputCount < 0 || inputOffset > inputBuffer.Length - inputCount)
				throw new ArgumentOutOfRangeException (nameof (inputCount));

			if (outputBuffer != null) {
				if (outputOffset < 0 || outputOffset > outputBuffer.Length - inputCount)
					throw new ArgumentOutOfRangeException (nameof (outputOffset));
			}

			HashCore (inputBuffer, inputOffset, inputCount);

			if (outputBuffer != null)
				Buffer.BlockCopy (inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);

			return inputCount;
		}

		public byte[] TransformFinalBlock (byte[] inputBuffer, int inputOffset, int inputCount)
		{
			if (inputCount < 0)
				throw new ArgumentOutOfRangeException (nameof (inputCount));

			var outputBuffer = new byte[inputCount];

			// note: other exceptions are handled by Buffer.BlockCopy
			Buffer.BlockCopy (inputBuffer, inputOffset, outputBuffer, 0, inputCount);

			HashCore (inputBuffer, inputOffset, inputCount);
			hashValue = HashFinal ();

			return outputBuffer;
		}

		void Dispose (bool disposing)
		{
			if (key != null) {
				Array.Clear (key, 0, Key.Length);
				key = null;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
			disposed = true;
		}
	}
#else
	class HMACMD5 : IDisposable
	{
		readonly HMac hash = new HMac (new MD5Digest ());
		byte[] hashValue, key;
		bool disposed;

		public HMACMD5 (byte[] key)
		{
			Key = key;
		}

		~HMACMD5 ()
		{
			Dispose (false);
		}

		public byte[] Hash
		{
			get {
				if (hashValue == null)
					throw new InvalidOperationException ("No hash value computed.");

				return hashValue;
			}
		}

		public byte[] Key {
			get { return key; }
			set {
				if (value == null)
					throw new ArgumentNullException (nameof (value));

				if (key != null)
					Array.Clear (key, 0, key.Length);

				key = value;
				Initialize ();
			}
		}

		void HashCore (byte[] block, int offset, int size)
		{
			hash.BlockUpdate (block, offset, size);
		}

		byte[] HashFinal ()
		{
			var value = new byte[hash.GetMacSize ()];

			hash.DoFinal (value, 0);
			hash.Reset ();

			return value;
		}

		public void Initialize ()
		{
			hash.Init (new KeyParameter (Key));
		}

		public void Clear ()
		{
			Dispose (false);
		}

		public byte[] ComputeHash (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0 || offset > buffer.Length - count)
				throw new ArgumentOutOfRangeException (nameof (count));

			if (disposed)
				throw new ObjectDisposedException ("HashAlgorithm");

			HashCore (buffer, offset, count);
			hashValue = HashFinal ();

			return hashValue;
		}

		public byte[] ComputeHash (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			return ComputeHash (buffer, 0, buffer.Length);
		}

		public byte[] ComputeHash (Stream inputStream)
		{
			// don't read stream unless object is ready to use
			if (disposed)
				throw new ObjectDisposedException ("HashAlgorithm");

			var buffer = new byte[4096];
			int nread;

			do {
				if ((nread = inputStream.Read (buffer, 0, buffer.Length)) > 0)
					HashCore (buffer, 0, nread);
			} while (nread > 0);

			hashValue = HashFinal ();

			return hashValue;
		}

		public int TransformBlock (byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			if (inputBuffer == null)
				throw new ArgumentNullException (nameof (inputBuffer));

			if (inputOffset < 0 || inputOffset > inputBuffer.Length)
				throw new ArgumentOutOfRangeException (nameof (inputOffset));

			if (inputCount < 0 || inputOffset > inputBuffer.Length - inputCount)
				throw new ArgumentOutOfRangeException (nameof (inputCount));

			if (outputBuffer != null) {
				if (outputOffset < 0 || outputOffset > outputBuffer.Length - inputCount)
					throw new ArgumentOutOfRangeException (nameof (outputOffset));
			}

			HashCore (inputBuffer, inputOffset, inputCount);

			if (outputBuffer != null)
				Buffer.BlockCopy (inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);

			return inputCount;
		}

		public byte[] TransformFinalBlock (byte[] inputBuffer, int inputOffset, int inputCount)
		{
			if (inputCount < 0)
				throw new ArgumentOutOfRangeException (nameof (inputCount));

			var outputBuffer = new byte[inputCount];

			// note: other exceptions are handled by Buffer.BlockCopy
			Buffer.BlockCopy (inputBuffer, inputOffset, outputBuffer, 0, inputCount);

			HashCore (inputBuffer, inputOffset, inputCount);
			hashValue = HashFinal ();

			return outputBuffer;
		}

		void Dispose (bool disposing)
		{
			if (key != null) {
				Array.Clear (key, 0, Key.Length);
				key = null;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
			disposed = true;
		}
	}
#endif
}
