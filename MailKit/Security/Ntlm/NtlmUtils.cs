//
// NtlmUtils.cs
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
using System.Security.Cryptography;

using SSCMD5 = System.Security.Cryptography.MD5;

namespace MailKit.Security.Ntlm {
	static class NtlmUtils
	{
		//static readonly byte[] ClientSealMagic = Encoding.ASCII.GetBytes ("session key to client-to-server sealing key magic constant");
		//static readonly byte[] ServerSealMagic = Encoding.ASCII.GetBytes ("session key to server-to-client sealing key magic constant");
		//static readonly byte[] ClientSignMagic = Encoding.ASCII.GetBytes ("session key to client-to-server signing key magic constant");
		//static readonly byte[] ServerSignMagic = Encoding.ASCII.GetBytes ("session key to server-to-client signing key magic constant");
		//static readonly byte[] SealKeySuffix40 = new byte[] { 0xe5, 0x38, 0xb0 };
		//static readonly byte[] SealKeySuffix56 = new byte[] { 0xa0 };
		static readonly byte[] Responserversion = new byte[] { 1 };
		static readonly byte[] HiResponserversion = new byte[] { 1 };
		static readonly byte[] Z24 = new byte[24];
		static readonly byte[] Z6 = new byte[6];
		static readonly byte[] Z4 = new byte[4];
		static readonly byte[] Z1 = new byte[1];

		public static byte[] ConcatenationOf (params string[] values)
		{
			var concatenatedValue = string.Concat (values);

			return Encoding.Unicode.GetBytes (concatenatedValue);
		}

		public static byte[] ConcatenationOf (params byte[][] values)
		{
			int index = 0, length = 0;

			for (int i = 0; i < values.Length; i++)
				length += values[i].Length;

			var concatenated = new byte[length];
			for (int i = 0; i < values.Length; i++) {
				length = values[i].Length;
				Buffer.BlockCopy (values[i], 0, concatenated, index, length);
				index += length;
			}

			return concatenated;
		}

		static byte[] MD4 (byte[] buffer)
		{
			using (var md4 = new MD4 ())
				return md4.ComputeHash (buffer);
		}

		static byte[] MD4 (string password)
		{
			var unicode = Encoding.Unicode.GetBytes (password);
			var hash = MD4 (unicode);

			Array.Clear (unicode, 0, unicode.Length);

			return hash;
		}

		public static byte[] MD5 (byte[] buffer)
		{
			using (var md5 = SSCMD5.Create ())
				return md5.ComputeHash (buffer);
		}

		public static byte[] HMACMD5 (byte[] key, params byte[][] values)
		{
			using (var md5 = new HMACMD5 (key)) {
				int i;

				for (i = 0; i < values.Length - 1; i++)
					md5.TransformBlock (values[i], 0, values[i].Length, null, 0);

				md5.TransformFinalBlock (values[i], 0, values[i].Length);

				return md5.Hash;
			}
		}

		public static byte[] NONCE (int size)
		{
			var nonce = new byte[size];

			using (var rng = RandomNumberGenerator.Create ())
				rng.GetBytes (nonce);

			return nonce;
		}

		public static byte[] RC4K (byte[] key, byte[] message)
		{
			try {
				using (var rc4 = new RC4 ()) {
					rc4.Key = key;

					return rc4.TransformFinalBlock (message, 0, message.Length);
				}
			} finally {
				Array.Clear (key, 0, key.Length);
			}
		}

#if false
		public static byte[] SEALKEY (NtlmFlags flags, byte[] exportedSessionKey, bool client = true)
		{
			if ((flags & NtlmFlags.NegotiateExtendedSessionSecurity) != 0) {
				byte[] subkey;

				if ((flags & NtlmFlags.Negotiate128) != 0) {
					subkey = exportedSessionKey;
				} else if ((flags & NtlmFlags.Negotiate56) != 0) {
					subkey = new byte[7];
					Buffer.BlockCopy (exportedSessionKey, 0, subkey, 0, subkey.Length);
				} else {
					subkey = new byte[5];
					Buffer.BlockCopy (exportedSessionKey, 0, subkey, 0, subkey.Length);
				}

				var magic = client ? ClientSealMagic : ServerSealMagic;
				var sealKey = MD5 (ConcatenationOf (subkey, magic));

				if (subkey != exportedSessionKey)
					Array.Clear (subkey, 0, subkey.Length);

				return sealKey;
			} else if ((flags & NtlmFlags.NegotiateLanManagerKey) != 0) {
				byte[] suffix;
				int length;

				if ((flags & NtlmFlags.Negotiate56) != 0) {
					suffix = SealKeySuffix56;
					length = 7;
				} else {
					suffix = SealKeySuffix40;
					length = 5;
				}

				var sealKey = new byte[length + suffix.Length];
				Buffer.BlockCopy (exportedSessionKey, 0, sealKey, 0, length);
				Buffer.BlockCopy (suffix, 0, sealKey, length, suffix.Length);

				return sealKey;
			} else {
				return exportedSessionKey;
			}
		}
#endif

#if false
		public static byte[] SIGNKEY (NtlmFlags flags, byte[] exportedSessionKey, bool client = true)
		{
			if ((flags & NtlmFlags.NegotiateExtendedSessionSecurity) != 0) {
				var magic = client ? ClientSignMagic : ServerSignMagic;
				return MD5 (ConcatenationOf (exportedSessionKey, magic));
			} else {
				return null;
			}
		}
#endif

