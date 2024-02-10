//
// SaslMechanism.cs
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

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Authentication.ExtendedProtection;

using MailKit.Net;

namespace MailKit.Security {
	/// <summary>
	/// A SASL authentication mechanism.
	/// </summary>
	/// <remarks>
	/// Authenticating via a SASL mechanism may be a multi-step process.
	/// To determine if the mechanism has completed the necessary steps
	/// to authentication, check the <see cref="IsAuthenticated"/> after
	/// each call to <see cref="Challenge(string,CancellationToken)"/>.
	/// </remarks>
	public abstract class SaslMechanism
	{
		/// <summary>
		/// The supported authentication mechanisms in order of strongest to weakest.
		/// </summary>
		/// <remarks>
		/// <para>Used by the various clients when authenticating via SASL to determine
		/// which order the SASL mechanisms supported by the server should be tried.</para>
		/// </remarks>
		static readonly string[] RankedAuthenticationMechanisms;
		static readonly bool md5supported;

		static SaslMechanism ()
		{
			try {
				using (var md5 = MD5.Create ())
					md5supported = true;
			} catch {
				md5supported = false;
			}

			// Note: It's probably arguable that NTLM is more secure than SCRAM but the odds of a server supporting both is probably low.
			var supported = new List<string> {
				"SCRAM-SHA-512",
				"SCRAM-SHA-256",
				"SCRAM-SHA-1",
				"NTLM"
			};
			if (md5supported) {
				supported.Add ("DIGEST-MD5");
				supported.Add ("CRAM-MD5");
			}
			supported.Add ("PLAIN");
			supported.Add ("LOGIN");

			RankedAuthenticationMechanisms = supported.ToArray ();
		}

		/// <summary>
		/// Rank authentication mechanisms in order of security.
		/// </summary>
		/// <remarks>
		/// <para>Ranks authentication mechanisms in order of security.</para>
		/// </remarks>
		/// <param name="authenticationMechanisms">The authentication mechanisms supported by the server.</param>
		/// <returns>The supported authentication mechanisms in ranked order.</returns>
		internal static IEnumerable<string> Rank (HashSet<string> authenticationMechanisms)
		{
			foreach (var mechanism in RankedAuthenticationMechanisms) {
				if (mechanism.StartsWith ("SCRAM-SHA", StringComparison.Ordinal)) {
					var plus = mechanism + "-PLUS";

					if (authenticationMechanisms.Contains (plus)) {
						// Note: If the server supports SCRAM-SHA-#-PLUS, we opt for the -PLUS variant and do not include the non-PLUS variant.
						yield return plus;
						continue;
					}
				}

				if (authenticationMechanisms.Contains (mechanism))
					yield return mechanism;
			}

			yield break;
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
		/// Get the name of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the SASL mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public abstract string MechanismName {
			get;
		}

		/// <summary>
		/// Get the user's credentials.
		/// </summary>
		/// <remarks>
		/// Gets the user's credentials.
		/// </remarks>
		/// <value>The user's credentials.</value>
		public NetworkCredential Credentials {
			get; private set;
		}

		/// <summary>
		/// Get whether or not the SASL mechanism supports channel binding.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the SASL mechanism supports channel binding.
		/// </remarks>
		/// <value><c>true</c> if the SASL mechanism supports channel binding; otherwise, <c>false</c>.</value>
		public virtual bool SupportsChannelBinding {
			get { return false; }
		}

		/// <summary>
		/// Get whether or not the SASL mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the SASL mechanism supports an initial response (SASL-IR).</para>
		/// <para>SASL mechanisms that support sending an initial client response to the server
		/// should return <value>true</value>.</para>
		/// </remarks>
		/// <value><c>true</c> if the SASL mechanism supports an initial response; otherwise, <c>false</c>.</value>
		public virtual bool SupportsInitialResponse {
			get { return false; }
		}

		/// <summary>
		/// Get or set whether the SASL mechanism has finished authenticating.
		/// </summary>
		/// <remarks>
		/// Gets or sets whether the SASL mechanism has finished authenticating.
		/// </remarks>
		/// <value><c>true</c> if the SASL mechanism has finished authenticating; otherwise, <c>false</c>.</value>
		public bool IsAuthenticated {
			get; protected set;
		}

