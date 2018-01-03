//
// UniqueIdMap.cs
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
using System.Collections;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A mapping of unique identifiers.
	/// </summary>
	/// <remarks>
	/// <para>A <see cref="UniqueIdMap"/> can be used to discover the mapping of one set of unique identifiers
	/// to another.</para>
	/// <para>For example, when copying or moving messages from one folder to another, it is often desirable
	/// to know what the unique identifiers are for each of the messages in the destination folder.</para>
	/// </remarks>
	public class UniqueIdMap : IReadOnlyDictionary<UniqueId, UniqueId>
	{
		/// <summary>
		/// Any empty mapping of unique identifiers.
		/// </summary>
		/// <remarks>
		/// Any empty mapping of unique identifiers.
		/// </remarks>
		public static readonly UniqueIdMap Empty = new UniqueIdMap ();

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdMap"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.UniqueIdMap"/>.
		/// </remarks>
		/// <param name="source">The unique identifiers used in the source folder.</param>
		/// <param name="destination">The unique identifiers used in the destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="source"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		public UniqueIdMap (IList<UniqueId> source, IList<UniqueId> destination)
		{
			if (source == null)
				throw new ArgumentNullException (nameof (source));

			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			Destination = destination;
			Source = source;
		}

		UniqueIdMap ()
		{
			Destination = Source = new UniqueId[0];
		}

		/// <summary>
		/// Gets the list of unique identifiers used in the source folder.
		/// </summary>
		/// <remarks>
		/// Gets the list of unique identifiers used in the source folder.
		/// </remarks>
		/// <value>The unique identifiers used in the source folder.</value>
		public IList<UniqueId> Source {
			get; private set;
		}

		/// <summary>
		/// Gets the list of unique identifiers used in the destination folder.
		/// </summary>
		/// <remarks>
		/// Gets the list of unique identifiers used in the destination folder.
		/// </remarks>
		/// <value>The unique identifiers used in the destination folder.</value>
		public IList<UniqueId> Destination {
			get; private set;
		}

		/// <summary>
		/// Gets the number of unique identifiers that have been remapped.
		/// </summary>
		/// <remarks>
		/// Gets the number of unique identifiers that have been remapped.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return Source.Count; }
		}

		/// <summary>
		/// Gets the keys.
		/// </summary>
		/// <remarks>
		/// Gets the keys.
		/// </remarks>
		/// <value>The keys.</value>
		public IEnumerable<UniqueId> Keys {
			get { return Source; }
		}

		/// <summary>
		/// Gets the values.
		/// </summary>
		/// <remarks>
		/// Gets the values.
		/// </remarks>
		/// <value>The values.</value>
		public IEnumerable<UniqueId> Values {
			get { return Destination; }
		}

		/// <summary>
		/// Checks if the specified unique identifier has been remapped.
		/// </summary>
		/// <remarks>
		/// Checks if the specified unique identifier has been remapped.
		/// </remarks>
		/// <returns><c>true</c> if the unique identifier has been remapped; otherwise, <c>false</c>.</returns>
		/// <param name="key">The unique identifier.</param>
		public bool ContainsKey (UniqueId key)
		{
			return Source.Contains (key);
		}

		/// <summary>
		/// Tries to get the remapped unique identifier.
		/// </summary>
		/// <remarks>
		/// Attempts to get the remapped unique identifier.
		/// </remarks>
		/// <returns><c>true</c> on success; otherwise, <c>false</c>.</returns>
		/// <param name="key">The unique identifier of the message in the source folder.</param>
		/// <param name="value">The unique identifier of the message in the destination folder.</param>
		public bool TryGetValue (UniqueId key, out UniqueId value)
		{
			int index = Source.IndexOf (key);

			if (index == -1 || index >= Destination.Count) {
				value = UniqueId.Invalid;
				return false;
			}

			value = Destination[index];

			return true;
		}

		/// <summary>
		/// Gets the remapped unique identifier.
		/// </summary>
		/// <remarks>
		/// Gets the remapped unique identifier.
		/// </remarks>
		/// <param name="index">The unique identifier of the message in the source folder.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public UniqueId this [UniqueId index] {
			get {
				UniqueId uid;

				if (!TryGetValue (index, out uid))
					throw new ArgumentOutOfRangeException (nameof (index));

				return uid;
			}
		}

		/// <summary>
		/// Gets the enumerator for the remapped unique identifiers.
		/// </summary>
		/// <remarks>
		/// Gets the enumerator for the remapped unique identifiers.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<KeyValuePair<UniqueId, UniqueId>> GetEnumerator ()
		{
			var dst = Destination.GetEnumerator ();
			var src = Source.GetEnumerator ();

			while (src.MoveNext () && dst.MoveNext ())
				yield return new KeyValuePair<UniqueId, UniqueId> (src.Current, dst.Current);

			yield break;
		}

		/// <summary>
		/// Gets the enumerator for the remapped unique identifiers.
		/// </summary>
		/// <remarks>
		/// Gets the enumerator for the remapped unique identifiers.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}
	}
}
