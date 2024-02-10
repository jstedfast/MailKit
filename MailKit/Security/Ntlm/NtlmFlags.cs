//
// NtlmFlags.cs
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

namespace MailKit.Security.Ntlm {
	/// <summary>
	/// The NTLM message header flags.
	/// </summary>
	/// <remarks>
	/// More details here: http://davenport.sourceforge.net/ntlm.html#theNtlmMessageHeaderLayout
	/// and at https://msdn.microsoft.com/en-us/library/cc236650.aspx
	/// </remarks>
	[Flags]
	enum NtlmFlags {
		/// <summary>
		/// Indicates that Unicode strings are supported for use in security buffer data.
		/// </summary>
		NegotiateUnicode = 0x00000001,

		/// <summary>
		/// Indicates that OEM strings are supported for use in security buffer data.
		/// </summary>
		NegotiateOem = 0x00000002,

		/// <summary>
		/// Requests that the server's authentication realm be included in the Type 2 message.
		/// </summary>
		RequestTarget = 0x00000004,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		R10 = 0x00000008,

		/// <summary>
		/// Specifies that authenticated communication between the client and server should carry a digital signature (message integrity).
		/// </summary>
		NegotiateSign = 0x00000010,

		/// <summary>
		/// Specifies that authenticated communication between the client and server should be encrypted (message confidentiality).
		/// </summary>
		NegotiateSeal = 0x00000020,

		/// <summary>
		/// Indicates that datagram authentication is being used.
		/// </summary>
		NegotiateDatagramStyle = 0x00000040,

		/// <summary>
		/// Indicates that the Lan Manager Session Key should be used for signing
		/// and sealing authenticated communications.
		/// </summary>
		NegotiateLanManagerKey = 0x00000080,

		/// <summary>
		/// This flag is unused and MUST be zero. (r8)
		/// </summary>
		R9 = 0x00000100,

		/// <summary>
		/// Indicates that NTLM authentication is being used.
		/// </summary>
		NegotiateNtlm = 0x00000200,

		/// <summary>
		/// This flag is unused and MUST be zero. (r8)
		/// </summary>
		R8 = 0x00000400,

		/// <summary>
		/// Sent by the client in the Type 3 message to indicate that an anonymous
		/// context has been established. This also affects the response fields.
		/// </summary>
		NegotiateAnonymous = 0x00000800,

		/// <summary>
		/// Sent by the client in the Type 1 message to indicate that the name of the
		/// domain in which the client workstation has membership is included in the
		/// message. This is used by the server to determine whether the client is
		/// eligible for local authentication.
		/// </summary>
		NegotiateDomainSupplied = 0x00001000,

		/// <summary>
		/// Sent by the client in the Type 1 message to indicate that the client
		/// workstation's name is included in the message. This is used by the server
		/// to determine whether the client is eligible for local authentication.
		/// </summary>
		NegotiateWorkstationSupplied = 0x00002000,

		/// <summary>
		/// Sent by the server to indicate that the server and client are on the same
		/// machine. Implies that the client may use the established local credentials
		/// for authentication instead of calculating a response to the challenge.
		/// </summary>
		NegotiateLocalCall = 0x00004000,
		R7 = NegotiateLocalCall,

		/// <summary>
		/// Indicates that authenticated communication between the client and server
		/// should be signed with a "dummy" signature.
		/// </summary>
		NegotiateAlwaysSign = 0x00008000,

		/// <summary>
		/// Sent by the server in the Type 2 message to indicate that the target
		/// authentication realm is a domain.
		/// </summary>
		TargetTypeDomain = 0x00010000,

		/// <summary>
		/// Sent by the server in the Type 2 message to indicate that the target
		/// authentication realm is a server.
		/// </summary>
		TargetTypeServer = 0x00020000,

		/// <summary>
		/// Sent by the server in the Type 2 message to indicate that the target
		/// authentication realm is a share. Presumably, this is for share-level
		/// authentication. Usage is unclear.
		/// </summary>
		TargetTypeShare = 0x00040000,
		R6 = TargetTypeShare,

		/// <summary>
		/// If set, requests usage of the NTLM v2 session security. NTLM v2 session
		/// security is a misnomer because it is not NTLM v2. It is NTLM v1 using the
		/// extended session security that is also in NTLM v2. NTLMSSP_NEGOTIATE_LM_KEY
		/// and NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY are mutually exclusive. If
		/// both NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY and NTLMSSP_NEGOTIATE_LM_KEY
		/// are requested, NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY alone MUST be
		/// returned to the client. NTLM v2 authentication session key generation MUST
		/// be supported by both the client and the DC in order to be used, and extended
		/// session security signing and sealing requires support from the client and
		/// the server in order to be used.
		/// </summary>
		NegotiateExtendedSessionSecurity = 0x00080000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		NegotiateIdentify = 0x00100000,

		/// <summary>
		/// This flag is unused and MUST be zero. (r5)
		/// </summary>
		R5 = 0x00200000,

		/// <summary>
		/// Indicates that the LMOWF function should be used to generate a session key.
		/// </summary>
		RequestNonNTSessionKey = 0x00400000,

		/// <summary>
		/// Sent by the server in the Type 2 message to indicate that it is including
		/// a Target Information block in the message. The Target Information block
		/// is used in the calculation of the NTLMv2 response.
		/// </summary>
		NegotiateTargetInfo = 0x00800000,

		/// <summary>
		/// This flag is unused and MUST be zero. (r4)
		/// </summary>
		R4 = 0x01000000,

		/// <summary>
		/// Indicates that the version field is present.
		/// </summary>
		NegotiateVersion = 0x02000000,

		/// <summary>
		/// This flag is unused and MUST be zero. (r3)
		/// </summary>
		R3 = 0x04000000,

		/// <summary>
		/// This flag is unused and MUST be zero. (r2)
		/// </summary>
		R2 = 0x08000000,

		/// <summary>
		/// This flag is unused and MUST be zero. (r1)
		/// </summary>
		R1 = 0x10000000,

		/// <summary>
		/// Indicates that 128-bit encryption is supported.
		/// </summary>
		Negotiate128 = 0x20000000,

		/// <summary>
		/// Indicates that the client will provide an encrypted master key in the
		/// "Session Key" field of the Type 3 message.
		/// </summary>
		NegotiateKeyExchange = 0x40000000,

		/// <summary>
		/// Indicates that 56-bit encryption is supported.
		/// </summary>
		Negotiate56 = (unchecked ((int) 0x80000000))
	}
}
