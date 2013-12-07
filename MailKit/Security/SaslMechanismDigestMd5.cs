//
// SaslMechanismDigestMd5.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MailKit.Security {
	public class SaslMechanismDigestMd5 : SaslMechanism
	{
		enum LoginState {
			Auth,
			Final
		}

		DigestChallenge challenge;
		DigestResponse response;
		LoginState state;

		public SaslMechanismDigestMd5 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		public override string MechanismName {
			get { return "DIGEST-MD5"; }
		}

		public override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (IsAuthenticated)
				throw new InvalidOperationException ();

			if (token == null)
				return null;

			var cred = Credentials.GetCredential (Uri, MechanismName);

			switch (state) {
			case LoginState.Auth:
				if (token.Length > 2048)
					throw new SaslException ("Server challenge too long.");

				challenge = DigestChallenge.Parse (Encoding.UTF8.GetString (token));
				response = new DigestResponse (challenge, Uri.Scheme, Uri.DnsSafeHost, cred.UserName, cred.Password);
				state = LoginState.Final;
				return response.Encode ();
			case LoginState.Final:
				if (token.Length == 0)
					throw new SaslException ("Server response did not contain any authentication data.");

				var text = Encoding.UTF8.GetString (token);
				string key, value;
				int index = 0;

				if (!DigestChallenge.TryParseKeyValuePair (text, ref index, out key, out value))
					throw new SaslException ("Server response contained incomplete authentication data.");

				var expected = response.ComputeHash (cred.Password, false);
				if (value != expected)
					throw new SaslException ("Server response did not contain the expected hash.");

				IsAuthenticated = true;
				return new byte[0];
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}

		public override void Reset ()
		{
			state = LoginState.Auth;
			challenge = null;
			response = null;
			base.Reset ();
		}
	}

	class DigestChallenge
	{
		public string[] Realms { get; private set; }
		public string Nonce { get; private set; }
		public HashSet<string> Qop { get; private set; }
		public bool Stale { get; private set; }
		public int MaxBuf { get; private set; }
		public string Charset { get; private set; }
		public string Algorithm { get; private set; }
		public HashSet<string> Ciphers { get; private set; }

		DigestChallenge ()
		{
			Ciphers = new HashSet<string> ();
			Qop = new HashSet<string> ();
		}

		static bool SkipWhiteSpace (string text, ref int index)
		{
			int startIndex = index;

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			return index > startIndex;
		}

		static bool TryParseKey (string text, ref int index, out string key)
		{
			int startIndex = index;

			key = null;

			while (index < text.Length && !char.IsWhiteSpace (text[index]) && text[index] != '=' && text[index] != ',')
				index++;

			if (index == startIndex)
				return false;

			key = text.Substring (startIndex, index - startIndex);

			return true;
		}

		static bool TryParseQuoted (string text, ref int index, out string value)
		{
			var builder = new StringBuilder ();
			bool escaped = false;

			value = null;

			// skip over leading '"'
			index++;

			while (index < text.Length) {
				if (text[index] == '\\') {
					if (escaped)
						builder.Append (text[index]);

					escaped = !escaped;
				} else if (!escaped) {
					if (text[index] == '"')
						break;

					builder.Append (text[index]);
				} else {
					escaped = false;
				}

				index++;
			}

			if (index >= text.Length || text[index] != '"')
				return false;

			index++;

			value = builder.ToString ();

			return true;
		}

		static bool TryParseValue (string text, ref int index, out string value)
		{
			if (text[index] == '"')
				return TryParseQuoted (text, ref index, out value);

			int startIndex = index;

			value = null;

			while (index < text.Length && !char.IsWhiteSpace (text[index]) && text[index] != ',')
				index++;

			if (index == startIndex)
				return false;

			value = text.Substring (startIndex, index - startIndex);

			return true;
		}

		public static bool TryParseKeyValuePair (string text, ref int index, out string key, out string value)
		{
			value = null;
			key = null;

			SkipWhiteSpace (text, ref index);

			if (!TryParseKey (text, ref index, out key))
				return false;

			SkipWhiteSpace (text, ref index);
			if (index >= text.Length || text[index] != '=')
				return false;

			// skip over '='
			index++;

			SkipWhiteSpace (text, ref index);

			return TryParseValue (text, ref index, out value);
		}

		public static DigestChallenge Parse (string token)
		{
			var challenge = new DigestChallenge ();
			int index = 0;

			while (index < token.Length) {
				string key, value;

				if (!TryParseKeyValuePair (token, ref index, out key, out value))
					throw new SaslException (string.Format ("Invalid SASL challenge from the server: {0}", token));

				switch (key.ToLowerInvariant ()) {
				case "realm":
					challenge.Realms = value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					break;
				case "nonce":
					challenge.Nonce = value;
					break;
				case "qop":
					foreach (var qop in value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						challenge.Qop.Add (qop.Trim ());
					break;
				case "stale":
					challenge.Stale = value.ToLowerInvariant () == "true";
					break;
				case "maxbuf":
					challenge.MaxBuf = int.Parse (value);
					break;
				case "charset":
					challenge.Charset = value;
					break;
				case "algorithm":
					challenge.Algorithm = value;
					break;
				case "cipher":
					foreach (var cipher in value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						challenge.Ciphers.Add (cipher.Trim ());
					break;
				}

				SkipWhiteSpace (token, ref index);
				if (index < token.Length && token[index] == ',')
					index++;
			}

			return challenge;
		}
	}

	class DigestResponse
	{
		public string UserName { get; private set; }
		public string Realm { get; private set; }
		public string Nonce { get; private set; }
		public string CNonce { get; private set; }
		public int Nc { get; private set; }
		public string Qop { get; private set; }
		public string DigestUri { get; private set; }
		public string Response { get; private set; }
		public int MaxBuf { get; private set; }
		public string Charset { get; private set; }
		public string Algorithm { get; private set; }
		public string Cipher { get; private set; }
		public string AuthZid { get; private set; }

		public DigestResponse (DigestChallenge challenge, string protocol, string hostName, string userName, string password)
		{
			var cnonce = new byte[15];
			new Random ().NextBytes (cnonce);

			UserName = userName;

			if (challenge.Realms.Length > 0)
				Realm = challenge.Realms[0];
			else
				Realm = string.Empty;

			Nonce = challenge.Nonce;
			CNonce = Convert.ToBase64String (cnonce);
			Nc = 1;

			// FIXME: make sure this is supported
			Qop = "auth";

			DigestUri = string.Format ("{0}://{1}", protocol, hostName);

			if (!string.IsNullOrEmpty (challenge.Charset))
				Charset = challenge.Charset;

			Algorithm = challenge.Algorithm;
			AuthZid = null;
			Cipher = null;

			Response = ComputeHash (password, true);
		}

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		public string ComputeHash (string password, bool client)
		{
			using (var checksum = HashAlgorithm.Create (Algorithm ?? "MD5")) {
				byte[] buf, digest;
				string text, a1, a2;

				// compute A1
				text = string.Format ("{0}:{1}:{2}", UserName, Realm, password);
				buf = Encoding.UTF8.GetBytes (text);
				checksum.Initialize ();
				digest = checksum.ComputeHash (buf);

				text = string.Format ("{0}:{1}:{2}", HexEncode (digest), Nonce, CNonce);
				buf = Encoding.UTF8.GetBytes (text);
				checksum.Initialize ();
				digest = checksum.ComputeHash (buf);
				a1 = HexEncode (digest);

				// compute A2
				text = client ? "AUTHENTICATE:" : ":";
				text += DigestUri;

				if (Qop == "auth-int" || Qop == "auth-conf")
					text += ":00000000000000000000000000000000";

				buf = Encoding.ASCII.GetBytes (text);
				checksum.Initialize ();
				digest = checksum.ComputeHash (buf);
				a2 = HexEncode (digest);

				// compute KD
				text = string.Format ("{0}:{1}:{2:8X}:{3}:{4}:{5}", a1, Nonce, Nc, CNonce, Qop, a2);
				buf = Encoding.ASCII.GetBytes (text);
				checksum.Initialize ();
				digest = checksum.ComputeHash (buf);

				return HexEncode (digest);
			}
		}

		public byte[] Encode ()
		{
			Encoding encoding;

			if (!string.IsNullOrEmpty (Charset))
				encoding = Encoding.GetEncoding (Charset);
			else
				encoding = Encoding.UTF8;

			using (var memory = new MemoryStream ()) {
				byte[] buf;

				buf = Encoding.ASCII.GetBytes ("username=\"");
				memory.Write (buf, 0, buf.Length);
				buf = encoding.GetBytes (UserName);
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",realm=\"");
				memory.Write (buf, 0, buf.Length);
				buf = encoding.GetBytes (Realm);
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",nonce=\"");
				memory.Write (buf, 0, buf.Length);
				buf = encoding.GetBytes (Nonce);
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",cnonce=\"");
				memory.Write (buf, 0, buf.Length);
				buf = encoding.GetBytes (CNonce);
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",nc=");
				memory.Write (buf, 0, buf.Length);
				buf = Encoding.ASCII.GetBytes (Nc.ToString ("X8"));
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes (",qop=\"");
				memory.Write (buf, 0, buf.Length);
				buf = Encoding.ASCII.GetBytes (Qop);
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",digest-uri=\"");
				memory.Write (buf, 0, buf.Length);
				buf = Encoding.ASCII.GetBytes (DigestUri.ToString ());
				memory.Write (buf, 0, buf.Length);

				buf = Encoding.ASCII.GetBytes ("\",response=\"");
				memory.Write (buf, 0, buf.Length);
				buf = Encoding.ASCII.GetBytes (Response);
				memory.Write (buf, 0, buf.Length);
				buf = new byte[] { (byte) '"' };
				memory.Write (buf, 0, buf.Length);

				if (MaxBuf > 0) {
					buf = Encoding.ASCII.GetBytes (",maxbuf=");
					memory.Write (buf, 0, buf.Length);
					buf = Encoding.ASCII.GetBytes (MaxBuf.ToString ());
					memory.Write (buf, 0, buf.Length);
				}

				if (!string.IsNullOrEmpty (Charset)) {
					buf = Encoding.ASCII.GetBytes (",charset=\"");
					memory.Write (buf, 0, buf.Length);
					buf = Encoding.ASCII.GetBytes (Charset);
					memory.Write (buf, 0, buf.Length);
					buf = new byte[] { (byte) '"' };
					memory.Write (buf, 0, buf.Length);
				}

				if (!string.IsNullOrEmpty (Algorithm)) {
					buf = Encoding.ASCII.GetBytes (",algorithm=\"");
					memory.Write (buf, 0, buf.Length);
					buf = Encoding.ASCII.GetBytes (Algorithm);
					memory.Write (buf, 0, buf.Length);
					buf = new byte[] { (byte) '"' };
					memory.Write (buf, 0, buf.Length);
				}

				if (!string.IsNullOrEmpty (Cipher)) {
					buf = Encoding.ASCII.GetBytes (",cipher=\"");
					memory.Write (buf, 0, buf.Length);
					buf = Encoding.ASCII.GetBytes (Cipher);
					memory.Write (buf, 0, buf.Length);
					buf = new byte[] { (byte) '"' };
					memory.Write (buf, 0, buf.Length);
				}

				if (!string.IsNullOrEmpty (AuthZid)) {
					buf = Encoding.ASCII.GetBytes (",authzid=\"");
					memory.Write (buf, 0, buf.Length);
					buf = Encoding.ASCII.GetBytes (AuthZid);
					memory.Write (buf, 0, buf.Length);
					buf = new byte[] { (byte) '"' };
					memory.Write (buf, 0, buf.Length);
				}

				return memory.ToArray ();
			}
		}
	}
}
