//
// Envelope.cs
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
using System.Text;
using System.Linq;

using MimeKit;
using MimeKit.Utils;

namespace MailKit {
	/// <summary>
	/// A message envelope containing a brief summary of the message.
	/// </summary>
	/// <remarks>
	/// The envelope of a message contains information such as the
	/// date the message was sent, the subject of the message,
	/// the sender of the message, who the message was sent to,
	/// which message(s) the message may be in reply to,
	/// and the message id.
	/// </remarks>
	public class Envelope
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Envelope"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Envelope"/>.
		/// </remarks>
		public Envelope ()
		{
			From = new InternetAddressList ();
			Sender = new InternetAddressList ();
			ReplyTo = new InternetAddressList ();
			To = new InternetAddressList ();
			Cc = new InternetAddressList ();
			Bcc = new InternetAddressList ();
		}

		/// <summary>
		/// Gets the address(es) that the message is from.
		/// </summary>
		/// <remarks>
		/// Gets the address(es) that the message is from.
		/// </remarks>
		/// <value>The address(es) that the message is from.</value>
		public InternetAddressList From {
			get; private set;
		}

		/// <summary>
		/// Gets the actual sender(s) of the message.
		/// </summary>
		/// <remarks>
		/// The senders may differ from the addresses in <see cref="From"/> if
		/// the message was sent by someone on behalf of someone else.
		/// </remarks>
		/// <value>The actual sender(s) of the message.</value>
		public InternetAddressList Sender {
			get; private set;
		}

		/// <summary>
		/// Gets the address(es) that replies should be sent to.
		/// </summary>
		/// <remarks>
		/// The senders of the message may prefer that replies are sent
		/// somewhere other than the address they used to send the message.
		/// </remarks>
		/// <value>The address(es) that replies should be sent to.</value>
		public InternetAddressList ReplyTo {
			get; private set;
		}

		/// <summary>
		/// Gets the list of addresses that the message was sent to.
		/// </summary>
		/// <remarks>
		/// Gets the list of addresses that the message was sent to.
		/// </remarks>
		/// <value>The address(es) that the message was sent to.</value>
		public InternetAddressList To {
			get; private set;
		}

		/// <summary>
		/// Gets the list of addresses that the message was carbon-copied to.
		/// </summary>
		/// <remarks>
		/// Gets the list of addresses that the message was carbon-copied to.
		/// </remarks>
		/// <value>The address(es) that the message was carbon-copied to.</value>
		public InternetAddressList Cc {
			get; private set;
		}

		/// <summary>
		/// Gets the list of addresses that the message was blind-carbon-copied to.
		/// </summary>
		/// <remarks>
		/// Gets the list of addresses that the message was blind-carbon-copied to.
		/// </remarks>
		/// <value>The address(es) that the message was carbon-copied to.</value>
		public InternetAddressList Bcc {
			get; private set;
		}

		/// <summary>
		/// The Message-Id that the message is replying to.
		/// </summary>
		/// <remarks>
		/// The Message-Id that the message is replying to.
		/// </remarks>
		/// <value>The Message-Id that the message is replying to.</value>
		public string InReplyTo {
			get; set;
		}

		/// <summary>
		/// Gets the date that the message was sent on, if available.
		/// </summary>
		/// <remarks>
		/// Gets the date that the message was sent on, if available.
		/// </remarks>
		/// <value>The date the message was sent.</value>
		public DateTimeOffset? Date {
			get; set;
		}

		/// <summary>
		/// Gets the ID of the message, if available.
		/// </summary>
		/// <remarks>
		/// Gets the ID of the message, if available.
		/// </remarks>
		/// <value>The message identifier.</value>
		public string MessageId {
			get; set;
		}

		/// <summary>
		/// Gets the subject of the message.
		/// </summary>
		/// <remarks>
		/// Gets the subject of the message.
		/// </remarks>
		/// <value>The subject.</value>
		public string Subject {
			get; set;
		}

