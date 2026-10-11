//
// ImapResponseCode.cs
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

#if NET5_0_OR_GREATER
using IReadOnlySetOfStrings = System.Collections.Generic.IReadOnlySet<string>;
#else
using IReadOnlySetOfStrings = System.Collections.Generic.ISet<string>;
#endif

namespace MailKit.Net.Imap {
	/// <summary>
	/// An enumeration of the known IMAP response codes.
	/// </summary>
	/// <remarks>
	/// <para>IMAP response codes are the bracketed atoms (such as <c>[TRYCREATE]</c>) that a server
	/// may include in a status response (<c>OK</c>, <c>NO</c>, <c>BAD</c>, <c>PREAUTH</c> or <c>BYE</c>)
	/// in order to provide additional, machine-readable information about the response.</para>
	/// <para>Response codes that are not recognized by MailKit are represented by
	/// <see cref="ImapResponseCodeType.Unknown"/>; the original atom is still available via the
	/// <see cref="ImapResponseCode.Atom"/> property.</para>
	/// </remarks>
	public enum ImapResponseCodeType : byte {
		/// <summary>
		/// The <c>ALERT</c> response code indicates that the human-readable text of the response
		/// is a special alert that must be presented to the user.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		Alert,

		/// <summary>
		/// The <c>BADCHARSET</c> response code indicates that a <c>SEARCH</c> failed because the
		/// specified charset is not supported by the server.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		BadCharset,

		/// <summary>
		/// The <c>CAPABILITY</c> response code contains a list of the server's capabilities.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		Capability,

		/// <summary>
		/// The <c>NEWNAME</c> response code indicates that the requested mailbox has been renamed.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc2060#section-7.1">rfc2060</a>
		/// (removed in rfc3501).</para>
		/// <para>This response code is represented by <see cref="NewNameResponseCode"/>.</para>
		/// </remarks>
		NewName,

		/// <summary>
		/// The <c>PARSE</c> response code indicates that the server failed to parse the headers
		/// of a message.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		Parse,

		/// <summary>
		/// The <c>PERMANENTFLAGS</c> response code contains the list of flags that the client can
		/// change permanently in the selected mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.</para>
		/// <para>This response code is represented by <see cref="PermanentFlagsResponseCode"/>.</para>
		/// </remarks>
		PermanentFlags,

		/// <summary>
		/// The <c>READ-ONLY</c> response code indicates that the mailbox is selected read-only.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		ReadOnly,

		/// <summary>
		/// The <c>READ-WRITE</c> response code indicates that the mailbox is selected read-write.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		ReadWrite,

		/// <summary>
		/// The <c>TRYCREATE</c> response code indicates that an <c>APPEND</c> or <c>COPY</c> failed
		/// because the target mailbox does not exist.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.
		/// </remarks>
		TryCreate,

		/// <summary>
		/// The <c>UIDNEXT</c> response code contains the next unique identifier value of the mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.</para>
		/// <para>This response code is represented by <see cref="UidNextResponseCode"/>.</para>
		/// </remarks>
		UidNext,

		/// <summary>
		/// The <c>UIDVALIDITY</c> response code contains the unique identifier validity value of the mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.</para>
		/// <para>This response code is represented by <see cref="UidValidityResponseCode"/>.</para>
		/// </remarks>
		UidValidity,

		/// <summary>
		/// The <c>UNSEEN</c> response code contains the sequence number of the first unseen message
		/// in the mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc3501#section-7.1">rfc3501</a>.</para>
		/// <para>This response code is represented by <see cref="UnseenResponseCode"/>.</para>
		/// </remarks>
		Unseen,

		/// <summary>
		/// The <c>REFERRAL</c> response code contains one or more IMAP URLs that the client should
		/// use instead.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc2221#section-4.2">rfc2221</a>.
		/// </remarks>
		Referral,

		/// <summary>
		/// The <c>UNKNOWN-CTE</c> response code indicates that the server does not know how to
		/// decode the Content-Transfer-Encoding of the requested body part.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc3516#section-4.3">rfc3516</a>.
		/// </remarks>
		UnknownCte,

		/// <summary>
		/// The <c>APPENDUID</c> response code contains the unique identifiers assigned to the
		/// appended message(s).
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc4315#section-3">rfc4315</a>.</para>
		/// <para>This response code is represented by <see cref="AppendUidResponseCode"/>.</para>
		/// </remarks>
		AppendUid,

		/// <summary>
		/// The <c>COPYUID</c> response code contains the unique identifiers of the source messages
		/// and the unique identifiers assigned to the copied messages.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc4315#section-3">rfc4315</a>.</para>
		/// <para>This response code is represented by <see cref="CopyUidResponseCode"/>.</para>
		/// </remarks>
		CopyUid,

		/// <summary>
		/// The <c>UIDNOTSTICKY</c> response code indicates that the selected mailbox does not
		/// support persistent unique identifiers.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc4315#section-3">rfc4315</a>.
		/// </remarks>
		UidNotSticky,

		/// <summary>
		/// The <c>URLMECH</c> response code contains the list of URLAUTH mechanisms supported by
		/// the server.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc4467#section-6">rfc4467</a>.
		/// </remarks>
		UrlMech,

		/// <summary>
		/// The <c>BADURL</c> response code indicates that a <c>CATENATE</c> URL could not be resolved.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc4469#section-5">rfc4469</a>.</para>
		/// <para>This response code is represented by <see cref="BadUrlResponseCode"/>.</para>
		/// </remarks>
		BadUrl,

		/// <summary>
		/// The <c>TOOBIG</c> response code indicates that the resulting message would be too large.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc4469#section-5">rfc4469</a>.
		/// </remarks>
		TooBig,

		/// <summary>
		/// The <c>HIGHESTMODSEQ</c> response code contains the highest mod-sequence value of all
		/// messages in the mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc7162#section-3.1.2.1">rfc7162</a>
		/// (originally rfc4551).</para>
		/// <para>This response code is represented by <see cref="HighestModSeqResponseCode"/>.</para>
		/// </remarks>
		HighestModSeq,

		/// <summary>
		/// The <c>MODIFIED</c> response code contains the messages that failed the
		/// <c>UNCHANGEDSINCE</c> test of a conditional <c>STORE</c>.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc7162#section-3.1.3">rfc7162</a>
		/// (originally rfc4551).</para>
		/// <para>This response code is represented by <see cref="ModifiedResponseCode"/>.</para>
		/// </remarks>
		Modified,

