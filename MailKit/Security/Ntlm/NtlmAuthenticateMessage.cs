//
// NtlmAuthenticateMessage.cs
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
using System.Text;

namespace MailKit.Security.Ntlm {
	class NtlmAuthenticateMessage : NtlmMessageBase
	{
		static readonly byte[] Z16 = new byte[16];

		readonly NtlmNegotiateMessage negotiate;
		readonly NtlmChallengeMessage challenge;
		byte[] clientChallenge;

		public NtlmAuthenticateMessage (NtlmNegotiateMessage negotiate, NtlmChallengeMessage challenge, string userName, string password, string domain, string workstation) : base (3)
		{
			if (negotiate == null)
				throw new ArgumentNullException (nameof (negotiate));

			if (challenge == null)
				throw new ArgumentNullException (nameof (challenge));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			clientChallenge = NtlmUtils.NONCE (8);
			this.negotiate = negotiate;
			this.challenge = challenge;

			if (!string.IsNullOrEmpty (domain)) {
				Domain = domain;
			} else if ((challenge.Flags & NtlmFlags.TargetTypeDomain) != 0) {
				// The server is domain-joined, so the TargetName will be the domain.
				Domain = challenge.TargetName;
			} else if (challenge.TargetInfo != null) {
				// The server is not domain-joined, so the TargetName will be the machine name of the server.
				Domain = challenge.TargetInfo.DomainName;
			}

			Workstation = workstation;
			UserName = userName;
			Password = password;

			// Use only the features supported by both the client and server.
			Flags = negotiate.Flags & challenge.Flags;

			// If the client and server both support NEGOTIATE_UNICODE, disable NEGOTIATE_OEM.
			if ((Flags & NtlmFlags.NegotiateUnicode) != 0)
				Flags &= ~NtlmFlags.NegotiateOem;
			// TODO: throw if Unicode && Oem are both unset?

			// If the client and server both support NEGOTIATE_EXTENDED_SESSIONSECURITY, disable NEGOTIATE_LM_KEY.
			if ((Flags & NtlmFlags.NegotiateExtendedSessionSecurity) != 0)
				Flags &= ~NtlmFlags.NegotiateLanManagerKey;

			// Disable NEGOTIATE_KEY_EXCHANGE if neither NEGOTIATE_SIGN nor NEGOTIATE_SEAL are also present.
			if ((Flags & NtlmFlags.NegotiateKeyExchange) != 0 && (Flags & (NtlmFlags.NegotiateSign | NtlmFlags.NegotiateSeal)) == 0)
				Flags &= ~NtlmFlags.NegotiateKeyExchange;

			// If we had RequestTarget in our initial NEGOTIATE_MESSAGE, include it again in this message(?)
			if ((negotiate.Flags & NtlmFlags.RequestTarget) != 0)
				Flags |= NtlmFlags.RequestTarget;

			// If NEGOTIATE_VERSION is set, grab the OSVersion from our original negotiate message.
			if ((Flags & NtlmFlags.NegotiateVersion) != 0)
				OSVersion = negotiate.OSVersion ?? OSVersion;
		}

		public NtlmAuthenticateMessage (byte[] message, int startIndex, int length) : base (3)
		{
			Decode (message, startIndex, length);
			challenge = null;
		}

		~NtlmAuthenticateMessage ()
		{
			if (clientChallenge != null)
				Array.Clear (clientChallenge, 0, clientChallenge.Length);

			if (LmChallengeResponse != null)
				Array.Clear (LmChallengeResponse, 0, LmChallengeResponse.Length);

			if (NtChallengeResponse != null)
				Array.Clear (NtChallengeResponse, 0, NtChallengeResponse.Length);

			if (ExportedSessionKey != null)
				Array.Clear (ExportedSessionKey, 0, ExportedSessionKey.Length);

			if (EncryptedRandomSessionKey != null)
				Array.Clear (EncryptedRandomSessionKey, 0, EncryptedRandomSessionKey.Length);
		}

		/// <summary>
		/// This is only used for unit testing purposes.
		/// </summary>
		internal byte[] ClientChallenge {
			get { return clientChallenge; }
			set {
				if (value == null)
					return;

				if (value.Length != 8)
					throw new ArgumentException ("Invalid nonce length (should be 8 bytes).", nameof (value));

				Array.Clear (clientChallenge, 0, clientChallenge.Length);
				clientChallenge = value;
			}
		}