		static void EncodeMailbox (StringBuilder builder, MailboxAddress mailbox)
		{
			builder.Append ('(');

			if (mailbox.Name != null)
				builder.AppendFormat ("{0} ", MimeUtils.Quote (mailbox.Name));
			else
				builder.Append ("NIL ");

			if (mailbox.Route.Count != 0)
				builder.AppendFormat ("\"{0}\" ", mailbox.Route);
			else
				builder.Append ("NIL ");

			if (mailbox.Address != null) {
				int at = mailbox.Address.LastIndexOf ('@');

				if (at >= 0) {
					var domain = mailbox.Address.Substring (at + 1);
					var user = mailbox.Address.Substring (0, at);

					builder.AppendFormat ("{0} {1}", MimeUtils.Quote (user), MimeUtils.Quote (domain));
				} else {
					builder.AppendFormat ("{0} NIL", MimeUtils.Quote (mailbox.Address));
				}
			} else {
				builder.Append ("NIL NIL");
			}

			builder.Append (')');
		}

		static void EncodeAddressList (StringBuilder builder, InternetAddressList list)
		{
			if (list.Count == 0) {
				builder.Append ("NIL");
				return;
			}

			builder.Append ('(');

			foreach (var mailbox in list.Mailboxes)
				EncodeMailbox (builder, mailbox);

			builder.Append (')');
		}