		/// <summary>
		/// The <c>NOMODSEQ</c> response code indicates that the selected mailbox does not support
		/// persistent mod-sequences.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc7162#section-3.1.2.2">rfc7162</a>
		/// (originally rfc4551).
		/// </remarks>
		NoModSeq,

		/// <summary>
		/// The <c>COMPRESSIONACTIVE</c> response code indicates that compression is already active.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc4978#section-3">rfc4978</a>.
		/// </remarks>
		CompressionActive,

		/// <summary>
		/// The <c>CLOSED</c> response code indicates that the previously selected mailbox has been closed.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc7162#section-3.2.11">rfc7162</a>
		/// (originally rfc5162).
		/// </remarks>
		Closed,

		/// <summary>
		/// The <c>NOTSAVED</c> response code indicates that the server is unable to save the
		/// search result.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5182#section-2.5">rfc5182</a>.
		/// </remarks>
		NotSaved,

		/// <summary>
		/// The <c>BADCOMPARATOR</c> response code indicates that the requested comparator is not supported.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5255#section-4.9">rfc5255</a>.
		/// </remarks>
		BadComparator,

		/// <summary>
		/// The <c>ANNOTATE</c> response code indicates that an annotation could not be stored.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5257#section-4.4">rfc5257</a>.</para>
		/// <para>This response code is represented by <see cref="AnnotateResponseCode"/>.</para>
		/// </remarks>
		Annotate,

		/// <summary>
		/// The <c>ANNOTATIONS</c> response code contains the annotation capabilities of the
		/// selected mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5257#section-4.3">rfc5257</a>.</para>
		/// <para>This response code is represented by <see cref="AnnotationsResponseCode"/>.</para>
		/// </remarks>
		Annotations,

		/// <summary>
		/// The <c>MAXCONVERTMESSAGES</c> response code indicates the maximum number of messages that
		/// can be converted by a single <c>CONVERT</c> command.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5259#section-4">rfc5259</a>.</para>
		/// <para>This response code is represented by <see cref="MaxConvertResponseCode"/>.</para>
		/// </remarks>
		MaxConvertMessages,

		/// <summary>
		/// The <c>MAXCONVERTPARTS</c> response code indicates the maximum number of body parts that
		/// can be converted by a single <c>CONVERT</c> command.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5259#section-4">rfc5259</a>.</para>
		/// <para>This response code is represented by <see cref="MaxConvertResponseCode"/>.</para>
		/// </remarks>
		MaxConvertParts,

		/// <summary>
		/// The <c>TEMPFAIL</c> response code indicates that a conversion failed due to a temporary
		/// condition.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5259#section-4">rfc5259</a>.
		/// </remarks>
		TempFail,

		/// <summary>
		/// The <c>NOUPDATE</c> response code indicates that the server will not send updates for
		/// the specified <c>CONTEXT</c> search.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5267#section-4.2">rfc5267</a>.</para>
		/// <para>This response code is represented by <see cref="NoUpdateResponseCode"/>.</para>
		/// </remarks>
		NoUpdate,

		/// <summary>
		/// The <c>METADATA</c> response code provides information about a <c>GETMETADATA</c> or
		/// <c>SETMETADATA</c> result.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5464#section-4">rfc5464</a>.</para>
		/// <para>This response code is represented by <see cref="MetadataResponseCode"/>.</para>
		/// </remarks>
		Metadata,

		/// <summary>
		/// The <c>NOTIFICATIONOVERFLOW</c> response code indicates that the server has disabled
		/// notifications because it could not keep up.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5465#section-5.8">rfc5465</a>.
		/// </remarks>
		NotificationOverflow,

		/// <summary>
		/// The <c>BADEVENT</c> response code indicates that the server does not support one or more
		/// of the requested <c>NOTIFY</c> events.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5465#section-5.9">rfc5465</a>.
		/// </remarks>
		BadEvent,

		/// <summary>
		/// The <c>UNDEFINED-FILTER</c> response code indicates that the requested search filter
		/// does not exist.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc5466#section-3.1">rfc5466</a>.</para>
		/// <para>This response code is represented by <see cref="UndefinedFilterResponseCode"/>.</para>
		/// </remarks>
		UndefinedFilter,

		/// <summary>
		/// The <c>UNAVAILABLE</c> response code indicates that a temporary failure occurred because
		/// a subsystem is down.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		Unavailable,

		/// <summary>
		/// The <c>AUTHENTICATIONFAILED</c> response code indicates that authentication failed.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		AuthenticationFailed,

		/// <summary>
		/// The <c>AUTHORIZATIONFAILED</c> response code indicates that authentication succeeded but
		/// the authenticated user is not permitted to use the requested authorization identity.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		AuthorizationFailed,

		/// <summary>
		/// The <c>EXPIRED</c> response code indicates that the credentials or data have expired.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		Expired,

		/// <summary>
		/// The <c>PRIVACYREQUIRED</c> response code indicates that the operation is not permitted
		/// without an encrypted connection.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		PrivacyRequired,

		/// <summary>
		/// The <c>CONTACTADMIN</c> response code indicates that the user should contact the
		/// system administrator.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		ContactAdmin,

		/// <summary>
		/// The <c>NOPERM</c> response code indicates that the user does not have permission to
		/// perform the operation.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		NoPerm,

		/// <summary>
		/// The <c>INUSE</c> response code indicates that the operation failed because a resource is
		/// in use by another process.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		InUse,

		/// <summary>
		/// The <c>EXPUNGEISSUED</c> response code indicates that another client expunged messages
		/// from the mailbox.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		ExpungeIssued,

		/// <summary>
		/// The <c>CORRUPTION</c> response code indicates that the server discovered corrupt data.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		Corruption,

		/// <summary>
		/// The <c>SERVERBUG</c> response code indicates that the server encountered a bug in itself.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		ServerBug,

		/// <summary>
		/// The <c>CLIENTBUG</c> response code indicates that the server believes the client has a bug.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		ClientBug,

		/// <summary>
		/// The <c>CANNOT</c> response code indicates that the operation violates an invariant of
		/// the server and can never succeed.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		CanNot,

