//
// SmtpSendResultExamples.cs
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

using MimeKit;

using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;

namespace MailKit.Examples {
	public static class SmtpSendResultExamples
	{
		#region SendResult
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.example.com", 587, SecureSocketOptions.StartTls);
				client.Authenticate ("username", "password");

				var result = (SmtpSendResult) client.Send (new SmtpSendRequest (message));

				// The ResponseText is the server's final response to the DATA (or BDAT) command.
				// Many servers include a queue identifier that can be useful for tracking the message.
				Console.WriteLine ("{0}: {1}", result.StatusCode, result.ResponseText);

				foreach (var recipient in result.AcceptedRecipients)
					Console.WriteLine ("Accepted: {0}", recipient);

				client.Disconnect (true);
			}
		}
		#endregion

		#region LenientSendRequest
		public class LenientSendRequest : SmtpSendRequest
		{
			public LenientSendRequest (MimeMessage message) : base (message)
			{
			}

			public override void OnRecipientNotAccepted (MailboxAddress recipient, SmtpResponse response)
			{
				// Don't throw; the recipient will be listed in SendResult.RejectedRecipients instead.
				Console.WriteLine ("{0} was rejected: {1} {2}", recipient, response.StatusCode, response.ResponseText);
			}
		}

		public static void SendToAvailableRecipients (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.example.com", 587, SecureSocketOptions.StartTls);
				client.Authenticate ("username", "password");

				try {
					var result = client.Send (new LenientSendRequest (message));

					foreach (var recipient in result.RejectedRecipients)
						Console.WriteLine ("Not delivered to: {0}", recipient);
				} catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.MessageNotAccepted) {
					// If *none* of the recipients were accepted, an SmtpCommandException is still thrown.
					Console.WriteLine ("None of the recipients were accepted.");
				}

				client.Disconnect (true);
			}
		}
		#endregion
	}
}
