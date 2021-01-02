//
// Mono.Security.Protocol.Ntlm.Type3Message - Authentication
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell, Inc (http://www.novell.com)
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

namespace MailKit.Security.Ntlm {
	class Type3Message : MessageBase
	{
		readonly Type2Message type2;
		readonly byte[] challenge;

		public Type3Message (byte[] message, int startIndex, int length) : base (3)
		{
			Decode (message, startIndex, length);
			type2 = null;
		}

		public Type3Message (Type2Message type2, Version osVersion, NtlmAuthLevel level, string userName, string password, string host) : base (3)
		{
			this.type2 = type2;

			challenge = type2.Nonce;
			Domain = type2.TargetName;
			OSVersion = osVersion;
			Username = userName;
			Password = password;
			Level = level;
			Host = host;
			Flags = 0;

			if (osVersion != null)
				Flags |= NtlmFlags.NegotiateVersion;

			if ((type2.Flags & NtlmFlags.NegotiateUnicode) != 0)
				Flags |= NtlmFlags.NegotiateUnicode;
			else
				Flags |= NtlmFlags.NegotiateOem;

			if ((type2.Flags & NtlmFlags.NegotiateNtlm) != 0)
				Flags |= NtlmFlags.NegotiateNtlm;

			if ((type2.Flags & NtlmFlags.NegotiateNtlm2Key) != 0)
				Flags |= NtlmFlags.NegotiateNtlm2Key;

			if ((type2.Flags & NtlmFlags.NegotiateTargetInfo) != 0)
				Flags |= NtlmFlags.NegotiateTargetInfo;

			if ((type2.Flags & NtlmFlags.RequestTarget) != 0)
				Flags |= NtlmFlags.RequestTarget;
		}

		~Type3Message ()
		{
			if (challenge != null)
				Array.Clear (challenge, 0, challenge.Length);

			if (LM != null)
				Array.Clear (LM, 0, LM.Length);

			if (NT != null)
				Array.Clear (NT, 0, NT.Length);
		}

		public NtlmAuthLevel Level {
			get; set;
		}

		public string Domain {
			get; set;
		}

		public string Host {
			get; set;
		}

		public string Password {
			get; set;
		}

		public string Username {
			get; set;
		}

		public byte[] LM {
			get; private set;
		}

		public byte[] NT {
			get; set;
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			ValidateArguments (message, startIndex, length);

			Password = null;

			if (message.Length >= 64)
				Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 60);
			else
				Flags = (NtlmFlags) 0x8201;

			int lmLength = BitConverterLE.ToUInt16 (message, startIndex + 12);
			int lmOffset = BitConverterLE.ToUInt16 (message, startIndex + 16);
			LM = new byte[lmLength];
			Buffer.BlockCopy (message, startIndex + lmOffset, LM, 0, lmLength);

			int ntLength = BitConverterLE.ToUInt16 (message, startIndex + 20);
			int ntOffset = BitConverterLE.ToUInt16 (message, startIndex + 24);
			NT = new byte[ntLength];
			Buffer.BlockCopy (message, startIndex + ntOffset, NT, 0, ntLength);

			int domainLength = BitConverterLE.ToUInt16 (message, startIndex + 28);
			int domainOffset = BitConverterLE.ToUInt16 (message, startIndex + 32);
			Domain = DecodeString (message, startIndex + domainOffset, domainLength);

			int userLength = BitConverterLE.ToUInt16 (message, startIndex + 36);
			int userOffset = BitConverterLE.ToUInt16 (message, startIndex + 40);
			Username = DecodeString (message, startIndex + userOffset, userLength);

			int hostLength = BitConverterLE.ToUInt16 (message, startIndex + 44);
			int hostOffset = BitConverterLE.ToUInt16 (message, startIndex + 48);
			Host = DecodeString (message, startIndex + hostOffset, hostLength);

			// Session key.  We don't use it yet.
			//int skeyLength = BitConverterLE.ToUInt16 (message, startIndex + 52);
			//int skeyOffset = BitConverterLE.ToUInt16 (message, startIndex + 56);

			// OSVersion
			if ((Flags & NtlmFlags.NegotiateVersion) != 0 && length >= 72) {
				// decode the OS Version
				int major = message[startIndex + 64];
				int minor = message[startIndex + 65];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 66);

				OSVersion = new Version (major, minor, build);
			}
		}

		string DecodeString (byte[] buffer, int offset, int len)
		{
			var encoding = (Flags & NtlmFlags.NegotiateUnicode) != 0 ? Encoding.Unicode : Encoding.UTF8;

			return encoding.GetString (buffer, offset, len);
		}

		byte[] EncodeString (string text)
		{
			if (text == null)
				return new byte[0];

			var encoding = (Flags & NtlmFlags.NegotiateUnicode) != 0 ? Encoding.Unicode : Encoding.UTF8;

			return encoding.GetBytes (text);
		}

		public override byte[] Encode ()
		{
			var target = EncodeString (Domain);
			var user = EncodeString (Username);
			var host = EncodeString (Host);
			var payloadOffset = 64;
			bool negotiateVersion;
			byte[] lm, ntlm;

			ChallengeResponse2.Compute (type2, Level, Username, Password, Domain, out lm, out ntlm);

			if (negotiateVersion = (type2.Flags & NtlmFlags.NegotiateVersion) != 0)
				payloadOffset += 8;

			var lmResponseLength = lm != null ? lm.Length : 0;
			var ntResponseLength = ntlm != null ? ntlm.Length : 0;

			var message = PrepareMessage (payloadOffset + target.Length + user.Length + host.Length + lmResponseLength + ntResponseLength);

			// LM response
			short lmResponseOffset = (short) (payloadOffset + target.Length + user.Length + host.Length);
			message[12] = (byte) lmResponseLength;
			message[13] = (byte) 0x00;
			message[14] = message[12];
			message[15] = message[13];
			message[16] = (byte) lmResponseOffset;
			message[17] = (byte) (lmResponseOffset >> 8);

			// NT response
			short ntResponseOffset = (short) (lmResponseOffset + lmResponseLength);
			message[20] = (byte) ntResponseLength;
			message[21] = (byte) (ntResponseLength >> 8);
			message[22] = message[20];
			message[23] = message[21];
			message[24] = (byte) ntResponseOffset;
			message[25] = (byte) (ntResponseOffset >> 8);

			// target
			short domainLength = (short) target.Length;
			short domainOffset = (short) payloadOffset;
			message[28] = (byte) domainLength;
			message[29] = (byte) (domainLength >> 8);
			message[30] = message[28];
			message[31] = message[29];
			message[32] = (byte) domainOffset;
			message[33] = (byte) (domainOffset >> 8);

			// username
			short userLength = (short) user.Length;
			short userOffset = (short) (domainOffset + domainLength);
			message[36] = (byte) userLength;
			message[37] = (byte) (userLength >> 8);
			message[38] = message[36];
			message[39] = message[37];
			message[40] = (byte) userOffset;
			message[41] = (byte) (userOffset >> 8);

			// host
			short hostLength = (short) host.Length;
			short hostOffset = (short) (userOffset + userLength);
			message[44] = (byte) hostLength;
			message[45] = (byte) (hostLength >> 8);
			message[46] = message[44];
			message[47] = message[45];
			message[48] = (byte) hostOffset;
			message[49] = (byte) (hostOffset >> 8);

			// message length
			short messageLength = (short) message.Length;
			message[56] = (byte) messageLength;
			message[57] = (byte) (messageLength >> 8);

			// options flags
			message[60] = (byte) Flags;
			message[61] = (byte)((uint) Flags >> 8);
			message[62] = (byte)((uint) Flags >> 16);
			message[63] = (byte)((uint) Flags >> 24);

			if (negotiateVersion) {
				message[64] = (byte) OSVersion.Major;
				message[65] = (byte) OSVersion.Minor;
				message[66] = (byte) OSVersion.Build;
				message[67] = (byte) (OSVersion.Build >> 8);
				message[68] = 0x00;
				message[69] = 0x00;
				message[70] = 0x00;
				message[71] = 0x0f;
			}

			Buffer.BlockCopy (target, 0, message, domainOffset, target.Length);
			Buffer.BlockCopy (user, 0, message, userOffset, user.Length);
			Buffer.BlockCopy (host, 0, message, hostOffset, host.Length);

			if (lm != null) {
				Buffer.BlockCopy (lm, 0, message, lmResponseOffset, lm.Length);
				Array.Clear (lm, 0, lm.Length);
			}

			if (ntlm != null) {
				Buffer.BlockCopy (ntlm, 0, message, ntResponseOffset, ntlm.Length);
				Array.Clear (ntlm, 0, ntlm.Length);
			}

			return message;
		}
	}
}
