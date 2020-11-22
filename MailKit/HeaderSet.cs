//
// HeaderSet.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Collections;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A set of headers.
	/// </summary>
	/// <remarks>
	/// A set of headers.
	/// </remarks>
	public class HeaderSet : ICollection<string>
	{
		const string AtomSafeCharacters = "!#$%&'*+-/=?^_`{|}~";

		/// <summary>
		/// A set of headers that only includes all headers.
		/// </summary>
		/// <remarks>
		/// When used with a <see cref="IFetchRequest"/>, this pre-computed set of headers can be used
		/// to fetch the entire list of headers for a message.
		/// </remarks>
		public static readonly HeaderSet All = new HeaderSet () { Exclude = true, IsReadOnly = true };

		/// <summary>
		/// A set of headers that only includes only the standard envelope headers.
		/// </summary>
		/// <remarks>
		/// When used with a <see cref="IFetchRequest"/>, this pre-computed set of headers can be used
		/// to fetch the standard envelope headers for a message.
		/// </remarks>
		public static readonly HeaderSet Envelope = new HeaderSet (new HeaderId[] {
			HeaderId.Sender,
			HeaderId.From,
			HeaderId.ReplyTo,
			HeaderId.To,
			HeaderId.Cc,
			HeaderId.Bcc,
			HeaderId.Subject,
			HeaderId.Date,
			HeaderId.MessageId,
			HeaderId.InReplyTo
		}) { IsReadOnly = true };

		/// <summary>
		/// A set of headers that only includes the <c>References</c> header.
		/// </summary>
		/// <remarks>
		/// When used with a <see cref="IFetchRequest"/>, this pre-computed set of headers can be used
		/// to fetch the <c>References</c> header for a message. Generally, this should be used in
		/// combination with <see cref="MessageSummaryItems.Envelope"/> in order to have all of the
		/// information needed to thread messages using the <see cref="ThreadingAlgorithm.References"/>
		/// threading algorithm.
		/// </remarks>
		public static readonly HeaderSet References = new HeaderSet (new HeaderId[] { HeaderId.References }) { IsReadOnly = true };

		readonly HashSet<string> hash = new HashSet<string> (StringComparer.Ordinal);
		bool exclude;

		/// <summary>
		/// Initializes a new instance of the <see cref="HeaderSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="HeaderSet"/>.
		/// </remarks>
		public HeaderSet ()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HeaderSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="HeaderSet"/>.
		/// </remarks>
		public HeaderSet (IEnumerable<HeaderId> headers)
		{
			AddRange (headers);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HeaderSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="HeaderSet"/>.
		/// </remarks>
		public HeaderSet (IEnumerable<string> headers)
		{
			AddRange (headers);
		}

		void CheckReadOnly ()
		{
			if (IsReadOnly)
				throw new InvalidOperationException ("The HeaderSet is read-only.");
		}

		/// <summary>
		/// Get the number of headers in the set.
		/// </summary>
		/// <remarks>
		/// Gets the number of headers in the set.
		/// </remarks>
		/// <value>The number of headers.</value>
		public int Count {
			get { return hash.Count; }
		}

		/// <summary>
		/// Get or set whether this set of headers is meant to be excluded when used with a <see cref="IFetchRequest"/>.
		/// </summary>
		/// <remarks>
		/// Get or set whether this set of headers is meant to be excluded when used with a <see cref="IFetchRequest"/>.
		/// </remarks>
		/// <value><c>true</c> if the headers are meant to be excluded; otherwise, <c>false</c>.</value>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public bool Exclude {
			get { return exclude; }
			set {
				CheckReadOnly ();
				exclude = value;
			}
		}

		/// <summary>
		/// Get whether or not the set of headers is read-only.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the set of headers is read-only.
		/// </remarks>
		/// <value><c>true</c> if this instance is read only; otherwise, <c>false</c>.</value>
		public bool IsReadOnly {
			get; private set;
		}

		static bool IsAsciiAtom (char c)
		{
			return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || AtomSafeCharacters.IndexOf (c) != -1;
		}

		static bool IsValid (string header)
		{
			if (header.Length == 0)
				return false;

			for (int i = 0; i < header.Length; i++) {
				if (header[i] < 127 && !IsAsciiAtom (header[i]))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Add the specified header.
		/// </summary>
		/// <remarks>
		/// Adds the specified header to the set of headers.
		/// </remarks>
		/// <returns><c>true</c> if the header was added to the set; otherwise, <c>false</c>.</returns>
		/// <param name="header">The header to add.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="header"/> is not a valid <see cref="HeaderId"/>.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public bool Add (HeaderId header)
		{
			if (header == HeaderId.Unknown)
				throw new ArgumentOutOfRangeException (nameof (header));

			CheckReadOnly ();

			return hash.Add (header.ToHeaderName ().ToUpperInvariant ());
		}

		/// <summary>
		/// Add the specified header.
		/// </summary>
		/// <remarks>
		/// Adds the specified header to the set of headers.
		/// </remarks>
		/// <returns><c>true</c> if the header was added to the set; otherwise, <c>false</c>.</returns>
		/// <param name="header">The header to add.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="header"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public bool Add (string header)
		{
			if (header == null)
				throw new ArgumentNullException (nameof (header));

			if (!IsValid (header))
				throw new ArgumentException ("The header field is invalid.", nameof (header));

			CheckReadOnly ();

			return hash.Add (header.ToUpperInvariant ());
		}

		/// <summary>
		/// Add the specified header.
		/// </summary>
		/// <remarks>
		/// Adds the specified header to the set of headers.
		/// </remarks>
		/// <param name="item">The header to add.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="item"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		void ICollection<string>.Add (string item)
		{
			Add (item);
		}

		/// <summary>
		/// Add a collection of headers.
		/// </summary>
		/// <remarks>
		/// Adds the specified headers to the set of headers.
		/// </remarks>
		/// <param name="headers">The headers to add.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public void AddRange (IEnumerable<HeaderId> headers)
		{
			if (headers == null)
				throw new ArgumentNullException (nameof (headers));

			CheckReadOnly ();

			foreach (var header in headers) {
				if (header == HeaderId.Unknown)
					throw new ArgumentException ("One or more of the headers is invalid.", nameof (headers));

				hash.Add (header.ToHeaderName ().ToUpperInvariant ());
			}
		}

		/// <summary>
		/// Add a collection of headers.
		/// </summary>
		/// <remarks>
		/// Adds the specified headers to the set of headers.
		/// </remarks>
		/// <param name="headers">The headers to add.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public void AddRange (IEnumerable<string> headers)
		{
			if (headers == null)
				throw new ArgumentNullException (nameof (headers));

			CheckReadOnly ();

			foreach (var header in headers) {
				if (header == null || !IsValid (header))
					throw new ArgumentException ("One or more of the headers is invalid.", nameof (headers));

				hash.Add (header.ToUpperInvariant ());
			}
		}

		/// <summary>
		/// Clear the set of headers.
		/// </summary>
		/// <remarks>
		/// Clears the set of headers.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public void Clear ()
		{
			CheckReadOnly ();
			hash.Clear ();
		}

		/// <summary>
		/// Copy all of the headers in the <see cref="HeaderSet"/> to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the headers within the <see cref="HeaderSet"/> into the array,
		/// starting at the specified array index.
		/// </remarks>
		/// <param name="array">The array to copy the headers to.</param>
		/// <param name="arrayIndex">The index into the array.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="array"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is out of range.
		/// </exception>
		public void CopyTo (string[] array, int arrayIndex)
		{
			hash.CopyTo (array, arrayIndex);
		}

		/// <summary>
		/// Check if the set of headers contains the specified header.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the set of headers contains the specified header.
		/// </remarks>
		/// <returns><value>true</value> if the specified header exists;
		/// otherwise <value>false</value>.</returns>
		/// <param name="header">The header identifier.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="header"/> is not a valid <see cref="HeaderId"/>.
		/// </exception>
		public bool Contains (HeaderId header)
		{
			if (header == HeaderId.Unknown)
				throw new ArgumentOutOfRangeException (nameof (header));

			return hash.Contains (header.ToHeaderName ().ToUpperInvariant ());
		}

		/// <summary>
		/// Check if the set of headers contains the specified header.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the set of headers contains the specified header.
		/// </remarks>
		/// <returns><value>true</value> if the specified header exists;
		/// otherwise <value>false</value>.</returns>
		/// <param name="header">The name of the header.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="header"/> is <c>null</c>.
		/// </exception>
		public bool Contains (string header)
		{
			if (header == null)
				throw new ArgumentNullException (nameof (header));

			return hash.Contains (header.ToUpperInvariant ());
		}

		/// <summary>
		/// Remove the specified header.
		/// </summary>
		/// <remarks>
		/// Removes the specified header if it exists.
		/// </remarks>
		/// <returns><c>true</c> if the specified header was removed;
		/// otherwise <c>false</c>.</returns>
		/// <param name="header">The header.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="header"/> is not a valid <see cref="HeaderId"/>.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public bool Remove (HeaderId header)
		{
			if (header == HeaderId.Unknown)
				throw new ArgumentOutOfRangeException (nameof (header));

			CheckReadOnly ();

			return hash.Remove (header.ToHeaderName ().ToUpperInvariant ());
		}

		/// <summary>
		/// Remove the specified header.
		/// </summary>
		/// <remarks>
		/// Removes the specified header if it exists.
		/// </remarks>
		/// <returns><c>true</c> if the specified header was removed;
		/// otherwise <c>false</c>.</returns>
		/// <param name="header">The header.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="header"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The operation is invalid because the <see cref="HeaderSet"/> is read-only.
		/// </exception>
		public bool Remove (string header)
		{
			if (header == null)
				throw new ArgumentNullException (nameof (header));

			CheckReadOnly ();

			return hash.Remove (header.ToUpperInvariant ());
		}

		/// <summary>
		/// Get an enumerator for the set of headers.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the set of headers.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<string> GetEnumerator ()
		{
			return hash.GetEnumerator ();
		}

		/// <summary>
		/// Get an enumerator for the set of headers.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the set of headers.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return hash.GetEnumerator ();
		}
	}
}
