//
// ARC4Managed.cs: Alleged RC4(tm) compatible symmetric stream cipher
//	RC4 is a trademark of RSA Security
//

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
using System.Security.Cryptography;

namespace MailKit.Security.Ntlm {
	// References:
	// a.	Usenet 1994 - RC4 Algorithm revealed
	//	http://www.qrst.de/html/dsds/rc4.htm

	class RC4 : SymmetricAlgorithm, ICryptoTransform
	{
		byte[] key, state;
		byte x, y;
		bool disposed;

		public RC4 () : base ()
		{
			state = new byte[256];
			KeySizeValue = 64;
		}

		~RC4 ()
		{
			Dispose (false);
		}

		public bool CanReuseTransform {
			get { return false; }
		}

		public bool CanTransformMultipleBlocks {
			get { return true; }
		}

		public int InputBlockSize {
			get { return 1; }
		}

		public int OutputBlockSize {
			get { return 1; }
		}

		public override byte[] Key {
			get {
				if (key == null)
					throw new InvalidOperationException ();

				return (byte[]) key.Clone ();
			}
			set {
				if (value == null)
					throw new ArgumentNullException (nameof (value));

				if (value.Length == 0)
					throw new ArgumentException ("Invalid key length.", nameof (value));

				KeySizeValue = value.Length << 3;
				key = (byte[]) value.Clone ();
				KeySetup (key);
			}
		}

		public override ICryptoTransform CreateEncryptor (byte[] rgbKey, byte[] rgvIV)
		{
			return new RC4 { Key = rgbKey };
		}

		public override ICryptoTransform CreateDecryptor (byte[] rgbKey, byte[] rgvIV)
		{
			return new RC4 { Key = rgbKey };
		}

		public override void GenerateIV ()
		{
			// not used for a stream cipher
			IV = new byte[0];
		}

		public override void GenerateKey ()
		{
			key = new byte[KeySizeValue >> 3];
			RandomNumberGenerator.Create ().GetBytes (key);
			KeySetup (key);
		}

		void KeySetup (byte[] key)
		{
			byte index1 = 0;
			byte index2 = 0;

			for (int counter = 0; counter < 256; counter++)
				state[counter] = (byte) counter;

			x = y = 0;

			for (int counter = 0; counter < 256; counter++) {
				index2 = (byte) (key[index1] + state[counter] + index2);
				// swap byte
				byte tmp = state[counter];
				state[counter] = state[index2];
				state[index2] = tmp;
				index1 = (byte) ((index1 + 1) % key.Length);
			}
		}

		void CheckInput (byte[] inputBuffer, int inputOffset, int inputCount)
		{
			if (inputBuffer == null)
				throw new ArgumentNullException (nameof (inputBuffer));

			if (inputOffset < 0 || inputOffset > inputBuffer.Length)
				throw new ArgumentOutOfRangeException (nameof (inputOffset));

			if (inputCount < 0 || inputOffset > inputBuffer.Length - inputCount)
				throw new ArgumentOutOfRangeException (nameof (inputCount));
		}

		public int TransformBlock (byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			CheckInput (inputBuffer, inputOffset, inputCount);

			// check output parameters
			if (outputBuffer == null)
				throw new ArgumentNullException (nameof (outputBuffer));

			if (outputOffset < 0 || outputOffset > outputBuffer.Length - inputCount)
				throw new ArgumentOutOfRangeException (nameof (outputOffset));

			return InternalTransformBlock (inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
		}

		int InternalTransformBlock (byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			byte xorIndex;

			for (int counter = 0; counter < inputCount; counter++) {
				x = (byte) (x + 1);
				y = (byte) (state[x] + y);

				// swap byte
				byte tmp = state[x];
				state[x] = state[y];
				state[y] = tmp;

				xorIndex = (byte) (state[x] + state[y]);
				outputBuffer[outputOffset + counter] = (byte) (inputBuffer[inputOffset + counter] ^ state[xorIndex]);
			}
			return inputCount;
		}

		public byte[] TransformFinalBlock (byte[] inputBuffer, int inputOffset, int inputCount)
		{
			CheckInput (inputBuffer, inputOffset, inputCount);

			var output = new byte[inputCount];
			InternalTransformBlock (inputBuffer, inputOffset, inputCount, output, 0);
			return output;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposed)
				return;

			x = y = 0;

			if (key != null)
				Array.Clear (key, 0, key.Length);

			Array.Clear (state, 0, state.Length);

			if (disposing) {
				state = null;
				key = null;
			}

			disposed = true;
		}
	}
}
