//
// PartialRange.cs
//
// Author: Andrii Martyniuk <pretosync@gmail.com>
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
using System.Globalization;

namespace MailKit {
	/// <summary>
	/// A range of results to return in response to a SEARCH or FETCH command.
	/// </summary>
	/// <remarks>
	/// <para>A range of results to return in response to a SEARCH or FETCH command.</para>
	/// <para>Positions within a partial range are 1-based indexes into the results, where the results
	/// are ordered from the oldest message (the message with the lowest unique identifier) to the
	/// newest message (the message with the highest unique identifier). Negative positions index the
	/// results in the opposite direction, starting from the newest message. For example, a range of
	/// <c>1:500</c> refers to the oldest 500 results while a range of <c>-1:-500</c> refers to the
	/// newest 500 results.</para>
	/// <para>The order of the two positions is not significant. In other words, a range of
	/// <c>500:400</c> is equivalent to a range of <c>400:500</c>.</para>
	/// <note type="note">For more information about partial ranges, see
	/// <a href="https://tools.ietf.org/html/rfc9394">rfc9394</a>.</note>
	/// </remarks>
	public readonly struct PartialRange : IEquatable<PartialRange>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.PartialRange"/> struct.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new range of search or fetch results.</para>
		/// <para>Both <paramref name="first"/> and <paramref name="last"/> must be non-zero values
		/// with the same sign. Positive positions are relative to the oldest result while negative
		/// positions are relative to the newest result.</para>
		/// </remarks>
		/// <param name="first">The first position in the range of results.</param>
		/// <param name="last">The last position in the range of results.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="first"/> is <c>0</c> or has a magnitude greater than <c>4294967295</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="last"/> is <c>0</c>, has a magnitude greater than <c>4294967295</c>,
		/// or does not have the same sign as <paramref name="first"/>.</para>
		/// </exception>
		public PartialRange (long first, long last)
		{
			if (first == 0 || first < -uint.MaxValue || first > uint.MaxValue)
				throw new ArgumentOutOfRangeException (nameof (first));

			if (last == 0 || last < -uint.MaxValue || last > uint.MaxValue || (first < 0) != (last < 0))
				throw new ArgumentOutOfRangeException (nameof (last));

			First = first;
			Last = last;
		}

		/// <summary>
		/// Get the first position in the range of results.
		/// </summary>
		/// <remarks>
		/// Gets the first position in the range of results.
		/// </remarks>
		/// <value>The first position in the range of results.</value>
		public long First {
			get;
		}

		/// <summary>
		/// Get the last position in the range of results.
		/// </summary>
		/// <remarks>
		/// Gets the last position in the range of results.
		/// </remarks>
		/// <value>The last position in the range of results.</value>
		public long Last {
			get;
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="MailKit.PartialRange"/> is equal to the current <see cref="MailKit.PartialRange"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="MailKit.PartialRange"/> is equal to the current <see cref="MailKit.PartialRange"/>.
		/// </remarks>
		/// <param name="other">The <see cref="MailKit.PartialRange"/> to compare with the current <see cref="MailKit.PartialRange"/>.</param>
		/// <returns><see langword="true" /> if the specified <see cref="MailKit.PartialRange"/> is equal to the current
		/// <see cref="MailKit.PartialRange"/>; otherwise, <see langword="false" />.</returns>
		public bool Equals (PartialRange other)
		{
			return other.First == First && other.Last == Last;
		}

		#endregion

		/// <summary>
		/// Determines whether two partial ranges are equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two partial ranges are equal.
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="range1"/> and <paramref name="range2"/> are equal; otherwise, <see langword="false" />.</returns>
		/// <param name="range1">The first partial range to compare.</param>
		/// <param name="range2">The second partial range to compare.</param>
		public static bool operator == (PartialRange range1, PartialRange range2)
		{
			return range1.Equals (range2);
		}

		/// <summary>
		/// Determines whether two partial ranges are not equal.
		/// </summary>
		/// <remarks>
		/// Determines whether two partial ranges are not equal.
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="range1"/> and <paramref name="range2"/> are not equal; otherwise, <see langword="false" />.</returns>
		/// <param name="range1">The first partial range to compare.</param>
		/// <param name="range2">The second partial range to compare.</param>
		public static bool operator != (PartialRange range1, PartialRange range2)
		{
			return !range1.Equals (range2);
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.PartialRange"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.PartialRange"/>.
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.PartialRange"/>.</param>
		/// <returns><see langword="true" /> if the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.PartialRange"/>;
		/// otherwise, <see langword="false" />.</returns>
		public override bool Equals (object? obj)
		{
			return obj != null && obj.GetType () == typeof (PartialRange) && Equals ((PartialRange) obj);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.PartialRange"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.PartialRange"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return First.GetHashCode () ^ Last.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.PartialRange"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.PartialRange"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.PartialRange"/>.</returns>
		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "{0}:{1}", First, Last);
		}

		/// <summary>
		/// Attempt to parse a partial range.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a partial range.
		/// </remarks>
		/// <returns><see langword="true" /> if the partial range was successfully parsed; otherwise, <see langword="false" />.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="range">The partial range.</param>
		internal static bool TryParse (string? token, out PartialRange range)
		{
			range = default;

			if (string.IsNullOrEmpty (token))
				return false;

			int colon = token!.IndexOf (':');

			if (colon <= 0 || colon == token.Length - 1)
				return false;

			if (!long.TryParse (token.Substring (0, colon), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long first) ||
				!long.TryParse (token.Substring (colon + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long last))
				return false;

			if (first == 0 || first < -uint.MaxValue || first > uint.MaxValue)
				return false;

			if (last == 0 || last < -uint.MaxValue || last > uint.MaxValue || (first < 0) != (last < 0))
				return false;

			range = new PartialRange (first, last);

			return true;
		}
	}
}