		/// <summary>
		/// Get whether or not channel-binding was negotiated by the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not channel-binding has been negotiated by the SASL mechanism.</para>
		/// <note type="note">Some SASL mechanisms, such as SCRAM-SHA1-PLUS and NTLM, are able to negotiate
		/// channel-bindings.</note>
		/// </remarks>
		/// <value><c>true</c> if channel-binding was negotiated; otherwise, <c>false</c>.</value>
		public virtual bool NegotiatedChannelBinding {
			get { return false; }
		}

		/// <summary>
		/// Get whether or not a security layer was negotiated by the SASL mechanism.
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
		/// Get or set the channel-binding context.
		/// </summary>
		/// <remarks>
		/// Gets or sets the channel-binding context.
		/// </remarks>
		/// <value>The channel-binding context.</value>
		internal IChannelBindingContext ChannelBindingContext {
			get; set;
		}

		/// <summary>
		/// Get or set the URI of the service.
		/// </summary>
		/// <remarks>
		/// Gets or sets the URI of the service.
		/// </remarks>
		/// <value>The URI of the service.</value>
		internal Uri Uri {
			get; set;
		}

		/// <summary>
		/// Try to get a channel-binding token.
		/// </summary>
		/// <remarks>
		/// Tries to get the specified channel-binding.
		/// </remarks>
		/// <param name="kind">The kind of channel-binding desired.</param>
		/// <param name="token">A buffer containing the channel-binding token.</param>
		/// <returns><c>true</c> if the channel-binding token was acquired; otherwise, <c>false</c>.</returns>
		protected bool TryGetChannelBindingToken (ChannelBindingKind kind, out byte[] token)
		{
			if (ChannelBindingContext == null) {
				token = null;
				return false;
			}

			return ChannelBindingContext.TryGetChannelBindingToken (kind, out token);
		}

		static byte[] Base64Decode (string token, out int length)
		{
			byte[] decoded = null;

			length = 0;

			if (token != null) {
				try {
					decoded = Convert.FromBase64String (token);
					length = decoded.Length;
				} catch (FormatException) {
				}
			}

			return decoded;
		}

		static string Base64Encode (byte[] challenge)
		{
			if (challenge == null || challenge.Length == 0)
				return string.Empty;

			return Convert.ToBase64String (challenge);
		}

		/// <summary>
		/// Parse the server's challenge token and return the next challenge response.
		/// </summary>
		/// <remarks>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </remarks>
		/// <returns>The next challenge response.</returns>
		/// <param name="token">The server's challenge token.</param>
		/// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
		/// <param name="length">The length of the server's challenge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected abstract byte[] Challenge (byte[] token, int startIndex, int length, CancellationToken cancellationToken);

		/// <summary>
		/// Decode the base64-encoded server challenge and return the next challenge response encoded in base64.
		/// </summary>
		/// <remarks>
		/// Decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </remarks>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		public string Challenge (string token, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			byte[] decoded = Base64Decode (token?.Trim (), out int length);

			var challenge = Challenge (decoded, 0, length, cancellationToken);

			return Base64Encode (challenge);
		}

		/// <summary>
		/// Asynchronously parse the server's challenge token and return the next challenge response.
		/// </summary>
		/// <remarks>
		/// Asynchronously parses the server's challenge token and returns the next challenge response.
		/// </remarks>
		/// <returns>The next challenge response.</returns>
		/// <param name="token">The server's challenge token.</param>
		/// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
		/// <param name="length">The length of the server's challenge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected virtual Task<byte[]> ChallengeAsync (byte[] token, int startIndex, int length, CancellationToken cancellationToken)
		{
			return Task.FromResult (Challenge (token, startIndex, length, cancellationToken));
		}

		/// <summary>
		/// Asynchronously decode the base64-encoded server challenge and return the next challenge response encoded in base64.
		/// </summary>
		/// <remarks>
		/// Asynchronously decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </remarks>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		public async Task<string> ChallengeAsync (string token, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			byte[] decoded = Base64Decode (token?.Trim (), out int length);

