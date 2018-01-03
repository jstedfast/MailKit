//
// Mono.Security.Protocol.Ntlm.Type1Message - Negotiation
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell, Inc (http://www.novell.com)
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
		internal static readonly NtlmFlags DefaultFlags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateOem | NtlmFlags.NegotiateUnicode | NtlmFlags.RequestTarget;

		string domain;
		string host;

		public Type1Message (string hostName, string domainName) : base (1)
		{
			Flags = DefaultFlags;
			Domain = domainName;
			Host = hostName;
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

		public Version OSVersion {
			get; set;
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			int offset, count;

			ValidateArguments (message, startIndex, length);

			Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 12);

			// decode the domain
			count = BitConverterLE.ToUInt16 (message, startIndex + 16);
			offset = BitConverterLE.ToUInt16 (message, startIndex + 20);
			domain = Encoding.UTF8.GetString (message, startIndex + offset, count);

			// decode the workstation/host
			count = BitConverterLE.ToUInt16 (message, startIndex + 24);
			offset = BitConverterLE.ToUInt16 (message, startIndex + 28);
			host = Encoding.UTF8.GetString (message, startIndex + offset, count);

			if (offset == 40) {
				// decode the OS Version
				int major = message[startIndex + 32];
				int minor = message[startIndex + 33];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 34);

				OSVersion = new Version (major, minor, build);
			}
		}

		public override byte[] Encode ()
		{
			int versionLength = OSVersion != null ? 8 : 0;
			int hostOffset = 32 + versionLength;
			int domainOffset = hostOffset + host.Length;

			var message = PrepareMessage (32 + domain.Length + host.Length + versionLength);

			message[12] = (byte) Flags;
			message[13] = (byte)((uint) Flags >> 8);
			message[14] = (byte)((uint) Flags >> 16);
			message[15] = (byte)((uint) Flags >> 24);

			message[16] = (byte) domain.Length;
			message[17] = (byte)(domain.Length >> 8);
			message[18] = message[16];
			message[19] = message[17];
			message[20] = (byte) domainOffset;
			message[21] = (byte)(domainOffset >> 8);

			message[24] = (byte) host.Length;
			message[25] = (byte)(host.Length >> 8);
			message[26] = message[24];
			message[27] = message[25];
			message[28] = (byte) hostOffset;
			message[29] = (byte)(hostOffset >> 8);

			if (OSVersion != null) {
				message[32] = (byte) OSVersion.Major;
				message[33] = (byte) OSVersion.Minor;
				message[34] = (byte)(OSVersion.Build);
				message[35] = (byte)(OSVersion.Build >> 8);
				message[36] = 0x00;
				message[37] = 0x00;
				message[38] = 0x00;
				message[39] = 0x0f;
			}

			var hostName = Encoding.UTF8.GetBytes (host.ToUpperInvariant ());
			Buffer.BlockCopy (hostName, 0, message, hostOffset, hostName.Length);

			var domainName = Encoding.UTF8.GetBytes (domain.ToUpperInvariant ());
			Buffer.BlockCopy (domainName, 0, message, domainOffset, domainName.Length);

			return message;
		}
	}
}
