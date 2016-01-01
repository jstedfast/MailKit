//
// NtlmFlags.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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

namespace MailKit.Security.Ntlm {
	/// <summary>
	/// The NTLM message header flags.
	/// </summary>
	/// <remarks>
	/// More details here: http://davenport.sourceforge.net/ntlm.html#theNtlmMessageHeaderLayout
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
		UnknownFlag1 = 0x00000008,

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
		/// This flag's usage has not been identified.
		/// </summary>
		NegotiateNetware = 0x00000100,

		/// <summary>
		/// Indicates that NTLM authentication is being used.
		/// </summary>
		NegotiateNtlm = 0x00000200,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag2 = 0x00000400,

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

		/// <summary>
		/// Indicates that the NTLM2 signing and sealing scheme should be used for
		/// protecting authenticated communications. Note that this refers to a
		/// particular session security scheme, and is not related to the use of
		/// NTLMv2 authentication. This flag can, however, have an effect on the
		/// response calculations.
		/// </summary>
		NegotiateNtlm2Key = 0x00080000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		RequestInitResponse = 0x00100000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		RequestAcceptResponse = 0x00200000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		RequestNonNTSessionKey = 0x00400000,

		/// <summary>
		/// Sent by the server in the Type 2 message to indicate that it is including
		/// a Target Information block in the message. The Target Information block
		/// is used in the calculation of the NTLMv2 response.
		/// </summary>
		NegotiateTargetInfo = 0x00800000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag3 = 0x01000000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag4 = 0x02000000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag5 = 0x04000000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag6 = 0x08000000,

		/// <summary>
		/// This flag's usage has not been identified.
		/// </summary>
		UnknownFlag7 = 0x10000000,

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
