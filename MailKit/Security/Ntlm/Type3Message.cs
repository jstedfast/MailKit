//
// Mono.Security.Protocol.Ntlm.Type3Message - Authentication
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
	class Type3Message : MessageBase
	{
		readonly Type2Message type2;
		readonly byte[] challenge;
		string domain;
		string host;

		public Type3Message (byte[] message, int startIndex, int length) : base (3)
		{
			Decode (message, startIndex, length);
			type2 = null;
		}

		public Type3Message (Type2Message type2, string userName, string hostName) : base (3)
		{
			this.type2 = type2;

			Level = NtlmSettings.DefaultAuthLevel;

			challenge = (byte[]) type2.Nonce.Clone ();
			Domain = type2.TargetName;
			Username = userName;
			Host = hostName;

			Flags = (NtlmFlags) 0x8200;
			if ((type2.Flags & NtlmFlags.NegotiateUnicode) != 0)
				Flags |= NtlmFlags.NegotiateUnicode;
			else
				Flags |= NtlmFlags.NegotiateOem;

			if ((type2.Flags & NtlmFlags.NegotiateNtlm2Key) != 0)
				Flags |= NtlmFlags.NegotiateNtlm2Key;

			if ((type2.Flags & NtlmFlags.NegotiateVersion) != 0)
				Flags |= NtlmFlags.NegotiateVersion;
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
			domain = DecodeString (message, startIndex + domainOffset, domainLength);

			int userLength = BitConverterLE.ToUInt16 (message, startIndex + 36);
			int userOffset = BitConverterLE.ToUInt16 (message, startIndex + 40);
			Username = DecodeString (message, startIndex + userOffset, userLength);

			int hostLength = BitConverterLE.ToUInt16 (message, startIndex + 44);
			int hostOffset = BitConverterLE.ToUInt16 (message, startIndex + 48);
			host = DecodeString (message, startIndex + hostOffset, hostLength);

			// Session key.  We don't use it yet.
			// int skeyLength = BitConverterLE.ToUInt16 (message, startIndex + 52);
			// int skeyOffset = BitConverterLE.ToUInt16 (message, startIndex + 56);
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
			var target = EncodeString (domain);
			var user = EncodeString (Username);
			var hostName = EncodeString (host);
			var payloadOffset = 64;
			bool reqVersion;
			byte[] lm, ntlm;

			if (type2 == null) {
				if (Level != NtlmAuthLevel.LM_and_NTLM)
					throw new InvalidOperationException ("Refusing to use legacy-mode LM/NTLM authentication unless explicitly enabled using NtlmSettings.DefaultAuthLevel.");
				
				using (var legacy = new ChallengeResponse (Password, challenge)) {
					lm = legacy.LM;
					ntlm = legacy.NT;
				}

				reqVersion = false;
			} else {
				ChallengeResponse2.Compute (type2, Level, Username, Password, domain, out lm, out ntlm);

				if ((reqVersion = (type2.Flags & NtlmFlags.NegotiateVersion) != 0))
					payloadOffset += 8;
			}

			var lmResponseLength = lm != null ? lm.Length : 0;
			var ntResponseLength = ntlm != null ? ntlm.Length : 0;

			var data = PrepareMessage (payloadOffset + target.Length + user.Length + hostName.Length + lmResponseLength + ntResponseLength);

			// LM response
			short lmResponseOffset = (short) (payloadOffset + target.Length + user.Length + hostName.Length);
			data[12] = (byte) lmResponseLength;
			data[13] = (byte) 0x00;
			data[14] = data[12];
			data[15] = data[13];
			data[16] = (byte) lmResponseOffset;
			data[17] = (byte) (lmResponseOffset >> 8);

			// NT response
			short ntResponseOffset = (short) (lmResponseOffset + lmResponseLength);
			data[20] = (byte) ntResponseLength;
			data[21] = (byte) (ntResponseLength >> 8);
			data[22] = data[20];
			data[23] = data[21];
			data[24] = (byte) ntResponseOffset;
			data[25] = (byte) (ntResponseOffset >> 8);

			// target
			short domainLength = (short) target.Length;
			short domainOffset = (short) payloadOffset;
			data[28] = (byte) domainLength;
			data[29] = (byte) (domainLength >> 8);
			data[30] = data[28];
			data[31] = data[29];
			data[32] = (byte) domainOffset;
			data[33] = (byte) (domainOffset >> 8);

			// username
			short userLength = (short) user.Length;
			short userOffset = (short) (domainOffset + domainLength);
			data[36] = (byte) userLength;
			data[37] = (byte) (userLength >> 8);
			data[38] = data[36];
			data[39] = data[37];
			data[40] = (byte) userOffset;
			data[41] = (byte) (userOffset >> 8);

			// host
			short hostLength = (short) hostName.Length;
			short hostOffset = (short) (userOffset + userLength);
			data[44] = (byte) hostLength;
			data[45] = (byte) (hostLength >> 8);
			data[46] = data[44];
			data[47] = data[45];
			data[48] = (byte) hostOffset;
			data[49] = (byte) (hostOffset >> 8);

			// message length
			short messageLength = (short) data.Length;
			data[56] = (byte) messageLength;
			data[57] = (byte) (messageLength >> 8);

			// options flags
			data[60] = (byte) Flags;
			data[61] = (byte)((uint) Flags >> 8);
			data[62] = (byte)((uint) Flags >> 16);
			data[63] = (byte)((uint) Flags >> 24);

			if (reqVersion) {
				// encode the Windows version as Windows 10.0
				data[64] = 0x0A;
				data[65] = 0x0;

				// encode the ProductBuild version
				data[66] = (byte) (10586 & 0xff);
				data[67] = (byte) (10586 >> 8);

				// next 3 bytes are reserved and should remain 0

				// encode the NTLMRevisionCurrent version
				data[71] = 0x0F;
			}

			Buffer.BlockCopy (target, 0, data, domainOffset, target.Length);
			Buffer.BlockCopy (user, 0, data, userOffset, user.Length);
			Buffer.BlockCopy (hostName, 0, data, hostOffset, hostName.Length);

			if (lm != null) {
				Buffer.BlockCopy (lm, 0, data, lmResponseOffset, lm.Length);
				Array.Clear (lm, 0, lm.Length);
			}

			if (ntlm != null) {
				Buffer.BlockCopy (ntlm, 0, data, ntResponseOffset, ntlm.Length);
				Array.Clear (ntlm, 0, ntlm.Length);
			}

			return data;
		}
	}
}
