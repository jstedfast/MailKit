//
// ImapCommandTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System.Text;

using MimeKit;
using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public  class ImapCommandTests : IDisposable
	{
		readonly ImapEngine Engine;
		readonly ImapFolder Inbox;

		public ImapCommandTests ()
		{
			Engine = new ImapEngine (CreateImapFolderDelegate) {
				Capabilities = ImapCapabilities.IMAP4rev1
			};

			var args = new ImapFolderConstructorArgs (Engine, "INBOX", FolderAttributes.None, '.');
			Inbox = new ImapFolder (args);
		}

		public void Dispose ()
		{
			Engine.Dispose ();
			GC.SuppressFinalize (this);
		}

		static ImapFolder CreateImapFolderDelegate (ImapFolderConstructorArgs args)
		{
			return new ImapFolder (args);
		}

		static Task UntaggedResponseHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			return Task.CompletedTask;
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ImapCommand (null, CancellationToken.None, Inbox, "NOOP\r\n"));
			Assert.Throws<ArgumentNullException> (() => new ImapCommand (Engine, CancellationToken.None, Inbox, null));

			Assert.Throws<ArgumentNullException> (() => new ImapCommand (null, CancellationToken.None, Inbox, FormatOptions.Default, "NOOP\r\n"));
			Assert.Throws<ArgumentNullException> (() => new ImapCommand (Engine, CancellationToken.None, Inbox, null, "NOOP\r\n"));
			Assert.Throws<ArgumentNullException> (() => new ImapCommand (Engine, CancellationToken.None, Inbox, FormatOptions.Default, null));

			var ic = new ImapCommand (Engine, CancellationToken.None, Inbox, "NOOP\r\n");
			Assert.Throws<ArgumentNullException> (() => ic.RegisterUntaggedHandler (null, UntaggedResponseHandler));
			Assert.Throws<ArgumentNullException> (() => ic.RegisterUntaggedHandler ("EVENT", null));

			ic.Status = ImapCommandStatus.Queued;
			Assert.Throws<InvalidOperationException> (() => ic.RegisterUntaggedHandler ("EVENT", UntaggedResponseHandler));

			ic.Status = ImapCommandStatus.Active;
			Assert.Throws<InvalidOperationException> (() => ic.RegisterUntaggedHandler ("EVENT", UntaggedResponseHandler));

			ic.Status = ImapCommandStatus.Complete;
			Assert.Throws<InvalidOperationException> (() => ic.RegisterUntaggedHandler ("EVENT", UntaggedResponseHandler));

			ic.Status = ImapCommandStatus.Error;
			Assert.Throws<InvalidOperationException> (() => ic.RegisterUntaggedHandler ("EVENT", UntaggedResponseHandler));
		}

		[Test]
		public void TestFormatExceptions ()
		{
			try {
				var ic = new ImapCommand (Engine, CancellationToken.None, null, "Lets try %X as a format argument.");
				Assert.Fail ("Expected FormatException");
			} catch (FormatException ex) {
				Assert.That (ex.Message, Is.EqualTo ("The %X format specifier is not supported."));
			} catch (Exception ex) {
				Assert.Fail ($"Expected FormatException, but got {ex.GetType ().Name}");
			}

			try {
				var ic = ImapCommand.EstimateCommandLength (Engine, "Lets try %Y as a format argument.");
				Assert.Fail ("Expected FormatException");
			} catch (FormatException ex) {
				Assert.That (ex.Message, Is.EqualTo ("The %Y format specifier is not supported."));
			} catch (Exception ex) {
				Assert.Fail ($"Expected FormatException, but got {ex.GetType ().Name}");
			}
		}

		[Test]
		public void TestEstimateCommandLengthWithLiteralString ()
		{
			const string koreanProverb = "꿩 먹고 알 먹는다";
			var literalLength = Encoding.UTF8.GetByteCount (koreanProverb);
			var expected = $"SEARCH TEXT {{{literalLength}}}\r\n{koreanProverb}".Length;

			var length = ImapCommand.EstimateCommandLength (Engine, "SEARCH TEXT %S", koreanProverb);
			Assert.That (length, Is.EqualTo (expected));

			try {
				Engine.Capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.LiteralPlus;
				expected = $"SEARCH TEXT {{{literalLength}+}}\r\n{koreanProverb}".Length;
				length = ImapCommand.EstimateCommandLength (Engine, "SEARCH TEXT %S", koreanProverb);
				Assert.That (length, Is.EqualTo (expected), "LITERAL+");

				Engine.Capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.LiteralMinus;
				expected = $"SEARCH TEXT {{{literalLength}+}}\r\n{koreanProverb}".Length;
				length = ImapCommand.EstimateCommandLength (Engine, "SEARCH TEXT %S", koreanProverb);
				Assert.That (length, Is.EqualTo (expected), "LITERAL-");
			} finally {
				Engine.Capabilities = ImapCapabilities.IMAP4rev1;
			}
		}
	}
}
