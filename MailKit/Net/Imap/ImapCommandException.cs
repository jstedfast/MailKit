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

#if SERIALIZABLE
using System.Security;
using System.Runtime.Serialization;
#endif

namespace MailKit.Net.Imap {
	/// <summary>
	/// An exception that is thrown when an IMAP command returns NO or BAD.
	/// </summary>
	/// <remarks>
	/// The exception that is thrown when an IMAP command fails. Unlike a <see cref="ImapProtocolException"/>,
	/// a <see cref="ImapCommandException"/> does not require the <see cref="ImapClient"/> to be reconnected.
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class ImapCommandException : CommandException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/> from the serialized data.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecuritySafeCritical]
		protected ImapCommandException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			Response = (ImapCommandResponse) info.GetValue ("Response", typeof (ImapCommandResponse));
			ResponseText = info.GetString ("ResponseText");
			ResponseCode = info.GetString ("ResponseCode");
		}
#endif

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
			ImapResponseCode? code = null;
			string? reason = null;
			string message;

			// Prefer the error response code from the tagged response, falling back to the last untagged error response code.
			for (int i = ic.RespCodes.Count - 1; i >= 0; i--) {
				if (ic.RespCodes[i].IsError) {
					if (ic.RespCodes[i].IsTagged) {
						code = ic.RespCodes[i];
						break;
					}

					code ??= ic.RespCodes[i];
				}
			}

			if (string.IsNullOrEmpty (ic.ResponseText)) {
				for (int i = ic.RespCodes.Count - 1; i >= 0; i--) {
					if (ic.RespCodes[i].IsError && !string.IsNullOrEmpty (ic.RespCodes[i].Message)) {
						reason = ic.RespCodes[i].Message;
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

			var responseCode = string.IsNullOrEmpty (code?.Atom) ? null : code!.Atom.ToUpperInvariant ();

			return ic.Exception != null ? new ImapCommandException (ic.Response, responseCode, reason, message, ic.Exception) : new ImapCommandException (ic.Response, responseCode, reason, message);
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
		public ImapCommandException (ImapCommandResponse response, string responseText, string message, Exception innerException) : this (response, null, responseText, message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseCode">The IMAP response code (e.g. <c>"OVERQUOTA"</c>), if any.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public ImapCommandException (ImapCommandResponse response, string? responseCode, string responseText, string message, Exception innerException) : base (message, innerException)
		{
			ResponseCode = responseCode;
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
		public ImapCommandException (ImapCommandResponse response, string responseText, string message) : this (response, null, responseText, message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapCommandException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapCommandException"/>.
		/// </remarks>
		/// <param name="response">The IMAP command response.</param>
		/// <param name="responseCode">The IMAP response code (e.g. <c>"OVERQUOTA"</c>), if any.</param>
		/// <param name="responseText">The human-readable response text.</param>
		/// <param name="message">The error message.</param>
		public ImapCommandException (ImapCommandResponse response, string? responseCode, string responseText, string message) : base (message)
		{
			ResponseCode = responseCode;
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
			ResponseText = responseText;
			Response = response;
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
		/// Get the IMAP response code, if any.
		/// </summary>
		/// <remarks>
		/// <para>Gets the IMAP response code that the server included in its error response, if any.</para>
		/// <para>Response codes are defined by various IMAP specifications such as
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a> and
		/// <a href="https://tools.ietf.org/html/rfc5530">rfc5530</a> and include values such as
		/// <c>"OVERQUOTA"</c>, <c>"NONEXISTENT"</c> or <c>"INUSE"</c>.</para>
		/// </remarks>
		/// <value>The upper-case response code or <see langword="null" /> if the server did not include an error response code.</value>
		public string? ResponseCode {
			get; private set;
		}

		/// <summary>
		/// Get the type of command error.
		/// </summary>
		/// <remarks>
		/// Gets the type of command error based on the <see cref="ResponseCode"/> and the <see cref="Response"/>.
		/// </remarks>
		/// <value>The type of command error.</value>
		public override CommandErrorType ErrorType {
			get {
				if (ResponseCode != null) {
					switch (ImapEngine.GetResponseCodeType (ResponseCode)) {
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

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <see langword="null" />.
		/// </exception>
		[SecurityCritical]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);

			info.AddValue ("Response", Response, typeof (ImapCommandResponse));
			info.AddValue ("ResponseText", ResponseText);
			info.AddValue ("ResponseCode", ResponseCode);
		}
#endif
	}
}
