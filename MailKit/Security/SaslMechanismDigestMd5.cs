//
// SaslMechanismDigestMd5.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Net;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;

#if NETSTANDARD1_3 || NETSTANDARD1_6
using MD5 = MimeKit.Cryptography.MD5;
#endif

namespace MailKit.Security {
	/// <summary>
	/// The DIGEST-MD5 SASL mechanism.
	/// </summary>
	/// <remarks>
	/// Unlike the PLAIN and LOGIN SASL mechanisms, the DIGEST-MD5 mechanism
	/// provides some level of protection and should be relatively safe to
	/// use even with a clear-text connection.
	/// </remarks>
	public class SaslMechanismDigestMd5 : SaslMechanism
	{
		static readonly Encoding Latin1;

		enum LoginState {
			Auth,
			Final
		}

		DigestChallenge challenge;
		DigestResponse response;
		internal string cnonce;
		Encoding encoding;
		LoginState state;

		static SaslMechanismDigestMd5 ()
		{
			try {
				Latin1 = Encoding.GetEncoding (28591);
			} catch (NotSupportedException) {
				Latin1 = Encoding.GetEncoding (1252);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismDigestMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new DIGEST-MD5 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismDigestMd5(NetworkCredential) instead.")]
		public SaslMechanismDigestMd5 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismDigestMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new DIGEST-MD5 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismDigestMd5(string, string) instead.")]
		public SaslMechanismDigestMd5 (Uri uri, string userName, string password) : base (uri, userName, password)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismDigestMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new DIGEST-MD5 SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismDigestMd5 (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismDigestMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new DIGEST-MD5 SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismDigestMd5 (string userName, string password) : base (userName, password)
		{
		}

		/// <summary>
		/// Gets or sets the authorization identifier.
		/// </summary>
		/// <remarks>
		/// The authorization identifier is the desired user account that the server should use
		/// for all accesses. This is separate from the user name used for authentication.
		/// </remarks>
		/// <value>The authorization identifier.</value>
		public string AuthorizationId {
			get; set;
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public override string MechanismName {
			get { return "DIGEST-MD5"; }
		}

		/// <summary>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </summary>
		/// <remarks>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </remarks>
		/// <returns>The next challenge response.</returns>
		/// <param name="token">The server's challenge token.</param>
		/// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
		/// <param name="length">The length of the server's challenge.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The SASL mechanism is already authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// THe SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (token == null)
				throw new NotSupportedException ("DIGEST-MD5 does not support SASL-IR.");

			if (IsAuthenticated)
				return null;

			switch (state) {
			case LoginState.Auth:
				if (token.Length > 2048)
					throw new SaslException (MechanismName, SaslErrorCode.ChallengeTooLong, "Server challenge too long.");

				challenge = DigestChallenge.Parse (Encoding.UTF8.GetString (token, startIndex, length));
				encoding = challenge.Charset != null ? Encoding.UTF8 : Latin1;
				cnonce = cnonce ?? GenerateEntropy (15);

				response = new DigestResponse (challenge, encoding, Uri.Scheme, Uri.DnsSafeHost, AuthorizationId, Credentials.UserName, Credentials.Password, cnonce);
				state = LoginState.Final;

				return response.Encode (encoding);
			case LoginState.Final:
				if (token.Length == 0)
					throw new SaslException (MechanismName, SaslErrorCode.MissingChallenge, "Server response did not contain any authentication data.");

				var text = encoding.GetString (token, startIndex, length);
				string key, value;

				if (!DigestChallenge.TryParseKeyValuePair (text, out key, out value))
					throw new SaslException (MechanismName, SaslErrorCode.IncompleteChallenge, "Server response contained incomplete authentication data.");

				if (!key.Equals ("rspauth", StringComparison.OrdinalIgnoreCase))
					throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, "Server response contained invalid data.");

				var expected = response.ComputeHash (encoding, Credentials.Password, false);
				if (value != expected)
					throw new SaslException (MechanismName, SaslErrorCode.IncorrectHash, "Server response did not contain the expected hash.");

				IsAuthenticated = true;
				break;
			}

			return null;
		}

		/// <summary>
		/// Resets the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			state = LoginState.Auth;
			challenge = null;
			response = null;
			cnonce = null;
			base.Reset ();
		}
	}

