//
// Mono.Security.Protocol.Ntlm.Type1Message - Negotiation
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell, Inc (http://www.novell.com)
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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
using System.Text;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Security.Ntlm {
	class Type1Message : MessageBase
	{
		string domain;
		string host;

		public Type1Message (string hostName, string domainName) : base (1)
		{
			Flags = (NtlmFlags) 0xb207;
			domain = domainName;
			host = hostName;
		}

		public Type1Message (byte[] message, int startIndex, int length) : base (1)
		{
			Decode (message, startIndex, length);
		}

		public string Domain {
			get { return domain; }
			set {
				if (string.IsNullOrEmpty (value)) {
					Flags &= ~NtlmFlags.NegotiateDomainSupplied;
					value = string.Empty;
				} else {
					Flags |= NtlmFlags.NegotiateDomainSupplied;
				}

				domain = value;
			}
		}

		public string Host {
			get { return host; }
			set {
				if (string.IsNullOrEmpty (value)) {
					Flags &= ~NtlmFlags.NegotiateWorkstationSupplied;
					value = string.Empty;
				} else {
					Flags |= NtlmFlags.NegotiateWorkstationSupplied;
				}

				host = value;
			}
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			ValidateArguments (message, startIndex, length);

			Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 12);

			int count = BitConverterLE.ToUInt16 (message, startIndex + 16);
			int offset = BitConverterLE.ToUInt16 (message, startIndex + 20);
			domain = Encoding.ASCII.GetString (message, startIndex + offset, count);

			count = BitConverterLE.ToUInt16 (message, startIndex + 24);
			host = Encoding.ASCII.GetString (message, startIndex + 32, count);
		}

		public override byte[] GetBytes ()
		{
			byte[] data = PrepareMessage (32 + domain.Length + host.Length);

			data[12] = (byte) Flags;
			data[13] = (byte)((uint) Flags >> 8);
			data[14] = (byte)((uint) Flags >> 16);
			data[15] = (byte)((uint) Flags >> 24);

			short offset = (short)(32 + host.Length);

			data[16] = (byte) domain.Length;
			data[17] = (byte)(domain.Length >> 8);
			data[18] = data[16];
			data[19] = data[17];
			data[20] = (byte) offset;
			data[21] = (byte)(offset >> 8);

			data[24] = (byte) host.Length;
			data[25] = (byte)(host.Length >> 8);
			data[26] = data[24];
			data[27] = data[25];
			data[28] = 0x20;
			data[29] = 0x00;

			byte[] hostName = Encoding.ASCII.GetBytes (host.ToUpperInvariant ());
			Buffer.BlockCopy (hostName, 0, data, 32, hostName.Length);

			byte[] domainName = Encoding.ASCII.GetBytes (domain.ToUpperInvariant ());
			Buffer.BlockCopy (domainName, 0, data, offset, domainName.Length);

			return data;
		}
	}
}
