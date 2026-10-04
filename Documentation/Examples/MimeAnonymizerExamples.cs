//
// MimeAnonymizerExamples.cs
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
	public static class MimeAnonymizerExamples
	{
		#region AnonymizeMessage
		public static void AnonymizeMessage (string inputFileName, string outputFileName)
		{
			// The MimeAnonymizer is useful for sharing a message that triggers a bug (for example,
			// in a bug report) without revealing any personal information. Letters and digits are
			// replaced with 'x' while punctuation, whitespace, line lengths and the MIME structure
			// of the message are preserved so that the anonymized message is likely to reproduce
			// the same parser behavior as the original.
			var message = MimeMessage.Load (inputFileName);
			var anonymizer = new MimeAnonymizer ();

			using (var stream = File.Create (outputFileName))
				anonymizer.Anonymize (message, stream);
		}

		// Given the following message as input:
		//
		// Received: from mail.friends.com (mail.friends.com [192.168.1.10])
		// 	by mx.example.com with ESMTPS id abc123
		// 	for <chandler@example.com>; Mon, 6 Apr 2026 10:15:32 -0400
		// From: Joey Tribbiani <joey@friends.com>
		// To: Chandler Bing <chandler@example.com>
		// Subject: Paintball this weekend?
		// Date: Mon, 6 Apr 2026 10:15:30 -0400
		// Message-Id: <20260406141530.12345@friends.com>
		// MIME-Version: 1.0
		// Content-Type: multipart/mixed; boundary="=-boundary-1"
		//
		// --=-boundary-1
		// Content-Type: text/plain; charset=utf-8
		// Content-Transfer-Encoding: 7bit
		//
		// Hey Chandler,
		//
		// Monica and I are going to play paintball on Saturday. You in?
		//
		// -- Joey
		//
		// --=-boundary-1
		// Content-Type: application/pdf; name="map.pdf"
		// Content-Disposition: attachment; filename="map.pdf"
		// Content-Transfer-Encoding: base64
		//
		// JVBERi0xLjQKJcfsj6IKNSAwIG9iago8PC9MZW5ndGggNiAwIFI+PgpzdHJlYW0K
		//
		// --=-boundary-1--
		//
		// The anonymized output will be:
		//
		// Received: from xxxx.xxxxxxx.xxx (xxxx.xxxxxxx.xxx [xxx.xxx.x.xx])
		// 	by xx.xxxxxxx.xxx with xxxxxx id xxxxxx
		// 	for <xxxxxxxx@xxxxxxx.xxx>; Mon, 6 Apr 2026 10:15:32 -0400
		// From: xxxx xxxxxxxxx <xxxx@xxxxxxx.xxx>
		// To: xxxxxxxx xxxx <xxxxxxxx@xxxxxxx.xxx>
		// Subject: xxxxxxxxx xxxx xxxxxxx?
		// Date: Mon, 6 Apr 2026 10:15:30 -0400
		// Message-Id: <xxxxxxxxxxxxxx.xxxxx@xxxxxxx.xxx>
		// MIME-Version: 1.0
		// Content-Type: multipart/mixed; boundary="=-boundary-1"
		//
		// --=-boundary-1
		// Content-Type: text/plain; charset=utf-8
		// Content-Transfer-Encoding: 7bit
		//
		// xxx xxxxxxxxx
		//
		// xxxxxx xxx x xxx xxxxx xx xxxx xxxxxxxxx xx xxxxxxxxx xxx xxx
		//
		// xx xxxx
		//
		// --=-boundary-1
		// Content-Type: application/pdf; name="xxxxxxx"
		// Content-Disposition: attachment; filename="xxxxxxx"
		// Content-Transfer-Encoding: base64
		//
		// xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
		//
		// --=-boundary-1--
		//
		// Note: Dates, as well as structural values such as the MIME-Version, Content-Type,
		// Content-Transfer-Encoding and boundary markers, are left as-is.
		#endregion

		#region PreserveHeaders
		public static byte[] AnonymizeMessagePreservingSubject (MimeMessage message)
		{
			var anonymizer = new MimeAnonymizer ();

			// Headers that do not contain sensitive information (or that are needed to reproduce a
			// problem) can be excluded from anonymization. Header names are case-insensitive.
			anonymizer.PreserveHeaders.Add ("Subject");

			// Use Unix line endings so that the output is easy to paste into a bug report.
			var options = FormatOptions.Default.Clone ();
			options.NewLineFormat = NewLineFormat.Unix;

			using (var memory = new MemoryStream ()) {
				anonymizer.Anonymize (options, message, memory);

				return memory.ToArray ();
			}
		}

		// Given a message with the following headers as input:
		//
		// Received: from mail.friends.com (mail.friends.com [192.168.1.10])
		// 	by mx.example.com with ESMTPS id abc123
		// 	for <chandler@example.com>; Mon, 6 Apr 2026 10:15:32 -0400
		// From: Joey Tribbiani <joey@friends.com>
		// To: Chandler Bing <chandler@example.com>
		// Subject: Paintball this weekend?
		// Date: Mon, 6 Apr 2026 10:15:30 -0400
		// Message-Id: <20260406141530.12345@friends.com>
		// MIME-Version: 1.0
		// Content-Type: multipart/mixed; boundary="=-boundary-1"
		//
		// The headers of the anonymized output will be:
		//
		// Received: from xxxx.xxxxxxx.xxx (xxxx.xxxxxxx.xxx [xxx.xxx.x.xx])
		// 	by xx.xxxxxxx.xxx with xxxxxx id xxxxxx
		// 	for <xxxxxxxx@xxxxxxx.xxx>; Mon, 6 Apr 2026 10:15:32 -0400
		// From: xxxx xxxxxxxxx <xxxx@xxxxxxx.xxx>
		// To: xxxxxxxx xxxx <xxxxxxxx@xxxxxxx.xxx>
		// Subject: Paintball this weekend?
		// Date: Mon, 6 Apr 2026 10:15:30 -0400
		// Message-Id: <xxxxxxxxxxxxxx.xxxxx@xxxxxxx.xxx>
		// MIME-Version: 1.0
		// Content-Type: multipart/mixed; boundary="=-boundary-1"
		#endregion

		#region AnonymizeEntity
		public static void AnonymizeFirstBodyPart (MimeMessage message, Stream output)
		{
			// An individual MIME entity can be anonymized as well, which is useful when only
			// one part of a message is relevant.
			var entity = message.BodyParts.First ();
			var anonymizer = new MimeAnonymizer ();

			anonymizer.Anonymize (entity, output);
		}

		// Given a message whose first body part is:
		//
		// Content-Type: text/plain; charset=utf-8
		// Content-Transfer-Encoding: 7bit
		//
		// Hey Chandler,
		//
		// Monica and I are going to play paintball on Saturday. You in?
		//
		// -- Joey
		//
		// The anonymized output will be:
		//
		// Content-Type: text/plain; charset=utf-8
		// Content-Transfer-Encoding: 7bit
		//
		// xxx xxxxxxxxx
		//
		// xxxxxx xxx x xxx xxxxx xx xxxx xxxxxxxxx xx xxxxxxxxx xxx xxx
		//
		// xx xxxx
		#endregion
	}
}
