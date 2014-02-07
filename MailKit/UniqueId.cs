//
// UniqueId.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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

namespace MailKit {
	/// <summary>
	/// A unique identifier.
	/// </summary>
	/// <remarks>
	/// Represents a unique identifier for messages in a <see cref="IFolder"/>.
	/// </remarks>
	public struct UniqueId : IComparable<UniqueId>, IEquatable<UniqueId>
	{
		/// <summary>
		/// The identifier.
		/// </summary>
		public readonly uint Id;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueId"/> struct.
		/// </summary>
		/// <param name="id">The unique identifier.</param>
		public UniqueId (uint id)
		{
			Id = id;
		}

		#region IComparable implementation

		/// <summary>
		/// Compares two <see cref="UniqueId"/> objects.
		/// </summary>
		/// <returns>
		/// A value less than <c>0</c> if this <see cref="UniqueId"/> is less than <paramref name="other"/>,
		/// a value of <c>0</c> if this <see cref="UniqueId"/> is equal to <paramref name="other"/>, or
		/// a value greater than <c>0</c> if this <see cref="UniqueId"/> is greater than <paramref name="other"/>.
		/// </returns>
		/// <param name="other">The other unique identifier.</param>
		public int CompareTo (UniqueId other)
		{
			return Id.CompareTo (other.Id);
		}

		#endregion

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="MailKit.UniqueId"/> is equal to the current <see cref="MailKit.UniqueId"/>.
		/// </summary>
		/// <param name="other">The <see cref="MailKit.UniqueId"/> to compare with the current <see cref="MailKit.UniqueId"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="MailKit.UniqueId"/> is equal to the current
		/// <see cref="MailKit.UniqueId"/>; otherwise, <c>false</c>.</returns>
		public bool Equals (UniqueId other)
		{
			return other.Id == Id;
		}

		#endregion

		/// <summary>
		/// Determines whether two unique identifiers are equal.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> and <paramref name="uid2"/> are equal; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator == (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id == uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is greater than another unique identifier.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> is greater than <paramref name="uid2"/>; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator > (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id > uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is greater than or equal to another unique identifier.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> is greater than or equal to <paramref name="uid2"/>; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator >= (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id >= uid2.Id;
		}

		/// <summary>
		/// Determines whether two unique identifiers are not equal.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> and <paramref name="uid2"/> are not equal; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator != (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id != uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is less than another unique identifier.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> is less than <paramref name="uid2"/>; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator < (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id < uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is less than or equal to another unique identifier.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="uid1"/> is less than or equal to <paramref name="uid2"/>; otherwise, <c>false</c>.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator <= (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id <= uid2.Id;
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.UniqueId"/>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.UniqueId"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.UniqueId"/>;
		/// otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return obj is UniqueId && ((UniqueId) obj).Id == Id;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.UniqueId"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Id.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.UniqueId"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.UniqueId"/>.</returns>
		public override string ToString ()
		{
			return Id.ToString ();
		}
	}
}
