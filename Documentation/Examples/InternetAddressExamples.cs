//
// InternetAddressExamples.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;

namespace MimeKit.Examples
{
	public static class InternetAddressExamples
	{
		#region ParseMailbox
		public static void ParseMailbox ()
		{
			// Parse() throws a ParseException if the text is not a valid mailbox address.
			var mailbox = MailboxAddress.Parse ("\"Bing, Chandler\" <chandler@friends.com>");

			Console.WriteLine ("Name: {0}", mailbox.Name);           // Bing, Chandler
			Console.WriteLine ("Address: {0}", mailbox.Address);     // chandler@friends.com
			Console.WriteLine ("LocalPart: {0}", mailbox.LocalPart); // chandler
			Console.WriteLine ("Domain: {0}", mailbox.Domain);       // friends.com

			// ToString() quotes the name if needed. Passing true for the encode argument also
			// rfc2047-encodes any non-ASCII characters, making the result suitable for use in
			// a message header.
			Console.WriteLine (mailbox.ToString (true));             // "Bing, Chandler" <chandler@friends.com>
		}
		#endregion

		#region TryParseMailbox
		public static bool IsValidMailbox (string text)
		{
			// TryParse() is the best way to validate user input since it does not throw.
			if (!MailboxAddress.TryParse (text, out var mailbox))
				return false;

			// The MimeKit address parser is fairly lenient by default. For example, it will
			// accept addresses that do not have a domain, such as "postmaster".
			return !string.IsNullOrEmpty (mailbox.Domain);
		}

		public static bool IsValidMailboxStrict (string text)
		{
			// For stricter validation, use ParserOptions with an RfcComplianceMode of Strict and
			// disallow addresses without a domain.
			var options = new ParserOptions {
				AddressParserComplianceMode = RfcComplianceMode.Strict,
				AllowAddressesWithoutDomain = false,
				AllowUnquotedCommasInAddresses = false
			};

			return MailboxAddress.TryParse (options, text, out _);
		}
		#endregion

		#region ParseAddressList
		public static void AddRecipients (MimeMessage message, string recipients)
		{
			// InternetAddressList.Parse() can parse a comma-separated list of addresses such as
			// what a user might type into the "To:" field of a mail client.
			var addresses = InternetAddressList.Parse (recipients);

			message.To.AddRange (addresses);
		}
		#endregion

		#region EnumerateMailboxes
		public static IList<string> GetRecipientAddresses (MimeMessage message)
		{
			var addresses = new List<string> ();

			// An address list may contain both mailboxes and groups (GroupAddress). The Mailboxes
			// property flattens the list, recursively including the members of each group, which
			// makes it convenient for collecting every recipient address.
			foreach (var mailbox in message.To.Mailboxes.Concat (message.Cc.Mailboxes).Concat (message.Bcc.Mailboxes))
				addresses.Add (mailbox.Address);

			// Alternatively, the list can be enumerated directly in order to handle each type
			// of address differently.
			foreach (var address in message.To) {
				if (address is GroupAddress group)
					Console.WriteLine ("Group: {0} ({1} members)", group.Name, group.Members.Count);
				else if (address is MailboxAddress mailbox)
					Console.WriteLine ("Mailbox: {0}", mailbox.Address);
			}

			return addresses;
		}
		#endregion

		#region GroupAddress
		public static void AddGroupRecipients (MimeMessage message)
		{
			// A group address is a named list of mailboxes. It will be serialized as:
			// Friends: Rachel Green <rachel@friends.com>, Ross Geller <ross@friends.com>;
			var friends = new GroupAddress ("Friends");
			friends.Members.Add (new MailboxAddress ("Rachel Green", "rachel@friends.com"));
			friends.Members.Add (new MailboxAddress ("Ross Geller", "ross@friends.com"));

			message.To.Add (friends);

			// A group with no members is a common way of hiding the list of recipients
			// and will be serialized as: undisclosed-recipients: ;
			message.Cc.Add (new GroupAddress ("undisclosed-recipients"));
		}
		#endregion

		#region InternationalAddresses
		public static void PrintInternationalAddress ()
		{
			var mailbox = new MailboxAddress ("Jörg Müller", "jörg@bücher.example");

			// An address is international if the local-part or domain contain non-ASCII characters.
			Console.WriteLine ("IsInternational: {0}", mailbox.IsInternational); // True

			// GetAddress (true) converts the domain into its ASCII-compatible (punycode) form.
			Console.WriteLine (mailbox.GetAddress (true)); // jörg@xn--bcher-kva.example

			// When encoding for use in a message header, the name is rfc2047-encoded and the domain
			// is punycode-encoded. If the SMTP server supports the SMTPUTF8 extension, enable the
			// FormatOptions.International option in order to leave the UTF-8 text as-is.
			var options = FormatOptions.Default.Clone ();
			options.International = true;

			// =?utf-8?b?SsO2cmcgTcO8bGxlcg==?= <jörg@xn--bcher-kva.example>
			Console.WriteLine (mailbox.ToString (FormatOptions.Default, true));

			// Jörg Müller <jörg@bücher.example>
			Console.WriteLine (mailbox.ToString (options, true));
		}
		#endregion
	}
}