		/// <summary>
		/// The <c>LIMIT</c> response code indicates that the operation exceeds a server limit.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		Limit,

		/// <summary>
		/// The <c>OVERQUOTA</c> response code indicates that the user would exceed a quota.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		OverQuota,

		/// <summary>
		/// The <c>ALREADYEXISTS</c> response code indicates that the target of the operation already exists.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		AlreadyExists,

		/// <summary>
		/// The <c>NONEXISTENT</c> response code indicates that the target of the operation does not exist.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc5530#section-3">rfc5530</a>.
		/// </remarks>
		NonExistent,

		/// <summary>
		/// The <c>USEATTR</c> response code indicates that the requested special-use attribute is
		/// not supported.
		/// </summary>
		/// <remarks>
		/// Defined in <a href="https://tools.ietf.org/html/rfc6154#section-6">rfc6154</a>.
		/// </remarks>
		UseAttr,

		/// <summary>
		/// The <c>MAILBOXID</c> response code contains the object identifier of a newly created mailbox.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc8474#section-4.1">rfc8474</a>.</para>
		/// <para>This response code is represented by <see cref="MailboxIdResponseCode"/>.</para>
		/// </remarks>
		MailboxId,

		/// <summary>
		/// The GMail-specific <c>WEBALERT</c> response code contains a URL that the user should visit,
		/// typically in order to re-enable access to their account.
		/// </summary>
		/// <remarks>
		/// This response code is represented by <see cref="WebAlertResponseCode"/>.
		/// </remarks>
		WebAlert,

		/// <summary>
		/// The <c>INPROGRESS</c> response code indicates that the server is still processing a command
		/// and may include an indication of how far it has progressed.
		/// </summary>
		/// <remarks>
		/// <para>Defined in <a href="https://tools.ietf.org/html/rfc9585">rfc9585</a>.</para>
		/// <para>This response code is represented by <see cref="InProgressResponseCode"/>.</para>
		/// </remarks>
		InProgress,

		/// <summary>
		/// An unrecognized response code.
		/// </summary>
		/// <remarks>
		/// The original atom is available via the <see cref="ImapResponseCode.Atom"/> property.
		/// </remarks>
		Unknown = 255
	}

	/// <summary>
	/// An IMAP response code.
	/// </summary>
	/// <remarks>
	/// <para>IMAP response codes are the bracketed atoms (such as <c>[TRYCREATE]</c>) that a server
	/// may include in a status response in order to provide additional, machine-readable information
	/// about the response.</para>
	/// <para>Response codes that carry additional data are represented by subclasses of
	/// <see cref="ImapResponseCode"/>, such as <see cref="CopyUidResponseCode"/>.</para>
	/// </remarks>
	/// <seealso cref="ImapCommandException.ResponseCodes"/>
	public class ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="ImapResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new <see cref="ImapResponseCode"/> for a response code that does not
		/// carry any additional data, such as <c>TRYCREATE</c> or <c>OVERQUOTA</c>.</para>
		/// <para>The <see cref="Type"/> is determined by <paramref name="atom"/> (case-insensitive)
		/// and <see cref="IsError"/> is determined by the <see cref="Type"/>. Unrecognized atoms
		/// are treated as errors.</para>
		/// </remarks>
		/// <param name="atom">The response code atom, such as <c>"TRYCREATE"</c>.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="atom"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="atom"/> is empty.</para>
		/// <para>-or-</para>
		/// <para><paramref name="atom"/> refers to a response code that is represented by a subclass
		/// of <see cref="ImapResponseCode"/>, such as <c>COPYUID</c>.</para>
		/// </exception>
		public ImapResponseCode (string atom, bool isTagged, string message)
		{
			if (atom is null)
				throw new ArgumentNullException (nameof (atom));

			if (atom.Length == 0)
				throw new ArgumentException ("The response code atom cannot be empty.", nameof (atom));

			if (message is null)
				throw new ArgumentNullException (nameof (message));

			var type = ImapEngine.GetResponseCodeType (atom);

			if (HasDerivedClass (type))
				throw new ArgumentException ($"The {atom.ToUpperInvariant ()} response code must be created using its specialized subclass.", nameof (atom));

			IsError = IsErrorByDefault (type);
			IsTagged = isTagged;
			Message = message;
			Atom = atom;
			Type = type;
		}

		internal ImapResponseCode (ImapResponseCodeType type, bool isError)
		{
			Message = string.Empty;
			Atom = string.Empty;
			IsError = isError;
			Type = type;
		}

		internal ImapResponseCode (ImapResponseCodeType type, bool isError, string atom, bool isTagged, string message)
		{
			if (message is null)
				throw new ArgumentNullException (nameof (message));

			IsTagged = isTagged;
			IsError = isError;
			Message = message;
			Atom = atom;
			Type = type;
		}

		/// <summary>
		/// Get the type of response code.
		/// </summary>
		/// <remarks>
		/// Gets the type of response code.
		/// </remarks>
		/// <value>The type of response code.</value>
		public ImapResponseCodeType Type {
			get;
		}

		/// <summary>
		/// Get whether the response code was in a tagged response.
		/// </summary>
		/// <remarks>
		/// Gets whether the response code was in a tagged response (the final response to a command)
		/// or in an untagged response.
		/// </remarks>
		/// <value><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</value>
		public bool IsTagged {
			get; internal set;
		}

		/// <summary>
		/// Get whether the response code indicates an error.
		/// </summary>
		/// <remarks>
		/// Gets whether the response code indicates an error.
		/// </remarks>
		/// <value><see langword="true" /> if the response code indicates an error; otherwise, <see langword="false" />.</value>
		public bool IsError {
			get; internal set;
		}

		/// <summary>
		/// Get the human-readable text that followed the response code.
		/// </summary>
		/// <remarks>
		/// Gets the human-readable text that followed the response code.
		/// </remarks>
		/// <value>The human-readable text.</value>
		public string Message {
			get; internal set;
		}

		/// <summary>
		/// Get the response code atom.
		/// </summary>
		/// <remarks>
		/// Gets the response code atom as it was sent by the server (such as <c>"TRYCREATE"</c>).
		/// This is especially useful when the <see cref="Type"/> is <see cref="ImapResponseCodeType.Unknown"/>.
		/// </remarks>
		/// <value>The response code atom.</value>
		public string Atom {
			get; internal set;
		}