		/// <summary>
		/// This is only used for unit testing purposes.
		/// </summary>
		internal long? Timestamp {
			get; set;
		}

		public string Domain {
			get; private set;
		}

		public string Workstation {
			get; private set;
		}

		public string Password {
			get; private set;
		}

		public string UserName {
			get; private set;
		}

		public byte[] Mic {
			get; private set;
		}

		public byte[] LmChallengeResponse {
			get; private set;
		}

		public byte[] NtChallengeResponse {
			get; private set;
		}

		public byte[] ExportedSessionKey {
			get; private set;
		}

		public byte[] EncryptedRandomSessionKey {
			get; private set;
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			int payloadOffset = length;
			int micOffset = 64;

			ValidateArguments (message, startIndex, length);

			if (message.Length >= 64)
				Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 60);
			else
				Flags = (NtlmFlags) 0x8201;

			int lmLength = BitConverterLE.ToUInt16 (message, startIndex + 12);
			int lmOffset = BitConverterLE.ToUInt16 (message, startIndex + 16);
			LmChallengeResponse = new byte[lmLength];
			Buffer.BlockCopy (message, startIndex + lmOffset, LmChallengeResponse, 0, lmLength);
			payloadOffset = Math.Min (payloadOffset, lmOffset);

			int ntLength = BitConverterLE.ToUInt16 (message, startIndex + 20);
			int ntOffset = BitConverterLE.ToUInt16 (message, startIndex + 24);
			NtChallengeResponse = new byte[ntLength];
			Buffer.BlockCopy (message, startIndex + ntOffset, NtChallengeResponse, 0, ntLength);
			payloadOffset = Math.Min (payloadOffset, ntOffset);

			int domainLength = BitConverterLE.ToUInt16 (message, startIndex + 28);
			int domainOffset = BitConverterLE.ToUInt16 (message, startIndex + 32);
			Domain = DecodeString (message, startIndex + domainOffset, domainLength);
			payloadOffset = Math.Min (payloadOffset, domainOffset);

			int userLength = BitConverterLE.ToUInt16 (message, startIndex + 36);
			int userOffset = BitConverterLE.ToUInt16 (message, startIndex + 40);
			UserName = DecodeString (message, startIndex + userOffset, userLength);
			payloadOffset = Math.Min (payloadOffset, userOffset);

			int workstationLength = BitConverterLE.ToUInt16 (message, startIndex + 44);
			int workstationOffset = BitConverterLE.ToUInt16 (message, startIndex + 48);
			Workstation = DecodeString (message, startIndex + workstationOffset, workstationLength);
			payloadOffset = Math.Min (payloadOffset, workstationOffset);

			int skeyLength = BitConverterLE.ToUInt16 (message, startIndex + 52);
			int skeyOffset = BitConverterLE.ToUInt16 (message, startIndex + 56);
			EncryptedRandomSessionKey = new byte[skeyLength];
			Buffer.BlockCopy (message, startIndex + skeyOffset, EncryptedRandomSessionKey, 0, skeyLength);
			payloadOffset = Math.Min (payloadOffset, skeyOffset);

			// OSVersion
			if ((Flags & NtlmFlags.NegotiateVersion) != 0 && length >= 72) {
				// decode the OS Version
				int major = message[startIndex + 64];
				int minor = message[startIndex + 65];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 66);

				OSVersion = new Version (major, minor, build);
				micOffset += 8;
			}

			// MIC
			if (micOffset + 16 <= payloadOffset) {
				Mic = new byte[16];
				Buffer.BlockCopy (message, startIndex + micOffset, Mic, 0, Mic.Length);
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
				return Array.Empty<byte> ();

			var encoding = (Flags & NtlmFlags.NegotiateUnicode) != 0 ? Encoding.Unicode : Encoding.UTF8;

			return encoding.GetBytes (text);
		}

