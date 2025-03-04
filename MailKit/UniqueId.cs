﻿//
// UniqueId.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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
	/// A unique identifier.
	/// </summary>
	/// <remarks>
	/// <para>Represents a unique identifier for messages in a <see cref="IMailFolder"/>.</para>
	/// <para>A 32-bit value assigned to each message, which when used with the
	/// unique identifier validity value (see below) forms a 64-bit value
	/// that MUST NOT refer to any other message in the mailbox or any
	/// subsequent mailbox with the same name forever.Unique identifiers
	/// are assigned in a strictly ascending fashion in the mailbox; as each
	/// message is added to the mailbox it is assigned a higher UID than the
	/// message(s) which were added previously. Unlike message sequence
	/// numbers, unique identifiers are not necessarily contiguous.</para>
	/// <para>The unique identifier of a message MUST NOT change during the
	/// session, and SHOULD NOT change between sessions. Any change of
	/// unique identifiers between sessions MUST be detectable using the
	/// UIDVALIDITY mechanism discussed below. Persistent unique identifiers
	/// are required for a client to resynchronize its state from a previous
	/// session with the server (e.g., disconnected or offline access
	/// clients); this is discussed further in
	/// [<a href="https://datatracker.ietf.org/doc/html/rfc3501#ref-IMAP-DISC">IMAP-DISC</a>].</para>
	/// <note type="note">For more information about unique identifiers, see
	/// <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-2.3.1.1">RFC 3501, section 2.3.1.1</a>.</note>
	/// </remarks>
	public readonly struct UniqueId : IComparable<UniqueId>, IEquatable<UniqueId>
	{
		/// <summary>
		/// The invalid <see cref="UniqueId"/> value.
		/// </summary>
		/// <remarks>
		/// The invalid <see cref="UniqueId"/> value.
		/// </remarks>
		public static readonly UniqueId Invalid;

		/// <summary>
		/// The minimum <see cref="UniqueId"/> value.
		/// </summary>
		/// <remarks>
		/// The minimum <see cref="UniqueId"/> value.
		/// </remarks>
		public static readonly UniqueId MinValue = new UniqueId (1);

		/// <summary>
		/// The maximum <see cref="UniqueId"/> value.
		/// </summary>
		/// <remarks>
		/// The maximum <see cref="UniqueId"/> value.
		/// </remarks>
		public static readonly UniqueId MaxValue = new UniqueId (uint.MaxValue);

		readonly uint validity;
		readonly uint id;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueId"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UniqueId"/> with the specified validity and value.
		/// </remarks>
		/// <param name="validity">The uid validity.</param>
		/// <param name="id">The unique identifier.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="id"/> is <c>0</c>.
		/// </exception>
		public UniqueId (uint validity, uint id)
		{
			if (id == 0)
				throw new ArgumentOutOfRangeException (nameof (id));

			this.validity = validity;
			this.id = id;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueId"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="UniqueId"/> with the specified value.
		/// </remarks>
		/// <param name="id">The unique identifier.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="id"/> is <c>0</c>.
		/// </exception>
		public UniqueId (uint id)
		{
			if (id == 0)
				throw new ArgumentOutOfRangeException (nameof (id));

			this.validity = 0;
			this.id = id;
		}

		/// <summary>
		/// Gets the identifier.
		/// </summary>
		/// <remarks>
		/// The identifier.
		/// </remarks>
		/// <value>The identifier.</value>
		public uint Id {
			get { return id; }
		}

		/// <summary>
		/// Gets the validity, if non-zero.
		/// </summary>
		/// <remarks>
		/// Gets the UidValidity of the containing folder.
		/// </remarks>
		/// <value>The UidValidity of the containing folder or <c>0</c> if not known.</value>
		public uint Validity {
			get { return validity; }
		}

		/// <summary>
		/// Gets whether or not the unique identifier is valid.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the unique identifier is valid.
		/// </remarks>
		/// <value><see langword="true" /> if the unique identifier is valid; otherwise, <see langword="false" />.</value>
		public bool IsValid {
			get { return Id != 0; }
		}

		#region IComparable implementation

		/// <summary>
		/// Compares two <see cref="UniqueId"/> objects.
		/// </summary>
		/// <remarks>
		/// <para>Compares two <see cref="UniqueId"/> objects.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
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
		/// <remarks>
		/// <para>Determines whether the specified <see cref="MailKit.UniqueId"/> is equal to the current <see cref="MailKit.UniqueId"/>.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <param name="other">The <see cref="MailKit.UniqueId"/> to compare with the current <see cref="MailKit.UniqueId"/>.</param>
		/// <returns><see langword="true" /> if the specified <see cref="MailKit.UniqueId"/> is equal to the current
		/// <see cref="MailKit.UniqueId"/>; otherwise, <see langword="false" />.</returns>
		public bool Equals (UniqueId other)
		{
			return other.Id == Id;
		}

		#endregion

		/// <summary>
		/// Determines whether two unique identifiers are equal.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether two unique identifiers are equal.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> and <paramref name="uid2"/> are equal; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator == (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id == uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is greater than another unique identifier.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether one unique identifier is greater than another unique identifier.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> is greater than <paramref name="uid2"/>; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator > (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id > uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is greater than or equal to another unique identifier.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether one unique identifier is greater than or equal to another unique identifier.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> is greater than or equal to <paramref name="uid2"/>; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator >= (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id >= uid2.Id;
		}

		/// <summary>
		/// Determines whether two unique identifiers are not equal.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether two unique identifiers are not equal.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> and <paramref name="uid2"/> are not equal; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator != (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id != uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is less than another unique identifier.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether one unique identifier is less than another unique identifier.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> is less than <paramref name="uid2"/>; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator < (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id < uid2.Id;
		}

		/// <summary>
		/// Determines whether one unique identifier is less than or equal to another unique identifier.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether one unique identifier is less than or equal to another unique identifier.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <returns><see langword="true" /> if <paramref name="uid1"/> is less than or equal to <paramref name="uid2"/>; otherwise, <see langword="false" />.</returns>
		/// <param name="uid1">The first unique id to compare.</param>
		/// <param name="uid2">The second unique id to compare.</param>
		public static bool operator <= (UniqueId uid1, UniqueId uid2)
		{
			return uid1.Id <= uid2.Id;
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.UniqueId"/>.
		/// </summary>
		/// <remarks>
		/// <para>Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.UniqueId"/>.</para>
		/// <note type="note">Validity values are not used in the comparison.</note>
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.UniqueId"/>.</param>
		/// <returns><see langword="true" /> if the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.UniqueId"/>;
		/// otherwise, <see langword="false" />.</returns>
		public override bool Equals (object obj)
		{
			return obj is UniqueId uid && uid.Id == Id;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.UniqueId"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.UniqueId"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Id.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.UniqueId"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.UniqueId"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.UniqueId"/>.</returns>
		public override string ToString ()
		{
			return Id.ToString (CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Attempt to parse a unique identifier.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a unique identifier.
		/// </remarks>
		/// <returns><see langword="true" /> if the unique identifier was successfully parsed; otherwise, <c>false.</c>.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="index">The index to start parsing.</param>
		/// <param name="uid">The unique identifier.</param>
		internal static bool TryParse (string token, ref int index, out uint uid)
		{
			uint value = 0;

			while (index < token.Length) {
				char c = token[index];
				uint v;

				if (c < '0' || c > '9')
					break;

				v = (uint) (c - '0');

				if (value > uint.MaxValue / 10 || (value == uint.MaxValue / 10 && v > uint.MaxValue % 10)) {
					uid = 0;
					return false;
				}

				value = (value * 10) + v;
				index++;
			}

			uid = value;

			return uid != 0;
		}

		/// <summary>
		/// Attempt to parse a unique identifier.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a unique identifier.
		/// </remarks>
		/// <returns><see langword="true" /> if the unique identifier was successfully parsed; otherwise, <c>false.</c>.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="validity">The UIDVALIDITY value.</param>
		/// <param name="uid">The unique identifier.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <see langword="null" />.
		/// </exception>
		public static bool TryParse (string token, uint validity, out UniqueId uid)
		{
			if (token == null)
				throw new ArgumentNullException (nameof (token));

			if (!uint.TryParse (token, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0) {
				uid = Invalid;
				return false;
			}

			uid = new UniqueId (validity, id);

			return true;
		}

		/// <summary>
		/// Attempt to parse a unique identifier.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a unique identifier.
		/// </remarks>
		/// <returns><see langword="true" /> if the unique identifier was successfully parsed; otherwise, <c>false.</c>.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="uid">The unique identifier.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <see langword="null" />.
		/// </exception>
		public static bool TryParse (string token, out UniqueId uid)
		{
			return TryParse (token, 0, out uid);
		}

		/// <summary>
		/// Parse a unique identifier.
		/// </summary>
		/// <remarks>
		/// Parses a unique identifier.
		/// </remarks>
		/// <returns>The unique identifier.</returns>
		/// <param name="token">A string containing the unique identifier.</param>
		/// <param name="validity">The UIDVALIDITY.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.FormatException">
		/// <paramref name="token"/> is not in the correct format.
		/// </exception>
		/// <exception cref="System.OverflowException">
		/// The unique identifier is greater than <see cref="MaxValue"/>.
		/// </exception>
		public static UniqueId Parse (string token, uint validity)
		{
			return new UniqueId (validity, uint.Parse (token, NumberStyles.None, CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Parse a unique identifier.
		/// </summary>
		/// <remarks>
		/// Parses a unique identifier.
		/// </remarks>
		/// <returns>The unique identifier.</returns>
		/// <param name="token">A string containing the unique identifier.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.FormatException">
		/// <paramref name="token"/> is not in the correct format.
		/// </exception>
		/// <exception cref="System.OverflowException">
		/// The unique identifier is greater than <see cref="MaxValue"/>.
		/// </exception>
		public static UniqueId Parse (string token)
		{
			return new UniqueId (uint.Parse (token, NumberStyles.None, CultureInfo.InvariantCulture));
		}
	}
}