		static bool HasDerivedClass (ImapResponseCodeType type)
		{
			switch (type) {
			case ImapResponseCodeType.NewName:
			case ImapResponseCodeType.PermanentFlags:
			case ImapResponseCodeType.UidNext:
			case ImapResponseCodeType.UidValidity:
			case ImapResponseCodeType.Unseen:
			case ImapResponseCodeType.AppendUid:
			case ImapResponseCodeType.CopyUid:
			case ImapResponseCodeType.BadUrl:
			case ImapResponseCodeType.HighestModSeq:
			case ImapResponseCodeType.Modified:
			case ImapResponseCodeType.Annotate:
			case ImapResponseCodeType.Annotations:
			case ImapResponseCodeType.MaxConvertMessages:
			case ImapResponseCodeType.MaxConvertParts:
			case ImapResponseCodeType.NoUpdate:
			case ImapResponseCodeType.Metadata:
			case ImapResponseCodeType.UndefinedFilter:
			case ImapResponseCodeType.MailboxId:
			case ImapResponseCodeType.WebAlert:
			case ImapResponseCodeType.InProgress:
				return true;
			default:
				return false;
			}
		}

		internal static bool IsErrorByDefault (ImapResponseCodeType type)
		{
			switch (type) {
			case ImapResponseCodeType.Alert:
			case ImapResponseCodeType.Capability:
			case ImapResponseCodeType.NewName:
			case ImapResponseCodeType.PermanentFlags:
			case ImapResponseCodeType.ReadOnly:
			case ImapResponseCodeType.ReadWrite:
			case ImapResponseCodeType.UidNext:
			case ImapResponseCodeType.UidValidity:
			case ImapResponseCodeType.Unseen:
			case ImapResponseCodeType.Referral:
			case ImapResponseCodeType.AppendUid:
			case ImapResponseCodeType.CopyUid:
			case ImapResponseCodeType.UidNotSticky:
			case ImapResponseCodeType.UrlMech:
			case ImapResponseCodeType.HighestModSeq:
			case ImapResponseCodeType.Modified:
			case ImapResponseCodeType.NoModSeq:
			case ImapResponseCodeType.Closed:
			case ImapResponseCodeType.Annotations:
			case ImapResponseCodeType.NotificationOverflow:
			case ImapResponseCodeType.MailboxId:
			case ImapResponseCodeType.WebAlert:
			case ImapResponseCodeType.InProgress:
				return false;
			default:
				return true;
			}
		}

