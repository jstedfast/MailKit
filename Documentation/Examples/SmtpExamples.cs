//
// SmtpExamples.cs
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
using System.Collections.Generic;

using MimeKit;
using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;

namespace MailKit.Examples {
	public static class SmtpExamples
	{
		#region ProtocolLogger
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient (new ProtocolLogger ("smtp.log"))) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Send (message);

				client.Disconnect (true);
			}
		}
		#endregion

		#region Capabilities
		public static void PrintCapabilities ()
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				if (client.Capabilities.HasFlag (SmtpCapabilities.Authentication)) {
					var mechanisms = string.Join (", ", client.AuthenticationMechanisms);
					Console.WriteLine ("The SMTP server supports the following SASL mechanisms: {0}", mechanisms);
					client.Authenticate ("username", "password");
				}

				if (client.Capabilities.HasFlag (SmtpCapabilities.Size))
					Console.WriteLine ("The SMTP server has a size restriction on messages: {0}.", client.MaxSize);

				if (client.Capabilities.HasFlag (SmtpCapabilities.Dsn))
					Console.WriteLine ("The SMTP server supports delivery-status notifications.");

				if (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime))
					Console.WriteLine ("The SMTP server supports Content-Transfer-Encoding: 8bit");

				if (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime))
					Console.WriteLine ("The SMTP server supports Content-Transfer-Encoding: binary");

				if (client.Capabilities.HasFlag (SmtpCapabilities.UTF8))
					Console.WriteLine ("The SMTP server supports UTF-8 in message headers.");

				client.Disconnect (true);
			}
		}
		#endregion

		#region ExceptionHandling
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				try {
					client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);
				} catch (SmtpCommandException ex) {
					Console.WriteLine ("Error trying to connect: {0}", ex.Message);
					Console.WriteLine ("\tStatusCode: {0}", ex.StatusCode);
					return;
				} catch (SmtpProtocolException ex) {
					Console.WriteLine ("Protocol error while trying to connect: {0}", ex.Message);
					return;
				}

				// Note: Not all SMTP servers support authentication, but GMail does.
				if (client.Capabilities.HasFlag (SmtpCapabilities.Authentication)) {
					try {
						client.Authenticate ("username", "password");
					} catch (AuthenticationException ex) {
						Console.WriteLine ("Invalid user name or password.");
						return;
					} catch (SmtpCommandException ex) {
						Console.WriteLine ("Error trying to authenticate: {0}", ex.Message);
						Console.WriteLine ("\tStatusCode: {0}", ex.StatusCode);
						return;
					} catch (SmtpProtocolException ex) {
						Console.WriteLine ("Protocol error while trying to authenticate: {0}", ex.Message);
						return;
					}
				}

				try {
					client.Send (message);
				} catch (SmtpCommandException ex) {
					Console.WriteLine ("Error sending message: {0}", ex.Message);
					Console.WriteLine ("\tStatusCode: {0}", ex.StatusCode);

					switch (ex.ErrorCode) {
					case SmtpErrorCode.RecipientNotAccepted:
						Console.WriteLine ("\tRecipient not accepted: {0}", ex.Mailbox);
						break;
					case SmtpErrorCode.SenderNotAccepted:
						Console.WriteLine ("\tSender not accepted: {0}", ex.Mailbox);
						break;
					case SmtpErrorCode.MessageNotAccepted:
						Console.WriteLine ("\tMessage not accepted.");
						break;
					}
				} catch (SmtpProtocolException ex) {
					Console.WriteLine ("Protocol error while sending message: {0}", ex.Message);
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region SendMessage
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Send (message);

				client.Disconnect (true);
			}
		}
		#endregion

		#region SendMessageUri
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				// Note: since GMail requires SSL at connection time, use the "smtps"
				// protocol instead of "smtp".
				var uri = new Uri ("smtps://smtp.gmail.com:465");

				client.Connect (uri);

				client.Authenticate ("username", "password");

				client.Send (message);

				client.Disconnect (true);
			}
		}
		#endregion

		#region SendMessageWithOptions
		public static void SendMessageWithOptions (MimeMessage message)
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				var options = FormatOptions.Default.Clone ();

				if (client.Capabilities.HasFlag (SmtpCapabilities.UTF8))
					options.International = true;

				client.Send (options, message);

				client.Disconnect (true);
			}
		}
		#endregion

		#region SendMessages
		public static void SendMessages (IList<MimeMessage> messages)
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				foreach (var message in messages) {
					client.Send (message);
				}

				client.Disconnect (true);
			}
		}
		#endregion

		#region DeliveryStatusNotification
		public class DSNSmtpClient : SmtpClient
		{
			public DSNSmtpClient ()
			{
			}

			/// <summary>
			/// Get the envelope identifier to be used with delivery status notifications.
			/// </summary>
			/// <remarks>
			/// <para>The envelope identifier, if non-empty, is useful in determining which message
			/// a delivery status notification was issued for.</para>
			/// <para>The envelope identifier should be unique and may be up to 100 characters in
			/// length, but must consist only of printable ASCII characters and no white space.</para>
			/// <para>For more information, see rfc3461, section 4.4.</para>
			/// </remarks>
			/// <returns>The envelope identifier.</returns>
			/// <param name="message">The message.</param>
			protected override string GetEnvelopeId (MimeMessage message)
			{
				// Since you will want to be able to map whatever identifier you return here to the
				// message, the obvious identifier to use is probably the Message-Id value.
				return message.MessageId;
			}

			/// <summary>
			/// Get the types of delivery status notification desired for the specified recipient mailbox.
			/// </summary>
			/// <remarks>
			/// Gets the types of delivery status notification desired for the specified recipient mailbox.
			/// </remarks>
			/// <returns>The desired delivery status notification type.</returns>
			/// <param name="message">The message being sent.</param>
			/// <param name="mailbox">The mailbox.</param>
			protected override DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
			{
				// In this example, we only want to be notified of failures to deliver to a mailbox.
				// If you also want to be notified of delays or successful deliveries, simply bitwise-or
				// whatever combination of flags you want to be notified about.
				return DeliveryStatusNotification.Failure;
			}
		}
		#endregion
	}
}
