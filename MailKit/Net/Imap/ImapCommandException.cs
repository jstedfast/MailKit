//
// ImapCommandException.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An exception that is thrown when an IMAP command returns NO or BAD.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when an IMAP command fails. Unlike a <see cref="ImapProtocolException"/>,
	/// a <see cref="ImapCommandException"/> does not require the <see cref="ImapClient"/> to be reconnected.
	/// </remarks>
	public class ImapCommandException : CommandException
	{
		static readonly IReadOnlyList<ImapResponseCode> EmptyResponseCodes = new ReadOnlyCollection<ImapResponseCode> (Array.Empty<ImapResponseCode> ());

		/// <summary>
		/// Create a new <see cref="ImapCommandException"/> based on the specified command name and <see cref="ImapCommand"/> state.
		/// </summary>
		/// <remarks>
		/// Create a new <see cref="ImapCommandException"/> based on the specified command name and <see cref="ImapCommand"/> state.
		/// </remarks>
		/// <returns>A new command exception.</returns>
		/// <param name="command">The command name.</param>
		/// <param name="ic">The command state.</param>
		internal static ImapCommandException Create (string command, ImapCommand ic)
		{
			var result = ic.Response.ToString ().ToUpperInvariant ();
			var codes = new List<ImapResponseCode> ();
			string? reason = null;
			string message;

			for (int i = 0; i < ic.RespCodes.Count; i++) {
				if (ic.RespCodes[i].IsError)
					codes.Add (ic.RespCodes[i]);
			}

			if (string.IsNullOrEmpty (ic.ResponseText)) {
				for (int i = codes.Count - 1; i >= 0; i--) {
					if (!string.IsNullOrEmpty (codes[i].Message)) {
						reason = codes[i].Message;
						break;
					}
				}

				reason ??= string.Empty;
			} else {
				reason = ic.ResponseText!;
			}

			if (!string.IsNullOrEmpty (reason))
				message = string.Format ("The IMAP server replied to the '{0}' command with a '{1}' response: {2}", command, result, reason);
			else
				message = string.Format ("The IMAP server replied to the '{0}' command with a '{1}' response.", command, result);

			return ic.Exception != null ? new ImapCommandException (ic.Response, codes, reason, message, ic.Exception) : new ImapCommandException (ic.Response, codes, reason, message);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="message">The error message.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="innerException">The inner exception.</param>
		public ImapCommandException (ImapCommandResponse response, string responseText, string message, Exception innerException) : base (message, innerException)
		{
			ResponseCodes = EmptyResponseCodes;
			ResponseText = responseText;
			Response = response;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseCodes">The error response codes received by the command.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="responseCodes"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="responseCodes"/> contains a <see langword="null" /> response code.
		/// </exception>
		public ImapCommandException (ImapCommandResponse response, IEnumerable<ImapResponseCode> responseCodes, string responseText, string message, Exception innerException) : base (message, innerException)
		{
			ResponseCodes = CreateResponseCodes (responseCodes);
			ResponseText = responseText;
			Response = response;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="message">The error message.</param>
		public ImapCommandException (ImapCommandResponse response, string responseText, string message) : base (message)
		{
			ResponseCodes = EmptyResponseCodes;
			ResponseText = responseText;
			Response = response;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseCodes">The error response codes received by the command.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="message">The error message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="responseCodes"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="responseCodes"/> contains a <see langword="null" /> response code.
		/// </exception>
		public ImapCommandException (ImapCommandResponse response, IEnumerable<ImapResponseCode> responseCodes, string responseText, string message) : base (message)
		{
			ResponseCodes = CreateResponseCodes (responseCodes);
			ResponseText = responseText;
			Response = response;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseText">The human-readable response text.</param>
		public ImapCommandException (ImapCommandResponse response, string responseText)
		{
			ResponseCodes = EmptyResponseCodes;
			ResponseText = responseText;
			Response = response;
		}

		static IReadOnlyList<ImapResponseCode> CreateResponseCodes (IEnumerable<ImapResponseCode> responseCodes)
		{
			if (responseCodes is null)
				throw new ArgumentNullException (nameof (responseCodes));

			var codes = new List<ImapResponseCode> (responseCodes);

			if (codes.Count == 0)
				return EmptyResponseCodes;

			for (int i = 0; i < codes.Count; i++) {
				if (codes[i] is null)
					throw new ArgumentException ("The response codes cannot contain null.", nameof (responseCodes));
			}

			return new ReadOnlyCollection<ImapResponseCode> (codes);
		}
		/// <summary>
		/// Gets the IMAP command response.
		/// </summary>
		/// <remarks>
		/// Gets the IMAP command response.
		/// </remarks>
		/// <value>The IMAP command response.</value>
		public ImapCommandResponse Response {
			get; private set;
		}

		/// <summary>
		/// Gets the human-readable IMAP command response text.
		/// </summary>
		/// <remarks>
		/// Gets the human-readable IMAP command response text.
		/// </remarks>
		/// <value>The response text.</value>
		public string ResponseText {
			get; private set;
		}

		/// <summary>
		/// Get the error response codes received by the command.
		/// </summary>
		/// <remarks>
		/// <para>Gets the error response codes (such as <c>[OVERQUOTA]</c> or <c>[TRYCREATE]</c>) that
		/// the server included in the tagged and untagged responses to the command, in the order that
		/// they were received.</para>
		/// <para>Response codes are defined by various IMAP specifications such as
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a> and
		/// <a href="https://tools.ietf.org/html/rfc5530">rfc5530</a>. Response codes that carry
		/// additional data are represented by subclasses of <see cref="ImapResponseCode"/>, such as
		/// <see cref="BadUrlResponseCode"/>.</para>
		/// </remarks>
		/// <value>The error response codes. If the server did not include any error response codes, the list is empty.</value>
		public IReadOnlyList<ImapResponseCode> ResponseCodes {
			get; private set;
		}

		ImapResponseCode? GetPrimaryErrorCode ()
		{
			ImapResponseCode? code = null;

			// Prefer the error response code from the tagged response, falling back to the last untagged error response code.
			for (int i = ResponseCodes.Count - 1; i >= 0; i--) {
				if (ResponseCodes[i].IsError) {
					if (ResponseCodes[i].IsTagged)
						return ResponseCodes[i];

					code ??= ResponseCodes[i];
				}
			}

			return code;
		}

		/// <summary>
		/// Get the type of command error.
		/// </summary>
		/// <remarks>
		/// <para>Gets the type of command error based on the <see cref="ResponseCodes"/> and the <see cref="Response"/>.</para>
		/// <para>If the tagged response contained an error response code, that response code determines the
		/// type of error. Otherwise, the last untagged error response code is used. If neither is
		/// recognized, the type of error is determined by the <see cref="Response"/>.</para>
		/// </remarks>
		/// <value>The type of command error.</value>
		public override CommandErrorType ErrorType {
			get {
				var code = GetPrimaryErrorCode ();

				if (code != null) {
					switch (code.Type) {
					case ImapResponseCodeType.CanNot:
					case ImapResponseCodeType.UnknownCte:
					case ImapResponseCodeType.BadCharset:
					case ImapResponseCodeType.BadComparator:
					case ImapResponseCodeType.BadEvent:
					case ImapResponseCodeType.UseAttr:
						return CommandErrorType.NotSupported;
					case ImapResponseCodeType.ClientBug:
						return CommandErrorType.InvalidCommand;
					case ImapResponseCodeType.NoPerm:
					case ImapResponseCodeType.PrivacyRequired:
					case ImapResponseCodeType.AuthenticationFailed:
					case ImapResponseCodeType.AuthorizationFailed:
					case ImapResponseCodeType.Expired:
					case ImapResponseCodeType.ContactAdmin:
						return CommandErrorType.PermissionDenied;
					case ImapResponseCodeType.NonExistent:
					case ImapResponseCodeType.TryCreate:
					case ImapResponseCodeType.UndefinedFilter:
					case ImapResponseCodeType.BadUrl:
						return CommandErrorType.NotFound;
					case ImapResponseCodeType.AlreadyExists:
						return CommandErrorType.AlreadyExists;
					case ImapResponseCodeType.OverQuota:
						return CommandErrorType.QuotaExceeded;
					case ImapResponseCodeType.Limit:
					case ImapResponseCodeType.TooBig:
					case ImapResponseCodeType.MaxConvertMessages:
					case ImapResponseCodeType.MaxConvertParts:
						return CommandErrorType.LimitExceeded;
					case ImapResponseCodeType.InUse:
						return CommandErrorType.InUse;
					case ImapResponseCodeType.Unavailable:
					case ImapResponseCodeType.TempFail:
						return CommandErrorType.TemporaryFailure;
					case ImapResponseCodeType.ServerBug:
					case ImapResponseCodeType.Corruption:
						return CommandErrorType.ServerError;
					}
				}

				switch (Response) {
				case ImapCommandResponse.Bad: return CommandErrorType.InvalidCommand;
				case ImapCommandResponse.No: return CommandErrorType.Rejected;
				default: return CommandErrorType.Unknown;
				}
			}
		}

	}
}