		internal void Encode (StringBuilder builder)
		{
			builder.Append ('(');

			if (Date.HasValue)
				builder.AppendFormat ("\"{0}\" ", DateUtils.FormatDate (Date.Value));
			else
				builder.Append ("NIL ");

			if (Subject != null)
				builder.AppendFormat ("{0} ", MimeUtils.Quote (Subject));
			else
				builder.Append ("NIL ");

			if (From.Count > 0) {
				EncodeAddressList (builder, From);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (Sender.Count > 0) {
				EncodeAddressList (builder, Sender);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (ReplyTo.Count > 0) {
				EncodeAddressList (builder, ReplyTo);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (To.Count > 0) {
				EncodeAddressList (builder, To);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (Cc.Count > 0) {
				EncodeAddressList (builder, Cc);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (Bcc.Count > 0) {
				EncodeAddressList (builder, Bcc);
				builder.Append (' ');
			} else {
				builder.Append ("NIL ");
			}

			if (InReplyTo != null) {
				if (InReplyTo.Length > 1 && InReplyTo[0] != '<' && InReplyTo[InReplyTo.Length - 1] != '>')
					builder.AppendFormat ("{0} ", MimeUtils.Quote ('<' + InReplyTo + '>'));
				else
					builder.AppendFormat ("{0} ", MimeUtils.Quote (InReplyTo));
			} else
				builder.Append ("NIL ");

			if (MessageId != null) {
				if (MessageId.Length > 1 && MessageId[0] != '<' && MessageId[MessageId.Length - 1] != '>')
					builder.AppendFormat ("{0}", MimeUtils.Quote ('<' + MessageId + '>'));
				else
					builder.AppendFormat ("{0}", MimeUtils.Quote (MessageId));
			} else
				builder.Append ("NIL");

			builder.Append (')');
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.Envelope"/>.
		/// </summary>
		/// <remarks>
		/// <para>The returned string can be parsed by <see cref="TryParse(string,out Envelope)"/>.</para>
		/// <note type="warning">The syntax of the string returned, while similar to IMAP's ENVELOPE syntax,
		/// is not completely compatible.</note>
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.Envelope"/>.</returns>
		public override string ToString ()
		{
			var builder = new StringBuilder ();

			Encode (builder);

			return builder.ToString ();
		}

		static bool TryParse (string text, ref int index, out string nstring)
		{
			nstring = null;

			while (index < text.Length && text[index] == ' ')
				index++;

			if (index >= text.Length)
				return false;

			if (text[index] != '"') {
				if (index + 3 <= text.Length && text.Substring (index, 3) == "NIL") {
					index += 3;
					return true;
				}

				return false;
			}

			var token = new StringBuilder ();
			bool escaped = false;

			index++;

			while (index < text.Length) {
				if (text[index] == '"' && !escaped)
					break;

				if (escaped || text[index] != '\\') {
					token.Append (text[index]);
					escaped = false;
				} else {
					escaped = true;
				}

				index++;
			}

			if (index >= text.Length)
				return false;

			nstring = token.ToString ();

			index++;

			return true;
		}

		static bool TryParse (string text, ref int index, out MailboxAddress mailbox)
		{
			string name, route, user, domain, address;
			DomainList domains;

			mailbox = null;

			if (text[index] != '(')
				return false;

			index++;

			if (!TryParse (text, ref index, out name))
				return false;

			if (!TryParse (text, ref index, out route))
				return false;

			if (!TryParse (text, ref index, out user))
				return false;

			if (!TryParse (text, ref index, out domain))
				return false;

			while (index < text.Length && text[index] == ' ')
				index++;

			if (index >= text.Length || text[index] != ')')
				return false;

			index++;

			address = domain != null ? user + "@" + domain : user;

			if (route != null && DomainList.TryParse (route, out domains))
				mailbox = new MailboxAddress (name, domains, address);
			else
				mailbox = new MailboxAddress (name, address);

			return true;
		}

		static bool TryParse (string text, ref int index, out InternetAddressList list)
		{
			MailboxAddress mailbox;

			list = null;

			while (index < text.Length && text[index] == ' ')
				index++;

			if (index >= text.Length)
				return false;

			if (text[index] != '(') {
				if (index + 3 <= text.Length && text.Substring (index, 3) == "NIL") {
					list = new InternetAddressList ();
					index += 3;
					return true;
				}

				return false;
			}

			index++;

			if (index >= text.Length)
				return false;

			list = new InternetAddressList ();

			do {
				if (text[index] == ')')
					break;

				if (!TryParse (text, ref index, out mailbox))
					return false;

				list.Add (mailbox);

				while (index < text.Length && text[index] == ' ')
					index++;
			} while (index < text.Length);

			if (index >= text.Length)
				return false;

			index++;

			return true;
		}

		internal static bool TryParse (string text, ref int index, out Envelope envelope)
		{
			InternetAddressList from, sender, replyto, to, cc, bcc;
			string inreplyto, messageid, subject, nstring;
			DateTimeOffset? date = null;

			envelope = null;

			while (index < text.Length && text[index] == ' ')
				index++;

			if (index >= text.Length || text[index] != '(') {
				if (index + 3 <= text.Length && text.Substring (index, 3) == "NIL") {
					index += 3;
					return true;
				}

				return false;
			}

			index++;

			if (!TryParse (text, ref index, out nstring))
				return false;

			if (nstring != null) {
				DateTimeOffset value;

				if (!DateUtils.TryParse (nstring, out value))
					return false;

				date = value;
			}

			if (!TryParse (text, ref index, out subject))
				return false;

			if (!TryParse (text, ref index, out from))
				return false;

			if (!TryParse (text, ref index, out sender))
				return false;

			if (!TryParse (text, ref index, out replyto))
				return false;

			if (!TryParse (text, ref index, out to))
				return false;

			if (!TryParse (text, ref index, out cc))
				return false;

			if (!TryParse (text, ref index, out bcc))
				return false;

			if (!TryParse (text, ref index, out inreplyto))
				return false;

			if (!TryParse (text, ref index, out messageid))
				return false;

			if (index >= text.Length || text[index] != ')')
				return false;

			index++;

			envelope = new Envelope {
				Date = date,
				Subject = subject,
				From = from,
				Sender = sender,
				ReplyTo = replyto,
				To = to,
				Cc = cc,
				Bcc = bcc,
				InReplyTo = inreplyto != null ? MimeUtils.EnumerateReferences (inreplyto).FirstOrDefault () ?? inreplyto : null,
				MessageId = messageid != null ? MimeUtils.EnumerateReferences (messageid).FirstOrDefault () ?? messageid : null
			};

			return true;
		}

		/// <summary>
		/// Tries to parse the given text into a new <see cref="MailKit.Envelope"/> instance.
		/// </summary>
		/// <remarks>
		/// <para>Parses an Envelope value from the specified text.</para>
		/// <note type="warning">This syntax, while similar to IMAP's ENVELOPE syntax, is not
		/// completely compatible.</note>
		/// </remarks>
		/// <returns><c>true</c>, if the envelope was successfully parsed, <c>false</c> otherwise.</returns>
		/// <param name="text">The text to parse.</param>
		/// <param name="envelope">The parsed envelope.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		public static bool TryParse (string text, out Envelope envelope)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			int index = 0;

			return TryParse (text, ref index, out envelope) && index == text.Length;
		}
	}
}
	