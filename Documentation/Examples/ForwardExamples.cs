//
// MimeVisitorExamples.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using MimeKit;

namespace MimeKit.Examples
{
	public static class ForwardExamples
	{
		#region ForwardAttached
		public static MimeMessage Forward (MimeMessage original, MailboxAddress from, IEnumerable<InternetAddress> to)
		{
			var message = new MimeMessage ();
			message.From.Add (from);
			message.To.AddRange (to);

			// set the forwarded subject
			if (!original.Subject.StartsWith ("FW:", StringComparison.OrdinalIgnoreCase))
				message.Subject = "FW: " + original.Subject;
			else
				message.Subject = original.Subject;

			// create the main textual body of the message
			var text = new TextPart ("plain") { Text = "Here's the forwarded message:" };

			// create the message/rfc822 attachment for the original message
			var rfc822 = new MessagePart { Message = original };

			// create a multipart/mixed container for the text body and the forwarded message
			var multipart = new Multipart ("mixed");
			multipart.Add (text);
			multipart.Add (rfc822);

			// set the multipart as the body of the message
			message.Body = multipart;

			return message;
		}
		#endregion ForwardAttached

		#region ForwardInline
		public static MimeMessage Forward (MimeMessage original, MailboxAddress from, IEnumerable<InternetAddress> to)
		{
			var message = new MimeMessage ();
			message.From.Add (from);
			message.To.AddRange (to);

			// set the forwarded subject
			if (!original.Subject.StartsWith ("FW:", StringComparison.OrdinalIgnoreCase))
				message.Subject = "FW: " + original.Subject;
			else
				message.Subject = original.Subject;

			// quote the original message text
			using (var text = new StringWriter ()) {
				text.WriteLine ();
				text.WriteLine ("-----Original Message-----");
				test.WriteLine ("From: {0}", original.From);
				text.WriteLine ("Sent: {0}", DateUtils.FormatDate (original.Date));
				text.WriteLine ("To: {0}", original.To);
				text.WriteLine ("Subject: {0}", original.Subject);
				text.WriteLine ();

				text.Write (original.TextBody);

				message.Body = new TextPart ("plain") {
					Text = text.ToString ()
				};
			}

			return message;
		}
		#endregion ForwardInline
	}
}