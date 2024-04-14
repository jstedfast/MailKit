//
// NtlmMessageBase.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-nlmp/b38c36ed-2804-4868-a9ff-8dd3182128e4

using System;
using System.Globalization;

namespace MailKit.Security.Ntlm {
	abstract class NtlmMessageBase
	{
		static readonly byte[] Signature = { (byte) 'N', (byte) 'T', (byte) 'L', (byte) 'M', (byte) 'S', (byte) 'S', (byte) 'P', 0x00 };

		protected NtlmMessageBase (int type)
		{
			Type = type;
		}

		public NtlmFlags Flags {
			get; protected set;
		}

		public Version OSVersion {
			get; protected set;
		}

		public int Type {
			get; private set;
		}

		protected byte[] PrepareMessage (int size)
		{
			var message = new byte[size];

			Buffer.BlockCopy (Signature, 0, message, 0, 8);

			message[ 8] = (byte) Type;
			message[ 9] = (byte)(Type >> 8);
			message[10] = (byte)(Type >> 16);
			message[11] = (byte)(Type >> 24);

			return message;
		}

		bool CheckSignature (byte[] message, int startIndex)
		{
			for (int i = 0; i < Signature.Length; i++) {
				if (message[startIndex + i] != Signature[i])
					return false;
			}

			return BitConverterLE.ToUInt32 (message, startIndex + 8) == Type;
		}

		protected void ValidateArguments (byte[] message, int startIndex, int length)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (startIndex < 0 || startIndex > message.Length)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (length < 12 || length > (message.Length - startIndex))
				throw new ArgumentOutOfRangeException (nameof (length));

			if (!CheckSignature (message, startIndex))
				throw new ArgumentException (string.Format (CultureInfo.InvariantCulture, "Invalid Type{0} message.", Type), nameof (message));

			var messageType = BitConverterLE.ToUInt32 (message, 8);
			if (messageType != Type)
				throw new ArgumentException (string.Format (CultureInfo.InvariantCulture, "Invalid Type{0} message.", Type), nameof (message));
		}

		public abstract byte[] Encode ();
	}
}
