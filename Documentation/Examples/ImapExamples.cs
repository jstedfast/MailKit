//
// ImapExamples.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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
using System.Collections;
using System.Collections.Generic;

using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	public static class ImapExamples
	{
		#region ProtocolLogger
		public static void DownloadMessages ()
		{
			using (var client = new ImapClient (new ProtocolLogger ("imap.log"))) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				var uids = client.Inbox.Search (SearchQuery.All);

				foreach (var uid in uids) {
					var message = client.Inbox.GetMessage (uid);

					// write the message to a file
					message.WriteTo (string.Format ("{0}.msg", uid));
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region Capabilities
		public static void Capabilities ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				var mechanisms = string.Join (", ", client.AuthenticationMechanisms);
				Console.WriteLine ("The IMAP server supports the following SASL authentication mechanisms: {0}", mechanisms);

				client.Authenticate ("username", "password");

				if (client.Capabilities.HasFlag (ImapCapabilities.Id)) {
					var clientImplementation = new ImapImplementation { Name = "MailKit", Version = "1.0" };
					var serverImplementation = client.Identify (clientImplementation);

					Console.WriteLine ("Server implementation details:");
					foreach (var property in serverImplementation.Properties)
						Console.WriteLine ("  {0} = {1}", property.Key, property.Value);
				}

				if (client.Capabilities.HasFlag (ImapCapabilities.Acl)) {
					Console.WriteLine ("The IMAP server supports Access Control Lists.");

					Console.WriteLine ("The IMAP server supports the following access rights: {0}", client.Rights);

					Console.WriteLine ("The Inbox has the following access controls:");
					var acl = client.Inbox.GetAccessControlList ();
					foreach (var ac in acl)
						Console.WriteLine ("  {0} = {1}", ac.Name, ac.Rights);

					var myRights = client.Inbox.GetMyAccessRights ();
					Console.WriteLine ("Your current rights for the Inbox folder are: {0}", myRights);
				}

				if (client.Capabilities.HasFlag (ImapCapabilities.Quota)) {
					Console.WriteLine ("The IMAP server supports quotas.");

					Console.WriteLine ("The current quota for the Inbox is:");
					var quota = client.Inbox.GetQuota ();

					if (quota.StorageLimit.HasValue && quota.StorageLimit.Value)
						Console.WriteLine ("  Limited by storage space. Using {0} out of {1} bytes.", quota.CurrentStorageSize.Value, quota.StorageLimit.Value);

					if (quota.MessageLimit.HasValue && quota.MessageLimit.Value)
						Console.WriteLine ("  Limited by the number of messages. Using {0} out of {1} bytes.", quota.CurrentMessageCount.Value, quota.MessageLimit.Value);

					Console.WriteLine ("The quota root is: {0}", quota.QuotaRoot);
				}

				if (client.Capabilities.HasFlag (ImapCapabilities.Thread)) {
					if (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.OrderedSubject))
						Console.WriteLine ("The IMAP server supports threading by subject.");
					if (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References))
						Console.WriteLine ("The IMAP server supports threading by references.");
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region DownloadMessages
		public static void DownloadMessages ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Inbox.Open (FolderAccess.ReadOnly);

				var uids = client.Inbox.Search (SearchQuery.All);

				foreach (var uid in uids) {
					var message = client.Inbox.GetMessage (uid);

					// write the message to a file
					message.WriteTo (string.Format ("{0}.msg", uid));
				}

				client.Disconnect (true);
			}
		}
		#endregion
	}
}
