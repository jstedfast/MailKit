//
// SmtpProtocolExceptionTests.cs
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

using System.Text;

using MailKit;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpProtocolExceptionTests
	{
		[Test]
		public void TestSmtpStreamUnexpectedDisconnect ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var ex = Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (SmtpCommand.Connect, CancellationToken.None));
				Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.UnexpectedDisconnect));
				Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Connect), "Command");
				Assert.That (ex.LastResponse, Is.Null, "LastResponse");
				Assert.That (ex.Message, Is.EqualTo ("The SMTP server has unexpectedly disconnected."), "Message");
			}
		}

		[Test]
		public void TestSmtpStreamUnexpectedDisconnectAfterResponse ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes ("250 2.1.0 sender ok\r\n");
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				// Simulate pipelined MAIL FROM + RCPT TO where the server disconnects after responding to MAIL FROM
				stream.QueueCommand ("MAIL FROM:<sender@example.com>\r\n", CancellationToken.None);
				stream.QueueCommand ("RCPT TO:<recipient@example.com>\r\n", CancellationToken.None);

				var response = stream.ReadResponse (SmtpCommand.MailFrom, CancellationToken.None);
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok));

				var ex = Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (SmtpCommand.RcptTo, CancellationToken.None));
				Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.UnexpectedDisconnect), "ErrorType");
				Assert.That (ex.Command, Is.EqualTo (SmtpCommand.RcptTo), "Command");
				Assert.That (ex.LastResponse, Is.Not.Null, "LastResponse");
				Assert.That (ex.LastResponse.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "LastResponse.StatusCode");
				Assert.That (ex.LastResponse.Response, Is.EqualTo ("2.1.0 sender ok"), "LastResponse.Response");
				Assert.That (ex.Message, Is.EqualTo ("The SMTP server has unexpectedly disconnected: 2.1.0 sender ok"), "Message");
			}
		}

		[Test]
		public async Task TestSmtpStreamUnexpectedDisconnectAfterResponseAsync ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes ("250 2.1.0 sender ok\r\n");
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				await stream.QueueCommandAsync ("MAIL FROM:<sender@example.com>\r\n", CancellationToken.None);
				await stream.QueueCommandAsync ("RCPT TO:<recipient@example.com>\r\n", CancellationToken.None);

				var response = await stream.ReadResponseAsync (SmtpCommand.MailFrom, CancellationToken.None);
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok));

				var ex = Assert.ThrowsAsync<SmtpProtocolException> (() => stream.ReadResponseAsync (SmtpCommand.RcptTo, CancellationToken.None));
				Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.UnexpectedDisconnect), "ErrorType");
				Assert.That (ex.Command, Is.EqualTo (SmtpCommand.RcptTo), "Command");
				Assert.That (ex.LastResponse, Is.Not.Null, "LastResponse");
				Assert.That (ex.LastResponse.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "LastResponse.StatusCode");
				Assert.That (ex.LastResponse.Response, Is.EqualTo ("2.1.0 sender ok"), "LastResponse.Response");
			}
		}

		[Test]
		public void TestSmtpStreamQueueCommandResetsLastResponse ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes ("250 2.1.0 sender ok\r\n");
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				stream.QueueCommand ("MAIL FROM:<sender@example.com>\r\n", CancellationToken.None);
				stream.ReadResponse (SmtpCommand.MailFrom, CancellationToken.None);

				// A new (non-pipelined) command should reset the last response so that it is not misattributed
				stream.QueueCommand ("RCPT TO:<recipient@example.com>\r\n", CancellationToken.None);

				var ex = Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (SmtpCommand.RcptTo, CancellationToken.None));
				Assert.That (ex.Command, Is.EqualTo (SmtpCommand.RcptTo), "Command");
				Assert.That (ex.LastResponse, Is.Null, "LastResponse");
				Assert.That (ex.Message, Is.EqualTo ("The SMTP server has unexpectedly disconnected."), "Message");
			}
		}

		[TestCase ("XXX This is an invalid response.\r\n")]
		[TestCase ("250-This is the first line of a response.\r\n340 And this is a mismatched response code.\r\n")]
		public void TestSmtpStreamInvalidResponse (string response)
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes (response);
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				var ex = Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (SmtpCommand.Ehlo, CancellationToken.None));
				Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.InvalidResponse));
				Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Ehlo), "Command");
				Assert.That (ex.LastResponse, Is.Null, "LastResponse");
			}
		}

		[Test]
		public void TestSmtpProtocolExceptionConstructors ()
		{
			var response = new SmtpResponse (SmtpStatusCode.Ok, "2.1.0 sender ok");
			var inner = new IOException ("inner");

			var ex = new SmtpProtocolException ("message", ProtocolErrorType.UnexpectedDisconnect, SmtpCommand.RcptTo, response);
			Assert.That (ex.Message, Is.EqualTo ("message"), "Message");
			Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.UnexpectedDisconnect), "ErrorType");
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.RcptTo), "Command");
			Assert.That (ex.LastResponse, Is.SameAs (response), "LastResponse");

			ex = new SmtpProtocolException ("message", ProtocolErrorType.InvalidResponse, SmtpCommand.Data, null, inner);
			Assert.That (ex.ErrorType, Is.EqualTo (ProtocolErrorType.InvalidResponse), "ErrorType");
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Data), "Command");
			Assert.That (ex.LastResponse, Is.Null, "LastResponse");
			Assert.That (ex.InnerException, Is.SameAs (inner), "InnerException");

			ex = new SmtpProtocolException ("message");
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Unknown), "Default Command");
			Assert.That (ex.LastResponse, Is.Null, "Default LastResponse");
		}
	}
}
