//
// MimeReaderExamples.cs
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
	public static class MimeReaderExamples
	{
		#region MimeStructureReader
		// MimeReader is a low-level, forward-only reader that reports the structure of a MIME
		// stream via protected virtual callbacks rather than constructing a MimeMessage. This
		// makes it ideal for scanning large messages without loading their content into memory.
		class MimeStructureReader : MimeReader
		{
			readonly TextWriter output;
			int depth;

			public MimeStructureReader (Stream stream, TextWriter output) : base (stream, MimeFormat.Entity)
			{
				this.output = output;
			}

			void WriteLine (string format, params object[] args)
			{
				output.Write (new string (' ', depth * 2));
				output.WriteLine (format, args);
			}

			protected override void OnHeaderRead (Header header, int beginLineNumber, CancellationToken cancellationToken)
			{
				if (header.Id == HeaderId.Subject)
					WriteLine ("Subject: {0}", header.Value);
			}

			protected override void OnMultipartBegin (ContentType contentType, long beginOffset, int beginLineNumber, CancellationToken cancellationToken)
			{
				WriteLine ("{0} (line {1})", contentType.MimeType, beginLineNumber);
				depth++;
			}

			protected override void OnMultipartEnd (ContentType contentType, long beginOffset, int beginLineNumber, long headersEndOffset, long endOffset, int lines, CancellationToken cancellationToken)
			{
				depth--;
			}

			protected override void OnMessagePartBegin (ContentType contentType, long beginOffset, int beginLineNumber, CancellationToken cancellationToken)
			{
				WriteLine ("{0} (line {1})", contentType.MimeType, beginLineNumber);
				depth++;
			}

			protected override void OnMessagePartEnd (ContentType contentType, long beginOffset, int beginLineNumber, long headersEndOffset, long endOffset, int lines, CancellationToken cancellationToken)
			{
				depth--;
			}

			protected override void OnMimePartEnd (ContentType contentType, long beginOffset, int beginLineNumber, long headersEndOffset, long endOffset, int lines, CancellationToken cancellationToken)
			{
				// The offsets make it possible to locate (and later extract) the raw part
				// within the original stream.
				WriteLine ("{0} (line {1}, {2} bytes)", contentType.MimeType, beginLineNumber, endOffset - beginOffset);
			}
		}

		public static void PrintMimeStructure (string fileName)
		{
			using (var stream = File.OpenRead (fileName)) {
				var reader = new MimeStructureReader (stream, Console.Out);

				// ReadMessage() parses the stream, invoking the callbacks along the way.
				reader.ReadMessage ();
			}
		}
		#endregion

		#region ComplianceLogger
		// IMimeComplianceLogger receives a MimeComplianceIssue for each violation of the MIME
		// specifications that the MimeReader (or MimeParser) encounters while parsing.
		class ComplianceReport : IMimeComplianceLogger
		{
			public readonly List<MimeComplianceIssue> Issues = new List<MimeComplianceIssue> ();

			public void Log (in MimeComplianceIssue issue)
			{
				Issues.Add (issue);
			}
		}

		public static MimeMessage ParseAndReportCompliance (string fileName)
		{
			using (var stream = File.OpenRead (fileName)) {
				var report = new ComplianceReport ();
				var parser = new MimeParser (stream, MimeFormat.Entity) {
					ComplianceLogger = report,

					// Messages from a local message store (such as an mbox or Maildir) are not
					// subject to the transport requirements (such as line-length limits) and so
					// such violations are rated with a lower severity.
					ComplianceContext = MimeComplianceContext.Storage,

					// When parsing untrusted messages, limit the number of times that each violation
					// may be reported to avoid unbounded memory usage.
					MaxComplianceIssuesPerViolation = 100
				};

				var message = parser.ParseMessage ();

				foreach (var issue in report.Issues) {
					// Each issue includes the location in the stream where the violation was found,
					// how severe it is, and a description of the problem.
					Console.WriteLine ("[{0}] line {1}, column {2}: {3}", issue.Severity, issue.LineNumber, issue.ColumnNumber, issue.Description);

					// The Categories property describes the kinds of risk that the violation poses.
					if ((issue.Categories & MimeComplianceCategories.Security) != 0)
						Console.WriteLine ("    This violation may have security implications: {0}", issue.Remarks);
				}

				return message;
			}
		}
		#endregion
	}
}
