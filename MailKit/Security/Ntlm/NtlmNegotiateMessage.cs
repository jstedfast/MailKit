//
// NtlmNegotiateMessage.cs
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

// NTLM specification: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-nlmp/b38c36ed-2804-4868-a9ff-8dd3182128e4
//
// NTLM registry key documentation: https://learn.microsoft.com/en-us/troubleshoot/windows-client/windows-security/enable-ntlm-2-authentication
// Note: Ideally, we'd default our flags based on the registry settings of the OS.

using System;
using System.Text;

namespace MailKit.Security.Ntlm {
	class NtlmNegotiateMessage : NtlmMessageBase
	{
		// System.Net.Mail seems to default to:           NtlmFlags.Negotiate56 | NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.NegotiateVersion | NtlmFlags.Negotiate128
		internal const NtlmFlags DefaultFlags = NtlmFlags.Negotiate56 | NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;

		byte[] cached;

		public NtlmNegotiateMessage (NtlmFlags flags, string domain, string workstation, Version osVersion = null) : base (1)
		{
			Flags = flags & ~(NtlmFlags.NegotiateDomainSupplied | NtlmFlags.NegotiateWorkstationSupplied | NtlmFlags.NegotiateVersion);

			// Note: If the NTLMSSP_NEGOTIATE_VERSION flag is set by the client application, the Version field
			// MUST be set to the current version (section 2.2.2.10), the DomainName field MUST be set to
			// a zero-length string, and the Workstation field MUST be set to a zero-length string.
			if (osVersion != null) {
				Flags |= NtlmFlags.NegotiateVersion;
				Workstation = string.Empty;
				Domain = string.Empty;
				OSVersion = osVersion;
			} else {
				if (!string.IsNullOrEmpty (workstation)) {
					Flags |= NtlmFlags.NegotiateWorkstationSupplied;
					Workstation = workstation.ToUpperInvariant ();
				} else {
					Workstation = string.Empty;
				}

				if (!string.IsNullOrEmpty (domain)) {
					Flags |= NtlmFlags.NegotiateDomainSupplied;
					Domain = domain.ToUpperInvariant ();
				} else {
					Domain = string.Empty;
				}
			}
		}

		public NtlmNegotiateMessage (string domain = null, string workstation = null, Version osVersion = null) : this (DefaultFlags, domain, workstation, osVersion)
		{
		}

		public NtlmNegotiateMessage (byte[] message, int startIndex, int length) : base (1)
		{
			Decode (message, startIndex, length);

			cached = new byte[length];
			Buffer.BlockCopy (message, startIndex, cached, 0, length);
		}

		public string Domain {
			get; private set;
		}

		public string Workstation {
			get; private set;
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			ValidateArguments (message, startIndex, length);

			Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 12);

			// decode the domain
			var domainLength = BitConverterLE.ToUInt16 (message, startIndex + 16);
			var domainOffset = BitConverterLE.ToUInt16 (message, startIndex + 20);
			Domain = Encoding.UTF8.GetString (message, startIndex + domainOffset, domainLength);

			// decode the workstation/host
			var workstationLength = BitConverterLE.ToUInt16 (message, startIndex + 24);
			var workstationOffset = BitConverterLE.ToUInt16 (message, startIndex + 28);
			Workstation = Encoding.UTF8.GetString (message, startIndex + workstationOffset, workstationLength);

			if ((Flags & NtlmFlags.NegotiateVersion) != 0 && length >= 40) {
				// decode the OS Version
				int major = message[startIndex + 32];
				int minor = message[startIndex + 33];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 34);

				OSVersion = new Version (major, minor, build);
			}
		}

		public override byte[] Encode ()
		{
			if (cached != null)
				return cached;

			var workstation = Encoding.UTF8.GetBytes (Workstation);
			var domain = Encoding.UTF8.GetBytes (Domain);
			const int versionLength = 8;
			int workstationOffset = 32 + versionLength;
			int domainOffset = workstationOffset + workstation.Length;

			var message = PrepareMessage (32 + domain.Length + workstation.Length + versionLength);

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

			message[24] = (byte) workstation.Length;
			message[25] = (byte)(workstation.Length >> 8);
			message[26] = message[24];
			message[27] = message[25];
			message[28] = (byte) workstationOffset;
			message[29] = (byte)(workstationOffset >> 8);

			if ((Flags & NtlmFlags.NegotiateVersion) != 0) {
				message[32] = (byte) OSVersion.Major;
				message[33] = (byte) OSVersion.Minor;
				message[34] = (byte) OSVersion.Build;
				message[35] = (byte)(OSVersion.Build >> 8);
				message[36] = 0x00;
				message[37] = 0x00;
				message[38] = 0x00;
				message[39] = 0x0f;
			}

			Buffer.BlockCopy (workstation, 0, message, workstationOffset, workstation.Length);
			Buffer.BlockCopy (domain, 0, message, domainOffset, domain.Length);

			cached = message;

			return message;
		}
	}
}
