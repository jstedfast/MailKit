//
// Mono.Security.Protocol.Ntlm.MessageBase
//	abstract class for all NTLM messages
//
// Author:
//	Sebastien Pouliot  <sebastien@ximian.com>
//
// Copyright (C) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
//
// References
// a.	NTLM Authentication Scheme for HTTP, Ronald Tschalär
//	http://www.innovation.ch/java/ntlm.html
// b.	The NTLM Authentication Protocol, Copyright © 2003 Eric Glass
//	http://davenport.sourceforge.net/ntlm.html
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

namespace MailKit.Security.Ntlm {
	abstract class MessageBase
	{
		static readonly byte[] header = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00 };

		readonly int type;

		protected MessageBase (int messageType)
		{
			type = messageType;
		}
		
		public NtlmFlags Flags {
			get; set;
		}

		public int Type { 
			get { return type; }
		}

		protected byte[] PrepareMessage (int size)
		{
			var message = new byte[size];

			Buffer.BlockCopy (header, 0, message, 0, 8);
			
			message[ 8] = (byte) type;
			message[ 9] = (byte)(type >> 8);
			message[10] = (byte)(type >> 16);
			message[11] = (byte)(type >> 24);

			return message;
		}

		bool CheckHeader (byte[] message, int startIndex)
		{
			for (int i = 0; i < header.Length; i++) {
				if (message[startIndex + i] != header[i])
					return false;
			}

			return BitConverterLE.ToUInt32 (message, startIndex + 8) == type;
		}

		protected void ValidateArguments (byte[] message, int startIndex, int length)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (startIndex < 0 || startIndex > message.Length)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (length < 12 || length > (message.Length - startIndex))
				throw new ArgumentOutOfRangeException (nameof (length));

			if (!CheckHeader (message, startIndex))
				throw new ArgumentException (string.Format ("Invalid Type{0} message.", type), nameof (message));
		}

		public abstract byte[] Encode ();
	}
}
