//
// ProgressStream.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Threading;
using System.Threading.Tasks;

using MimeKit.IO;

namespace MailKit {
	class ProgressStream : Stream, ICancellableStream
	{
		readonly ICancellableStream cancellable;

		public ProgressStream (Stream source, Action<int> update)
		{
			if (source == null)
				throw new ArgumentNullException (nameof (source));

			cancellable = source as ICancellableStream;
			Source = source;
			Update = update;
		}

		public Stream Source {
			get; private set;
		}

		Action<int> Update {
			get; set;
		}

		public override bool CanRead {
			get { return Source.CanRead; }
		}

		public override bool CanWrite {
			get { return Source.CanWrite; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanTimeout {
			get { return Source.CanTimeout; }
		}

		public override long Length {
			get { return Source.Length; }
		}

		public override long Position {
			get { return Source.Position; }
			set { Source.Position = value; }
		}

		public override int ReadTimeout {
			get { return Source.ReadTimeout; }
			set { Source.ReadTimeout = value; }
		}

		public override int WriteTimeout {
			get { return Source.WriteTimeout; }
			set { Source.WriteTimeout = value; }
		}

		public int Read (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int n;

			if (cancellable != null) {
				if ((n = cancellable.Read (buffer, offset, count, cancellationToken)) > 0)
					Update (n);
			} else {
				if ((n = Source.Read (buffer, offset, count)) > 0)
					Update (n);
			}

			return n;
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			int n;

			if ((n = Source.Read (buffer, offset, count)) > 0)
				Update (n);

			return n;
		}

		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int n;

			if ((n = await Source.ReadAsync (buffer, offset, count, cancellationToken).ConfigureAwait (false)) > 0)
				Update (n);

			return n;
		}

		public void Write (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (cancellable != null)
				cancellable.Write (buffer, offset, count, cancellationToken);
			else
				Source.Write (buffer, offset, count);

			if (count > 0)
				Update (count);
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			Source.Write (buffer, offset, count);

			if (count > 0)
				Update (count);
		}

		public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await Source.WriteAsync (buffer, offset, count, cancellationToken).ConfigureAwait (false);

			if (count > 0)
				Update (count);
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ("The stream does not support seeking.");
		}

		public void Flush (CancellationToken cancellationToken)
		{
			if (cancellable != null)
				cancellable.Flush (cancellationToken);
			else
				Source.Flush ();
		}

		public override void Flush ()
		{
			Source.Flush ();
		}

		public override Task FlushAsync (CancellationToken cancellationToken)
		{
			return Source.FlushAsync (cancellationToken);
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ("The stream does not support resizing.");
		}
	}
}
