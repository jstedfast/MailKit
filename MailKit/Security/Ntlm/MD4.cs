//
// MD4.cs
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004-2005, 2010 Novell, Inc (http://www.novell.com)
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;

namespace MailKit.Security.Ntlm {
	sealed class MD4 : IDisposable
	{
		const int S11 = 3;
		const int S12 = 7;
		const int S13 = 11;
		const int S14 = 19;
		const int S21 = 3;
		const int S22 = 5;
		const int S23 = 9;
		const int S24 = 13;
		const int S31 = 3;
		const int S32 = 9;
		const int S33 = 11;
		const int S34 = 15;

		bool disposed;
		byte[] hashValue;
		byte[] buffered;
		uint[] state;
		uint[] count;
		uint[] x;

		public MD4 ()
		{
			// we allocate the context memory
			buffered = new byte[64];
			state = new uint[4];
			count = new uint[2];

			// temporary buffer in MD4Transform that we don't want to allocate on each iteration
			x = new uint[16];

			// the initialize our context
			Initialize ();
		}

		~MD4 ()
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

		void HashCore (byte[] block, int offset, int size)
		{
			// Compute number of bytes mod 64
			int index = (int) ((count[0] >> 3) & 0x3F);

			// Update number of bits
			count[0] += (uint) (size << 3);
			if (count[0] < (size << 3))
				count[1]++;

			count[1] += (uint) (size >> 29);

			int partLen = 64 - index;
			int i = 0;

			// Transform as many times as possible.
			if (size >= partLen) {
				Buffer.BlockCopy (block, offset, buffered, index, partLen);
				MD4Transform (buffered, 0);

				for (i = partLen; i + 63 < size; i += 64)
					MD4Transform (block, offset + i);

				index = 0;
			}

			// Buffer remaining input
			Buffer.BlockCopy (block, offset + i, buffered, index, size - i);
		}

		byte[] HashFinal ()
		{
			// Save number of bits
			var bits = new byte[8];
			Encode (bits, count);

			// Pad out to 56 mod 64.
			uint index = ((count [0] >> 3) & 0x3f);
			int padLen = (int) ((index < 56) ? (56 - index) : (120 - index));
			HashCore (Padding (padLen), 0, padLen);

			// Append length (before padding)
			HashCore (bits, 0, 8);

			// Store state in digest
			var digest = new byte[16];
			Encode (digest, state);

			return digest;
		}

		public void Initialize ()
		{
			count[0] = 0;
			count[1] = 0;
			state[0] = 0x67452301;
			state[1] = 0xefcdab89;
			state[2] = 0x98badcfe;
			state[3] = 0x10325476;

			// Clear sensitive information
			Array.Clear (buffered, 0, 64);
			Array.Clear (x, 0, 16);
		}

		static byte[] Padding (int length)
		{
			if (length > 0) {
				var padding = new byte[length];
				padding[0] = 0x80;
				return padding;
			}

			return null;
		}

		// F, G and H are basic MD4 functions.
		static uint F (uint x, uint y, uint z)
		{
			return (x & y) | (~x & z);
		}

		static uint G (uint x, uint y, uint z)
		{
			return (x & y) | (x & z) | (y & z);
		}

		static uint H (uint x, uint y, uint z)
		{
			return x ^ y ^ z;
		}

		// ROTATE_LEFT rotates x left n bits.
		static uint ROL (uint x, byte n)
		{
			return (x << n) | (x >> (32 - n));
		}

		/* FF, GG and HH are transformations for rounds 1, 2 and 3 */
		/* Rotation is separate from addition to prevent recomputation */
		static void FF (ref uint a, uint b, uint c, uint d, uint x, byte s)
		{
			a += F (b, c, d) + x;
			a = ROL (a, s);
		}

		static void GG (ref uint a, uint b, uint c, uint d, uint x, byte s)
		{
			a += G (b, c, d) + x + 0x5a827999;
			a = ROL (a, s);
		}

		static void HH (ref uint a, uint b, uint c, uint d, uint x, byte s)
		{
			a += H (b, c, d) + x + 0x6ed9eba1;
			a = ROL (a, s);
		}

		static void Encode (byte[] output, uint[] input)
		{
			for (int i = 0, j = 0; j < output.Length; i++, j += 4) {
				output[j + 0] = (byte) (input[i]);
				output[j + 1] = (byte) (input[i] >> 8);
				output[j + 2] = (byte) (input[i] >> 16);
				output[j + 3] = (byte) (input[i] >> 24);
			}
		}

		static void Decode (uint[] output, byte[] input, int index)
		{
			for (int i = 0, j = index; i < output.Length; i++, j += 4)
				output[i] = (uint) ((input[j]) | (input[j + 1] << 8) | (input[j + 2] << 16) | (input[j + 3] << 24));
		}

		void MD4Transform (byte[] block, int index)
		{
			uint a = state[0];
			uint b = state[1];
			uint c = state[2];
			uint d = state[3];

			Decode (x, block, index);

			/* Round 1 */
			FF (ref a, b, c, d, x[ 0], S11); /* 1 */
			FF (ref d, a, b, c, x[ 1], S12); /* 2 */
			FF (ref c, d, a, b, x[ 2], S13); /* 3 */
			FF (ref b, c, d, a, x[ 3], S14); /* 4 */
			FF (ref a, b, c, d, x[ 4], S11); /* 5 */
			FF (ref d, a, b, c, x[ 5], S12); /* 6 */
			FF (ref c, d, a, b, x[ 6], S13); /* 7 */
			FF (ref b, c, d, a, x[ 7], S14); /* 8 */
			FF (ref a, b, c, d, x[ 8], S11); /* 9 */
			FF (ref d, a, b, c, x[ 9], S12); /* 10 */
			FF (ref c, d, a, b, x[10], S13); /* 11 */
			FF (ref b, c, d, a, x[11], S14); /* 12 */
			FF (ref a, b, c, d, x[12], S11); /* 13 */
			FF (ref d, a, b, c, x[13], S12); /* 14 */
			FF (ref c, d, a, b, x[14], S13); /* 15 */
			FF (ref b, c, d, a, x[15], S14); /* 16 */

			/* Round 2 */
			GG (ref a, b, c, d, x[ 0], S21); /* 17 */
			GG (ref d, a, b, c, x[ 4], S22); /* 18 */
			GG (ref c, d, a, b, x[ 8], S23); /* 19 */
			GG (ref b, c, d, a, x[12], S24); /* 20 */
			GG (ref a, b, c, d, x[ 1], S21); /* 21 */
			GG (ref d, a, b, c, x[ 5], S22); /* 22 */
			GG (ref c, d, a, b, x[ 9], S23); /* 23 */
			GG (ref b, c, d, a, x[13], S24); /* 24 */
			GG (ref a, b, c, d, x[ 2], S21); /* 25 */
			GG (ref d, a, b, c, x[ 6], S22); /* 26 */
			GG (ref c, d, a, b, x[10], S23); /* 27 */
			GG (ref b, c, d, a, x[14], S24); /* 28 */
			GG (ref a, b, c, d, x[ 3], S21); /* 29 */
			GG (ref d, a, b, c, x[ 7], S22); /* 30 */
			GG (ref c, d, a, b, x[11], S23); /* 31 */
			GG (ref b, c, d, a, x[15], S24); /* 32 */

			HH (ref a, b, c, d, x[ 0], S31); /* 33 */
			HH (ref d, a, b, c, x[ 8], S32); /* 34 */
			HH (ref c, d, a, b, x[ 4], S33); /* 35 */
			HH (ref b, c, d, a, x[12], S34); /* 36 */
			HH (ref a, b, c, d, x[ 2], S31); /* 37 */
			HH (ref d, a, b, c, x[10], S32); /* 38 */
			HH (ref c, d, a, b, x[ 6], S33); /* 39 */
			HH (ref b, c, d, a, x[14], S34); /* 40 */
			HH (ref a, b, c, d, x[ 1], S31); /* 41 */
			HH (ref d, a, b, c, x[ 9], S32); /* 42 */
			HH (ref c, d, a, b, x[ 5], S33); /* 43 */
			HH (ref b, c, d, a, x[13], S34); /* 44 */
			HH (ref a, b, c, d, x[ 3], S31); /* 45 */
			HH (ref d, a, b, c, x[11], S32); /* 46 */
			HH (ref c, d, a, b, x[ 7], S33); /* 47 */
			HH (ref b, c, d, a, x[15], S34); /* 48 */

			state [0] += a;
			state [1] += b;
			state [2] += c;
			state [3] += d;
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
				throw new ObjectDisposedException (nameof (MD4));

			HashCore (buffer, offset, count);
			hashValue = HashFinal ();
			Initialize ();

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
				throw new ObjectDisposedException (nameof (MD4));

			var buffer = new byte[4096];
			int nread;

			do {
				if ((nread = inputStream.Read (buffer, 0, buffer.Length)) > 0)
					HashCore (buffer, 0, nread);
			} while (nread > 0);

			hashValue = HashFinal ();
			Initialize ();

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
			Initialize ();

			return outputBuffer;
		}

		void Dispose (bool disposing)
		{
			if (buffered != null) {
				Array.Clear (buffered, 0, buffered.Length);
				buffered = null;
			}

			if (state != null) {
				Array.Clear (state, 0, state.Length);
				state = null;
			}

			if (count != null) {
				Array.Clear (count, 0, count.Length);
				count = null;
			}

			if (x != null) {
				Array.Clear (x, 0, x.Length);
				x = null;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
			disposed = true;
		}
	}
}
