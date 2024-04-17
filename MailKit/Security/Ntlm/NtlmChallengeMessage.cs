//
// NtlmChallengeMessage.cs
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
	class NtlmChallengeMessage : NtlmMessageBase
	{
		const NtlmFlags DefaultFlags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateUnicode /*| NtlmFlags.NegotiateAlwaysSign*/;
		byte[] serverChallenge;
		byte[] cached;

		public NtlmChallengeMessage (NtlmFlags flags, Version osVersion = null) : base (2)
		{
			serverChallenge = NtlmUtils.NONCE (8);
			OSVersion = osVersion;
			Flags = flags;
		}

		public NtlmChallengeMessage (Version osVersion = null) : this (DefaultFlags, osVersion)
		{
		}

		public NtlmChallengeMessage (byte[] message, int startIndex, int length) : base (2)
		{
			serverChallenge = new byte[8];
			Decode (message, startIndex, length);

			cached = new byte[length];
			Buffer.BlockCopy (message, startIndex, cached, 0, length);
		}

		~NtlmChallengeMessage ()
		{
			if (serverChallenge != null)
				Array.Clear (serverChallenge, 0, serverChallenge.Length);
		}

		public byte[] ServerChallenge {
			get { return serverChallenge; }
			set { 
				if (value == null)
					throw new ArgumentNullException (nameof (value));

				if (value.Length != 8)
					throw new ArgumentException ("Invalid nonce length (should be 8 bytes).", nameof (value));

				Array.Clear (serverChallenge, 0, serverChallenge.Length);
				serverChallenge = value;
			}
		}

		public string TargetName {
			get; set;
		}

		public NtlmTargetInfo TargetInfo {
			get; set;
		}

		public byte[] GetEncodedTargetInfo ()
		{
			return TargetInfo?.Encode ((Flags & NtlmFlags.NegotiateUnicode) != 0);
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			ValidateArguments (message, startIndex, length);

			Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 20);

			Buffer.BlockCopy (message, startIndex + 24, serverChallenge, 0, 8);

			var targetNameLength = BitConverterLE.ToUInt16 (message, startIndex + 12);
			//var targetNameMaxLength = BitConverterLE.ToUInt16 (message, startIndex + 14);
			var targetNameOffset = BitConverterLE.ToInt32 (message, startIndex + 16);

			if (targetNameLength > 0) {
				var encoding = (Flags & NtlmFlags.NegotiateUnicode) != 0 ? Encoding.Unicode : Encoding.UTF8;

				TargetName = encoding.GetString (message, startIndex + targetNameOffset, targetNameLength);
			}

			if ((Flags & NtlmFlags.NegotiateVersion) != 0 && length >= 56) {
				// decode the OS Version
				int major = message[startIndex + 48];
				int minor = message[startIndex + 49];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 50);

				OSVersion = new Version (major, minor, build);
			}

			// The Target Info block is optional.
			if (length >= 48 && targetNameOffset >= 48) {
				var targetInfoLength = BitConverterLE.ToUInt16 (message, startIndex + 40);
				var targetInfoOffset = BitConverterLE.ToUInt16 (message, startIndex + 44);

				if (targetInfoLength > 0 && targetInfoOffset < length && targetInfoLength <= (length - targetInfoOffset))
					TargetInfo = new NtlmTargetInfo (message, startIndex + targetInfoOffset, targetInfoLength, (Flags & NtlmFlags.NegotiateUnicode) != 0);
			}
		}

		public override byte[] Encode ()
		{
			if (cached != null)
				return cached;

			var targetInfo = GetEncodedTargetInfo ();
			int targetNameOffset = 48;
			int targetInfoOffset = 56;
			byte[] targetName = null;
			int size = 48;

			if (TargetName != null) {
				var encoding = (Flags & NtlmFlags.NegotiateUnicode) != 0 ? Encoding.Unicode : Encoding.UTF8;

				targetName = encoding.GetBytes (TargetName);
				targetInfoOffset += targetName.Length;
				size += targetName.Length;
			}

			if (targetInfo != null) {
				size += 8 + targetInfo.Length;
				targetNameOffset += 8;
			}

			// 12 bytes
			var message = PrepareMessage (size);

			// TargetName (8 bytes)
			if (targetName != null) {
				message[12] = (byte) targetName.Length;
				message[13] = (byte)(targetName.Length >> 8);
				message[14] = (byte)targetName.Length;
				message[15] = (byte)(targetName.Length >> 8);
				message[16] = (byte) targetNameOffset;
				message[17] = (byte)(targetNameOffset >> 8);
				//message[18] = (byte) (targetNameOffset >> 16);
				//message[19] = (byte) (targetNameOffset >> 24);

				// TargetName Payload
				Buffer.BlockCopy (targetName, 0, message, targetNameOffset, targetName.Length);
			}

			// NegotiateFlags (4 bytes)
			message[20] = (byte) Flags;
			message[21] = (byte) ((uint) Flags >> 8);
			message[22] = (byte) ((uint) Flags >> 16);
			message[23] = (byte) ((uint) Flags >> 24);

			// ServerChallenge (8 bytes)
			Buffer.BlockCopy (serverChallenge, 0, message, 24, serverChallenge.Length);

			// Reserved (8 bytes)

			// TargetInfo (8 bytes)
			if (targetInfo != null) {
				message[40] = (byte) targetInfo.Length;
				message[41] = (byte)(targetInfo.Length >> 8);
				message[42] = (byte) targetInfo.Length;
				message[43] = (byte)(targetInfo.Length >> 8);
				message[44] = (byte) targetInfoOffset;
				message[45] = (byte)(targetInfoOffset >> 8);

				// TargetInfo Payload
				Buffer.BlockCopy (targetInfo, 0, message, targetInfoOffset, targetInfo.Length);
			}

			if ((Flags & NtlmFlags.NegotiateVersion) != 0) {
				message[48] = (byte) OSVersion.Major;
				message[49] = (byte) OSVersion.Minor;
				message[50] = (byte) OSVersion.Build;
				message[51] = (byte)(OSVersion.Build >> 8);
				message[52] = 0x00;
				message[53] = 0x00;
				message[54] = 0x00;
				message[55] = 0x0f;
			}

			cached = message;

			return message;
		}
	}
}