		public void ComputeNtlmV2 (string targetName, bool unverifiedTargetName, byte[] channelBinding)
		{
			var targetInfo = new NtlmTargetInfo ();
			int avFlags = 0;

			// If the CHALLENGE_MESSAGE contains a TargetInfo field
			if (challenge.TargetInfo != null) {
				challenge.TargetInfo.CopyTo (targetInfo);

				if (targetInfo.Flags.HasValue)
					avFlags = targetInfo.Flags.Value;

				// If the CHALLENGE_MESSAGE TargetInfo field (section 2.2.1.2) has an MsvAvTimestamp present, the client SHOULD provide a MIC.
				if (challenge.TargetInfo?.Timestamp != null) {
					// If there is an AV_PAIR structure (section 2.2.2.1) with the AvId field set to MsvAvFlags, then in the Value field, set bit 0x2 to 1.
					// Else add an AV_PAIR structure and set the AvId field to MsvAvFlags and the Value field bit 0x2 to 1.
					targetInfo.Flags = avFlags |= 0x2;

					// Temporarily set the MIC to Z16.
					Mic = Z16;
				}

				// If ClientSuppliedTargetName (section 3.1.1.2) is not NULL
				if (targetName != null) {
					// If UnverifiedTargetName (section 3.1.1.2) is TRUE, then in AvId field = MsvAvFlags set 0x00000004 bit.
					if (unverifiedTargetName)
						targetInfo.Flags = avFlags |= 0x4;

					// Add an AV_PAIR structure and set the AvId field to MsvAvTargetName and the Value field to ClientSuppliedTargetName without
					// terminating NULL.
					targetInfo.TargetName = targetName;
				} else {
					// Else add an AV_PAIR structure and set the AvId field to MsvAvTargetName and the Value field to an empty string without terminating NULL.
					targetInfo.TargetName = string.Empty;
				}

				// The client SHOULD send the channel binding AV_PAIR:
				// If the ClientChannelBindingsUnhashed (section 3.1.1.2) is not NULL
				if (channelBinding != null) {
					// Add an AV_PAIR structure and set the AvId field to MsvAvChannelBindings and the Value field to MD5_HASH(ClientChannelBindingsUnhashed).
					targetInfo.ChannelBinding = NtlmUtils.MD5 (channelBinding);
				} else {
					// Else add an AV_PAIR structure and set the AvId field to MsvAvChannelBindings and the Value field to Z(16).
					targetInfo.ChannelBinding = Z16;
				}
			}

			var encodedTargetInfo = targetInfo.Encode ((Flags & NtlmFlags.NegotiateUnicode) != 0);

			// Note: For NTLMv2, the sessionBaseKey is the same as the keyExchangeKey.
			NtlmUtils.ComputeNtlmV2 (challenge, Domain, UserName, Password, encodedTargetInfo, clientChallenge, Timestamp, out var ntChallengeResponse, out var lmChallengeResponse, out var keyExchangeKey);

			NtChallengeResponse = ntChallengeResponse;
			LmChallengeResponse = lmChallengeResponse;

			if ((Flags & NtlmFlags.NegotiateKeyExchange) != 0 && (Flags & (NtlmFlags.NegotiateSign | NtlmFlags.NegotiateSeal)) != 0) {
				ExportedSessionKey = NtlmUtils.NONCE (16);
				EncryptedRandomSessionKey = NtlmUtils.RC4K (keyExchangeKey, ExportedSessionKey);
			} else {
				ExportedSessionKey = keyExchangeKey;
				EncryptedRandomSessionKey = null;
			}

			// If the CHALLENGE_MESSAGE TargetInfo field (section 2.2.1.2) has an MsvAvTimestamp present, the client SHOULD provide a MIC.
			if ((avFlags & 0x2) != 0)
				Mic = NtlmUtils.HMACMD5 (ExportedSessionKey, NtlmUtils.ConcatenationOf (negotiate.Encode (), challenge.Encode (), Encode ()));
		}