	class DigestChallenge
	{
		public string[] Realms { get; private set; }
		public string Nonce { get; private set; }
		public HashSet<string> Qop { get; private set; }
		public bool? Stale { get; private set; }
		public int? MaxBuf { get; private set; }
		public string Charset { get; private set; }
		public string Algorithm { get; private set; }
		public HashSet<string> Ciphers { get; private set; }

		DigestChallenge ()
		{
			Ciphers = new HashSet<string> (StringComparer.Ordinal);
			Qop = new HashSet<string> (StringComparer.Ordinal);
		}

		static bool SkipWhiteSpace (string text, ref int index)
		{
			int startIndex = index;

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			return index > startIndex;
		}

		static string GetKey (string text, ref int index)
		{
			int startIndex = index;

			while (index < text.Length && !char.IsWhiteSpace (text[index]) && text[index] != '=' && text[index] != ',')
				index++;

			return text.Substring (startIndex, index - startIndex);
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

			value = text.Substring (startIndex, index - startIndex);

			return true;
		}

		static bool TryParseKeyValuePair (string text, ref int index, out string key, out string value)
		{
			value = null;

			key = GetKey (text, ref index);

			SkipWhiteSpace (text, ref index);
			if (index >= text.Length || text[index] != '=')
				return false;

			// skip over '='
			index++;

			SkipWhiteSpace (text, ref index);
			if (index >= text.Length)
				return false;

			return TryParseValue (text, ref index, out value);
		}

		public static bool TryParseKeyValuePair (string text, out string key, out string value)
		{
			int index = 0;

			value = null;
			key = null;

			SkipWhiteSpace (text, ref index);
			if (index >= text.Length || !TryParseKeyValuePair (text, ref index, out key, out value))
				return false;

			return true;
		}

		public static DigestChallenge Parse (string token)
		{
			var challenge = new DigestChallenge ();
			int index = 0;
			int maxbuf;

			SkipWhiteSpace (token, ref index);

			while (index < token.Length) {
				string key, value;

				if (!TryParseKeyValuePair (token, ref index, out key, out value))
					throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));