			var challenge = await ChallengeAsync (decoded, 0, length, cancellationToken).ConfigureAwait (false);

			return Base64Encode (challenge);
		}

		/// <summary>
		/// Reset the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public virtual void Reset ()
		{
			IsAuthenticated = false;
		}

		/// <summary>
		/// Determine if the specified SASL mechanism is supported by MailKit.
		/// </summary>
		/// <remarks>
		/// Use this method to make sure that a SASL mechanism is supported before calling
		/// <see cref="Create(string,NetworkCredential)"/>.
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
			case "SCRAM-SHA-512-PLUS": return true;
			case "SCRAM-SHA-512":      return true;
			case "SCRAM-SHA-256-PLUS": return true;
			case "SCRAM-SHA-256":      return true;
			case "SCRAM-SHA-1-PLUS":   return true;
			case "SCRAM-SHA-1":        return true;
			case "DIGEST-MD5":         return md5supported;
			case "CRAM-MD5":           return md5supported;
			case "OAUTHBEARER":        return true;
			case "XOAUTH2":            return true;
			case "PLAIN":              return true;
			case "LOGIN":              return true;
			case "NTLM":               return true;
			case "ANONYMOUS":          return true;
			default:                   return false;
			}
		}

		/// <summary>
		/// Create an instance of the specified SASL mechanism using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// If unsure that a particular SASL mechanism is supported, you should first call
		/// <see cref="IsSupported"/>.
		/// </remarks>
		/// <returns>An instance of the requested SASL mechanism if supported; otherwise <c>null</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <param name="encoding">The text encoding to use for the credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mechanism"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public static SaslMechanism Create (string mechanism, Encoding encoding, NetworkCredential credentials)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			switch (mechanism) {
			//case "KERBEROS_V4":      return null;
			case "SCRAM-SHA-512-PLUS": return new SaslMechanismScramSha512Plus (credentials);
			case "SCRAM-SHA-512":      return new SaslMechanismScramSha512 (credentials);
			case "SCRAM-SHA-256-PLUS": return new SaslMechanismScramSha256Plus (credentials);
			case "SCRAM-SHA-256":      return new SaslMechanismScramSha256 (credentials);
			case "SCRAM-SHA-1-PLUS":   return new SaslMechanismScramSha1Plus (credentials);
			case "SCRAM-SHA-1":        return new SaslMechanismScramSha1 (credentials);
			case "DIGEST-MD5":         return md5supported ? new SaslMechanismDigestMd5 (credentials) : null;
			case "CRAM-MD5":           return md5supported ? new SaslMechanismCramMd5 (credentials) : null;
			//case "GSSAPI":           return null;
			case "OAUTHBEARER":        return new SaslMechanismOAuthBearer (credentials);
			case "XOAUTH2":            return new SaslMechanismOAuth2 (credentials);
			case "PLAIN":              return new SaslMechanismPlain (encoding, credentials);
			case "LOGIN":              return new SaslMechanismLogin (encoding, credentials);
			case "NTLM":               return new SaslMechanismNtlm (credentials);
			case "ANONYMOUS":          return new SaslMechanismAnonymous (encoding, credentials);
			default:                   return null;
			}
		}

		/// <summary>
		/// Create an instance of the specified SASL mechanism using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// If unsure that a particular SASL mechanism is supported, you should first call
		/// <see cref="IsSupported"/>.
		/// </remarks>
		/// <returns>An instance of the requested SASL mechanism if supported; otherwise <c>null</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mechanism"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public static SaslMechanism Create (string mechanism, NetworkCredential credentials)
		{
			return Create (mechanism, Encoding.UTF8, credentials);
		}

		/// <summary>
		/// Determine if the character is a non-ASCII space.
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
		/// Determine if the character is commonly mapped to nothing.
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
		/// Determine if the character is prohibited.
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
		/// Prepare the user name or password string.
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

			return builder.ToString ().Normalize (NormalizationForm.FormKC);
		}

		internal static string GenerateEntropy (int n)
		{
			var entropy = new byte[n];

			using (var rng = RandomNumberGenerator.Create ())
				rng.GetBytes (entropy);

			return Convert.ToBase64String (entropy);
		}
	}
}