		internal static ImapResponseCode Create (ImapResponseCodeType type)
		{
			switch (type) {
			case ImapResponseCodeType.NewName:              return new NewNameResponseCode (type);
			case ImapResponseCodeType.PermanentFlags:       return new PermanentFlagsResponseCode (type);
			case ImapResponseCodeType.UidNext:              return new UidNextResponseCode (type);
			case ImapResponseCodeType.UidValidity:          return new UidValidityResponseCode (type);
			case ImapResponseCodeType.Unseen:               return new UnseenResponseCode (type);
			case ImapResponseCodeType.AppendUid:            return new AppendUidResponseCode (type);
			case ImapResponseCodeType.CopyUid:              return new CopyUidResponseCode (type);
			case ImapResponseCodeType.BadUrl:               return new BadUrlResponseCode (type);
			case ImapResponseCodeType.HighestModSeq:        return new HighestModSeqResponseCode (type);
			case ImapResponseCodeType.Modified:             return new ModifiedResponseCode (type);
			case ImapResponseCodeType.Annotate:             return new AnnotateResponseCode (type);
			case ImapResponseCodeType.Annotations:          return new AnnotationsResponseCode (type);
			case ImapResponseCodeType.MaxConvertMessages:   return new MaxConvertResponseCode (type);
			case ImapResponseCodeType.MaxConvertParts:      return new MaxConvertResponseCode (type);
			case ImapResponseCodeType.NoUpdate:             return new NoUpdateResponseCode (type);
			case ImapResponseCodeType.Metadata:             return new MetadataResponseCode (type);
			case ImapResponseCodeType.UndefinedFilter:      return new UndefinedFilterResponseCode (type);
			case ImapResponseCodeType.MailboxId:            return new MailboxIdResponseCode (type);
			case ImapResponseCodeType.WebAlert:             return new WebAlertResponseCode (type);
			case ImapResponseCodeType.InProgress:           return new InProgressResponseCode (type);
			default:                                        return new ImapResponseCode (type, IsErrorByDefault (type));
			}
		}
	}

	/// <summary>
	/// A <c>NEWNAME</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>NEWNAME</c> response code indicates that the requested mailbox has been renamed.
	/// </remarks>
	public sealed class NewNameResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="NewNameResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NewNameResponseCode"/>.
		/// </remarks>
		/// <param name="oldName">The old name of the mailbox.</param>
		/// <param name="newName">The new name of the mailbox.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="oldName"/>, <paramref name="newName"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public NewNameResponseCode (string oldName, string newName, bool isTagged, string message) : base (ImapResponseCodeType.NewName, false, "NEWNAME", isTagged, message)
		{
			OldName = oldName ?? throw new ArgumentNullException (nameof (oldName));
			NewName = newName ?? throw new ArgumentNullException (nameof (newName));
		}

		internal NewNameResponseCode (ImapResponseCodeType type) : base (type, false)
		{
			OldName = string.Empty;
			NewName = string.Empty;
		}

		/// <summary>
		/// Get the old name of the mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the old name of the mailbox.
		/// </remarks>
		/// <value>The old name of the mailbox.</value>
		public string OldName {
			get; internal set;
		}

		/// <summary>
		/// Get the new name of the mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the new name of the mailbox.
		/// </remarks>
		/// <value>The new name of the mailbox.</value>
		public string NewName {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>PERMANENTFLAGS</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>PERMANENTFLAGS</c> response code contains the list of flags that the client can change
	/// permanently in the selected mailbox.
	/// </remarks>
	public sealed class PermanentFlagsResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="PermanentFlagsResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="PermanentFlagsResponseCode"/>.
		/// </remarks>
		/// <param name="flags">The permanent message flags.</param>
		/// <param name="keywords">The permanent keywords.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="keywords"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public PermanentFlagsResponseCode (MessageFlags flags, IEnumerable<string> keywords, bool isTagged, string message) : base (ImapResponseCodeType.PermanentFlags, false, "PERMANENTFLAGS", isTagged, message)
		{
			if (keywords is null)
				throw new ArgumentNullException (nameof (keywords));

			KeywordSet = new HashSet<string> (keywords, StringComparer.Ordinal);
			Flags = flags;
		}

		internal PermanentFlagsResponseCode (ImapResponseCodeType type) : base (type, false)
		{
			KeywordSet = new HashSet<string> (StringComparer.Ordinal);
		}

		internal HashSet<string> KeywordSet { get; }

		/// <summary>
		/// Get the permanent keywords.
		/// </summary>
		/// <remarks>
		/// Gets the user-defined keywords that the client can change permanently.
		/// </remarks>
		/// <value>The permanent keywords.</value>
		public IReadOnlySetOfStrings Keywords {
			get { return KeywordSet; }
		}

		/// <summary>
		/// Get the permanent message flags.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message flags that the client can change permanently.</para>
		/// <para>If <see cref="MessageFlags.UserDefined"/> is set, the client can create new
		/// keywords.</para>
		/// </remarks>
		/// <value>The permanent message flags.</value>
		public MessageFlags Flags {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>UIDNEXT</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>UIDNEXT</c> response code contains the next unique identifier value of the mailbox.
	/// </remarks>
	public sealed class UidNextResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="UidNextResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UidNextResponseCode"/>.
		/// </remarks>
		/// <param name="uid">The next unique identifier value.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public UidNextResponseCode (UniqueId uid, bool isTagged, string message) : base (ImapResponseCodeType.UidNext, false, "UIDNEXT", isTagged, message)
		{
			Uid = uid;
		}

		internal UidNextResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the next unique identifier value.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifier value that will be assigned to the next message added to the mailbox.
		/// </remarks>
		/// <value>The next unique identifier value.</value>
		public UniqueId Uid {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>UIDVALIDITY</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>UIDVALIDITY</c> response code contains the unique identifier validity value of the mailbox.
	/// </remarks>
	public class UidValidityResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="UidValidityResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UidValidityResponseCode"/>.
		/// </remarks>
		/// <param name="uidValidity">The unique identifier validity value.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public UidValidityResponseCode (uint uidValidity, bool isTagged, string message) : this (ImapResponseCodeType.UidValidity, "UIDVALIDITY", uidValidity, isTagged, message)
		{
		}

		internal UidValidityResponseCode (ImapResponseCodeType type, string atom, uint uidValidity, bool isTagged, string message) : base (type, false, atom, isTagged, message)
		{
			UidValidity = uidValidity;
		}

		internal UidValidityResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the unique identifier validity value.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifier validity value of the mailbox.
		/// </remarks>
		/// <value>The unique identifier validity value.</value>
		public uint UidValidity {
			get; internal set;
		}
	}

	/// <summary>
	/// An <c>UNSEEN</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>UNSEEN</c> response code contains the index of the first unseen message in the mailbox.
	/// </remarks>
	public sealed class UnseenResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="UnseenResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UnseenResponseCode"/>.
		/// </remarks>
		/// <param name="index">The zero-based index of the first unseen message.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is negative.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public UnseenResponseCode (int index, bool isTagged, string message) : base (ImapResponseCodeType.Unseen, false, "UNSEEN", isTagged, message)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));

			Index = index;
		}

		internal UnseenResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the index of the first unseen message.
		/// </summary>
		/// <remarks>
		/// Gets the zero-based index of the first unseen message in the mailbox.
		/// </remarks>
		/// <value>The zero-based index of the first unseen message.</value>
		public int Index {
			get; internal set;
		}
	}

	/// <summary>
	/// An <c>APPENDUID</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>APPENDUID</c> response code contains the unique identifiers assigned to the appended message(s).
	/// </remarks>
	public sealed class AppendUidResponseCode : UidValidityResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="AppendUidResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AppendUidResponseCode"/>.
		/// </remarks>
		/// <param name="uidValidity">The unique identifier validity value of the destination mailbox.</param>
		/// <param name="uidSet">The unique identifiers assigned to the appended message(s).</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uidSet"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public AppendUidResponseCode (uint uidValidity, UniqueIdSet uidSet, bool isTagged, string message) : base (ImapResponseCodeType.AppendUid, "APPENDUID", uidValidity, isTagged, message)
		{
			UidSet = uidSet ?? throw new ArgumentNullException (nameof (uidSet));
		}

		internal AppendUidResponseCode (ImapResponseCodeType type) : base (type)
		{
			UidSet = new UniqueIdSet ();
		}

		/// <summary>
		/// Get the unique identifiers assigned to the appended message(s).
		/// </summary>
		/// <remarks>
		/// Gets the unique identifiers assigned to the appended message(s).
		/// </remarks>
		/// <value>The unique identifiers.</value>
		public UniqueIdSet UidSet {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>COPYUID</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>COPYUID</c> response code contains the unique identifiers of the source messages and the
	/// unique identifiers assigned to the copied messages.
	/// </remarks>
	public sealed class CopyUidResponseCode : UidValidityResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="CopyUidResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="CopyUidResponseCode"/>.
		/// </remarks>
		/// <param name="uidValidity">The unique identifier validity value of the destination mailbox.</param>
		/// <param name="sourceUidSet">The unique identifiers of the source messages.</param>
		/// <param name="destinationUidSet">The unique identifiers assigned to the copied messages.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="sourceUidSet"/>, <paramref name="destinationUidSet"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public CopyUidResponseCode (uint uidValidity, UniqueIdSet sourceUidSet, UniqueIdSet destinationUidSet, bool isTagged, string message) : base (ImapResponseCodeType.CopyUid, "COPYUID", uidValidity, isTagged, message)
		{
			SourceUidSet = sourceUidSet ?? throw new ArgumentNullException (nameof (sourceUidSet));
			DestinationUidSet = destinationUidSet ?? throw new ArgumentNullException (nameof (destinationUidSet));
		}

		internal CopyUidResponseCode (ImapResponseCodeType type) : base (type)
		{
			SourceUidSet = new UniqueIdSet ();
			DestinationUidSet = new UniqueIdSet ();
		}

		/// <summary>
		/// Get the unique identifiers of the source messages.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifiers of the source messages, in the same order as <see cref="DestinationUidSet"/>.
		/// </remarks>
		/// <value>The unique identifiers of the source messages.</value>
		public UniqueIdSet SourceUidSet {
			get; internal set;
		}

		/// <summary>
		/// Get the unique identifiers assigned to the copied messages.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifiers assigned to the copied messages, in the same order as <see cref="SourceUidSet"/>.
		/// </remarks>
		/// <value>The unique identifiers assigned to the copied messages.</value>
		public UniqueIdSet DestinationUidSet {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>BADURL</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>BADURL</c> response code indicates that a <c>CATENATE</c> URL could not be resolved.
	/// </remarks>
	public sealed class BadUrlResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="BadUrlResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="BadUrlResponseCode"/>.
		/// </remarks>
		/// <param name="badUrl">The URL that could not be resolved.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="badUrl"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public BadUrlResponseCode (string badUrl, bool isTagged, string message) : base (ImapResponseCodeType.BadUrl, true, "BADURL", isTagged, message)
		{
			BadUrl = badUrl ?? throw new ArgumentNullException (nameof (badUrl));
		}

		internal BadUrlResponseCode (ImapResponseCodeType type) : base (type, true)
		{
			BadUrl = string.Empty;
		}

		/// <summary>
		/// Get the URL that could not be resolved.
		/// </summary>
		/// <remarks>
		/// Gets the URL that could not be resolved.
		/// </remarks>
		/// <value>The URL.</value>
		public string BadUrl {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>HIGHESTMODSEQ</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>HIGHESTMODSEQ</c> response code contains the highest mod-sequence value of all messages in the mailbox.
	/// </remarks>
	public sealed class HighestModSeqResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="HighestModSeqResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="HighestModSeqResponseCode"/>.
		/// </remarks>
		/// <param name="highestModSeq">The highest mod-sequence value.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public HighestModSeqResponseCode (ulong highestModSeq, bool isTagged, string message) : base (ImapResponseCodeType.HighestModSeq, false, "HIGHESTMODSEQ", isTagged, message)
		{
			HighestModSeq = highestModSeq;
		}

		internal HighestModSeqResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the highest mod-sequence value.
		/// </summary>
		/// <remarks>
		/// Gets the highest mod-sequence value of all messages in the mailbox.
		/// </remarks>
		/// <value>The highest mod-sequence value.</value>
		public ulong HighestModSeq {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>MODIFIED</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>MODIFIED</c> response code contains the messages that failed the <c>UNCHANGEDSINCE</c>
	/// test of a conditional <c>STORE</c>.
	/// </remarks>
	public sealed class ModifiedResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="ModifiedResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ModifiedResponseCode"/>.
		/// </remarks>
		/// <param name="uidSet">The unique identifiers of the messages that were not modified.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uidSet"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public ModifiedResponseCode (UniqueIdSet uidSet, bool isTagged, string message) : base (ImapResponseCodeType.Modified, false, "MODIFIED", isTagged, message)
		{
			UidSet = uidSet ?? throw new ArgumentNullException (nameof (uidSet));
		}

		internal ModifiedResponseCode (ImapResponseCodeType type) : base (type, false)
		{
			UidSet = new UniqueIdSet ();
		}

		/// <summary>
		/// Get the messages that were not modified.
		/// </summary>
		/// <remarks>
		/// <para>Gets the messages that failed the <c>UNCHANGEDSINCE</c> test and so were not modified.</para>
		/// <para>When the <c>STORE</c> command used sequence numbers, the values are message sequence
		/// numbers rather than unique identifiers.</para>
		/// </remarks>
		/// <value>The messages that were not modified.</value>
		public UniqueIdSet UidSet {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>MAXCONVERTMESSAGES</c> or <c>MAXCONVERTPARTS</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>MAXCONVERTMESSAGES</c> and <c>MAXCONVERTPARTS</c> response codes indicate the maximum
	/// number of messages or body parts that can be converted by a single <c>CONVERT</c> command.
	/// </remarks>
	public sealed class MaxConvertResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="MaxConvertResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MaxConvertResponseCode"/>.
		/// </remarks>
		/// <param name="type">Either <see cref="ImapResponseCodeType.MaxConvertMessages"/> or <see cref="ImapResponseCodeType.MaxConvertParts"/>.</param>
		/// <param name="maxConvert">The maximum number of messages or body parts.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="type"/> is not <see cref="ImapResponseCodeType.MaxConvertMessages"/> or <see cref="ImapResponseCodeType.MaxConvertParts"/>.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public MaxConvertResponseCode (ImapResponseCodeType type, uint maxConvert, bool isTagged, string message) : base (ValidateType (type), true, type == ImapResponseCodeType.MaxConvertMessages ? "MAXCONVERTMESSAGES" : "MAXCONVERTPARTS", isTagged, message)
		{
			MaxConvert = maxConvert;
		}

		internal MaxConvertResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}

		static ImapResponseCodeType ValidateType (ImapResponseCodeType type)
		{
			if (type != ImapResponseCodeType.MaxConvertMessages && type != ImapResponseCodeType.MaxConvertParts)
				throw new ArgumentOutOfRangeException (nameof (type));

			return type;
		}

		/// <summary>
		/// Get the maximum number of messages or body parts.
		/// </summary>
		/// <remarks>
		/// Gets the maximum number of messages or body parts that can be converted by a single <c>CONVERT</c> command.
		/// </remarks>
		/// <value>The maximum number of messages or body parts.</value>
		public uint MaxConvert {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>NOUPDATE</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>NOUPDATE</c> response code indicates that the server will not send updates for the
	/// specified <c>CONTEXT</c> search.
	/// </remarks>
	public sealed class NoUpdateResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="NoUpdateResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NoUpdateResponseCode"/>.
		/// </remarks>
		/// <param name="tag">The tag of the search command.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="tag"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public NoUpdateResponseCode (string tag, bool isTagged, string message) : base (ImapResponseCodeType.NoUpdate, true, "NOUPDATE", isTagged, message)
		{
			Tag = tag ?? throw new ArgumentNullException (nameof (tag));
		}

		internal NoUpdateResponseCode (ImapResponseCodeType type) : base (type, true)
		{
			Tag = string.Empty;
		}

		/// <summary>
		/// Get the tag of the search command.
		/// </summary>
		/// <remarks>
		/// Gets the tag of the search command that the server will no longer send updates for.
		/// </remarks>
		/// <value>The tag.</value>
		public string Tag {
			get; internal set;
		}
	}

	/// <summary>
	/// The reason that an annotation could not be stored.
	/// </summary>
	/// <remarks>
	/// The reason that an annotation could not be stored.
	/// </remarks>
	/// <seealso cref="AnnotateResponseCode"/>
	public enum AnnotateResponseCodeSubType
	{
		/// <summary>
		/// The annotation value is too large (<c>ANNOTATE TOOBIG</c>).
		/// </summary>
		TooBig,

		/// <summary>
		/// The message already has the maximum number of annotations (<c>ANNOTATE TOOMANY</c>).
		/// </summary>
		TooMany
	}

	/// <summary>
	/// An <c>ANNOTATE</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>ANNOTATE</c> response code indicates that an annotation could not be stored.
	/// </remarks>
	public sealed class AnnotateResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="AnnotateResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotateResponseCode"/>.
		/// </remarks>
		/// <param name="subType">The reason that the annotation could not be stored.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public AnnotateResponseCode (AnnotateResponseCodeSubType subType, bool isTagged, string message) : base (ImapResponseCodeType.Annotate, true, "ANNOTATE", isTagged, message)
		{
			SubType = subType;
		}

		internal AnnotateResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}

		/// <summary>
		/// Get the reason that the annotation could not be stored.
		/// </summary>
		/// <remarks>
		/// Gets the reason that the annotation could not be stored.
		/// </remarks>
		/// <value>The reason.</value>
		public AnnotateResponseCodeSubType SubType {
			get; internal set;
		}
	}

	/// <summary>
	/// An <c>ANNOTATIONS</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>ANNOTATIONS</c> response code contains the annotation capabilities of the selected mailbox.
	/// </remarks>
	public sealed class AnnotationsResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="AnnotationsResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationsResponseCode"/>.
		/// </remarks>
		/// <param name="access">The annotation access level.</param>
		/// <param name="scopes">The supported annotation scopes.</param>
		/// <param name="maxSize">The maximum size of an annotation value, or <c>0</c> if there is no limit.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public AnnotationsResponseCode (AnnotationAccess access, AnnotationScope scopes, uint maxSize, bool isTagged, string message) : base (ImapResponseCodeType.Annotations, false, "ANNOTATIONS", isTagged, message)
		{
			Access = access;
			Scopes = scopes;
			MaxSize = maxSize;
		}

		internal AnnotationsResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the annotation access level.
		/// </summary>
		/// <remarks>
		/// Gets the annotation access level of the selected mailbox.
		/// </remarks>
		/// <value>The annotation access level.</value>
		public AnnotationAccess Access {
			get; internal set;
		}

		/// <summary>
		/// Get the supported annotation scopes.
		/// </summary>
		/// <remarks>
		/// Gets the annotation scopes supported by the selected mailbox.
		/// </remarks>
		/// <value>The supported annotation scopes.</value>
		public AnnotationScope Scopes {
			get; internal set;
		}

		/// <summary>
		/// Get the maximum size of an annotation value.
		/// </summary>
		/// <remarks>
		/// Gets the maximum size of an annotation value, in bytes, or <c>0</c> if there is no limit.
		/// </remarks>
		/// <value>The maximum size of an annotation value.</value>
		public uint MaxSize {
			get; internal set;
		}
	}

	/// <summary>
	/// The kind of <c>METADATA</c> response code.
	/// </summary>
	/// <remarks>
	/// The kind of <c>METADATA</c> response code.
	/// </remarks>
	/// <seealso cref="MetadataResponseCode"/>
	public enum MetadataResponseCodeSubType
	{
		/// <summary>
		/// Some entries were not returned because their values were larger than the requested
		/// <c>MAXSIZE</c> (<c>METADATA LONGENTRIES</c>). This is not an error.
		/// </summary>
		LongEntries,

		/// <summary>
		/// The value is larger than the maximum size supported by the server (<c>METADATA MAXSIZE</c>).
		/// </summary>
		MaxSize,

		/// <summary>
		/// The server cannot store any more entries (<c>METADATA TOOMANY</c>).
		/// </summary>
		TooMany,

		/// <summary>
		/// The server does not support private entries on the mailbox (<c>METADATA NOPRIVATE</c>).
		/// </summary>
		NoPrivate
	}

	/// <summary>
	/// A <c>METADATA</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>METADATA</c> response code provides information about a <c>GETMETADATA</c> or
	/// <c>SETMETADATA</c> result.
	/// </remarks>
	public sealed class MetadataResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="MetadataResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MetadataResponseCode"/>. The <see cref="ImapResponseCode.IsError"/>
		/// property is <see langword="false" /> for <see cref="MetadataResponseCodeSubType.LongEntries"/>
		/// and <see langword="true" /> otherwise.
		/// </remarks>
		/// <param name="subType">The kind of <c>METADATA</c> response code.</param>
		/// <param name="value">The value associated with the response code, or <c>0</c> if there is none.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public MetadataResponseCode (MetadataResponseCodeSubType subType, uint value, bool isTagged, string message) : base (ImapResponseCodeType.Metadata, subType != MetadataResponseCodeSubType.LongEntries, "METADATA", isTagged, message)
		{
			SubType = subType;
			Value = value;
		}

		internal MetadataResponseCode (ImapResponseCodeType type) : base (type, true)
		{
		}

		/// <summary>
		/// Get the kind of <c>METADATA</c> response code.
		/// </summary>
		/// <remarks>
		/// Gets the kind of <c>METADATA</c> response code.
		/// </remarks>
		/// <value>The kind of <c>METADATA</c> response code.</value>
		public MetadataResponseCodeSubType SubType {
			get; internal set;
		}

		/// <summary>
		/// Get the value associated with the response code.
		/// </summary>
		/// <remarks>
		/// <para>For <see cref="MetadataResponseCodeSubType.LongEntries"/>, this is the size of the
		/// largest entry value that was not returned.</para>
		/// <para>For <see cref="MetadataResponseCodeSubType.MaxSize"/>, this is the maximum value
		/// size supported by the server.</para>
		/// <para>Otherwise, the value is <c>0</c>.</para>
		/// </remarks>
		/// <value>The value.</value>
		public uint Value {
			get; internal set;
		}
	}

	/// <summary>
	/// An <c>UNDEFINED-FILTER</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>UNDEFINED-FILTER</c> response code indicates that the requested search filter does not exist.
	/// </remarks>
	public sealed class UndefinedFilterResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="UndefinedFilterResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UndefinedFilterResponseCode"/>.
		/// </remarks>
		/// <param name="name">The name of the undefined filter.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public UndefinedFilterResponseCode (string name, bool isTagged, string message) : base (ImapResponseCodeType.UndefinedFilter, true, "UNDEFINED-FILTER", isTagged, message)
		{
			Name = name ?? throw new ArgumentNullException (nameof (name));
		}

		internal UndefinedFilterResponseCode (ImapResponseCodeType type) : base (type, true)
		{
			Name = string.Empty;
		}

		/// <summary>
		/// Get the name of the undefined filter.
		/// </summary>
		/// <remarks>
		/// Gets the name of the undefined filter.
		/// </remarks>
		/// <value>The name of the filter.</value>
		public string Name {
			get; internal set;
		}
	}

	/// <summary>
	/// A <c>MAILBOXID</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>MAILBOXID</c> response code contains the object identifier of a newly created mailbox.
	/// </remarks>
	public sealed class MailboxIdResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="MailboxIdResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailboxIdResponseCode"/>.
		/// </remarks>
		/// <param name="mailboxId">The object identifier of the mailbox.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mailboxId"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public MailboxIdResponseCode (string mailboxId, bool isTagged, string message) : base (ImapResponseCodeType.MailboxId, false, "MAILBOXID", isTagged, message)
		{
			MailboxId = mailboxId ?? throw new ArgumentNullException (nameof (mailboxId));
		}

		internal MailboxIdResponseCode (ImapResponseCodeType type) : base (type, false)
		{
			MailboxId = string.Empty;
		}

		/// <summary>
		/// Get the object identifier of the mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the object identifier of the mailbox.
		/// </remarks>
		/// <value>The object identifier of the mailbox.</value>
		public string MailboxId {
			get; internal set;
		}
	}

	/// <summary>
	/// A GMail-specific <c>WEBALERT</c> response code.
	/// </summary>
	/// <remarks>
	/// The <c>WEBALERT</c> response code contains a URL that the user should visit, typically in order
	/// to re-enable access to their account.
	/// </remarks>
	public sealed class WebAlertResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="WebAlertResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="WebAlertResponseCode"/>.
		/// </remarks>
		/// <param name="webUri">The URL that the user should visit.</param>
		/// <param name="isTagged"><see langword="true" /> if the response code was in a tagged response; otherwise, <see langword="false" />.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="webUri"/> or <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		public WebAlertResponseCode (Uri webUri, bool isTagged, string message) : base (ImapResponseCodeType.WebAlert, false, "WEBALERT", isTagged, message)
		{
			WebUri = webUri ?? throw new ArgumentNullException (nameof (webUri));
		}

		internal WebAlertResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the URL that the user should visit.
		/// </summary>
		/// <remarks>
		/// Gets the URL that the user should visit, or <see langword="null" /> if the server sent a
		/// URL that could not be parsed.
		/// </remarks>
		/// <value>The URL.</value>
		public Uri? WebUri {
			get; internal set;
		}
	}

	/// <summary>
	/// An <c>INPROGRESS</c> response code.
	/// </summary>
	/// <remarks>
	/// <para>The <c>INPROGRESS</c> response code is sent by the server in an untagged <c>OK</c> response
	/// to indicate that it is still processing a command and, optionally, how far it has progressed.</para>
	/// <para>Defined in <a href="https://tools.ietf.org/html/rfc9585">rfc9585</a>.</para>
	/// </remarks>
	public sealed class InProgressResponseCode : ImapResponseCode
	{
		/// <summary>
		/// Initialize a new instance of the <see cref="InProgressResponseCode"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="InProgressResponseCode"/>.
		/// </remarks>
		/// <param name="tag">The tag of the command that is in progress, or <see langword="null" /> if unknown.</param>
		/// <param name="progress">The number of items processed so far, or <see langword="null" /> if unknown.</param>
		/// <param name="goal">The total number of items to process, or <see langword="null" /> if unknown.</param>
		/// <param name="message">The human-readable text that followed the response code.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="progress"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="goal"/> is less than <c>1</c>, less than <paramref name="progress"/>,
		/// or specified without a <paramref name="progress"/>.</para>
		/// </exception>
		public InProgressResponseCode (string? tag, long? progress, long? goal, string message) : base (ImapResponseCodeType.InProgress, false, "INPROGRESS", false, message)
		{
			if (progress < 0)
				throw new ArgumentOutOfRangeException (nameof (progress));

			if (goal.HasValue && (goal.Value < 1 || !progress.HasValue || progress.Value > goal.Value))
				throw new ArgumentOutOfRangeException (nameof (goal));

			Progress = progress;
			Goal = goal;
			Tag = tag;
		}

		internal InProgressResponseCode (ImapResponseCodeType type) : base (type, false)
		{
		}

		/// <summary>
		/// Get the tag of the command that is in progress.
		/// </summary>
		/// <remarks>
		/// Gets the tag of the command that is in progress, or <see langword="null" /> if the server
		/// did not specify it.
		/// </remarks>
		/// <value>The tag.</value>
		public string? Tag {
			get; internal set;
		}

		/// <summary>
		/// Get the number of items processed so far.
		/// </summary>
		/// <remarks>
		/// Gets the number of items that the server has processed so far, or <see langword="null" />
		/// if the server did not specify it.
		/// </remarks>
		/// <value>The number of items processed so far.</value>
		public long? Progress {
			get; internal set;
		}

		/// <summary>
		/// Get the total number of items to process.
		/// </summary>
		/// <remarks>
		/// <para>Gets the total number of items that the server expects to process, or <see langword="null" />
		/// if the server did not specify it.</para>
		/// <para>If the server is unable to count items, then it may report progress as a percentage, in
		/// which case the goal will be <c>100</c>.</para>
		/// </remarks>
		/// <value>The total number of items to process.</value>
		public long? Goal {
			get; internal set;
		}
	}
}