				switch (key.ToLowerInvariant ()) {
				case "realm":
					challenge.Realms = value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					break;
				case "nonce":
					if (challenge.Nonce != null)
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					challenge.Nonce = value;
					break;
				case "qop":
					foreach (var qop in value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						challenge.Qop.Add (qop.Trim ());
					break;
				case "stale":
					if (challenge.Stale.HasValue)
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					challenge.Stale = value.ToLowerInvariant () == "true";
					break;
				case "maxbuf":
					if (challenge.MaxBuf.HasValue || !int.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out maxbuf))
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					challenge.MaxBuf = maxbuf;
					break;
				case "charset":
					if (challenge.Charset != null || !value.Equals ("utf-8", StringComparison.OrdinalIgnoreCase))
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					challenge.Charset = "utf-8";
					break;
				case "algorithm":
					if (challenge.Algorithm != null)
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					challenge.Algorithm = value;
					break;
				case "cipher":
					if (challenge.Ciphers.Count > 0)
						throw new SaslException ("DIGEST-MD5", SaslErrorCode.InvalidChallenge, string.Format ("Invalid SASL challenge from the server: {0}", token));
					foreach (var cipher in value.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						challenge.Ciphers.Add (cipher.Trim ());
					break;
				}

				SkipWhiteSpace (token, ref index);
				if (index < token.Length && token[index] == ',') {
					index++;

					SkipWhiteSpace (token, ref index);
				}
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
		public int? MaxBuf { get; private set; }
		public string Charset { get; private set; }
		public string Algorithm { get; private set; }
		public string Cipher { get; private set; }
		public string AuthZid { get; private set; }

		public DigestResponse (DigestChallenge challenge, Encoding encoding, string protocol, string hostName, string authzid, string userName, string password, string cnonce)
		{
			UserName = userName;

			if (challenge.Realms != null && challenge.Realms.Length > 0)
				Realm = challenge.Realms[0];
			else
				Realm = string.Empty;

			Nonce = challenge.Nonce;
			CNonce = cnonce;
			Nc = 1;

			// FIXME: make sure this is supported
			Qop = "auth";

			DigestUri = string.Format ("{0}/{1}", protocol, hostName);
			Algorithm = challenge.Algorithm;
			Charset = challenge.Charset;
			MaxBuf = challenge.MaxBuf;
			AuthZid = authzid;
			Cipher = null;

			Response = ComputeHash (encoding, password, true);
		}

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		public string ComputeHash (Encoding encoding, string password, bool client)
		{
			string text, a1, a2;
			byte[] buf, digest;

			// compute A1
			text = string.Format ("{0}:{1}:{2}", UserName, Realm, password);
			buf = encoding.GetBytes (text);
			using (var md5 = MD5.Create ())
				digest = md5.ComputeHash (buf);

			using (var md5 = MD5.Create ()) {
				md5.TransformBlock (digest, 0, digest.Length, null, 0);
				text = string.Format (":{0}:{1}", Nonce, CNonce);
				if (!string.IsNullOrEmpty (AuthZid))
					text += ":" + AuthZid;
				buf = encoding.GetBytes (text);
				md5.TransformFinalBlock (buf, 0, buf.Length);
				a1 = HexEncode (md5.Hash);
			}

			// compute A2
			text = client ? "AUTHENTICATE:" : ":";
			text += DigestUri;

			if (Qop == "auth-int" || Qop == "auth-conf")
				text += ":00000000000000000000000000000000";

			buf = encoding.GetBytes (text);
			using (var md5 = MD5.Create ())
				digest = md5.ComputeHash (buf);
			a2 = HexEncode (digest);

			// compute KD
			text = string.Format ("{0}:{1}:{2:x8}:{3}:{4}:{5}", a1, Nonce, Nc, CNonce, Qop, a2);
			buf = encoding.GetBytes (text);
			using (var md5 = MD5.Create ())
				digest = md5.ComputeHash (buf);

			return HexEncode (digest);
		}

		static string Quote (string text)
		{
			var quoted = new StringBuilder ();

			quoted.Append ("\"");
			for (int i = 0; i < text.Length; i++) {
				if (text[i] == '\\' || text[i] == '"')
					quoted.Append ('\\');
				quoted.Append (text[i]);
			}
			quoted.Append ("\"");

			return quoted.ToString ();
		}

		public byte[] Encode (Encoding encoding)
		{
			var builder = new StringBuilder ();
			builder.AppendFormat ("username={0}", Quote (UserName));
			builder.AppendFormat (",realm=\"{0}\"", Realm);
			builder.AppendFormat (",nonce=\"{0}\"", Nonce);
			builder.AppendFormat (",cnonce=\"{0}\"", CNonce);
			builder.AppendFormat (",nc={0:x8}", Nc);
			builder.AppendFormat (",qop=\"{0}\"", Qop);
			builder.AppendFormat (",digest-uri=\"{0}\"", DigestUri);
			builder.AppendFormat (",response={0}", Response);
			if (MaxBuf.HasValue)
				builder.AppendFormat (CultureInfo.InvariantCulture, ",maxbuf={0}", MaxBuf.Value);
			if (!string.IsNullOrEmpty (Charset))
				builder.AppendFormat (",charset={0}", Charset);
			if (!string.IsNullOrEmpty (Algorithm))
				builder.AppendFormat (",algorithm={0}", Algorithm);
			if (!string.IsNullOrEmpty (Cipher))
				builder.AppendFormat (",cipher=\"{0}\"", Cipher);
			if (!string.IsNullOrEmpty (AuthZid))
				builder.AppendFormat (",authzid=\"{0}\"", AuthZid);

			return encoding.GetBytes (builder.ToString ());
		}
	}
}