		public override byte[] Encode ()
		{
			var target = EncodeString (Domain);
			var user = EncodeString (UserName);
			var workstation = EncodeString (Workstation);
			int payloadOffset = 72, micOffset = -1;

			if (Mic != null) {
				micOffset = payloadOffset;
				payloadOffset += Mic.Length;
			}

			var lmResponseLength = LmChallengeResponse != null ? LmChallengeResponse.Length : 0;
			var ntResponseLength = NtChallengeResponse != null ? NtChallengeResponse.Length : 0;
			int skeyLength = EncryptedRandomSessionKey != null ? EncryptedRandomSessionKey.Length : 0;

			var message = PrepareMessage (payloadOffset + target.Length + user.Length + workstation.Length + lmResponseLength + ntResponseLength + skeyLength);

			// LmChallengeResponse
			short lmResponseOffset = (short) (payloadOffset + target.Length + user.Length + workstation.Length);
			message[12] = (byte) lmResponseLength;
			message[13] = (byte) 0x00;
			message[14] = message[12];
			message[15] = message[13];
			message[16] = (byte) lmResponseOffset;
			message[17] = (byte) (lmResponseOffset >> 8);
			//message[18] = (byte) (lmResponseOffset >> 16);
			//message[19] = (byte) (lmResponseOffset >> 24);

			// NtChallengeResponse
			short ntResponseOffset = (short) (lmResponseOffset + lmResponseLength);
			message[20] = (byte) ntResponseLength;
			message[21] = (byte) (ntResponseLength >> 8);
			message[22] = message[20];
			message[23] = message[21];
			message[24] = (byte) ntResponseOffset;
			message[25] = (byte) (ntResponseOffset >> 8);
			//message[26] = (byte) (ntResponseOffset >> 16);
			//message[27] = (byte) (ntResponseOffset >> 24);

			// Target
			short domainLength = (short) target.Length;
			short domainOffset = (short) payloadOffset;
			message[28] = (byte) domainLength;
			message[29] = (byte) (domainLength >> 8);
			message[30] = message[28];
			message[31] = message[29];
			message[32] = (byte) domainOffset;
			message[33] = (byte) (domainOffset >> 8);
			//message[34] = (byte) (domainOffset >> 16);
			//message[35] = (byte) (domainOffset >> 24);

			// UserName
			short userLength = (short) user.Length;
			short userOffset = (short) (domainOffset + domainLength);
			message[36] = (byte) userLength;
			message[37] = (byte) (userLength >> 8);
			message[38] = message[36];
			message[39] = message[37];
			message[40] = (byte) userOffset;
			message[41] = (byte) (userOffset >> 8);
			//message[42] = (byte) (userOffset >> 16);
			//message[43] = (byte) (userOffset >> 24);

			// Workstation
			short workstationLength = (short) workstation.Length;
			short workstationOffset = (short) (userOffset + userLength);
			message[44] = (byte) workstationLength;
			message[45] = (byte) (workstationLength >> 8);
			message[46] = message[44];
			message[47] = message[45];
			message[48] = (byte) workstationOffset;
			message[49] = (byte) (workstationOffset >> 8);
			//message[50] = (byte) (workstationOffset >> 16);
			//message[51] = (byte) (workstationOffset >> 24);

			// EncryptedRandomSessionKey
			short skeyOffset = (short) (ntResponseOffset + ntResponseLength);
			message[52] = (byte) skeyLength;
			message[53] = (byte) (skeyLength >> 8);
			message[54] = message[52];
			message[55] = message[53];
			message[56] = (byte) skeyOffset;
			message[57] = (byte) (skeyOffset >> 8);
			//message[58] = (byte) (skeyOffset >> 16);
			//message[59] = (byte) (skeyOffset >> 24);

			// options flags
			message[60] = (byte) Flags;
			message[61] = (byte)((uint) Flags >> 8);
			message[62] = (byte)((uint) Flags >> 16);
			message[63] = (byte)((uint) Flags >> 24);

			if ((challenge.Flags & NtlmFlags.NegotiateVersion) != 0 && OSVersion != null) {
				message[64] = (byte) OSVersion.Major;
				message[65] = (byte) OSVersion.Minor;
				message[66] = (byte) OSVersion.Build;
				message[67] = (byte)(OSVersion.Build >> 8);
				message[68] = 0x00;
				message[69] = 0x00;
				message[70] = 0x00;
				message[71] = 0x0f;
			}

			if (Mic != null)
				Buffer.BlockCopy (Mic, 0, message, micOffset, Mic.Length);

			Buffer.BlockCopy (target, 0, message, domainOffset, target.Length);
			Buffer.BlockCopy (user, 0, message, userOffset, user.Length);
			Buffer.BlockCopy (workstation, 0, message, workstationOffset, workstation.Length);

			if (LmChallengeResponse != null)
				Buffer.BlockCopy (LmChallengeResponse, 0, message, lmResponseOffset, LmChallengeResponse.Length);

			if (NtChallengeResponse != null)
				Buffer.BlockCopy (NtChallengeResponse, 0, message, ntResponseOffset, NtChallengeResponse.Length);

			if ((Flags & NtlmFlags.NegotiateKeyExchange) != 0 && EncryptedRandomSessionKey != null)
				Buffer.BlockCopy (EncryptedRandomSessionKey, 0, message, skeyOffset, EncryptedRandomSessionKey.Length);

			return message;
		}
	}
}
