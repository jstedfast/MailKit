//
// LabelsChangedEventArgs.cs
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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MailKit {
	/// <summary>
	/// Event args for the <see cref="IMailFolder.MessageLabelsChanged"/> event.
	/// </summary>
	/// <remarks>
	/// Event args for the <see cref="IMailFolder.MessageLabelsChanged"/> event.
	/// </remarks>
	public class MessageLabelsChangedEventArgs : MessageEventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageLabelsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageLabelsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		internal MessageLabelsChangedEventArgs (int index) : base (index)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageLabelsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageLabelsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <param name="labels">The message labels.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public MessageLabelsChangedEventArgs (int index, IList<string> labels) : base (index)
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			Labels = new ReadOnlyCollection<string> (labels);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageLabelsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageLabelsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <param name="labels">The message labels.</param>
		/// <param name="modseq">The modification sequence value.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public MessageLabelsChangedEventArgs (int index, IList<string> labels, ulong modseq) : base (index)
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			Labels = new ReadOnlyCollection<string> (labels);
			ModSeq = modseq;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageLabelsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageLabelsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <param name="uid">The unique id of the message.</param>
		/// <param name="labels">The message labels.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public MessageLabelsChangedEventArgs (int index, UniqueId uid, IList<string> labels) : base (index)
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			Labels = new ReadOnlyCollection<string> (labels);
			UniqueId = uid;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageLabelsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageLabelsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <param name="uid">The unique id of the message.</param>
		/// <param name="labels">The message labels.</param>
		/// <param name="modseq">The modification sequence value.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public MessageLabelsChangedEventArgs (int index, UniqueId uid, IList<string> labels, ulong modseq) : base (index)
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			Labels = new ReadOnlyCollection<string> (labels);
			UniqueId = uid;
			ModSeq = modseq;
		}

		/// <summary>
		/// Gets the unique ID of the message that changed, if available.
		/// </summary>
		/// <remarks>
		/// Gets the unique ID of the message that changed, if available.
		/// </remarks>
		/// <value>The unique ID of the message.</value>
		public UniqueId? UniqueId {
			get; internal set;
		}

		/// <summary>
		/// Gets the updated labels.
		/// </summary>
		/// <remarks>
		/// Gets the updated labels.
		/// </remarks>
		/// <value>The updated labels.</value>
		public IList<string> Labels {
			get; internal set;
		}

		/// <summary>
		/// Gets the updated mod-sequence value of the message, if available.
		/// </summary>
		/// <remarks>
		/// Gets the updated mod-sequence value of the message, if available.
		/// </remarks>
		/// <value>The mod-sequence value.</value>
		public ulong? ModSeq {
			get; internal set;
		}
	}
}
