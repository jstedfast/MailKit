//
// ImapLiteral.cs
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
using System.Threading;
using System.Threading.Tasks;

using MimeKit;
using MimeKit.IO;

namespace MailKit.Net.Imap {
	enum ImapLiteralType
	{
		String,
		//Stream,
		MimeMessage
	}

	/// <summary>
	/// An IMAP literal object.
	/// </summary>
	/// <remarks>
	/// The literal can be a string, byte[], Stream, or a MimeMessage.
	/// </remarks>
	class ImapLiteral
	{
		public readonly ImapLiteralType Type;
		public readonly object Literal;
		readonly FormatOptions format;
		readonly Action<int> update;

		static void DefaultUpdate (int value)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapLiteral"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapLiteral"/>.
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="action">The progress update action.</param>
		public ImapLiteral (FormatOptions options, MimeMessage message, Action<int> action)
		{
			format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			update = action;

			Type = ImapLiteralType.MimeMessage;
			Literal = message;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapLiteral"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.Net.Imap.ImapLiteral"/>.
		/// </remarks>
		/// <param name="options">The formatting options.</param>
		/// <param name="literal">The literal.</param>
		public ImapLiteral (FormatOptions options, byte[] literal)
		{
			format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			update = DefaultUpdate;

			Type = ImapLiteralType.String;
			Literal = literal;
		}

		/// <summary>
		/// Get the length of the literal, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the length of the literal, in bytes.
		/// </remarks>
		/// <value>The length.</value>
		public long Length {
			get {
				if (Type == ImapLiteralType.String)
					return ((byte[]) Literal).Length;

				using (var measure = new MeasuringStream ()) {
					//if (Type == ImapLiteralType.Stream) {
					//	var stream = (Stream) Literal;
					//	stream.CopyTo (measure, 4096);
					//	stream.Position = 0;

					//	return measure.Length;
					//}

					((MimeMessage) Literal).WriteTo (format, measure);

					return measure.Length;
				}
			}
		}

		/// <summary>
		/// Write the literal to the specified stream.
		/// </summary>
		/// <remarks>
		/// Writes the literal to the specified stream.
		/// </remarks>
		/// <param name="stream">The stream.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public void WriteTo (ImapStream stream, CancellationToken cancellationToken)
		{
			if (Type == ImapLiteralType.String) {
				var bytes = (byte[]) Literal;

				stream.Write (bytes, 0, bytes.Length, cancellationToken);
				stream.Flush (cancellationToken);
				return;
			}

			//if (Type == ImapLiteralType.Stream) {
			//	var literal = (Stream) Literal;
			//	var buf = new byte[4096];
			//	int nread;

			//	while ((nread = literal.Read (buf, 0, buf.Length)) > 0)
			//		stream.Write (buf, 0, nread, cancellationToken);

			//	stream.Flush (cancellationToken);
			//	return;
			//}

			var message = (MimeMessage) Literal;

			using (var s = new ProgressStream (stream, update)) {
				message.WriteTo (format, s, cancellationToken);
				s.Flush (cancellationToken);
			}
		}

		/// <summary>
		/// Asynchronously write the literal to the specified stream.
		/// </summary>
		/// <remarks>
		/// Asynchronously writes the literal to the specified stream.
		/// </remarks>
		/// <param name="stream">The stream.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public async Task WriteToAsync (ImapStream stream, CancellationToken cancellationToken)
		{
			if (Type == ImapLiteralType.String) {
				var bytes = (byte[]) Literal;

				await stream.WriteAsync (bytes, 0, bytes.Length, cancellationToken).ConfigureAwait (false);
				await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
				return;
			}

			//if (Type == ImapLiteralType.Stream) {
			//	var literal = (Stream) Literal;
			//	var buf = new byte[4096];
			//	int nread;

			//	while ((nread = await literal.ReadAsync (buf, 0, buf.Length, cancellationToken).ConfigureAwait (false)) > 0)
			//		await stream.WriteAsync (buf, 0, nread, cancellationToken).ConfigureAwait (false);

			//	await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
			//	return;
			//}

			var message = (MimeMessage) Literal;

			using (var s = new ProgressStream (stream, update)) {
				await message.WriteToAsync (format, s, cancellationToken).ConfigureAwait (false);
				await s.FlushAsync (cancellationToken).ConfigureAwait (false);
			}
		}
	}
}
