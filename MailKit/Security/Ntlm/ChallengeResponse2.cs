//
// Mono.Security.Protocol.Ntlm.ChallengeResponse
//	Implements Challenge Response for NTLM v1 and NTLM v2 Session
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//	        Martin Baulig <martin.baulig@xamarin.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell (http://www.novell.com)
// Copyright (c) 2012 Xamarin, Inc. (http://www.xamarin.com)
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
using MD5 = MimeKit.Cryptography.MD5;
#else
using System.Security.Cryptography;
#endif

namespace MailKit.Security.Ntlm {
	static class ChallengeResponse2
	{
		static readonly byte[] Magic = { 0x4B, 0x47, 0x53, 0x21, 0x40, 0x23, 0x24, 0x25 };

		// This is the pre-encrypted magic value with a null DES key (0xAAD3B435B51404EE)
		// Ref: http://packetstormsecurity.nl/Crackers/NT/l0phtcrack/l0phtcrack2.5-readme.html
		static readonly byte[] NullEncMagic = { 0xAA, 0xD3, 0xB4, 0x35, 0xB5, 0x14, 0x04, 0xEE };

		static byte[] ComputeLM (string password, byte[] challenge)
		{
			var buffer = new byte[21];

			// create Lan Manager password
			using (var des = DES.Create ()) {
				des.Mode = CipherMode.ECB;

				// Note: In .NET DES cannot accept a weak key
				// this can happen for a null password
				if (string.IsNullOrEmpty (password)) {
					Buffer.BlockCopy (NullEncMagic, 0, buffer, 0, 8);
				} else {
					des.Key = PasswordToKey (password, 0);
					using (var ct = des.CreateEncryptor ())
						ct.TransformBlock (Magic, 0, 8, buffer, 0);
				}

				// and if a password has less than 8 characters
				if (password == null || password.Length < 8) {
					Buffer.BlockCopy (NullEncMagic, 0, buffer, 8, 8);
				} else {
					des.Key = PasswordToKey (password, 7);
					using (var ct = des.CreateEncryptor ())
						ct.TransformBlock (Magic, 0, 8, buffer, 8);
				}
			}

			return GetResponse (challenge, buffer);
		}

		static byte[] ComputeNtlmPassword (string password)
		{
			var buffer = new byte[21];

			// create NT password
			using (var md4 = new MD4 ()) {
				var data = password == null ? new byte[0] : Encoding.Unicode.GetBytes (password);
				var hash = md4.ComputeHash (data);
				Buffer.BlockCopy (hash, 0, buffer, 0, 16);

				// clean up
				Array.Clear (data, 0, data.Length);
				Array.Clear (hash, 0, hash.Length);
			}

			return buffer;
		}

		static byte[] ComputeNtlm (string password, byte[] challenge)
		{
			var buffer = ComputeNtlmPassword (password);
			return GetResponse (challenge, buffer);
		}

		static void ComputeNtlmV2Session (string password, byte[] challenge, out byte[] lm, out byte[] ntlm)
		{
			var nonce = new byte[8];

			using (var rng = RandomNumberGenerator.Create ())
				rng.GetBytes (nonce);

			var sessionNonce = new byte[challenge.Length + 8];
			challenge.CopyTo (sessionNonce, 0);
			nonce.CopyTo (sessionNonce, challenge.Length);

			lm = new byte[24];
			nonce.CopyTo (lm, 0);

			using (var md5 = MD5.Create ()) {
				var hash = md5.ComputeHash (sessionNonce);
				var newChallenge = new byte[8];

				Array.Copy (hash, newChallenge, 8);

				ntlm = ComputeNtlm (password, newChallenge);

				// clean up
				Array.Clear (newChallenge, 0, newChallenge.Length);
				Array.Clear (hash, 0, hash.Length);
			}

			// clean up
			Array.Clear (sessionNonce, 0, sessionNonce.Length);
			Array.Clear (nonce, 0, nonce.Length);
		}

