//
// SaslMechanism.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Security {
	/// <summary>
	/// A SASL authentication mechanism.
	/// </summary>
	/// <remarks>
	/// Authenticating via a SASL mechanism may be a multi-step process.
	/// To determine if the mechanism has completed the necessary steps
	/// to authentication, check the <see cref="IsAuthenticated"/> after
	/// each call to <see cref="Challenge(string)"/>.
	/// </remarks>
	public abstract class SaslMechanism
	{
		/// <summary>
		/// The supported authentication mechanisms in order of strongest to weakest.
		/// </summary>
		/// <remarks>
		/// Used by the various clients when authenticating via SASL to determine
		/// which order the SASL mechanisms supported by the server should be tried.
		/// </remarks>
		public static readonly string[] AuthMechanismRank = {
			"SCRAM-SHA-256", "SCRAM-SHA-1", "CRAM-MD5", "DIGEST-MD5", "PLAIN", "LOGIN"
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanism"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanism(NetworkCredential) instead.")]
		protected SaslMechanism (Uri uri, ICredentials credentials)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			Credentials = credentials.GetCredential (uri, MechanismName);
			Uri = uri;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanism"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context.
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
		[Obsolete ("Use SaslMechanism(string, string) instead.")]
		protected SaslMechanism (Uri uri, string userName, string password)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			Credentials = new NetworkCredential (userName, password);
			Uri = uri;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanism"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		protected SaslMechanism (NetworkCredential credentials)
		{
			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			Credentials = credentials;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanism"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		protected SaslMechanism (string userName, string password)
		{
			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			Credentials = new NetworkCredential (userName, password);
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public abstract string MechanismName {
			get;
		}

		/// <summary>
		/// Gets the user's credentials.
		/// </summary>
		/// <remarks>
		/// Gets the user's credentials.
		/// </remarks>
		/// <value>The user's credentials.</value>
		public NetworkCredential Credentials {
			get; private set;
		}

		/// <summary>
		/// Gets whether or not the mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// SASL mechanisms that support sending an initial client response to the server
		/// should return <value>true</value>.
		/// </remarks>
		/// <value><c>true</c> if the mechanism supports an initial response; otherwise, <c>false</c>.</value>
		public virtual bool SupportsInitialResponse {
			get { return false; }
		}

		/// <summary>
		/// Gets or sets whether the SASL mechanism has finished authenticating.
		/// </summary>
		/// <remarks>
		/// Gets or sets whether the SASL mechanism has finished authenticating.
		/// </remarks>
		/// <value><c>true</c> if the SASL mechanism has finished authenticating; otherwise, <c>false</c>.</value>
		public bool IsAuthenticated {
			get; protected set;
		}

		/// <summary>
		/// Gets whether or not a security layer was negotiated.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not a security layer has been negotiated by the SASL mechanism.</para>
		/// <note type="note">Some SASL mechanisms, such as GSSAPI, are able to negotiate security layers
		/// such as integrity and confidentiality protection.</note>
		/// </remarks>
		/// <value><c>true</c> if a security layer was negotiated; otherwise, <c>false</c>.</value>
		public virtual bool NegotiatedSecurityLayer {
			get { return false; }
		}

		/// <summary>
		/// Gets or sets the URI of the service.
		/// </summary>
		/// <remarks>
		/// Gets or sets the URI of the service.
		/// </remarks>
		/// <value>The URI of the service.</value>
		internal Uri Uri {
			get; set;
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
		protected abstract byte[] Challenge (byte[] token, int startIndex, int length);

		/// <summary>
		/// Decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </summary>
		/// <remarks>
		/// Decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </remarks>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The SASL mechanism is already authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// THe SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		public string Challenge (string token)
		{
			byte[] decoded = null;
			int length = 0;

			if (token != null) {
				try {
					decoded = Convert.FromBase64String (token.Trim ());
					length = decoded.Length;
				} catch (FormatException) {
				}
			}

			var challenge = Challenge (decoded, 0, length);

			if (challenge == null)
				return null;

			return Convert.ToBase64String (challenge);
		}

		/// <summary>
		/// Resets the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public virtual void Reset ()
		{
			IsAuthenticated = false;
		}

		/// <summary>
		/// Determines if the specified SASL mechanism is supported by MailKit.
		/// </summary>
		/// <remarks>
		/// Use this method to make sure that a SASL mechanism is supported before calling
		/// <see cref="Create(string,Uri,ICredentials)"/>.
		/// </remarks>
		/// <returns><c>true</c> if the specified SASL mechanism is supported; otherwise, <c>false</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mechanism"/> is <c>null</c>.
		/// </exception>
		public static bool IsSupported (string mechanism)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			switch (mechanism) {
			case "SCRAM-SHA-256": return true;
			case "SCRAM-SHA-1":   return true;
			case "DIGEST-MD5":    return true;
			case "CRAM-MD5":      return true;
			case "XOAUTH2":       return true;
			case "PLAIN":         return true;
			case "LOGIN":         return true;
			case "NTLM":          return true;
			default:              return false;
			}
		}

		/// <summary>
		/// Create an instance of the specified SASL mechanism using the uri and credentials.
		/// </summary>
		/// <remarks>
		/// If unsure that a particular SASL mechanism is supported, you should first call
		/// <see cref="IsSupported"/>.
		/// </remarks>
		/// <returns>An instance of the requested SASL mechanism if supported; otherwise <c>null</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <param name="uri">The URI of the service to authenticate against.</param>
		/// <param name="encoding">The text encoding to use for the credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mechanism"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public static SaslMechanism Create (string mechanism, Uri uri, Encoding encoding, ICredentials credentials)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			var cred = credentials.GetCredential (uri, mechanism);

			switch (mechanism) {
			//case "KERBEROS_V4":   return null;
			case "SCRAM-SHA-256": return new SaslMechanismScramSha256 (cred) { Uri = uri };
			case "SCRAM-SHA-1":   return new SaslMechanismScramSha1 (cred) { Uri = uri };
			case "DIGEST-MD5":    return new SaslMechanismDigestMd5 (cred) { Uri = uri };
			case "CRAM-MD5":      return new SaslMechanismCramMd5 (cred) { Uri = uri };
			//case "GSSAPI":        return null;
			case "XOAUTH2":       return new SaslMechanismOAuth2 (cred) { Uri = uri };
			case "PLAIN":         return new SaslMechanismPlain (encoding, cred) { Uri = uri };
			case "LOGIN":         return new SaslMechanismLogin (encoding, cred) { Uri = uri };
			case "NTLM":          return new SaslMechanismNtlm (cred) { Uri = uri };
			default:              return null;
			}
		}

		/// <summary>
		/// Create an instance of the specified SASL mechanism using the uri and credentials.
		/// </summary>
		/// <remarks>
		/// If unsure that a particular SASL mechanism is supported, you should first call
		/// <see cref="IsSupported"/>.
		/// </remarks>
		/// <returns>An instance of the requested SASL mechanism if supported; otherwise <c>null</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <param name="uri">The URI of the service to authenticate against.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mechanism"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public static SaslMechanism Create (string mechanism, Uri uri, ICredentials credentials)
		{
			return Create (mechanism, uri, Encoding.UTF8, credentials);
		}

		/// <summary>
		/// Determines if the character is a non-ASCII space.
		/// </summary>
		/// <remarks>
		/// This list was obtained from http://tools.ietf.org/html/rfc3454#appendix-C.1.2
		/// </remarks>
		/// <returns><c>true</c> if the character is a non-ASCII space; otherwise, <c>false</c>.</returns>
		/// <param name="c">The character.</param>
		static bool IsNonAsciiSpace (char c)
		{
			switch (c) {
			case '\u00A0': // NO-BREAK SPACE
			case '\u1680': // OGHAM SPACE MARK
			case '\u2000': // EN QUAD
			case '\u2001': // EM QUAD
			case '\u2002': // EN SPACE
			case '\u2003': // EM SPACE
			case '\u2004': // THREE-PER-EM SPACE
			case '\u2005': // FOUR-PER-EM SPACE
			case '\u2006': // SIX-PER-EM SPACE
			case '\u2007': // FIGURE SPACE
			case '\u2008': // PUNCTUATION SPACE
			case '\u2009': // THIN SPACE
			case '\u200A': // HAIR SPACE
			case '\u200B': // ZERO WIDTH SPACE
			case '\u202F': // NARROW NO-BREAK SPACE
			case '\u205F': // MEDIUM MATHEMATICAL SPACE
			case '\u3000': // IDEOGRAPHIC SPACE
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Determines if the character is commonly mapped to nothing.
		/// </summary>
		/// <remarks>
		/// This list was obtained from http://tools.ietf.org/html/rfc3454#appendix-B.1
		/// </remarks>
		/// <returns><c>true</c> if the character is commonly mapped to nothing; otherwise, <c>false</c>.</returns>
		/// <param name="c">The character.</param>
		static bool IsCommonlyMappedToNothing (char c)
		{
			switch (c) {
			case '\u00AD': case '\u034F': case '\u1806':
			case '\u180B': case '\u180C': case '\u180D':
			case '\u200B': case '\u200C': case '\u200D':
			case '\u2060': case '\uFE00': case '\uFE01':
			case '\uFE02': case '\uFE03': case '\uFE04':
			case '\uFE05': case '\uFE06': case '\uFE07':
			case '\uFE08': case '\uFE09': case '\uFE0A':
			case '\uFE0B': case '\uFE0C': case '\uFE0D':
			case '\uFE0E': case '\uFE0F': case '\uFEFF':
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Determines if the character is prohibited.
		/// </summary>
		/// <remarks>
		/// This list was obtained from http://tools.ietf.org/html/rfc3454#appendix-C.3
		/// </remarks>
		/// <returns><c>true</c> if the character is prohibited; otherwise, <c>false</c>.</returns>
		/// <param name="s">The string.</param>
		/// <param name="index">The character index.</param>
		static bool IsProhibited (string s, int index)
		{
			int u = char.ConvertToUtf32 (s, index);

			// Private Use characters: http://tools.ietf.org/html/rfc3454#appendix-C.3
			if ((u >= 0xE000 && u <= 0xF8FF) || (u >= 0xF0000 && u <= 0xFFFFD) || (u >= 0x100000 && u <= 0x10FFFD))
				return true;

			// Non-character code points: http://tools.ietf.org/html/rfc3454#appendix-C.4
			if ((u >= 0xFDD0 && u <= 0xFDEF) || (u >= 0xFFFE && u <= 0xFFFF) || (u >= 0x1FFFE && u <= 0x1FFFF) ||
				(u >= 0x2FFFE && u <= 0x2FFFF) || (u >= 0x3FFFE && u <= 0x3FFFF) || (u >= 0x4FFFE && u <= 0x4FFFF) ||
				(u >= 0x5FFFE && u <= 0x5FFFF) || (u >= 0x6FFFE && u <= 0x6FFFF) || (u >= 0x7FFFE && u <= 0x7FFFF) ||
				(u >= 0x8FFFE && u <= 0x8FFFF) || (u >= 0x9FFFE && u <= 0x9FFFF) || (u >= 0xAFFFE && u <= 0xAFFFF) ||
				(u >= 0xBFFFE && u <= 0xBFFFF) || (u >= 0xCFFFE && u <= 0xCFFFF) || (u >= 0xDFFFE && u <= 0xDFFFF) ||
				(u >= 0xEFFFE && u <= 0xEFFFF) || (u >= 0xFFFFE && u <= 0xFFFFF) || (u >= 0x10FFFE && u <= 0x10FFFF))
				return true;

			// Surrogate code points: http://tools.ietf.org/html/rfc3454#appendix-C.5
			if (u >= 0xD800 && u <= 0xDFFF)
				return true;

			// Inappropriate for plain text characters: http://tools.ietf.org/html/rfc3454#appendix-C.6
			switch (u) {
			case 0xFFF9: // INTERLINEAR ANNOTATION ANCHOR
			case 0xFFFA: // INTERLINEAR ANNOTATION SEPARATOR
			case 0xFFFB: // INTERLINEAR ANNOTATION TERMINATOR
			case 0xFFFC: // OBJECT REPLACEMENT CHARACTER
			case 0xFFFD: // REPLACEMENT CHARACTER
				return true;
			}

			// Inappropriate for canonical representation: http://tools.ietf.org/html/rfc3454#appendix-C.7
			if (u >= 0x2FF0 && u <= 0x2FFB)
				return true;

			// Change display properties or are deprecated: http://tools.ietf.org/html/rfc3454#appendix-C.8
			switch (u) {
			case 0x0340: // COMBINING GRAVE TONE MARK
			case 0x0341: // COMBINING ACUTE TONE MARK
			case 0x200E: // LEFT-TO-RIGHT MARK
			case 0x200F: // RIGHT-TO-LEFT MARK
			case 0x202A: // LEFT-TO-RIGHT EMBEDDING
			case 0x202B: // RIGHT-TO-LEFT EMBEDDING
			case 0x202C: // POP DIRECTIONAL FORMATTING
			case 0x202D: // LEFT-TO-RIGHT OVERRIDE
			case 0x202E: // RIGHT-TO-LEFT OVERRIDE
			case 0x206A: // INHIBIT SYMMETRIC SWAPPING
			case 0x206B: // ACTIVATE SYMMETRIC SWAPPING
			case 0x206C: // INHIBIT ARABIC FORM SHAPING
			case 0x206D: // ACTIVATE ARABIC FORM SHAPING
			case 0x206E: // NATIONAL DIGIT SHAPES
			case 0x206F: // NOMINAL DIGIT SHAPES
				return true;
			}

			// Tagging characters: http://tools.ietf.org/html/rfc3454#appendix-C.9
			if (u == 0xE0001 || (u >= 0xE0020 && u <= 0xE007F))
				return true;

			return false;
		}

		/// <summary>
		/// Prepares the user name or password string.
		/// </summary>
		/// <remarks>
		/// Prepares a user name or password string according to the rules of rfc4013.
		/// </remarks>
		/// <returns>The prepared string.</returns>
		/// <param name="s">The string to prepare.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="s"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="s"/> contains prohibited characters.
		/// </exception>
		public static string SaslPrep (string s)
		{
			if (s == null)
				throw new ArgumentNullException (nameof (s));

			if (s.Length == 0)
				return s;

			var builder = new StringBuilder (s.Length);
			for (int i = 0; i < s.Length; i++) {
				if (IsNonAsciiSpace (s[i])) {
					// non-ASII space characters [StringPrep, C.1.2] that can be
					// mapped to SPACE (U+0020).
					builder.Append (' ');
				} else if (IsCommonlyMappedToNothing (s[i])) {
					// the "commonly mapped to nothing" characters [StringPrep, B.1]
					// that can be mapped to nothing.
				} else if (char.IsControl (s[i])) {
					throw new ArgumentException ("Control characters are prohibited.", nameof (s));
				} else if (IsProhibited (s, i)) {
					throw new ArgumentException ("One or more characters in the string are prohibited.", nameof (s));
				} else {
					builder.Append (s[i]);
				}
			}

#if !NETFX_CORE && !NETSTANDARD
			return builder.ToString ().Normalize (NormalizationForm.FormKC);
#else
			return builder.ToString ();
#endif
		}
	}
}