		static byte[] NTOWFv2 (string domain, string userName, string password)
		{
			var hash = MD4 (password);
			byte[] responseKey;

			using (var md5 = new HMACMD5 (hash)) {
				var userDom = ConcatenationOf (userName.ToUpperInvariant (), domain);
				responseKey = md5.ComputeHash (userDom);
			}

			Array.Clear (hash, 0, hash.Length);

			return responseKey;
		}

		public static void ComputeNtlmV2 (NtlmChallengeMessage type2, string domain, string userName, string password, byte[] targetInfo, byte[] clientChallenge, long? time, out byte[] ntChallengeResponse, out byte[] lmChallengeResponse, out byte[] sessionBaseKey)
		{
			if (userName.Length == 0 && password.Length == 0) {
				// Special case for anonymous authentication
				ntChallengeResponse = null;
				lmChallengeResponse = Z1;
				sessionBaseKey = null;
				return;
			}

			var timestamp = (time ?? DateTime.UtcNow.Ticks) - 504911232000000000;
			var responseKey = NTOWFv2 (domain, userName, password);

			// Note: If NTLM v2 authentication is used, the client SHOULD send the timestamp in the CHALLENGE_MESSAGE.
			if (type2.TargetInfo?.Timestamp != null)
				timestamp = type2.TargetInfo.Timestamp.Value;

			// Set temp to ConcatenationOf(Responserversion, HiResponserversion, Z(6), Time, ClientChallenge, Z(4), ServerName, Z(4))
			var temp = ConcatenationOf (Responserversion, HiResponserversion, Z6, BitConverterLE.GetBytes (timestamp), clientChallenge, Z4, targetInfo, Z4);

			// Set NTProofStr to HMAC_MD5(ResponseKeyNT, ConcatenationOf(CHALLENGE_MESSAGE.ServerChallenge, temp))
			var proof = HMACMD5 (responseKey, type2.ServerChallenge, temp);

			// Set SessionBaseKey to HMAC_MD5(ResponseKeyNT, NTProofStr)
			sessionBaseKey = HMACMD5 (responseKey, proof);

			// Set NtChallengeResponse to ConcatenationOf(NTProofStr, temp)
			ntChallengeResponse = ConcatenationOf (proof, temp);
			Array.Clear (proof, 0, proof.Length);
			Array.Clear (temp, 0, temp.Length);

			// Note: If NTLM v2 authentication is used and the CHALLENGE_MESSAGE TargetInfo field (section 2.2.1.2) has an
			// MsvAvTimestamp present, the client SHOULD NOT send the LmChallengeResponse and SHOULD send Z(24) instead.
			if (type2.TargetInfo?.Timestamp == null) {
				// Set LmChallengeResponse to ConcatenationOf(HMAC_MD5(ResponseKeyLM, ConcatenationOf(CHALLENGE_MESSAGE.ServerChallenge, ClientChallenge)), ClientChallenge)
				var hash = HMACMD5 (responseKey, type2.ServerChallenge, clientChallenge);
				lmChallengeResponse = ConcatenationOf (hash, clientChallenge);
				Array.Clear (hash, 0, hash.Length);
			} else {
				lmChallengeResponse = Z24;
			}

			Array.Clear (responseKey, 0, responseKey.Length);
		}
	}
}