		static byte[] ComputeNtlmV2 (Type2Message type2, string username, string password, string domain)
		{
			var ntlm_hash = ComputeNtlmPassword (password);

			var ubytes = Encoding.Unicode.GetBytes (username.ToUpperInvariant ());
			var tbytes = Encoding.Unicode.GetBytes (domain);

			var bytes = new byte[ubytes.Length + tbytes.Length];
			ubytes.CopyTo (bytes, 0);
			Array.Copy (tbytes, 0, bytes, ubytes.Length, tbytes.Length);

			byte[] ntlm_v2_hash;

			using (var md5 = new HMACMD5 (ntlm_hash))
				ntlm_v2_hash = md5.ComputeHash (bytes);

			Array.Clear (ntlm_hash, 0, ntlm_hash.Length);

			using (var md5 = new HMACMD5 (ntlm_v2_hash)) {
				var now = DateTime.Now;
				var timestamp = now.Ticks - 504911232000000000;
				var nonce = new byte[8];

				using (var rng = RandomNumberGenerator.Create ())
					rng.GetBytes (nonce);

				var targetInfo = type2.EncodedTargetInfo;
				var blob = new byte[28 + targetInfo.Length];
				blob[0] = 0x01;
				blob[1] = 0x01;

				Buffer.BlockCopy (BitConverterLE.GetBytes (timestamp), 0, blob, 8, 8);

				Buffer.BlockCopy (nonce, 0, blob, 16, 8);
				Buffer.BlockCopy (targetInfo, 0, blob, 28, targetInfo.Length);

				var challenge = type2.Nonce;

				var hashInput = new byte[challenge.Length + blob.Length];
				challenge.CopyTo (hashInput, 0);
				blob.CopyTo (hashInput, challenge.Length);

				var blobHash = md5.ComputeHash (hashInput);

				var response = new byte[blob.Length + blobHash.Length];
				blobHash.CopyTo (response, 0);
				blob.CopyTo (response, blobHash.Length);

				Array.Clear (ntlm_v2_hash, 0, ntlm_v2_hash.Length);
				Array.Clear (hashInput, 0, hashInput.Length);
				Array.Clear (blobHash, 0, blobHash.Length);
				Array.Clear (nonce, 0, nonce.Length);
				Array.Clear (blob, 0, blob.Length);

				return response;
			}
		}

		public static void Compute (Type2Message type2, NtlmAuthLevel level, string username, string password, string domain, out byte[] lm, out byte[] ntlm)
		{
			lm = null;

			switch (level) {
			case NtlmAuthLevel.LM_and_NTLM:
				lm = ComputeLM (password, type2.Nonce);
				ntlm = ComputeNtlm (password, type2.Nonce);
				break;
			case NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session:
				if ((type2.Flags & NtlmFlags.NegotiateNtlm2Key) == 0)
					goto case NtlmAuthLevel.LM_and_NTLM;
				ComputeNtlmV2Session (password, type2.Nonce, out lm, out ntlm);
				break;
			case NtlmAuthLevel.NTLM_only:
				if ((type2.Flags & NtlmFlags.NegotiateNtlm2Key) != 0)
					ComputeNtlmV2Session (password, type2.Nonce, out lm, out ntlm);
				else
					ntlm = ComputeNtlm (password, type2.Nonce);
				break;
			case NtlmAuthLevel.NTLMv2_only:
				ntlm = ComputeNtlmV2 (type2, username, password, domain);
				break;
			default:
				throw new InvalidOperationException ();
			}
		}

		static byte[] GetResponse (byte[] challenge, byte[] pwd) 
		{
			var response = new byte[24];

			using (var des = DES.Create ()) {
				des.Mode = CipherMode.ECB;
				des.Key = PrepareDESKey (pwd, 0);

				using (var ct = des.CreateEncryptor ())
					ct.TransformBlock (challenge, 0, 8, response, 0);

				des.Key = PrepareDESKey (pwd, 7);

				using (var ct = des.CreateEncryptor ())
					ct.TransformBlock (challenge, 0, 8, response, 8);

				des.Key = PrepareDESKey (pwd, 14);

				using (var ct = des.CreateEncryptor ())
					ct.TransformBlock (challenge, 0, 8, response, 16);
			}

			return response;
		}

		static byte[] PrepareDESKey (byte[] key56bits, int position) 
		{
			// convert to 8 bytes
			var key = new byte[8];

			key[0] = key56bits [position];
			key[1] = (byte) ((key56bits[position] << 7)     | (key56bits[position + 1] >> 1));
			key[2] = (byte) ((key56bits[position + 1] << 6) | (key56bits[position + 2] >> 2));
			key[3] = (byte) ((key56bits[position + 2] << 5) | (key56bits[position + 3] >> 3));
			key[4] = (byte) ((key56bits[position + 3] << 4) | (key56bits[position + 4] >> 4));
			key[5] = (byte) ((key56bits[position + 4] << 3) | (key56bits[position + 5] >> 5));
			key[6] = (byte) ((key56bits[position + 5] << 2) | (key56bits[position + 6] >> 6));
			key[7] = (byte)  (key56bits[position + 6] << 1);

			return key;
		}

		static byte[] PasswordToKey (string password, int position) 
		{
			int len = Math.Min (password.Length - position, 7);
			var key7 = new byte[7];

			Encoding.ASCII.GetBytes (password.ToUpper (), position, len, key7, 0);
			var key8 = PrepareDESKey (key7, 0);

			// cleanup intermediate key material
			Array.Clear (key7, 0, key7.Length);

			return key8;
		}
	}
}
