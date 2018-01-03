//
// Mono.Security.Protocol.Ntlm.ChallengeResponse
//	Implements Challenge Response for NTLM v1
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell (http://www.novell.com)
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

#if !NETFX_CORE
using System.Security.Cryptography;
#else
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Security.Ntlm {
	class ChallengeResponse : IDisposable
	{
		static readonly byte[] magic = { 0x4B, 0x47, 0x53, 0x21, 0x40, 0x23, 0x24, 0x25 };

		// This is the pre-encrypted magic value with a null DES key (0xAAD3B435B51404EE)
		// Ref: http://packetstormsecurity.nl/Crackers/NT/l0phtcrack/l0phtcrack2.5-readme.html
		static readonly byte[] nullEncMagic = { 0xAA, 0xD3, 0xB4, 0x35, 0xB5, 0x14, 0x04, 0xEE };

		byte[] challenge, lmpwd, ntpwd;
		bool disposed;

		public ChallengeResponse () 
		{
			lmpwd = new byte[21];
			ntpwd = new byte[21];
		}
		
		public ChallengeResponse (string password, byte[] challenge) : this ()
		{
			Password = password;
			Challenge = challenge;
		}

		~ChallengeResponse () 
		{
			Dispose (false);
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (ChallengeResponse));
		}

		public string Password {
			get { return null; }
			set { 
				CheckDisposed ();

				// create Lan Manager password
				using (var des = DES.Create ()) {
					des.Mode = CipherMode.ECB;

					// Note: In .NET DES cannot accept a weak key
					// this can happen for a null password
					if (string.IsNullOrEmpty (value)) {
						Buffer.BlockCopy (nullEncMagic, 0, lmpwd, 0, 8);
					} else {
						des.Key = PasswordToKey (value, 0);
						using (var ct = des.CreateEncryptor ())
							ct.TransformBlock (magic, 0, 8, lmpwd, 0);
					}

					// and if a password has less than 8 characters
					if (value == null || value.Length < 8) {
						Buffer.BlockCopy (nullEncMagic, 0, lmpwd, 8, 8);
					} else {
						des.Key = PasswordToKey (value, 7);
						using (var ct = des.CreateEncryptor ())
							ct.TransformBlock (magic, 0, 8, lmpwd, 8);
					}

					// create NT password
					using (var md4 = new MD4 ()) {
						var data = value == null ? new byte[0] : Encoding.Unicode.GetBytes (value);
						var hash = md4.ComputeHash (data);

						Buffer.BlockCopy (hash, 0, ntpwd, 0, 16);

						// clean up
						Array.Clear (data, 0, data.Length);
						Array.Clear (hash, 0, hash.Length);
					}
				}
			}
		}

		public byte[] Challenge {
			get { return null; }
			set {
				if (value == null)
					throw new ArgumentNullException (nameof (value));

				CheckDisposed ();

				// we don't want the caller to modify the value afterward
				challenge = (byte[]) value.Clone ();
			}
		}

		public byte[] LM {
			get { 
				CheckDisposed ();

				return GetResponse (lmpwd);
			}
		}

		public byte[] NT {
			get { 
				CheckDisposed ();

				return GetResponse (ntpwd);
			}
		}

		static byte[] PrepareDESKey (byte[] key56bits, int position)
		{
			// convert to 8 bytes
			var key = new byte[8];

			key[0] = key56bits [position];
			key[1] = (byte) ((key56bits [position] << 7)     | (key56bits [position + 1] >> 1));
			key[2] = (byte) ((key56bits [position + 1] << 6) | (key56bits [position + 2] >> 2));
			key[3] = (byte) ((key56bits [position + 2] << 5) | (key56bits [position + 3] >> 3));
			key[4] = (byte) ((key56bits [position + 3] << 4) | (key56bits [position + 4] >> 4));
			key[5] = (byte) ((key56bits [position + 4] << 3) | (key56bits [position + 5] >> 5));
			key[6] = (byte) ((key56bits [position + 5] << 2) | (key56bits [position + 6] >> 6));
			key[7] = (byte)  (key56bits [position + 6] << 1);

			return key;
		}

		static byte[] PasswordToKey (string password, int position)
		{
			int len = Math.Min (password.Length - position, 7);
			var key7 = new byte[7];

			Encoding.ASCII.GetBytes (password.ToUpper (), position, len, key7, 0);
			var key8 = PrepareDESKey (key7, 0);

			Array.Clear (key7, 0, key7.Length);

			return key8;
		}

		byte[] GetResponse (byte[] pwd)
		{
			var response = new byte[24];

			using (var des = DES.Create ()) {
				des.Mode = CipherMode.ECB;
				des.Key = PrepareDESKey (pwd, 0);

				using (var transform = des.CreateEncryptor ())
					transform.TransformBlock (challenge, 0, 8, response, 0);

				des.Key = PrepareDESKey (pwd, 7);

				using (var transform = des.CreateEncryptor ())
					transform.TransformBlock (challenge, 0, 8, response, 8);

				des.Key = PrepareDESKey (pwd, 14);

				using (var transform = des.CreateEncryptor ())
					transform.TransformBlock (challenge, 0, 8, response, 16);
			}

			return response;
		}

		void Dispose (bool disposing)
		{
			if (!disposed) {
				// cleanup our stuff
				Array.Clear (lmpwd, 0, lmpwd.Length);
				Array.Clear (ntpwd, 0, ntpwd.Length);

				if (challenge != null)
					Array.Clear (challenge, 0, challenge.Length);
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
			disposed = true;
		}
	}
}
