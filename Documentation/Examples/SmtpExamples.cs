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
		#region SaveToPickupDirectory
		public static void SaveToPickupDirectory (MimeMessage message, string pickupDirectory)
		{
			do {
				// Generate a random file name to save the message to.
				var path = Path.Combine (pickupDirectory, Guid.NewGuid ().ToString () + ".eml");
				Stream stream;

				try {
					// Attempt to create the new file.
					stream = File.Open (path, FileMode.CreateNew);
				} catch (IOException) {
					// If the file already exists, try again with a new Guid.
					if (File.Exists (path))
						continue;

					// Otherwise, fail immediately since it probably means that there is
					// no graceful way to recover from this error.
					throw;
				}

				try {
					using (stream) {
						// IIS pickup directories expect the message to be "byte-stuffed"
						// which means that lines beginning with "." need to be escaped
						// by adding an extra "." to the beginning of the line.
						//
						// Use an SmtpDataFilter "byte-stuff" the message as it is written
						// to the file stream. This is the same process that an SmtpClient
						// would use when sending the message in a `DATA` command.
						using (var filtered = new FilteredStream (stream)) {
							filtered.Add (new SmtpDataFilter ());

							// Make sure to write the message in DOS (<CR><LF>) format.
							var options = FormatOptions.Default.Clone ();
							options.NewLineFormat = NewLineFormat.Dos;

							message.WriteTo (options, filtered);
							filtered.Flush ();
							return;
						}
					}
				} catch {
					// An exception here probably means that the disk is full.
					//
					// Delete the file that was created above so that incomplete files are not
					// left behind for IIS to send accidentally.
					File.Delete (path);
					throw;
				}
			} while (true);
		}
		#endif

		#region ProtocolLogger
		public static void SendMessage (MimeMessage message)
		{
			using (var client = new SmtpClient (new ProtocolLogger ("smtp.log"))) {
				client.Connect ("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

				client.Authenticate ("username", "password");

				client.Send (message);

				client.Disconnect (true);
			}

			// Example log:
			//
			// Connected to smtps://smtp.gmail.com:465/
			// S: 220 smtp.gmail.com ESMTP w81sm22057166qkg.43 - gsmtp
			// C: EHLO [192.168.1.220]
			// S: 250-smtp.gmail.com at your service, [192.168.1.220]
			// S: 250-SIZE 35882577
			// S: 250-8BITMIME
			// S: 250-AUTH LOGIN PLAIN XOAUTH2 PLAIN-CLIENTTOKEN OAUTHBEARER XOAUTH
			// S: 250-ENHANCEDSTATUSCODES
			// S: 250-PIPELINING
			// S: 250-CHUNKING
			// S: 250 SMTPUTF8
			// C: AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk
			// S: 235 2.7.0 Accepted
			// C: MAIL FROM:<from.addr@gmail.com>
			// C: RCPT TO:<to.addr@gmail.com>
			// S: 250 2.1.0 OK w81sm22057166qkg.43 - gsmtp
			// S: 250 2.1.5 OK w81sm22057166qkg.43 - gsmtp
			// C: DATA
			// S: 354  Go ahead w81sm22057166qkg.43 - gsmtp
			// C: From: "LastName, FirstName" <from.addr@gmail.com>
			// C: Date: Thu, 27 Dec 2018 10:55:18 -0500
			// C: Subject: This is a test message
			// C: Message-Id: <C7GVXWE3C6U4.7ZQ0K9OUHTDP1@MADUNLA-SP4.northamerica.corp.microsoft.com>
			// C: To: "LastName, FirstName" <to.addr@gmail.com>
			// C: MIME-Version: 1.0
			// C: Content-Type: multipart/alternative; boundary="=-CToJI+AD2gS6z+fFlzDvhg=="
			// C: 
			// C: --=-CToJI+AD2gS6z+fFlzDvhg==
			// C: Content-Type: text/plain; charset=utf-8
			// C: Content-Transfer-Encoding: quoted-printable
			// C: 
			// C: This is the text/plain message body.
			// C: --=-CToJI+AD2gS6z+fFlzDvhg==
			// C: Content-Type: text/html; charset=utf-8
			// C: Content-Transfer-Encoding: quoted-printable
			// C: 
			// C: <html><body><center>This is the <b>text/html</b> message body.</center></body></html>
			// C: --=-CToJI+AD2gS6z+fFlzDvhg==--
			// C: 
			// C: .
			// S: 250 2.0.0 OK 1545926120 w81sm22057166qkg.43 - gsmtp
			// C: QUIT
			// S: 221 2.0.0 closing connection w81sm22057166qkg.43 - gsmtp
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
